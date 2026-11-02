using System;
using Microsoft.Internal.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace AmbientExtension
{
    // These COM interfaces are shipped by VS2026, but live in an internal SDK namespace.
    // Keep version-dependent theme access here; the synchronization logic has no SDK dependency.
    internal sealed class ThemeService
    {
        private readonly IVsColorThemeService service;

        internal ThemeService()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            service = Package.GetGlobalService(typeof(SVsColorThemeService)) as IVsColorThemeService
                ?? throw new NotSupportedException("Visual Studio did not expose its color theme service.");
        }

        internal Guid CurrentTheme
        {
            get
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                return service.CurrentTheme.ThemeId;
            }
        }

        internal void Apply(Guid themeId)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (CurrentTheme == themeId) return;
            var theme = service.Themes.GetThemeFromId(themeId);
            if (theme == null) throw new InvalidOperationException("The associated Visual Studio theme is not installed: " + themeId);
            // This is also the direct call used by Visual Studio's own General Options page.
            theme.Apply();
            if (CurrentTheme != themeId)
                throw new InvalidOperationException("Visual Studio did not activate the associated theme: " + themeId);
        }
    }
}
