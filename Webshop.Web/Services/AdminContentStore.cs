using Webshop.Core.Interfaces;
using Webshop.Shared.Models;

namespace Webshop.Web.Services;

public sealed class AdminContentStore
{
    private readonly IProductService _productService;

    public AdminContentStore(IProductService productService)
    {
        _productService = productService;
    }

    public Task<Product> AddProduct(Product product) => _productService.CreateProductAsync(product);

    public Task<Collection> AddCollection(Collection collection) => _productService.CreateCollectionAsync(collection);
}
