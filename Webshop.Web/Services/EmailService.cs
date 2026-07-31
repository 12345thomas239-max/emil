using Webshop.Shared.Models;

namespace Webshop.Web.Services;

public sealed class EmailService : IEmailService
{
    public Task SendOrderConfirmationAsync(string recipientEmail, OrderView order)
    {
        // Placeholder email implementation.
        Console.WriteLine($"[Email] Sending order confirmation to {recipientEmail}");
        Console.WriteLine($"Order {order.Number} confirmed with {order.ItemCount} items and total {order.Total:0} kr.");
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string recipientEmail, string resetLink)
    {
        // Placeholder email implementation for password reset.
        Console.WriteLine($"[Email] Sending password reset to {recipientEmail}");
        Console.WriteLine($"Reset link: {resetLink}");
        return Task.CompletedTask;
    }
}
