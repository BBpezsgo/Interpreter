if (Test-Path -Path ./Win32-Stuff) { } else
{
    mkdir ./Win32-Stuff
    git clone https://github.com/BBpezsgo/Win32-Stuff.git ./Win32-Stuff
}

if (Test-Path -Path ./Core) { } else
{
    mkdir ./Core
    git clone https://github.com/BBpezsgo/Interpreter.git ./Core
}

if (Test-Path -Path ./LanguageServer) { } else
{
    mkdir ./LanguageServer
    git clone https://github.com/BBpezsgo/BBCode-LanguageServer.git ./LanguageServer
}
