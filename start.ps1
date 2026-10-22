<#
  Starts AgentGate locally: checks what it needs, then runs the Aspire AppHost.
  Windows: powershell -ExecutionPolicy Bypass -File .\start.ps1
  macOS/Linux (PowerShell 7): pwsh ./start.ps1
  Keep the window open while you use the console; Ctrl+C stops everything.
#>
param(
    # Keycloak's port. Leave it to the script: it moves off 8080 when that port is taken or reserved.
    [int]$KeycloakPort = 0
)

$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot

function Write-Ok([string]$message) { Write-Host "[ ok ] $message" -ForegroundColor Green }
function Write-Note([string]$message) { Write-Host "[note] $message" -ForegroundColor Yellow }
function Stop-With([string]$message, [string]$fix) {
    Write-Host "[fail] $message" -ForegroundColor Red
    if ($fix) { Write-Host "       $fix" }
    exit 1
}

function Test-Command([string]$name) { return [bool](Get-Command $name -ErrorAction SilentlyContinue) }

function Test-PortListening([int]$port) {
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $connect = $client.BeginConnect('127.0.0.1', $port, $null, $null)
        return $connect.AsyncWaitHandle.WaitOne(300) -and $client.Connected
    }
    catch { return $false }
    finally { $client.Close() }
}

function Test-PortReserved([int]$port) {
    # Hyper-V and WSL reserve port ranges on Windows; a reserved port refuses every connection.
    if (-not $IsWindows -and $PSVersionTable.PSEdition -eq 'Core') { return $false }
    $ErrorActionPreference = 'Continue'
    $ranges = netsh interface ipv4 show excludedportrange protocol=tcp 2>$null
    foreach ($line in $ranges) {
        if ($line -match '^\s*(\d+)\s+(\d+)') {
            if ($port -ge [int]$Matches[1] -and $port -le [int]$Matches[2]) { return $true }
        }
    }
    return $false
}

function Test-Docker {
    # Windows PowerShell 5.1 turns a native command's stderr into terminating errors under 'Stop'.
    $ErrorActionPreference = 'Continue'
    docker info --format '{{.ServerVersion}}' *> $null
    return $LASTEXITCODE -eq 0
}

Write-Host ''
Write-Host 'AgentGate: checking what it needs' -ForegroundColor Cyan

# .NET 10 SDK
if (-not (Test-Command 'dotnet')) {
    Stop-With '.NET is not installed.' 'Install the .NET 10 SDK from https://dotnet.microsoft.com/download, then open a new PowerShell window.'
}
$sdks = (dotnet --list-sdks) -join "`n"
if ($sdks -notmatch '(?m)^10\.') {
    Stop-With '.NET 10 SDK not found (installed SDKs below).' "Install it from https://dotnet.microsoft.com/download`n$sdks"
}
Write-Ok ".NET SDK $((dotnet --version).Trim())"

# Node.js 22+ (the console runs on Vite)
if (-not (Test-Command 'node') -or -not (Test-Command 'npm')) {
    Stop-With 'Node.js is not installed.' 'Install Node.js 22 or newer from https://nodejs.org, then open a new PowerShell window.'
}
$nodeVersion = (node --version).Trim()
if ([int]($nodeVersion.TrimStart('v').Split('.')[0]) -lt 22) {
    Stop-With "Node.js $nodeVersion is too old." 'Install Node.js 22 or newer from https://nodejs.org.'
}
Write-Ok "Node.js $nodeVersion"

# Docker (Postgres, RabbitMQ and Keycloak run in containers)
if (-not (Test-Command 'docker')) {
    Stop-With 'Docker is not installed.' 'Install Docker Desktop from https://www.docker.com/products/docker-desktop/'
}
if (-not (Test-Docker)) {
    $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if ($env:ProgramFiles -and (Test-Path $desktop)) {
        Write-Note 'Docker is not running; starting Docker Desktop (this can take a minute)...'
        Start-Process -FilePath $desktop | Out-Null
    }
    else {
        Write-Note 'Docker is not running. Start Docker Desktop; waiting for it...'
    }
    $deadline = (Get-Date).AddMinutes(3)
    while (-not (Test-Docker)) {
        if ((Get-Date) -gt $deadline) {
            Stop-With 'Docker did not start within 3 minutes.' 'Open Docker Desktop, wait for "Engine running", then run this script again.'
        }
        Start-Sleep -Seconds 3
    }
}
Write-Ok 'Docker is running'

# Ports
foreach ($port in 5173, 15180) {
    if (Test-PortListening $port) {
        Stop-With "Port $port is already in use, probably by an AgentGate that is still running." 'Close that window (or press Ctrl+C in it), then run this script again.'
    }
}
if ($KeycloakPort -eq 0) {
    $KeycloakPort = 8080
    if ((Test-PortReserved 8080) -or (Test-PortListening 8080)) {
        $KeycloakPort = 18080
        Write-Note 'Port 8080 is in use or reserved; Keycloak will use 18080.'
    }
}
if ((Test-PortReserved $KeycloakPort) -or (Test-PortListening $KeycloakPort)) {
    Stop-With "Port $KeycloakPort is not free for Keycloak." 'Run again with a free port, e.g. .\start.ps1 -KeycloakPort 28080'
}
Write-Ok "Ports free (console 5173, dashboard 15180, Keycloak $KeycloakPort)"

Write-Host ''
Write-Host 'Starting AgentGate. The first run downloads container images and takes a few minutes.' -ForegroundColor Cyan
Write-Host '  Dashboard: http://localhost:15180  (opens by itself; wait until everything is Running)'
Write-Host '  Console:   http://localhost:5173   (sign in as alice / alice-dev)'
Write-Host '  Demo:      press the play button next to demo-agent in the dashboard'
Write-Host '  Stop:      Ctrl+C in this window'
Write-Host ''

dotnet run --project src/AgentGate.AppHost -- "--KeycloakPort=$KeycloakPort"
exit $LASTEXITCODE
