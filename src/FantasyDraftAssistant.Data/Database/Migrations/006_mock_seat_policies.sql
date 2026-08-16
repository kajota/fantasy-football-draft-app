CREATE TABLE MockSeatPolicies (
    DraftId TEXT NOT NULL REFERENCES Drafts(DraftId) ON DELETE CASCADE,
    BranchId TEXT NOT NULL,
    TeamId TEXT NOT NULL,
    Personality TEXT NOT NULL,
    IsCpu INTEGER NOT NULL,
    PRIMARY KEY (DraftId, BranchId, TeamId)
);

CREATE INDEX IX_MockSeatPolicies_Branch ON MockSeatPolicies(DraftId, BranchId);
