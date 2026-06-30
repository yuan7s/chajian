param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [string]$OutputDir = "publish",

    [switch]$SkipBuild,

    [switch]$SkipRuntime,          # Skip downloading .NET Desktop Runtime

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
if (-not $SkipRuntime) {
    Write-Host "[5/5] Downloading .NET $RuntimeVersion Desktop Runtime installer..." -ForegroundColor Yellow
    $runtimeInstaller = "$OutputDir\dotnet-runtime-$RuntimeVersion-win-x64.exe"

    if (-not (Test-Path -LiteralPath $runtimeInstaller)) {
        try {
            $releasesJson = Invoke-RestMethod -Uri "https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/$RuntimeVersion/releases.json" -TimeoutSec 15
            $runtimeFile = $releasesJson.releases[0].runtime.files |
                Where-Object { $_.name -match "windows-x64.*installer" } |
                Select-Object -First 1
            if ($runtimeFile) {
                $runtimeUrl = $runtimeFile.url
            }
            else {
                throw "Could not find installer in release metadata"
            }
        }
        catch {
            # Fallback: construct URL from known pattern
            Write-Host "       Release JSON lookup failed, using fallback URL..." -ForegroundColor DarkYellow
            $runtimeUrl = "https://download.visualstudio.microsoft.com/download/pr/dotnet-$RuntimeVersion-runtime-desktop-win-x64-installer.exe"
        }

        Write-Host "       Downloading..." -NoNewline
        Invoke-WebRequest -Uri $runtimeUrl -OutFile $runtimeInstaller -UseBasicParsing
        Write-Host " done" -ForegroundColor Green
    }
    else {
        Write-Host "       Already downloaded, skipping" -ForegroundColor Green
    }
}
else {
    Write-Host "[5/5] Runtime download skipped" -ForegroundColor DarkGray
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
