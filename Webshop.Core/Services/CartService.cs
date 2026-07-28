using Webshop.Core.Interfaces;
using Webshop.Shared.Models;

namespace Webshop.Core.Services;

/// <summary>
/// Midlertidig kurv-implementering der kun gemmer i hukommelsen (per bruger-session,
/// da den registreres som Scoped i Program.cs). Fint til at bygge og teste
/// frontend-flowet. Skal senere evt. erstattes/udvides til at gemme kurven mere
/// holdbart (cookie/database), uden at siderne der bruger ICartService ændres.
/// </summary>
public class CartService : ICartService
{
    private readonly List<CartItem> _items = new();

    public event Action? OnChange;

    public IReadOnlyList<CartItem> Items => _items;

    public int TotalItemCount => _items.Sum(i => i.Quantity);

    public decimal TotalAmount => _items.Sum(i => i.LineTotal);

    public void AddItem(Product product, int quantity)
    {
        if (quantity <= 0) return;

        var existing = _items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            _items.Add(new CartItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = quantity
            });
        }

        NotifyStateChanged();
    }

    public void RemoveItem(string productId)
    {
        _items.RemoveAll(i => i.ProductId == productId);
        NotifyStateChanged();
    }

    public void UpdateQuantity(string productId, int quantity)
    {
        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing is null) return;

        if (quantity <= 0)
        {
            _items.Remove(existing);
        }
        else
        {
            existing.Quantity = quantity;
        }

        NotifyStateChanged();
    }

    public void Clear()
    {
        _items.Clear();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
