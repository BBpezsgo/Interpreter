if (Test-Path -Path ./Published/Windows_x64_RuntimeIndependent)
{
    Compress-Archive -Path ./Published/Windows_x64_RuntimeIndependent -DestinationPath ./Windows_x64_RuntimeIndependent.zip
}

if (Test-Path -Path ./Published/Windows_x64_RuntimeDependent)
{
    Compress-Archive -Path ./Published/Windows_x64_RuntimeDependent -DestinationPath ./Windows_x64_RuntimeDependent.zip
}

if (Test-Path -Path ./Published/Windows_x64_AOT)
{
    Compress-Archive -Path ./Published/Windows_x64_AOT -DestinationPath ./Windows_x64_AOT.zip
}
