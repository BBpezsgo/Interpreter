@echo off

echo Publishing for win-x64 ...
dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-x64   --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-win-x64.txt
rem echo Publishing for win-x86 ...
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-x86   --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-win-x86.txt
rem echo Publishing for win-arm64 ...
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-arm64 --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-win-arm64.txt

echo Publishing for win-x64 (AOT) ...
dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-x64   -p:PublishAot=true -p:DefineConstants=AOT
rem echo Publishing for win-x86 (AOT) ...
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-x86   -p:PublishAot=true -p:DefineConstants=AOT
rem echo Publishing for win-arm64 (AOT) ...
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-arm64 -p:PublishAot=true -p:DefineConstants=AOT

rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime linux-x64  --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-linux-x64.txt
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime linux-arm  --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-linux-arm.txt
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime linux-arm64 --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-linux-arm64.txt

echo Done
