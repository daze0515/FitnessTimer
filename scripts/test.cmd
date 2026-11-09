@echo off
setlocal
set "DOTNET_CLI_HOME=%~dp0..\.dotnet-home"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
dotnet test "%~dp0..\FitnessTimer.sln"
