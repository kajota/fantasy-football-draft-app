namespace FantasyDraftAssistant.Core.Ai;

public static class TauntStyles
{
    public const string Melville = "melville";
    public const string Kayfabe = "kayfabe";
    public const string LockerRoom = "locker-room";
    public const string PromptKind = "taunt";

    public static IReadOnlyDictionary<string, string> Assign(IEnumerable<string> enabledProviderKeys)
    {
        var keys = enabledProviderKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var grok = keys.FirstOrDefault(key => key.Equals(AiProviderCatalog.Xai, StringComparison.OrdinalIgnoreCase));
        if (grok is not null)
            map[grok] = LockerRoom;

        var rest = keys
            .Where(key => !map.ContainsKey(key))
            .OrderBy(ProviderOrder)
            .ToList();
        var leftover = new Queue<string>();
        leftover.Enqueue(Melville);
        leftover.Enqueue(Kayfabe);
        if (grok is null)
            leftover.Enqueue(LockerRoom);

        foreach (var key in rest)
            map[key] = leftover.Count > 0 ? leftover.Dequeue() : Kayfabe;

        return map;
    }

    public static string Title(string style) => style switch
    {
        Melville => "Melville",
        Kayfabe => "Kayfabe",
        LockerRoom => "Locker room",
        _ => "Taunt"
    };

    public static string Instructions(string style) => style switch
    {
        Melville =>
            """
            Voice: 19th-century American English, Melville / Moby-Dick. Long rolling sentences, nautical and biblical diction, scorn.
            Mean is allowed. No modern slang. One short paragraph. No pick advice.
            """,
        Kayfabe =>
            """
            Voice: professional-wrestling promo. Theatrical, second-person, insult their draft like a heel cut.
            Mean is allowed. Four to eight short sentences. No pick advice.
            """,
        LockerRoom =>
            """
            Voice: vulgar locker-room roast. Swearing is allowed and expected. Short, mean, funny.
            Punch their reaches and holes. Do not threaten real-world harm. No pick advice.
            """,
        _ => "Write a short mean taunt. No pick advice."
    };

    private static int ProviderOrder(string key)
    {
        if (key.Equals(AiProviderCatalog.Anthropic, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (key.Equals(AiProviderCatalog.OpenAi, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }
}
