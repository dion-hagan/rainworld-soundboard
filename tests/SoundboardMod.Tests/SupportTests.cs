using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace SoundboardMod.Tests
{
    public class EventCatalogTests
    {
        private static readonly EventCatalog Catalog = new EventCatalog(new[] { "RedLizard", "Scavenger", "Spider", "MirosVulture" });

        [Fact]
        public void ResolvesIgnoringCaseSpacesDashesAndUnderscores()
        {
            Assert.Equal("PlayerDeath", Catalog.Resolve("player death"));
            Assert.Equal("PlayerDeath", Catalog.Resolve("PLAYER-DEATH"));
            Assert.Equal("PlayerDeath", Catalog.Resolve("player_death"));
            Assert.Null(Catalog.Resolve("PlayerDeth"));
            Assert.Null(Catalog.Resolve(null));
        }

        [Fact]
        public void GeneratesPerCreatureEvents()
        {
            Assert.Equal("RedLizardDeath", Catalog.Resolve("RedLizardDeath"));
            Assert.Equal("PlayerSpottedByRedLizard", Catalog.Resolve("PlayerSpottedByRedLizard"));
            Assert.Equal("PlayerSpottedByMirosVulture", Catalog.Resolve("playerspottedbymirosvulture"));
            Assert.Equal("ScavengerDeath", Catalog.Resolve("ScavengerDeath"));
        }

        [Fact]
        public void GeneratedEventsDoNotDuplicateFixedOnes()
        {
            // The creature type "Spider" would generate "SpiderDeath", which the fixed list already has.
            Assert.Single(Catalog.All, e => e.Name == "SpiderDeath");
            Assert.Equal(Catalog.All.Count, Catalog.All.Select(e => NameMatch.Normalize(e.Name)).Distinct().Count());
        }

        [Fact]
        public void NewEventsAreDeclared()
        {
            foreach (string name in new[] { "PlayerRoomTransition", "PlayerJump", "PlayerJumpCooldown", "PlayerTerminalVelocity" })
            {
                Assert.NotNull(Catalog.Resolve(name));
            }
        }

        [Fact]
        public void SuggestsCloseNamesOnly()
        {
            Assert.Equal("PlayerDeath", Catalog.Suggest("PlayerDeth"));
            Assert.Equal("RedLizardDeath", Catalog.Suggest("RedLizrdDeath"));
            Assert.Null(Catalog.Suggest("CompletelyDifferentThing"));
        }

        [Fact]
        public void EveryEventHasADescriptionAndSection()
        {
            Assert.All(Catalog.All, e =>
            {
                Assert.False(string.IsNullOrWhiteSpace(e.Description));
                Assert.False(string.IsNullOrWhiteSpace(e.Section));
            });
        }

        [Fact]
        public void EventListMentionsEveryEvent()
        {
            string text = Catalog.DescribeAll();
            Assert.All(Catalog.All, e => Assert.Contains(e.Name, text));
        }
    }

    public class DelayQueueTests
    {
        [Fact]
        public void RunsActionsAfterTheRightNumberOfTicks()
        {
            var queue = new DelayQueue();
            var log = new List<string>();
            queue.Add(3, () => log.Add("three"));
            queue.Add(1, () => log.Add("one"));

            queue.Tick();
            Assert.Equal(new[] { "one" }, log);
            queue.Tick();
            queue.Tick();
            Assert.Equal(new[] { "one", "three" }, log);
            Assert.Equal(0, queue.Count);
        }

        [Fact]
        public void SameTickKeepsInsertionOrder()
        {
            var queue = new DelayQueue();
            var log = new List<int>();
            for (int i = 0; i < 5; i++)
            {
                int n = i;
                queue.Add(2, () => log.Add(n));
            }

            queue.Tick();
            queue.Tick();
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, log);
        }

        [Fact]
        public void ActionsMayScheduleMoreWork()
        {
            var queue = new DelayQueue();
            var log = new List<string>();
            queue.Add(1, () => queue.Add(1, () => log.Add("second")));
            queue.Tick();
            Assert.Empty(log);
            queue.Tick();
            Assert.Equal(new[] { "second" }, log);
        }

        [Fact]
        public void AFailingActionDoesNotStopTheOthers()
        {
            var errors = new List<Exception>();
            var queue = new DelayQueue(errors.Add);
            bool ran = false;
            queue.Add(1, () => throw new InvalidOperationException("boom"));
            queue.Add(1, () => ran = true);
            queue.Tick();
            Assert.True(ran);
            Assert.Single(errors);
        }

        [Fact]
        public void ClearDropsPendingWork()
        {
            var queue = new DelayQueue();
            bool ran = false;
            queue.Add(1, () => ran = true);
            queue.Clear();
            queue.Tick();
            Assert.False(ran);
        }

        [Theory]
        [InlineData(0f, 0)]
        [InlineData(-1f, 0)]
        [InlineData(0.001f, 1)]
        [InlineData(1f, 40)]
        [InlineData(2.5f, 100)]
        public void ConvertsSecondsToTicks(float seconds, int ticks)
        {
            Assert.Equal(ticks, DelayQueue.SecondsToTicks(seconds));
        }
    }

    public class FallTrackerTests
    {
        [Fact]
        public void FiresOnceWhenTheThresholdIsFirstReached()
        {
            var tracker = new FallTracker();
            Assert.False(tracker.Update(10f, false, 40f));
            Assert.False(tracker.Update(39.9f, false, 40f));
            Assert.True(tracker.Update(40f, false, 40f));
            Assert.False(tracker.Update(45f, false, 40f));
            Assert.False(tracker.Update(60f, false, 40f));
        }

        [Fact]
        public void RearmsAfterLanding()
        {
            var tracker = new FallTracker();
            Assert.True(tracker.Update(50f, false, 40f));
            Assert.False(tracker.Update(0f, true, 40f));
            Assert.True(tracker.Update(50f, false, 40f));
        }

        [Fact]
        public void RearmsAfterSlowingWellDownWithoutLanding()
        {
            var tracker = new FallTracker();
            Assert.True(tracker.Update(50f, false, 40f));
            Assert.False(tracker.Update(30f, false, 40f)); // still fast enough to count as the same fall
            Assert.False(tracker.Update(15f, false, 40f)); // caught a pole: re-armed now
            Assert.True(tracker.Update(41f, false, 40f));
        }

        [Fact]
        public void NeverFiresWhileGrounded()
        {
            var tracker = new FallTracker();
            Assert.False(tracker.Update(100f, true, 40f));
        }

        [Fact]
        public void TracksPeakSpeedForTuning()
        {
            var tracker = new FallTracker();
            tracker.Update(20f, false, 40f);
            tracker.Update(33f, false, 40f);
            tracker.Update(28f, false, 40f);
            Assert.Equal(33f, tracker.Peak);
            tracker.Update(0f, true, 40f);
            Assert.Equal(0f, tracker.Peak);
        }
    }

    public class CooldownTests
    {
        [Fact]
        public void BlocksUntilThePeriodHasPassedPerKey()
        {
            float now = 0f;
            var cooldown = new Cooldown(10f, () => now);
            object a = new object(), b = new object();

            Assert.True(cooldown.TryTrigger(a));
            Assert.False(cooldown.TryTrigger(a));
            Assert.True(cooldown.TryTrigger(b)); // separate key, separate cooldown
            now = 9.9f;
            Assert.False(cooldown.TryTrigger(a));
            now = 10f;
            Assert.True(cooldown.TryTrigger(a));
        }

        [Fact]
        public void LengthCanChangeWhileRunning()
        {
            float now = 0f;
            float length = 10f;
            var cooldown = new Cooldown(() => length, () => now);
            object key = new object();

            Assert.True(cooldown.TryTrigger(key));
            now = 3f;
            Assert.False(cooldown.TryTrigger(key));
            length = 2f; // player edited the setting and reloaded
            Assert.True(cooldown.TryTrigger(key));
        }

        [Fact]
        public void ZeroLengthNeverBlocks()
        {
            var cooldown = new Cooldown(0f, () => 1f);
            object key = new object();
            Assert.True(cooldown.TryTrigger(key));
            Assert.True(cooldown.TryTrigger(key));
        }
    }

    public class SoundRotationTests
    {
        [Fact]
        public void StepsThroughKeysAndWraps()
        {
            var keys = new[] { "a", "b", "c" };
            Assert.Equal("a", SoundRotation.NextKey(keys, _ => true, null));
            Assert.Equal("b", SoundRotation.NextKey(keys, _ => true, "a"));
            Assert.Equal("a", SoundRotation.NextKey(keys, _ => true, "c"));
        }

        [Fact]
        public void SkipsUnplayableKeysButKeepsTheirPlace()
        {
            var keys = new[] { "a", "b", "c" };
            Assert.Equal("c", SoundRotation.NextKey(keys, k => k != "b", "a"));
            Assert.Null(SoundRotation.NextKey(keys, _ => false, null));
            Assert.Null(SoundRotation.NextKey(new string[0], _ => true, null));
        }

        // --- shuffled ---------------------------------------------------------------

        /// <summary>Draws n keys the way SoundboardRuntime.Fire does, keeping the bag and last key between draws.</summary>
        private static List<string> Draw(int n, IList<string> keys, Func<string, bool> isPlayable, List<string> bag, Random random)
        {
            string last = null;
            var drawn = new List<string>();
            for (int i = 0; i < n; i++)
            {
                last = SoundRotation.NextShuffled(keys, isPlayable, bag, last, random);
                drawn.Add(last);
            }

            return drawn;
        }

        [Fact]
        public void ShuffledPlaysEveryKeyOncePerLap()
        {
            var keys = new[] { "a", "b", "c", "d", "e" };
            List<string> played = Draw(keys.Length * 20, keys, _ => true, new List<string>(), new Random(1));

            for (int lap = 0; lap < 20; lap++)
            {
                Assert.Equal(keys, played.Skip(lap * keys.Length).Take(keys.Length).OrderBy(k => k).ToArray());
            }
        }

        [Fact]
        public void ShuffledOrderDependsOnTheRandomSource()
        {
            var keys = Enumerable.Range(0, 8).Select(i => "k" + i).ToArray();
            var orders = new HashSet<string>();

            for (int seed = 0; seed < 30; seed++)
            {
                orders.Add(string.Join(",", Draw(keys.Length, keys, _ => true, new List<string>(), new Random(seed))));
            }

            // 8 keys have 40320 orders; 30 launches landing on only a couple of them would mean it isn't shuffling.
            Assert.True(orders.Count > 20, "only " + orders.Count + " different orders in 30 launches");
        }

        [Fact]
        public void ShuffledNeverRepeatsTheSameKeyBackToBack()
        {
            var keys = new[] { "a", "b", "c" };
            var bag = new List<string>();
            var random = new Random(7);

            List<string> played = Draw(600, keys, _ => true, bag, random);
            for (int i = 1; i < played.Count; i++)
            {
                Assert.NotEqual(played[i - 1], played[i]);
            }
        }

        [Fact]
        public void ShuffledSkipsUnplayableKeysAndThePlayableOnesStillTakeTurns()
        {
            var keys = new[] { "a", "b", "c", "d" };
            var bag = new List<string>();
            var random = new Random(3);

            List<string> played = Draw(30, keys, k => k != "b", bag, random);
            Assert.DoesNotContain("b", played);
            for (int lap = 0; lap < 10; lap++)
            {
                Assert.Equal(new[] { "a", "c", "d" }, played.Skip(lap * 3).Take(3).OrderBy(k => k).ToArray());
            }
        }

        [Fact]
        public void ShuffledKeyThatBecomesPlayableAgainStillGetsItsTurn()
        {
            var keys = new[] { "a", "b", "c" };
            var bag = new List<string>();
            var random = new Random(5);
            bool bReady = false;
            Func<string, bool> playable = k => k != "b" || bReady;

            List<string> first = Draw(2, keys, playable, bag, random);
            Assert.DoesNotContain("b", first);

            bReady = true;
            // The lap started with a, b, c: a and c have played, so b is all that's left in it.
            Assert.Equal("b", SoundRotation.NextShuffled(keys, playable, bag, first[1], random));
        }

        [Fact]
        public void ShuffledWithASingleKeyRepeatsIt()
        {
            var keys = new[] { "only" };
            Assert.Equal(new[] { "only", "only", "only" }, Draw(3, keys, _ => true, new List<string>(), new Random(0)));
        }

        [Fact]
        public void ShuffledReturnsNullWhenNothingIsPlayable()
        {
            var bag = new List<string>();
            Assert.Null(SoundRotation.NextShuffled(new[] { "a", "b" }, _ => false, bag, null, new Random(0)));
            Assert.Null(SoundRotation.NextShuffled(new string[0], _ => true, bag, null, new Random(0)));
        }

        [Fact]
        public void ShuffledForgetsKeysRemovedFromTheEvent()
        {
            var bag = new List<string> { "gone", "b" };
            string next = SoundRotation.NextShuffled(new[] { "a", "b" }, _ => true, bag, null, new Random(0));
            Assert.Equal("b", next);
            Assert.DoesNotContain("gone", bag);
        }
    }

    public class ConfigFilesTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "soundboard-tests-" + Guid.NewGuid().ToString("N"));

        public ConfigFilesTests()
        {
            Directory.CreateDirectory(root);
        }

        public void Dispose()
        {
            Directory.Delete(root, true);
        }

        private string Touch(params string[] parts)
        {
            string path = Path.Combine(new[] { root }.Concat(parts).ToArray());
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "x");
            return path;
        }

        [Fact]
        public void FirstRunCopiesTheDefaultConfigAndNeverOverwritesIt()
        {
            Touch("mod", "soundboard.yaml");
            File.WriteAllText(Path.Combine(root, "mod", "soundboard.yaml"), "default");
            var locations = new ConfigLocations(Path.Combine(root, "mod"), Path.Combine(root, "user"));

            Assert.True(locations.TryEnsureUserFiles(out _));
            Assert.Equal("default", File.ReadAllText(locations.UserConfigPath));
            Assert.True(Directory.Exists(locations.UserSoundsFolder));

            File.WriteAllText(locations.UserConfigPath, "mine");
            Assert.True(locations.TryEnsureUserFiles(out _));
            Assert.Equal("mine", File.ReadAllText(locations.UserConfigPath));
            Assert.Equal("mine", locations.ReadConfig(out string path));
            Assert.Equal(locations.UserConfigPath, path);
        }

        [Fact]
        public void FallsBackToTheBundledConfigWhenThereIsNoUserFile()
        {
            Touch("mod", "soundboard.yaml");
            var locations = new ConfigLocations(Path.Combine(root, "mod"), Path.Combine(root, "user"));
            locations.ReadConfig(out string path);
            Assert.Equal(locations.BundledConfigPath, path);
        }

        [Fact]
        public void UserSoundsBeatBundledSoundsWithTheSameName()
        {
            string bundled = Touch("mod", "sounds", "boom.wav");
            string user = Touch("user", "sounds", "boom.wav");
            var locations = new ConfigLocations(Path.Combine(root, "mod"), Path.Combine(root, "user"));

            Assert.Equal(user, SoundFileResolver.Find("boom.wav", locations.SoundFolders, out _));
            File.Delete(user);
            Assert.Equal(bundled, SoundFileResolver.Find("boom.wav", locations.SoundFolders, out _));
        }

        [Fact]
        public void ExtensionIsOptionalAndSubfoldersWork()
        {
            string wav = Touch("s", "boom.wav");
            string ogg = Touch("s", "funny", "haha.ogg");
            var folders = new[] { Path.Combine(root, "s") };

            Assert.Equal(wav, SoundFileResolver.Find("boom", folders, out _));
            Assert.Equal(wav, SoundFileResolver.Find("BOOM.WAV", folders, out _), ignoreCase: true); // Windows file names ignore case
            Assert.Equal(ogg, SoundFileResolver.Find("funny/haha.ogg", folders, out _));
            Assert.Equal(ogg, SoundFileResolver.Find("funny/haha", folders, out _));
        }

        [Fact]
        public void MissingFileSaysWhereToPutItAndSuggestsTypos()
        {
            Touch("s", "vine-boom.wav");
            var folders = new[] { Path.Combine(root, "s") };

            Assert.Null(SoundFileResolver.Find("vine-bom.wav", folders, out string problem));
            Assert.Contains("Did you mean 'vine-boom.wav'?", problem);
            Assert.Contains(folders[0], problem);
        }

        [Fact]
        public void UnsupportedAudioTypeIsExplained()
        {
            Touch("s", "song.flac");
            Assert.Null(SoundFileResolver.Find("song.flac", new[] { Path.Combine(root, "s") }, out string problem));
            Assert.Contains(".wav, .ogg or .mp3", problem);
        }

        [Fact]
        public void ResolveDropsUnplayableSoundsAndKeepsTheRest()
        {
            Touch("s", "good.wav");
            var catalog = new EventCatalog(new string[0]);
            SoundboardConfig config = SoundboardConfigParser.Parse(
                "events:\n  PlayerDeath:\n    - good.wav\n    - missing.wav\n  PlayerJump:\n    - alsomissing.wav\n", catalog);

            SoundFileResolver.Resolve(config, new[] { Path.Combine(root, "s") });

            Assert.Equal(2, config.Issues.Count);
            Assert.All(config.Issues, i => Assert.Equal(IssueSeverity.Error, i.Severity));
            EventBinding death = config.Events.Single(); // PlayerJump had nothing playable, so it's gone
            Assert.Equal("PlayerDeath", death.EventName);
            Assert.Equal("good.wav", death.Choices.Single().Sounds.Single().File);
            Assert.NotNull(death.Choices.Single().Sounds.Single().ResolvedPath);
        }
    }
}
