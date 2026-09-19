using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace SoundboardMod
{
    /// <summary>
    /// Builds the mod's in-game options screen (Options -> Mods -> Custom Soundboard):
    /// buttons to reload the config and open its folder, a list of any
    /// problems found in soundboard.yaml, and one checkbox per sound entry.
    ///
    /// soundboard.yaml is the only place on/off state lives. The checkboxes
    /// are plain screen widgets (not saved by Remix's own config system):
    /// they start from the file's "enabled:" values, and ticking one writes
    /// the change back into the file. So the file and the screen can't
    /// disagree, and RELOAD CONFIG refreshes the boxes from the file.
    /// </summary>
    public class Options : OptionInterface
    {
        private const int MaxProblemsShown = 4;
        private const int MaxProblemLength = 170;

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        public static Options Instance { get; private set; }

        private readonly Dictionary<string, OpCheckBox> checkBoxesById = new Dictionary<string, OpCheckBox>();

        // True while the code (not the player) is changing checkboxes, so that
        // doesn't get written back to the file as if it were a click.
        private bool updatingFromCode;

        private OpLabel statusLabel;
        private OpLabelLong problemsLabel;

        public Options()
        {
            Instance = this;
        }

        public override void Initialize()
        {
            base.Initialize();

            SoundboardConfig soundboard = SoundboardRuntime.Config;
            OpTab tab = new OpTab(this, "Sounds");
            Tabs = new OpTab[] { tab };

            var enableAllButton = new OpSimpleButton(new Vector2(20f, 505f), new Vector2(105f, 30f), "ENABLE ALL")
            {
                description = "Switch every sound below on (saved into soundboard.yaml).",
            };
            enableAllButton.OnClick += _ => SetAll(true);

            var disableAllButton = new OpSimpleButton(new Vector2(133f, 505f), new Vector2(105f, 30f), "DISABLE ALL")
            {
                description = "Switch every sound below off (saved into soundboard.yaml).",
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
                new OpLabel(20f, 538f, "Ticking a box saves it into soundboard.yaml. You can also edit that file (OPEN FOLDER), then RELOAD CONFIG.", false),
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
            float y = ContentHeight(soundboard) - 30f;
            foreach (EventBinding binding in soundboard.Events)
            {
                items.Add(new OpLabel(20f, y, binding.EventName, true));
                y -= 32f;

                foreach (SoundChoice choice in binding.Choices)
                {
                    // Not bound to Remix's saved settings: the file is the only place the state lives.
                    var setting = new Configurable<bool>(choice.Enabled, new ConfigurableInfo(Describe(soundboard, choice)));
                    var checkBox = new OpCheckBox(setting, new Vector2(30f, y))
                    {
                        description = Describe(soundboard, choice),
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
        /// Makes the checkboxes match the config again - after RELOAD CONFIG,
        /// where the file may have been edited by hand. Entries that are new
        /// since this screen was opened appear the next time it's opened.
        /// </summary>
        public void RefreshToggles()
        {
            updatingFromCode = true;
            try
            {
                foreach (SoundChoice choice in SoundboardRuntime.Config.Events.SelectMany(e => e.Choices))
                {
                    if (checkBoxesById.TryGetValue(choice.Id, out OpCheckBox box))
                    {
                        SetBox(box, choice.Enabled);
                    }
                }
            }
            catch (Exception e)
            {
                // The screen was closed and its widgets are gone; it'll be rebuilt from the config when reopened.
                Log.LogDebug($"Couldn't refresh the checkboxes: {e.Message}");
            }
            finally
            {
                updatingFromCode = false;
            }
        }

        private static void SetBox(OpCheckBox box, bool on)
        {
            box.value = on ? "true" : "false";
        }

        private void OnToggled(string choiceId, bool on)
        {
            if (updatingFromCode)
            {
                return;
            }

            string problem = SoundboardRuntime.SetEnabled(choiceId, on);
            ShowStatus(problem ?? "Saved to soundboard.yaml.");
        }

        private void SetAll(bool on)
        {
            updatingFromCode = true;
            try
            {
                foreach (OpCheckBox box in checkBoxesById.Values)
                {
                    SetBox(box, on);
                }
            }
            finally
            {
                updatingFromCode = false;
            }

            string problem = SoundboardRuntime.SetEnabled(checkBoxesById.Keys.Select(id => new KeyValuePair<string, bool>(id, on)));
            ShowStatus(problem ?? (on ? "Every sound switched on and saved to soundboard.yaml." : "Every sound switched off and saved to soundboard.yaml."));
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

        private void Reload()
        {
            try
            {
                string message = SoundboardRuntime.Reload();
                ShowStatus(message + " Reopen this screen to see new entries.");
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
