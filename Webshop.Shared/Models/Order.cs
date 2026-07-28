namespace Webshop.Shared.Models;

/// <summary>
/// En ordre. Skal gemmes i minimum 5 år jf. bogføringsloven, derfor må den
/// IKKE afhænge direkte af Customer-collectionen, som skal kunne anonymiseres.
/// I stedet gemmes et "øjebliksbillede" af leveringsoplysninger på ordren selv.
/// </summary>
public class Order
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;

    // Snapshot af leverings-info på købstidspunktet - ændres ikke,
    // selvom kunden senere opdaterer eller sletter sin profil.
    public string DeliveryName { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string DeliveryCity { get; set; } = string.Empty;
    public string DeliveryPostalCode { get; set; } = string.Empty;

    public List<CartItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Cancelled
}
