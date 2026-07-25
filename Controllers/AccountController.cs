using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.UsersDatabase;
using YAGOT_2._0.Data;


using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Configuration;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Controllers;

public class AccountController : Controller
{
    private readonly NeondbContext _db;
    private readonly UsersDbContext _dbUser;
    private readonly IConfiguration _configuration;
    private readonly IVisitService _visitService;
    private readonly GuestCartService _guestCartService;

    public AccountController(NeondbContext db, IConfiguration configuration, IVisitService visitService, UsersDbContext user, GuestCartService guestCartService)
    {
        _db = db;
        _configuration = configuration;
        _visitService = visitService;
        _dbUser = user;
        _guestCartService = guestCartService;
    }

    [HttpGet]
    public IActionResult Auth(string? returnUrl = null, bool register = false)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetRedirectUrl(returnUrl));
        }

        ;

        ViewData["ReturnUrl"] = returnUrl;
        ViewData["ShowRegister"] = register;
        return View();
    }

    public IActionResult AuthR(string? returnUrl = null, string? email = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        TempData["Email"] = email;
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
        var phone = model.Phone.Trim();
       
        if (string.IsNullOrWhiteSpace(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "رقم الجوال مطلوب.");
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "كلمة المرور مطلوبة.");
        if (!ModelState.IsValid)
            return View("Auth");
        var userGloble = await _dbUser.Users
     .FirstOrDefaultAsync(i => i.Phone == HashPhone(phone));

        if (userGloble == null)
        {
            return View("Auth");
        }

        var userSiteVB = await _db.UserSites
            .FirstOrDefaultAsync(i => i.UserId == userGloble.Id);

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



        var user = await _dbUser.Users.FirstOrDefaultAsync(u => u.Phone == HashPhone(phone));

        if (user == null || !VerifyHashedPassword(model.Password, user.Passwordhash))
        {
            ModelState.AddModelError(string.Empty, "رقم الجوال أو كلمة المرور غير صحيحة.");
            return View("Auth");
        }
        ;
        await SignInUserAsync(user,user.Id);
        await _guestCartService.MergeIntoUserCartAsync(user.Id);
        TempData["UserName"] = user.Name;
        await _visitService.SaveVisitAsync(HttpContext, user.Name);
        return LocalRedirect(GetRedirectUrl(returnUrl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        TempData["ShowRegister"] = true;

        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError(nameof(model.Name), "الاسم الكامل مطلوب.");
        else if (model.Name.Trim().Length < 2)
            ModelState.AddModelError(nameof(model.Name), "الاسم يجب أن يكون حرفين على الأقل.");

        if (string.IsNullOrWhiteSpace(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "رقم الجوال مطلوب.");
        else if (model.Phone.Trim().Length < 9)
            ModelState.AddModelError(nameof(model.Phone), "رقم الجوال غير صالح.");

        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "كلمة المرور مطلوبة.");
        else if (model.Password.Length < 6)
            ModelState.AddModelError(nameof(model.Password), "كلمة المرور يجب أن تكون 6 أحرف على الأقل.");

        if (string.IsNullOrWhiteSpace(model.ConfirmPassword))
            ModelState.AddModelError(nameof(model.ConfirmPassword), "تأكيد كلمة المرور مطلوب.");

        if (string.IsNullOrWhiteSpace(model.Email))
            ModelState.AddModelError(nameof(model.Email), "الايميل مطلوب");

        if (!ModelState.IsValid)
            return View("Auth");

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "كلمة المرور وتأكيدها غير متطابقين.");
            return View("Auth");
        }

        var phone = model.Phone.Trim();
        if (await _dbUser.Users.AnyAsync(u => u.Phone == HashPhone(phone)))
        {
            ModelState.AddModelError(nameof(model.Phone), "رقم الجوال مستخدم بالفعل. سجّل الدخول أو استخدم رقماً آخر.");
            return View("Auth");
        }

        var user = new Models.UsersDatabase.User
        {
            Name = model.Name.Trim(),
            Phone = HashPhone(phone),
            Passwordhash = HashPassword(model.Password),
            Email = model.Email?.Trim()
        };

        _dbUser.Users.Add(user);
        await _dbUser.SaveChangesAsync();
        var userSite = new UserSite
        {
            UserId = user.Id,
            Role = "Customer",
        };

        _db.UserSites.Add(userSite);
        await _db.SaveChangesAsync();
        await SignInUserAsync(user,user.Id);
        await _guestCartService.MergeIntoUserCartAsync(user.Id);

        TempData["UserName"] = user.Name;
        TempData.Remove("ShowRegister");
        TempData["Success"] = true;
        TempData["Email"] = model.Email;
        return LocalRedirect(GetRedirectUrl(returnUrl));
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
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

        // منع تعديل ملف مستخدم آخر عبر التلاعب بالنموذج (Id قادم من حقل مخفي)
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

        // إعادة تسجيل الدخول لتحديث الـ Claims — الاسم يُستخدم لربط السلة والطلبات بالمستخدم
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

    [HttpGet]
    public IActionResult GoogleLogin(string? returnUrl = "/Account/AuthR")
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleResponse), new { returnUrl })
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult GoogleResponse(string? returnUrl = null)
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
        {
            return RedirectToAction("AuthR", "Account");
        }

        var email = User.FindFirst(ClaimTypes.Email)?.Value;

        return RedirectToAction("AuthR", "Account", new { email = email });
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
        string key = _configuration["Encryption:Key"];
        string iv = _configuration["Encryption:IV"];

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

    public sealed class RegisterModel
    {
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
