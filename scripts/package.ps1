param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [string]$OutputDir = "publish",

    [switch]$SkipBuild,

    [switch]$IncludeRuntime,      # Also download .NET Desktop Runtime installer

    [string]$RuntimeVersion = "9.0.8"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path "$ScriptDir\.."

Write-Host "=== Package ExternalProgram $Configuration ===" -ForegroundColor Cyan

# ── Build ──────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Host "`n[1/4] Restoring packages..." -ForegroundColor Yellow
    dotnet restore "$RepoRoot\ExternalProgram.sln" -r win-x64
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

    Write-Host "`n[2/4] Building SwAddin & Runtime..." -ForegroundColor Yellow
    dotnet build "$RepoRoot\ExternalProgram.SwAddin\ExternalProgram.SwAddin.csproj" -c $Configuration --no-restore -v minimal
    if ($LASTEXITCODE -ne 0) { throw "SwAddin build failed" }

    dotnet build "$RepoRoot\ExternalProgram.SwAddin.Runtime\ExternalProgram.SwAddin.Runtime.csproj" -c $Configuration --no-restore -v minimal
    if ($LASTEXITCODE -ne 0) { throw "SwAddin.Runtime build failed" }

    Write-Host "`n[3/4] Publishing ExternalProgram & ReadBom..." -ForegroundColor Yellow
    dotnet publish "$RepoRoot\ExternalProgram.csproj" -c $Configuration -r win-x64 -o "$OutputDir\ExternalProgram" --no-restore -v minimal -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) { throw "ExternalProgram publish failed" }

    dotnet publish "$RepoRoot\ReadBom\ReadBom.csproj" -c $Configuration -r win-x64 -o "$OutputDir\ReadBom" --no-restore -v minimal -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) { throw "ReadBom publish failed" }
}
else {
    Write-Host "[skip] Build skipped" -ForegroundColor DarkGray
}

# ── Copy SwAddin Runtime ────────────────────────────────
Write-Host "`n[4/4] Copying SwAddin Runtime..." -ForegroundColor Yellow
$runtimeSource = "$RepoRoot\ExternalProgram.SwAddin\bin\$Configuration\net48\Runtime"
if (Test-Path -LiteralPath $runtimeSource) {
    Copy-Item -LiteralPath $runtimeSource -Destination "$OutputDir\ExternalProgram\Runtime" -Recurse -Force
    Write-Host "       Runtime copied from $runtimeSource" -ForegroundColor Green
}
else {
    Write-Host "       Runtime dir not found: $runtimeSource (may be OK for Debug)" -ForegroundColor DarkYellow
}

# ── Download Runtime Installer ──────────────────────────
if ($IncludeRuntime) {
    Write-Host "`n[+] Downloading .NET $RuntimeVersion Desktop Runtime..." -ForegroundColor Yellow
    $runtimeUrl = "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-$RuntimeVersion-windows-x64-installer"
    # Direct download link
    $runtimeUrl = "https://download.visualstudio.microsoft.com/download/pr/dotnet-runtime-$RuntimeVersion-win-x64.exe"

    # Fallback: try the official channel release JSON
    $runtimeInstaller = "$OutputDir\dotnet-runtime-$RuntimeVersion-win-x64.exe"

    try {
        $releasesJson = Invoke-RestMethod -Uri "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/$RuntimeVersion/releases.json" -TimeoutSec 10
        $runtimeFile = $releasesJson.releases[0].runtime.files | Where-Object { $_.name -like "*windows-x64-installer*" -or $_.rid -eq "win-x64" } | Select-Object -First 1
        if ($runtimeFile) {
            $runtimeUrl = $runtimeFile.url
        }
    }
    catch {
        Write-Host "       Could not resolve exact download URL, using fallback..." -ForegroundColor DarkYellow
    }

    Write-Host "       Downloading from: $runtimeUrl"
    Invoke-WebRequest -Uri $runtimeUrl -OutFile $runtimeInstaller -UseBasicParsing
    Write-Host "       Runtime installer saved to: $runtimeInstaller" -ForegroundColor Green
}

# ── Summary ─────────────────────────────────────────────
Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host "Output: $RepoRoot\$OutputDir"
Get-ChildItem "$OutputDir" -Recurse -File | ForEach-Object {
    $size = "{0,8:N0} KB" -f ($_.Length / 1KB)
    $relPath = $_.FullName.Substring((Resolve-Path $OutputDir).Path.Length + 1)
    Write-Host "  $size  $relPath"
}

Write-Host "`nTo create a zip:" -ForegroundColor Cyan
Write-Host "  Compress-Archive -Path $OutputDir\* -DestinationPath ExternalProgram.zip -Force" -ForegroundColor White
