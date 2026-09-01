using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;
using YAGOT_2._0.Data;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// لعدم حدوث تضارب او اخطاء عند تخزين وقت الطلبات او العمليات
// لان PostgreSQL تطلب منك تدخل zone وهي صارمة وهذا يسبب مشكلة. يقوم هذا الكود باخبارها ان تتجاهله
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Increase request body size limit for file uploads
// الملفات الي بيتم رفعها تكون اقل من 10 ميقا
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 5 * 1024 * 1024; // 5MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10MB
});

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddMvcOptions(options =>
    {
        options.MaxModelBindingCollectionSize = 1000;
        ArabicModelBindingMessages.Configure(options.ModelBindingMessageProvider);
    });

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// Performance: In-memory cache for SiteStatus
builder.Services.AddMemoryCache();

// Antiforgery configuration to support RequestVerificationToken header for JSON fetch requests
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("Auth", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// Registration Session & OTP Service
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = "YAGOT.RegSession";
});
builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.FromAddress),
        "Email:FromAddress is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Smtp.Host),
        "Email:Smtp:Host is required.")
    .Validate(options => options.Smtp.Port is > 0 and <= 65535,
        "Email:Smtp:Port must be between 1 and 65535.")
    .Validate(options => options.Smtp.TimeoutMilliseconds > 0,
        "Email:Smtp:TimeoutMilliseconds must be greater than zero.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Smtp.UserName),
        "Email:Smtp:UserName is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Smtp.Password),
        "Email:Smtp:Password is required.")
    .ValidateOnStart();
builder.Services.AddOptions<PublicUrlOptions>()
    .Bind(builder.Configuration.GetSection(PublicUrlOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                         && (uri.Scheme == Uri.UriSchemeHttps
                             || builder.Environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp),
        "PublicUrl:BaseUrl must be an absolute HTTPS URL (HTTP is allowed only in Development).")
    .ValidateOnStart();
builder.Services.AddScoped<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();

static void ConfigureExternalApiClient(IServiceProvider services, HttpClient client)
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["ExternalApi:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("ExternalApi:BaseUrl is not configured.");
    }

    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
}

builder.Services.AddHttpClient<DealingAPI>(ConfigureExternalApiClient);
builder.Services.AddScoped<IVisitService, VisitService>();
builder.Services.AddScoped<StoreSettingsService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Auth";
        options.AccessDeniedPath = "/Account/Auth";
        options.Cookie.Name = "YAGOT.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.ReturnUrlParameter = "ReturnUrl";

        options.Events.OnRedirectToLogin = context =>
        {
            if (IsAjaxOrJsonRequest(context.Request))
            {
                return WriteAuthJsonAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "session_expired",
                    "انتهت جلسة تسجيل الدخول. يرجى تسجيل الدخول مرة أخرى.");
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (IsAjaxOrJsonRequest(context.Request))
            {
                return WriteAuthJsonAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "forbidden",
                    "ليس لديك صلاحية لتنفيذ هذا الطلب.");
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = async context =>
        {
            var endpointRequiresAuthorization = context.HttpContext.GetEndpoint()?
                .Metadata.GetMetadata<IAuthorizeData>() != null;
            var isAdminRequest = context.Request.Path.StartsWithSegments(
                "/Admin",
                StringComparison.OrdinalIgnoreCase);

            if (!endpointRequiresAuthorization && !isAdminRequest)
            {
                return;
            }

            var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var cookieRole = context.Principal?.FindFirstValue(ClaimTypes.Role);
            if (!int.TryParse(userIdValue, out var userId) || userId <= 0 || string.IsNullOrWhiteSpace(cookieRole))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var dbContext = context.HttpContext.RequestServices.GetRequiredService<NeondbContext>();
            var userSite = await dbContext.UserSites
                .AsNoTracking()
                .FirstOrDefaultAsync(site => site.UserId == userId, context.HttpContext.RequestAborted);
            var databaseRole = string.IsNullOrWhiteSpace(userSite?.Role) ? "Customer" : userSite.Role;

            if (userSite == null || userSite.SearchNameSyncVersion == 1 || !string.Equals(databaseRole, cookieRole, StringComparison.Ordinal))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                if (userSite?.SearchNameSyncVersion == 1 && !IsAjaxOrJsonRequest(context.HttpContext.Request))
                {
                    context.HttpContext.Response.Redirect("/Account/Blocked");
                }
            }
        };
    })
    .AddCookie(AuthenticationSchemes.External, options =>
    {
        options.Cookie.Name = "YAGOT.External";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.SlidingExpiration = false;
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;

        // Google may only establish a short-lived external principal. The
        // application cookie is issued by AccountController after account match.
        options.SignInScheme = AuthenticationSchemes.External;
        options.CallbackPath = "/signin-google";
        options.UserInformationEndpoint = "https://www.googleapis.com/oauth2/v2/userinfo";
        options.SaveTokens = false;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");

        // Keep both framework-standard and raw Google claim names available.
        options.ClaimActions.MapJsonKey("email", "email");
        options.ClaimActions.MapJsonKey("name", "name");
        options.ClaimActions.MapJsonKey("id", "id");
        options.ClaimActions.MapJsonKey("sub", "sub");
        options.ClaimActions.MapJsonKey("picture", "picture");
        options.ClaimActions.MapJsonKey("email_verified", "verified_email");
        options.ClaimActions.MapJsonKey("google_email_verified", "verified_email");
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

builder.Services.AddDbContext<NeondbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("MYDB"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        });
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddDataProtection()
    .SetApplicationName("YAGOT")
    .PersistKeysToDbContext<NeondbContext>();

builder.Services.AddDbContext<UsersDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("User"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        });
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

