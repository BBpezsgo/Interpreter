@echo off
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-win64.txt