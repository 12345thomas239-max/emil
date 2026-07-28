using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Webshop.Shared.Models;

public sealed record AccountView(
    string FirstName,
    string LastName,
    string Phone,
    string Email,
    DateTime MemberSince,
    bool IsAdmin,
    IReadOnlyList<OrderView> Orders,
    string Address = "",
    string PostalCode = "",
    string City = "")
{
    public string FullName => $"{FirstName} {LastName}";
}

public sealed record OrderView(
    string Id,
    string Number,
    DateTime OrderedAt,
    string Status,
    string Fulfillment,
    int ItemCount,
    decimal Total,
    IReadOnlyList<OrderItemView> Items,
    IReadOnlyList<ReceiptLineView> ReceiptLines);

public sealed record OrderItemView(string Name, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public sealed record ReceiptLineView(string Label, decimal Amount);

public sealed record AccountRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordSaltBase64 { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int PasswordIterations { get; set; }
    public DateTime MemberSince { get; set; }
    public bool IsAdmin { get; set; }
    public string Address { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public List<OrderView> Orders { get; set; } = new();
}
