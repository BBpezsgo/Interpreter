@echo off
set source="bin\debug\net6.0\*"
copy %source% ..\Debugger\
echo Copyed to "..\Debugger\"
copy %source% C:\Users\bazsi\Documents\GitHub\InterpreterVSCodeExtension\
echo Copyed to "C:\Users\bazsi\Documents\GitHub\InterpreterVSCodeExtension\"