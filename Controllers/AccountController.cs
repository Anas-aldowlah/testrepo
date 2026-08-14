using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YAGOT_2._0.Data;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.UsersDatabase;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Controllers;

public class AccountController : Controller
{
    private const string REG_SESSION_KEY = "YAGOT_RegistrationSessionState";

    private readonly NeondbContext _db;
    private readonly UsersDbContext _dbUser;
    private readonly IConfiguration _configuration;
    private readonly IVisitService _visitService;
    private readonly GuestCartService _guestCartService;
    private readonly IPasswordResetEmailSender _passwordResetEmailSender;
    private readonly ITimeLimitedDataProtector _passwordResetProtector;
    private readonly Uri _publicBaseUri;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        NeondbContext db,
        IConfiguration configuration,
        IVisitService visitService,
        UsersDbContext user,
        GuestCartService guestCartService,
        IPasswordResetEmailSender passwordResetEmailSender,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PublicUrlOptions> publicUrlOptions,
        ILogger<AccountController> logger)
    {
        _db = db;
        _configuration = configuration;
        _visitService = visitService;
        _dbUser = user;
        _guestCartService = guestCartService;
        _passwordResetEmailSender = passwordResetEmailSender;
        _passwordResetProtector = dataProtectionProvider
            .CreateProtector("YAGOT.PasswordReset.v1")
            .ToTimeLimitedDataProtector();
        _publicBaseUri = new Uri(publicUrlOptions.Value.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        _logger = logger;
    }

    #region Registration Session State Helpers

    private RegistrationSessionState GetRegistrationState()
    {
        var json = HttpContext.Session.GetString(REG_SESSION_KEY);
        if (string.IsNullOrEmpty(json))
        {
            return new RegistrationSessionState();
        }
        try
        {
            return JsonSerializer.Deserialize<RegistrationSessionState>(json) ?? new RegistrationSessionState();
        }
        catch
        {
            return new RegistrationSessionState();
        }
    }

    private void SaveRegistrationState(RegistrationSessionState state)
    {
        var json = JsonSerializer.Serialize(state);
        HttpContext.Session.SetString(REG_SESSION_KEY, json);
    }

    private void ClearRegistrationState()
    {
        HttpContext.Session.Remove(REG_SESSION_KEY);
    }

    #endregion

    [HttpGet]
    public IActionResult Auth(string? returnUrl = null, bool register = false)
    {
        returnUrl = GetRedirectUrl(returnUrl);
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(returnUrl);
        }

        var regState = GetRegistrationState();
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["ShowRegister"] = register || regState.Step > 1;
        ViewData["RegState"] = regState;
        return View();
    }

    public IActionResult AuthR(string? returnUrl = null, string? email = null)
    {
        ViewData["ReturnUrl"] = GetRedirectUrl(returnUrl);
        TempData["Email"] = email;
        var regState = GetRegistrationState();
        ViewData["RegState"] = regState;
        ViewData["ShowRegister"] = true;
        return View("Auth");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> Login(LoginModel model, string? returnUrl = null)
    {
        returnUrl = GetRedirectUrl(returnUrl);
        ViewData["ReturnUrl"] = returnUrl;
        var phone = model.Phone?.Trim() ?? string.Empty;
       
        if (string.IsNullOrWhiteSpace(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "رقم الجوال مطلوب.");
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "كلمة المرور مطلوبة.");
        if (!ModelState.IsValid)
        {
            ViewData["RegState"] = GetRegistrationState();
            return View("Auth");
        }

        var hashedPhone = HashPhone(phone);
        var userGloble = await _dbUser.Users.FirstOrDefaultAsync(i => i.Phone == hashedPhone);

        if (userGloble == null || !VerifyHashedPassword(model.Password, userGloble.Passwordhash))
        {
            ModelState.AddModelError(string.Empty, "رقم الجوال أو كلمة المرور غير صحيحة.");
            ViewData["RegState"] = GetRegistrationState();
            return View("Auth");
        }

        var userSiteVB = await _db.UserSites.FirstOrDefaultAsync(i => i.UserId == userGloble.Id);
        if (userSiteVB == null)
        {
            var userSite = new UserSite
            {
                UserId = userGloble.Id,
                Role = "Customer"
            };

            _db.UserSites.Add(userSite);
            await _db.SaveChangesAsync();
        }

        await SignInUserAsync(userGloble, userGloble.Id);
        await _guestCartService.MergeIntoUserCartAsync(userGloble.Id);
        TempData["UserName"] = userGloble.Name;
        await _visitService.SaveVisitAsync(HttpContext, userGloble.Name);
        return LocalRedirect(returnUrl);
    }

    #region Google Login Actions

    [AllowAnonymous]
    [HttpGet]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> GoogleLogin(string? returnUrl = null, bool isRegister = false)
    {
        returnUrl = GetRedirectUrl(returnUrl);
        TempData.Remove("GoogleLoginError");
        TempData.Remove("GoogleLoginErrorTitle");
        TempData.Remove("RegistrationNotice");
        ClearRegistrationState();
        await HttpContext.SignOutAsync(AuthenticationSchemes.External);

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleResponse), new { returnUrl, isRegister })
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GoogleResponse(string? returnUrl = null, bool isRegister = false)
    {
        returnUrl = GetRedirectUrl(returnUrl);
        var externalResult = await HttpContext.AuthenticateAsync(AuthenticationSchemes.External);
        if (!externalResult.Succeeded || externalResult.Principal == null)
        {
            _logger.LogWarning(
                "Google external authentication ticket could not be read. Succeeded={Succeeded}.",
                externalResult.Succeeded);
            await HttpContext.SignOutAsync(AuthenticationSchemes.External);
            TempData["GoogleLoginErrorTitle"] = "تعذر تسجيل الدخول عبر Google";
            TempData["GoogleLoginError"] = "تعذر إكمال المصادقة بواسطة Google.";
            return RedirectToAction("Auth", "Account", new { returnUrl });
        }

        var externalPrincipal = externalResult.Principal;
        var googleClaims = externalPrincipal.Claims.ToList();
        _logger.LogInformation("Google callback received {ClaimCount} claims.", googleClaims.Count);

        var email = externalPrincipal.FindFirst(ClaimTypes.Email)?.Value
            ?? externalPrincipal.FindFirst("email")?.Value;
        var name = externalPrincipal.FindFirst(ClaimTypes.Name)?.Value
            ?? externalPrincipal.FindFirst("name")?.Value;
        var sub = externalPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? externalPrincipal.FindFirst("sub")?.Value
            ?? externalPrincipal.FindFirst("id")?.Value;
        var emailVerifiedClaim = externalPrincipal.FindFirst("google_email_verified")?.Value
            ?? externalPrincipal.FindFirst("email_verified")?.Value;
        var picture = externalPrincipal.FindFirst("picture")?.Value
            ?? externalPrincipal.FindFirst("urn:google:picture")?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogWarning("Google callback did not contain the required email identity indicator.");
            await HttpContext.SignOutAsync(AuthenticationSchemes.External);
            TempData["GoogleLoginErrorTitle"] = "حساب Google غير مرتبط";
            TempData["GoogleLoginError"] = "تعذر الحصول على البريد الإلكتروني من Google.";
            return RedirectToAction("Auth", "Account", new { returnUrl });
        }

        email = email.Trim();
        var normalizedEmail = email.ToLowerInvariant();
        var hasVerifiedClaim = bool.TryParse(emailVerifiedClaim, out var isEmailVerified);
        _logger.LogInformation(
            "Google identity resolved. EmailPresent={EmailPresent}, EmailVerified={EmailVerified}, VerificationClaimPresent={VerificationClaimPresent}, SubjectPresent={SubjectPresent}.",
            true,
            hasVerifiedClaim && isEmailVerified,
            hasVerifiedClaim,
            !string.IsNullOrWhiteSpace(sub));

        if (!hasVerifiedClaim || !isEmailVerified)
        {
            _logger.LogWarning(
                "Rejected Google authentication because the provider did not assert a verified email address. VerificationClaimPresent={VerificationClaimPresent}.",
                hasVerifiedClaim);
            await HttpContext.SignOutAsync(AuthenticationSchemes.External);
            ClearRegistrationState();
            TempData["GoogleLoginErrorTitle"] = "تعذر التحقق من البريد الإلكتروني";
            TempData["GoogleLoginError"] = "يجب التحقق من البريد الإلكتروني في حساب Google قبل تسجيل الدخول أو إنشاء حساب.";
            return RedirectToAction("Auth", "Account", new { returnUrl });
        }

        // The external identity has now been read and must never become the app identity.
        await HttpContext.SignOutAsync(AuthenticationSchemes.External);

        // Check if Google email already belongs to an existing user
        var existingUser = await _dbUser.Users.FirstOrDefaultAsync(
            u => u.Email != null && u.Email.ToLower() == normalizedEmail);
        if (existingUser != null)
        {
            // User exists -> perform login & account linking immediately
            TempData.Remove("GoogleLoginError");
            TempData.Remove("GoogleLoginErrorTitle");
            TempData.Remove("RegistrationNotice");
            await SignInUserAsync(existingUser, existingUser.Id);
            try
            {
                await _guestCartService.MergeIntoUserCartAsync(existingUser.Id);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not merge the guest cart for Google user {UserId}; continuing with an empty cart.",
                    existingUser.Id);
            }
            TempData["UserName"] = existingUser.Name;
            await _visitService.SaveVisitAsync(HttpContext, existingUser.Name);
            ClearRegistrationState();
            return LocalRedirect(GetRedirectUrl(returnUrl));
        }

        // A verified Google identity without a local account starts registration.
        // It is not issued an application cookie until registration completes.
        var state = new RegistrationSessionState
        {
            GoogleSubjectId = sub ?? normalizedEmail,
            GoogleEmail = email,
            GoogleName = name,
            GooglePicture = picture,
            IsGoogleVerified = true,
            FullName = name,
            Step = 2
        };

        SaveRegistrationState(state);
        TempData.Remove("GoogleLoginError");
        TempData.Remove("GoogleLoginErrorTitle");
        TempData["RegistrationNotice"] = "هذا البريد غير مسجل، يرجى استكمال بيانات الحساب أولاً.";

        return RedirectToAction("Auth", "Account", new { register = true, returnUrl });
    }

    [HttpGet]
    public IActionResult GetRegisterState()
    {
        var state = GetRegistrationState();
        return Json(new
        {
            step = state.Step,
            googleEmail = state.GoogleEmail,
            googleName = state.GoogleName,
            isGoogleVerified = state.IsGoogleVerified,
            fullName = state.FullName,
            phoneNumber = state.PhoneNumber,
            isPhoneStepCompleted = state.IsPhoneStepCompleted
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> SaveStep2Info([FromBody] Step2InfoRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Phone))
        {
            return Json(new { success = false, message = "رقم الجوال مطلوب." });
        }

        var fullName = request.FullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 2)
        {
            return Json(new { success = false, message = "الاسم الكامل يجب أن يكون حرفين على الأقل." });
        }

        var phone = request.Phone.Trim();
        if (phone.Length < 9 || !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\d{9}$"))
        {
            return Json(new { success = false, message = "رقم الجوال يجب أن يتكون من 9 أرقام بالضبط." });
        }

        var state = GetRegistrationState();
        if (!state.IsGoogleVerified
            || string.IsNullOrWhiteSpace(state.GoogleSubjectId)
            || string.IsNullOrWhiteSpace(state.GoogleEmail)
            || state.Step != 2)
        {
            return Json(new { success = false, message = "يرجى إكمال المصادقة عبر Google أولاً." });
        }

        // Check if phone number already belongs to another user
        var hashedPhone = HashPhone(phone);
        if (await _dbUser.Users.AnyAsync(u => u.Phone == hashedPhone))
        {
            return Json(new { success = false, message = "رقم الجوال مستخدم بالفعل. سجّل الدخول أو استخدم رقماً آخر." });
        }

        state.FullName = fullName;
        state.PhoneNumber = phone;
        state.IsPhoneStepCompleted = true;
        state.Step = 3;

        SaveRegistrationState(state);

        return Json(new { success = true, nextStep = 3 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> CompleteRegistration([FromBody] CompleteRegistrationModel model, string? returnUrl = null)
    {
        if (model == null)
        {
            return Json(new { success = false, message = "البيانات المدخلة غير صالحة." });
        }

        var state = GetRegistrationState();
        if (!state.IsGoogleVerified
            || string.IsNullOrWhiteSpace(state.GoogleSubjectId)
            || string.IsNullOrWhiteSpace(state.GoogleEmail)
            || !state.IsPhoneStepCompleted
            || state.Step != 3)
        {
            return Json(new { success = false, message = "يرجى التحقق عبر Google أولاً." });
        }

        if (string.IsNullOrWhiteSpace(state.PhoneNumber))
        {
            return Json(new { success = false, message = "يرجى أدخال رقم الجوال في الخطوة الثانية أولاً." });
        }

        var fullName = string.IsNullOrWhiteSpace(model.FullName) ? state.FullName : model.FullName.Trim();
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length < 2)
        {
            return Json(new { success = false, message = "الاسم الكامل يجب أن يكون حرفين على الأقل." });
        }

        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
        {
            return Json(new { success = false, message = "كلمة المرور يجب أن تكون 6 أحرف على الأقل." });
        }

        if (model.Password != model.ConfirmPassword)
        {
            return Json(new { success = false, message = "كلمة المرور وتأكيدها غير متطابقين." });
        }

        var phone = state.PhoneNumber.Trim();
        var hashedPhone = HashPhone(phone);

        if (await _dbUser.Users.AnyAsync(u => u.Phone == hashedPhone))
        {
            return Json(new { success = false, message = "رقم الجوال مستخدم بالفعل. سجّل الدخول أو استخدم رقماً آخر." });
        }

        var user = new Models.UsersDatabase.User
        {
            Name = fullName,
            Phone = hashedPhone,
            Passwordhash = HashPassword(model.Password),
            Email = state.GoogleEmail.Trim().ToLowerInvariant(),
            Createdat = DateTime.UtcNow
        };

        _dbUser.Users.Add(user);
        await _dbUser.SaveChangesAsync();

        var userSite = new UserSite
        {
            UserId = user.Id,
            Role = "Customer"
        };
        _db.UserSites.Add(userSite);
        await _db.SaveChangesAsync();

        await SignInUserAsync(user, user.Id);
        await _guestCartService.MergeIntoUserCartAsync(user.Id);

        TempData["UserName"] = user.Name;
        TempData["Success"] = true;

        ClearRegistrationState();

        return Json(new { success = true, redirectUrl = GetRedirectUrl(returnUrl) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetRegisterStep(int step)
    {
        var state = GetRegistrationState();
        if (step == 3 && (!state.IsGoogleVerified || !state.IsPhoneStepCompleted || string.IsNullOrWhiteSpace(state.PhoneNumber)))
        {
            return Json(new { success = false, message = "لا يمكن الانتقال للخطوة 3 قبل إدخال رقم الجوال في الخطوة 2." });
        }
        if (step == 2 && !state.IsGoogleVerified)
        {
            return Json(new { success = false, message = "لا يمكن الانتقال للخطوة 2 قبل المصادقة بواسطة Google." });
        }

        if (step >= 1 && step <= 3)
        {
            state.Step = step;
            SaveRegistrationState(state);
        }

        return Json(new { success = true, step = state.Step });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ResetRegistration()
    {
        ClearRegistrationState();
        return Json(new { success = true });
    }

    #endregion

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        ClearRegistrationState();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userIdVal = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdVal) || !int.TryParse(userIdVal, out var userId))
            return Challenge();

        var user = await _dbUser.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();

        var model = new ProfileVM
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = _db.UserSites.Where(s => s.UserId == user.Id).Select(f => f.Role).FirstOrDefault() ?? "Customer",
            CreatedAt = user.Createdat
        };
        if (model.Role == "Admin")
        {
            model.Role = "مدير";
        }
        else if (model.Role == "Developer")
        {
            model.Role = "مطور";
        }
        else
        {
            model.Role = "زبون";
        }

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileVM model)
    {
        var userIdVal = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdVal) || !int.TryParse(userIdVal, out var userId))
            return Challenge();

        var currentUser = await _dbUser.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (currentUser == null) return NotFound();

        if (model.Id != currentUser.Id)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "الاسم الكامل مطلوب.");
        else if (model.Name.Trim().Length < 2)
            ModelState.AddModelError(nameof(model.Name), "الاسم يجب أن يكون حرفين على الأقل.");
        else if (await _dbUser.Users.AnyAsync(u => u.Id != currentUser.Id && u.Name == model.Name.Trim()))
            ModelState.AddModelError(nameof(model.Name), "هذا الاسم مستخدم بالفعل.");

        if (!ModelState.IsValid)
        {
            model.CreatedAt = currentUser.Createdat;
            return View(model);
        }

        currentUser.Name = model.Name.Trim();
        // Email is an authentication identifier established through Google.
        // It must only change through a future verified email-linking flow.
        model.Email = currentUser.Email;

        await _dbUser.SaveChangesAsync();

        await SignInUserAsync(currentUser, currentUser.Id);

        TempData["ProfileSuccess"] = true;
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult RecoveryAccount()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> RecoveryAccount(RecoveryModel model)
    {
        var email = model.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            ModelState.AddModelError(nameof(model.Email), "يرجى إدخال بريد إلكتروني صالح.");
            return View(model);
        }

        var normalizedEmail = email.ToLower();
        var accountUser = await _dbUser.Users.FirstOrDefaultAsync(
            u => u.Email != null && u.Email.ToLower() == normalizedEmail);

        if (accountUser != null)
        {
            var payload = JsonSerializer.Serialize(new PasswordResetTokenPayload(
                accountUser.Id,
                ComputePasswordFingerprint(accountUser.Passwordhash)));
            var token = _passwordResetProtector.Protect(payload, TimeSpan.FromMinutes(15));
            var resetPath = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { token })
                ?? throw new InvalidOperationException("Could not generate the password-reset route.");
            var resetUrl = new Uri(_publicBaseUri, resetPath).AbsoluteUri;

            try
            {
                await _passwordResetEmailSender.SendResetLinkAsync(
                    accountUser.Email!,
                    resetUrl,
                    HttpContext.RequestAborted);
            }
            catch (Exception)
            {
                // Do not reveal account existence or mail configuration details.
                _logger.LogError(
                    "Failed to send a password reset email for user {UserId}; sensitive delivery details were suppressed.",
                    accountUser.Id);
            }
        }

        TempData["RecoveryMessage"] = "إذا كان البريد مرتبطاً بحساب، فسيصلك رابط صالح لمدة 15 دقيقة.";
        return RedirectToAction(nameof(RecoveryAccount));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? token)
    {
        var payload = await ValidatePasswordResetTokenAsync(token);
        if (payload == null)
        {
            ViewData["ResetError"] = "رابط إعادة التعيين غير صالح أو منتهي الصلاحية.";
        }

        return View(new ResetPasswordModel { Token = token ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("Auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
        {
            ModelState.AddModelError(nameof(model.Password), "كلمة المرور يجب أن تكون 6 أحرف على الأقل.");
        }
        if (!string.Equals(model.Password, model.ConfirmPassword, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "كلمتا المرور غير متطابقتين.");
        }

        var payload = await ValidatePasswordResetTokenAsync(model.Token);
        if (payload == null)
        {
            ModelState.AddModelError(string.Empty, "رابط إعادة التعيين غير صالح أو منتهي الصلاحية.");
        }

        if (!ModelState.IsValid || payload == null)
        {
            return View(model);
        }

        var accountUser = await _dbUser.Users.FirstOrDefaultAsync(u => u.Id == payload.UserId);
        if (accountUser == null)
        {
            ModelState.AddModelError(string.Empty, "الحساب المرتبط بالرابط لم يعد موجوداً.");
            return View(model);
        }

        accountUser.Passwordhash = HashPassword(model.Password);
        await _dbUser.SaveChangesAsync();

        TempData["PasswordResetSuccess"] = "تم تغيير كلمة المرور. يمكنك تسجيل الدخول الآن.";
        return RedirectToAction(nameof(Auth));
    }

    private string GetRedirectUrl(string? returnUrl)
    {
        return Url.IsLocalUrl(returnUrl) ? returnUrl! : "/Home/Index";
    }

    private async Task<PasswordResetTokenPayload?> ValidatePasswordResetTokenAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var json = _passwordResetProtector.Unprotect(token, out _);
            var payload = JsonSerializer.Deserialize<PasswordResetTokenPayload>(json);
            if (payload == null || payload.UserId <= 0)
            {
                return null;
            }

            var accountUser = await _dbUser.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == payload.UserId);
            if (accountUser == null)
            {
                return null;
            }

            return string.Equals(
                payload.PasswordFingerprint,
                ComputePasswordFingerprint(accountUser.Passwordhash),
                StringComparison.Ordinal)
                ? payload
                : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }

    private static string ComputePasswordFingerprint(string passwordHash)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash)));
    }

    private async Task SignInUserAsync(Models.UsersDatabase.User user, int Id)
    {
        if (Id <= 0 || user.Id != Id)
        {
            throw new UnauthorizedAccessException("Cannot issue an application cookie without a valid linked user identifier.");
        }

        var userSite = await _db.UserSites.FirstOrDefaultAsync(s => s.UserId == Id);
        if (userSite == null)
        {
            userSite = new UserSite
            {
                UserId = Id,
                Role = "Customer"
            };
            _db.UserSites.Add(userSite);
            await _db.SaveChangesAsync();
        }

        var role = userSite.Role;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.MobilePhone, user.Phone),
            new(ClaimTypes.Role, role ?? "Customer")
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7),
            IssuedUtc = DateTimeOffset.UtcNow
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }

    private static string HashPassword(string password)
    {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        using var algorithm = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        var hash = algorithm.GetBytes(32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
    }

    private string HashPhone(string phone)
    {
        string key = _configuration["Encryption:Key"] ?? "12345678901234567890123456789012";
        string iv = _configuration["Encryption:IV"] ?? "1234567890123456";

        using var aes = Aes.Create();

        aes.Key = Encoding.UTF8.GetBytes(key);
        aes.IV = Encoding.UTF8.GetBytes(iv);

        using var encryptor = aes.CreateEncryptor();

        byte[] phoneBytes = Encoding.UTF8.GetBytes(phone);
        byte[] encryptedBytes = encryptor.TransformFinalBlock(phoneBytes, 0, phoneBytes.Length);

        return Convert.ToBase64String(encryptedBytes);
    }

    private static bool VerifyHashedPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        using var algorithm = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        var actualHash = algorithm.GetBytes(32);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    public sealed class LoginModel
    {
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class Step2InfoRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public sealed class CompleteRegistrationModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    private sealed record PasswordResetTokenPayload(int UserId, string PasswordFingerprint);
}
