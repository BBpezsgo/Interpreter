@echo off





rem +-------------------------+
rem |      win-x64 (JIT)      +
rem +-------------------------+
echo Publishing for win-x64 ...
dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-x64   --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link
echo Compressing ...
cd bin/Release/net8.0/win-x64/publish
tar.exe -a -cf ../BBC_win64_jit.zip *
cd ../../../../../
echo Uploading ...
call upload.bat "D:/Program Files/BBCodeProject/BBCode/bin/Release/net8.0/win-x64/BBC_win64_jit.zip"
cd "D:/Program Files/BBCodeProject/BBCode/"
echo Cleaning ...
del "D:/Program Files/BBCodeProject/BBCode/bin/Release/net8.0/win-x64/BBC_win64_jit.zip"
echo Ok





rem +-------------------------+
rem |      win-x86 (JIT)      +
rem +-------------------------+
echo Publishing for win-x86 ...
dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-x86   --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link
echo Compressing ...
cd bin/Release/net8.0/win-x86/publish
tar.exe -a -cf ../BBC_win86_jit.zip *
cd ../../../../../
echo Uploading ...
call upload.bat "D:/Program Files/BBCodeProject/BBCode/bin/Release/net8.0/win-x86/BBC_win86_jit.zip"
cd "D:/Program Files/BBCodeProject/BBCode/"
echo Cleaning ...
del "D:/Program Files/BBCodeProject/BBCode/bin/Release/net8.0/win-x86/BBC_win86_jit.zip"
echo Ok





rem +-------------------------+
rem |     win-arm64 (JIT)     +
rem +-------------------------+
rem echo Publishing for win-arm64 ...
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-arm64 --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-win-arm64.txt





rem +-------------------------+
rem |      win-x64 (AOT)      +
rem +-------------------------+
echo Publishing for win-x64 (AOT) ...
dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-x64   -p:PublishAot=true -p:DefineConstants=AOT
echo Compressing ...
cd bin/Release/net8.0/win-x64/publish
tar.exe -a -cf ../BBC_win64_aot.zip *
cd ../../../../../
echo Uploading ...
call upload.bat "D:/Program Files/BBCodeProject/BBCode/bin/Release/net8.0/win-x64/BBC_win64_aot.zip"
cd "D:/Program Files/BBCodeProject/BBCode/"
echo Cleaning ...
del "D:/Program Files/BBCodeProject/BBCode/bin/Release/net8.0/win-x64/BBC_win64_aot.zip"
echo Ok





rem +-------------------------+
rem |      win-x86 (AOT)      +
rem +-------------------------+
echo Publishing for win-x86 (AOT) ...
dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-x86   -p:PublishAot=true -p:DefineConstants=AOT
echo Compressing ...
cd bin/Release/net8.0/win-x86/publish
tar.exe -a -cf ../BBC_win86_aot.zip *
cd ../../../../../
echo Uploading ...
call upload.bat "D:/Program Files/BBCodeProject/BBCode/bin/Release/net8.0/win-x86/BBC_win86_aot.zip"
cd "D:/Program Files/BBCodeProject/BBCode/"
echo Cleaning ...
del "D:/Program Files/BBCodeProject/BBCode/bin/Release/net8.0/win-x86/BBC_win86_aot.zip"
echo Ok





rem +-------------------------+
rem |     win-arm64 (AOT)     +
rem +-------------------------+
rem echo Publishing for win-arm64 (AOT) ...
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime win-arm64 -p:PublishAot=true -p:DefineConstants=AOT





rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime linux-x64  --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-linux-x64.txt
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime linux-arm  --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-linux-arm.txt
rem dotnet publish BBCodeInterpreter.csproj --verbosity minimal --configuration Release --runtime linux-arm64 --self-contained true -p:PublishTrimmed=true -p:TrimMode=Link >publish-linux-arm64.txt

echo Done
