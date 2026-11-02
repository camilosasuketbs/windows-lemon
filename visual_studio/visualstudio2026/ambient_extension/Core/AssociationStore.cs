using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace AmbientExtension.Core
{
    internal sealed class AssociationStore
    {
        private readonly string path;
        private readonly string mutexName;

        internal AssociationStore(string path)
        {
            this.path = Path.GetFullPath(path);
            using (var hash = SHA256.Create())
                mutexName = "Local\\AmbientExtension.ThemeAssociations." +
                    BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(this.path.ToUpperInvariant()))).Replace("-", "");
        }

        // Under the same cross-process lock: merge manual changes, or seed only a missing key.
        // Two newly started instances must not overwrite each other's established associations.
        internal Guid Resolve(string ambience, Guid currentTheme, Guid? manualTheme)
        {
            using (var mutex = new Mutex(false, mutexName))
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(5)); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new TimeoutException("Another instance is busy saving Ambient theme associations.");
                    var associations = Load();
                    Guid selected;
                    bool changed = false;
                    if (manualTheme.HasValue && manualTheme.Value != Guid.Empty)
                    {
                        selected = manualTheme.Value;
                        changed = !associations.TryGetValue(ambience, out Guid old) || old != selected;
                    }
                    else if (!associations.TryGetValue(ambience, out selected))
                    {
                        selected = currentTheme;
                        changed = selected != Guid.Empty;
                    }
                    if (changed)
                    {
                        associations[ambience] = selected;
                        Save(associations);
                    }
                    return selected;
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }

        private Dictionary<string, Guid> Load()
        {
            var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return result;
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("The theme association file is too large.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var reader = XmlReader.Create(path, settings))
            {
                var root = XDocument.Load(reader).Root;
                if (root == null || root.Name != "ambientThemes" || (string?)root.Attribute("version") != "1")
                    throw new InvalidDataException("Unknown Ambient theme association format; the file was kept unchanged.");
                foreach (var item in root.Elements("association"))
                {
                    string? ambience = (string?)item.Attribute("ambience");
                    if (string.IsNullOrWhiteSpace(ambience) || !Guid.TryParse((string?)item.Attribute("theme"), out Guid theme)
                        || theme == Guid.Empty || result.ContainsKey(ambience!))
                        throw new InvalidDataException("Invalid or duplicate Ambient theme association; the file was kept unchanged.");
                    result.Add(ambience!, theme);
                }
            }
            return result;
        }

        private void Save(Dictionary<string, Guid> associations)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var document = new XDocument(new XElement("ambientThemes", new XAttribute("version", "1"),
                    associations.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair =>
                        new XElement("association", new XAttribute("ambience", pair.Key), new XAttribute("theme", pair.Value)))));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    document.Save(stream);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
