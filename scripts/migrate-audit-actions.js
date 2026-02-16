// ═══════════════════════════════════════════════════════════════════════════
// MongoDB Migration Script: Audit Action Enum Refactoring
// ═══════════════════════════════════════════════════════════════════════════
//
// Run with: mongosh "mongodb://localhost:27017/YourDbName" migrate-audit-actions.js
//   or:     mongosh "mongodb+srv://..." migrate-audit-actions.js
//
// This script must be run BEFORE deploying the new code.
// It remaps AuditAction integer values, normalizes summary text,
// and removes orphan records with deleted action types.
//
// IMPORTANT: Test on a backup/staging database first!
// ═══════════════════════════════════════════════════════════════════════════

const collection = db.auditEntries;

print("=== Audit Action Migration Script ===");
print(`Total audit entries before migration: ${collection.countDocuments()}`);
print("");

// ─────────────────────────────────────────────────────────────────────────
// STEP 0: Pre-flight counts
// ─────────────────────────────────────────────────────────────────────────

const actionCounts = collection.aggregate([
    { $group: { _id: "$Action", count: { $sum: 1 } } },
    { $sort: { _id: 1 } }
]).toArray();

print("Current action distribution:");
actionCounts.forEach(a => print(`  Action ${a._id}: ${a.count} entries`));
print("");

// ─────────────────────────────────────────────────────────────────────────
// STEP 1: Delete orphan records (actions with no meaningful remap)
// Must happen BEFORE remapping because old FileUploaded=20 conflicts
// with new EmailSent=20
// ─────────────────────────────────────────────────────────────────────────

print("--- Step 1: Deleting orphan records ---");

const orphanActions = [0, 20, 21, 90];
// 0=Created, 20=FileUploaded, 21=FileDeleted, 90=SystemMigration

const orphanCount = collection.countDocuments({ Action: { $in: orphanActions } });
print(`  Found ${orphanCount} orphan records to delete (Created, FileUploaded, FileDeleted, SystemMigration)`);

if (orphanCount > 0) {
    const deleteResult = collection.deleteMany({ Action: { $in: orphanActions } });
    print(`  Deleted ${deleteResult.deletedCount} orphan records`);
}
print("");

// ─────────────────────────────────────────────────────────────────────────
// STEP 2: Two-pass Action integer remapping
// Pass 1: Old values → negative sentinels (avoids collisions)
// Pass 2: Sentinels → final new values
// ─────────────────────────────────────────────────────────────────────────

print("--- Step 2: Remapping Action integer values ---");

// Pass 1: Old → Sentinel
// Includes both kept actions AND removed actions (remapped to their target's sentinel)
const pass1Mappings = [
    // Kept actions: old → sentinel
    { from: 10, to: -1 },    // Submitted
    { from: 1,  to: -2 },    // Updated
    { from: 2,  to: -3 },    // Deleted
    { from: 11, to: -10 },   // StatusChanged
    { from: 14, to: -11 },   // Withdrawn
    { from: 17, to: -12 },   // Disqualified
    { from: 30, to: -20 },   // EmailSent
    { from: 31, to: -21 },   // SmsSent
    { from: 40, to: -30 },   // NiqabBypassCreated
    { from: 41, to: -31 },   // NiqabBypassClaimed
    { from: 42, to: -32 },   // NiqabBypassDeleted
    { from: 50, to: -40 },   // SettingsUpdated
    { from: 61, to: -50 },   // ManualCorrection
    { from: 91, to: -60 },   // DataImport
    { from: 92, to: -61 },   // DataExport
    // Removed actions → sentinel of their target
    { from: 12, to: -10 },   // Approved → StatusChanged
    { from: 13, to: -10 },   // Rejected → StatusChanged
    { from: 15, to: -11 },   // WithdrawalRequested → Withdrawn
    { from: 16, to: -10 },   // Verified → StatusChanged
    { from: 51, to: -40 },   // DivisionUpdated → SettingsUpdated
    { from: 52, to: -40 },   // CategoryUpdated → SettingsUpdated
    { from: 60, to: -50 },   // AdminOverride → ManualCorrection
];

