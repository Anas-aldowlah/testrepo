[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$wwwroot = Join-Path $repoRoot 'wwwroot'
$failures = [System.Collections.Generic.List[string]]::new()

function Get-RepoRelativePath {
    param([Parameter(Mandatory)] [string] $Path)

    return $Path.Substring($repoRoot.Length + 1).Replace('\', '/')
}

function Get-AssetReferences {
    param([Parameter(Mandatory)] [string] $Content)

    $references = [System.Collections.Generic.List[string]]::new()
    $patterns = @(
        '(?is)<script\b[^>]*\bsrc\s*=\s*["''](?<path>[^"'']+)["''][^>]*>',
        '(?is)<link\b(?=[^>]*\brel\s*=\s*["''][^"'']*stylesheet[^"'']*["''])[^>]*\bhref\s*=\s*["''](?<path>[^"'']+)["''][^>]*>'
    )

    foreach ($pattern in $patterns) {
        foreach ($match in [regex]::Matches($Content, $pattern)) {
            $references.Add($match.Groups['path'].Value)
        }
    }

    return $references
}

function Get-LocalStaticReferences {
    param([Parameter(Mandatory)] [string] $Content)

    $pattern = '(?is)<(?:script|link|img)\b[^>]*\b(?:src|href)\s*=\s*["'']~/(?<path>[^"''?#]+)'
    return @([regex]::Matches($Content, $pattern) | ForEach-Object { $_.Groups['path'].Value })
}

function Get-TotalBytes {
    param([Parameter(Mandatory)] [AllowEmptyCollection()] [object[]] $Files)

    if ($Files.Count -eq 0) {
        return 0
    }

    return ($Files | Measure-Object Length -Sum).Sum
}

$staticFiles = @(Get-ChildItem -LiteralPath $wwwroot -Recurse -File)
$firstPartyFiles = @($staticFiles | Where-Object { $_.FullName -notmatch '\\wwwroot\\lib\\' })
$javascriptFiles = @($firstPartyFiles | Where-Object { $_.Extension -in '.js', '.mjs' })
$cssFiles = @($firstPartyFiles | Where-Object { $_.Extension -eq '.css' })
$imageFiles = @($firstPartyFiles | Where-Object { $_.Extension -in '.png', '.jpg', '.jpeg', '.jfif', '.gif', '.webp', '.avif', '.svg', '.ico' })
$fontFiles = @($firstPartyFiles | Where-Object { $_.Extension -in '.woff', '.woff2', '.ttf', '.otf', '.eot' })

Write-Host "First-party JavaScript: $($javascriptFiles.Count) files / $(Get-TotalBytes $javascriptFiles) bytes"
Write-Host "First-party CSS: $($cssFiles.Count) files / $(Get-TotalBytes $cssFiles) bytes"
Write-Host "First-party images: $($imageFiles.Count) files / $(Get-TotalBytes $imageFiles) bytes"
Write-Host "First-party fonts: $($fontFiles.Count) files / $(Get-TotalBytes $fontFiles) bytes"

Write-Host 'Largest first-party static assets:'
$firstPartyFiles |
    Sort-Object Length -Descending |
    Select-Object -First 10 |
    ForEach-Object { Write-Host "  $($_.Length) bytes  $(Get-RepoRelativePath $_.FullName)" }

# Product, category, and receipt files may be database-owned. They are reported,
# but they do not fail the frontend-owned >1 MB guard without an ownership map.
$dataBackedPattern = '^wwwroot/(?:images/(?:products|categories)/|uploads/)'
$knownLargeStaticAssets = @{
    'wwwroot/images/home/perfume-hero-golden-v1.png' = 2347274
}

foreach ($image in $imageFiles) {
    $relativePath = Get-RepoRelativePath $image.FullName
    if ($image.Length -le 1MB -or $relativePath -match $dataBackedPattern) {
        continue
    }

    if ($knownLargeStaticAssets.ContainsKey($relativePath)) {
        if ($image.Length -gt $knownLargeStaticAssets[$relativePath]) {
            $failures.Add("Known large static asset grew beyond its baseline: $relativePath")
        }
        else {
            Write-Host "KNOWN >1 MB: $relativePath ($($image.Length) bytes)"
        }
    }
    else {
        $failures.Add("Unexpected frontend-owned image exceeds 1 MB: $relativePath ($($image.Length) bytes)")
    }
}

$razorFiles = @(Get-ChildItem -Path (Join-Path $repoRoot 'Views'), (Join-Path $repoRoot 'Areas') -Recurse -File -Filter '*.cshtml')
foreach ($razorFile in $razorFiles) {
    $content = Get-Content -LiteralPath $razorFile.FullName -Raw -Encoding UTF8
    $duplicates = @(Get-AssetReferences $content | Group-Object | Where-Object { $_.Count -gt 1 })
    foreach ($duplicate in $duplicates) {
        $failures.Add("Duplicate JS/CSS reference in $(Get-RepoRelativePath $razorFile.FullName): $($duplicate.Name)")
    }

    foreach ($localReference in Get-LocalStaticReferences $content) {
        $assetPath = Join-Path $wwwroot $localReference.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            $failures.Add("Missing local static asset referenced by $(Get-RepoRelativePath $razorFile.FullName): ~/$localReference")
        }
    }
}

$layoutPaths = @(
    'Views/Shared/_Layout.cshtml',
    'Views/Shared/_AuthLayout.cshtml',
    'Areas/Admin/Views/Shared/_AdminLayout.cshtml'
)

foreach ($layoutPath in $layoutPaths) {
    $absolutePath = Join-Path $repoRoot $layoutPath
    $content = Get-Content -LiteralPath $absolutePath -Raw -Encoding UTF8
    $headMatch = [regex]::Match($content, '(?is)<head\b[^>]*>(?<content>.*?)</head>')
    foreach ($script in [regex]::Matches($headMatch.Groups['content'].Value, '(?is)<script\b(?=[^>]*\bsrc\s*=)[^>]*>')) {
        if ($script.Value -notmatch '(?i)\b(?:defer|async)\b') {
            $failures.Add("Blocking external script in the head of ${layoutPath}: $($script.Value.Trim())")
        }
    }

    foreach ($asset in [regex]::Matches($content, '(?is)<(?:script|link)\b(?=[^>]*(?:src|href)\s*=\s*["'']~/)[^>]*>')) {
        if ($asset.Value -notmatch '(?i)\basp-append-version\s*=\s*["'']true["'']') {
            $failures.Add("Unversioned local layout asset in ${layoutPath}: $($asset.Value.Trim())")
        }
    }
}

$fontDeliveryCss = Get-Content -LiteralPath (Join-Path $wwwroot 'fonts\font-delivery.css') -Raw -Encoding UTF8
$shippedIcons = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($match in [regex]::Matches($fontDeliveryCss, '(?m)^\.bi-(?<icon>[a-z0-9-]+)::before\s*\{')) {
    $shippedIcons.Add($match.Groups['icon'].Value) | Out-Null
}

$frontendSourceFiles = @(Get-ChildItem -Path $repoRoot -Recurse -File -Include *.cshtml,*.js,*.css |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.vs|artifacts|lib)\\' -and $_.Name -ne 'font-delivery.css' })

$iconSearchPatterns = @(
    '(?is)\bclass\s*=\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)\bclassName\s*=\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)\.classList\.(?:add|remove|toggle|contains|replace)\(\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b[^"'']*["'']',
    '(?is)(?<=\s|^|\})\.bi-(?<icon>[a-z0-9-]+)\b'
)

$discoveredIcons = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $frontendSourceFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($pattern in $iconSearchPatterns) {
        foreach ($match in [regex]::Matches($content, $pattern)) {
            $iconName = $match.Groups['icon'].Value
            if (-not $discoveredIcons.ContainsKey($iconName)) {
                $discoveredIcons[$iconName] = [System.Collections.Generic.List[string]]::new()
            }
            $relativePath = Get-RepoRelativePath $file.FullName
            if (-not $discoveredIcons[$iconName].Contains($relativePath)) {
                $discoveredIcons[$iconName].Add($relativePath)
            }
        }
    }
}

foreach ($iconName in $discoveredIcons.Keys) {
    if (-not $shippedIcons.Contains($iconName)) {
        $usedIn = $discoveredIcons[$iconName] -join ', '
        $failures.Add("Required Bootstrap Icon 'bi-$iconName' is missing from the local subset (1.11.3). Used in: $usedIn")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error "FAIL: $_" }
    exit 1
}

Write-Host 'Frontend asset guard passed.'
