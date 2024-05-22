(Get-Content "./Core/Core.csproj") -replace '\.\.\\\.\.', '..' | Set-Content "./Core/Core.csproj"

(Get-Content "./LanguageServer/LanguageServer.csproj") -replace '<BaseOutputPath>C:\\Users\\bazsi\\Projects\\BBLang\\VSCodeExtension\\language-server</BaseOutputPath>', '' | Set-Content "./LanguageServer/LanguageServer.csproj"

if (Test-Path -Path ./Published)
{
    Remove-Item -LiteralPath ./Published -Force -Recurse
}

dotnet publish ./Core/Core.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained true   -p:PublishTrimmed=true  -p:PublishSingleFile=true   --output ./Published/Windows_x64_RuntimeIndependent
dotnet publish ./Core/Core.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained false  -p:PublishTrimmed=false -p:PublishSingleFile=false  --output ./Published/Windows_x64_RuntimeDependent
dotnet publish ./Core/Core.csproj --configuration Release --runtime win-x64  -p:PublishAot=true    --self-contained false  -p:PublishTrimmed=      -p:PublishSingleFile=false  --output ./Published/Windows_x64_AOT -p:DefineConstants=AOT

dotnet publish ./LanguageServer/LanguageServer.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained true   -p:PublishTrimmed=false  -p:PublishSingleFile=false   --output ./Published/LanguageServer_Windows_x64_RuntimeIndependent
dotnet publish ./LanguageServer/LanguageServer.csproj --configuration Release --runtime win-x64  -p:PublishAot=false   --self-contained false  -p:PublishTrimmed=false -p:PublishSingleFile=false  --output ./Published/LanguageServer_Windows_x64_RuntimeDependent
