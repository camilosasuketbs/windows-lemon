using System;

namespace AmbientExtension.Core
{
    internal sealed class PendingAssociation
    {
        internal readonly string Ambience;
        internal readonly Guid Theme;
        internal PendingAssociation(string ambience, Guid theme) { Ambience = ambience; Theme = theme; }
    }

    internal sealed class StateSnapshot
    {
        internal readonly long Revision;
        internal readonly PendingAssociation? Pending;
        internal StateSnapshot(long revision, PendingAssociation? pending) { Revision = revision; Pending = pending; }
        internal Guid? ManualThemeFor(string ambience) => Pending != null &&
            string.Equals(Pending.Ambience, ambience, StringComparison.OrdinalIgnoreCase) ? Pending.Theme : (Guid?)null;
    }

    internal sealed class SynchronizationState
    {
        private readonly object gate = new object();
        private string? activeAmbience;
        private Guid lastTheme;
        private Guid? expectedAutomaticTheme;
        private bool applying;
        private long revision;
        private PendingAssociation? pending;

        internal StateSnapshot Snapshot() { lock (gate) return new StateSnapshot(revision, pending); }

        internal bool ObserveTheme(Guid theme)
        {
            lock (gate)
            {
                if (theme == Guid.Empty || applying || theme == lastTheme) return false;
                lastTheme = theme;
                if (theme == expectedAutomaticTheme) { expectedAutomaticTheme = null; return false; }
                expectedAutomaticTheme = null;
                if (activeAmbience != null) pending = new PendingAssociation(activeAmbience, theme);
                ++revision;
                return true;
            }
        }

        internal bool Prepare(StateSnapshot snapshot, string ambience, Guid currentTheme)
        {
            lock (gate)
            {
                if (revision != snapshot.Revision) return false;
                // Establish the file's current ambience before writing the initial association.
                // A manual theme event during that write can then be attributed and retained.
                activeAmbience = ambience;
                lastTheme = currentTheme;
                return true;
            }
        }

        internal bool Commit(StateSnapshot snapshot, string ambience, Guid currentTheme, Guid desiredTheme, Action<Guid> apply)
        {
            lock (gate)
            {
                // A user change that arrived during file I/O takes priority over this older scan.
                if (revision != snapshot.Revision) return false;
                activeAmbience = ambience;
                lastTheme = currentTheme;
                if (ReferenceEquals(pending, snapshot.Pending)) pending = null;
                if (desiredTheme == Guid.Empty || desiredTheme == currentTheme) return true;
                expectedAutomaticTheme = desiredTheme;
                applying = true;
                try { apply(desiredTheme); lastTheme = desiredTheme; }
                finally { applying = false; }
                return true;
            }
        }
    }
}
