using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace AmbientExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Ambient Extension", "Synchronizes Visual Studio themes with Ambient", "1.0")]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [Guid("8B3FD454-4266-4CD9-9972-D31D38BAA4C5")]
    public sealed class AmbientPackage : AsyncPackage
    {
        private AmbientSynchronizer? synchronizer;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            try
            {
                synchronizer = new AmbientSynchronizer(this, new ThemeService());
                synchronizer.Start();
            }
            catch (Exception exception)
            {
                synchronizer?.Dispose();
                synchronizer = null;
                ActivityLog.LogError("Ambient Extension", exception.ToString());
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { synchronizer?.Dispose(); synchronizer = null; }
            base.Dispose(disposing);
        }
    }
}
