if (Test-Path -Path ./DataUtilities) { } else
{
    mkdir ./DataUtilities
    git clone https://github.com/BBpezsgo/DataUtilities.git ./DataUtilities
}

if (Test-Path -Path ./Win32-Stuff) { } else
{
    mkdir ./Win32-Stuff
    git clone https://github.com/BBpezsgo/Win32-Stuff.git ./Win32-Stuff
}

if (Test-Path -Path ./Interpreter) { } else
{
    mkdir ./Interpreter
    git clone https://github.com/BBpezsgo/Interpreter.git ./Interpreter
}
