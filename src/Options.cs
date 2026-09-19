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
    /// Builds the mod's in-game options screen (Options -> Mods -> Custom Soundboard).
    /// It has three tabs:
    ///
    /// "Sounds": buttons to reload the config and open its folder, a list of any
    /// problems found in soundboard.yaml, and one checkbox per sound entry.
    ///
    /// "Add Sound": dropdowns for an event and up to three sound files (each with number
    /// boxes for volume and delay) and a "play together" checkbox; SAVE adds that sound,
    /// or group of sounds, to the event's list in soundboard.yaml.
    ///
    /// "Edit Sound": pick an entry that's already in the file and change the volume and delay
    /// of its sounds and its cooldown; SAVE writes just those numbers back in place.
    ///
    /// soundboard.yaml is the only place any of this really lives. Both tabs are a
    /// normal Remix editing screen over it: they start from the file every time
    /// the screen opens, changing anything marks a pending change (so SAVE, REVERT
    /// and the "unsaved changes" prompt all work like on any other mod), and SAVE
    /// writes the changes into the file. The values Remix itself remembers between
    /// launches are never trusted - the file always wins - so the two can't drift apart.
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

            CreatePickerSettings();
            CreateEditSettings();

            // Each time the page is opened Remix reloads its own saved copy of the settings into
            // the widgets. That copy can be stale (or from an older version of this mod), so on
            // every open the boxes are re-seeded from soundboard.yaml, which is the only thing that
            // counts - and the Add Sound form from what this mod last knew about it.
            OnActivate += RefreshToggles;
            OnActivate += SeedPicker;
            OnActivate += SeedEditor;

            // The widgets are gone once the menu is left; anything that runs before the next
            // Initialize must not touch them.
            OnUnload += ForgetPicker;
            OnUnload += ForgetEditor;
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
            Tabs = new OpTab[] { tab, BuildAddSoundTab(), BuildEditTab() };

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
                new OpLabel(20f, 538f, "Tick or untick sounds, then press SAVE. Add sounds on the Add Sound tab and change their numbers on Edit Sound - or edit soundboard.yaml (OPEN FOLDER) and RELOAD CONFIG.", false),
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
        private string CommitPending()
        {
            if (checkBoxesById.Count == 0)
            {
                return null; // no screen: the game is just loading its saved settings
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
                return null;
            }

            string problem = SoundboardRuntime.SetEnabled(changes);
            return problem ?? "Saved " + changes.Count + " change(s) to soundboard.yaml.";
        }

        /// <summary>
        /// Everything SAVE does, in order: first the checkbox changes, then the sound
        /// from the Add Sound form. That order matters: adding a sound re-reads the
        /// whole file (so it plays straight away), which would otherwise throw away
        /// checkbox changes that hadn't been written yet.
        /// </summary>
        private void SaveScreen()
        {
            string ticked = null;
            try
            {
                ticked = CommitPending();
            }
            catch (Exception e)
            {
                Log.LogError($"Saving the checkbox changes to soundboard.yaml failed: {e}");
                ticked = "Couldn't save to soundboard.yaml: " + e.Message;
            }

            // Editing before adding: both re-read the file, and an edit is checked against the config as the screen showed it.
            string edited = null;
            try
            {
                edited = CommitEdit();
            }
            catch (Exception e)
            {
                Log.LogError($"Changing the sound in soundboard.yaml failed: {e}");
                edited = "Couldn't change the sound: " + e.Message;
                ShowEditStatus(edited, true);
            }

            string added = null;
            try
            {
                added = CommitPicker();
            }
            catch (Exception e)
            {
                Log.LogError($"Adding the sound to soundboard.yaml failed: {e}");
                added = "Couldn't add the sound: " + e.Message;
                ShowPickerStatus(added, true);
            }

            string message = string.Join(" ", new[] { ticked, edited, added }.Where(m => !string.IsNullOrEmpty(m)));
            if (message.Length > 0)
            {
                ShowStatus(message);
            }
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

            string cooldown = choice.Cooldown > 0f ? "  (cooldown " + choice.Cooldown.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "s)" : string.Empty;
            return (choice.Description != null ? choice.Description + "  " : when) + "Plays: " + files + cooldown;
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
                        options.SaveScreen();
                    }
                    catch (Exception e)
                    {
                        Log.LogError($"Saving to soundboard.yaml failed: {e}");
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
                ShowStatus(message + " New entries show up in the list the next time you open the Mods menu.");
                RefreshProblems();
                RefreshSoundList();
                RefreshEditor();
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

        // ===================================================================================
        //  Add Sound tab
        // ===================================================================================

        private const string PickEventKey = "AddSound_Event";
        private const string PickTogetherKey = "AddSound_Together";
        private const string PickCooldownKey = "AddSound_Cooldown";

        // Sound row n (1-based) uses the keys AddSound_Sound<n>, AddSound_Volume<n> and AddSound_Delay<n>.
        // The rows are built up front and always shown - the widgets are never created or removed while
        // the screen is open - so a group can hold at most this many sounds: the first plus two more.
        private const int SoundRowCount = 3;

        private const int DefaultVolumePercent = 100;
        private const int MaxVolumePercent = (int)(NewSound.MaxVolume * 100f);

        private static readonly Color PickerErrorColor = new Color(1f, 0.45f, 0.4f);

        // The form's Remix settings. Created once, like the checkbox ones (Remix doesn't allow a key twice).
        private Configurable<string> pickEvent;
        private Configurable<bool> pickTogether;
        private Configurable<float> pickCooldown;

        private readonly SoundRow[] rows = new SoundRow[SoundRowCount];

        // What's on the page right now. All null while the menu is closed, or if the page couldn't be built.
        private OpComboBox eventBox;
        private OpCheckBox togetherBox;
        private PickerUpdown cooldownBox;
        private OpLabelLong eventInfoLabel;
        private OpLabelLong soundInfoLabel;
        private OpLabelLong pickStatusLabel;
        private Color pickStatusColor;

        // A failed build leaves the form's settings bound to widgets Remix never got to unload,
        // so they can't be used again: the page then just explains that it isn't available.
        private bool pickerBroken;

        // What the form shows whenever the page is opened: blank, or - after an add that
        // failed - the choices that weren't added, so a typo in the file doesn't cost the player their picks.
        private PickerForm pickerState = new PickerForm();

        /// <summary>One sound row: its Remix settings (kept for good) and its widgets (rebuilt with the page).</summary>
        private sealed class SoundRow
        {
            public Configurable<string> Sound;
            public Configurable<int> Volume;
            public Configurable<float> Delay;

            public OpComboBox Box;
            public PickerUpdown VolumeBox;
            public PickerUpdown DelayBox;

            public void ForgetWidgets()
            {
                Box = null;
                VolumeBox = null;
                DelayBox = null;
            }
        }

        private sealed class RowForm
        {
            public string Sound = string.Empty;
            public int VolumePercent = DefaultVolumePercent;
            public float Delay;
        }

        private sealed class PickerForm
        {
            public string Event = string.Empty;
            public bool Together;
            public float Cooldown;
            public readonly RowForm[] Rows = Enumerable.Range(0, SoundRowCount).Select(_ => new RowForm()).ToArray();
        }

        /// <summary>
        /// A number box whose shown number can be re-drawn after ForceValue. ForceValue changes
        /// the value without telling the box, which is what's wanted when re-seeding the form
        /// (it mustn't count as an unsaved change) - but a number box only rewrites its digits
        /// when told its value changed.
        /// </summary>
        private sealed class PickerUpdown : OpUpdown
        {
            public PickerUpdown(Configurable<int> setting, Vector2 pos, float width)
                : base(setting, pos, width)
            {
            }

            public PickerUpdown(Configurable<float> setting, Vector2 pos, float width, byte decimals)
                : base(setting, pos, width, decimals)
            {
            }

            public void Redraw()
            {
                Change();
            }
        }

        private void CreatePickerSettings()
        {
            try
            {
                pickEvent = config.Bind(PickEventKey, string.Empty, new ConfigurableInfo("The event the new sound plays for."));
                pickTogether = config.Bind(PickTogetherKey, false, new ConfigurableInfo("Play the chosen sounds all at once, as a single entry in the event's list."));
                pickCooldown = config.Bind(PickCooldownKey, 0f, new ConfigAcceptableRange<float>(0f, NewSound.MaxCooldown));

                for (int i = 0; i < SoundRowCount; i++)
                {
                    string n = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    rows[i] = new SoundRow
                    {
                        Sound = config.Bind("AddSound_Sound" + n, string.Empty, new ConfigurableInfo("An audio file the new sound plays.")),
                        Volume = config.Bind("AddSound_Volume" + n, DefaultVolumePercent, new ConfigAcceptableRange<int>(0, MaxVolumePercent)),
                        Delay = config.Bind("AddSound_Delay" + n, 0f, new ConfigAcceptableRange<float>(0f, NewSound.MaxDelay)),
                    };
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Couldn't create the settings for the Add Sound page, so it won't be available: {e.Message}");
                pickerBroken = true;
            }
        }

        private OpTab BuildAddSoundTab()
        {
            var page = new OpTab(this, "Add Sound");
            ForgetPicker();

            if (!pickerBroken)
            {
                try
                {
                    page.AddItems(AddSoundItems());
                    return page;
                }
                catch (Exception e)
                {
                    Log.LogError($"Couldn't build the Add Sound page: {e}");
                    pickerBroken = true;
                    ForgetPicker();
                }
            }

            page.AddItems(
                new OpLabel(20f, 560f, "Add a Sound", true),
                new OpLabelLong(new Vector2(20f, 480f), new Vector2(560f, 60f), "This page couldn't be set up (the reason is in BepInEx/LogOutput.log). You can still add sounds by editing soundboard.yaml - press OPEN FOLDER on the Sounds tab.")
                {
                    allowOverflow = false,
                });
            return page;
        }

        private UIelement[] AddSoundItems()
        {
            var events = new List<ListItem>();
            foreach (EventInfo info in SoundboardRuntime.Catalog.All)
            {
                events.Add(new ListItem(info.Name) { desc = info.Description });
            }

            List<string> files = SoundLibrary.List(SoundboardRuntime.Locations.SoundFolders);

            soundInfoLabel = new OpLabelLong(new Vector2(20f, 118f), new Vector2(560f, 40f), SoundCountText(files.Count))
            {
                allowOverflow = false,
            };

            if (files.Count == 0)
            {
                // Nothing to pick from: just explain, and build none of the form.
                return new UIelement[]
                {
                    new OpLabel(20f, 560f, "Add a Sound", true),
                    new OpLabelLong(new Vector2(20f, 480f), new Vector2(560f, 60f), SoundCountText(0)) { allowOverflow = false },
                };
            }

            eventInfoLabel = new OpLabelLong(new Vector2(170f, 384f), new Vector2(400f, 60f), string.Empty)
            {
                allowOverflow = false,
            };

            pickStatusLabel = new OpLabelLong(new Vector2(20f, 30f), new Vector2(560f, 80f), string.Empty)
            {
                allowOverflow = false,
            };
            pickStatusColor = pickStatusLabel.color;

            cooldownBox = new PickerUpdown(pickCooldown, new Vector2(170f, 176f), 100f, 1)
            {
                description = "After this sound (or group) plays, it can't play again for this many seconds - an event's other sounds still take their turn meanwhile. 0 = no limit.",
            };

            togetherBox = new OpCheckBox(pickTogether, new Vector2(20f, 350f))
            {
                description = "Play the sounds in all the rows below at the same time, as a single entry in the event's list. It ticks itself when you pick a sound in the second or third row.",
            };

            // Bottom edge of each sound row, top to bottom.
            float[] rowY = { 292f, 258f, 224f };

            var rowWidgets = new List<UIelement>();
            for (int i = 0; i < SoundRowCount; i++)
            {
                SoundRow row = rows[i];
                bool extra = i > 0;

                row.VolumeBox = new PickerUpdown(row.Volume, new Vector2(350f, rowY[i]), 100f)
                {
                    description = "How loud this sound is, as a percentage of the file's own volume. 100 = as recorded, 50 = half as loud, 200 = twice as loud.",
                };

                row.DelayBox = new PickerUpdown(row.Delay, new Vector2(460f, rowY[i]), 100f, 1)
                {
                    description = "Seconds to wait after the event before this sound plays. 0 = right away.",
                };

                row.Box = new OpComboBox(row.Sound, new Vector2(20f, rowY[i] + 3f), 320f, files.Select(f => new ListItem(f)).ToList())
                {
                    listHeight = 8,
                    description = extra
                        ? "An extra sound to play together with the first. Only used when 'Play several sounds together' is ticked (picking one ticks it). Click for the list, or start typing to search it."
                        : "The audio file to play. Click for the list, or start typing to search it.",
                };

                if (extra)
                {
                    row.Box.OnValueUpdate += (box, value, oldValue) =>
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            TickTogether();
                        }
                    };
                }

                rowWidgets.Add(row.VolumeBox);
                rowWidgets.Add(row.DelayBox);
            }

            eventBox = new OpComboBox(pickEvent, new Vector2(170f, 452f), 400f, events)
            {
                listHeight = 10,
                description = "The in-game event that makes the sound play. Click for the list (hover a name to see what it means), or start typing to search it.",
            };

            eventBox.OnValueUpdate += (box, value, oldValue) => UpdateEventInfo();

            var items = new List<UIelement>
            {
                new OpLabel(20f, 560f, "Add a Sound", true),
                new OpLabelLong(new Vector2(20f, 486f), new Vector2(560f, 64f), "Pick an event and a sound file, set how loud it is and how long to wait, then press SAVE. To play up to three sounds at once as a single entry, pick them in the rows below (\"Play several sounds together\" ticks itself).")
                {
                    allowOverflow = false,
                },
                new OpLabel(20f, 456f, "When this happens:", false),
                new OpLabel(56f, 353f, "Play several sounds together", false),
                new OpLabel(20f, 181f, "Cooldown:", false),
                new OpLabel(280f, 181f, "seconds before it can play again (0 = no limit)", false),
                new OpLabel(20f, 326f, "Sound", false),
                new OpLabel(350f, 326f, "Volume (%)", false),
                new OpLabel(460f, 326f, "Delay (seconds)", false),
                eventInfoLabel,
                soundInfoLabel,
                pickStatusLabel,
                togetherBox,
            };

            items.AddRange(rowWidgets);
            items.Add(cooldownBox);

            // The dropdowns go in last, lowest row first: a list that opens over other widgets has
            // to be drawn on top of them, and each row's list opens over the rows beneath it.
            for (int i = SoundRowCount - 1; i >= 0; i--)
            {
                items.Add(rows[i].Box);
            }

            items.Add(eventBox);
            UpdateEventInfo();
            return items.ToArray();
        }

        private static string SoundCountText(int count)
        {
            return count == 0
                ? "No sound files found. Press OPEN FOLDER on the Sounds tab, put .wav, .ogg or .mp3 files in the sounds folder, then press RELOAD CONFIG."
                : count + " sound file(s) to choose from. To add your own, press OPEN FOLDER on the Sounds tab, drop them in the sounds folder and press RELOAD CONFIG.";
        }

        private void UpdateEventInfo()
        {
            if (eventInfoLabel == null || eventBox == null)
            {
                return;
            }

            string name = eventBox.value;
            EventInfo info = string.IsNullOrEmpty(name) ? null : SoundboardRuntime.Catalog?.All.FirstOrDefault(i => i.Name == name);
            eventInfoLabel.text = info == null ? "Pick the event the sound should play for." : info.Description;
        }

        /// <summary>Picking an extra sound means the player wants a group, so tick the box for them.</summary>
        private void TickTogether()
        {
            if (togetherBox != null && togetherBox.value != "true")
            {
                togetherBox.value = "true";
            }
        }

        private void ForgetPicker()
        {
            eventBox = null;
            togetherBox = null;
            cooldownBox = null;
            eventInfoLabel = null;
            soundInfoLabel = null;
            pickStatusLabel = null;
            foreach (SoundRow row in rows)
            {
                row?.ForgetWidgets();
            }
        }

        /// <summary>The page was opened: show what the form should hold (Remix's own remembered values are ignored).</summary>
        private void SeedPicker()
        {
            ApplyForm(pickerState);
        }

        private void ApplyForm(PickerForm form)
        {
            try
            {
                // ForceValue rather than value =: this must not count as a change the player made.
                eventBox?.ForceValue(Known(eventBox, form.Event));
                togetherBox?.ForceValue(form.Together ? "true" : "false");

                if (cooldownBox != null)
                {
                    cooldownBox.ForceValue(form.Cooldown.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                    cooldownBox.Redraw();
                }

                for (int i = 0; i < SoundRowCount; i++)
                {
                    SoundRow row = rows[i];
                    RowForm values = form.Rows[i];

                    row.Box?.ForceValue(Known(row.Box, values.Sound));

                    if (row.VolumeBox != null)
                    {
                        row.VolumeBox.ForceValue(values.VolumePercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        row.VolumeBox.Redraw();
                    }

                    if (row.DelayBox != null)
                    {
                        row.DelayBox.ForceValue(values.Delay.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                        row.DelayBox.Redraw();
                    }
                }

                UpdateEventInfo();
            }
            catch (Exception e)
            {
                Log.LogDebug($"Couldn't reset the Add Sound form: {e.Message}");
            }
        }

        /// <summary>The name if it's in the box's list, otherwise "" (shown as ---): a file may have been deleted since.</summary>
        private static string Known(OpComboBox box, string name)
        {
            return !string.IsNullOrEmpty(name) && box.GetItemList().Any(item => item.name == name) ? name : string.Empty;
        }

        private PickerForm ReadPicker()
        {
            var form = new PickerForm
            {
                Event = eventBox.value ?? string.Empty,
                Together = togetherBox != null && togetherBox.value == "true",
                Cooldown = cooldownBox != null ? cooldownBox.valueFloat : 0f,
            };

            for (int i = 0; i < SoundRowCount; i++)
            {
                SoundRow row = rows[i];
                form.Rows[i].Sound = row.Box?.value ?? string.Empty;
                form.Rows[i].VolumePercent = row.VolumeBox != null ? row.VolumeBox.valueInt : DefaultVolumePercent;
                form.Rows[i].Delay = row.DelayBox != null ? row.DelayBox.valueFloat : 0f;
            }

            return form;
        }

        /// <summary>
        /// The player pressed SAVE: if an event and a sound are picked, add it to soundboard.yaml -
        /// as a "together" group when that box is ticked and at least two sounds are picked.
        /// Returns a message for the status line, or null if there was nothing to add. A failure
        /// leaves the picks in the form.
        /// </summary>
        private string CommitPicker()
        {
            if (eventBox == null || togetherBox == null)
            {
                return null; // no page: the game is just loading its saved settings, or the page couldn't be built
            }

            PickerForm form = ReadPicker();
            List<RowForm> picked = form.Rows.Where(r => !string.IsNullOrEmpty(r.Sound)).ToList();
            bool noEvent = string.IsNullOrEmpty(form.Event);

            if (noEvent && picked.Count == 0)
            {
                pickerState = form; // only the numbers may have been touched
                return null;
            }

            if (noEvent || picked.Count == 0)
            {
                pickerState = form;
                string incomplete = "Add Sound: pick " + (noEvent ? "an event" : "a sound") + " too - nothing was added.";
                ShowPickerStatus(incomplete, true);
                return incomplete;
            }

            // Extra rows only count when "together" is ticked; a group of one is just that sound.
            bool together = form.Together && picked.Count >= 2;
            var sound = new NewSound { EventName = form.Event, Together = together, Cooldown = (float)Math.Round(form.Cooldown, 1) };
            foreach (RowForm row in together ? picked : picked.Take(1).ToList())
            {
                sound.Parts.Add(new NewSoundPart
                {
                    File = row.Sound,
                    Volume = row.VolumePercent / 100f,
                    Delay = (float)Math.Round(row.Delay, 1),
                });
            }

            string note = string.Empty;
            if (form.Together && picked.Count == 1)
            {
                note = " Only one sound was picked, so it was added on its own.";
            }
            else if (!form.Together && picked.Count > 1)
            {
                note = " The extra sounds were left out because \"Play several sounds together\" isn't ticked.";
            }

            string problem = SoundboardRuntime.AddSound(sound, out bool written);
            if (!written)
            {
                pickerState = form;
                string failed = "Couldn't add the sound: " + problem + ". Your choices are still on the Add Sound tab.";
                ShowPickerStatus(failed, true);
                return failed;
            }

            pickerState = new PickerForm();
            ApplyForm(pickerState);

            string added = "Added \"" + sound.Label + "\" to " + form.Event + (together ? " (played together)." : ".") + note;
            if (problem != null)
            {
                added += " But " + problem + ".";
                ShowPickerStatus(added, true);
            }
            else
            {
                ShowPickerStatus(added + " It plays now, and shows up on the Sounds tab the next time you open the Mods menu.", false);
            }

            try
            {
                RefreshProblems();
            }
            catch (Exception e)
            {
                Log.LogDebug($"Couldn't refresh the problem list: {e.Message}");
            }

            return added;
        }

        private void ShowPickerStatus(string text, bool isProblem)
        {
            if (pickStatusLabel != null)
            {
                pickStatusLabel.text = text;
                pickStatusLabel.color = isProblem ? PickerErrorColor : pickStatusColor;
            }
        }

        /// <summary>
        /// After RELOAD CONFIG: adds files that have appeared in the sounds folder to the
        /// dropdowns, and drops ones that have gone, without touching what's selected.
        /// </summary>
        private void RefreshSoundList()
        {
            if (rows[0]?.Box == null)
            {
                return;
            }

            try
            {
                List<string> files = SoundLibrary.List(SoundboardRuntime.Locations.SoundFolders);
                var want = new HashSet<string>(files);

                foreach (SoundRow row in rows)
                {
                    if (row.Box == null)
                    {
                        continue;
                    }

                    var have = new HashSet<string>(row.Box.GetItemList().Select(item => item.name));
                    ListItem[] appeared = files.Where(f => !have.Contains(f)).Select(f => new ListItem(f)).ToArray();
                    string[] gone = have.Where(n => !want.Contains(n)).ToArray();

                    if (appeared.Length > 0)
                    {
                        row.Box.AddItems(true, appeared);
                    }

                    // A dropdown can't be emptied, so if every file vanished the old list stays.
                    if (gone.Length > 0 && files.Count > 0)
                    {
                        row.Box.RemoveItems(false, gone);
                    }
                }

                if (soundInfoLabel != null)
                {
                    soundInfoLabel.text = SoundCountText(files.Count);
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Couldn't refresh the list of sound files: {e.Message}");
            }
        }

        // ===================================================================================
        //  Edit Sound tab
        // ===================================================================================

        private const string EditEntryKey = "EditSound_Entry";
        private const string EditCooldownKey = "EditSound_Cooldown";

        // Like the Add Sound page, the rows are built up front and never added or removed while the
        // screen is open: an entry with more sounds than this only has its first few editable.
        private const int EditRowCount = 3;
        private const int MaxShownFileName = 46;

        private Configurable<string> editEntry;
        private Configurable<float> editCooldown;
        private readonly EditRow[] editRows = new EditRow[EditRowCount];

        // What's on the page right now. All null while the menu is closed, or if the page couldn't be built.
        private OpComboBox editBox;
        private PickerUpdown editCooldownBox;
        private OpLabelLong editInfoLabel;
        private OpLabelLong editStatusLabel;
        private Color editStatusColor;

        private bool editorBroken;

        // The entry the page shows whenever it's opened (the one just edited, or one picked but not yet saved).
        private string editSelected = string.Empty;

        /// <summary>One sound row: its Remix settings (kept for good), its widgets (rebuilt with the page) and which sound it currently shows.</summary>
        private sealed class EditRow
        {
            public Configurable<int> Volume;
            public Configurable<float> Delay;

            public OpLabel FileLabel;
            public PickerUpdown VolumeBox;
            public PickerUpdown DelayBox;

            /// <summary>The sound shown (SoundRef.Member and its file), or Member -1 when the row is empty.</summary>
            public int Member = -1;
            public string File = string.Empty;

            public void ForgetWidgets()
            {
                FileLabel = null;
                VolumeBox = null;
                DelayBox = null;
            }
        }

        private void CreateEditSettings()
        {
            try
            {
                editEntry = config.Bind(EditEntryKey, string.Empty, new ConfigurableInfo("The entry to change."));
                editCooldown = config.Bind(EditCooldownKey, 0f, new ConfigAcceptableRange<float>(0f, NewSound.MaxCooldown));

                for (int i = 0; i < EditRowCount; i++)
                {
                    string n = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    editRows[i] = new EditRow
                    {
                        Volume = config.Bind("EditSound_Volume" + n, DefaultVolumePercent, new ConfigAcceptableRange<int>(0, MaxVolumePercent)),
                        Delay = config.Bind("EditSound_Delay" + n, 0f, new ConfigAcceptableRange<float>(0f, NewSound.MaxDelay)),
                    };
                }
            }
            catch (Exception e)
            {
                Log.LogWarning($"Couldn't create the settings for the Edit Sound page, so it won't be available: {e.Message}");
                editorBroken = true;
            }
        }

        private OpTab BuildEditTab()
        {
            var page = new OpTab(this, "Edit Sound");
            ForgetEditor();

            if (!editorBroken)
            {
                try
                {
                    page.AddItems(EditItems());
                    return page;
                }
                catch (Exception e)
                {
                    Log.LogError($"Couldn't build the Edit Sound page: {e}");
                    editorBroken = true;
                    ForgetEditor();
                }
            }

            page.AddItems(
                new OpLabel(20f, 560f, "Edit a Sound", true),
                new OpLabelLong(new Vector2(20f, 480f), new Vector2(560f, 60f), "This page couldn't be set up (the reason is in BepInEx/LogOutput.log). You can still change volume, delay and cooldown by editing soundboard.yaml - press OPEN FOLDER on the Sounds tab.")
                {
                    allowOverflow = false,
                });
            return page;
        }

        private UIelement[] EditItems()
        {
            // One dropdown item per entry, in the order they appear in the file. The name is the entry's id.
            var entries = new List<ListItem>();
            foreach (EventBinding binding in SoundboardRuntime.Config.Events)
            {
                foreach (SoundChoice choice in binding.Choices)
                {
                    entries.Add(new ListItem(choice.Id, binding.EventName + ": " + choice.Label, entries.Count)
                    {
                        desc = "Plays: " + string.Join(" + ", choice.Sounds.Select(s => s.File)),
                    });
                }
            }

            if (entries.Count == 0)
            {
                return new UIelement[]
                {
                    new OpLabel(20f, 560f, "Edit a Sound", true),
                    new OpLabelLong(new Vector2(20f, 480f), new Vector2(560f, 60f), "There are no sounds to edit yet. Add one on the Add Sound tab, or edit soundboard.yaml (OPEN FOLDER on the Sounds tab).")
                    {
                        allowOverflow = false,
                    },
                };
            }

            editInfoLabel = new OpLabelLong(new Vector2(170f, 384f), new Vector2(400f, 60f), string.Empty)
            {
                allowOverflow = false,
            };

            editStatusLabel = new OpLabelLong(new Vector2(20f, 30f), new Vector2(560f, 110f), string.Empty)
            {
                allowOverflow = false,
            };
            editStatusColor = editStatusLabel.color;

            editCooldownBox = new PickerUpdown(editCooldown, new Vector2(170f, 176f), 100f, 1)
            {
                description = "After this entry plays, it can't play again for this many seconds - the event's other entries still take their turns meanwhile. 0 = no limit. For a group of sounds it covers the whole group.",
            };

            float[] rowY = { 292f, 258f, 224f };
            var widgets = new List<UIelement>();
            for (int i = 0; i < EditRowCount; i++)
            {
                EditRow row = editRows[i];

                row.FileLabel = new OpLabel(20f, rowY[i] + 5f, " ", false);

                row.VolumeBox = new PickerUpdown(row.Volume, new Vector2(350f, rowY[i]), 100f)
                {
                    description = "How loud this sound is, as a percentage of the file's own volume. 100 = as recorded, 50 = half as loud, 200 = twice as loud.",
                };

                row.DelayBox = new PickerUpdown(row.Delay, new Vector2(460f, rowY[i]), 100f, 1)
                {
                    description = "Seconds to wait after the event before this sound plays. 0 = right away.",
                };

                widgets.Add(row.FileLabel);
                widgets.Add(row.VolumeBox);
                widgets.Add(row.DelayBox);
            }

            editBox = new OpComboBox(editEntry, new Vector2(170f, 452f), 400f, entries)
            {
                listHeight = 10,
                description = "The entry to change. Click for the list (hover one to see its files), or start typing to search it.",
            };

            editBox.OnValueUpdate += (box, value, oldValue) => ShowEntry(value);

            var items = new List<UIelement>
            {
                new OpLabel(20f, 560f, "Edit a Sound", true),
                new OpLabelLong(new Vector2(20f, 486f), new Vector2(560f, 64f), "Pick an entry, change how loud its sounds are, how long they wait or its cooldown, then press SAVE. Only those numbers in soundboard.yaml change - to switch an entry off use the Sounds tab, to add one use Add Sound.")
                {
                    allowOverflow = false,
                },
                new OpLabel(20f, 456f, "Entry:", false),
                new OpLabel(20f, 326f, "Sound", false),
                new OpLabel(350f, 326f, "Volume (%)", false),
                new OpLabel(460f, 326f, "Delay (seconds)", false),
                new OpLabel(20f, 181f, "Cooldown:", false),
                new OpLabel(280f, 181f, "seconds before it can play again (0 = no limit)", false),
                editInfoLabel,
                editStatusLabel,
                editCooldownBox,
            };

            items.AddRange(widgets);

            // The dropdown goes in last so its list is drawn on top of the rows beneath it.
            items.Add(editBox);
            return items.ToArray();
        }

        private void ForgetEditor()
        {
            editBox = null;
            editCooldownBox = null;
            editInfoLabel = null;
            editStatusLabel = null;
            foreach (EditRow row in editRows)
            {
                row?.ForgetWidgets();
            }
        }

        /// <summary>The page was opened: show the entry it should be on (Remix's own remembered values are ignored).</summary>
        private void SeedEditor()
        {
            if (editBox == null)
            {
                return;
            }

            try
            {
                string id = EntryKnown(editSelected) ? editSelected : string.Empty;
                editBox.ForceValue(id);
                ShowEntry(id);
            }
            catch (Exception e)
            {
                Log.LogDebug($"Couldn't reset the Edit Sound page: {e.Message}");
            }
        }

        private bool EntryKnown(string id)
        {
            return !string.IsNullOrEmpty(id) && editBox != null && editBox.GetItemList().Any(item => item.name == id);
        }

        private static SoundChoice FindEntry(string id)
        {
            return string.IsNullOrEmpty(id) ? null : SoundboardRuntime.Config.Events.SelectMany(e => e.Choices).FirstOrDefault(c => c.Id == id);
        }

        private static int ToPercent(float volume)
        {
            return Mathf.Clamp((int)Math.Round(volume * 100f), 0, MaxVolumePercent);
        }

        /// <summary>
        /// Loads an entry's numbers into the boxes. ForceValue rather than value =: showing an entry
        /// mustn't count as a change the player made. Rows the entry has no sound for are greyed out.
        /// </summary>
        private void ShowEntry(string id)
        {
            if (editBox == null || editCooldownBox == null)
            {
                return;
            }

            try
            {
                SoundChoice choice = FindEntry(id);

                for (int i = 0; i < EditRowCount; i++)
                {
                    EditRow row = editRows[i];
                    SoundRef sound = choice != null && i < choice.Sounds.Count ? choice.Sounds[i] : null;

                    row.Member = sound?.Member ?? -1;
                    row.File = sound?.File ?? string.Empty;
                    row.FileLabel.text = sound != null ? ShortenTo(sound.File, MaxShownFileName) : (choice != null ? "-" : " ");

                    row.VolumeBox.ForceValue((sound != null ? ToPercent(sound.OwnVolume) : DefaultVolumePercent).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    row.VolumeBox.Redraw();
                    row.DelayBox.ForceValue((sound != null ? sound.OwnDelay : 0f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                    row.DelayBox.Redraw();
                    row.VolumeBox.greyedOut = sound == null;
                    row.DelayBox.greyedOut = sound == null;
                }

                editCooldownBox.ForceValue((choice != null ? choice.Cooldown : 0f).ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                editCooldownBox.Redraw();
                editCooldownBox.greyedOut = choice == null;

                if (editInfoLabel != null)
                {
                    editInfoLabel.text = EntryInfo(choice);
                }
            }
            catch (Exception e)
            {
                Log.LogDebug($"Couldn't show the entry on the Edit Sound page: {e.Message}");
            }
        }

        private static string EntryInfo(SoundChoice choice)
        {
            if (choice == null)
            {
                return "Pick an entry to change its volume, delay and cooldown.";
            }

            string info = choice.Enabled ? string.Empty : "(switched off) ";
            if (choice.Sounds.Count > EditRowCount)
            {
                info += "This entry has " + choice.Sounds.Count + " sounds; only the first " + EditRowCount + " can be edited here. ";
            }

            if (choice.Sounds.Count > 1)
            {
                info += "Volume and delay are set per sound; the cooldown covers the whole group.";
            }

            return info.Length > 0 ? info : "Plays: " + choice.Sounds[0].File;
        }

        private static string ShortenTo(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max - 3) + "...";
        }

        /// <summary>
        /// The player pressed SAVE: whatever numbers on the page differ from what the entry has now
        /// are written into soundboard.yaml. Returns a message for the status line, or null if there
        /// was nothing to change. A failure leaves the boxes as the player set them.
        /// </summary>
        private string CommitEdit()
        {
            if (editBox == null || editCooldownBox == null)
            {
                return null; // no page: the game is just loading its saved settings, or the page couldn't be built
            }

            string id = editBox.value ?? string.Empty;
            SoundChoice choice = FindEntry(id);
            editSelected = choice != null ? id : string.Empty;
            if (choice == null)
            {
                return null;
            }

            var tweak = new EntryTweak { ChoiceId = id };
            var changed = new List<string>();

            for (int i = 0; i < EditRowCount; i++)
            {
                EditRow row = editRows[i];
                SoundRef sound = row.Member >= 0 ? choice.Sounds.FirstOrDefault(s => s.Member == row.Member && s.File == row.File) : null;
                if (sound == null)
                {
                    continue;
                }

                int percent = row.VolumeBox.valueInt;
                float delay = (float)Math.Round(row.DelayBox.valueFloat, 1);
                var change = new SoundTweak { Member = row.Member, File = row.File };

                if (percent != ToPercent(sound.OwnVolume))
                {
                    change.Volume = percent / 100f;
                    changed.Add(choice.Sounds.Count > 1 ? sound.File + " volume " + percent + "%" : "volume " + percent + "%");
                }

                if (Math.Abs(delay - Math.Round(sound.OwnDelay, 1)) > 0.001)
                {
                    change.Delay = delay;
                    changed.Add(choice.Sounds.Count > 1 ? sound.File + " delay " + delay.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "s" : "delay " + delay.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "s");
                }

                if (change.Volume.HasValue || change.Delay.HasValue)
                {
                    tweak.Sounds.Add(change);
                }
            }

            float cooldown = (float)Math.Round(editCooldownBox.valueFloat, 1);
            if (Math.Abs(cooldown - Math.Round(choice.Cooldown, 1)) > 0.001)
            {
                tweak.Cooldown = cooldown;
                changed.Add("cooldown " + cooldown.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "s");
            }

            if (tweak.Cooldown == null && tweak.Sounds.Count == 0)
            {
                return null; // nothing differs from the file: say nothing
            }

            string problem = SoundboardRuntime.EditEntry(tweak, out bool written);
            if (!written)
            {
                if (problem == null)
                {
                    return null; // the file already says this
                }

                string failed = "Couldn't change the sound: " + problem + ". Your numbers are still on the Edit Sound tab.";
                ShowEditStatus(failed, true);
                return failed;
            }

            ShowEntry(id); // now shows the numbers as saved (the config has just been re-read)

            string done = "Changed \"" + choice.Label + "\" (" + string.Join(", ", changed) + ").";
            if (problem != null)
            {
                done += " But " + problem + ".";
                ShowEditStatus(done, true);
            }
            else
            {
                ShowEditStatus(done + " It takes effect now.", false);
            }

            try
            {
                RefreshProblems();
            }
            catch (Exception e)
            {
                Log.LogDebug($"Couldn't refresh the problem list: {e.Message}");
            }

            return done;
        }

        private void ShowEditStatus(string text, bool isProblem)
        {
            if (editStatusLabel != null)
            {
                editStatusLabel.text = text;
                editStatusLabel.color = isProblem ? PickerErrorColor : editStatusColor;
            }
        }

        /// <summary>After RELOAD CONFIG: shows the selected entry as the re-read file has it (the file always wins).</summary>
        private void RefreshEditor()
        {
            if (editBox != null)
            {
                ShowEntry(editBox.value);
            }
        }
    }
}
