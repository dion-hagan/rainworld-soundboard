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
    /// It has two tabs:
    ///
    /// "Sounds": buttons to reload the config and open its folder, a list of any
    /// problems found in soundboard.yaml, and one checkbox per sound entry.
    ///
    /// "Add Sound": dropdowns for an event and a sound file plus number boxes for
    /// volume and delay; SAVE adds that sound to the event's list in soundboard.yaml.
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

            // Each time the page is opened Remix reloads its own saved copy of the settings into
            // the widgets. That copy can be stale (or from an older version of this mod), so on
            // every open the boxes are re-seeded from soundboard.yaml, which is the only thing that
            // counts - and the Add Sound form from what this mod last knew about it.
            OnActivate += RefreshToggles;
            OnActivate += SeedPicker;

            // The widgets are gone once the menu is left; anything that runs before the next
            // Initialize must not touch them.
            OnUnload += ForgetPicker;
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
            Tabs = new OpTab[] { tab, BuildAddSoundTab() };

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
                new OpLabel(20f, 538f, "Tick or untick sounds, then press SAVE. To add a new sound, use the Add Sound tab - or edit soundboard.yaml (OPEN FOLDER) and RELOAD CONFIG.", false),
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

            string added = null;
            try
            {
                added = CommitPicker();
            }
            catch (Exception e)
            {
                Log.LogError($"Adding the sound to soundboard.yaml failed: {e}");
                added = "Couldn't add the sound: " + e.Message;
                ShowPickerStatus(added);
            }

            string message = string.Join(" ", new[] { ticked, added }.Where(m => !string.IsNullOrEmpty(m)));
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
        private const string PickSoundKey = "AddSound_Sound";
        private const string PickVolumeKey = "AddSound_Volume";
        private const string PickDelayKey = "AddSound_Delay";

        private const int DefaultVolumePercent = 100;
        private const int MaxVolumePercent = (int)(NewSound.MaxVolume * 100f);

        private static readonly Color PickerErrorColor = new Color(1f, 0.45f, 0.4f);

        // The form's Remix settings. Created once, like the checkbox ones (Remix doesn't allow a key twice).
        private Configurable<string> pickEvent;
        private Configurable<string> pickSound;
        private Configurable<int> pickVolume;
        private Configurable<float> pickDelay;

        // What's on the page right now. All null while the menu is closed, or if the page couldn't be built.
        private OpComboBox eventBox;
        private OpComboBox soundBox;
        private PickerUpdown volumeBox;
        private PickerUpdown delayBox;
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

        private sealed class PickerForm
        {
            public string Event = string.Empty;
            public string Sound = string.Empty;
            public int VolumePercent = DefaultVolumePercent;
            public float Delay;
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
                pickSound = config.Bind(PickSoundKey, string.Empty, new ConfigurableInfo("The audio file the new sound plays."));
                pickVolume = config.Bind(PickVolumeKey, DefaultVolumePercent, new ConfigAcceptableRange<int>(0, MaxVolumePercent));
                pickDelay = config.Bind(PickDelayKey, 0f, new ConfigAcceptableRange<float>(0f, NewSound.MaxDelay));
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

            eventInfoLabel = new OpLabelLong(new Vector2(170f, 372f), new Vector2(400f, 72f), string.Empty)
            {
                allowOverflow = false,
            };

            soundInfoLabel = new OpLabelLong(new Vector2(170f, 282f), new Vector2(400f, 40f), SoundCountText(files.Count))
            {
                allowOverflow = false,
            };

            pickStatusLabel = new OpLabelLong(new Vector2(20f, 60f), new Vector2(560f, 80f), string.Empty)
            {
                allowOverflow = false,
            };
            pickStatusColor = pickStatusLabel.color;

            volumeBox = new PickerUpdown(pickVolume, new Vector2(170f, 238f), 100f)
            {
                description = "How loud the sound is, as a percentage of the file's own volume. 100 = as recorded, 50 = half as loud, 200 = twice as loud.",
            };

            delayBox = new PickerUpdown(pickDelay, new Vector2(170f, 194f), 100f, 1)
            {
                description = "Seconds to wait after the event before the sound plays. 0 = right away.",
            };

            // The dropdowns go in last: a list that opens over other widgets has to be drawn on top of them.
            soundBox = files.Count == 0
                ? null
                : new OpComboBox(pickSound, new Vector2(170f, 328f), 400f, files.Select(f => new ListItem(f)).ToList())
                {
                    listHeight = 8,
                    description = "The audio file to play. Click for the list, or start typing to search it.",
                };

            eventBox = new OpComboBox(pickEvent, new Vector2(170f, 452f), 400f, events)
            {
                listHeight = 10,
                description = "The in-game event that makes the sound play. Click for the list (hover a name to see what it means), or start typing to search it.",
            };

            eventBox.OnValueUpdate += (box, value, oldValue) => UpdateEventInfo();

            var items = new List<UIelement>
            {
                new OpLabel(20f, 560f, "Add a Sound", true),
                new OpLabelLong(new Vector2(20f, 486f), new Vector2(560f, 64f), "Pick an event and a sound file, set how loud it is and how long to wait, then press SAVE. The sound is added to the end of that event's list in soundboard.yaml and works straight away.")
                {
                    allowOverflow = false,
                },
                new OpLabel(20f, 456f, "When this happens:", false),
                new OpLabel(20f, 332f, "Play this sound:", false),
                new OpLabel(20f, 243f, "Volume:", false),
                new OpLabel(20f, 199f, "Delay:", false),
                new OpLabel(280f, 243f, "%  (100 = as recorded)", false),
                new OpLabel(280f, 199f, "seconds after the event", false),
                eventInfoLabel,
                soundInfoLabel,
                pickStatusLabel,
                volumeBox,
                delayBox,
            };

            if (soundBox != null)
            {
                items.Add(soundBox);
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

        private void ForgetPicker()
        {
            eventBox = null;
            soundBox = null;
            volumeBox = null;
            delayBox = null;
            eventInfoLabel = null;
            soundInfoLabel = null;
            pickStatusLabel = null;
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
                soundBox?.ForceValue(Known(soundBox, form.Sound));

                if (volumeBox != null)
                {
                    volumeBox.ForceValue(form.VolumePercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    volumeBox.Redraw();
                }

                if (delayBox != null)
                {
                    delayBox.ForceValue(form.Delay.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
                    delayBox.Redraw();
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
            return new PickerForm
            {
                Event = eventBox.value ?? string.Empty,
                Sound = soundBox?.value ?? string.Empty,
                VolumePercent = volumeBox != null ? volumeBox.valueInt : DefaultVolumePercent,
                Delay = delayBox != null ? delayBox.valueFloat : 0f,
            };
        }

        /// <summary>
        /// The player pressed SAVE: if an event and a sound are picked, add that sound to
        /// soundboard.yaml. Returns a message for the status line, or null if there was
        /// nothing to add. A failure leaves the picks in the form.
        /// </summary>
        private string CommitPicker()
        {
            if (eventBox == null || volumeBox == null || delayBox == null)
            {
                return null; // no page: the game is just loading its saved settings, or the page couldn't be built
            }

            PickerForm form = ReadPicker();
            bool noEvent = string.IsNullOrEmpty(form.Event);
            bool noSound = string.IsNullOrEmpty(form.Sound);
            if (noEvent && noSound)
            {
                pickerState = form; // only the numbers may have been touched
                return null;
            }

            if (noEvent || noSound)
            {
                pickerState = form;
                string incomplete = "Add Sound: pick " + (noEvent ? "an event" : "a sound") + " too - nothing was added.";
                ShowPickerStatus(incomplete, true);
                return incomplete;
            }

            var sound = new NewSound
            {
                EventName = form.Event,
                File = form.Sound,
                Volume = form.VolumePercent / 100f,
                Delay = (float)Math.Round(form.Delay, 1),
            };

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

            string added = "Added \"" + SoundboardConfigParser.PrettyName(form.Sound) + "\" to " + form.Event + ".";
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

        private void ShowPickerStatus(string text)
        {
            ShowPickerStatus(text, true);
        }

        /// <summary>
        /// After RELOAD CONFIG: adds files that have appeared in the sounds folder to the
        /// dropdown, and drops ones that have gone, without touching what's selected.
        /// </summary>
        private void RefreshSoundList()
        {
            if (soundBox == null)
            {
                return;
            }

            try
            {
                List<string> files = SoundLibrary.List(SoundboardRuntime.Locations.SoundFolders);
                var have = new HashSet<string>(soundBox.GetItemList().Select(item => item.name));
                var want = new HashSet<string>(files);

                ListItem[] appeared = files.Where(f => !have.Contains(f)).Select(f => new ListItem(f)).ToArray();
                string[] gone = have.Where(n => !want.Contains(n)).ToArray();

                if (appeared.Length > 0)
                {
                    soundBox.AddItems(true, appeared);
                }

                // A dropdown can't be emptied, so if every file vanished the old list stays.
                if (gone.Length > 0 && files.Count > 0)
                {
                    soundBox.RemoveItems(false, gone);
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
    }
}
