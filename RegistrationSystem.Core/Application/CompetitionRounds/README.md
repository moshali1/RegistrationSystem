# CompetitionRounds Domain

## Overview

The CompetitionRounds domain manages the lifecycle of competitors through multiple qualification and competition stages. Each `CompetitionRound` record tracks one competitor's journey through the rounds **configured for their category**.

## Business Context

### Round Configuration

**Not all categories have all rounds.** The active rounds for each category are determined by Settings:

- **Video Qualification**: `category.RequiresVideo` (typically 3 Juz category of the Memorization division)
- **Screening Round**: `category.ScreeningRoundEnabled` (typically other categories in the Memorization division)
- **Preliminary Round**: Always present (all categories)
- **Final Round**: Always present (all categories)

**Example category configurations:**

| Category | Video | Screening | Preliminary | Final |
|----------|-------|-----------|-------------|-------|
| Memorization - 5 Juz | ✅ | ❌ | ✅ | ✅ |
| Memorization - 3 Juz | ❌ | ✅ | ✅ | ✅ |
| Ten Qira'at - 15 Juz | ❌ | ❌ | ✅ | ✅ |

### Competition Workflows

**Workflow A: Video → Preliminary → Final** (Recitation division)
1. Competitor submits video recitation
2. Admin reviews and sets Pass/Fail
3. Those who **Pass** get assigned to preliminary round
4. Those who **Fail** see their result (no further rounds)
5. After preliminary round, **Qualified** competitors get assigned to final round
6. Those **NotQualified** or **NoShow** see their result (no final round)
7. Final round occurs, results recorded

**Workflow B: Screening → Preliminary → Final** (Memorization division)
1. Competitor assigned to screening round (or bypassed if previous year finalist)
2. Admin grades screening: Pass/Fail/NoShow
3. Those who **Pass** (or were **Bypassed**) get assigned to preliminary round
4. Flow continues same as Workflow A from step 5

**Workflow C: Preliminary → Final only** (Business Plan, other categories)
1. Competitor assigned to preliminary round (no qualification step)
2. After preliminary round, **Qualified** competitors get assigned to final round
3. Those **NotQualified** or **NoShow** see their result (no final round)
4. Final round occurs, results recorded

### Key Principle: Results Are Paired With Next Round Scheduling

**Critical design decision:** When admins post results for a round, they simultaneously assign schedules for the next round to those who passed. This means:

- Video Pass → Immediate preliminary assignment
- Screening Pass → Immediate preliminary assignment  
- Preliminary Qualified → Immediate final assignment

This pairing eliminates the need for separate "assessed at" timestamps. The **next round's acknowledgment timestamp** implicitly proves when the competitor learned they passed the previous round.

**Example:** If `PreliminaryRoundAcknowledgedAt = "2025-02-15 3:45pm"`, we know the competitor learned they passed video/screening by that time.

## What Gets Tracked vs. What Doesn't

### Tracked (Schedule Acknowledgments)

Competitors must acknowledge **schedules** because the system needs to know:
- Who confirmed they'll show up (operational planning)
- Who's ignoring notifications (follow-up needed)
- Room/judge allocation headcounts

**Tracked acknowledgments:**
- `ScreeningRoundAcknowledged` + `ScreeningRoundAcknowledgedAt`
- `PreliminaryRoundAcknowledged` + `PreliminaryRoundAcknowledgedAt`
- `FinalRoundAcknowledged` + `FinalRoundAcknowledgedAt`

### Not Tracked (Result Views)

Competitors do **not** acknowledge **results** (Pass/Fail/Qualified/NoShow) because:
- Failed competitors have no actionable next step
- Whether they "saw" the failure message is not operationally relevant
- SMS/email notifications document when results were sent (via audit trail)

**Not tracked:**
- ❌ Video qualification result viewed
- ❌ Screening result viewed
- ❌ Preliminary result viewed
- ❌ Final result viewed