if (Test-Path -Path ./BuildJob) { } else
{
    mkdir ./BuildJob
}

Set-Location BuildJob

Write-Output "Cloning repos ..."

..\clone-repos

Write-Output "Building ..."

..\build

Write-Output "Cleaning up the project folders ..."

..\cleanup-projects

Write-Output "Compressing ..."

..\compress

Write-Output "Done"

if (Test-Path -Path ./Published) { Remove-Item -LiteralPath ./Published -Force -Recurse }

Set-Location ..
