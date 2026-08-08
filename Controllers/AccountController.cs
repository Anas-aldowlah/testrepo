using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IOtpService _otpService;

    public AccountController(
        NeondbContext db,
        IConfiguration configuration,
        IVisitService visitService,
        UsersDbContext user,
        GuestCartService guestCartService,
        IOtpService otpService)
    {
        _db = db;
        _configuration = configuration;
        _visitService = visitService;
        _dbUser = user;
        _guestCartService = guestCartService;
        _otpService = otpService;
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
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetRedirectUrl(returnUrl));
        }

        var regState = GetRegistrationState();
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["ShowRegister"] = register || regState.Step > 1;
        ViewData["RegState"] = regState;
        return View();
    }

    public IActionResult AuthR(string? returnUrl = null, string? email = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        TempData["Email"] = email;
        var regState = GetRegistrationState();
        ViewData["RegState"] = regState;
        ViewData["ShowRegister"] = true;
        return View("Auth");
    }

    public IActionResult RecoveryAccountTem(string? returnUrl = null, string? email = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        TempData["Email"] = email;
        return View("RecoveryAccount");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model, string? returnUrl = null)
    {
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
        return LocalRedirect(GetRedirectUrl(returnUrl));
    }

    #region Direct Email Login & Google Login Actions

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailLogin(string email, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["GoogleLoginError"] = "يرجى إدخال البريد الإلكتروني.";
            return RedirectToAction("Auth", "Account", new { returnUrl });
        }

        var cleanEmail = email.Trim();
        var existingUser = await _dbUser.Users.FirstOrDefaultAsync(u => u.Email == cleanEmail);
        if (existingUser != null)
        {
            // Account exists -> Log in immediately without Google 2FA prompt
            await SignInUserAsync(existingUser, existingUser.Id);
            await _guestCartService.MergeIntoUserCartAsync(existingUser.Id);
            TempData["UserName"] = existingUser.Name;
            await _visitService.SaveVisitAsync(HttpContext, existingUser.Name);
            ClearRegistrationState();
            return LocalRedirect(GetRedirectUrl(returnUrl));
        }

        // Account does not exist in DB
        TempData["GoogleLoginError"] = "لم يتم العثور على حساب مرتبط بهذا البريد الإلكتروني. يرجى إنشاء حساب أولاً.";
        return RedirectToAction("Auth", "Account", new { returnUrl });
    }

    [HttpGet]
    public IActionResult GoogleLogin(string? returnUrl = null, bool isRegister = false)
    {
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
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            if (isRegister)
            {
                return RedirectToAction("Auth", "Account", new { register = true, returnUrl });
            }
            TempData["GoogleLoginError"] = "تعذر إكمال المصادقة بواسطة Google.";
            return RedirectToAction("Auth", "Account", new { returnUrl });
        }

        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var picture = User.FindFirst("picture")?.Value ?? User.FindFirst("urn:google:picture")?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            if (isRegister)
            {
                return RedirectToAction("Auth", "Account", new { register = true, returnUrl });
            }
            TempData["GoogleLoginError"] = "تعذر الحصول على البريد الإلكتروني من Google.";
            return RedirectToAction("Auth", "Account", new { returnUrl });
        }

        email = email.Trim();

        // Sign out temporary google auth cookie so principal is clean until session is set
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Check if Google email already belongs to an existing user
        var existingUser = await _dbUser.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (existingUser != null)
        {
            // User exists -> perform login & account linking immediately
            await SignInUserAsync(existingUser, existingUser.Id);
            await _guestCartService.MergeIntoUserCartAsync(existingUser.Id);
            TempData["UserName"] = existingUser.Name;
            await _visitService.SaveVisitAsync(HttpContext, existingUser.Name);
            ClearRegistrationState();
            return LocalRedirect(GetRedirectUrl(returnUrl));
        }

        // IF USER DOES NOT EXIST IN DATABASE:
        if (!isRegister)
        {
            // Flow originated from Login page -> Do NOT create account automatically!
            TempData["GoogleLoginError"] = "حساب Google هذا غير مرتبط بحساب في ياقوت. يرجى إنشاء حساب أولاً.";
            return RedirectToAction("Auth", "Account", new { returnUrl });
        }

        // Flow originated from Registration page -> Populate session state & advance to Step 2
        var state = GetRegistrationState();
        state.GoogleSubjectId = sub;
        state.GoogleEmail = email;
        state.GoogleName = name;
        state.GooglePicture = picture;
        state.IsGoogleVerified = true;
        state.FullName = name ?? state.FullName;
        state.Step = 2; // Auto advance to step 2

        SaveRegistrationState(state);

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
            isPhoneVerified = state.IsPhoneVerified
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveStep2Info([FromBody] SendOtpRequest request)
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
        if (!state.IsGoogleVerified)
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
        state.IsPhoneVerified = true;
        state.Step = 3;

        SaveRegistrationState(state);

        return Json(new { success = true, nextStep = 3 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPhoneOtp([FromBody] SendOtpRequest request)
    {
        return await SaveStep2Info(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPhoneOtp([FromBody] VerifyOtpRequest request)
    {
        var state = GetRegistrationState();
        if (!state.IsGoogleVerified)
        {
            return Json(new { success = false, message = "جلسة التسجيل غير صالحة. يرجى البدء من الخطوة الأولى." });
        }

        state.IsPhoneVerified = true;
        state.Step = 3;
        SaveRegistrationState(state);

        return Json(new { success = true, message = "تم حفظ رقم الجوال بنجاح.", nextStep = 3 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRegistration([FromBody] CompleteRegistrationModel model, string? returnUrl = null)
    {
        if (model == null)
        {
            return Json(new { success = false, message = "البيانات المدخلة غير صالحة." });
        }

        var state = GetRegistrationState();
        if (!state.IsGoogleVerified || string.IsNullOrWhiteSpace(state.GoogleEmail))
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
            Email = state.GoogleEmail.Trim(),
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
    public IActionResult SetRegisterStep(int step)
    {
        var state = GetRegistrationState();
        if (step == 3 && (!state.IsGoogleVerified || string.IsNullOrWhiteSpace(state.PhoneNumber)))
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
    public IActionResult ResetRegistration()
    {
        ClearRegistrationState();
        return Json(new { success = true });
    }

    #endregion

    [HttpGet]
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
        currentUser.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();

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
    public async Task<IActionResult> RecoveryAccount(RecoveryModel model)
    {
        var accountUser = await _dbUser.Users.FirstOrDefaultAsync(u =>
            u.Email == model.Email || u.Phone == HashPhone(model.Phone));
        if (accountUser == null)
        {
            ModelState.AddModelError(string.Empty, "لم يتم العثور على حساب بهذا البريد الإلكتروني أو رقم الجوال.");
            return View("RecoveryAccount");
        }
        accountUser.Passwordhash = HashPassword(model.Password);
        await _dbUser.SaveChangesAsync();
        return View("Auth");
    }

    private static string GetRedirectUrl(string? returnUrl)
    {
        return string.IsNullOrWhiteSpace(returnUrl) || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            ? "/Home/Index"
            : returnUrl!;
    }

    public IActionResult GoogleLoginRecovery()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleResponseRecovery))
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult GoogleResponseRecovery()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            return RedirectToAction("RecoveryAccountTem", "Account");
        }

        var email = User.FindFirst(ClaimTypes.Email)?.Value;

        return RedirectToAction("RecoveryAccountTem", "Account", new { email = email });
    }

    private async Task SignInUserAsync(Models.UsersDatabase.User user, int Id)
    {
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

    public sealed class SendOtpRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public sealed class VerifyOtpRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public sealed class CompleteRegistrationModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
