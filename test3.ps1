$repoRoot = "C:\Users\WinDows\source\repos\YAGOT"
$frontendSourceFiles = @(Get-ChildItem -Path $repoRoot -Recurse -File -Include *.cshtml,*.js,*.css |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.vs|artifacts|lib)\\' -and $_.Name -ne 'font-delivery.css' })

$discoveredIcons = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $frontendSourceFiles) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($content, '(?is)\bbi-(?<icon>[a-z0-9-]+)\b')) {
        $discoveredIcons.Add($match.Groups["icon"].Value) | Out-Null
    }
}
$discoveredIcons.Count
