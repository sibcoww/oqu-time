[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $Version = "0.1.0-alpha"
)

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$') {
    throw "Version must look like 1.2.3 or 1.2.3-preview.1."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "SchoolScheduler.App\SchoolScheduler.App.csproj"
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\win-x64"
$releaseDirectory = Join-Path $repositoryRoot "artifacts\release"
$installerScript = Join-Path $PSScriptRoot "SchoolScheduler.iss"

dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
    -p:PublishProfile=win-x64 -p:PublishDir="$publishDirectory\" -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "Не удалось опубликовать приложение." }

$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw "Inno Setup 6 не найден. Установите его и повторите запуск installer\build-installer.ps1."
}

& $compiler "/DAppVersion=$Version" $installerScript
if ($LASTEXITCODE -ne 0) { throw "Не удалось собрать установщик." }

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
$portableArchive = Join-Path $releaseDirectory "SchoolScheduler-$Version-win-x64-portable.zip"
if (Test-Path -LiteralPath $portableArchive) {
    Remove-Item -LiteralPath $portableArchive -Force
}
Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $portableArchive

$installer = Join-Path $repositoryRoot "artifacts\installer\SchoolScheduler-$Version-win-x64-setup.exe"
Copy-Item -LiteralPath $installer -Destination $releaseDirectory -Force

Write-Host "Release files:"
Get-ChildItem -LiteralPath $releaseDirectory | Select-Object Name, Length
