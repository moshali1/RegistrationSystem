# CompetitionRounds Application Layer

## ICompetitionRoundRepository
Data access contract for CompetitionRound records. Supports single and batch lookups by registration, year, date, date/time slot, and qualification/acknowledgment status.

**Date vs DateTime queries:** Both exist intentionally. Date queries return all competitors on a given day (schedule overview). DateTime queries return competitors in a specific time slot (room/judge assignment).

**`GetByIdAsync`** is defined but not called by the service. Confirm it's used elsewhere or remove it.

## CompetitionRoundService

### Video Qualification
- `SetVideoQualificationAsync` — Admin sets pass/fail on a competitor's recitation video. Creates the CompetitionRound record if one doesn't exist yet.
- `GetPendingVideoQualificationsAsync` — Returns all rounds awaiting video review for a year.

### Round Assignment
- `AssignPreliminaryRoundAsync` / `AssignFinalRoundAsync` — Admin assigns a date/time. Resets acknowledgment state on reassignment.
- `AssignFinalRoundAsync` additionally blocks competitors who failed video qualification.
- `BulkAssign*` methods iterate one-by-one (3 DB calls per registration). Consider batch repository methods if this becomes a bottleneck.

### Acknowledgments
- `AcknowledgePreliminaryRoundAsync` / `AcknowledgeFinalRoundAsync` — User confirms their round assignment. Validates ownership via `CreatorUserId`.

### Queries
Pass-through methods to the repository. `GetByRegistrationIdsAsync` returns a dictionary keyed by registration ID for efficient lookups.

## Business Rules
- Only **Reviewed** or **Verified** registrations can have rounds assigned or video qualification assessed.
- Failed video qualification blocks final round assignment.
- Acknowledgments can only be performed by the registration's creator.
- Reassigning a round date resets its acknowledgment state.