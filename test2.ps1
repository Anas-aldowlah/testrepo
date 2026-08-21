$repoRoot = "C:\Users\WinDows\source\repos\YAGOT"
$fontDeliveryCss = Get-Content -LiteralPath "C:\Users\WinDows\source\repos\YAGOT\wwwroot\fonts\font-delivery.css" -Raw -Encoding UTF8
$shippedIcons = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($match in [regex]::Matches($fontDeliveryCss, '(?m)^\.bi-(?<icon>[a-z0-9-]+)::before\s*\{')) {
    $shippedIcons.Add($match.Groups["icon"].Value) | Out-Null
}

$frontendSourceFiles = @(Get-ChildItem -Path $repoRoot -Recurse -File -Include *.cshtml,*.js,*.css |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.vs|artifacts|lib)\\' -and $_.Name -ne 'font-delivery.css' })

$iconSearchPatterns = @(
    '(?is)\bclass\s*=\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)\bclassName\s*=\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)\.classList\.(?:add|remove|toggle|contains|replace)\(\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b[^"'']*["'']'
)

$discoveredIcons = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $frontendSourceFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($pattern in $iconSearchPatterns) {
        foreach ($match in [regex]::Matches($content, $pattern)) {
            $discoveredIcons.Add($match.Groups["icon"].Value) | Out-Null
        }
    }
}

$missing = $shippedIcons | Where-Object { -not $discoveredIcons.Contains($_) }
Write-Output "Shipped but not discovered: $($missing -join ', ')"
$missing2 = $discoveredIcons | Where-Object { -not $shippedIcons.Contains($_) }
Write-Output "Discovered but not shipped: $($missing2 -join ', ')"
