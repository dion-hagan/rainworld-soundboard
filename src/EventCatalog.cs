using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SoundboardMod
{
    /// <summary>
    /// Forgiving name matching for the config file: "player death",
    /// "Player-Death" and "playerdeath" all mean the same thing, and a typo
    /// gets a "did you mean ...?" suggestion.
    /// </summary>
    public static class NameMatch
    {
        /// <summary>Lower-cases and drops everything but letters and digits.</summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }

            return sb.ToString();
        }

        /// <summary>The candidate closest to input, or null if nothing is reasonably close.</summary>
        public static string Closest(string input, IEnumerable<string> candidates)
        {
            string wanted = Normalize(input);
            if (wanted.Length == 0)
            {
                return null;
            }

            string best = null;
            int bestDistance = int.MaxValue;
            foreach (string candidate in candidates)
            {
                string normalized = Normalize(candidate);
                int distance = Distance(wanted, normalized);

                // "Player" -> "PlayerDeath"-style prefixes are also worth suggesting.
                if (normalized.StartsWith(wanted) || wanted.StartsWith(normalized))
                {
                    distance = Math.Min(distance, 1);
                }

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            int allowed = Math.Max(2, wanted.Length / 4);
            return bestDistance <= allowed ? best : null;
        }

        /// <summary>Levenshtein edit distance.</summary>
        public static int Distance(string a, string b)
        {
            int[] previous = new int[b.Length + 1];
            int[] current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++)
            {
                previous[j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                }

                int[] swap = previous;
                previous = current;
                current = swap;
            }

            return previous[b.Length];
        }
    }

    public sealed class EventInfo
    {
        public string Name;
        public string Description;
        public string Section;
    }

    /// <summary>
    /// Every event name a sound can be attached to: the fixed ones, plus two
    /// families generated from the game's creature list so that every
    /// creature type (including ones added by other mods) gets its own
    /// "spotted by" and "dies" event without a hook per creature.
    /// </summary>
    public sealed class EventCatalog
    {
        public const string SectionPlayer = "The player";
        public const string SectionWorld = "The world";
        public const string SectionCreatureDeath = "Creatures dying";
        public const string SectionSpotted = "The player being spotted";
        public const string SectionWater = "Water and breathing";
        public const string SectionGourmand = "The Gourmand";

        /// <summary>Event key fired when a creature of the given type (e.g. "RedLizard") is spotted by/spots the player.</summary>
        public static string SpottedKey(string creatureType)
        {
            return "PlayerSpottedBy" + creatureType;
        }

        /// <summary>Event key fired when a creature of the given type (e.g. "GreenLizard") dies.</summary>
        public static string DeathKey(string creatureType)
        {
            return creatureType + "Death";
        }

        private static readonly EventInfo[] FixedEvents =
        {
            new EventInfo { Section = SectionPlayer, Name = "PlayerDeath", Description = "The slugcat dies." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerJump", Description = "The slugcat jumps. Every single jump, no limit - see PlayerJumpCooldown for a rate-limited version." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerJumpCooldown", Description = "The slugcat jumps, but at most once per 'player-jump-cooldown' seconds (setting, default 2). Good for longer sounds that shouldn't pile up." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerJumpWithCicada", Description = "The slugcat jumps while holding a Cicada (\"squidcada\"). Fires alongside PlayerJump." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerArtificerPyroJump", Description = "Artificer's explosion-boosted jump, at most once per 'artificer-pyro-jump-cooldown' seconds (setting, default 10)." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerHardLanding", Description = "The slugcat lands hard: impact speed above 'hard-landing-speed' (setting, default 30)." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerTerminalVelocity", Description = "The slugcat falls at 'terminal-velocity' speed or faster (setting, default 40). Fires once per fall, when the speed is first reached." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerEat", Description = "The slugcat eats anything - fruit, plants or meat." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerEatCreature", Description = "The slugcat eats a creature (meat) rather than fruit or plants." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerGrabExplosive", Description = "The slugcat picks up an explosive spear or a scavenger bomb." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerGrabSlugcat", Description = "The slugcat picks up another slugcat." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerGrabYeek", Description = "The slugcat grabs a Yeek." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerThrowExplosiveSpear", Description = "The slugcat throws an explosive spear." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerBitByLizard", Description = "A lizard's bite lands on the slugcat." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerHitByDartMaggot", Description = "A Spitter Spider's dart maggot sticks into the slugcat." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerRoomTransition", Description = "The slugcat moves from one room into another (through a pipe/shortcut)." },
            new EventInfo { Section = SectionPlayer, Name = "PlayerEnterShelter", Description = "The slugcat walks into a shelter, before the door closes (so a long sound has time to play). Fires every time you enter one, even if you leave again without sleeping." },

            new EventInfo { Section = SectionWorld, Name = "RegionGateTransition", Description = "A region gate starts carrying you into the next region." },
            new EventInfo { Section = SectionWorld, Name = "CreatureEnteredOccupiedShelter", Description = "Any creature walks into a shelter that already has a player in it." },
            new EventInfo { Section = SectionWorld, Name = "SnailExplosion", Description = "A snail pops (its stunning shockwave)." },
            new EventInfo { Section = SectionWorld, Name = "VultureGrubSignal", Description = "A thrown vulture grub starts calling for vultures." },
            new EventInfo { Section = SectionWorld, Name = "FlareBombThrown", Description = "A flashbang is thrown by anyone." },
            new EventInfo { Section = SectionWorld, Name = "CyanLizardJump", Description = "A Cyan Lizard leaps." },
            new EventInfo { Section = SectionWorld, Name = "ScavengerThrowSpear", Description = "A scavenger throws a spear." },

            new EventInfo { Section = SectionCreatureDeath, Name = "ScavengerDeath", Description = "Any scavenger dies (every variant). For one variant use e.g. ScavengerEliteDeath." },
            new EventInfo { Section = SectionCreatureDeath, Name = "LizardDeath", Description = "Any lizard dies (all colours). For one colour use e.g. RedLizardDeath." },
            new EventInfo { Section = SectionCreatureDeath, Name = "SpiderDeath", Description = "A Spider or any Big Spider variant dies." },
            new EventInfo { Section = SectionCreatureDeath, Name = "CicadaOrLanternMouseDeath", Description = "A cicada or lantern mouse dies." },

            new EventInfo { Section = SectionSpotted, Name = "PlayerSpottedByPredator", Description = "A lizard, spider or vulture notices you - except scavengers, Cyan Lizards, Miros and the 'major threats' below, which have their own events. Once per creature per 'spotted-cooldown' seconds (setting, default 10)." },
            new EventInfo { Section = SectionSpotted, Name = "PlayerSpottedByScavenger", Description = "Any scavenger (every variant) notices you. For one variant use e.g. PlayerSpottedByScavengerElite." },
            new EventInfo { Section = SectionSpotted, Name = "PlayerSpottedByMajorThreat", Description = "A Red Lizard, Red Centipede, King Vulture or Daddy Long Legs notices you." },
            new EventInfo { Section = SectionSpotted, Name = "PlayerSpottedByMiros", Description = "A Miros Bird or Miros Vulture notices you." },

            new EventInfo { Section = SectionWater, Name = "PlayerSwimUnderwater", Description = "The slugcat dives under the surface: head fully under water and swimming (the deep-swim animation). Fires once per dive, and at most once per 'swim-underwater-cooldown' seconds (setting, default 5) so bobbing at the surface doesn't spam it." },
            new EventInfo { Section = SectionWater, Name = "PlayerDrowning", Description = "The slugcat runs low on air underwater - the point where the game slows it down and makes it thrash about. Fires once per struggle; it fires again only after the slugcat has recovered most of its breath." },
            new EventInfo { Section = SectionWater, Name = "PlayerDrowned", Description = "The slugcat dies of drowning. Fires alongside PlayerDeath." },
            new EventInfo { Section = SectionGourmand, Name = "GourmandSlideHit", Description = "The Gourmand's belly slide (or the rocket jump out of one) slams into a living creature and hurts it. Plays at the Gourmand, once per creature per half second." },
            new EventInfo { Section = SectionGourmand, Name = "GourmandDropHit", Description = "The Gourmand comes down hard on a living creature (a fast fall onto it) and hurts it. Plays at the Gourmand, once per creature per half second." },
            new EventInfo { Section = SectionGourmand, Name = "GourmandRollHit", Description = "The Gourmand rolls into a living creature and hurts it (the roll has its own half-second lockout). Plays at the Gourmand." },
        };

        private readonly List<EventInfo> all = new List<EventInfo>();
        private readonly Dictionary<string, string> byNormalized = new Dictionary<string, string>();

        /// <param name="creatureTypeNames">
        /// Names of every creature type the game knows (CreatureTemplate.Type),
        /// each of which gets a PlayerSpottedBy... and ...Death event.
        /// </param>
        public EventCatalog(IEnumerable<string> creatureTypeNames)
        {
            foreach (EventInfo info in FixedEvents)
            {
                Add(info);
            }

            foreach (string type in (creatureTypeNames ?? Enumerable.Empty<string>()).Where(t => !string.IsNullOrEmpty(t)).Distinct().OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
            {
                Add(new EventInfo { Section = SectionCreatureDeath, Name = DeathKey(type), Description = "A " + type + " dies." });
                Add(new EventInfo { Section = SectionSpotted, Name = SpottedKey(type), Description = "A " + type + " notices you. Once per creature per 'spotted-cooldown' seconds." });
            }
        }

        public IReadOnlyList<EventInfo> All => all;

        private void Add(EventInfo info)
        {
            string key = NameMatch.Normalize(info.Name);
            if (byNormalized.ContainsKey(key))
            {
                return; // e.g. the creature type "Spider" would generate the fixed "SpiderDeath" again
            }

            byNormalized[key] = info.Name;
            all.Add(info);
        }

        /// <summary>The canonical spelling of an event name, or null if it isn't one we know of.</summary>
        public string Resolve(string userText)
        {
            return byNormalized.TryGetValue(NameMatch.Normalize(userText), out string canonical) ? canonical : null;
        }

        public string Suggest(string userText)
        {
            return NameMatch.Closest(userText, all.Select(e => e.Name));
        }

        /// <summary>Human-readable list of every event, written next to the config so people can look names up.</summary>
        public string DescribeAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("CUSTOM SOUNDBOARD - EVERY EVENT NAME YOU CAN USE IN soundboard.yaml");
            sb.AppendLine("(Generated by the mod each time the game starts - don't edit this file.)");
            sb.AppendLine();
            sb.AppendLine("Names are not case sensitive, and spaces, dashes and underscores are ignored:");
            sb.AppendLine("'PlayerDeath', 'player death' and 'player-death' are the same event.");

            foreach (var section in all.GroupBy(e => e.Section))
            {
                sb.AppendLine();
                sb.AppendLine("== " + section.Key + " ==");
                foreach (EventInfo info in section)
                {
                    sb.AppendLine("  " + info.Name);
                    sb.AppendLine("      " + info.Description);
                }
            }

            return sb.ToString();
        }
    }
}
