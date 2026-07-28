using System.Security.Authentication;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Configuration;
using Webshop.Core.Interfaces;
using Webshop.Shared.Models;

namespace Webshop.Data.Services;

public sealed class MongoProductService : IProductService
{
    private readonly IMongoCollection<Product> _products;
    private readonly IMongoCollection<Collection> _collections;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public MongoProductService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDb")
            ?? configuration["MongoDb:ConnectionString"]
            ?? configuration["MongoDb__ConnectionString"]
            ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");

        var databaseName = configuration["MongoDb:DatabaseName"]
            ?? configuration["MongoDb__DatabaseName"]
            ?? "webshop";

        var allowInsecureTls = bool.TryParse(
            configuration["MongoDb:AllowInsecureTls"]
            ?? configuration["MongoDb__AllowInsecureTls"]
            ?? Environment.GetEnvironmentVariable("MONGODB_ALLOW_INSECURE_TLS"),
            out var parsedAllowInsecureTls) && parsedAllowInsecureTls;

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("MongoDB connection string is missing. Set MongoDb:ConnectionString or MONGODB_CONNECTION_STRING.");
        }

        try
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.AllowInsecureTls = allowInsecureTls;
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(30);
            settings.ConnectTimeout = TimeSpan.FromSeconds(30);
            settings.SslSettings = new SslSettings
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CheckCertificateRevocation = false,
                ClientCertificateSelectionCallback = null
            };

            var caBundlePath = Environment.GetEnvironmentVariable("SSL_CERT_FILE")
                ?? "/opt/homebrew/etc/openssl@3/cert.pem";
            if (!string.IsNullOrWhiteSpace(caBundlePath) && File.Exists(caBundlePath))
            {
                settings.SslSettings = new SslSettings
                {
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CheckCertificateRevocation = false,
                    ClientCertificateSelectionCallback = null
                };
            }

            var client = new MongoClient(settings);
            var database = client.GetDatabase(databaseName);
            _products = database.GetCollection<Product>("products");
            _collections = database.GetCollection<Collection>("collections");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to connect to MongoDB. Check the connection string, network access, and MongoDB server status.", ex);
        }
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        await EnsureSeededAsync();
        return await _products.Find(FilterDefinition<Product>.Empty).SortBy(p => p.Name).ToListAsync();
    }

    public async Task<Product?> GetProductByIdAsync(string id)
    {
        await EnsureSeededAsync();
        return await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
    }

    public async Task<List<Product>> GetFeaturedProductsAsync()
    {
        await EnsureSeededAsync();
        return await _products.Find(p => p.IsFeatured).SortBy(p => p.Name).ToListAsync();
    }

    public async Task<List<Product>> GetByCategoryAsync(string category)
    {
        await EnsureSeededAsync();
        if (string.IsNullOrWhiteSpace(category) || category == "All")
        {
            return await _products.Find(FilterDefinition<Product>.Empty).SortBy(p => p.Name).ToListAsync();
        }

        return await _products.Find(p => p.Category == category).SortBy(p => p.Name).ToListAsync();
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        await EnsureSeededAsync();
        var categories = await _products.Find(FilterDefinition<Product>.Empty)
            .Project(p => p.Category)
            .ToListAsync();

        var ordered = new[] { "Jackets", "Tops", "Bottoms", "Headwear", "Accessories" }
            .Where(c => categories.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToList();
        return ordered;
    }

    public async Task<List<Product>> GetRelatedProductsAsync(string productId, int count = 4)
    {
        await EnsureSeededAsync();
        var currentMongo = await _products.Find(p => p.Id == productId).FirstOrDefaultAsync();
        if (currentMongo is null)
        {
            return new List<Product>();
        }

        return await _products.Find(p => p.Id != productId && p.Category == currentMongo.Category)
            .Limit(count)
            .ToListAsync();
    }

    public async Task<List<Collection>> GetCollectionsAsync()
    {
        await EnsureSeededAsync();
        return await _collections.Find(FilterDefinition<Collection>.Empty).SortBy(c => c.Number).ToListAsync();
    }

    public async Task<Collection?> GetCollectionByNumberAsync(int number)
    {
        await EnsureSeededAsync();
        return await _collections.Find(c => c.Number == number).FirstOrDefaultAsync();
    }

    public async Task<Collection?> GetLatestCollectionAsync()
    {
        await EnsureSeededAsync();
        return await _collections.Find(FilterDefinition<Collection>.Empty).SortByDescending(c => c.Number).FirstOrDefaultAsync();
    }

    public async Task<List<Product>> GetProductsByCollectionAsync(int collectionNumber)
    {
        await EnsureSeededAsync();
        return await _products.Find(p => p.CollectionNumber == collectionNumber).SortBy(p => p.Name).ToListAsync();
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        await EnsureSeededAsync();
        if (string.IsNullOrWhiteSpace(product.Id))
        {
            product.Id = Guid.NewGuid().ToString("N");
        }

        product.GalleryImages = product.GalleryImages?.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        if (string.IsNullOrWhiteSpace(product.ImageUrl) && product.GalleryImages.Count > 0)
        {
            product.ImageUrl = product.GalleryImages[0];
        }

        await _products.InsertOneAsync(product);
        return product;
    }

    public async Task<Collection> CreateCollectionAsync(Collection collection)
    {
        await EnsureSeededAsync();
        if (collection.Number <= 0)
        {
            var highest = await _collections.Find(FilterDefinition<Collection>.Empty).SortByDescending(c => c.Number).FirstOrDefaultAsync();
            collection.Number = (highest?.Number ?? 0) + 1;
        }

        if (string.IsNullOrWhiteSpace(collection.Id))
        {
            collection.Id = ObjectId.GenerateNewId().ToString();
        }

        await _collections.InsertOneAsync(collection);
        return collection;
    }

    public async Task<Product?> UpdateProductAsync(Product product)
    {
        await EnsureSeededAsync();
        var updateResult = await _products.ReplaceOneAsync(p => p.Id == product.Id, product);
        return updateResult.MatchedCount == 0 ? null : product;
    }

    public async Task<bool> DeleteProductAsync(string id)
    {
        await EnsureSeededAsync();
        var deleteResult = await _products.DeleteOneAsync(p => p.Id == id);
        return deleteResult.DeletedCount > 0;
    }

    public async Task<Collection?> UpdateCollectionAsync(Collection collection)
    {
        await EnsureSeededAsync();

        if (string.IsNullOrWhiteSpace(collection.Id))
        {
            var existing = await _collections.Find(c => c.Number == collection.Number).FirstOrDefaultAsync();
            if (existing is not null)
            {
                collection.Id = existing.Id;
            }
        }

        var updateResult = await _collections.ReplaceOneAsync(c => c.Number == collection.Number, collection);
        return updateResult.MatchedCount == 0 ? null : collection;
    }

    public async Task<bool> DeleteCollectionAsync(int number)
    {
        await EnsureSeededAsync();
        var deleteResult = await _collections.DeleteOneAsync(c => c.Number == number);
        return deleteResult.DeletedCount > 0;
    }

    private async Task EnsureSeededAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            var collectionsCount = await _collections.CountDocumentsAsync(FilterDefinition<Collection>.Empty);
            if (collectionsCount == 0)
            {
                await _collections.InsertManyAsync(GetSeedCollections());
            }

            var productsCount = await _products.CountDocumentsAsync(FilterDefinition<Product>.Empty);
            if (productsCount == 0)
            {
                await _products.InsertManyAsync(GetSeedProducts());
            }

            _initialized = true;
        }
        catch (MongoAuthenticationException ex)
        {
            throw new InvalidOperationException("MongoDB authentication failed. Check the username and password in MONGODB_CONNECTION_STRING.", ex);
        }
        catch (MongoConnectionException ex)
        {
            throw new InvalidOperationException("MongoDB connection failed. Check the connection string, network access, and Atlas IP access settings.", ex);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static List<Collection> GetSeedCollections() => new()
    {
        new Collection { Number = 1, Name = "GROUND WORK", Season = "Vol. 01", Tagline = "Where the archive started — raw seams, no polish.", CoverImage = "https://picsum.photos/seed/osarkiv-c01-cover/1000/1250" },
        new Collection { Number = 2, Name = "STATIC", Season = "Vol. 02", Tagline = "Grain, noise, camo. Shot on the way out the door.", CoverImage = "https://picsum.photos/seed/osarkiv-c02-cover/1000/1250" },
        new Collection { Number = 3, Name = "EGO DEATH", Season = "Vol. 03", Tagline = "Dark prints, gothic type, pieces that bite back.", CoverImage = "https://picsum.photos/seed/osarkiv-c03-cover/1000/1250" },
        new Collection { Number = 4, Name = "NIGHT SHIFT", Season = "Vol. 04", Tagline = "Studs, buckles, leather worked by hand after hours.", CoverImage = "https://picsum.photos/seed/osarkiv-c04-cover/1000/1250" },
        new Collection { Number = 5, Name = "FIELD NOTES", Season = "Vol. 05", Tagline = "Utility fabric pulled apart and reassembled.", CoverImage = "https://picsum.photos/seed/osarkiv-c05-cover/1000/1250" },
        new Collection { Number = 6, Name = "GREYSCALE", Season = "Vol. 06", Tagline = "Everything reduced to tone — knit, wool, shadow.", CoverImage = "https://picsum.photos/seed/osarkiv-c06-cover/1000/1250" },
        new Collection { Number = 7, Name = "LATEST DROP", Season = "Vol. 07 — Current", Tagline = "The newest case file. Still being catalogued.", CoverImage = "https://picsum.photos/seed/osarkiv-c07-cover/1000/1250" }
    };

    private static List<Product> GetSeedProducts() => new()
    {
        new Product { Id = "1", ArchiveNumber = "ARK-014", CollectionNumber = 1, Name = "Reworked Denim Jacket", Category = "Jackets", Description = "Oversized denim jacket pieced together from three worn-out jackets sourced at Copenhagen flea markets. Visible patchwork seams, hand-embroidered detail on the back.", OriginStory = "The fabric comes from three different '90s denim jackets, otherwise discarded for sleeve wear.", Price = 1295m, Size = "M", Color = "Blue / light blue", Material = "100% recycled denim", StockQuantity = 1, IsOneOfOne = true, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-014/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-014/900/1150", "https://picsum.photos/seed/ark-014b/900/1150", "https://picsum.photos/seed/ark-014c/900/1150" } },
        new Product { Id = "2", ArchiveNumber = "ARK-021", CollectionNumber = 1, Name = "Patchwork Shirt Dress", Category = "Bottoms", Description = "Long shirt-dress assembled from five different patterned men's shirts. Adjustable tie belt at the waist.", OriginStory = "The shirts were collected at clothing-swap nights and sewn into one new pattern.", Price = 1095m, Size = "S/M", Color = "Multi", Material = "Recycled cotton shirting", StockQuantity = 1, IsOneOfOne = true, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-021/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-021/900/1150", "https://picsum.photos/seed/ark-021b/900/1150" } },
        new Product { Id = "3", ArchiveNumber = "ARK-007", CollectionNumber = 1, Name = "Re-Embroidered Knit Top", Category = "Tops", Description = "Fine '80s wool knit, rescued and hand-embroidered with a new graphic motif in contrast yarn.", OriginStory = "The base knit came from a donated wardrobe; the embroidery was added in our workshop.", Price = 695m, Size = "L", Color = "Cream / rust", Material = "Recycled wool, cotton thread", StockQuantity = 2, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-007/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-007/900/1150", "https://picsum.photos/seed/ark-007b/900/1150" } },
        new Product { Id = "4", ArchiveNumber = "ARK-062", CollectionNumber = 1, Name = "Woven Shoulder Bag", Category = "Accessories", Description = "Shoulder bag woven from offcut strips of surplus leather and old seatbelt webbing.", OriginStory = "The belts were donated by a scrapyard, the leather is offcut from a furniture upholsterer.", Price = 495m, Size = "One Size", Color = "Black / brown", Material = "Recycled leather, seatbelt webbing", StockQuantity = 2, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-062/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-062/900/1150", "https://picsum.photos/seed/ark-062b/900/1150" } },
        new Product { Id = "5", ArchiveNumber = "ARK-033", CollectionNumber = 2, Name = "Camo Wide-Leg Cargos", Category = "Bottoms", Description = "Classic camo cargo trousers re-cut into a wide, dropped leg, extra utility pockets added in contrast fabric.", OriginStory = "Built from a batch of surplus workwear trousers from a Danish supplier.", Price = 895m, Size = "M", Color = "Camo / olive", Material = "Recycled cotton/poly ripstop", StockQuantity = 3, IsOneOfOne = false, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-033/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-033/900/1150", "https://picsum.photos/seed/ark-033b/900/1150" } },
        new Product { Id = "6", ArchiveNumber = "ARK-070", CollectionNumber = 2, Name = "Reworked Football Jersey", Category = "Tops", Description = "Vintage club jersey with a hand-applied graphic overprint and re-set sleeves for a boxier fit.", OriginStory = "Sourced as a single deadstock jersey found at a market stall — one shirt, one piece.", Price = 595m, Size = "M/L", Color = "Blue / white", Material = "Recycled polyester", StockQuantity = 1, IsOneOfOne = true, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-070/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-070/900/1150", "https://picsum.photos/seed/ark-070b/900/1150" } },
        new Product { Id = "7", ArchiveNumber = "ARK-071", CollectionNumber = 2, Name = "Hooded Utility Overshirt", Category = "Jackets", Description = "Boxy hooded overshirt built from a deconstructed workwear jacket, raw-edge hood added by hand.", OriginStory = "Base garment came from a surplus lot too damaged to resell as-is.", Price = 990m, Size = "L", Color = "Charcoal", Material = "Recycled cotton twill", StockQuantity = 2, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-071/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-071/900/1150", "https://picsum.photos/seed/ark-071b/900/1150" } },
        new Product { Id = "8", ArchiveNumber = "ARK-072", CollectionNumber = 2, Name = "Blazer, Re-Cut", Category = "Jackets", Description = "Tailored blazer taken apart and rebuilt with a dropped shoulder and shortened body.", OriginStory = "Originally two mismatched blazers — the better panels of each were combined into one.", Price = 1150m, Size = "M", Color = "Sand / beige", Material = "Recycled wool blend", StockQuantity = 1, IsOneOfOne = true, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-072/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-072/900/1150", "https://picsum.photos/seed/ark-072b/900/1150" } },
        new Product { Id = "9", ArchiveNumber = "ARK-080", CollectionNumber = 3, Name = "\"Ego Death\" Graphic Tee", Category = "Tops", Description = "Heavyweight tee with a hand-drawn gothic print across the chest. Garment-dyed for a worn-in finish.", OriginStory = "Printed in small batches on deadstock blanks left over from a closed workshop.", Price = 450m, Size = "L", Color = "Black", Material = "Recycled cotton jersey", StockQuantity = 4, IsOneOfOne = false, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-080/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-080/900/1150", "https://picsum.photos/seed/ark-080b/900/1150" } },
        new Product { Id = "10", ArchiveNumber = "ARK-081", CollectionNumber = 3, Name = "Marker Tag Tee", Category = "Tops", Description = "Raw white tee scrawled with an original marker-tag graphic, sealed by hand so it won't fade.", OriginStory = "Each tag is drawn freehand — no two tees carry the exact same marks.", Price = 420m, Size = "M", Color = "White / black", Material = "Recycled cotton jersey", StockQuantity = 1, IsOneOfOne = true, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-081/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-081/900/1150" } },
        new Product { Id = "11", ArchiveNumber = "ARK-082", CollectionNumber = 3, Name = "Quilted Vest", Category = "Jackets", Description = "Sleeveless quilted vest sewn from scrap fabric in ten different tones — each vest is unique in pattern.", OriginStory = "Offcuts from previous collections — nothing bigger than a palm goes to waste.", Price = 850m, Size = "L", Color = "Multi", Material = "Fabric scraps, recycled wadding", StockQuantity = 1, IsOneOfOne = true, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-082/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-082/900/1150", "https://picsum.photos/seed/ark-082b/900/1150" } },
        new Product { Id = "12", ArchiveNumber = "ARK-083", CollectionNumber = 3, Name = "Studded Trucker Cap", Category = "Headwear", Description = "Black six-panel cap finished with a hand-applied gothic patch and raised metal studs.", OriginStory = "Blank caps sourced from a discontinued run, patched and studded in-house.", Price = 380m, Size = "One Size", Color = "Black", Material = "Recycled cotton twill, metal studs", StockQuantity = 3, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-083/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-083/900/1150" } },
        new Product { Id = "13", ArchiveNumber = "ARK-090", CollectionNumber = 4, Name = "Grommet Leather Cuff", Category = "Accessories", Description = "Wide leather cuff hand-punched with a dense grommet grid, adjustable buckle closure.", OriginStory = "Cut from a single reclaimed leather jacket too damaged to wear as-is.", Price = 320m, Size = "One Size", Color = "Black", Material = "Recycled leather, metal grommets", StockQuantity = 2, IsOneOfOne = false, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-090/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-090/900/1150", "https://picsum.photos/seed/ark-090b/900/1150" } },
        new Product { Id = "14", ArchiveNumber = "ARK-019", CollectionNumber = 4, Name = "Patchwork Bomber Jacket", Category = "Jackets", Description = "Cropped bomber in patchworked recycled leather and cotton, quilted lining made from old bedspreads.", OriginStory = "The leather panels are offcuts from a furniture upholsterer, the lining an upcycled quilt.", Price = 1690m, Size = "M/L", Color = "Brown / black", Material = "Recycled leather, cotton", StockQuantity = 1, IsOneOfOne = true, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-019/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-019/900/1150", "https://picsum.photos/seed/ark-019b/900/1150" } },
        new Product { Id = "15", ArchiveNumber = "ARK-091", CollectionNumber = 4, Name = "Zip Hoodie, Distressed", Category = "Tops", Description = "Heavyweight zip hoodie, hand-distressed and re-stitched at the seams with visible contrast thread.", OriginStory = "Base hoodie from a returns pallet, damage turned into the main design detail.", Price = 720m, Size = "L", Color = "Washed black", Material = "Recycled cotton fleece", StockQuantity = 2, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-091/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-091/900/1150", "https://picsum.photos/seed/ark-091b/900/1150" } },
        new Product { Id = "16", ArchiveNumber = "ARK-092", CollectionNumber = 4, Name = "Buckled Watch Strap", Category = "Accessories", Description = "Double-buckle wrist strap cut from reclaimed leather, worn stacked like the archive's field notes.", OriginStory = "Cut from the same hide as ARK-090, kept as a matching pair.", Price = 180m, Size = "One Size", Color = "Black", Material = "Recycled leather", StockQuantity = 3, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-092/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-092/900/1150" } },
        new Product { Id = "17", ArchiveNumber = "ARK-050", CollectionNumber = 5, Name = "Sashiko-Patched Jeans", Category = "Bottoms", Description = "Vintage Levi's 501s with visible sashiko-style patches at the knees and a turned-up hem.", OriginStory = "The jeans' original holes are hand-repaired using traditional Japanese sashiko stitching.", Price = 995m, Size = "29/32", Color = "Dark denim", Material = "Recycled denim", StockQuantity = 1, IsOneOfOne = true, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-050/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-050/900/1150", "https://picsum.photos/seed/ark-050b/900/1150" } },
        new Product { Id = "18", ArchiveNumber = "ARK-028", CollectionNumber = 5, Name = "Oversized Cotton Shirt", Category = "Tops", Description = "Loose men's shirt re-cut to an oversized silhouette, with a visible contrast seam at the shoulders.", OriginStory = "The shirt comes fra a batch of surplus stock from a Danish shirt factory.", Price = 595m, Size = "One Size", Color = "White", Material = "Recycled cotton", StockQuantity = 4, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-028/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-028/900/1150", "https://picsum.photos/seed/ark-028b/900/1150" } },
        new Product { Id = "19", ArchiveNumber = "ARK-051", CollectionNumber = 5, Name = "Woven Wool Shawl", Category = "Accessories", Description = "Oversized shawl braided from strips cut out of four worn-out wool blankets.", OriginStory = "The blankets came from a recycling station, too worn to resell whole.", Price = 395m, Size = "One Size", Color = "Multi / warm tones", Material = "Recycled wool", StockQuantity = 3, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-055/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-055/900/1150" } },
        new Product { Id = "20", ArchiveNumber = "ARK-052", CollectionNumber = 5, Name = "Field Cargo Vest", Category = "Jackets", Description = "Multi-pocket utility vest built from a cut-down surplus jacket, worn open over knitwear.", OriginStory = "Base fabric is offcut canvas from a tent-repair workshop.", Price = 780m, Size = "M", Color = "Olive", Material = "Recycled canvas", StockQuantity = 2, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-052/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-052/900/1150", "https://picsum.photos/seed/ark-052b/900/1150" } },
        new Product { Id = "21", ArchiveNumber = "ARK-038", CollectionNumber = 6, Name = "Asymmetric Midi Skirt", Category = "Bottoms", Description = "Asymmetric wool-blend skirt pieced together from two different blazers, cut open and rejoined.", OriginStory = "The two blazers couldn't be sold individually due to lining damage — the outer fabric was untouched.", Price = 750m, Size = "M", Color = "Grey / black", Material = "Recycled wool blend", StockQuantity = 1, IsOneOfOne = true, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-038/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-038/900/1150", "https://picsum.photos/seed/ark-038b/900/1150" } },
        new Product { Id = "22", ArchiveNumber = "ARK-044", CollectionNumber = 6, Name = "Back-Print Knit Sweater", Category = "Tops", Description = "Heavy grey knit with a photographic back-print, finished raw at the collar and cuffs.", OriginStory = "Knit base sourced from a discontinued wool run, print applied in-house.", Price = 690m, Size = "L", Color = "Grey", Material = "Recycled wool blend", StockQuantity = 2, IsOneOfOne = false, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-044/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-044/900/1150", "https://picsum.photos/seed/ark-044b/900/1150" } },
        new Product { Id = "23", ArchiveNumber = "ARK-045", CollectionNumber = 6, Name = "Draped Satin Dress", Category = "Bottoms", Description = "Short, draped dress sewn from surplus satin from a local bridal designer.", OriginStory = "The offcuts come from cutting three wedding dresses — none of the fabric goes to waste.", Price = 1450m, Size = "S", Color = "Ivory", Material = "Surplus satin", StockQuantity = 1, IsOneOfOne = true, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-041/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-041/900/1150", "https://picsum.photos/seed/ark-041b/900/1150" } },
        new Product { Id = "24", ArchiveNumber = "ARK-101", CollectionNumber = 7, Name = "Angel Wing Sweatshirt", Category = "Tops", Description = "Heavyweight crewneck with a raw hand-stitched wing motif across the back, distressed hem.", OriginStory = "First piece catalogued in this drop — full origin notes still being written up.", Price = 780m, Size = "M", Color = "Off-white", Material = "Recycled cotton fleece", StockQuantity = 2, IsOneOfOne = false, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-101/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-101/900/1150", "https://picsum.photos/seed/ark-101b/900/1150" } },
        new Product { Id = "25", ArchiveNumber = "ARK-102", CollectionNumber = 7, Name = "Reworked Track Jacket", Category = "Jackets", Description = "Colour-blocked track jacket rebuilt from two mismatched vintage shells.", OriginStory = "Currently being catalogued — check back for the full write-up.", Price = 890m, Size = "M/L", Color = "Navy / red", Material = "Recycled nylon", StockQuantity = 1, IsOneOfOne = true, IsFeatured = true, ImageUrl = "https://picsum.photos/seed/ark-102/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-102/900/1150", "https://picsum.photos/seed/ark-102b/900/1150" } },
        new Product { Id = "26", ArchiveNumber = "ARK-103", CollectionNumber = 7, Name = "Chain Detail Cap", Category = "Headwear", Description = "Low-crown cap finished with a hand-fixed chain detail across the brim.", OriginStory = "Currently being catalogued — check back for the full write-up.", Price = 340m, Size = "One Size", Color = "Black", Material = "Recycled cotton twill, metal chain", StockQuantity = 3, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-103/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-103/900/1150" } },
        new Product { Id = "27", ArchiveNumber = "ARK-104", CollectionNumber = 7, Name = "Layered Mesh Top", Category = "Tops", Description = "Sheer mesh layering top hand-seamed from deadstock sports mesh.", OriginStory = "Currently being catalogued — check back for the full write-up.", Price = 460m, Size = "S/M", Color = "Black", Material = "Recycled polyester mesh", StockQuantity = 2, IsOneOfOne = false, IsFeatured = false, ImageUrl = "https://picsum.photos/seed/ark-104/900/1150", GalleryImages = new() { "https://picsum.photos/seed/ark-104/900/1150" } }
    };
}
