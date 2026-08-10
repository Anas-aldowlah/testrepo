using System.Threading.Tasks;

namespace YAGOT_2._0.Services
{
    public interface IOtpService
    {
        Task<(bool Success, string Message, string? CodeForDev)> SendOtpAsync(string phone);
        Task<(bool Success, string Message)> VerifyOtpAsync(string phone, string code);
    }
}
