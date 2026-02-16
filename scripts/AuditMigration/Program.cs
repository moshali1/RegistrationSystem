using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

// ═══════════════════════════════════════════════════════════════════════════
// MongoDB Migration: Audit Action Enum Refactoring (C# version)
// ═══════════════════════════════════════════════════════════════════════════
// This migration was run successfully on 2026-02-16 against shatibi_dev_registration_db.
// It remaps AuditAction integer values, normalizes summary text,
// and removes orphan records with deleted action types.
// ═══════════════════════════════════════════════════════════════════════════

var connectionString = args.Length > 0 ? args[0]
    : "mongodb+srv://NASQCDatabaseAdmin:ooyDMDhmvSMPzMDD@shatibicompetition-clus.bc4jf.mongodb.net/?retryWrites=true&w=majority";
var databaseName = args.Length > 1 ? args[1] : "shatibi_dev_registration_db";
var collectionName = "auditEntries";

var client = new MongoClient(connectionString);
var database = client.GetDatabase(databaseName);
var collection = database.GetCollection<BsonDocument>(collectionName);

Console.WriteLine("=== Audit Action Migration Script (C#) ===");
Console.WriteLine($"Database: {databaseName}");
Console.WriteLine($"Collection: {collectionName}");
var totalBefore = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
Console.WriteLine($"Total audit entries before migration: {totalBefore}");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────
// STEP 0: Pre-flight counts
// ─────────────────────────────────────────────────────────────────────────

var pipeline = new[]
{
    new BsonDocument("$group", new BsonDocument { { "_id", "$Action" }, { "count", new BsonDocument("$sum", 1) } }),
    new BsonDocument("$sort", new BsonDocument("_id", 1))
};

Console.WriteLine("Current action distribution:");
var actionCounts = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
foreach (var a in actionCounts)
    Console.WriteLine($"  Action {a["_id"]}: {a["count"]} entries");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────
// STEP 1: Delete orphan records
// Must happen BEFORE remapping because old FileUploaded=20 conflicts
// with new EmailSent=20
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("--- Step 1: Deleting orphan records ---");

var orphanActions = new BsonArray { 0, 20, 21, 90 };
// 0=Created, 20=FileUploaded, 21=FileDeleted, 90=SystemMigration

var orphanFilter = Builders<BsonDocument>.Filter.In("Action", orphanActions);
var orphanCount = await collection.CountDocumentsAsync(orphanFilter);
Console.WriteLine($"  Found {orphanCount} orphan records to delete (Created, FileUploaded, FileDeleted, SystemMigration)");

if (orphanCount > 0)
{
    var deleteResult = await collection.DeleteManyAsync(orphanFilter);
    Console.WriteLine($"  Deleted {deleteResult.DeletedCount} orphan records");
}
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────
// STEP 2: Two-pass Action integer remapping
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("--- Step 2: Remapping Action integer values ---");

// Pass 1: Old → Sentinel
var pass1Mappings = new (int from, int to)[]
{
    // Kept actions: old → sentinel
    (10, -1),    // Submitted
    (1, -2),     // Updated
    (2, -3),     // Deleted
    (11, -10),   // StatusChanged
    (14, -11),   // Withdrawn
    (17, -12),   // Disqualified
    (30, -20),   // EmailSent
    (31, -21),   // SmsSent
    (40, -30),   // NiqabBypassCreated
    (41, -31),   // NiqabBypassClaimed
    (42, -32),   // NiqabBypassDeleted
    (50, -40),   // SettingsUpdated
    (61, -50),   // ManualCorrection
    (91, -60),   // DataImport
    (92, -61),   // DataExport
    // Removed actions → sentinel of their target
    (12, -10),   // Approved → StatusChanged
    (13, -10),   // Rejected → StatusChanged
    (15, -11),   // WithdrawalRequested → Withdrawn
    (16, -10),   // Verified → StatusChanged
    (51, -40),   // DivisionUpdated → SettingsUpdated
    (52, -40),   // CategoryUpdated → SettingsUpdated
    (60, -50),   // AdminOverride → ManualCorrection
};

