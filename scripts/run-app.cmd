@echo off
setlocal
set "DOTNET_CLI_HOME=%~dp0..\.dotnet-home"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_CLI_USE_MSBUILD_SERVER=0"
set "MSBUILDDISABLENODEREUSE=1"
set "PROJECT_ROOT=%~dp0.."
set "APP_PROJECT=%PROJECT_ROOT%\FitnessTimer.App\FitnessTimer.App.csproj"
set "APP_EXE=%PROJECT_ROOT%\FitnessTimer.App\bin\Debug\net8.0-windows\FitnessTimer.App.exe"

dotnet build "%APP_PROJECT%" --nologo /nodeReuse:false /p:UseSharedCompilation=false
if errorlevel 1 exit /b %errorlevel%

dotnet build-server shutdown >nul 2>nul
"%APP_EXE%"
