# Ambient Extension

A C# Visual Studio package for Visual Studio 2026 (64-bit). It uses the Visual Studio SDK's 17.x API compatibility model and .NET Framework 4.8. It changes the live theme directly; it never imports or exports `.vssettings` files.

## Behavior

- Watch `%APPDATA%\app-core\ambient\ambient.txt`. On Camilo's machine this is `C:\Users\camilo\AppData\Roaming\app-core\ambient\ambient.txt`.
- The file contains a single ambience name, such as `lite`, `dark`, or `cloudy`. Ignore transient empty, missing or partially written files.
- On startup or an ambience change, apply an existing association. If the ambience has never been associated, keep the current theme and learn it for that ambience.
- When the user chooses a different Visual Studio theme, save it for the active ambience. Changes made by this extension do not relearn/overwrite associations. Visual Studio does not label the origin of its theme events, so changes made by another extension or the system-following feature are also treated as external choices.
- All running instances with the extension share `%APPDATA%\app-core\ambient\visual_studio_theme_associations.xml`. An atomic file replacement and a named mutex protect simultaneous writes; each update merges the latest file. A manual choice in one instance is picked up by the others. If users make competing choices, the last saved manual choice wins.
- A missing/uninstalled saved theme is reported and its association is preserved. The extension does not substitute a different theme.

File notifications are debounced by 200 ms. Stable file reads tolerate truncate/write and rename replacement. A five-second reconciliation also recovers missed notifications. Visual Studio theme access always runs on the UI thread; file I/O runs on worker threads.

## Build

Install Visual Studio 2026 with the **Visual Studio extension development** workload and the .NET Framework 4.8 targeting pack. Package restore needs access to NuGet.

Open `AmbientExtension.sln`, select Release, and build, or run `build.cmd` from Command Prompt. From Git Bash, run `cmd.exe //c build.cmd`.

The VSIX is produced at `build/bin/AmbientExtension/Release/AmbientExtension.vsix`. All generated objects, restore assets, binaries and test outputs are under the ignored `build/` directory. IDE state (`.vs/`, `*.user`, `*.suo`) is ignored too.

## Install and try

1. Build, then open the generated `.vsix` file to install **Ambient Extension**. Restart the Visual Studio instances where it should run. Building alone does not install it. Each build copies the VSIX to `%USERPROFILE%\dropbox\net\public\download\visual_studio_2026\AmbientExtension.vsix`, for publication at `https://ca2.site/download/visual_studio_2026/AmbientExtension.vsix`. Override `AmbientExtensionPublishDirectory` to change that folder, or set `PublishAmbientExtension=false` to disable the copy.
2. Ambient's old Visual Studio theme automation is disabled by default with `CHANGE_VS_THEME_FROM_APP_CORE_AMBIENT=0`, so it does not compete with the extension. Get Settings offers the extension installer when it is not found. The general settings export/import buttons are separate and can still be used deliberately.
3. Publish an ambience such as `lite`, then choose the desired theme in Visual Studio's theme settings. Publish `dark`, then choose its theme. Switching back to `lite` should restore the first choice in every participating instance.
4. To use an isolated test instance, open the solution, enable VSIX deployment for Debug if needed, and launch Visual Studio with `/RootSuffix Exp /Log`. Install/deploy the VSIX into that experimental instance before testing.

To inspect failures, start `devenv /Log`, reproduce, then inspect `%APPDATA%\Microsoft\VisualStudio\18.0_*\ActivityLog.xml` (or the corresponding experimental profile). Entries use the source **Ambient Extension**.

## Tests

The dependency-free core checks require the .NET 10 SDK:

```powershell
dotnet run --project Tests/AmbientExtension.Core.Tests.csproj -c Release
```

They cover association persistence, concurrent writers, initialization across instances, manual versus automatic changes, stale scans, malformed files and failed theme application. They do not substitute for testing the installed VSIX inside Visual Studio.

## Theme API boundary

`ThemeService.cs` uses `SVsColorThemeService`, `IVsColorThemeService.CurrentTheme`, `IVsColorThemes.GetThemeFromId`, and `IVsColorTheme.Apply`. The installed VS2026 General Options page uses this same `Apply` call. Although these COM interfaces are shipped in the Visual Studio interop assembly, their namespace is `Microsoft.Internal.VisualStudio.Shell.Interop`; this is a version-sensitive boundary, isolated in one source file. The package logs an error and stays inactive if the service is unavailable. No private Visual Studio assemblies are redistributed.

The manifest follows Microsoft's VS2026 compatibility model: API version `[17.0,)` and architecture `amd64`. Its internal theme-service use has been inspected against the installed VS2026; VS2022 and future releases need runtime verification.
