(Get-Content "./Interpreter/BBCodeInterpreter.csproj") -replace 'C:\\Users\\bazsi\\source\\repos', '..' | Set-Content "./Interpreter/BBCodeInterpreter.csproj"

if (Test-Path -Path ./Published)
{
    Remove-Item -LiteralPath ./Published -Force -Recurse
}

if (Test-Path -Path ./Interpreter/post-build.bat)
{
    Set-Content -Path "./Interpreter/post-build.bat" -Value ""
}

dotnet publish ./Interpreter/BBCodeInterpreter.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained true   -p:PublishTrimmed=true  -p:PublishSingleFile=true   --output ./Published/Windows_x64_RuntimeIndependent
dotnet publish ./Interpreter/BBCodeInterpreter.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained false  -p:PublishTrimmed=false -p:PublishSingleFile=false  --output ./Published/Windows_x64_RuntimeDependent
dotnet publish ./Interpreter/BBCodeInterpreter.csproj --configuration Release --runtime win-x64  -p:PublishAot=true    --self-contained false  -p:PublishTrimmed=      -p:PublishSingleFile=false  --output ./Published/Windows_x64_AOT -p:DefineConstants=AOT