using Microsoft.Extensions.Options;

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