Console.WriteLine("  Pass 1: Old values → sentinels");
long pass1Total = 0;
foreach (var (from, to) in pass1Mappings)
{
    var filter = Builders<BsonDocument>.Filter.Eq("Action", from);
    var count = await collection.CountDocumentsAsync(filter);
    if (count > 0)
    {
        var update = Builders<BsonDocument>.Update.Set("Action", to);
        await collection.UpdateManyAsync(filter, update);
        Console.WriteLine($"    Action {from} → {to}: {count} records");
        pass1Total += count;
    }
}
Console.WriteLine($"  Pass 1 total: {pass1Total} records updated");
Console.WriteLine();

// Pass 2: Sentinel → Final
var pass2Mappings = new (int from, int to)[]
{
    (-1, 1),     // Submitted
    (-2, 2),     // Updated
    (-3, 3),     // Deleted
    (-10, 10),   // StatusChanged
    (-11, 11),   // Withdrawn
    (-12, 12),   // Disqualified
    (-20, 20),   // EmailSent
    (-21, 21),   // SmsSent
    (-30, 30),   // NiqabBypassCreated
    (-31, 31),   // NiqabBypassClaimed
    (-32, 32),   // NiqabBypassDeleted
    (-40, 40),   // SettingsUpdated
    (-50, 50),   // ManualCorrection
    (-60, 60),   // DataImport
    (-61, 61),   // DataExport
};

Console.WriteLine("  Pass 2: Sentinels → final values");
long pass2Total = 0;
foreach (var (from, to) in pass2Mappings)
{
    var filter = Builders<BsonDocument>.Filter.Eq("Action", from);
    var count = await collection.CountDocumentsAsync(filter);
    if (count > 0)
    {
        var update = Builders<BsonDocument>.Update.Set("Action", to);
        await collection.UpdateManyAsync(filter, update);
        Console.WriteLine($"    Action {from} → {to}: {count} records");
        pass2Total += count;
    }
}
Console.WriteLine($"  Pass 2 total: {pass2Total} records updated");
Console.WriteLine();

// Verify no sentinel values remain
var sentinelFilter = Builders<BsonDocument>.Filter.Lt("Action", 0);
var sentinelCheck = await collection.CountDocumentsAsync(sentinelFilter);
if (sentinelCheck > 0)
    Console.WriteLine($"  WARNING: {sentinelCheck} records still have negative sentinel values!");
else
    Console.WriteLine("  OK: No sentinel values remaining");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────
// STEP 3: Normalize Summary text
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("--- Step 3: Normalizing Summary text ---");

// 3a: "AI ID Verification: Pass" → "Auto verification: Pass"
var aiFilter = Builders<BsonDocument>.Filter.Regex("Summary", new BsonRegularExpression("AI ID Verification"));
var aiDocs = await collection.CountDocumentsAsync(aiFilter);
if (aiDocs > 0)
{
    var cursor = await collection.FindAsync(aiFilter);
    long count3a = 0;
    await cursor.ForEachAsync(async doc =>
    {
        var summary = doc.GetValue("Summary", "").AsString;
        var newSummary = summary.Replace("AI ID Verification: Pass", "Auto verification: Pass");
        if (newSummary != summary)
        {
            var idFilter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
            var upd = Builders<BsonDocument>.Update.Set("Summary", newSummary);
            await collection.UpdateOneAsync(idFilter, upd);
            count3a++;
        }
    });
    Console.WriteLine($"  3a: AI ID Verification → Auto verification: {count3a} records");
}
else
{
    Console.WriteLine("  3a: AI ID Verification → Auto verification: 0 records");
}

