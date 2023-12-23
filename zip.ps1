if (Test-Path -Path ./Published/JIT) {
    Compress-Archive -Path ./Published/JIT -DestinationPath ./Published/JIT.zip
}

if (Test-Path -Path ./Published/AOT) {
    Compress-Archive -Path ./Published/AOT -DestinationPath ./Published/AOT.zip
}
