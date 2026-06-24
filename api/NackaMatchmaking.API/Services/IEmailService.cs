using System.Threading.Tasks;

namespace NackaMatchmaking.API.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlContent);
    }
}
