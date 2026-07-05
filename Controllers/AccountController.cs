using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Models;

namespace YAGOT_2._0.Controllers;

public class AccountController : Controller
{
    private readonly NeondbContext _db;

    public AccountController(NeondbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Auth(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetRedirectUrl(returnUrl));
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginModel model, string? returnUrl = null)
    {

        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrWhiteSpace(model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "رقم الجوال مطلوب.");
        if (string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "كلمة المرور مطلوبة.");

        if (!ModelState.IsValid)
            return View("Auth");

        var phone = model.Phone.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Phone == phone);

        if (user == null || !VerifyHashedPassword(model.Password, user.Passwordhash))
        {
            ModelState.AddModelError(string.Empty, "رقم الجوال أو كلمة المرور غير صحيحة.");
            return View("Auth");
        }

        await SignInUserAsync(user);
        TempData["UserName"] = user.Name;
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

        if (!ModelState.IsValid)
            return View("Auth");

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(nameof(model.ConfirmPassword), "كلمة المرور وتأكيدها غير متطابقين.");
            return View("Auth");
        }

        var phone = model.Phone.Trim();
        if (await _db.Users.AnyAsync(u => u.Phone == phone))
        {
            ModelState.AddModelError(nameof(model.Phone), "رقم الجوال مستخدم بالفعل. سجّل الدخول أو استخدم رقماً آخر.");
            return View("Auth");
        }

        var user = new User
        {
            Name = model.Name.Trim(),
            Phone = phone,
            Passwordhash = HashPassword(model.Password),
            Role = "Customer"
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await SignInUserAsync(user);

        TempData["UserName"] = user.Name;
        TempData.Remove("ShowRegister");
        return LocalRedirect(GetRedirectUrl(returnUrl));
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Auth));
    }

    private static string GetRedirectUrl(string? returnUrl)
    {
        return string.IsNullOrWhiteSpace(returnUrl) || !Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
            ? "/Home/Index"
            : returnUrl!;
    }

    private async Task SignInUserAsync(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.MobilePhone, user.Phone),
            new(ClaimTypes.Role, user.Role ?? "Customer")
        };

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
    }
}
