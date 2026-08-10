namespace YAGOT_2._0.Services;

public interface IPasswordResetEmailSender
{
    Task SendResetLinkAsync(string email, string resetUrl, CancellationToken cancellationToken = default);
}
