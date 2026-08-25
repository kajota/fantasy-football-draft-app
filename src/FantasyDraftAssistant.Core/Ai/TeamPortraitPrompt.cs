using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;

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
        "dropping nachos onto a keyboard",
        "talking into a dead headset",
        "pointing at the wrong column on a spreadsheet",
        "scratching his balls while doomscrolling the waiver wire",
        "picking his nose and wiping it on the remote",
        "belching beer foam onto his draft board",
        "farting then waving the stench toward his face",
        "adjusting his sweaty crotch mid-trade rant",
        "spitting sunflower seeds straight into his laptop keyboard",
        "scratching deep under his ballsack during the draft",
        "drooling pizza grease down his stained tank top",
        "yelling with crumbs stuck in his teeth and beard",
        "burping, scratching his ass, then high-fiving the air"
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

    public static readonly PortraitArtStyle RandomArtStyle = new("", "Random", "", PortraitMedium.Mixed);

    public static readonly IReadOnlyList<PortraitArtStyle> ArtStyles =
    [
        RandomArtStyle,
        new("graphic-novel", "Graphic novel",
            "bold graphic-novel ink and flat color, hard black contours, cel shading, comic-book panel, not a painting",
            PortraitMedium.Ink),
        new("oil", "Oil portrait",
            "painterly oil on canvas, visible brushstrokes, gallery lighting, thick paint, not a photo",
            PortraitMedium.Paint),
        new("magazine", "1960s magazine",
            "clean 1960s print-magazine illustration, limited ink, vintage halftone, Mad Men editorial",
            PortraitMedium.Print),
        new("animation", "Animation still",
            "2D animation still, clean character lines, studio-cartoon shading, not live action, not oil paint",
            PortraitMedium.Ink),
        new("noir", "Noir comic",
            "black-and-white noir comic, heavy ink shadows, screentone, Sin City linework, almost no color, not a color painting",
            PortraitMedium.Ink),
        new("risograph", "Risograph poster",
            "misregistered risograph poster, 2-3 ink colors, grainy paper, DIY print shop, not cinematic",
            PortraitMedium.Print),
        new("gouache", "Storybook gouache",
            "opaque gouache storybook painting, matte pigment, soft edges, children's-book plate, not a photo",
            PortraitMedium.Paint),
        new("pop-art", "Pop art",
            "1960s pop art, Ben-Day dots, flat primary colors, thick comic outline, Warhol/Lichtenstein, not painterly realism",
            PortraitMedium.Print),
        new("photoreal", "Photoreal",
            "photoreal cinematic still, natural skin, real camera, sharp detail, not a cartoon, not illustrated, not painted",
            PortraitMedium.Photo),
        new("flyer", "Supermarket flyer",
            "ugly supermarket weekly flyer art, cheap clip-art shading, harsh product lighting, coupon-page character, not cinematic",
            PortraitMedium.Print),
        new("cartoon", "Editorial cartoon",
            "newspaper editorial cartoon: thick black ink, crosshatching, exaggerated caricature, 2-3 flat colors on newsprint, political-cartoon headshot",
            PortraitMedium.Ink),
        new("clip-art", "Clip art",
            "1990s Microsoft clip-art, simple vector shapes, tiny color palette, office-software mascot, not detailed illustration",
            PortraitMedium.Print),
        new("watercolor", "Muddy watercolor",
            "wet muddy watercolor on cheap paper, bleeds and stains, loose pigment",
            PortraitMedium.Paint),
        new("snapshot", "Phone snapshot",
            "bad phone snapshot, on-camera flash, awkward selfie angle, jpeg noise, real photography, not illustrated",
            PortraitMedium.Photo),
        new("wallpaper", "1998 wallpaper",
            "1998 Windows desktop wallpaper collage, lens flares, beveled CGI, stock photos pasted together, not a painted portrait",
            PortraitMedium.Collage),
        new("studio", "Grocery-store studio",
            "unkind mall portrait-studio photo, mottled backdrop, flash, stiff pose, real photography, not a painting",
            PortraitMedium.Photo),
        new("zine", "Zine drawing",
            "scratchy photocopied zine drawing, ballpoint and marker, white paper, xerox speckles",
            PortraitMedium.Ink)
    ];

    public static PortraitArtStyle StyleByKey(string? key) =>
        ArtStyles.FirstOrDefault(style => style.Key == key) ?? RandomArtStyle;

    public static string Resolve(TeamPortraitRequest request) =>
        !string.IsNullOrWhiteSpace(request.CustomPrompt)
            ? request.CustomPrompt.Trim()
            : Build(
                request.TeamName,
                request.OwnerName,
                request.Tone,
                request.TeamId,
                request.PortraitNotes,
                request.Spin,
                request.ArtStyleKey);

    public static string Build(
        string teamName,
        string? ownerName,
        TeamPortraitTone tone,
        TeamId teamId,
        string? portraitNotes = null,
        int spin = 0,
        string? artStyleKey = null)
    {
        var who = string.IsNullOrWhiteSpace(ownerName) || ownerName == teamName
            ? $"the fantasy football manager of the team \"{teamName}\""
            : $"the fantasy football manager {ownerName}, who runs the team \"{teamName}\"";
        var notes = portraitNotes?.Trim();
        var notAMan = SuggestsNotAMan(ownerName, teamName);
        var subject = string.IsNullOrWhiteSpace(notes)
            ? notAMan
                ? $"{Pick(WomanSubjects, teamId, 0)} in their {Pick(Ages, teamId, 1)}, {Pick(Features, teamId, 2)}"
                : $"{Pick(WhiteManSubjects, teamId, 0)} in their {Pick(Ages, teamId, 1)}, {Pick(WhiteManFeatures, teamId, 2)}"
            : $"described here: {notes}. Honor that description for age, gender, race, hair, and build. Do not invent a different person";
        var roastFaces = notAMan ? RoastFacesWoman : RoastFacesMan;
        var style = ResolveStyle(spin, artStyleKey);
        var outfit = PickSpin(LooksFor(tone, style.Medium), spin, 3);
        var place = PickSpin(PlacesFor(tone, style.Medium), spin, 4);

        if (tone is TeamPortraitTone.Hero or TeamPortraitTone.Normal)
        {
            return $"""
                Art style: {style.Phrase}.
                Square image of {who} as {subject}.
                Outfit: {outfit}. Setting: {place}.
                Mood: {PickSpin(HeroEnergy, spin, 5)}.
                Let the team name "{teamName}" flavor the colors and attitude.
                Specific to this person. Distinct face.
                {Marking(teamName, ownerName, spin)}
                """;
        }

        return $"""
            Art style: {style.Phrase}.
            Square roast of {who} as {subject}.
            Face: {PickSpin(roastFaces, spin, 3)}. Outfit: {PickSpin(RoastLooks, spin, 4)}.
            Setting: {PickSpin(PlacesFor(tone, style.Medium), spin, 5)}. Caught: {PickSpin(RoastFails, spin, 6)}.
            Let the team name "{teamName}" flavor the mess.
            Mean and funny. Distinct face and body.
            {Marking(teamName, ownerName, spin)}
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

    public static string Marking(string teamName, string? ownerName, int spin)
    {
        var owner = string.IsNullOrWhiteSpace(ownerName) ? teamName : ownerName;
        return PickSpin(
        [
            "No lettering and no logos this time.",
            "No readable words. Colors only.",
            $"A handmade emblem inspired by \"{teamName}\", letters optional.",
            $"The name \"{teamName}\" appears once, small, on a mug, hat, or pennant.",
            $"A jersey or hoodie that might say \"{teamName}\" if it fits naturally.",
            $"A background poster or draft board with \"{teamName}\" on it, not the focus.",
            $"Tiny initials for {owner}, easy to miss.",
            "A logo-ish shape only. Skip words."
        ], spin, 8);
    }

    private static PortraitArtStyle ResolveStyle(int spin, string? artStyleKey)
    {
        var picked = StyleByKey(artStyleKey);
        if (!string.IsNullOrEmpty(picked.Key))
            return picked;
        var pool = ArtStyles.Where(style => style.Key.Length > 0).ToArray();
        return pool[(int)((uint)unchecked(spin * 104729 + 6 * 7919) % (uint)pool.Length)];
    }

    private static IReadOnlyList<string> LooksFor(TeamPortraitTone tone, PortraitMedium medium)
    {
        if (tone is not TeamPortraitTone.Hero and not TeamPortraitTone.Normal)
            return RoastLooks;
        return medium is PortraitMedium.Ink or PortraitMedium.Print
            ? InkLooks
            : HeroLooks;
    }

    private static IReadOnlyList<string> PlacesFor(TeamPortraitTone tone, PortraitMedium medium)
    {
        if (medium is PortraitMedium.Ink or PortraitMedium.Print)
            return InkPlaces;
        return tone is TeamPortraitTone.Hero or TeamPortraitTone.Normal ? HeroPlaces : RoastPlaces;
    }

    private static readonly string[] InkLooks =
    [
        "a rumpled suit and a too-tight collar",
        "a bow tie and a patriotic lapel pin",
        "shirtsleeves and rolled cuffs, ink on the fingers",
        "a debate-stage blazer",
        "a plain tee, drawn with three lines",
        "an oversized jacket and tiny head, caricature proportions",
        "a newspaper-column headshot collar",
        "no fancy tailoring — keep the clothes as simple ink shapes"
    ];

    private static readonly string[] InkPlaces =
    [
        "on blank newsprint with a caption box",
        "as a newspaper editorial-page headshot",
        "behind a podium on a white page",
        "on a desk with an inkwell, hatched shadows only",
        "against a plain background, no city, no rain",
        "inside a comic panel with a fat black border",
        "on a xeroxed flyer, white paper showing",
        "as a political-cartoon corner portrait, empty space around"
    ];

    private static string Pick(IReadOnlyList<string> items, TeamId teamId, int lane)
    {
        var bytes = teamId.Value.ToByteArray();
        var index = Math.Abs(bytes[lane % bytes.Length] + bytes[(lane + 7) % bytes.Length] * 13 + lane * 31) % items.Count;
        return items[index];
    }

    private static string PickSpin(IReadOnlyList<string> items, int spin, int lane)
    {
        var mixed = unchecked(spin * 104729 + lane * 7919);
        var index = (int)((uint)mixed % (uint)items.Count);
        return items[index];
    }
}

public sealed record PortraitArtStyle(string Key, string Title, string Phrase, PortraitMedium Medium)
{
    public override string ToString() => Title;
}

public enum PortraitMedium
{
    Mixed = 0,
    Ink = 1,
    Paint = 2,
    Photo = 3,
    Print = 4,
    Collage = 5
}
