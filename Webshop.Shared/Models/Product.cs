using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Webshop.Shared.Models;

/// <summary>
/// Represents a single product (reworked/upcycled clothing, one-of-one archive
/// piece) in the webshop.
/// NOTE: this is a frontend-driven extension of the model — the fields here
/// are what the UI needs to render. When Webshop.Data is built, these fields
/// just get mapped onto the real storage (Mongo document); nothing in
/// Webshop.Web needs to change.
/// </summary>
public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Short, "human" catalog number shown in the UI, e.g. "ARK-014".
    /// Separate from Id, which is the technical key.
    /// </summary>
    [BsonElement("ArchiveNumber")]
    public string ArchiveNumber { get; set; } = string.Empty;

    [BsonElement("Name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category, used for filtering on the Shop page, e.g. "Jackets", "Tops".
    /// </summary>
    [BsonElement("Category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Which numbered collection (01–07) this piece belongs to.
    /// </summary>
    [BsonElement("CollectionNumber")]
    public int CollectionNumber { get; set; }

    [BsonElement("Price")]
    public decimal Price { get; set; }

    [BsonElement("Size")]
    public string Size { get; set; } = string.Empty;

    [BsonElement("Color")]
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// What recycled material(s) the piece is made from / reworked out of.
    /// </summary>
    [BsonElement("Material")]
    public string Material { get; set; } = string.Empty;

    /// <summary>
    /// Short note on where the fabric/piece came from, e.g. "Reworked from
    /// three denim jackets found at flea markets."
    /// </summary>
    [BsonElement("OriginStory")]
    public string OriginStory { get; set; } = string.Empty;

    [BsonElement("StockQuantity")]
    public int StockQuantity { get; set; }

    /// <summary>
    /// True if the piece is one-of-a-kind (typical for reworked/upcycled pieces).
    /// </summary>
    [BsonElement("IsOneOfOne")]
    public bool IsOneOfOne { get; set; }

    /// <summary>
    /// True if the product should be featured on the homepage.
    /// </summary>
    [BsonElement("IsFeatured")]
    public bool IsFeatured { get; set; }

    /// <summary>
    /// Primary image (used in grids/cards).
    /// </summary>
    [BsonElement("ImageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Extra images for the product page gallery. Always contains at least ImageUrl.
    /// </summary>
    [BsonElement("GalleryImages")]
    public List<string> GalleryImages { get; set; } = new();
}
