namespace FantasyDraftAssistant.Core.Ai;

public static class AiAnalysisMode
{
    public const string FastAdvisor = "Fast Advisor";
    public const string DeepAdvisor = "Deep Advisor";
    public const string DraftWatcher = "Draft Watcher";

    public static bool IsDeep(bool askDeep, string? role) =>
        askDeep || string.Equals(role, DeepAdvisor, StringComparison.OrdinalIgnoreCase);

    public static bool IsWatcher(string? role) =>
        string.Equals(role, DraftWatcher, StringComparison.OrdinalIgnoreCase);
}
