using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Xunit;

namespace SoundboardMod.Tests
{
    /// <summary>
    /// What Rain World's in-game Steam Workshop uploader (SteamWorkshopUploader / RainWorldSteamManager in the
    /// decompiled game) and Remix's mod loader (ModManager.LoadModFromJson) need from mod/, so a broken package is
    /// caught here instead of in the middle of an upload. The rules are quoted from the game's code in the comments.
    /// </summary>
    public class WorkshopPackageTests
    {
        private static string RepoRoot()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "SoundboardMod.sln")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.NotNull(dir);
            return dir;
        }

        private static string ModFolder() => Path.Combine(RepoRoot(), "mod");

        private static Dictionary<string, object> LoadModinfo()
        {
            string json = File.ReadAllText(Path.Combine(ModFolder(), "modinfo.json"));
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        }

        private static string Str(Dictionary<string, object> info, string key)
        {
            Assert.True(info.ContainsKey(key), "modinfo.json has no \"" + key + "\"");
            return Assert.IsType<string>(info[key]);
        }

        // ---- modinfo.json ----

        [Fact]
        public void ModinfoIsValidJson()
        {
            Assert.NotEmpty(LoadModinfo());
        }

        [Fact]
        public void ModinfoHasTheFieldsTheWorkshopNeeds()
        {
            Dictionary<string, object> info = LoadModinfo();
            foreach (string key in new[] { "id", "name", "version", "target_game_version", "authors", "description" })
            {
                Assert.False(string.IsNullOrWhiteSpace(Str(info, key)), key + " is empty");
            }

            // The uploader sends these to Steam as hidden item metadata / tags (RainWorldSteamManager.UploadWorkshopMod).
            Assert.True(info.ContainsKey("requirements") && info.ContainsKey("requirements_names") && info.ContainsKey("tags"));
            Assert.NotEmpty(Assert.IsType<System.Collections.ArrayList>(info["tags"]));
        }

        [Fact]
        public void ModinfoOnlyUsesFieldsTheGameReads()
        {
            // ModManager.LoadModFromJson ignores anything else, so an unknown key is a typo that silently does nothing.
            string[] known =
            {
                "id", "name", "version", "hide_version", "target_game_version", "authors", "description",
                "youtube_trailer_id", "requirements", "requirements_names", "tags", "priorities", "checksum_override_version",
            };
            Assert.Empty(LoadModinfo().Keys.Except(known));
        }

        [Fact]
        public void ModinfoListsAreListsOfStringsAndRequirementsLineUp()
        {
            Dictionary<string, object> info = LoadModinfo();
            foreach (string key in new[] { "requirements", "requirements_names", "tags", "priorities" })
            {
                if (!info.ContainsKey(key)) continue;
                var list = Assert.IsType<System.Collections.ArrayList>(info[key]);
                Assert.All(list.Cast<object>(), item => Assert.IsType<string>(item));
            }

            // Remix shows requirements_names beside the ids, so they must pair up.
            Assert.Equal(((System.Collections.ArrayList)info["requirements"]).Count, ((System.Collections.ArrayList)info["requirements_names"]).Count);
        }

        [Fact]
        public void ModinfoBooleanFlagsAreBooleans()
        {
            // The game casts these straight to bool, so "false" (a string) would throw when Remix loads the mod.
            Dictionary<string, object> info = LoadModinfo();
            foreach (string key in new[] { "hide_version", "checksum_override_version" })
            {
                if (info.ContainsKey(key)) Assert.IsType<bool>(info[key]);
            }
        }

        [Fact]
        public void ModinfoIdAndVersionAgreeWithThePlugin()
        {
            // README: "mod/modinfo.json, the [BepInPlugin] version in Plugin.cs and the tag should agree."
            Dictionary<string, object> info = LoadModinfo();
            string plugin = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Plugin.cs"));
            Match id = Regex.Match(plugin, "MOD_ID\\s*=\\s*\"([^\"]+)\"");
            Match version = Regex.Match(plugin, "\\[BepInPlugin\\([^,]+,[^,]+,\\s*\"([^\"]+)\"\\)\\]");
            Assert.True(id.Success && version.Success, "couldn't find MOD_ID / [BepInPlugin] version in src/Plugin.cs");
            Assert.Equal(id.Groups[1].Value, Str(info, "id"));
            Assert.Equal(version.Groups[1].Value, Str(info, "version"));
            Assert.Matches("^\\d+\\.\\d+\\.\\d+$", Str(info, "version"));
        }

        [Fact]
        public void ModinfoDescriptionFitsInASteamDescription()
        {
            // Steam's description limit is 8000 bytes, and the uploader copies this text there.
            Assert.True(Encoding.UTF8.GetByteCount(Str(LoadModinfo(), "description")) < 8000);
        }

        // ---- thumbnail.png ----

        private static byte[] ThumbnailBytes()
        {
            string path = Path.Combine(ModFolder(), "thumbnail.png");
            Assert.True(File.Exists(path), "mod/thumbnail.png is missing");
            return File.ReadAllBytes(path);
        }

        [Fact]
        public void ThumbnailIsAPng()
        {
            byte[] png = ThumbnailBytes();
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            Assert.True(png.Length > 24);
            Assert.Equal(signature, png.Take(8).ToArray());
        }

        [Fact]
        public void ThumbnailIsUnderTheUploadersSizeLimit()
        {
            // RainWorldSteamManager.ValidateWorkshopModForProblems: Length >= 1000000 -> "must be less than 1 MB in size".
            Assert.True(ThumbnailBytes().Length < 1000000);
        }

        [Fact]
        public void ThumbnailIsSixteenByNine()
        {
            // Same method: height/width outside 0.5616 - 0.5634 -> "should have a 16:9 aspect ratio".
            // Width and height are the first two big-endian ints of the IHDR chunk, right after the 8-byte signature.
            byte[] png = ThumbnailBytes();
            int width = ReadBigEndian(png, 16);
            int height = ReadBigEndian(png, 20);
            Assert.True(width > 0 && height > 0);
            double ratio = (double)height / width;
            Assert.True(ratio >= 0.5616 && ratio <= 0.5634, "thumbnail is " + width + "x" + height);
        }

        private static int ReadBigEndian(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        // ---- what ships ----

        [Fact]
        public void ModFolderShipsOnlyWhatItShould()
        {
            // The uploader sends the whole installed mod folder (SteamUGC.SetItemContent(mod.path)), and deploy.ps1
            // copies everything in mod/ there, so anything that lands in mod/ is published.
            string mod = ModFolder();
            string[] allowedTopLevel = { "modinfo.json", "thumbnail.png", "soundboard.yaml", "plugins", "sounds" };
            string[] topLevel = Directory.GetFileSystemEntries(mod).Select(Path.GetFileName).ToArray();
            Assert.Empty(topLevel.Except(allowedTopLevel));
            foreach (string required in new[] { "modinfo.json", "thumbnail.png", "soundboard.yaml" })
            {
                Assert.Contains(required, topLevel);
            }

            string[] plugins = Directory.GetFileSystemEntries(Path.Combine(mod, "plugins")).Select(Path.GetFileName).ToArray();
            Assert.All(plugins, f => Assert.True(f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase), f + " doesn't belong in plugins/"));
            Assert.Contains("SoundboardMod.dll", plugins);

            string[] audio = { ".wav", ".ogg", ".mp3" };
            string[] sounds = Directory.GetFileSystemEntries(Path.Combine(mod, "sounds"));
            Assert.NotEmpty(sounds);
            Assert.All(sounds, f => Assert.True(audio.Contains(Path.GetExtension(f).ToLowerInvariant()), f + " isn't a .wav/.ogg/.mp3"));
        }

        [Fact]
        public void ModFolderIsNotAbsurdlyLarge()
        {
            // Not a Steam rule (the game only checks the thumbnail; Workshop items here run to ~1 GB) - a tripwire so a
            // huge file doesn't get committed and published by accident.
            long total = new DirectoryInfo(ModFolder()).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            Assert.True(total < 300L * 1024 * 1024, "mod/ is " + total / (1024 * 1024) + " MB");
        }

        [Fact]
        public void WorkshopHelperFilesStayOutsideTheModFolder()
        {
            Assert.False(File.Exists(Path.Combine(ModFolder(), "description.bbcode.txt")));
            Assert.False(File.Exists(Path.Combine(ModFolder(), "PUBLISHING.md")));
            // The game's uploader neither writes nor reads a workshopid.txt, so there's nothing to ship or commit.
            Assert.False(File.Exists(Path.Combine(ModFolder(), "workshopid.txt")));
        }

        // ---- workshop/description.bbcode.txt ----

        private static string Bbcode()
        {
            string path = Path.Combine(RepoRoot(), "workshop", "description.bbcode.txt");
            Assert.True(File.Exists(path), "workshop/description.bbcode.txt is missing");
            return File.ReadAllText(path);
        }

        [Fact]
        public void WorkshopDescriptionFitsInASteamDescription()
        {
            string text = Bbcode();
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.True(Encoding.UTF8.GetByteCount(text) < 8000, "Steam allows 8000 bytes; this is " + Encoding.UTF8.GetByteCount(text));
        }

        [Fact]
        public void WorkshopDescriptionBbcodeTagsAreBalanced()
        {
            string text = Bbcode();
            foreach (string tag in new[] { "h1", "h2", "h3", "b", "i", "u", "list", "olist", "code", "url", "quote" })
            {
                int opens = Regex.Matches(text, "\\[" + tag + "(=[^\\]]*)?\\]").Count;
                int closes = Regex.Matches(text, "\\[/" + tag + "\\]").Count;
                Assert.True(opens == closes, "[" + tag + "] opens " + opens + " times but closes " + closes);
            }
        }

        [Fact]
        public void WorkshopDescriptionCarriesTheRequiredNotes()
        {
            string text = Bbcode();
            Assert.Contains("Remix", text);
            Assert.Contains("Custom Soundboard", text);
            Assert.Contains("AI-assisted", text);
            Assert.Contains("github.com/dion-hagan/rainworld-soundboard", text);
        }
    }
}
