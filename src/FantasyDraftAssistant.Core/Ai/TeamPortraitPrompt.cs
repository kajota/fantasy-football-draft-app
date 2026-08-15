using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Ai;

public enum TeamPortraitTone
{
    Hero = 0,
    Roast = 1,
    Normal = 2
}

public static class TeamPortraitPrompt
{
    public static TeamPortraitTone ToneFor(bool isUserTeam, bool normalImage) =>
        isUserTeam || normalImage ? TeamPortraitTone.Hero : TeamPortraitTone.Roast;

    private static readonly string[] RoastProps =
    [
        "wearing a cheese hat backwards",
        "holding a crumpled, coffee-stained draft board",
        "with nacho cheese on their shirt",
        "staring at a laptop that says 'AUTO-DRAFT FAILED'",
        "clutching a kicker in the first round on a tiny whiteboard",
        "wearing a tiny plastic football helmet that does not fit",
        "surrounded by empty pizza boxes and bad takes"
    ];

    private static readonly string[] HeroProps =
    [
        "in a sharp jacket with warm golden light",
        "looking calmly at a clean draft board they clearly own",
        "with a slight winning smirk, well-liked by the room",
        "looking like the commissioner everyone actually trusts"
    ];

    public static string Build(string teamName, string? ownerName, TeamPortraitTone tone, TeamId teamId)
    {
        var who = string.IsNullOrWhiteSpace(ownerName) || ownerName == teamName
            ? $"the fantasy football manager of the team \"{teamName}\""
            : $"the fantasy football manager {ownerName}, who runs the team \"{teamName}\"";

        if (tone is TeamPortraitTone.Hero or TeamPortraitTone.Normal)
        {
            return $"""
                Square caricature portrait of {who}, {Pick(HeroProps, teamId)}.
                Over-the-top awesome: extremely handsome, brilliant, magnetic, the coolest person in the draft room.
                Clearly a fantasy football genius. People love them. Heroic comic-book lighting, cinematic, flattering.
                Not a real celebrity. No text, no logos, no watermark, no extra panels.
                """;
        }

        return $"""
            Square caricature portrait of {who}, {Pick(RoastProps, teamId)}.
            Unflattering roast cartoon: goofy, a bit ugly, lost, terrible at fantasy football.
            Mean and funny, not photoreal, not a real celebrity, not gore.
            No text, no logos, no watermark, no extra panels.
            """;
    }

    private static string Pick(IReadOnlyList<string> items, TeamId teamId)
    {
        var bytes = teamId.Value.ToByteArray();
        var index = Math.Abs(BitConverter.ToInt32(bytes, 0)) % items.Count;
        return items[index];
    }
}
