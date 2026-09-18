using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace SoundboardMod
{
    /// <summary>
    /// Builds the mod's in-game options screen (Options -> Mods -> Custom Soundboard):
    /// an "enable all" / "disable all" pair of buttons, plus one checkbox per sound
    /// found in soundeffects/meta.json.
    /// </summary>
    public class Options : OptionInterface
    {
        public static Options Instance { get; private set; }

        private readonly Dictionary<string, Configurable<bool>> enabledConfigs = new Dictionary<string, Configurable<bool>>();
        private readonly List<OpCheckBox> checkBoxes = new List<OpCheckBox>();

        public Options()
        {
            Instance = this;

            foreach (SoundEntry entry in SoundboardData.Sounds)
            {
                enabledConfigs[entry.id] = config.Bind(
                    entry.id,
                    entry.defaultEnabled,
                    new ConfigurableInfo(entry.description));
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            OpTab tab = new OpTab(this, "Sounds");
            Tabs = new OpTab[] { tab };

            var enableAllButton = new OpSimpleButton(new Vector2(20f, 490f), new Vector2(110f, 30f), "ENABLE ALL");
            enableAllButton.OnClick += _ => SetAll(true);

            var disableAllButton = new OpSimpleButton(new Vector2(140f, 490f), new Vector2(110f, 30f), "DISABLE ALL");
            disableAllButton.OnClick += _ => SetAll(false);

            var items = new List<UIelement>
            {
                new OpLabel(20f, 550f, "Custom Soundboard", true),
                new OpLabel(20f, 525f, "Enable or disable individual sound effects below.", false),
                enableAllButton,
                disableAllButton
            };

            checkBoxes.Clear();
            float y = 445f;
            foreach (SoundEntry entry in SoundboardData.Sounds)
            {
                var checkBox = new OpCheckBox(enabledConfigs[entry.id], new Vector2(20f, y))
                {
                    description = entry.description
                };
                checkBoxes.Add(checkBox);

                items.Add(checkBox);
                items.Add(new OpLabel(60f, y + 4f, entry.displayName ?? entry.id, false));

                y -= 34f;
            }

            if (SoundboardData.Sounds.Count == 0)
            {
                items.Add(new OpLabel(20f, y, "No sounds registered yet - see README.md.", false));
            }

            tab.AddItems(items.ToArray());
        }

        public bool IsEnabled(string soundId)
        {
            return enabledConfigs.TryGetValue(soundId, out Configurable<bool> cfg) && cfg.Value;
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
