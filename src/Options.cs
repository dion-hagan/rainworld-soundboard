using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using HarmonyLib;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace SoundboardMod
{
    /// <summary>
    /// Builds the mod's in-game options screen (Options -> Mods -> Custom Soundboard):
    /// buttons to reload the config and open its folder, a list of any
    /// problems found in soundboard.yaml, and one checkbox per sound entry.
    ///
    /// soundboard.yaml is the only place on/off state really lives. The
    /// checkboxes are a normal Remix editing screen over it: they start from the
    /// file's "enabled:" values every time the screen opens, ticking one marks a
    /// pending change (so SAVE, REVERT and the "unsaved changes" prompt all work
    /// like on any other mod), and SAVE writes the changes into the file. The
    /// values Remix itself remembers between launches are never trusted - the
    /// file always wins - so the two can't drift apart.
    /// </summary>
    public class Options : OptionInterface
    {
        private const int MaxProblemsShown = 4;
        private const int MaxProblemLength = 170;

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        public static Options Instance { get; private set; }

        // Remix-tracked settings, one per entry. Created once (Remix doesn't allow the same key twice).
        private readonly Dictionary<string, Configurable<bool>> settings = new Dictionary<string, Configurable<bool>>();

        // What's on the screen right now.
        private readonly Dictionary<string, OpCheckBox> checkBoxesById = new Dictionary<string, OpCheckBox>();

        // Entries whose checkbox waits for SAVE. Any entry that couldn't get a Remix setting
        // is missing from here and saves the moment it's ticked instead.
        private readonly HashSet<string> stagedIds = new HashSet<string>();

        private OpLabel statusLabel;
        private OpLabelLong problemsLabel;

        public Options()
        {
            Instance = this;

            foreach (SoundChoice choice in SoundboardRuntime.Config.Events.SelectMany(e => e.Choices))
            {
                EnsureSetting(choice);
            }

            // Remix builds this screen once per launch, but each time the page is opened it reloads
            // its own saved copy of the settings into the checkboxes. That copy can be stale (or
            // from an older version of this mod), so on every open the boxes are re-seeded from
            // soundboard.yaml, which is the only thing that counts.
            OnActivate += RefreshToggles;
        }

        private Configurable<bool> EnsureSetting(SoundChoice choice)
        {
            if (settings.TryGetValue(choice.Id, out Configurable<bool> existing))
            {
                return existing;
            }

            try
            {
                // Default true: what RESET to defaults should mean. The real value is set from the file on every open.
                Configurable<bool> created = config.Bind(choice.Id, true, new ConfigurableInfo(string.Empty));
                settings[choice.Id] = created;
                return created;
            }
            catch (Exception e)
            {
                Log.LogWarning($"Couldn't create a Remix setting for '{choice.Label}' ({choice.Id}); its checkbox will save instantly instead: {e.Message}");
                return null;
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            SoundboardConfig soundboard = SoundboardRuntime.Config;
            OpTab tab = new OpTab(this, "Sounds");
            Tabs = new OpTab[] { tab };

            var enableAllButton = new OpSimpleButton(new Vector2(20f, 505f), new Vector2(105f, 30f), "ENABLE ALL")
            {
                description = "Tick every sound below. Press SAVE to write it into soundboard.yaml.",
            };
            enableAllButton.OnClick += _ => SetAll(true);

            var disableAllButton = new OpSimpleButton(new Vector2(133f, 505f), new Vector2(105f, 30f), "DISABLE ALL")
            {
                description = "Untick every sound below. Press SAVE to write it into soundboard.yaml.",
            };
            disableAllButton.OnClick += _ => SetAll(false);

            var reloadButton = new OpSimpleButton(new Vector2(246f, 505f), new Vector2(150f, 30f), "RELOAD CONFIG")
            {
                description = "Re-read soundboard.yaml and any sound files you changed, without restarting the game.",
            };
            reloadButton.OnClick += _ => Reload();

            var openFolderButton = new OpSimpleButton(new Vector2(404f, 505f), new Vector2(150f, 30f), "OPEN FOLDER")
            {
                description = "Open the folder holding soundboard.yaml and your sounds.",
            };
            openFolderButton.OnClick += _ => OpenFolder();

            statusLabel = new OpLabel(20f, 480f, StatusText(), false);

            problemsLabel = new OpLabelLong(new Vector2(20f, 350f), new Vector2(560f, 125f), string.Empty)
            {
                allowOverflow = false,
            };

            float listTop = 340f;
            var scrollBox = new OpScrollBox(new Vector2(0f, 0f), new Vector2(600f, listTop), ContentHeight(soundboard));

            tab.AddItems(
                new OpLabel(20f, 560f, "Custom Soundboard", true),
                new OpLabel(20f, 538f, "Tick or untick sounds, then press SAVE to write them into soundboard.yaml. Or edit that file (OPEN FOLDER) and RELOAD CONFIG.", false),
                enableAllButton,
                disableAllButton,
                reloadButton,
                openFolderButton,
                statusLabel,
                problemsLabel,
                scrollBox);

            RefreshProblems();

            // Items go into the scroll box only after it's been added to the tab.
            // Inside it, y=0 is the bottom of the content, so start at the top.
            var items = new List<UIelement>();
            checkBoxesById.Clear();
            stagedIds.Clear();
            float y = ContentHeight(soundboard) - 30f;
            foreach (EventBinding binding in soundboard.Events)
            {
                items.Add(new OpLabel(20f, y, binding.EventName, true));
                y -= 32f;

                foreach (SoundChoice choice in binding.Choices)
                {
                    string description = Describe(soundboard, choice);
                    Configurable<bool> setting = EnsureSetting(choice);
                    if (setting != null)
                    {
                        // Start from the file, whatever Remix remembered from last time.
                        setting.Value = choice.Enabled;
                        stagedIds.Add(choice.Id);
                    }
                    else
                    {
                        // Not tracked by Remix (its own setting couldn't be created): saves instantly.
                        setting = new Configurable<bool>(choice.Enabled, new ConfigurableInfo(description));
                    }

                    var checkBox = new OpCheckBox(setting, new Vector2(30f, y))
                    {
                        description = description,
                    };

                    SetBox(checkBox, choice.Enabled);
                    string id = choice.Id;
                    checkBox.OnValueChanged += (box, value, oldValue) => OnToggled(id, value == "true");

                    checkBoxesById[choice.Id] = checkBox;
                    items.Add(checkBox);
                    items.Add(new OpLabel(70f, y + 4f, choice.Label, false));
                    y -= 30f;
                }

                y -= 6f;
            }

            if (soundboard.Events.Count == 0)
            {
                items.Add(new OpLabel(20f, y, "No sounds are set up yet - open soundboard.yaml and add some (there are examples at the top).", false));
            }

            scrollBox.AddItems(items.ToArray());
        }

        /// <summary>
        /// Makes the checkboxes (and Remix's copy of their values) match the config:
        /// every time the page is opened, and after RELOAD CONFIG, where the file may
        /// have been edited by hand. Nothing here counts as a pending change. Entries
        /// that are new since the screen was built appear after the game is restarted.
        /// </summary>
        public void RefreshToggles()
        {
            try
            {
                foreach (SoundChoice choice in SoundboardRuntime.Config.Events.SelectMany(e => e.Choices))
                {
                    if (settings.TryGetValue(choice.Id, out Configurable<bool> setting))
                    {
                        setting.Value = choice.Enabled;
                    }

                    if (checkBoxesById.TryGetValue(choice.Id, out OpCheckBox box))
                    {
                        // ForceValue changes what's shown without recording an unsaved change.
                        box.ForceValue(choice.Enabled ? "true" : "false");
                    }
                }
            }
            catch (Exception e)
            {
                // The screen was closed and its widgets are gone; it'll be rebuilt from the config when reopened.
                Log.LogDebug($"Couldn't refresh the checkboxes: {e.Message}");
            }
        }

        private static void SetBox(OpCheckBox box, bool on)
        {
            box.value = on ? "true" : "false";
        }

        private void OnToggled(string choiceId, bool on)
        {
            if (stagedIds.Contains(choiceId))
            {
                ShowStatus("Changed - press SAVE to write it into soundboard.yaml.");
                return;
            }

            string problem = SoundboardRuntime.SetEnabled(choiceId, on);
            ShowStatus(problem ?? "Saved to soundboard.yaml.");
        }

        /// <summary>
        /// The player pressed SAVE: whatever the checkboxes show that differs from
        /// the file gets written into it. Works from what's on screen versus the
        /// file (not from a list of clicks), so a reverted or repeated click can
        /// never write the wrong thing.
        /// </summary>
        private void CommitPending()
        {
            if (checkBoxesById.Count == 0)
            {
                return; // no screen: the game is just loading its saved settings
            }

            var changes = new List<KeyValuePair<string, bool>>();
            foreach (SoundChoice choice in SoundboardRuntime.Config.Events.SelectMany(e => e.Choices))
            {
                if (stagedIds.Contains(choice.Id) && checkBoxesById.TryGetValue(choice.Id, out OpCheckBox box))
                {
                    bool shown = box.value == "true";
                    if (shown != choice.Enabled)
                    {
                        changes.Add(new KeyValuePair<string, bool>(choice.Id, shown));
                    }
                }
            }

            if (changes.Count == 0)
            {
                return;
            }

            string problem = SoundboardRuntime.SetEnabled(changes);
            ShowStatus(problem ?? "Saved " + changes.Count + " change(s) to soundboard.yaml.");
        }

        private void SetAll(bool on)
        {
            foreach (OpCheckBox box in checkBoxesById.Values)
            {
                SetBox(box, on);
            }

            if (checkBoxesById.Count > 0 && stagedIds.Count > 0)
            {
                ShowStatus((on ? "Everything ticked" : "Everything unticked") + " - press SAVE to write it into soundboard.yaml.");
            }
        }

        private void ShowStatus(string text)
        {
            if (statusLabel != null)
            {
                statusLabel.text = text;
            }
        }

        private static float ContentHeight(SoundboardConfig soundboard)
        {
            float needed = soundboard.Events.Sum(e => 38f + e.Choices.Count * 30f) + 40f;
            return Mathf.Max(340f, needed);
        }

        private static string Describe(SoundboardConfig soundboard, SoundChoice choice)
        {
            string files = string.Join(" + ", choice.Sounds.Select(s =>
                System.IO.Path.GetFileName(s.File)
                + (Math.Abs(s.Volume - 1f) > 0.001f ? " (volume " + s.Volume.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")" : string.Empty)
                + (s.Delay > 0f ? " (delay " + s.Delay.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "s)" : string.Empty)));

            string when = string.Empty;
            EventBinding binding = soundboard.Events.FirstOrDefault(e => e.Choices.Contains(choice));
            EventInfo info = binding == null ? null : SoundboardRuntime.Catalog?.All.FirstOrDefault(i => i.Name == binding.EventName);
            if (info != null)
            {
                when = info.Description + "  ";
            }

            return (choice.Description != null ? choice.Description + "  " : when) + "Plays: " + files;
        }

        private string StatusText()
        {
            string where = SoundboardRuntime.ConfigPath ?? "(none)";
            return SoundboardRuntime.Config.SoundCount + " sound(s) for " + SoundboardRuntime.Config.Events.Count + " event(s) from " + where;
        }

        private void RefreshProblems()
        {
            IReadOnlyList<ConfigIssue> issues = SoundboardRuntime.Issues;
            if (issues.Count == 0)
            {
                problemsLabel.text = string.Empty;
                return;
            }

            bool anyErrors = issues.Any(i => i.Severity == IssueSeverity.Error);
            problemsLabel.color = anyErrors ? new Color(1f, 0.45f, 0.4f) : new Color(1f, 0.85f, 0.4f);

            // Errors first, then warnings; only a few, since the space is small (the log has all of them).
            var shown = issues.OrderBy(i => i.Severity == IssueSeverity.Error ? 0 : 1).Take(MaxProblemsShown).ToList();
            var lines = shown.Select(i => (i.Severity == IssueSeverity.Error ? "Problem - " : "Note - ") + Shorten(i.ToString()));
            string more = issues.Count > shown.Count ? "\n...and " + (issues.Count - shown.Count) + " more (see BepInEx/LogOutput.log)" : string.Empty;
            problemsLabel.text = string.Join("\n", lines) + more;
        }

        private static string Shorten(string text)
        {
            return text.Length <= MaxProblemLength ? text : text.Substring(0, MaxProblemLength - 3) + "...";
        }

        /// <summary>
        /// Runs right after Remix saves this mod's settings (the SAVE button, or another
        /// mod asking for a save). Deliberately not OnConfigChanged: Remix also fires that
        /// every time it reloads its saved settings when the page is opened, which is not a
        /// save and must never write to the file.
        /// </summary>
        [HarmonyPatch(typeof(OptionInterface), "_SaveConfigFile")]
        private static class OptionInterface_SaveConfigFile_Patch
        {
            [HarmonyPostfix]
            private static void Postfix(OptionInterface __instance)
            {
                if (__instance is Options options)
                {
                    try
                    {
                        options.CommitPending();
                    }
                    catch (Exception e)
                    {
                        Log.LogError($"Saving the checkbox changes to soundboard.yaml failed: {e}");
                        options.ShowStatus("Couldn't save to soundboard.yaml: " + e.Message);
                    }
                }
            }
        }

        private void Reload()
        {
            try
            {
                string message = SoundboardRuntime.Reload();
                ShowStatus(message + " New entries show up after a game restart.");
                RefreshProblems();
            }
            catch (Exception e)
            {
                Log.LogError($"Reloading the config failed: {e}");
                ShowStatus("Reload failed unexpectedly: " + e.Message);
            }
        }

        private static void OpenFolder()
        {
            string folder = SoundboardRuntime.Locations?.UserFolder;
            try
            {
                if (folder != null && System.IO.Directory.Exists(folder))
                {
                    Application.OpenURL("file:///" + folder.Replace('\\', '/'));
                    return;
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Couldn't open {folder}: {e.Message}");
            }

            Log.LogInfo($"Config folder: {folder}");
        }
    }
}
