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
    /// problems found in soundboard.yaml, and one checkbox per sound entry so
    /// individual sounds can be switched off without editing the file.
    /// </summary>
    public class Options : OptionInterface
    {
        private const int MaxProblemsShown = 4;
        private const int MaxProblemLength = 170;

        private static readonly ManualLogSource Log = BepInEx.Logging.Logger.CreateLogSource("SoundboardMod");

        public static Options Instance { get; private set; }

        private readonly Dictionary<string, Configurable<bool>> enabledConfigs = new Dictionary<string, Configurable<bool>>();
        private readonly List<OpCheckBox> checkBoxes = new List<OpCheckBox>();

        private OpLabel statusLabel;
        private OpLabelLong problemsLabel;

        public Options()
        {
            Instance = this;
            BindChoices(SoundboardRuntime.Config);
        }

        /// <summary>
        /// Makes sure every sound entry in the config has its saved on/off
        /// setting. Called when the config is (re)loaded; entries that are no
        /// longer in the file keep their saved value in case they come back.
        /// </summary>
        public void BindChoices(SoundboardConfig soundboardConfig)
        {
            foreach (SoundChoice choice in soundboardConfig.Events.SelectMany(e => e.Choices))
            {
                if (enabledConfigs.ContainsKey(choice.Id))
                {
                    continue;
                }

                try
                {
                    enabledConfigs[choice.Id] = config.Bind(choice.Id, choice.DefaultEnabled, new ConfigurableInfo(Describe(soundboardConfig, choice)));
                }
                catch (Exception e)
                {
                    // Not worth losing the rest of the list over: the sound just can't be toggled.
                    Log.LogWarning($"Couldn't create an on/off setting for '{choice.Label}' ({choice.Id}): {e.Message}");
                }
            }
        }

        public bool IsEnabled(string choiceId, bool fallback)
        {
            return enabledConfigs.TryGetValue(choiceId, out Configurable<bool> setting) ? setting.Value : fallback;
        }

        public override void Initialize()
        {
            base.Initialize();

            SoundboardConfig soundboard = SoundboardRuntime.Config;
            OpTab tab = new OpTab(this, "Sounds");
            Tabs = new OpTab[] { tab };

            var enableAllButton = new OpSimpleButton(new Vector2(20f, 505f), new Vector2(105f, 30f), "ENABLE ALL")
            {
                description = "Switch every sound below on.",
            };
            enableAllButton.OnClick += _ => SetAll(true);

            var disableAllButton = new OpSimpleButton(new Vector2(133f, 505f), new Vector2(105f, 30f), "DISABLE ALL")
            {
                description = "Switch every sound below off.",
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
                new OpLabel(20f, 538f, "Edit soundboard.yaml (OPEN FOLDER) to change what plays, then RELOAD CONFIG.", false),
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
            checkBoxes.Clear();
            float y = ContentHeight(soundboard) - 30f;
            foreach (EventBinding binding in soundboard.Events)
            {
                items.Add(new OpLabel(20f, y, binding.EventName, true));
                y -= 32f;

                foreach (SoundChoice choice in binding.Choices)
                {
                    if (enabledConfigs.TryGetValue(choice.Id, out Configurable<bool> setting))
                    {
                        var checkBox = new OpCheckBox(setting, new Vector2(30f, y))
                        {
                            description = Describe(soundboard, choice),
                        };
                        checkBoxes.Add(checkBox);
                        items.Add(checkBox);
                    }

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
                statusLabel.text = message + " Reopen this screen to refresh the list.";
                RefreshProblems();
            }
            catch (Exception e)
            {
                Log.LogError($"Reloading the config failed: {e}");
                statusLabel.text = "Reload failed unexpectedly: " + e.Message;
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

        private void SetAll(bool value)
        {
            foreach (OpCheckBox checkBox in checkBoxes)
            {
                checkBox.value = value ? "true" : "false";
            }
        }
    }
}