//  انشاء كائن object (Dependency Injection - DI) كل مايتم انشاء HTTP Request
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<CartLockService>();
builder.Services.AddScoped<GuestCartService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IDraftEditSessionService, DraftEditSessionService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddSingleton<ReceiptStorageService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<ProductCatalogService>();
builder.Services.AddScoped<IBestSellerService, BestSellerService>();
builder.Services.AddHostedService<BestSellerBackgroundService>();
builder.Services.AddScoped<CategoryServer>();
builder.Services.AddScoped<Image>();
builder.Services.AddHttpClient<SiteStatusFilter>(ConfigureExternalApiClient);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<SiteStatusFilterAdmin>(ConfigureExternalApiClient);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<NeondbContext>();
    await dbContext.Database.MigrateAsync();

    var userDbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    await userDbContext.Database.MigrateAsync();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});

app.UseResponseCompression();

app.Use(async (context, next) =>
{
        var nonce = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        context.Items["csp-nonce"] = nonce;

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Content-Security-Policy"] =
                $"default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
                $"form-action 'self'; script-src 'self' 'nonce-{nonce}' https://cdn.jsdelivr.net; " +
                $"style-src 'self' 'nonce-{nonce}' https://fonts.googleapis.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                $"font-src 'self' data: https://fonts.gstatic.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                $"img-src 'self' data: blob: https:; connect-src 'self'; upgrade-insecure-requests";
            return Task.CompletedTask;
        });

    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (!context.Context.Request.Path.StartsWithSegments("/fonts", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var extension = Path.GetExtension(context.File.Name);
        context.Context.Response.Headers.CacheControl = extension is ".woff" or ".woff2"
            ? "public,max-age=31536000,immutable"
            : "public,max-age=604800";
    }
});
app.UseRouting();
app.UseRateLimiter();
app.UseCors("AllowAll");
app.UseSession();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;

    var isStaticAsset = path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWithSegments("/favicon.ico", StringComparison.OrdinalIgnoreCase);

    if (isStaticAsset)
    {
        await next();
        return;
    }
    // حماية لوحة الإدارة
    if (path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
    {
        if (!isAuthenticated)
        {
            if (IsAjaxOrJsonRequest(context.Request))
            {
                await WriteAuthJsonAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "session_expired",
                    "انتهت جلسة تسجيل الدخول. يرجى تسجيل الدخول مرة أخرى.");
                return;
            }

            context.Response.Redirect($"/Account/Auth?returnUrl={Uri.EscapeDataString(path + context.Request.QueryString)}");
            return;
        }
        if (!context.User.IsInRole("Admin") && !context.User.IsInRole("Developer"))
        {
            if (IsAjaxOrJsonRequest(context.Request))
            {
                await WriteAuthJsonAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "forbidden",
                    "ليس لديك صلاحية لتنفيذ هذا الطلب.");
                return;
            }

            context.Response.Redirect("/Account/Auth");
            return;
        }
    }

    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/DirectiveDevClose/Developer")
        || context.Request.Path.StartsWithSegments("/DirectiveDevClose/close")
        || context.Request.Path.StartsWithSegments("/Account/Auth")
        || context.Request.Path.StartsWithSegments("/Account/Google"))
    {
        await next();
        return;
    }

    // Performance: Cache site status for 5 minutes to avoid external API call on every request
    const string cacheKey = "YQ_SiteStatus";
    var cache = context.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();

    if (!cache.TryGetValue(cacheKey, out object? cachedStatus) || cachedStatus == null)
    {
        var dealingApi = context.RequestServices.GetRequiredService<DealingAPI>();
        var status = await dealingApi.checkDeveloperMode(1);
        cache.Set(cacheKey, status, TimeSpan.FromMinutes(5));
        context.Items["SiteStatus"] = status;
    }
    else
    {
        context.Items["SiteStatus"] = cachedStatus;
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

// 1. مسار جديد يدعم بادئات اللغات مثل /en/ أو /ar/
app.MapControllerRoute(
    name: "localized",
    pattern: "{culture}/{controller=Home}/{action=Index}/{id?}",
    constraints: new { culture = @"^[a-zA-Z]{2}(-[a-zA-Z]{2})?$" });

// 2. المسار الافتراضي الحالي للموقع
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

static bool IsAjaxOrJsonRequest(HttpRequest request)
{
    if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (request.Headers["X-Requested-With"].Any(value =>
            string.Equals(value, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    if (request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
    {
        return true;
    }

    return request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;
}

static Task WriteAuthJsonAsync(HttpContext context, int statusCode, string code, string message)
{
    context.Response.StatusCode = statusCode;
    context.Response.Headers.CacheControl = "no-store";
    return context.Response.WriteAsJsonAsync(
        new { success = false, code, message },
        context.RequestAborted);
}
