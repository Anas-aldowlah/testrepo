$repoRoot = "C:\Users\WinDows\source\repos\YAGOT"
$frontendSourceFiles = @(Get-ChildItem -Path $repoRoot -Recurse -File -Include *.cshtml,*.js,*.css |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.vs|artifacts|lib)\\' -and $_.Name -ne 'font-delivery.css' })

$iconSearchPatterns = @(
    '(?is)\bclass\s*=\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)\bclassName\s*=\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)\.classList\.(?:add|remove|toggle|contains|replace)\(\s*["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b',
    '(?is)["''][^"'']*\bbi-(?<icon>[a-z0-9-]+)\b[^"'']*["'']'
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
            $discoveredIcons[$iconName].Add($file.Name)
        }
    }
}
$discoveredIcons.Keys | Measure-Object | Select-Object -ExpandProperty Count
