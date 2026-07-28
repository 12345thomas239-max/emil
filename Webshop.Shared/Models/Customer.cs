namespace Webshop.Shared.Models;

/// <summary>
/// Personoplysninger på en kunde. Dette er den collection der skal kunne
/// anonymiseres/slettes, når en kunde beder om det (GDPR-ret til sletning).
/// </summary>
public class Customer
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;

    public List<ConsentRecord> Consents { get; set; } = new();
}

/// <summary>
/// Registrerer hvornår og hvad kunden har givet samtykke til (fx marketing-mails).
/// Selve købet kræver ikke samtykke, det er en kontrakt - men fx nyhedsbrev gør.
/// </summary>
public class ConsentRecord
{
    public ConsentType Type { get; set; }
    public bool Granted { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public enum ConsentType
{
    Marketing,
    Analytics
}
