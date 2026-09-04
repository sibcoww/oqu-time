[CmdletBinding()]
param(
    [string] $Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "SchoolScheduler.App\SchoolScheduler.App.csproj"
$publishDirectory = Join-Path $repositoryRoot "artifacts\publish\win-x64"
$installerScript = Join-Path $PSScriptRoot "SchoolScheduler.iss"

dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
    -p:PublishProfile=win-x64 -p:PublishDir="$publishDirectory\"
if ($LASTEXITCODE -ne 0) { throw "Не удалось опубликовать приложение." }

$compilerCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw "Inno Setup 6 не найден. Установите его и повторите запуск installer\build-installer.ps1."
}

& $compiler $installerScript
if ($LASTEXITCODE -ne 0) { throw "Не удалось собрать установщик." }
