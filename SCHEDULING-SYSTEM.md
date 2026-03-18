# Scheduling System — Architecture & Flow

## Overview

The scheduling system allows participants to self-book time slots for competition rounds (e.g., screening round). It supports parallel sections (Section A, B, C…), load-balanced assignment, admin controls, and participant notifications via the red dot on the registrations page.

---

## Data Stores

### 1. `SchedulingBookings` Collection (MongoDB)
**Source of truth for occupancy.**

Each document records one booking event — active or cancelled. Never deleted; cancelled bookings are kept for audit history.

| Field | Description |
|---|---|
| `RegistrationId` | Which registration made the booking |
| `SessionId` | Which scheduling session was booked |
| `SlotId` | Which specific time slot was booked |
| `Status` | `Active` or `Cancelled` |
| `BookedAt` | UTC timestamp of booking |
| `CancelledAt` | UTC timestamp of cancellation (if cancelled) |
| `CancellationReason` | Admin-entered reason for cancellation |

### 2. `CompetitionSettings.SchedulingSessions` (embedded in Settings)
**Admin configuration — what slots exist and their max capacity.**

Sessions are embedded inside the main competition settings document. Saved via the Settings page.

Key fields on `SchedulingSession`:

| Field | Description |
|---|---|
| `Id` | Unique session ID |
| `Name` | Display name, e.g. "Screening Round 2026 — Section A" |
| `GroupId` | Groups parallel sections together (same round, different sections) |
| `SectionLabel` | Section letter, e.g. "A", "B", "C" |
| `LinkedRoundName` | Matches a `RoundDefinition.Name` in category settings (case-insensitive) |
| `IsOpen` | Whether participants can currently book |
| `VirtualLink` | Optional section-specific conference link (shown after booking) |
| `SchedulingOpensAt` / `SchedulingClosesAt` | Optional automatic open/close window (UTC) |
| `GenderFilter` | All / Male Only / Female Only |
| `GeographicFilter` | All / Minnesota Only / Outside Minnesota |
| `AllowedCategoryIds` | Restricts which categories can book (empty = all) |
| `Slots` | List of `SchedulingSlot` (date, time UTC, capacity, active flag) |

**Important:** `Slot.Capacity` is a static maximum set by admin. It never decrements. Remaining capacity is always computed live as:
```
remaining = slot.Capacity - COUNT(active SchedulingBookings for that slotId)
```

### 3. `CompetitionProgress.RoundEntry` (per-registration document)
**Denormalized snapshot — the competitor's current round state.**

When a participant books a slot, three fields on their `RoundEntry` are updated:

| Field | Set to |
|---|---|
| `Status` | `RoundEntryStatus.Scheduled` |
| `ScheduledDateTime` | UTC time of the booked slot |
| `ScheduledSection` | `SectionLabel` of the session booked into |
| `Acknowledged` | `true` (booking = confirmation, no separate step needed) |
| `AcknowledgedAt` | UTC timestamp |

When a booking is cancelled, the round entry reverts to `Status = Active` and `ScheduledDateTime` is cleared.

### 4. Audit Log
**Not used for scheduling.** The `SchedulingBookings` collection itself serves as the audit trail since cancelled bookings are retained with reason and timestamp.

---

## Parallel Sections

Sessions sharing the same `GroupId` are treated as parallel sections for the same round (e.g., Section A, B, C all for "Screening Round").

On the `/schedule` page, parallel sections are **merged** — the same time slot appears once, not once per section. When a participant clicks a slot:
- If not admin: they are auto-assigned to the section with the most remaining capacity (load balancing)
- If admin: they see a section picker and can override the assignment

The `SectionLabel` stored on `RoundEntry.ScheduledSection` is what shows in the competitor's progress tracker and messages page.

---

## Red Dot Notification Logic

The red dot on the `/registrations` page has two independent triggers:

### Trigger 1 — Result / Bypass Notification
Fires when `CompetitionProgress.HasPendingAcknowledgment` is true:
- A round result (Pass, Fail, Qualified, etc.) was entered → `Acknowledged = false` on that round → red dot
- A round was bypassed → `Acknowledged = false` on that round → red dot

Clears when:
- **Results**: participant visits `/messages` and clicks Acknowledge
- **Bypass**: participant visits `/messages` — the page auto-acknowledges bypassed rounds on load (no button needed)

### Trigger 2 — Scheduling Reminder
Fires when:
1. Participant has a round with `Status = Active`
2. AND at least one `SchedulingSession` exists with `IsOpen = true` and `LinkedRoundName` matching that active round

**Clears automatically** when the participant books — their round status changes from `Active` to `Scheduled`, so the condition is no longer met on next page load.

**Admin does not need to touch individual participants.** Flipping `IsOpen = true` on a session in Settings instantly triggers the red dot for all eligible participants.

---

## Booking Flow

1. Participant goes to `/schedule` (linked from their messages page when scheduling is open)
2. Page shows merged time slots across all sections for their eligible sessions
3. They click a slot → confirm modal shows
   - **Participants**: sees their auto-assigned section
   - **Admins**: sees section picker to override
4. On confirm → `BookSlotAsync`:
   - Cancels any existing active booking first (reschedule scenario)
   - Inserts new `SchedulingBooking` document
   - Updates `CompetitionProgress.RoundEntry`: Status=Scheduled, ScheduledDateTime, ScheduledSection, Acknowledged=true
5. Red dot clears automatically (status is now Scheduled, not Active)

---

## Admin Bookings Page (`/admin/scheduling-bookings`)

Shows all sessions grouped by section tabs. For each session:
- Slots grouped by date
- Capacity bar showing remaining/total
- Per-slot list of active bookings (CID, name, booked at CT, view link, cancel button)
- Collapsed section for cancelled bookings with reason

Cancelling a booking from this page sets the booking status to Cancelled and reverts the participant's `RoundEntry` back to Active.

---

## Dual-Write Consistency

Booking writes to two places: `SchedulingBookings` (insert) and `CompetitionProgress` (update). MongoDB does not provide free transactions across collections. In the rare case of partial failure:

- `SchedulingBookings` is the source of truth for occupancy
- `CompetitionProgress` is the source of truth for "what round is this competitor in"

If they drift, the fix is to re-run the progress sync from active bookings. A future admin utility could automate this.

---

## Time Zones

All times are stored in **UTC** throughout. Displayed to participants and admins in **Central Time (CT)**, DST-aware.

Helper methods in `SchedulingService`:
- `ToCentralTime(DateTimeOffset utc)` — convert UTC DateTimeOffset to CT DateTime
- `CentralTimeToUtc(DateTime centralDt)` — convert CT datetime-local input to UTC DateTimeOffset
- `CentralTimeOnlyToUtc(TimeOnly central)` — for slot time inputs
- `UtcTimeOnlyToCentral(TimeOnly utc)` — for slot time display

---

## Settings Page — Managing Sessions

- **Add session**: creates a new standalone session
- **Edit session**: opens two-column modal (session config left, eligibility filters right)
  - Set Virtual Link for section-specific conference URLs
  - Set Opens/Closes At for automatic scheduling windows (stored UTC, displayed CT)
- **Duplicate session**: copies a session into multiple parallel sections (B through H) with the same slots and a shared Group ID
- **Manage Slots**: slot generator creates slots at a given interval; individual slots can be toggled active/inactive, capacity adjusted, or given admin notes
- **Toggle Open**: quick open/close without opening the full editor

Changes to sessions are **not saved until the main Settings Save button is clicked.** `SettingsCloner` and `SettingsComparer` both include `SchedulingSessions` so the unsaved-changes indicator and discard-changes flow work correctly.