print("  Pass 1: Old values → sentinels");
let pass1Total = 0;
for (const m of pass1Mappings) {
    const count = collection.countDocuments({ Action: m.from });
    if (count > 0) {
        collection.updateMany({ Action: m.from }, { $set: { Action: m.to } });
        print(`    Action ${m.from} → ${m.to}: ${count} records`);
        pass1Total += count;
    }
}
print(`  Pass 1 total: ${pass1Total} records updated`);
print("");

// Pass 2: Sentinel → Final
const pass2Mappings = [
    { from: -1,  to: 1 },    // Submitted
    { from: -2,  to: 2 },    // Updated
    { from: -3,  to: 3 },    // Deleted
    { from: -10, to: 10 },   // StatusChanged
    { from: -11, to: 11 },   // Withdrawn
    { from: -12, to: 12 },   // Disqualified
    { from: -20, to: 20 },   // EmailSent
    { from: -21, to: 21 },   // SmsSent
    { from: -30, to: 30 },   // NiqabBypassCreated
    { from: -31, to: 31 },   // NiqabBypassClaimed
    { from: -32, to: 32 },   // NiqabBypassDeleted
    { from: -40, to: 40 },   // SettingsUpdated
    { from: -50, to: 50 },   // ManualCorrection
    { from: -60, to: 60 },   // DataImport
    { from: -61, to: 61 },   // DataExport
];

print("  Pass 2: Sentinels → final values");
let pass2Total = 0;
for (const m of pass2Mappings) {
    const count = collection.countDocuments({ Action: m.from });
    if (count > 0) {
        collection.updateMany({ Action: m.from }, { $set: { Action: m.to } });
        print(`    Action ${m.from} → ${m.to}: ${count} records`);
        pass2Total += count;
    }
}
print(`  Pass 2 total: ${pass2Total} records updated`);
print("");

// Verify no sentinel values remain
const sentinelCheck = collection.countDocuments({ Action: { $lt: 0 } });
if (sentinelCheck > 0) {
    print(`  WARNING: ${sentinelCheck} records still have negative sentinel values!`);
} else {
    print("  OK: No sentinel values remaining");
}
print("");

// ─────────────────────────────────────────────────────────────────────────
// STEP 3: Normalize Summary text
// ─────────────────────────────────────────────────────────────────────────

print("--- Step 3: Normalizing Summary text ---");

// 3a: "AI ID Verification: Pass" → "Auto verification: Pass"
let result = collection.updateMany(
    { Summary: /AI ID Verification/ },
    [{ $set: { Summary: { $replaceAll: { input: "$Summary", find: "AI ID Verification: Pass", replacement: "Auto verification: Pass" } } } }]
);
print(`  3a: AI ID Verification → Auto verification: ${result.modifiedCount} records`);

// 3b: "Bulk status change: X → Y" → "X → Y"
result = collection.updateMany(
    { Summary: /^Bulk status change: / },
    [{ $set: { Summary: { $replaceAll: { input: "$Summary", find: "Bulk status change: ", replacement: "" } } } }]
);
print(`  3b: Bulk status change prefix removed: ${result.modifiedCount} records`);

// 3c: "Status changed from X to Y" → "X → Y" (preserving parenthetical if present)
// Use cursor iteration for complex regex parsing
let count3c = 0;
collection.find({ Summary: /^Status changed from / }).forEach(doc => {
    const match = doc.Summary.match(/^Status changed from (.+?) to (.+?)(\s*\(.*\))?$/);
    if (match) {
        const oldStatus = match[1];
        const newStatus = match[2];
        const suffix = match[3] ? match[3].trim() : "";
        const newSummary = suffix ? `${oldStatus} → ${newStatus} ${suffix}` : `${oldStatus} → ${newStatus}`;
        collection.updateOne({ _id: doc._id }, { $set: { Summary: newSummary } });
        count3c++;
    }
});
print(`  3c: Status changed from X to Y → X → Y: ${count3c} records`);

