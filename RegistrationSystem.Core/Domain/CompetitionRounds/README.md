# CompetitionRounds Domain Model

## CompetitionRound
Tracks round assignments and acknowledgments for a registration. **One-to-one** relationship with Registration — each registration gets exactly one CompetitionRound record.

## Denormalized Fields
`CompetitionYear`, `DivisionId`, `CategoryId`, `Cid`, and `CompetitorName` are copied from Registration at creation time. This avoids joins when querying rounds for reporting or listing.

## Video Qualification
Before competing in the preliminary round, a competitor's recitation video is reviewed by an admin.

- **Pending** → Not yet reviewed
- **Pass** → Eligible to compete
- **Fail** → Not eligible (reason stored in `VideoQualificationComment`)

## Round Flow

### Preliminary Round
1. Admin assigns a date/time (`PreliminaryRoundDateTime`)
2. Competitor acknowledges the assignment (`PreliminaryRoundAcknowledged`)
3. Competitors who failed video qualification skip this — they are not assigned a date, but `HasPreliminaryRound` still returns true so the system treats them as resolved.

### Final Round
1. `IsQualified` is set based on preliminary round performance
2. `IsAttended` tracks whether the competitor showed up to the preliminary round
3. Admin assigns a final round date/time (`FinalRoundDateTime`)
4. Competitor acknowledges (`FinalRoundAcknowledged`)

## Computed Properties

| Property | Logic |
|---|---|
| `HasPreliminaryRound` | A date is assigned OR video qualification has been resolved (pass or fail) |
| `HasFinalRound` | A final round date is assigned |
| `AllRoundsAcknowledged` | Every assigned round has been acknowledged |
| `HasPendingAcknowledgment` | Currently checks only final round acknowledgment — revisit if prelim ack is also needed |

## VideoQualificationStatus Enum
- **Pending** — Default, awaiting review
- **Pass** — Video approved
- **Fail** — Video rejected
