<#
.SYNOPSIS
    Starts the MD Pipeline API (http://localhost:5100) and frontend (http://localhost:3100) for local development.

.DESCRIPTION
    - Loads KEY=VALUE pairs from .env in the repo root (if present) into this process' environment, so secrets such as
      Swing__OdbcConnectionString stay out of appsettings files. Variables already set in the shell take precedence.
    - Checks that the .NET 10 SDK and Node.js/npm are installed and that ports 5100/3100 are free.
    - Restores the local dotnet tools and installs frontend packages when node_modules is missing (or with -Install).
    - Starts both processes in this console, waits until /api/health answers through the Vite proxy and opens the browser.
    - Ctrl+C (or either process exiting) stops both process trees.

    The Development environment uses the Swing and Migration Tool fixtures, applies EF migrations and seeds the users
    admin|approver|analyst|viewer@mdpipeline.local (password ChangeMe123!).

.EXAMPLE
    .\start.ps1
.EXAMPLE
    .\start.ps1 -Install -NoBrowser
.EXAMPLE
    .\start.ps1 -Reset        # deletes the local SQLite metadata DB and the data\ folder first
#>
[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$NoBrowser,
    [switch]$Reset,
    [string]$Environment = 'Development',
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot
$backendDir = Join-Path $root 'src\backend'
$apiDir = Join-Path $backendDir 'Haworth.MdPipeline.Api'
$frontendDir = Join-Path $root 'src\frontend'
$apiUrl = 'http://localhost:5100'
$webUrl = 'http://localhost:3100'

function Write-Step([string]$message) { Write-Host "==> $message" -ForegroundColor Cyan }

function Assert-Command([string]$name, [string]$hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name was not found on PATH. $hint"
    }
}

function Assert-PortFree([int]$port) {
    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $port)
        $listener.Start()
    }
    catch {
        throw "Port $port is already in use. Stop the process using it and try again."
    }
    finally {
        if ($listener) { $listener.Stop() }
    }
}

function Invoke-Checked([string]$file, [string[]]$arguments, [string]$workingDirectory) {
    Push-Location $workingDirectory
    try {
        & $file @arguments
        if ($LASTEXITCODE -ne 0) { throw "'$file $($arguments -join ' ')' failed with exit code $LASTEXITCODE." }
    }
    finally {
        Pop-Location
    }
}

function Stop-Tree($process) {
    if ($process -and -not $process.HasExited) {
        # dotnet run and npm spawn child processes, so stop the whole tree.
        & taskkill.exe /PID $process.Id /T /F 2>&1 | Out-Null
    }
}

function Test-Url([string]$url) {
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5
        return $response.StatusCode -lt 500
    }
    catch {
        return $false
    }
}

# Prints each source check (Migration Tool, Swing, data folder) as a warning; never blocks startup.
function Write-SourceHealth([string]$url) {
    Write-Step 'Checking data sources'
    $body = $null
    try {
        $body = (Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60).Content
    }
    catch {
        if ($_.Exception.Response) {
            try { $body = (New-Object IO.StreamReader($_.Exception.Response.GetResponseStream())).ReadToEnd() } catch { }
        }
    }

    if (-not $body) {
        Write-Warning 'Could not read /api/health (sources may be slow to respond). The app is still usable.'
        return
    }

    foreach ($check in (ConvertFrom-Json $body).checks) {
        if ($check.healthy) {
            Write-Host "    OK    $($check.name): $($check.detail)" -ForegroundColor Green
        }
        else {
            Write-Host "    FAIL  $($check.name): $($check.detail)" -ForegroundColor Yellow
        }
    }
}

function Import-DotEnv([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) {
        return
    }

    $loaded = @()
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $path) {
        $lineNumber++
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) { continue }

        $separator = $trimmed.IndexOf('=')
        if ($separator -lt 1) {
            throw ".env line ${lineNumber} is not KEY=VALUE. Comment lines must start with '#' (did a '#' get removed from a heading?)."
        }

        $name = $trimmed.Substring(0, $separator).Trim()
        if ($name.StartsWith('export ')) { $name = $name.Substring(7).Trim() }
        $value = $trimmed.Substring($separator + 1).Trim()
        if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($name))) {
            [Environment]::SetEnvironmentVariable($name, $value, 'Process')
            $loaded += $name
        }
    }

    # Names only; values may be secrets.
    if ($loaded.Count -gt 0) { Write-Host "    loaded from .env: $($loaded -join ', ')" }
}

