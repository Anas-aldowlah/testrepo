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
$publicLayoutPath = Join-Path $repoRoot 'Views\Shared\_Layout.cshtml'
$productDetailsPath = Join-Path $repoRoot 'Views\Products\Details.cshtml'
$programPath = Join-Path $repoRoot 'Program.cs'
$adminLayoutPath = Join-Path $repoRoot 'Areas\Admin\Views\Shared\_AdminLayout.cshtml'
$adminAjaxPath = Join-Path $repoRoot 'wwwroot\js\admin\ajax-response.js'
$draftSessionPath = Join-Path $repoRoot 'wwwroot\js\quick-sales\draft-edit-session.js'
$ledgerPath = Join-Path $repoRoot 'Areas\Admin\Views\QuickSales\Ledger.cshtml'

$ordersView = Get-Content -LiteralPath $ordersViewPath -Raw
$ordersScript = Get-Content -LiteralPath $ordersScriptPath -Raw
$newSale = Get-Content -LiteralPath $newSalePath -Raw
$validation = Get-Content -LiteralPath $validationPath -Raw
$publicLayout = Get-Content -LiteralPath $publicLayoutPath -Raw
$productDetails = Get-Content -LiteralPath $productDetailsPath -Raw
$program = Get-Content -LiteralPath $programPath -Raw
$adminLayout = Get-Content -LiteralPath $adminLayoutPath -Raw
$adminAjax = Get-Content -LiteralPath $adminAjaxPath -Raw
$draftSession = Get-Content -LiteralPath $draftSessionPath -Raw
$ledger = Get-Content -LiteralPath $ledgerPath -Raw

Assert-Contract (([regex]::Matches($ordersView, 'data-yq-orders-page')).Count -eq 1) 'Admin Orders has one page root'
Assert-Contract (([regex]::Matches($ordersView, 'data-yq-update-status-url')).Count -eq 1) 'Admin Orders has one encoded status URL contract'
Assert-Contract (([regex]::Matches($ordersView, '~/js/admin/orders/index\.js')).Count -eq 1) 'Admin Orders page asset is referenced once'
Assert-Contract (-not [regex]::IsMatch($ordersView, '(?i)(?:~/|/)?js/admin/orders\.js')) 'Legacy Admin Orders asset is not referenced'
Assert-Contract (([regex]::Matches($ordersView, '(?is)<script\b(?![^>]*\bsrc\s*=)[^>]*>')).Count -eq 0) 'Admin Orders contains no inline script block'
Assert-Contract (([regex]::Matches($ordersView, '(?is)\son[a-z]+\s*=')).Count -eq 0) 'Admin Orders contains no inline event handler'
Assert-Contract ($ordersScript.Contains("document.querySelector('[data-yq-orders-page]')")) 'Admin Orders script is root-scoped'
Assert-Contract ($ordersScript.Contains("root.dataset.yqOrdersInitialized === 'true'")) 'Admin Orders prevents duplicate initialization'
Assert-Contract ($ordersScript.Contains('fetch(updateStatusUrl')) 'Admin Orders request reads the DOM URL contract'
Assert-Contract ($ordersScript.Contains('YaqutAdminAjax.readJson(response)')) 'Admin Orders uses the shared safe JSON parser'
Assert-Contract (-not [regex]::IsMatch($ordersScript, '(?i)\b(innerHTML|insertAdjacentHTML)\b')) 'Admin Orders avoids unsafe HTML sinks'
Assert-Contract (-not (Test-Path -LiteralPath $legacyOrdersScriptPath)) 'Deleted legacy Admin Orders script remains absent'

