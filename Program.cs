using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Data;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;


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
    });
builder.Services.AddHttpClient<DealingAPI>();
builder.Services.AddScoped<IVisitService, VisitService>();

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
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;

        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

builder.Services.AddDbContext<NeondbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("MYDB"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        }));

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("User"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
        }));

//  انشاء كائن object (Dependency Injection - DI) كل مايتم انشاء HTTP Request
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CategoryServer>();
builder.Services.AddScoped<Image>();
builder.Services.AddScoped<SiteStatusFilter>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SiteStatusFilterAdmin>();
builder.Services.AddHttpClient<SiteStatusFilterAdmin>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Drop old database foreign key constraints pointing to the deleted users table
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NeondbContext>();
    try
    {
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE carts DROP CONSTRAINT IF EXISTS fk_user_cart;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE orders DROP CONSTRAINT IF EXISTS fk_user_order;");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE securitylogs DROP CONSTRAINT IF EXISTS fk_user_logs;");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error dropping constraints: {ex.Message}");
    }
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowAll");

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

    // صفحة البداية للزائر: توجيه الجذر فقط إلى تسجيل الدخول (الشعار يوجّه إلى Home/Index)
    if (!isAuthenticated && path == "/")
    {
        context.Response.Redirect("/Account/Auth");
        return;
    }
    //if (context.User.Identity?.IsAuthenticated == true)
    //{
    //    // الحصول على اسم المستخدم من الـ Cookie
    //    var userName = context.User.Identity?.Name;

    //    if (!string.IsNullOrEmpty(userName))
    //    {
    //        using var scope = app.Services.CreateScope();
    //        var db = scope.ServiceProvider.GetRequiredService<NeondbContext>();

    //        // التحقق من وجود المستخدم في قاعدة البيانات
    //        bool exists = await db.Users.AnyAsync(u => u.Name == userName);

    //        if (!exists)
    //        {
    //            // حذف الـ Cookie
    //            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    //            // إعادة التوجيه إلى صفحة تسجيل الدخول
    //            context.Response.Redirect("/Account/Auth");
    //            return;
    //        }
    //    }
    //    }
    // حماية لوحة الإدارة
    if (path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
    {
        if (!isAuthenticated)
        {
            context.Response.Redirect($"/Account/Auth?returnUrl={Uri.EscapeDataString(path + context.Request.QueryString)}");
            return;
        }
        if (!context.User.IsInRole("Admin") && !context.User.IsInRole("Developer"))
        {
            context.Response.Redirect("/Account/Auth");
            return;
        }
    }

    await next();
});

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/DirectiveDevClose/Developer") || context.Request.Path.StartsWithSegments("/DirectiveDevClose/close") || context.Request.Path.StartsWithSegments("/Account/Auth"))
    {
        await next();
        return;
    }

    var dealingApi = context.RequestServices.GetRequiredService<DealingAPI>();

    var status = await dealingApi.checkDeveloperMode(1);

    context.Items["SiteStatus"] = status;

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();