// 3b: "Bulk status change: X → Y" → "X → Y"
var bulkFilter = Builders<BsonDocument>.Filter.Regex("Summary", new BsonRegularExpression("^Bulk status change: "));
var bulkCount = await collection.CountDocumentsAsync(bulkFilter);
if (bulkCount > 0)
{
    var cursor = await collection.FindAsync(bulkFilter);
    long count3b = 0;
    await cursor.ForEachAsync(async doc =>
    {
        var summary = doc.GetValue("Summary", "").AsString;
        var newSummary = summary.Replace("Bulk status change: ", "");
        var idFilter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
        var upd = Builders<BsonDocument>.Update.Set("Summary", newSummary);
        await collection.UpdateOneAsync(idFilter, upd);
        count3b++;
    });
    Console.WriteLine($"  3b: Bulk status change prefix removed: {count3b} records");
}
else
{
    Console.WriteLine("  3b: Bulk status change prefix removed: 0 records");
}

// 3c: "Status changed from X to Y" → "X → Y"
var statusChangedRegex = new Regex(@"^Status changed from (.+?) to (.+?)(\s*\(.*\))?$");
var scFilter = Builders<BsonDocument>.Filter.Regex("Summary", new BsonRegularExpression("^Status changed from "));
var scCount = await collection.CountDocumentsAsync(scFilter);
long count3c = 0;
if (scCount > 0)
{
    var cursor = await collection.FindAsync(scFilter);
    await cursor.ForEachAsync(async doc =>
    {
        var summary = doc.GetValue("Summary", "").AsString;
        var match = statusChangedRegex.Match(summary);
        if (match.Success)
        {
            var oldStatus = match.Groups[1].Value;
            var newStatus = match.Groups[2].Value;
            var suffix = match.Groups[3].Success ? match.Groups[3].Value.Trim() : "";
            var newSummary = string.IsNullOrEmpty(suffix) ? $"{oldStatus} → {newStatus}" : $"{oldStatus} → {newStatus} {suffix}";
            var idFilter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
            var upd = Builders<BsonDocument>.Update.Set("Summary", newSummary);
            await collection.UpdateOneAsync(idFilter, upd);
            count3c++;
        }
    });
}
Console.WriteLine($"  3c: Status changed from X to Y → X → Y: {count3c} records");

// 3d: "Status reverted from X to Y (Reason: Z)" → "X → Y (Reverted: Z)"
var revertedRegex = new Regex(@"^Status reverted from (.+?) to (.+?) \(Reason: (.+)\)$");
var revertFilter = Builders<BsonDocument>.Filter.Regex("Summary", new BsonRegularExpression("^Status reverted from "));
var revertCount = await collection.CountDocumentsAsync(revertFilter);
long count3d = 0;
if (revertCount > 0)
{
    var cursor = await collection.FindAsync(revertFilter);
    await cursor.ForEachAsync(async doc =>
    {
        var summary = doc.GetValue("Summary", "").AsString;
        var match = revertedRegex.Match(summary);
        if (match.Success)
        {
            var newSummary = $"{match.Groups[1].Value} → {match.Groups[2].Value} (Reverted: {match.Groups[3].Value})";
            var idFilter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
            var upd = Builders<BsonDocument>.Update.Set("Summary", newSummary);
            await collection.UpdateOneAsync(idFilter, upd);
            count3d++;
        }
    });
}
Console.WriteLine($"  3d: Status reverted → arrow format: {count3d} records");

// 3e: "Registration withdrawn. Reason: X" → "OldStatus → Withdrawn (Reason: X)"
var withdrawnFilter = Builders<BsonDocument>.Filter.Regex("Summary", new BsonRegularExpression("^Registration withdrawn\\. Reason: "));
var withdrawnCount = await collection.CountDocumentsAsync(withdrawnFilter);
long count3e = 0;
if (withdrawnCount > 0)
{
    var cursor = await collection.FindAsync(withdrawnFilter);
    await cursor.ForEachAsync(async doc =>
    {
        var summary = doc.GetValue("Summary", "").AsString;
        var reason = summary.Replace("Registration withdrawn. Reason: ", "");
        var oldStatus = "Unknown";

        // Try to get old status from Changes array
        if (doc.Contains("Changes") && doc["Changes"].IsBsonArray)
        {
            foreach (var change in doc["Changes"].AsBsonArray)
            {
                if (change.IsBsonDocument)
                {
                    var changeDoc = change.AsBsonDocument;
                    if (changeDoc.GetValue("FieldName", "").AsString == "Status" &&
                        changeDoc.Contains("OldValue") && !changeDoc["OldValue"].IsBsonNull)
                    {
                        oldStatus = changeDoc["OldValue"].AsString;
                        break;
                    }
                }
            }
        }

        var newSummary = $"{oldStatus} → Withdrawn (Reason: {reason})";
        var idFilter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
        var upd = Builders<BsonDocument>.Update.Set("Summary", newSummary);
        await collection.UpdateOneAsync(idFilter, upd);
        count3e++;
    });
}
Console.WriteLine($"  3e: Registration withdrawn → arrow format: {count3e} records");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────
// STEP 4: Add Method metadata to AI verification records
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("--- Step 4: Adding Method metadata ---");

