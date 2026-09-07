using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Integration.SiteState;

public static class SiteStateWebhookApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSiteStateWebhookProtocol(
        this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (!SiteStateWebhookRoute.Matches(context.Request.Path))
            {
                await next();
                return;
            }

            context.Response.Headers.CacheControl = "no-store";
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                context.Response.Headers.Allow = HttpMethods.Post;
                await context.Response.WriteAsJsonAsync(
                    new SiteStateWebhookResponse(
                        false,
                        "method_not_allowed"),
                    context.RequestAborted);
                return;
            }

            await next();
        });
    }

    public static IApplicationBuilder UseLegacySiteStatusWithWebhookBypass(
        this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (SiteStateWebhookRoute.Matches(context.Request.Path))
            {
                await next();
                return;
            }

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
            var cache = context.RequestServices.GetRequiredService<
                Microsoft.Extensions.Caching.Memory.IMemoryCache>();

            if (!cache.TryGetValue(cacheKey, out object? cachedStatus) ||
                cachedStatus is null)
            {
                var dealingApi = context.RequestServices
                    .GetRequiredService<DealingAPI>();
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
    }
}

public static class SiteStateWebhookServiceCollectionExtensions
{
    public static IServiceCollection AddSiteStateWebhook(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<
            IValidateOptions<SiteStateWebhookOptions>,
            SiteStateWebhookOptionsValidator>();
        services.AddOptions<SiteStateWebhookOptions>()
            .Bind(configuration.GetSection(SiteStateWebhookOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<ISiteStateWebhookAuthenticator,
            SiteStateWebhookAuthenticator>();
        services.AddSingleton<ISiteStateSnapshotV1JsonParser,
            SiteStateSnapshotV1JsonParser>();
        return services;
    }
}

public sealed record SiteStateWebhookResponse(
    bool Success,
    string Code,
    string? Outcome = null);
