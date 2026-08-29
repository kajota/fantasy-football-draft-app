# How Draft Grades Are Calculated

Implementation: `src/FantasyDraftAssistant.Core/Analytics/DraftGrader.cs`

Grades are **relative to the room**, not an absolute quality score. A solid team
in a stacked draft can land a B; a mediocre team in a weak draft can land an A-.
Every team is measured against the median of the other teams in that same draft.

Kicker and defense are excluded entirely — undrafted, drafted early, drafted
late, doesn't matter. See [Why K/DEF are excluded](#why-kdef-are-excluded) below.

## Step 1 — Per-team raw stats

For each team, three things get computed from its actual picks (K/DEF picks are
filtered out before any of this runs):

- **Average ADP value** — for every pick with ADP data, `(overall pick number) −
  (expected ADP slot, scaled to your league size)`. Positive means the player
  fell further than expected (a steal); negative means you took them earlier
  than expected (a reach). Averaged across all your picks.
- **Starter points** — sum of projected season points (using your league's
  actual scoring rules) for players who land in a *starting* slot — not bench —
  once your roster is filled out by best-fit position.
- **Open starters** — how many starting slots (QB / RB / WR / TE / FLEX) are
  still empty. K and DEF slots are never counted here, even if empty.

## Step 2 — Score, starting from a baseline of 82

```
score  = 82
score += clamp(your average value − room's median value, -16, 16) × 0.75
score -= open starters × 6
score += clamp((your starter points − room's median starter points)
                 / max(20, room's median starter points × 0.08), -10, 10)
score  = clamp(score, 0, 100)
```

In plain terms:

| Factor | Effect |
|---|---|
| ADP value vs. the room's median | up to **±12 points** (0.75 per pick of value, capped at 16 picks of value) |
| Each empty starting slot (not counting K/DEF) | **−6 points** |
| Starter points vs. the room's median | up to **±10 points**, scaled to the room's own scoring level |

## Step 3 — Letter grade

The final 0–100 score maps to a letter with fixed bands:

| Score | Letter | Score | Letter | Score | Letter |
|---|---|---|---|---|---|
| 97+ | A+ | 87+ | B+ | 70+ | C- |
| 93+ | A  | 83+ | B  | 67+ | D+ |
| 90+ | A- | 80+ | B- | 63+ | D  |
|     |    | 77+ | C+ | 60+ | D- |
|     |    | 73+ | C  | <60 | F  |

A team with zero picks is ungraded (`—`), not an F.

## Step 4 — Notes

Each team also gets a few plain-English notes: best-value pick, biggest reach
(only flagged past a threshold — +4 picks of value / -6 picks of reach), average
ADP value in words, projected starter points, and either what's still needed or
"starting lineup is filled." All of this is K/DEF-blind too.

## Why K/DEF are excluded

Before this change, two things skewed grades around kicker and defense:

1. **An empty K or DEF slot subtracted 6 points**, same as an empty starting
   RB or WR slot — even though leaving K/DEF for literally the last picks of
   the draft is normal, correct strategy, not a mistake.
2. **K/DEF picks were included in the ADP value math.** Kicker and defense ADP
   is mostly noise — nobody scouts kickers — so drafting one a round "early" or
   "late" could register as a real reach or steal and swing a team's average
   value and trigger a "biggest reach" note for a position nobody cares about.

Grading now drops K/DEF slots from the roster board before starters/open-slots
are counted, and filters K/DEF picks out before the value math runs — so
whether a team has drafted a kicker or defense yet has zero effect on its grade.

See `tests/FantasyDraftAssistant.Core.Tests/DraftGraderTests.cs`:
`Missing_kicker_and_defense_do_not_lower_the_grade` and
`A_kicker_reach_does_not_affect_the_value_grade` for the regression tests that
pin this behavior down.
