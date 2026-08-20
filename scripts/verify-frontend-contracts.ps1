[CmdletBinding()]
param()

if ($PSVersionTable.PSEdition -ne 'Core') {
    $pwsh = Get-Command pwsh -ErrorAction Stop
    & $pwsh.Source -NoProfile -File $PSCommandPath
    exit $LASTEXITCODE
}

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
$authViewPath = Join-Path $repoRoot 'Views\Account\Auth.cshtml'
$accountControllerPath = Join-Path $repoRoot 'Controllers\AccountController.cs'

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
$authView = Get-Content -LiteralPath $authViewPath -Raw
$accountController = Get-Content -LiteralPath $accountControllerPath -Raw

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

Assert-Contract ($authView.Contains('data-yq-complete-registration-url="@Url.Action("CompleteRegistration", "Account", new { returnUrl })"')) 'Google registration exposes one server-generated completion URL through the DOM'
Assert-Contract ($authView.Contains('fetch(btnComplete.dataset.yqCompleteRegistrationUrl, {')) 'Google registration fetch reads the DOM URL contract'
Assert-Contract (-not $authView.Contains("encodeURIComponent('@returnUrl')")) 'Google registration does not encode a Razor HTML-encoded return URL in executable JavaScript'
Assert-Contract (-not [regex]::IsMatch($authView, '(?i)Html\.Raw\s*\(')) 'Auth return URL transport avoids Html.Raw'
Assert-Contract ([regex]::IsMatch($accountController, 'private\s+string\s+GetRedirectUrl\s*\(string\?\s+returnUrl\)\s*\{\s*return\s+Url\.IsLocalUrl\(returnUrl\)\s*\?\s*returnUrl!\s*:\s*"/Home/Index";\s*\}', [System.Text.RegularExpressions.RegexOptions]::Singleline)) 'Auth redirects retain the Url.IsLocalUrl fallback gate'

$project = Get-Content -LiteralPath (Join-Path $repoRoot 'YAGOT_2.0.csproj') -Raw
$targetFrameworkMatch = [regex]::Match($project, '<TargetFramework>net(?<major>\d+)\.\d+</TargetFramework>')
if (-not $targetFrameworkMatch.Success) {
    throw 'Could not resolve the ASP.NET Core major version from TargetFramework.'
}

$runtimeMajor = $targetFrameworkMatch.Groups['major'].Value
$aspNetRuntime = & dotnet --list-runtimes |
    ForEach-Object {
        if ($_ -match "^Microsoft\.AspNetCore\.App\s+(?<version>$runtimeMajor\.\d+\.\d+)\s+\[(?<root>.+)\]$") {
            [pscustomobject]@{ Version = [version]$Matches['version']; Root = $Matches['root'] }
        }
    } |
    Sort-Object Version -Descending |
    Select-Object -First 1

if ($null -eq $aspNetRuntime) {
    throw "Microsoft.AspNetCore.App $runtimeMajor.x is required for the auth return URL contract checks."
}

$aspNetRuntimePath = Join-Path $aspNetRuntime.Root $aspNetRuntime.Version.ToString()
Get-ChildItem -LiteralPath $aspNetRuntimePath -Filter '*.dll' | ForEach-Object {
    try { [System.Reflection.Assembly]::LoadFrom($_.FullName) | Out-Null } catch { }
}

$services = [Microsoft.Extensions.DependencyInjection.ServiceCollection]::new()
[Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions]::AddLogging($services) | Out-Null
[Microsoft.Extensions.DependencyInjection.RoutingServiceCollectionExtensions]::AddRouting($services) | Out-Null
$serviceProvider = [Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions]::BuildServiceProvider($services)
$routeOptionsType = [Microsoft.Extensions.Options.IOptions``1].MakeGenericType([Microsoft.AspNetCore.Routing.RouteOptions])
$constraintResolver = [Microsoft.AspNetCore.Routing.DefaultInlineConstraintResolver]::new(
    $serviceProvider.GetService($routeOptionsType),
    $serviceProvider)
$routeHandler = [Microsoft.AspNetCore.Routing.RouteHandler]::new(
    [Microsoft.AspNetCore.Http.RequestDelegate]{ param($context) [System.Threading.Tasks.Task]::CompletedTask })

