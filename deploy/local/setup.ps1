# Local HTTPS for local.lantern.api on Windows. Adds the hosts entry and trusts the local CA.
# Run from any PowerShell; it asks for elevation itself. Use -WhatIf to see the changes first.
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$Remove,
    [switch]$Force,
    [switch]$Elevated
)

$ErrorActionPreference = 'Stop'

$HostName = 'local.lantern.api'
$CaSubject = 'CN=Lantern Local CA'
$Certs = Join-Path $PSScriptRoot 'certs'
$CaFile = Join-Path $Certs 'ca.crt'
$HostsFile = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'

function Test-Admin {
    $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function New-Certificates {
    $ready = (Test-Path $CaFile) -and (Test-Path (Join-Path $Certs "$HostName.crt")) -and (Test-Path (Join-Path $Certs "$HostName.key"))
    if ($ready -and -not $Force) {
        Write-Host 'Certificates already exist (use -Force to regenerate).'
        return
    }
    if (-not $PSCmdlet.ShouldProcess($Certs, 'Generate certificates with Docker')) { return }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker is required to generate the certificates.'
    }
    New-Item -ItemType Directory -Force -Path $Certs | Out-Null
    docker run --rm -v "${PSScriptRoot}:/work" -e "HOST=$HostName" -e OUT=/work/certs alpine:3 sh -c 'apk add --no-cache openssl >/dev/null && sh /work/mkcert.sh'
    if ($LASTEXITCODE -ne 0) { throw 'Certificate generation failed.' }
}

function Get-HostsLines {
    return @(Get-Content -Path $HostsFile)
}

function Test-HostsEntry {
    $pattern = '^\s*[^#\s]+\s+([^#]*\s)?' + [regex]::Escape($HostName) + '(\s|$)'
    return [bool](Get-HostsLines | Where-Object { $_ -match $pattern })
}

function Add-HostsEntry {
    if (Test-HostsEntry) {
        Write-Host "hosts already maps $HostName."
        return
    }
    if (-not $PSCmdlet.ShouldProcess($HostsFile, "Append '127.0.0.1 $HostName'")) { return }
    $raw = Get-Content -Path $HostsFile -Raw
    $prefix = ''
    if ($raw -and -not $raw.EndsWith("`n")) { $prefix = "`r`n" }
    Add-Content -Path $HostsFile -Value ($prefix + "127.0.0.1 $HostName")
}

function Remove-HostsEntry {
    if (-not (Test-HostsEntry)) { return }
    $pattern = '^\s*[^#\s]+\s+([^#]*\s)?' + [regex]::Escape($HostName) + '(\s|$)'
    $kept = Get-HostsLines | Where-Object { $_ -notmatch $pattern }
    if (-not $PSCmdlet.ShouldProcess($HostsFile, "Remove the $HostName entry")) { return }
    [IO.File]::WriteAllLines($HostsFile, [string[]]$kept, (New-Object Text.UTF8Encoding($false)))
}

function Add-CaTrust {
    $ca = New-Object Security.Cryptography.X509Certificates.X509Certificate2($CaFile)
    $store = Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -eq $CaSubject }
    foreach ($stale in ($store | Where-Object { $_.Thumbprint -ne $ca.Thumbprint })) {
        if ($PSCmdlet.ShouldProcess($stale.Thumbprint, 'Remove an older Lantern local CA')) {
            Remove-Item -Path $stale.PSPath
        }
    }
    if ($store | Where-Object { $_.Thumbprint -eq $ca.Thumbprint }) {
        Write-Host 'The local CA is already trusted.'
        return
    }
    if ($PSCmdlet.ShouldProcess('Cert:\LocalMachine\Root', "Trust $CaSubject")) {
        Import-Certificate -FilePath $CaFile -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
    }
}

function Remove-CaTrust {
    foreach ($cert in (Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Subject -eq $CaSubject })) {
        if ($PSCmdlet.ShouldProcess($cert.Thumbprint, "Stop trusting $CaSubject")) {
            Remove-Item -Path $cert.PSPath
        }
    }
}

if (-not $Remove) { New-Certificates }

if (-not (Test-Admin) -and -not $WhatIfPreference) {
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Elevated')
    if ($Remove) { $argList += '-Remove' }
    $process = Start-Process powershell.exe -Verb RunAs -Wait -PassThru -ArgumentList $argList
    exit $process.ExitCode
}

try {
    if ($Remove) {
        Remove-HostsEntry
        Remove-CaTrust
        if (-not $WhatIfPreference) { Write-Host 'Removed.' }
    }
    else {
        Add-HostsEntry
        Add-CaTrust
        Write-Host ''
        Write-Host 'Start it:  docker compose -f deploy/local/docker-compose.yml up --build'
        Write-Host "Then open: https://$HostName/health/live"
    }
}
finally {
    if ($Elevated) { Read-Host 'Press Enter to close' }
}
