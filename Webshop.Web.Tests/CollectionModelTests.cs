using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Webshop.Data.Services;
using Webshop.Shared.Models;

namespace Webshop.Web.Tests;

public class CollectionModelTests
{
    [Fact]
    public void Can_deserialize_collection_documents_with_id_field()
    {
        var bson = new BsonDocument
        {
            { "_id", "collection-1" },
            { "Number", 1 },
            { "Name", "GROUND WORK" },
            { "Tagline", "Where the archive started" },
            { "Season", "Vol. 01" },
            { "CoverImage", "https://example.com/cover.jpg" }
        };

        var collection = BsonSerializer.Deserialize<Collection>(bson);

        Assert.Equal("collection-1", collection.Id);
        Assert.Equal(1, collection.Number);
        Assert.Equal("GROUND WORK", collection.Name);
    }

    [Fact]
    public void Missing_connection_string_throws_invalid_operation_exception()
    {
        Assert.Throws<InvalidOperationException>(() => new MongoProductService(new ConfigurationBuilder().Build()));
    }
}
