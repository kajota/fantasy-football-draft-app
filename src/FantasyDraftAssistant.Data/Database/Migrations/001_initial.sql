CREATE TABLE IF NOT EXISTS SchemaVersion (
    Version INTEGER PRIMARY KEY,
    AppliedAt TEXT NOT NULL
);

CREATE TABLE Leagues (
    LeagueId TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Platform TEXT NOT NULL,
    ExternalLeagueId TEXT,
    Season INTEGER NOT NULL,
    TeamCount INTEGER NOT NULL,
    UserTeamId TEXT,
    DraftType TEXT NOT NULL,
    RoundCount INTEGER NOT NULL,
    RosterSize INTEGER NOT NULL,
    DraftSourcePreference TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE Teams (
    TeamId TEXT PRIMARY KEY,
    LeagueId TEXT NOT NULL REFERENCES Leagues(LeagueId) ON DELETE CASCADE,
    Name TEXT NOT NULL,
    OwnerName TEXT,
    DisplayLabel TEXT,
    DraftPosition INTEGER NOT NULL,
    ExternalTeamId TEXT
);

CREATE INDEX IX_Teams_League ON Teams(LeagueId, DraftPosition);

CREATE TABLE RosterSlots (
    RosterSlotId TEXT PRIMARY KEY,
    LeagueId TEXT NOT NULL REFERENCES Leagues(LeagueId) ON DELETE CASCADE,
    SlotCode TEXT NOT NULL,
    SlotKind TEXT NOT NULL,
    Count INTEGER NOT NULL
);

CREATE TABLE RosterSlotEligiblePositions (
    RosterSlotId TEXT NOT NULL REFERENCES RosterSlots(RosterSlotId) ON DELETE CASCADE,
    Position TEXT NOT NULL,
    PRIMARY KEY (RosterSlotId, Position)
);

CREATE TABLE ScoringRules (
    ScoringRuleId TEXT PRIMARY KEY,
    LeagueId TEXT NOT NULL REFERENCES Leagues(LeagueId) ON DELETE CASCADE,
    Category TEXT NOT NULL,
    Points TEXT NOT NULL,
    UNIQUE (LeagueId, Category)
);

CREATE TABLE Drafts (
    DraftId TEXT PRIMARY KEY,
    LeagueId TEXT NOT NULL REFERENCES Leagues(LeagueId) ON DELETE CASCADE,
    Name TEXT NOT NULL,
    Season INTEGER NOT NULL,
    Status TEXT NOT NULL,
    ActiveBranchId TEXT,
    CurrentStateVersion INTEGER NOT NULL DEFAULT 0,
    SourceMode TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    StartedAt TEXT,
    CompletedAt TEXT
);

CREATE TABLE DraftBranches (
    BranchId TEXT PRIMARY KEY,
    DraftId TEXT NOT NULL REFERENCES Drafts(DraftId) ON DELETE CASCADE,
    Name TEXT NOT NULL,
    ParentBranchId TEXT,
    BranchPointOverallPick INTEGER NOT NULL DEFAULT 0,
    CreatedFromStateVersion INTEGER NOT NULL DEFAULT 0,
    CurrentHeadEventId TEXT,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE DraftSlots (
    DraftSlotId TEXT PRIMARY KEY,
    DraftId TEXT NOT NULL REFERENCES Drafts(DraftId) ON DELETE CASCADE,
    OverallPick INTEGER NOT NULL,
    Round INTEGER NOT NULL,
    RoundPick INTEGER NOT NULL,
    TeamId TEXT NOT NULL,
    IsKeeperSlot INTEGER NOT NULL DEFAULT 0,
    UNIQUE (DraftId, OverallPick)
);

CREATE TABLE DraftEvents (
    EventId TEXT PRIMARY KEY,
    DraftId TEXT NOT NULL REFERENCES Drafts(DraftId) ON DELETE CASCADE,
    BranchId TEXT NOT NULL,
    EventType TEXT NOT NULL,
    StateVersion INTEGER NOT NULL,
    SequenceNumber INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    CreatedBy TEXT NOT NULL,
    CorrelationId TEXT,
    PayloadJson TEXT NOT NULL,
    UNIQUE (DraftId, SequenceNumber)
);

CREATE INDEX IX_DraftEvents_Branch ON DraftEvents(DraftId, BranchId, SequenceNumber);

CREATE TABLE DraftCheckpoints (
    CheckpointId TEXT PRIMARY KEY,
    DraftId TEXT NOT NULL,
    BranchId TEXT NOT NULL,
    StateVersion INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL,
    Note TEXT
);

CREATE TABLE Keepers (
    KeeperId TEXT PRIMARY KEY,
    DraftId TEXT NOT NULL REFERENCES Drafts(DraftId) ON DELETE CASCADE,
    TeamId TEXT NOT NULL,
    PlayerId TEXT NOT NULL,
    RoundCost INTEGER NOT NULL,
    DraftSlotId TEXT,
    Notes TEXT
);

CREATE TABLE DraftQueueItems (
    QueueItemId TEXT PRIMARY KEY,
    DraftId TEXT NOT NULL,
    BranchId TEXT NOT NULL,
    PlayerId TEXT NOT NULL,
    SortOrder INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE Players (
    PlayerId TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    NflTeam TEXT NOT NULL,
    PrimaryPosition TEXT NOT NULL,
    ByeWeek INTEGER,
    Status TEXT NOT NULL,
    StatusUpdatedAt TEXT
);

CREATE TABLE PlayerEligiblePositions (
    PlayerId TEXT NOT NULL REFERENCES Players(PlayerId) ON DELETE CASCADE,
    Position TEXT NOT NULL,
    PRIMARY KEY (PlayerId, Position)
);

CREATE TABLE PlayerProviderIds (
    PlayerId TEXT NOT NULL REFERENCES Players(PlayerId) ON DELETE CASCADE,
    ProviderKey TEXT NOT NULL,
    ExternalId TEXT NOT NULL,
    PRIMARY KEY (PlayerId, ProviderKey)
);

CREATE TABLE PlayerStatuses (
    PlayerId TEXT PRIMARY KEY REFERENCES Players(PlayerId) ON DELETE CASCADE,
    Status TEXT NOT NULL,
    StatusUpdatedAt TEXT
);

CREATE TABLE FantasyDataProviders (
    ProviderKey TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL
);

CREATE TABLE FantasyDataRefreshes (
    RefreshId TEXT PRIMARY KEY,
    ProviderKey TEXT NOT NULL,
    Dataset TEXT NOT NULL,
    RefreshedAt TEXT NOT NULL,
    RecordCount INTEGER NOT NULL
);

CREATE TABLE RankingSources (
    SourceKey TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL
);

CREATE TABLE PlayerRankings (
    PlayerId TEXT NOT NULL,
    SourceKey TEXT NOT NULL,
    OverallRank INTEGER NOT NULL,
    PositionRank INTEGER,
    Tier INTEGER,
    SourceTimestamp TEXT,
    CachedAt TEXT NOT NULL,
    PRIMARY KEY (PlayerId, SourceKey)
);

CREATE TABLE ProjectionSources (
    SourceKey TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL
);

CREATE TABLE PlayerProjections (
    PlayerId TEXT NOT NULL,
    SourceKey TEXT NOT NULL,
    PassingAttempts REAL NOT NULL DEFAULT 0,
    Completions REAL NOT NULL DEFAULT 0,
    PassingYards REAL NOT NULL DEFAULT 0,
    PassingTouchdowns REAL NOT NULL DEFAULT 0,
    Interceptions REAL NOT NULL DEFAULT 0,
    RushingAttempts REAL NOT NULL DEFAULT 0,
    RushingYards REAL NOT NULL DEFAULT 0,
    RushingTouchdowns REAL NOT NULL DEFAULT 0,
    Targets REAL NOT NULL DEFAULT 0,
    Receptions REAL NOT NULL DEFAULT 0,
    ReceivingYards REAL NOT NULL DEFAULT 0,
    ReceivingTouchdowns REAL NOT NULL DEFAULT 0,
    SourceTimestamp TEXT,
    CachedAt TEXT NOT NULL,
    PRIMARY KEY (PlayerId, SourceKey)
);

CREATE TABLE AdpSources (
    SourceKey TEXT PRIMARY KEY,
    DisplayName TEXT NOT NULL
);

CREATE TABLE PlayerAdp (
    PlayerId TEXT NOT NULL,
    SourceKey TEXT NOT NULL,
    OverallAdp REAL NOT NULL,
    SourceTimestamp TEXT,
    CachedAt TEXT NOT NULL,
    PRIMARY KEY (PlayerId, SourceKey)
);

CREATE TABLE PlayerTiers (
    PlayerId TEXT NOT NULL,
    SourceKey TEXT NOT NULL,
    Tier INTEGER NOT NULL,
    PRIMARY KEY (PlayerId, SourceKey)
);

CREATE TABLE AiProviderConfigs (
    ProviderKey TEXT PRIMARY KEY,
    Enabled INTEGER NOT NULL,
    Model TEXT NOT NULL,
    Role TEXT NOT NULL,
    PerDraftSpendLimit TEXT,
    PerSessionSpendLimit TEXT
);

CREATE TABLE AiUsageRecords (
    UsageId TEXT PRIMARY KEY,
    DraftId TEXT NOT NULL,
    Provider TEXT NOT NULL,
    Model TEXT NOT NULL,
    AnalyzedStateVersion INTEGER NOT NULL,
    InputTokens INTEGER,
    OutputTokens INTEGER,
    EstimatedCost TEXT,
    LatencyMs INTEGER,
    RequestStartedAt TEXT NOT NULL,
    ResponseCompletedAt TEXT
);

CREATE TABLE AiResponses (
    ResponseId TEXT PRIMARY KEY,
    DraftId TEXT NOT NULL,
    BranchId TEXT NOT NULL,
    Provider TEXT NOT NULL,
    Model TEXT NOT NULL,
    AnalyzedStateVersion INTEGER NOT NULL,
    RequestStartedAt TEXT NOT NULL,
    ResponseCompletedAt TEXT,
    Body TEXT
);

CREATE TABLE ActiveDraftSelections (
    DraftId TEXT NOT NULL,
    BranchId TEXT NOT NULL,
    OverallPick INTEGER NOT NULL,
    EventId TEXT NOT NULL,
    DraftSlotId TEXT NOT NULL,
    Round INTEGER NOT NULL,
    RoundPick INTEGER NOT NULL,
    TeamId TEXT NOT NULL,
    PlayerId TEXT NOT NULL,
    Source TEXT NOT NULL,
    ExternalSourceId TEXT,
    ObservedAt TEXT NOT NULL,
    PRIMARY KEY (DraftId, BranchId, OverallPick)
);

CREATE TABLE TeamRosterProjection (
    DraftId TEXT NOT NULL,
    BranchId TEXT NOT NULL,
    TeamId TEXT NOT NULL,
    PlayerId TEXT NOT NULL,
    OverallPick INTEGER NOT NULL,
    PRIMARY KEY (DraftId, BranchId, TeamId, PlayerId)
);

CREATE TABLE PlayerAvailabilityProjection (
    DraftId TEXT NOT NULL,
    BranchId TEXT NOT NULL,
    PlayerId TEXT NOT NULL,
    IsAvailable INTEGER NOT NULL,
    PRIMARY KEY (DraftId, BranchId, PlayerId)
);

CREATE TABLE AppSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);
