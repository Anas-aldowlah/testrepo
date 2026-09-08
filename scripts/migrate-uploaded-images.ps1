[CmdletBinding()]
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$KeepSource
)

$ErrorActionPreference = 'Stop'
$resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$sourceRoot = Join-Path (Join-Path $resolvedRoot 'wwwroot') 'images'
$destinationRoot = Join-Path (Join-Path $resolvedRoot 'App_Data') 'images'
$allowedFolders = @('products', 'categories')
$summary = [ordered]@{ Migrated = 0; VerifiedExisting = 0; Skipped = 0; Failed = 0; RemovedSources = 0 }

foreach ($folder in $allowedFolders) {
    $sourceDirectory = Join-Path $sourceRoot $folder
    $destinationDirectory = Join-Path $destinationRoot $folder
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null

    if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) {
        continue
    }

    foreach ($sourceFile in Get-ChildItem -LiteralPath $sourceDirectory -File) {
        $destinationFile = Join-Path $destinationDirectory $sourceFile.Name
        try {
            if (Test-Path -LiteralPath $destinationFile -PathType Leaf) {
                $sourceHash = (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash
                $destinationHash = (Get-FileHash -LiteralPath $destinationFile -Algorithm SHA256).Hash
                if ($sourceHash -ne $destinationHash) {
                    throw "Destination already exists with different content: $destinationFile"
                }
                $summary.VerifiedExisting++
            }
            else {
                Copy-Item -LiteralPath $sourceFile.FullName -Destination $destinationFile
                $sourceHash = (Get-FileHash -LiteralPath $sourceFile.FullName -Algorithm SHA256).Hash
                $destinationHash = (Get-FileHash -LiteralPath $destinationFile -Algorithm SHA256).Hash
                if ($sourceHash -ne $destinationHash) {
                    Remove-Item -LiteralPath $destinationFile -Force
                    throw "Copied file failed SHA-256 verification: $destinationFile"
                }

                $probe = [System.IO.File]::OpenRead($destinationFile)
                $probe.Dispose()
                $summary.Migrated++
            }

            if ($KeepSource) {
                $summary.Skipped++
            }
            else {
                Remove-Item -LiteralPath $sourceFile.FullName -Force
                $summary.RemovedSources++
            }
        }
        catch {
            $summary.Failed++
            Write-Error -ErrorAction Continue $_
        }
    }
}

[pscustomobject]$summary
if ($summary.Failed -gt 0) {
    exit 1
}
