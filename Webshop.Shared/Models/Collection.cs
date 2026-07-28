using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Webshop.Shared.Models;

/// <summary>
/// A single archived collection (drop). Collections are numbered 01–07,
/// oldest to newest. Frontend-facing model — maps 1:1 to a DB document later.
/// </summary>
public class Collection
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonElement("_id")]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonElement("Number")]
    public int Number { get; set; }

    [BsonElement("Name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("Tagline")]
    public string Tagline { get; set; } = string.Empty;

    [BsonElement("Season")]
    public string Season { get; set; } = string.Empty;

    [BsonElement("CoverImage")]
    public string CoverImage { get; set; } = string.Empty;
}
