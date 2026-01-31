# Registration Domain Model

## Registration
Main aggregate representing a competitor's registration for a specific category.

**Key Concepts:**
- One registration = one category entry
- CID format: `[DivisionLetter][StateCode][Sequence]` (e.g., M3001)
- Creator can be parent, teacher, or the competitor themselves

**Status Flow:**
1. AwaitingReview → Initial submission
2. Pending → Admin sent back for corrections
3. Reviewed → Admin approved
4. Verified → Final verification before competition
5. Withdrawn → User/admin withdrew
6. Disqualified → Ineligible (DOB mismatch, etc.)

## Value Objects

### PersonalInfo
Competitor's personal details. `DisplayName` shows preferred name if set, otherwise first name.

### AddressInfo
Location data. Supports US, Canada, Mexico (see LocationData in Infrastructure).

### CompetitionSelection
Division and category choice. `PortionChoice` only relevant when category allows TopOrBottom.

### ParentInfo
Always required. Primary contact for competitor.

### TeacherInfo
Optional. Use when competitor has institutional affiliation.

### FileUploadInfo
References to uploaded files (stored in Azure Blob Storage).
- **IdDocument**: Government-issued ID
- **Photo**: For face verification
- **Video**: Recitation video (category-dependent)
- **NiqabBypass**: Skips face detection for niqab-wearing competitors

### FileValidationResult
Result of automated validation (face detection, image analysis).
`Details` includes method and reason.

## Business Rules

**Age Calculation:**
Uses `CalculateAgeAsOf(cutoffDate)` - competition defines cutoff date in settings.

**Edit/Withdraw Permissions:**
Handled in Application layer - checks both registration status AND category settings.

**File Requirements:**
- ID + Photo always required
- Video required only if category specifies `RequiresVideo`
- Niqab bypass approved by admin, uses one-time code