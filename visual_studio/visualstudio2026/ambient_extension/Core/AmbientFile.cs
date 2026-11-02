using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AmbientExtension.Core
{
    internal static class AmbientFile
    {
        // File changes can precede the end of a truncate/write or an atomic replacement.
        internal static string? ReadStable(string path)
        {
            string? previous = null;
            for (int attempt = 0; attempt < 5; ++attempt)
            {
                try
                {
                    string? current = ReadOnce(path);
                    if (current != null && current == previous) return current;
                    previous = current;
                }
                catch (IOException) { previous = null; }
                catch (UnauthorizedAccessException) { previous = null; }
                if (attempt < 4) Thread.Sleep(75);
            }
            return null;
        }

        private static string? ReadOnce(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                var buffer = new char[1024];
                int length = reader.ReadBlock(buffer, 0, buffer.Length);
                if (length == buffer.Length) return null;
                string value = new string(buffer, 0, length).Trim();
                return value.Length == 0 || value.Length > 256 || value.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0
                    ? null : value;
            }
        }
    }
}
