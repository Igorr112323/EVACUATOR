[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$SdkDir,
    [string]$OutputPath = "sdk-preflight.json"
)
$ErrorActionPreference = 'Stop'
# [VERIFY_IN_NANOCAD_SDK] Diagnostic only. This script never registers or installs anything.
$root = [IO.Path]::GetFullPath($SdkDir)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    Write-Error "SDK directory not found: $root"
    exit 1
}
$items = foreach ($name in @('mapimgd.dll', 'hostdbmgd.dll', 'hostmgd.dll')) {
    $matches = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter $name -ErrorAction Stop)
    if ($matches.Count -eq 0) {
        [pscustomobject]@{ name=$name; found=$false; paths=@() }
    } else {
        [pscustomobject]@{ name=$name; found=$true; paths=@($matches | ForEach-Object {
            [pscustomobject]@{ path=$_.FullName; sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash;
                fileVersion=$_.VersionInfo.FileVersion }
        }) }
    }
}
$report = [pscustomobject]@{
    sdkDirectory=$root; files=@($items); hostCompatibilityVerified=$false;
    bridgeImplemented=$false; status='VERIFY_IN_NANOCAD_SDK';
    notice='Diagnostic only: no SDK integration, package, registration or compatibility is claimed.'
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Preflight written to $OutputPath. SDK bridge and nanoCAD compatibility remain unverified."
exit 2 # even if DLLs exist, the real bridge is intentionally not implemented
