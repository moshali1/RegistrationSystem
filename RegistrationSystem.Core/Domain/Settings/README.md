# Settings Domain Model

## CompetitionSettings
Root aggregate controlling all competition configuration. **Singleton** - only one instance exists (ID: `default-competition-settings`).

## Registration Hierarchy

Registration availability follows a **4-level hierarchy**:

1. **Global** - `RegistrationEnabled` is the master switch
2. **Division** - `Division.IsEnabled` must be true
3. **Category** - `Category.IsEnabled` must be true  
4. **Date Window** - Current time must be within start/end dates

All levels must pass for registration to be open.

## Global Settings

**RegistrationStart/End:**
Default date window for all categories. Categories can override with their own dates.

**AgeCutoffDate:**
Determines how competitor ages are calculated. Example: cutoff = Jan 1, 2025 → someone born Jan 2, 2010 is 14 years old.

## Division
Groups related categories (e.g., Boys, Girls, Adults). Enabling a division requires global registration to be enabled.

## Category

### Eligibility Rules
- **MaxAgeYears**: Age limit (null = no restriction)
- **PortionOption**: Which Qur'an portions are allowed (NotApplicable, TopOnly, BottomOnly, TopOrBottom)

### Schedule Override
- **RegistrationStart/End**: Custom dates for this category (null = use global dates)

### Video Requirements
- **RequiresVideo**: Whether video upload is mandatory
- **VideoInstructions**: What to record (e.g., "Surah Al-Baqarah verses 1-20")

### Competitor Permissions
- **AllowMultipleInDivision**: Can register for multiple categories in same division
- **AllowEdit**: Can edit registration when status is Pending
- **AllowWithdraw**: Can withdraw registration

## CompetitionInfo
Basic competition metadata and policy URLs. Configurable in admin UI, defaults provided for convenience.

## CidConfiguration
Controls Competitor ID generation.

**Format:** `[DivisionLetter][StateCode][3-digit sequence]`  
**Example:** M3001 = Memorization (M), MN state (code 3), competitor #1

**State Mappings:**
High-volume states get dedicated codes (MN=3, TN=5, TX=7). All others use default code (9).

**TODO:** Make state mappings configurable in UI instead of hardcoded.