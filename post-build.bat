@echo off

echo == Post-build started ==

echo   D:\Program Files\BBCodeProject\BBCode\bin\ -^> C:\Users\bazsi\Documents\GitHub\InterpreterVSCodeExtension\interpreter\
robocopy "D:\Program Files\BBCodeProject\BBCode\bin\ " "C:\Users\bazsi\Documents\GitHub\InterpreterVSCodeExtension\interpreter\ " /E /njh /njs /ns /nc /ndl /np /nfl

echo == Post-build done ==

exit 0