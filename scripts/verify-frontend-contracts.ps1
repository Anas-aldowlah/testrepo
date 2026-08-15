[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()
$passes = [System.Collections.Generic.List[string]]::new()

function Assert-Contract {
    param(
        [Parameter(Mandatory)] [bool] $Condition,
        [Parameter(Mandatory)] [string] $Name
    )

    if ($Condition) {
        $passes.Add($Name)
    }
    else {
        $failures.Add($Name)
    }
}

$ordersViewPath = Join-Path $repoRoot 'Areas\Admin\Views\Orders\Index.cshtml'
$ordersScriptPath = Join-Path $repoRoot 'wwwroot\js\admin\orders\index.js'
$legacyOrdersScriptPath = Join-Path $repoRoot 'wwwroot\js\admin\orders.js'
$newSalePath = Join-Path $repoRoot 'Areas\Admin\Views\QuickSales\NewSale.cshtml'
$validationPath = Join-Path $repoRoot 'Views\Shared\_ValidationScriptsPartial.cshtml'

$ordersView = Get-Content -LiteralPath $ordersViewPath -Raw
$ordersScript = Get-Content -LiteralPath $ordersScriptPath -Raw
$newSale = Get-Content -LiteralPath $newSalePath -Raw
$validation = Get-Content -LiteralPath $validationPath -Raw

Assert-Contract (([regex]::Matches($ordersView, 'data-yq-orders-page')).Count -eq 1) 'Admin Orders has one page root'
Assert-Contract (([regex]::Matches($ordersView, 'data-yq-update-status-url')).Count -eq 1) 'Admin Orders has one encoded status URL contract'
Assert-Contract (([regex]::Matches($ordersView, '~/js/admin/orders/index\.js')).Count -eq 1) 'Admin Orders page asset is referenced once'
Assert-Contract (-not [regex]::IsMatch($ordersView, '(?i)(?:~/|/)?js/admin/orders\.js')) 'Legacy Admin Orders asset is not referenced'
Assert-Contract (([regex]::Matches($ordersView, '(?is)<script\b(?![^>]*\bsrc\s*=)[^>]*>')).Count -eq 0) 'Admin Orders contains no inline script block'
Assert-Contract (([regex]::Matches($ordersView, '(?is)\son[a-z]+\s*=')).Count -eq 0) 'Admin Orders contains no inline event handler'
Assert-Contract ($ordersScript.Contains("document.querySelector('[data-yq-orders-page]')")) 'Admin Orders script is root-scoped'
Assert-Contract ($ordersScript.Contains("root.dataset.yqOrdersInitialized === 'true'")) 'Admin Orders prevents duplicate initialization'
Assert-Contract ($ordersScript.Contains('fetch(updateStatusUrl')) 'Admin Orders request reads the DOM URL contract'
Assert-Contract ($ordersScript.Contains('if (!response.ok)')) 'Admin Orders checks HTTP success before parsing'
Assert-Contract (-not [regex]::IsMatch($ordersScript, '(?i)\b(innerHTML|insertAdjacentHTML)\b')) 'Admin Orders avoids unsafe HTML sinks'
Assert-Contract (-not (Test-Path -LiteralPath $legacyOrdersScriptPath)) 'Deleted legacy Admin Orders script remains absent'

$completeSaleBinding = "getElementById\('btnCompleteSale'\)\.addEventListener\('click',\s*completeSale\)"
Assert-Contract (([regex]::Matches($newSale, $completeSaleBinding)).Count -eq 1) 'POS Complete Sale retains one frontend binding'

$jqueryPosition = $validation.IndexOf('jquery.min.js', [StringComparison]::OrdinalIgnoreCase)
$validatePosition = $validation.IndexOf('jquery.validate.min.js', [StringComparison]::OrdinalIgnoreCase)
$unobtrusivePosition = $validation.IndexOf('jquery.validate.unobtrusive.min.js', [StringComparison]::OrdinalIgnoreCase)
Assert-Contract ($jqueryPosition -ge 0 -and $jqueryPosition -lt $validatePosition -and $validatePosition -lt $unobtrusivePosition) 'Validation dependency order remains jQuery then Validate then Unobtrusive'

$frontendFiles = Get-ChildItem -Path $repoRoot -Recurse -File -Include *.cshtml,*.js |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git|\.vs|artifacts|lib)\\' }
$sanitizeBypasses = $frontendFiles | Select-String -Pattern 'sanitize\s*:\s*false'
Assert-Contract (@($sanitizeBypasses).Count -eq 0) 'Bootstrap sanitizer bypass remains absent'

$passes | ForEach-Object { Write-Host "PASS: $_" }
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error "FAIL: $_" }
    exit 1
}

Write-Host "Frontend architecture contracts passed: $($passes.Count)"