$defaultRouteMatch = [regex]::Match($program, 'name:\s*"default"\s*,\s*pattern:\s*"(?<pattern>[^"]+)"', [System.Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $defaultRouteMatch.Success) {
    throw 'Could not resolve the current default controller route from Program.cs.'
}

$routeDefaults = [Microsoft.AspNetCore.Routing.RouteValueDictionary]::new()
$route = [Microsoft.AspNetCore.Routing.Route]::new(
    $routeHandler,
    'default',
    $defaultRouteMatch.Groups['pattern'].Value,
    $routeDefaults,
    $null,
    [Microsoft.AspNetCore.Routing.RouteValueDictionary]::new(),
    $constraintResolver)
$httpContext = [Microsoft.AspNetCore.Http.DefaultHttpContext]::new()
$httpContext.RequestServices = $serviceProvider
$routeData = [Microsoft.AspNetCore.Routing.RouteData]::new()
$routeData.Routers.Add($route)
$actionContext = [Microsoft.AspNetCore.Mvc.ActionContext]::new(
    $httpContext,
    $routeData,
    [Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor]::new())
$urlHelper = [Microsoft.AspNetCore.Mvc.Routing.UrlHelper]::new($actionContext)

$urlContractMatch = [regex]::Match(
    $authView,
    'data-yq-complete-registration-url="@Url\.Action\("(?<action>[^"]+)",\s*"(?<controller>[^"]+)",\s*new\s*\{\s*returnUrl\s*\}\)"')
if (-not $urlContractMatch.Success) {
    throw 'Could not resolve the current CompleteRegistration Url.Action contract from Auth.cshtml.'
}

$returnUrlScenarios = @(
    '/Products?categoryId=4&search=oud',
    '/Products?search=عود&sort=price&page=2',
    '/Products?a=1&a=2',
    '/Products?search=oud perfume+oil&tag=50%',
    '/Products?next=%2FProducts%3Fa%3D1%26a%3D2'
)

$scenarioMarkup = [System.Collections.Generic.List[string]]::new()
foreach ($scenario in $returnUrlScenarios) {
    $routeValues = [Microsoft.AspNetCore.Routing.RouteValueDictionary]::new()
    $routeValues['returnUrl'] = $scenario
    $generatedUrl = [Microsoft.AspNetCore.Mvc.UrlHelperExtensions]::Action(
        $urlHelper,
        $urlContractMatch.Groups['action'].Value,
        $urlContractMatch.Groups['controller'].Value,
        $routeValues)
    $boundReturnUrl = [Microsoft.AspNetCore.WebUtilities.QueryHelpers]::ParseQuery(
        ([Uri]::new("https://yagot.local$generatedUrl")).Query)['returnUrl'].ToString()

    Assert-Contract ($boundReturnUrl -ceq $scenario) "Url.Action request contract preserves returnUrl: $scenario"
    Assert-Contract ($urlHelper.IsLocalUrl($scenario)) "Auth redirect gate accepts local returnUrl: $scenario"

    $scenarioMarkup.Add(('<button data-yq-complete-registration-url="{0}" data-expected-return-url="{1}"></button>' -f
        [System.Text.Encodings.Web.HtmlEncoder]::Default.Encode($generatedUrl),
        [System.Text.Encodings.Web.HtmlEncoder]::Default.Encode($scenario)))
}

$browserPath = @(
    $env:CHROME_PATH,
    'C:\Program Files\Google\Chrome\Application\chrome.exe',
    'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
    'C:\Program Files\Microsoft\Edge\Application\msedge.exe'
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

Assert-Contract ($null -ne $browserPath) 'Chrome or Edge is available for the Auth DOM contract'
if ($null -ne $browserPath) {
    $tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("yagot-auth-return-url-{0}" -f [Guid]::NewGuid().ToString('N'))
    $htmlPath = Join-Path $tempRoot 'contract.html'
    try {
        New-Item -ItemType Directory -Path $tempRoot | Out-Null
        $html = @"
<!doctype html>
<html><body data-result="pending">
$($scenarioMarkup -join [Environment]::NewLine)
<script>
const passed = [...document.querySelectorAll('[data-yq-complete-registration-url]')].every((button) => {
    const requestUrl = new URL(button.dataset.yqCompleteRegistrationUrl, 'https://yagot.local');
    return requestUrl.pathname === '/Account/CompleteRegistration'
        && requestUrl.searchParams.get('returnUrl') === button.dataset.expectedReturnUrl;
});
document.body.dataset.result = passed ? 'pass' : 'fail';
</script>
</body></html>
"@
        [IO.File]::WriteAllText($htmlPath, $html, [Text.UTF8Encoding]::new($false))
        $domOutput = & $browserPath '--headless=new' '--disable-gpu' '--no-first-run' '--disable-default-apps' '--dump-dom' ([Uri]::new($htmlPath).AbsoluteUri) 2>$null | Out-String
        Assert-Contract ($LASTEXITCODE -eq 0 -and $domOutput.Contains('data-result="pass"')) 'Razor-encoded data attribute preserves every Url.Action returnUrl through DOM dataset semantics'
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }
}

$unsafeReturnUrls = @(
    [pscustomobject]@{ Name = 'absolute external URL'; Value = 'https://evil.example/path' },
    [pscustomobject]@{ Name = 'protocol-relative URL'; Value = '//evil.example/path' },
    [pscustomobject]@{ Name = 'backslash-host URL'; Value = '/\evil.example/path' },
    [pscustomobject]@{ Name = 'javascript-style URL'; Value = 'javascript:alert(1)' },
    [pscustomobject]@{ Name = 'encoded protocol-relative URL'; Value = '%2F%2Fevil.example/path' },
    [pscustomobject]@{ Name = 'encoded external URL'; Value = 'https:%2F%2Fevil.example/path' }
)

foreach ($unsafeReturnUrl in $unsafeReturnUrls) {
    $redirectUrl = if ($urlHelper.IsLocalUrl($unsafeReturnUrl.Value)) { $unsafeReturnUrl.Value } else { '/Home/Index' }
    Assert-Contract ($redirectUrl -ceq '/Home/Index') "Auth redirect gate safely falls back for $($unsafeReturnUrl.Name)"
}

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
