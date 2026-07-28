namespace Webshop.Shared.Models;

/// <summary>
/// Et enkelt produkt i kunden kurv, med det antal kunden har valgt.
/// </summary>
public class CartItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
