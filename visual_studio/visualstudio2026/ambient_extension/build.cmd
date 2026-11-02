@echo off
setlocal
set "AMBIENT_EXTENSION_PROJECT=%~dp0AmbientExtension.csproj"
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'; $install = & $vswhere -latest -version '[18.0,19.0)' -requires Microsoft.Component.MSBuild -property installationPath; if (!$install) { throw 'Visual Studio 2026 with MSBuild was not found.' }; & (Join-Path $install 'MSBuild\Current\Bin\MSBuild.exe') $env:AMBIENT_EXTENSION_PROJECT /restore /p:Configuration=Release /m /v:minimal /nologo; exit $LASTEXITCODE"
exit /b %errorlevel%