Write-Step 'Checking prerequisites'
Assert-Command 'dotnet' 'Install the .NET 10 SDK: https://dotnet.microsoft.com/download'
Assert-Command 'npm' 'Install Node.js (includes npm): https://nodejs.org'
if (-not (& dotnet --list-sdks | Where-Object { $_ -match '^10\.' })) {
    throw '.NET 10 SDK is required (dotnet --list-sdks shows no 10.x SDK).'
}
Assert-PortFree 5100
Assert-PortFree 3100

Write-Step 'Loading .env'
Import-DotEnv (Join-Path $root '.env')

if ($Reset) {
    Write-Step 'Resetting local metadata DB and data folder'
    Get-ChildItem $apiDir -Filter 'md_pipeline.db*' -File | Remove-Item -Force
    $dataDir = Join-Path $apiDir 'data'
    if (Test-Path $dataDir) { Remove-Item $dataDir -Recurse -Force }
}

Write-Step 'Restoring dotnet tools'
Invoke-Checked 'dotnet' @('tool', 'restore') $backendDir

if ($Install -or -not (Test-Path (Join-Path $frontendDir 'node_modules'))) {
    Write-Step 'Installing frontend packages (npm ci)'
    Invoke-Checked 'npm.cmd' @('ci', '--no-audit', '--no-fund') $frontendDir
}

$api = $null
$web = $null
try {
    Write-Step "Starting API ($Environment) on $apiUrl"
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $env:ASPNETCORE_URLS = $apiUrl
    $api = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', 'Haworth.MdPipeline.Api', '--no-launch-profile') `
        -WorkingDirectory $backendDir -NoNewWindow -PassThru
    $null = $api.Handle  # caches the handle so ExitCode is available on Windows PowerShell 5.1

    Write-Step 'Waiting for the API to respond'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    # Liveness only (GET /). /api/health also checks Swing and the Migration Tool, which may legitimately be down
    # (e.g. an expired bearer token) and must not block startup.
    while (-not (Test-Url "$apiUrl/")) {
        if ($api.HasExited) { throw "API exited with code $($api.ExitCode) during startup." }
        if ((Get-Date) -gt $deadline) { throw "API not ready after $TimeoutSeconds s." }
        Start-Sleep -Seconds 2
    }

    Write-Step "Starting frontend on $webUrl"
    $web = Start-Process -FilePath 'npm.cmd' -ArgumentList @('run', 'dev') -WorkingDirectory $frontendDir -NoNewWindow -PassThru
    $null = $web.Handle

    Write-Step 'Waiting for the frontend proxy'
    while (-not (Test-Url "$webUrl/")) {
        if ($api.HasExited) { throw "API exited with code $($api.ExitCode) during startup." }
        if ($web.HasExited) { throw "Frontend exited with code $($web.ExitCode) during startup." }
        if ((Get-Date) -gt $deadline) { throw "Frontend not ready after $TimeoutSeconds s." }
        Start-Sleep -Seconds 1
    }

    Write-SourceHealth "$apiUrl/api/health"

    Write-Host ''
    Write-Host "MD Pipeline is running:" -ForegroundColor Green
    Write-Host "  Frontend : $webUrl"
    Write-Host "  API      : $apiUrl  (health: $apiUrl/api/health)"
    if ($Environment -eq 'Development') {
        Write-Host '  Login    : admin@mdpipeline.local / ChangeMe123!'
    }
    Write-Host '  Press Ctrl+C to stop both.' -ForegroundColor Yellow
    Write-Host ''

    if (-not $NoBrowser) { Start-Process $webUrl }

    while (-not $api.HasExited -and -not $web.HasExited) {
        Start-Sleep -Seconds 1
    }

    if ($api.HasExited) { Write-Warning "API stopped (exit code $($api.ExitCode))." }
    if ($web.HasExited) { Write-Warning "Frontend stopped (exit code $($web.ExitCode))." }
}
finally {
    Write-Step 'Stopping API and frontend'
    Stop-Tree $web
    Stop-Tree $api
}
