@echo off
cd TestFiles
for /l %%x in (1, 1, 100) do (
    if exist .\test%%x.bbc (
        rem exists
    ) else (
        echo Create new test test%%x.bbc
        echo using System; > .\test%%x.bbc
        echo:
        goto :eol
    )
)
:eol
pause