using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AmbientExtension.Core;

internal static class Program
{
    private static int passed;
    private static readonly Guid Light = Guid.Parse("de3dbbcd-f642-433c-8353-8f1df4370aba");
    private static readonly Guid Dark = Guid.Parse("1ded0138-47ce-435e-84ef-9ec1f439b749");

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception("FAILED: " + description);
        ++passed;
        Console.WriteLine("PASS: " + description);
    }

    private static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--writer")
        {
            var shared = new AssociationStore(args[1]);
            for (int i = 0; i < 3; ++i) shared.Resolve(args[2] + i, Light, null);
            return 0;
        }
        string folder = Path.Combine(Path.GetTempPath(), "AmbientExtension.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "associations.xml");
            var store = new AssociationStore(path);
            Check(store.Resolve("lite", Light, null) == Light, "first ambience learns current theme");
            Check(store.Resolve("lite", Dark, null) == Light, "a second instance cannot overwrite a learned theme during initialization");
            Check(store.Resolve("lite", Light, Dark) == Dark, "manual theme choice replaces an association");
            Check(store.Resolve("LITE", Light, null) == Dark, "ambience comparison is case insensitive");
            Check(store.Resolve("cloudy & rainy", Light, null) == Light, "XML escapes ambience names");
            Parallel.For(0, 16, i => new AssociationStore(path).Resolve("ambient-" + i, Light, null));
            Check(XDocument.Load(path).Root!.Elements("association").Count() == 18, "concurrent writers merge associations without lost updates");
            Check(!Directory.GetFiles(folder, "*.tmp").Any(), "atomic saves clean their temporary files");
            var writers = Enumerable.Range(0, 4).Select(i =>
            {
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                start.ArgumentList.Add("--writer");
                start.ArgumentList.Add(path);
                start.ArgumentList.Add("process-" + i + "-");
                return Process.Start(start)!;
            }).ToArray();
            foreach (var writer in writers)
            {
                using (writer)
                {
                    if (!writer.WaitForExit(10000)) { writer.Kill(); writer.WaitForExit(); throw new Exception("Writer process timed out."); }
                    if (writer.ExitCode != 0) throw new Exception("Writer process failed.");
                }
            }
            Check(XDocument.Load(path).Root!.Elements("association").Count() == 30, "separate processes merge updates under the named mutex");

            var starting = new SynchronizationState();
            var initial = starting.Snapshot();
            Check(starting.Prepare(initial, "lite", Light), "initialization establishes the active ambience before storage I/O");
            Check(starting.ObserveTheme(Dark) && starting.Snapshot().ManualThemeFor("lite") == Dark,
                "manual theme choice during initialization remains associated with the file's ambience");
            Check(!starting.Commit(initial, "lite", Light, Light, _ => throw new Exception()),
                "initialization cannot overwrite a newer user theme choice");


            var state = new SynchronizationState();
            Check(state.Commit(state.Snapshot(), "lite", Light, Light, _ => throw new Exception()), "unchanged theme does not call Apply");
            Check(state.ObserveTheme(Dark), "manual theme change is detected");
            var manual = state.Snapshot();
            Check(manual.ManualThemeFor("lite") == Dark, "manual change belongs to active ambience");
            Check(manual.ManualThemeFor("dark") == null, "manual change cannot leak to a new ambience");
            Check(state.Commit(manual, "dark", Dark, Light, theme =>
            {
                Check(!state.ObserveTheme(theme), "synchronous automatic theme event is suppressed");
            }), "ambient transition applies saved target theme");
            Check(!state.ObserveTheme(Light), "delayed automatic theme event is suppressed");
            Check(state.Snapshot().Pending == null, "automatic changes never become manual associations");
            var stale = state.Snapshot();
            Check(state.ObserveTheme(Dark), "new manual choice is detected after automatic application");
            Check(!state.Commit(stale, "dark", Dark, Light, _ => throw new Exception()), "manual input wins over a stale scan");
            var latest = state.Snapshot();
            bool failed = false;
            try { state.Commit(latest, "other", Dark, Light, _ => throw new InvalidOperationException("missing theme")); }
            catch (InvalidOperationException) { failed = true; }
            Check(failed && state.Snapshot().Pending == null, "failed automatic application does not invent an association");
            Check(!state.ObserveTheme(Light), "late completion of an attempted automatic apply is not a manual edit");

            string ambient = Path.Combine(folder, "ambient.txt");
            Check(AmbientFile.ReadStable(ambient) == null, "missing ambient file is tolerated");
            File.WriteAllText(ambient, "  lite\r\n");
            Check(AmbientFile.ReadStable(ambient) == "lite", "ambient reader trims whitespace");
            File.WriteAllText(ambient, "");
            Check(AmbientFile.ReadStable(ambient) == null, "transient empty writes are ignored");
            File.WriteAllText(ambient, "lite\ndark");
            Check(AmbientFile.ReadStable(ambient) == null, "malformed multi-line ambience is ignored");
            File.WriteAllText(ambient, "dark");
            Check(AmbientFile.ReadStable(ambient) == "dark", "ambient reader recovers after a partial write");

            File.WriteAllText(path, "not valid XML");
            bool corruptRejected = false;
            try { store.Resolve("lite", Light, null); }
            catch (System.Xml.XmlException) { corruptRejected = true; }
            Check(corruptRejected && File.ReadAllText(path) == "not valid XML", "corrupt association file is preserved");
            File.WriteAllText(path, "<ambientThemes version=\"1\"><association ambience=\"lite\" theme=\"" + Light + "\"/><association ambience=\"LITE\" theme=\"" + Dark + "\"/></ambientThemes>");
            bool duplicateRejected = false;
            try { store.Resolve("lite", Light, null); }
            catch (InvalidDataException) { duplicateRejected = true; }
            Check(duplicateRejected, "duplicate association keys are rejected");
            Console.WriteLine(passed + " checks passed.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { Directory.Delete(folder, true); }
    }
}
