@echo off
dotnet publish BBCodeInterpreter.csproj -c Release -r win-x64 -p:PublishAot=true -p:DefineConstants=AOT
timeout /t 5
