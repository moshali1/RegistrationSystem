using MongoDB.Bson;
using MongoDB.Driver;

// ═══════════════════════════════════════════════════════════════════════════
// Clear StatusComment on Verified registrations
// ═══════════════════════════════════════════════════════════════════════════
// When a registration is verified, the old Pending comment is no longer
// relevant — the issues have been rectified. The audit trail preserves
// the full history. This script clears leftover comments on Verified regs.
// ═══════════════════════════════════════════════════════════════════════════

var connectionString = args.Length > 0 ? args[0]
    : "mongodb+srv://NASQCDatabaseAdmin:ooyDMDhmvSMPzMDD@shatibicompetition-clus.bc4jf.mongodb.net/?retryWrites=true&w=majority";
var databaseName = args.Length > 1 ? args[1] : "shatibi_dev_registration_db";

var client = new MongoClient(connectionString);
var database = client.GetDatabase(databaseName);
var registrations = database.GetCollection<BsonDocument>("registrations");

Console.WriteLine("=== Clear StatusComment on Verified Registrations ===");
Console.WriteLine($"Database: {databaseName}");
Console.WriteLine();

// Find Verified registrations (Status = 3) that have a non-null, non-empty StatusComment
var filter = Builders<BsonDocument>.Filter.And(
    Builders<BsonDocument>.Filter.Eq("Status", 3), // Verified
    Builders<BsonDocument>.Filter.Ne("StatusComment", BsonNull.Value),
    Builders<BsonDocument>.Filter.Ne("StatusComment", "")
);

var regs = await registrations.Find(filter).ToListAsync();
Console.WriteLine($"Found {regs.Count} Verified registrations with a StatusComment");
Console.WriteLine();

var clearedCount = 0;

foreach (var reg in regs)
{
    var id = reg["_id"].AsString;
    var comment = reg.Contains("StatusComment") ? reg["StatusComment"].AsString : "";
    var fullName = "";
    var cid = reg.Contains("Cid") ? reg["Cid"].AsString : "?";

    if (reg.Contains("PersonalInfo") && reg["PersonalInfo"].IsBsonDocument)
    {
        var pi = reg["PersonalInfo"].AsBsonDocument;
        var first = pi.Contains("FirstName") ? pi["FirstName"].AsString : "";
        var last = pi.Contains("LastName") ? pi["LastName"].AsString : "";
        fullName = $"{first} {last}".Trim();
    }

    await registrations.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("_id", id),
        Builders<BsonDocument>.Update.Set("StatusComment", BsonNull.Value));

    Console.WriteLine($"  Cleared: {fullName} ({cid}) — was: \"{comment}\"");
    clearedCount++;
}

Console.WriteLine();
Console.WriteLine($"Cleared: {clearedCount}");
Console.WriteLine("\n=== Done ===");