Assert-Contract ($program.Contains('IsAjaxOrJsonRequest(context.Request)') -and $program.Contains('StatusCodes.Status401Unauthorized') -and $program.Contains('"session_expired"')) 'Backend AJAX authentication failures use the 401 JSON contract'
Assert-Contract ($program.Contains('OnRedirectToAccessDenied') -and $program.Contains('StatusCodes.Status403Forbidden') -and $program.Contains('"forbidden"')) 'Backend AJAX authorization failures use the 403 JSON contract'
Assert-Contract ($program.Contains('context.Response.Redirect(context.RedirectUri)') -and $program.Contains('context.Response.Redirect($"/Account/Auth?returnUrl=')) 'Normal browser authentication redirects remain present'
Assert-Contract (([regex]::Matches($adminLayout, '~/js/admin/ajax-response\.js')).Count -eq 1) 'Admin layout loads one shared AJAX parser'
Assert-Contract ($adminLayout.IndexOf('~/js/admin/ajax-response.js', [StringComparison]::Ordinal) -lt $adminLayout.IndexOf('RenderSectionAsync("Scripts"', [StringComparison]::Ordinal)) 'Shared AJAX parser loads before page request owners'
Assert-Contract ($adminAjax.Contains("response.status === 401") -and $adminAjax.Contains("'session_expired'")) 'Shared parser classifies session expiry'
Assert-Contract ($adminAjax.Contains("response.status === 403") -and $adminAjax.Contains("'forbidden'")) 'Shared parser classifies forbidden responses'
Assert-Contract ($adminAjax.Contains("'unexpected_content'") -and $adminAjax.Contains("'malformed_json'") -and $adminAjax.Contains("'server'")) 'Shared parser distinguishes content, JSON, and server failures'
Assert-Contract ($adminAjax.Contains("'network'") -and $adminAjax.Contains('normalizeError')) 'Shared parser normalizes network failures'
Assert-Contract ($newSale.Contains('posSessionExpired') -and $newSale.Contains('draftSaveCoordinator.cancelPending()')) 'POS session expiry stops scheduled and queued autosave while retaining the page state'
Assert-Contract ($newSale.Contains("requestError.kind === 'session_expired' || requestError.kind === 'forbidden') return;") -and $newSale.IndexOf("availablePaymentMethods = [{ id: 1", [StringComparison]::Ordinal) -gt 0) 'POS authentication failure cannot enter the Cash fallback branch'
Assert-Contract (-not $newSale.Contains('.then(res => res.json())') -and -not $newSale.Contains('await response.json()') -and -not $newSale.Contains('await deleteResponse.json()')) 'POS network responses are not blindly parsed as JSON'
Assert-Contract ($draftSession.Contains('YaqutAdminAjax.readJson(response)') -and $draftSession.Contains('expireAuthentication(requestError.message)') -and $draftSession.Contains('handleAuthFailure: expireAuthentication')) 'Draft session expiry stops polling and write ownership'
Assert-Contract ($ledger.Contains('YaqutAdminAjax.readJson') -and -not $ledger.Contains('.then(res => res.json())')) 'Ledger uses the shared safe JSON parser'

$completeSaleBinding = "getElementById\('btnCompleteSale'\)\.addEventListener\('click',\s*completeSale\)"
Assert-Contract (([regex]::Matches($newSale, $completeSaleBinding)).Count -eq 1) 'POS Complete Sale retains one frontend binding'

$jqueryPosition = $validation.IndexOf('jquery.min.js', [StringComparison]::OrdinalIgnoreCase)
$validatePosition = $validation.IndexOf('jquery.validate.min.js', [StringComparison]::OrdinalIgnoreCase)
$unobtrusivePosition = $validation.IndexOf('jquery.validate.unobtrusive.min.js', [StringComparison]::OrdinalIgnoreCase)
Assert-Contract ($jqueryPosition -ge 0 -and $jqueryPosition -lt $validatePosition -and $validatePosition -lt $unobtrusivePosition) 'Validation dependency order remains jQuery then Validate then Unobtrusive'

Assert-Contract (([regex]::Matches($publicLayout, '(?is)<meta\s+name="description"')).Count -eq 1) 'Public layout has one meta description owner'
Assert-Contract ($publicLayout.Contains('ViewData["Description"]') -and $publicLayout.Contains('string.IsNullOrWhiteSpace(pageDescription)')) 'Public layout supports a non-empty page description with fallback'
Assert-Contract (([regex]::Matches($productDetails, 'ViewData\["Title"\]\s*=')).Count -eq 1 -and $productDetails.Contains('ViewData["Title"] = Model?.Name;')) 'Product Details leaves the brand suffix to the public layout'
Assert-Contract ($productDetails.Contains('ViewData["Description"] = Model?.Description?.Trim();')) 'Product Details supplies its trimmed model description'
Assert-Contract (-not [regex]::IsMatch("$publicLayout`n$productDetails", '(?i)Html\.Raw\s*\(')) 'Product metadata avoids Html.Raw construction'

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