// 3d: "Status reverted from X to Y (Reason: Z)" → "X → Y (Reverted: Z)"
let count3d = 0;
collection.find({ Summary: /^Status reverted from / }).forEach(doc => {
    const match = doc.Summary.match(/^Status reverted from (.+?) to (.+?) \(Reason: (.+)\)$/);
    if (match) {
        const newSummary = `${match[1]} → ${match[2]} (Reverted: ${match[3]})`;
        collection.updateOne({ _id: doc._id }, { $set: { Summary: newSummary } });
        count3d++;
    }
});
print(`  3d: Status reverted → arrow format: ${count3d} records`);

// 3e: "Registration withdrawn. Reason: X" → "OldStatus → Withdrawn (Reason: X)"
let count3e = 0;
collection.find({ Summary: /^Registration withdrawn\. Reason: / }).forEach(doc => {
    const reason = doc.Summary.replace("Registration withdrawn. Reason: ", "");
    // Try to get old status from Changes array
    let oldStatus = "Unknown";
    if (doc.Changes && Array.isArray(doc.Changes)) {
        const statusChange = doc.Changes.find(c => c.FieldName === "Status");
        if (statusChange && statusChange.OldValue) {
            oldStatus = statusChange.OldValue;
        }
    }
    const newSummary = `${oldStatus} → Withdrawn (Reason: ${reason})`;
    collection.updateOne({ _id: doc._id }, { $set: { Summary: newSummary } });
    count3e++;
});
print(`  3e: Registration withdrawn → arrow format: ${count3e} records`);
print("");

// ─────────────────────────────────────────────────────────────────────────
// STEP 4: Add Method metadata to AI verification records
// ─────────────────────────────────────────────────────────────────────────

print("--- Step 4: Adding Method metadata ---");

// Records with "Auto verification" in summary (from step 3a)
result = collection.updateMany(
    { Summary: /Auto verification/ },
    { $set: { "Metadata.Method": "Auto verification" } }
);
print(`  Auto verification metadata added: ${result.modifiedCount} records`);
print("");

// ─────────────────────────────────────────────────────────────────────────
// STEP 5: Post-migration verification
// ─────────────────────────────────────────────────────────────────────────

print("--- Step 5: Post-migration verification ---");
print(`Total audit entries after migration: ${collection.countDocuments()}`);

const newActionCounts = collection.aggregate([
    { $group: { _id: "$Action", count: { $sum: 1 } } },
    { $sort: { _id: 1 } }
]).toArray();

print("New action distribution:");
const actionNames = {
    1: "Submitted", 2: "Updated", 3: "Deleted",
    10: "StatusChanged", 11: "Withdrawn", 12: "Disqualified",
    20: "EmailSent", 21: "SmsSent",
    30: "NiqabBypassCreated", 31: "NiqabBypassClaimed", 32: "NiqabBypassDeleted",
    40: "SettingsUpdated", 50: "ManualCorrection",
    60: "DataImport", 61: "DataExport"
};
newActionCounts.forEach(a => {
    const name = actionNames[a._id] || `UNKNOWN(${a._id})`;
    print(`  ${name} (${a._id}): ${a.count} entries`);
});

// Check for any unexpected action values
const unexpectedActions = newActionCounts.filter(a => !actionNames[a._id]);
if (unexpectedActions.length > 0) {
    print("\n  WARNING: Unexpected action values found:");
    unexpectedActions.forEach(a => print(`    Action ${a._id}: ${a.count} entries`));
} else {
    print("\n  OK: All action values are expected");
}

// Sample some summaries to verify format
print("\nSample summaries (StatusChanged):");
collection.find({ Action: 10 }).limit(5).forEach(doc => {
    print(`  "${doc.Summary}"`);
});

print("\n=== Migration Complete ===");
