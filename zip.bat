@echo off

echo Compressing Debug.zip
cd bin/Debug/net8.0
tar.exe -a -cf ../../../out/Debug.zip *
cd ../../..

echo Compressing Release.zip
cd bin/Release/net8.0
tar.exe -a -cf ../../../out/Release.zip *
cd ../../..

copy .\out\Release.zip .\out\BBCodeInterpreter.zip

echo Done