var autoVerifyFilter = Builders<BsonDocument>.Filter.Regex("Summary", new BsonRegularExpression("Auto verification"));

// First, initialize Metadata to empty doc where it's null (can't set nested field on null)
var nullMetadataFilter = Builders<BsonDocument>.Filter.And(
    autoVerifyFilter,
    Builders<BsonDocument>.Filter.Or(
        Builders<BsonDocument>.Filter.Eq("Metadata", BsonNull.Value),
        Builders<BsonDocument>.Filter.Exists("Metadata", false)
    )
);
var initResult = await collection.UpdateManyAsync(nullMetadataFilter,
    Builders<BsonDocument>.Update.Set("Metadata", new BsonDocument()));
Console.WriteLine($"  Initialized null Metadata fields: {initResult.ModifiedCount} records");

// Now set the Method field
var autoVerifyUpdate = Builders<BsonDocument>.Update.Set("Metadata.Method", "Auto verification");
var autoResult = await collection.UpdateManyAsync(autoVerifyFilter, autoVerifyUpdate);
Console.WriteLine($"  Auto verification metadata added: {autoResult.ModifiedCount} records");
Console.WriteLine();

// ─────────────────────────────────────────────────────────────────────────
// STEP 5: Post-migration verification
// ─────────────────────────────────────────────────────────────────────────

Console.WriteLine("--- Step 5: Post-migration verification ---");
var totalAfter = await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);
Console.WriteLine($"Total audit entries after migration: {totalAfter}");

var actionNames = new Dictionary<int, string>
{
    [1] = "Submitted", [2] = "Updated", [3] = "Deleted",
    [10] = "StatusChanged", [11] = "Withdrawn", [12] = "Disqualified",
    [20] = "EmailSent", [21] = "SmsSent",
    [30] = "NiqabBypassCreated", [31] = "NiqabBypassClaimed", [32] = "NiqabBypassDeleted",
    [40] = "SettingsUpdated", [50] = "ManualCorrection",
    [60] = "DataImport", [61] = "DataExport"
};

Console.WriteLine("\nNew action distribution:");
var newActionCounts = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
var hasUnexpected = false;
foreach (var a in newActionCounts)
{
    var actionId = a["_id"].AsInt32;
    var name = actionNames.GetValueOrDefault(actionId, $"UNKNOWN({actionId})");
    Console.WriteLine($"  {name} ({actionId}): {a["count"]} entries");
    if (!actionNames.ContainsKey(actionId))
        hasUnexpected = true;
}

if (hasUnexpected)
    Console.WriteLine("\n  WARNING: Unexpected action values found!");
else
    Console.WriteLine("\n  OK: All action values are expected");

// Sample some summaries
Console.WriteLine("\nSample summaries (StatusChanged, Action=10):");
var sampleFilter = Builders<BsonDocument>.Filter.Eq("Action", 10);
var samples = await collection.Find(sampleFilter).Limit(5).ToListAsync();
foreach (var doc in samples)
    Console.WriteLine($"  \"{doc.GetValue("Summary", "N/A")}\"");

Console.WriteLine("\n=== Migration Complete ===");
