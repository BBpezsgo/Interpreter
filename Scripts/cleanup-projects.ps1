if (Test-Path -Path ./Core)
{
    Remove-Item -LiteralPath ./Core -Force -Recurse
}

if (Test-Path -Path ./Win32-Stuff)
{
    Remove-Item -LiteralPath ./Win32-Stuff -Force -Recurse
}

if (Test-Path -Path ./LanguageServer)
{
    Remove-Item -LiteralPath ./LanguageServer -Force -Recurse
}
