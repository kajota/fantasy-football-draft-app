namespace FantasyDraftAssistant.Core.Enums;

public enum FantasyPlatform
{
    Manual = 0,
    Yahoo = 1
}

public enum DraftType
{
    Snake = 0,
    Linear = 1,
    Custom = 2
}

public enum DraftStatus
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2
}

public enum SlotKind
{
    Required = 0,
    Flex = 1,
    Bench = 2,
    Inactive = 3
}

public enum PlayerPosition
{
    QB = 0,
    RB = 1,
    WR = 2,
    TE = 3,
    K = 4,
    DEF = 5
}

public enum PlayerStatus
{
    Active = 0,
    Questionable = 1,
    Doubtful = 2,
    Out = 3,
    InjuredReserve = 4,
    PhysicallyUnableToPerform = 5,
    Suspended = 6,
    NonFootballInjury = 7
}

public enum DraftEventType
{
    DraftStarted = 0,
    PlayerDrafted = 1,
    PickCorrected = 2,
    DraftRolledBack = 3,
    DraftRedone = 4,
    DraftBranchCreated = 5,
    DraftCompleted = 6,
    KeepersReplaced = 7
}

public enum PickSource
{
    Manual = 0,
    Yahoo = 1,
    Keeper = 2,
    Simulation = 3
}

public enum MockPersonality
{
    BestAvailable = 0,
    RbFirst = 1,
    HeroRb = 2,
    ZeroRb = 3,
    WrHeavy = 4,
    QbEarly = 5,
    LateQb = 6,
    RookieHunter = 7,
    AdpHunter = 8,

    // Hands the seat's picks to an AI model. Deliberately absent from
    // MockPersonalityCatalog.DealBag, so it is never assigned at random -
    // a seat only drafts this way if someone chose it by hand.
    Ai = 9
}

public enum DraftSourceMode
{
    Manual = 0,
    YahooSynchronized = 1,
    YahooReconnecting = 2,
    ReconciliationRequired = 3
}

public enum DraftSourcePreference
{
    Manual = 0,
    Yahoo = 1
}

public enum ReadinessLevel
{
    Ready = 0,
    Attention = 1,
    Failed = 2,
    Optional = 3,
    Disabled = 4
}

public enum AlertKind
{
    Value = 0,
    Position = 1,
    DraftRun = 2,
    OpponentNeed = 3,
    QbScarcity = 4,
    PickApproaching = 5,
    Handcuff = 6
}

public enum ScoringCategory
{
    PassingYard = 0,
    PassingTouchdown = 1,
    Interception = 2,
    RushingYard = 3,
    RushingTouchdown = 4,
    Reception = 5,
    ReceivingYard = 6,
    ReceivingTouchdown = 7,
    FumbleLost = 8,
    TwoPointConversion = 9,
    FieldGoal = 10,
    ExtraPoint = 11,
    DefensiveTouchdown = 12,
    Sack = 13,
    DefensiveInterception = 14,
    FumbleRecovery = 15,
    Safety = 16,
    PointsAllowed0 = 17,
    PointsAllowed1To6 = 18,
    PointsAllowed7To13 = 19,
    PointsAllowed14To20 = 20,
    PointsAllowed21To27 = 21,
    PointsAllowed28To34 = 22,
    PointsAllowed35Plus = 23,
    FieldGoal0To19 = 24,
    FieldGoal20To29 = 25,
    FieldGoal30To39 = 26,
    FieldGoal40To49 = 27,
    FieldGoal50Plus = 28,
    ExtraPointReturned = 29
}
