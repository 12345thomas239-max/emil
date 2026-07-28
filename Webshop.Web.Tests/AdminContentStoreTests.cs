using Webshop.Core.Interfaces;
using Webshop.Shared.Models;
using Webshop.Web.Services;

namespace Webshop.Web.Tests;

public class AdminContentStoreTests
{
    [Fact]
    public async Task AddProduct_DelegatesToProductService()
    {
        var productService = new StubProductService();
        var store = new AdminContentStore(productService);

        var product = new Product { Name = "Test product", Category = "Tops" };
        var created = await store.AddProduct(product);

        Assert.Same(product, created);
        Assert.Single(productService.CreatedProducts);
    }

    private sealed class StubProductService : IProductService
    {
        public List<Product> CreatedProducts { get; } = [];
        public List<Collection> CreatedCollections { get; } = [];

        public Task<List<Product>> GetAllProductsAsync() => Task.FromResult(new List<Product>());

        public Task<Product?> GetProductByIdAsync(string id) => Task.FromResult<Product?>(null);

        public Task<List<Product>> GetFeaturedProductsAsync() => Task.FromResult(new List<Product>());

        public Task<List<Product>> GetByCategoryAsync(string category) => Task.FromResult(new List<Product>());

        public Task<List<string>> GetCategoriesAsync() => Task.FromResult(new List<string>());

        public Task<List<Product>> GetRelatedProductsAsync(string productId, int count = 4) => Task.FromResult(new List<Product>());

        public Task<List<Collection>> GetCollectionsAsync() => Task.FromResult(new List<Collection>());

        public Task<Collection?> GetCollectionByNumberAsync(int number) => Task.FromResult<Collection?>(null);

        public Task<Collection?> GetLatestCollectionAsync() => Task.FromResult<Collection?>(null);

        public Task<List<Product>> GetProductsByCollectionAsync(int collectionNumber) => Task.FromResult(new List<Product>());

        public Task<Product> CreateProductAsync(Product product)
        {
            CreatedProducts.Add(product);
            return Task.FromResult(product);
        }

        public Task<Collection> CreateCollectionAsync(Collection collection)
        {
            CreatedCollections.Add(collection);
            return Task.FromResult(collection);
        }

        public Task<Product?> UpdateProductAsync(Product product) => Task.FromResult<Product?>(product);

        public Task<bool> DeleteProductAsync(string id) => Task.FromResult(true);

        public Task<Collection?> UpdateCollectionAsync(Collection collection) => Task.FromResult<Collection?>(collection);

        public Task<bool> DeleteCollectionAsync(int number) => Task.FromResult(true);
    }
}
