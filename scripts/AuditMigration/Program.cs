using MongoDB.Bson;
using MongoDB.Driver;

// ═══════════════════════════════════════════════════════════════════════════
// Fix CID audit entry timestamps to match DateTimeOffset BSON format
// ═══════════════════════════════════════════════════════════════════════════
// The C# MongoDB driver serializes DateTimeOffset as a document:
//   { DateTime: ISODate(...), Ticks: NumberLong(...), Offset: 0 }
// The migration script used plain BsonDateTime which doesn't deserialize
// back to DateTimeOffset. This script converts them to the correct format.
// ═══════════════════════════════════════════════════════════════════════════

var connectionString = args.Length > 0 ? args[0]
    : "mongodb+srv://NASQCDatabaseAdmin:ooyDMDhmvSMPzMDD@shatibicompetition-clus.bc4jf.mongodb.net/?retryWrites=true&w=majority";
var databaseName = args.Length > 1 ? args[1] : "shatibi_dev_registration_db";

var client = new MongoClient(connectionString);
var database = client.GetDatabase(databaseName);
var auditEntries = database.GetCollection<BsonDocument>("auditEntries");

Console.WriteLine("=== Fix CID Audit Entry Timestamps ===");
Console.WriteLine($"Database: {databaseName}");
Console.WriteLine();

// Find all CID reassignment audit entries
var filter = Builders<BsonDocument>.Filter.And(
    Builders<BsonDocument>.Filter.Eq("Action", 50),
    Builders<BsonDocument>.Filter.Regex("Summary", new BsonRegularExpression("CID reassigned"))
);

var entries = await auditEntries.Find(filter).ToListAsync();
Console.WriteLine($"Found {entries.Count} CID reassignment audit entries");

var fixedCount = 0;

foreach (var entry in entries)
{
    var ts = entry["Timestamp"];

    // If it's a plain BsonDateTime, convert to the DateTimeOffset document format
    if (ts.BsonType == BsonType.DateTime)
    {
        var dt = ts.ToUniversalTime();
        var dto = new DateTimeOffset(dt, TimeSpan.Zero);
        var ticks = dto.Ticks;

        // Build the DateTimeOffset BSON document that C# driver expects
        var tsDoc = new BsonDocument
        {
            ["DateTime"] = new BsonDateTime(dt),
            ["Ticks"] = ticks,
            ["Offset"] = 0
        };

        await auditEntries.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", entry["_id"].AsString),
            Builders<BsonDocument>.Update.Set("Timestamp", tsDoc)
        );

        var summary = entry["Summary"].AsString;
        Console.WriteLine($"  Fixed: {summary}");
        fixedCount++;
    }
    else if (ts.BsonType == BsonType.Document)
    {
        Console.WriteLine($"  Already correct format: {entry["Summary"].AsString}");
    }
    else
    {
        Console.WriteLine($"  Unexpected type {ts.BsonType}: {entry["Summary"].AsString}");
    }
}

Console.WriteLine();
Console.WriteLine($"Timestamps fixed: {fixedCount}");
Console.WriteLine("\n=== Done ===");
