@echo off

robocopy "D:\Program Files\BBCodeProject\BBCode\bin\ " "C:\Users\bazsi\Documents\GitHub\InterpreterVSCodeExtension\interpreter\ " /E

if %ERRORLEVEL% EQU 1 ( 
	exit 0
)