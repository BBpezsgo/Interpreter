@echo off
dotnet publish BBCodeInterpreter.csproj -c Release -r win-x64 -p:PublishAot=true -p:DefineConstants=AOT >publish-win64-aot.txt