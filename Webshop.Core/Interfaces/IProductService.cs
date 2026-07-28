using Webshop.Shared.Models;

namespace Webshop.Core.Interfaces;

/// <summary>
/// Contract for product-related business logic. Blazor pages ONLY talk to
/// this interface — they don't know whether data comes from fake data or MongoDB.
/// </summary>
public interface IProductService
{
    Task<List<Product>> GetAllProductsAsync();
    Task<Product?> GetProductByIdAsync(string id);
    Task<List<Product>> GetFeaturedProductsAsync();
    Task<List<Product>> GetByCategoryAsync(string category);
    Task<Product> CreateProductAsync(Product product);
    Task<Product?> UpdateProductAsync(Product product);
    Task<bool> DeleteProductAsync(string id);
    Task<Collection> CreateCollectionAsync(Collection collection);
    Task<Collection?> UpdateCollectionAsync(Collection collection);
    Task<bool> DeleteCollectionAsync(int number);

    /// <summary>
    /// Returns every category found across the products, in the order they
    /// should appear in the filter.
    /// </summary>
    Task<List<string>> GetCategoriesAsync();

    /// <summary>
    /// Used on the product page to suggest other pieces in the same category.
    /// </summary>
    Task<List<Product>> GetRelatedProductsAsync(string productId, int count = 4);

    /// <summary>
    /// All collections (01–07), ordered oldest to newest.
    /// </summary>
    Task<List<Collection>> GetCollectionsAsync();

    Task<Collection?> GetCollectionByNumberAsync(int number);

    /// <summary>
    /// The most recently dropped collection — used for the homepage CTA.
    /// </summary>
    Task<Collection?> GetLatestCollectionAsync();

    Task<List<Product>> GetProductsByCollectionAsync(int collectionNumber);
}
