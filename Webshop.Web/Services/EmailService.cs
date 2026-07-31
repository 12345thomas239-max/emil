using Webshop.Shared.Models;

namespace Webshop.Web.Services;

public sealed class EmailService : IEmailService
{
    public Task SendOrderConfirmationAsync(string recipientEmail, OrderView order)
    {
        // Placeholder email implementation.
        // This does not send a real email yet; it only logs the intent.
        // Configure an SMTP/SendGrid provider later to make this send actual email.
        Console.WriteLine($"[Email] Sending order confirmation to {recipientEmail}");
        Console.WriteLine($"Order {order.Number} confirmed with {order.ItemCount} items and total {order.Total:0} kr.");
        return Task.CompletedTask;
    }
}
