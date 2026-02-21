using MongoDB.Bson;
using MongoDB.Driver;

// ═══════════════════════════════════════════════════════════════════════════
// Reseed CID counters from actual registration data
// ═══════════════════════════════════════════════════════════════════════════
// Scans all registrations, finds the max sequence per CID prefix,
// and updates/creates counter documents so future CIDs are unique.
// Also fixes any duplicate CIDs found (assigns next available sequence).
// ═══════════════════════════════════════════════════════════════════════════

var connectionString = args.Length > 0 ? args[0]
    : "mongodb+srv://NASQCDatabaseAdmin:ooyDMDhmvSMPzMDD@shatibicompetition-clus.bc4jf.mongodb.net/?retryWrites=true&w=majority";
var databaseName = args.Length > 1 ? args[1] : "shatibi_dev_registration_db";

var client = new MongoClient(connectionString);
var database = client.GetDatabase(databaseName);
var registrations = database.GetCollection<BsonDocument>("registrations");
var counters = database.GetCollection<BsonDocument>("counters");
var auditEntries = database.GetCollection<BsonDocument>("auditEntries");

Console.WriteLine("=== Reseed CID Counters & Fix Duplicates ===");
Console.WriteLine($"Database: {databaseName}");
Console.WriteLine();

// ── Step 1: Load all registrations with CIDs ──────────────────────────────

var allRegs = await registrations.Find(
    Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Ne("Cid", BsonNull.Value),
        Builders<BsonDocument>.Filter.Ne("Cid", "")
    ))
    .Project(Builders<BsonDocument>.Projection
        .Include("_id").Include("Cid").Include("CompetitionYear")
        .Include("PersonalInfo.FirstName").Include("PersonalInfo.LastName"))
    .ToListAsync();

Console.WriteLine($"Found {allRegs.Count} registrations with CIDs");

// ── Step 2: Parse CIDs and find max sequence per (year, prefix) ───────────

// CID format: {prefix}{sequence} where prefix is letters (e.g. "M9") and sequence is digits
var cidData = new List<(string Id, string Cid, int Year, string Prefix, int Seq, string Name)>();

foreach (var reg in allRegs)
{
    var cid = reg["Cid"].AsString;
    var year = reg["CompetitionYear"].AsInt32;
    var id = reg["_id"].AsString;
    var firstName = reg["PersonalInfo"]["FirstName"].AsString;
    var lastName = reg["PersonalInfo"]["LastName"].AsString;

    // Split CID into prefix (letters) and sequence (digits from the end)
    var seqStart = cid.Length;
    while (seqStart > 0 && char.IsDigit(cid[seqStart - 1]))
        seqStart--;

    if (seqStart == cid.Length || seqStart == 0) continue; // no digits or no prefix

    var prefix = cid[..seqStart];
    if (int.TryParse(cid[seqStart..], out var seq))
    {
        cidData.Add((id, cid, year, prefix, seq, $"{firstName} {lastName}"));
    }
}

// Group by (year, prefix) and find max
var maxByPrefix = cidData
    .GroupBy(c => (c.Year, c.Prefix))
    .ToDictionary(g => g.Key, g => g.Max(c => c.Seq));

Console.WriteLine($"\nFound {maxByPrefix.Count} unique (year, prefix) combinations:");
foreach (var kvp in maxByPrefix.OrderBy(k => k.Key.Year).ThenBy(k => k.Key.Prefix))
{
    Console.WriteLine($"  {kvp.Key.Year}:{kvp.Key.Prefix} → max sequence = {kvp.Value}");
}

// ── Step 3: Find and fix duplicate CIDs ───────────────────────────────────

var duplicates = cidData
    .GroupBy(c => c.Cid)
    .Where(g => g.Count() > 1)
    .ToList();

Console.WriteLine($"\nDuplicate CIDs found: {duplicates.Count} groups");

var fixedDuplicates = 0;
var now = DateTime.UtcNow;

foreach (var dupGroup in duplicates)
{
    var members = dupGroup.OrderBy(d => d.Id).ToList();
    Console.WriteLine($"\n  CID {dupGroup.Key}: {members.Count} registrations");

    // Keep the first one, reassign the rest
    var keeper = members[0];
    Console.WriteLine($"    Keep: {keeper.Name} ({keeper.Id})");

    for (var i = 1; i < members.Count; i++)
    {
        var dup = members[i];
        var key = (dup.Year, dup.Prefix);

        // Increment max and use it
        maxByPrefix[key] = maxByPrefix[key] + 1;
        var newSeq = maxByPrefix[key];
        var newCid = $"{dup.Prefix}{newSeq:D3}";

        Console.WriteLine($"    Fix:  {dup.Name} ({dup.Id}) → {newCid}");

        // Update registration
        await registrations.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", dup.Id),
            Builders<BsonDocument>.Update.Set("Cid", newCid));

        // Create audit entry with proper DateTimeOffset format
        var dto = new DateTimeOffset(now, TimeSpan.Zero);
        var auditEntry = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId().ToString(),
            ["EntityType"] = "Registration",
            ["EntityId"] = dup.Id,
            ["Action"] = 50, // ManualCorrection
            ["Summary"] = $"CID reassigned from {dup.Cid} to {newCid} (duplicate fix)",
            ["Timestamp"] = new BsonDocument
            {
                ["DateTime"] = new BsonDateTime(now),
                ["Ticks"] = dto.Ticks,
                ["Offset"] = 0
            },
            ["UserId"] = "system",
            ["UserDisplayName"] = "System Migration",
            ["Changes"] = new BsonArray
            {
                new BsonDocument
                {
                    ["FieldName"] = "Cid",
                    ["DisplayName"] = "Competitor ID",
                    ["OldValue"] = dup.Cid,
                    ["NewValue"] = newCid
                }
            }
        };

        await auditEntries.InsertOneAsync(auditEntry);
        fixedDuplicates++;
    }
}

// ── Step 4: Reseed all counters ───────────────────────────────────────────

Console.WriteLine($"\n── Reseeding counters ──");
var seeded = 0;

foreach (var kvp in maxByPrefix)
{
    var counterId = $"{kvp.Key.Year}:{kvp.Key.Prefix}";
    var maxSeq = kvp.Value;

    // Use $max to only increase, never decrease (safe with concurrent app usage)
    var result = await counters.UpdateOneAsync(
        Builders<BsonDocument>.Filter.Eq("_id", counterId),
        Builders<BsonDocument>.Update.Max("seq", maxSeq),
        new UpdateOptions { IsUpsert = true });

    var action = result.UpsertedId != null ? "Created" : (result.ModifiedCount > 0 ? "Updated" : "Already OK");
    Console.WriteLine($"  {counterId} → seq = {maxSeq} ({action})");
    seeded++;
}

Console.WriteLine($"\n=== Summary ===");
Console.WriteLine($"Counters seeded: {seeded}");
Console.WriteLine($"Duplicates fixed: {fixedDuplicates}");
Console.WriteLine("\n=== Done ===");
