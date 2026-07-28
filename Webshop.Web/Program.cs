using Webshop.Core.Interfaces;
using Webshop.Core.Services;
using Webshop.Data.Services;
using Webshop.Shared.Models;
using Webshop.Web.Components;
using Webshop.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// --- Dependency injection af vores egne services ---
builder.Services.AddScoped<IProductService, MongoProductService>();

// Kurven er også bag et interface (ICartService), så den fx senere kan
// gemmes et andet sted end i hukommelsen, uden at siderne der bruger den ændres.
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IAccountService, MongoAccountService>();
builder.Services.AddScoped<AdminContentStore>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/api/admin/products", async (IProductService store) =>
    Results.Ok(await store.GetAllProductsAsync()));

app.MapGet("/api/admin/collections", async (IProductService store) =>
    Results.Ok(await store.GetCollectionsAsync()));

app.MapPost("/api/admin/products", async (IProductService store, Product product) =>
{
    var created = await store.CreateProductAsync(product);
    return Results.Created($"/api/admin/products/{created.Id}", created);
});

app.MapPut("/api/admin/products/{id}", async (IProductService store, string id, Product product) =>
{
    product.Id = id;
    var updated = await store.UpdateProductAsync(product);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapDelete("/api/admin/products/{id}", async (IProductService store, string id) =>
{
    return await store.DeleteProductAsync(id) ? Results.NoContent() : Results.NotFound();
});

app.MapPost("/api/admin/collections", async (IProductService store, Collection collection) =>
{
    var created = await store.CreateCollectionAsync(collection);
    return Results.Created($"/api/admin/collections/{created.Number}", created);
});

app.MapPut("/api/admin/collections/{number}", async (IProductService store, int number, Collection collection) =>
{
    collection.Number = number;
    var updated = await store.UpdateCollectionAsync(collection);
    return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapDelete("/api/admin/collections/{number}", async (IProductService store, int number) =>
{
    return await store.DeleteCollectionAsync(number) ? Results.NoContent() : Results.NotFound();
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
