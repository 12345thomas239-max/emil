using Webshop.Shared.Models;

namespace Webshop.Core.Interfaces;

/// <summary>
/// Kontrakten for kurv-logik. Ligesom IProductService taler Blazor-siderne
/// KUN med denne interface. Den nuværende implementering (CartService)
/// gemmer kun i hukommelsen for den aktuelle bruger-session (Blazor Server
/// scoped service) - når I evt. vil gemme kurven på tværs af sessioner
/// (fx i en cookie eller database), er det kun implementeringen der skiftes.
/// </summary>
public interface ICartService
{
    /// <summary>
    /// Rejses hver gang kurven ændres, så UI'et (fx kurv-ikon i header) kan opdatere sig selv.
    /// </summary>
    event Action? OnChange;

    IReadOnlyList<CartItem> Items { get; }

    int TotalItemCount { get; }

    decimal TotalAmount { get; }

    void AddItem(Product product, int quantity);

    void RemoveItem(string productId);

    void UpdateQuantity(string productId, int quantity);

    void Clear();
}
