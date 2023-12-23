if (Test-Path -Path ./DataUtilities) { } else {
    mkdir ./DataUtilities
    git clone https://github.com/BBpezsgo/DataUtilities.git ./DataUtilities
}

if (Test-Path -Path ./Win32-Stuff) { } else {
    mkdir ./Win32-Stuff
    git clone https://github.com/BBpezsgo/Win32-Stuff.git ./Win32-Stuff
}

if (Test-Path -Path ./Interpreter) { } else {
    mkdir ./Interpreter
    git clone https://github.com/BBpezsgo/Interpreter.git ./Interpreter
}

(Get-Content "./Interpreter/BBCodeInterpreter.csproj") -replace 'C:\\Users\\bazsi\\source\\repos', '..' | Set-Content "./Interpreter/BBCodeInterpreter.csproj"

if (Test-Path -Path ./Output) {
    Remove-Item -LiteralPath ./Output -Force -Recurse
}

if (Test-Path -Path ./Interpreter/post-build.bat) {
    Set-Content -Path "./Interpreter/post-build.bat" -Value ""
}

dotnet publish ./Interpreter/BBCodeInterpreter.csproj --configuration Release --output ./Published/JIT --self-contained true -p:PublishAot=false -p:PublishTrimmed=true -p:TrimMode=Link

# dotnet publish ./Interpreter/BBCodeInterpreter.csproj --configuration Release --output ./Published/AOT -p:PublishAot=true -p:DefineConstants=AOT
