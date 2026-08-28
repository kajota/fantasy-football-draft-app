-- An AI seat in the practice draft.
--
-- The seat's model and strategy are copied onto the policy row rather than read
-- live from the team, so a branch keeps drafting the way it started even if the
-- team is edited later.
ALTER TABLE Teams ADD COLUMN PracticeAiModel TEXT;
ALTER TABLE MockSeatPolicies ADD COLUMN AiModel TEXT;
ALTER TABLE MockSeatPolicies ADD COLUMN AiStrategy TEXT;

-- Picks are event-sourced, so the rationale for one lives beside the log rather
-- than in it. UsedFallback records that the deterministic policy made the pick
-- after the model failed, which is the difference between "the model reasoned
-- badly" and "the model never answered".
CREATE TABLE MockPickReasons (
    DraftId TEXT NOT NULL REFERENCES Drafts(DraftId) ON DELETE CASCADE,
    BranchId TEXT NOT NULL,
    OverallPick INTEGER NOT NULL,
    TeamId TEXT NOT NULL,
    Provider TEXT NOT NULL,
    Model TEXT NOT NULL,
    Strategy TEXT,
    Reason TEXT NOT NULL,
    UsedFallback INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    PRIMARY KEY (DraftId, BranchId, OverallPick)
);

CREATE INDEX IX_MockPickReasons_Branch ON MockPickReasons(DraftId, BranchId);
