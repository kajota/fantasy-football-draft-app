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

    private static readonly string[] Ages =
    [
        "early 20s",
        "late 20s",
        "mid 30s",
        "early 40s",
        "mid 50s",
        "early 60s"
    ];

    private static readonly string[] WhiteManSubjects =
    [
        "a white man",
        "a white man with a full beard",
        "a lanky white man",
        "a stocky white man",
        "a white man with silver hair",
        "a white man with a crew cut",
        "a white man with a thick mustache",
        "a white man with glasses"
    ];

    private static readonly string[] WhiteManFeatures =
    [
        "fair skin and blue eyes",
        "pale freckled skin and sandy hair",
        "light skin and a square jaw",
        "ruddy complexion and brown hair",
        "fair skin and a roman nose",
        "pale skin and close-cropped blond hair",
        "light skin and salt-and-pepper stubble",
        "fair skin and dark brown hair"
    ];

    private static readonly string[] WomanSubjects =
    [
        "a woman",
        "a woman with short hair",
        "a woman with silver hair",
        "a stocky woman",
        "a lanky woman",
        "a woman with glasses",
        "a woman with a sharp bob",
        "a woman with long hair"
    ];

    private static readonly string[] Features =
    [
        "deep brown skin and gold jewelry",
        "light olive skin and a sharp nose",
        "pale freckled skin and red hair",
        "warm tan skin and tight curls",
        "East Asian features and round glasses",
        "South Asian features and a bright smile",
        "dark skin and a shaved head",
        "light brown skin and a beauty mark"
    ];

    private static readonly HashSet<string> NotAManTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "she", "her", "hers", "girl", "girls", "lady", "ladies", "woman", "women",
        "mom", "mum", "mama", "mother", "aunt", "grandma", "grandmother", "nana", "nanny",
        "mrs", "miss", "ms", "queen", "princess", "wife", "sister", "daughter",
        "sara", "sarah", "emily", "emma", "olivia", "ava", "mia", "amelia", "sophia", "isabella",
        "charlotte", "harper", "evelyn", "abigail", "elizabeth", "sofia", "ella", "scarlett",
        "grace", "chloe", "camila", "penelope", "layla", "riley", "zoey", "nora", "lily",
        "eleanor", "hannah", "lillian", "addison", "aubrey", "ellie", "stella", "natalie",
        "zoe", "leah", "hazel", "violet", "aurora", "savannah", "audrey", "brooklyn", "bella",
        "claire", "skylar", "lucy", "paisley", "everly", "anna", "caroline", "nova", "genesis",
        "aaliyah", "kennedy", "kinsley", "allison", "maya", "willow", "naomi", "aileen",
        "jennifer", "jessica", "ashley", "amanda", "stephanie", "nicole", "melissa", "michelle",
        "kimberly", "amy", "angela", "heather", "rebecca", "laura", "stephanie", "rachel",
        "katherine", "kathryn", "catherine", "karen", "susan", "nancy", "betty", "dorothy",
        "helen", "sandra", "donna", "carol", "ruth", "sharon", "patricia", "linda", "barbara",
        "mary", "maria", "marie", "ann", "anne", "annie", "diane", "diana", "deborah", "debra",
        "cynthia", "kathleen", "pamela", "martha", "debra", "janet", "catherine", "frances",
        "christine", "annette", "julie", "joyce", "victoria", "andrea", "jasmine",
        "brittany", "samantha", "alexandra", "alexa", "christina", "vanessa", "tiffany",
        "lauren", "Megan", "megan", "alison", "erica", "erika", "holly", "katie", "kate",
        "kathy", "cathy", "cindy", "trina", "trina", "brenda", "denise", "tracy", "wendy",
        "gina", "tanya", "sonia", "priya", "aisha", "fatima", "yin", "mei", "yuki", "hana",
        "sofia", "lucia", "isabel", "elena", "carmen", "rosa", "ana", "juana", "gabriela"
    };

    private static readonly string[] HeroLooks =
    [
        "tailored midnight suit and no tie",
        "vintage leather jacket over a clean tee",
        "cream linen shirt, sleeves rolled, expensive watch",
        "black turtleneck and a wool overcoat",
        "game-day bomber with tasteful team colors",
        "silk scarf, gold frames, unbothered posture",
        "athlete-casual: fitted hoodie and championship energy",
        "western shirt, good hat, movie-star stubble"
    ];

    private static readonly string[] HeroPlaces =
    [
        "on a rooftop at golden hour",
        "in a dark wood study with a glowing board behind them",
        "walking through falling confetti",
        "in a sunlit kitchen that somehow looks expensive",
        "leaning on a vintage convertible",
        "in a packed sports bar that is cheering for them",
        "on a mountain lookout with a city below",
        "in a neon night market, rain-slick street"
    ];

    private static readonly string[] HeroStyles =
    [
        "bold graphic-novel ink and flat color",
        "painterly oil-portrait lighting",
        "clean 1960s magazine illustration",
        "crisp modern animation still",
        "rich noir comic with gold highlights",
        "sun-faded risograph poster",
        "lush storybook gouache",
        "high-contrast pop-art portrait"
    ];

    private static readonly string[] HeroEnergy =
    [
        "quiet unstoppable confidence",
        "laughing like they already won",
        "a look that says they drafted three rounds ahead",
        "warm, beloved, the person everyone sits next to",
        "cinematic hero landing, coat moving in the wind",
        "subtle smirk, one eyebrow, total control"
    ];

    private static readonly string[] RoastFacesMan =
    [
        "tiny eyes too close together",
        "a huge shiny forehead",
        "a weak chin and a startled mouth",
        "one eyebrow permanently raised in confusion",
        "a patchy mustache and a blotchy neck",
        "bags under the eyes that have bags",
        "a lopsided grin with a missing vibe, not a missing tooth",
        "hair that lost a fight with a ceiling fan"
    ];

    private static readonly string[] RoastFacesWoman =
    [
        "tiny eyes too close together",
        "a huge shiny forehead",
        "a weak chin and a startled mouth",
        "one eyebrow permanently raised in confusion",
        "a blotchy neck and a stiff smile",
        "bags under the eyes that have bags",
        "a lopsided grin with a missing vibe, not a missing tooth",
        "hair that lost a fight with a ceiling fan"
    ];

    private static readonly string[] RoastLooks =
    [
        "a stained team hoodie two sizes too small",
        "cargo shorts and black socks with sandals",
        "a wrinkled button-down half tucked",
        "a novelty jersey of a player who retired",
        "a Bluetooth headset from 2009",
        "a fanny pack worn as a bandolier",
        "pajama pants and a blazer",
        "a visor, zinc on the nose, indoors"
    ];

    private static readonly string[] RoastPlaces =
    [
        "on a sagging lawn chair in a garage",
        "at a folding table under a buzzing fluorescent",
        "in a basement with one sad pennant",
        "in a minivan full of crushed cans",
        "in a cubicle with a dying plant",
        "on a porch at 11am in February",
        "in a food-court booth alone",
        "next to a grill that will not light"
    ];

    private static readonly string[] RoastFails =
    [
        "clutching a kicker magnet on an empty draft board",
        "staring at a laptop that is clearly auto-drafting",
        "holding a ranking printout from 2019",
        "celebrating a waiver pickup nobody wanted",
        "wearing a cheese hat backwards",
        "dropping nachos onto a keyboard",
        "talking into a dead headset",
        "pointing at the wrong column on a spreadsheet"
    ];

    private static readonly string[] RoastStyles =
    [
        "ugly-cute supermarket flyer illustration",
        "mean editorial cartoon with thick ink",
        "cheap clip-art energy, on purpose",
        "muddy watercolor that looks slightly damp",
        "overlit phone snapshot turned into a cartoon",
        "1998 desktop-wallpaper collage",
        "grocery-store portrait-studio lighting, unkind",
        "scratchy zine drawing"
    ];

    public static string Build(string teamName, string? ownerName, TeamPortraitTone tone, TeamId teamId)
    {
        var who = string.IsNullOrWhiteSpace(ownerName) || ownerName == teamName
            ? $"the fantasy football manager of the team \"{teamName}\""
            : $"the fantasy football manager {ownerName}, who runs the team \"{teamName}\"";
        var notAMan = SuggestsNotAMan(ownerName, teamName);
        var subject = notAMan
            ? $"{Pick(WomanSubjects, teamId, 0)} in their {Pick(Ages, teamId, 1)}, {Pick(Features, teamId, 2)}"
            : $"{Pick(WhiteManSubjects, teamId, 0)} in their {Pick(Ages, teamId, 1)}, {Pick(WhiteManFeatures, teamId, 2)}";
        var roastFaces = notAMan ? RoastFacesWoman : RoastFacesMan;

        if (tone is TeamPortraitTone.Hero or TeamPortraitTone.Normal)
        {
            return $"""
                Square illustrated portrait of {who} as {subject}.
                Outfit: {Pick(HeroLooks, teamId, 3)}. Setting: {Pick(HeroPlaces, teamId, 4)}.
                Mood: {Pick(HeroEnergy, teamId, 5)}. Art style: {Pick(HeroStyles, teamId, 6)}.
                Let the team name "{teamName}" flavor the colors and attitude.
                Over-the-top awesome and specific to this person. Extremely magnetic and good at fantasy football.
                Distinct face. Do not reuse a generic handsome leading-man. Make them memorable and different.
                Not a real celebrity. No watermark. No extra panels.
                {Marking(teamName, ownerName, teamId)}
                """;
        }

        return $"""
            Square illustrated roast portrait of {who} as {subject}.
            Face: {Pick(roastFaces, teamId, 3)}. Outfit: {Pick(RoastLooks, teamId, 4)}.
            Setting: {Pick(RoastPlaces, teamId, 5)}. Caught: {Pick(RoastFails, teamId, 6)}.
            Art style: {Pick(RoastStyles, teamId, 7)}.
            Let the team name "{teamName}" flavor the mess.
            Mean and funny, a unique loser — not the same goofy guy every time.
            Distinct face and body. Not photoreal, not a real celebrity, not gore.
            No watermark. No extra panels.
            {Marking(teamName, ownerName, teamId)}
            """;
    }

    public static bool SuggestsNotAMan(string? ownerName, string teamName)
    {
        foreach (var token in Tokens(ownerName).Concat(Tokens(teamName)))
        {
            if (NotAManTokens.Contains(token))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> Tokens(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;
        var parts = value.Split([' ', '-', '_', '.', ',', '/', '&', '\'', '"'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
            yield return part.Trim();
    }

    public static string Marking(string teamName, string? ownerName, TeamId teamId)
    {
        var owner = string.IsNullOrWhiteSpace(ownerName) ? teamName : ownerName;
        return Pick(
        [
            "No lettering and no logos this time.",
            "No readable words. Colors only.",
            $"A handmade emblem inspired by \"{teamName}\", letters optional.",
            $"The name \"{teamName}\" appears once, small, on a mug, hat, or pennant.",
            $"A jersey or hoodie that might say \"{teamName}\" if it fits naturally.",
            $"A background poster or draft board with \"{teamName}\" on it, not the focus.",
            $"Tiny initials for {owner}, easy to miss.",
            "A logo-ish shape only. Skip words."
        ], teamId, 8);
    }

    private static string Pick(IReadOnlyList<string> items, TeamId teamId, int lane)
    {
        var bytes = teamId.Value.ToByteArray();
        var index = Math.Abs(bytes[lane % bytes.Length] + bytes[(lane + 7) % bytes.Length] * 13 + lane * 31) % items.Count;
        return items[index];
    }
}
