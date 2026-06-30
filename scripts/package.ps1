param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [string]$OutputDir = "publish",

    [switch]$SkipBuild,

    [switch]$IncludeRuntime,       # Also include .NET Desktop Runtime installer in package

    [switch]$SkipZip,              # Skip creating zip

    [string]$RuntimeVersion = "9.0.8",

    [string]$ZipName = "ExternalProgram"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path "$ScriptDir\.."
$PublishDir = "$RepoRoot\$OutputDir"

Write-Host "=== Package ExternalProgram $Configuration ===" -ForegroundColor Cyan

# ── Clean ────────────────────────────────────────────────
if (-not $SkipBuild) {
    if (Test-Path -LiteralPath $PublishDir) {
        Remove-Item -LiteralPath $PublishDir -Recurse -Force
        Write-Host "[clean] Removed $PublishDir"
    }
}

# ── Build ──────────────────────────────────────────────
if (-not $SkipBuild) {
    Write-Host "[1/5] Restoring packages..." -ForegroundColor Yellow
    dotnet restore "$RepoRoot\ExternalProgram.sln" -r win-x64
    if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

    Write-Host "[2/5] Building SwAddin & Runtime..." -ForegroundColor Yellow
    dotnet build "$RepoRoot\ExternalProgram.SwAddin\ExternalProgram.SwAddin.csproj" -c $Configuration --no-restore -v minimal
    if ($LASTEXITCODE -ne 0) { throw "SwAddin build failed" }

    dotnet build "$RepoRoot\ExternalProgram.SwAddin.Runtime\ExternalProgram.SwAddin.Runtime.csproj" -c $Configuration --no-restore -v minimal
    if ($LASTEXITCODE -ne 0) { throw "SwAddin.Runtime build failed" }

    Write-Host "[3/5] Publishing ExternalProgram & ReadBom..." -ForegroundColor Yellow
    dotnet publish "$RepoRoot\ExternalProgram.csproj" -c $Configuration -r win-x64 -o "$OutputDir\ExternalProgram" --no-restore -v minimal -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) { throw "ExternalProgram publish failed" }

    dotnet publish "$RepoRoot\ReadBom\ReadBom.csproj" -c $Configuration -r win-x64 -o "$OutputDir\ReadBom" --no-restore -v minimal -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) { throw "ReadBom publish failed" }
}

# ── Copy SwAddin Runtime ────────────────────────────────
Write-Host "[4/5] Copying SwAddin Runtime..." -ForegroundColor Yellow
$runtimeSource = "$RepoRoot\ExternalProgram.SwAddin\bin\$Configuration\net48\Runtime"
if (Test-Path -LiteralPath $runtimeSource) {
    Copy-Item -LiteralPath $runtimeSource -Destination "$OutputDir\ExternalProgram\Runtime" -Recurse -Force
    Write-Host "       Runtime copied" -ForegroundColor Green
}
else {
    Write-Host "       Runtime dir not found, skipping" -ForegroundColor DarkYellow
}

# ── Download .NET Desktop Runtime ───────────────────────
$runtimeInstallerName = "dotnet-runtime-$RuntimeVersion-win-x64.exe"
$runtimeCacheDir = "$RepoRoot\.cache"
$runtimeCachePath = "$runtimeCacheDir\$runtimeInstallerName"

if ($IncludeRuntime) {
    Write-Host "[5/5] .NET $RuntimeVersion Desktop Runtime installer..." -ForegroundColor Yellow

    # Download to fixed cache location (persists across builds)
    if (-not (Test-Path -LiteralPath $runtimeCachePath)) {
        New-Item -ItemType Directory -Force -Path $runtimeCacheDir | Out-Null

        try {
            $channel = $RuntimeVersion.Substring(0, $RuntimeVersion.LastIndexOf('.'))
            $releasesJson = Invoke-RestMethod -Uri "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/$channel/releases.json" -TimeoutSec 15
            $release = $releasesJson.releases | Where-Object { $_.runtime.version -eq $RuntimeVersion } | Select-Object -First 1
            if (-not $release) { throw "Version $RuntimeVersion not found in release metadata" }

            $runtimeFile = $release.runtime.files |
                Where-Object { $_.name -match "windowsdesktop-runtime.*win-x64\.exe$" } |
                Select-Object -First 1
            if ($runtimeFile) {
                $runtimeUrl = $runtimeFile.url
            }
            else {
                throw "Could not find windowsdesktop-runtime installer"
            }
        }
        catch {
            Write-Host "       Release JSON lookup failed: $_" -ForegroundColor DarkYellow
            Write-Host "       Falling back to direct CDN URL..." -ForegroundColor DarkYellow
            $runtimeUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$RuntimeVersion/windowsdesktop-runtime-$RuntimeVersion-win-x64.exe"
        }

        Write-Host "       Downloading to .cache\..." -NoNewline
        Invoke-WebRequest -Uri $runtimeUrl -OutFile $runtimeCachePath -UseBasicParsing
        Write-Host " done" -ForegroundColor Green
    }
    else {
        Write-Host "       Using cached: .cache\$runtimeInstallerName" -ForegroundColor Green
    }

    # Copy to publish output
    Copy-Item -LiteralPath $runtimeCachePath -Destination "$OutputDir\$runtimeInstallerName" -Force
}
else {
    Write-Host "[5/5] Runtime skipped (use -IncludeRuntime to bundle)" -ForegroundColor DarkGray
}

# ── Create Zip ──────────────────────────────────────────
if (-not $SkipZip) {
    Write-Host "`n[zip] Creating $ZipName.zip..." -ForegroundColor Yellow
    $zipPath = "$RepoRoot\$ZipName.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }
    Compress-Archive -Path "$PublishDir\*" -DestinationPath $zipPath -Force
    $zipSize = "{0:N1} MB" -f ((Get-Item $zipPath).Length / 1MB)
    Write-Host "       $zipPath  ($zipSize)" -ForegroundColor Green
}

# ── Summary ─────────────────────────────────────────────
Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host "Output: $PublishDir"
Get-ChildItem "$PublishDir" -Recurse -File | ForEach-Object {
    $size = "{0,7:N0} KB" -f ($_.Length / 1KB)
    $relPath = $_.FullName.Substring($PublishDir.Length + 1)
    Write-Host "  $size  $relPath"
}
