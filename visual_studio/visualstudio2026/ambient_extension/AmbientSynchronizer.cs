using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AmbientExtension.Core;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace AmbientExtension
{
    internal sealed class AmbientSynchronizer : IDisposable
    {
        private readonly AsyncPackage package;
        private readonly ThemeService themes;
        private readonly string directory;
        private readonly string ambientPath;
        private readonly string associationsPath;
        private readonly AssociationStore store;
        private readonly SynchronizationState state = new SynchronizationState();
        private readonly SemaphoreSlim cycleGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;
        private readonly Timer timer;
        private FileSystemWatcher? watcher;
        private int disposed;
        private int requested;
        private int resetWatcher;
        private string? lastError;

        internal AmbientSynchronizer(AsyncPackage package, ThemeService themes)
        {
            this.package = package;
            this.themes = themes;
            directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "app-core", "ambient");
            ambientPath = Path.Combine(directory, "ambient.txt");
            associationsPath = Path.Combine(directory, "visual_studio_theme_associations.xml");
            store = new AssociationStore(associationsPath);
            cancellationToken = shutdown.Token;
            timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        internal void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            VSColorTheme.ThemeChanged += OnThemeChanged;
            Request(0);
        }

        private void Request(int delay = 200)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            Interlocked.Exchange(ref requested, 1);
            try { timer.Change(delay, 5000); }
            catch (ObjectDisposedException) { }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs args)
        {
            if (string.Equals(args.Name, "ambient.txt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args.Name, Path.GetFileName(associationsPath), StringComparison.OrdinalIgnoreCase)) Request();
        }

        private void OnFileRenamed(object sender, RenamedEventArgs args)
        {
            OnFileChanged(sender, args);
            if (string.Equals(args.OldName, "ambient.txt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(args.OldName, Path.GetFileName(associationsPath), StringComparison.OrdinalIgnoreCase)) Request();
        }

        private void OnWatcherError(object sender, ErrorEventArgs args)
        {
            Interlocked.Exchange(ref resetWatcher, 1);
            LogError(args.GetException());
            // A periodic reconciliation also handles missed events and directory replacement.
            Request();
        }

        private void OnThemeChanged(ThemeChangedEventArgs args)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            package.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                    if (state.ObserveTheme(themes.CurrentTheme)) Request();
                }
                catch (OperationCanceledException) { }
                catch (Exception exception) { LogError(exception); }
            }).FileAndForget("AmbientExtension/ThemeChanged");
        }

        private void EnsureWatcher()
        {
            if (Volatile.Read(ref disposed) != 0) return;
            bool reset = Interlocked.Exchange(ref resetWatcher, 0) != 0;
            if (watcher != null && Directory.Exists(directory) && !reset) return;
            Interlocked.Exchange(ref watcher, null)?.Dispose();
            Directory.CreateDirectory(directory);
            var created = new FileSystemWatcher(directory)
            {
                Filter = "*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            created.Changed += OnFileChanged;
            created.Created += OnFileChanged;
            created.Deleted += OnFileChanged;
            created.Renamed += OnFileRenamed;
            created.Error += OnWatcherError;
            created.EnableRaisingEvents = true;
            if (Volatile.Read(ref disposed) != 0) created.Dispose();
            else
            {
                Interlocked.Exchange(ref watcher, created)?.Dispose();
                if (Volatile.Read(ref disposed) != 0) Interlocked.Exchange(ref watcher, null)?.Dispose();
            }
        }

        private void OnTimer(object? unused)
        {
            // All exceptions are observed inside RunCycleAsync.
            _ = RunCycleAsync();
        }

        private async Task RunCycleAsync()
        {
            if (Volatile.Read(ref disposed) != 0 || !await cycleGate.WaitAsync(0).ConfigureAwait(false)) return;
            Interlocked.Exchange(ref requested, 0);
            try
            {
                var snapshot = state.Snapshot();
                string? ambience = await Task.Run(() => { EnsureWatcher(); return AmbientFile.ReadStable(ambientPath); }, cancellationToken);
                if (ambience == null) return; // Missing/empty/partially written files never erase associations.
                await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                Guid currentTheme = themes.CurrentTheme;
                if (!state.Prepare(snapshot, ambience, currentTheme)) { Request(); return; }
                Guid? desiredTheme = await Task.Run(() =>
                {
                    // Recheck after the UI-thread hop, before saving a possible manual change.
                    if (AmbientFile.ReadStable(ambientPath) != ambience) return (Guid?)null;
                    return store.Resolve(ambience, currentTheme, snapshot.ManualThemeFor(ambience));
                }, cancellationToken);
                if (!desiredTheme.HasValue) { Request(); return; }
                await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                if (!state.Commit(snapshot, ambience, currentTheme, desiredTheme.Value, themes.Apply)) Request();
                lastError = null;
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { LogError(exception); }
            finally
            {
                cycleGate.Release();
                if (Interlocked.Exchange(ref requested, 0) != 0) Request();
            }
        }

        private void LogError(Exception exception)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            string message = exception.ToString();
            if (Interlocked.Exchange(ref lastError, message) == message) return;
            package.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await package.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                    ActivityLog.LogError("Ambient Extension", message);
                }
                catch (OperationCanceledException) { }
            }).FileAndForget("AmbientExtension/Log");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            VSColorTheme.ThemeChanged -= OnThemeChanged;
            shutdown.Cancel();
            timer.Dispose();
            Interlocked.Exchange(ref watcher, null)?.Dispose();
            shutdown.Dispose();
            // In-flight reconciliations release cycleGate themselves; disposing it here would race them.
        }
    }
}
