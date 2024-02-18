(Get-Content "./Interpreter/BBCodeInterpreter.csproj") -replace 'C:\\Users\\bazsi\\source\\repos', '..' | Set-Content "./Interpreter/BBCodeInterpreter.csproj"

(Get-Content "./LanguageServer/BBCodeLanguageServer.csproj") -replace '<BaseOutputPath>C:\\Users\\bazsi\\Documents\\GitHub\\InterpreterVSCodeExtension\\out\\language-server</BaseOutputPath>', '' | Set-Content "./LanguageServer/BBCodeLanguageServer.csproj"

(Get-Content "./LanguageServer/BBCodeLanguageServer.csproj") -replace '..\\..\\BBCode\\BBCodeInterpreter.csproj', '..\Interpreter\\BBCodeInterpreter.csproj' | Set-Content "./LanguageServer/BBCodeLanguageServer.csproj"

if (Test-Path -Path ./Published)
{
    Remove-Item -LiteralPath ./Published -Force -Recurse
}

dotnet publish ./Interpreter/BBCodeInterpreter.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained true   -p:PublishTrimmed=true  -p:PublishSingleFile=true   --output ./Published/Windows_x64_RuntimeIndependent
dotnet publish ./Interpreter/BBCodeInterpreter.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained false  -p:PublishTrimmed=false -p:PublishSingleFile=false  --output ./Published/Windows_x64_RuntimeDependent
dotnet publish ./Interpreter/BBCodeInterpreter.csproj --configuration Release --runtime win-x64  -p:PublishAot=true    --self-contained false  -p:PublishTrimmed=      -p:PublishSingleFile=false  --output ./Published/Windows_x64_AOT -p:DefineConstants=AOT

dotnet publish ./LanguageServer/BBCodeLanguageServer.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained true   -p:PublishTrimmed=false  -p:PublishSingleFile=false   --output ./Published/LanguageServer_Windows_x64_RuntimeIndependent
dotnet publish ./LanguageServer/BBCodeLanguageServer.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained false  -p:PublishTrimmed=false -p:PublishSingleFile=false  --output ./Published/LanguageServer_Windows_x64_RuntimeDependent
