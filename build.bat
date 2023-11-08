@echo off
set DOTNET_ROOT=%USERPROFILE%\dotnet7
set PATH=%USERPROFILE%\dotnet7;%PATH%
set DOTNET_MULTILEVEL_LOOKUP=0
dotnet build BBCodeInterpreter.csproj --configuration Debug