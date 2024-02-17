if (Test-Path -Path ./Interpreter)
{
    Remove-Item -LiteralPath ./Interpreter -Force -Recurse
}

if (Test-Path -Path ./DataUtilities)
{
    Remove-Item -LiteralPath ./DataUtilities -Force -Recurse
}

if (Test-Path -Path ./Win32-Stuff)
{
    Remove-Item -LiteralPath ./Win32-Stuff -Force -Recurse
}

if (Test-Path -Path ./LanguageServer)
{
    Remove-Item -LiteralPath ./LanguageServer -Force -Recurse
}
