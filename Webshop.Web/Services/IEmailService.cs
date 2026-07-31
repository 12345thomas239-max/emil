using Webshop.Shared.Models;

namespace Webshop.Web.Services;

public interface IEmailService
{
    Task SendOrderConfirmationAsync(string recipientEmail, OrderView order);
    Task SendPasswordResetAsync(string recipientEmail, string resetLink);
}
