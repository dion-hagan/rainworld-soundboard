using BepInEx;
using HarmonyLib;
using Menu.Remix.MixedUI;

namespace SoundboardMod
{
    [BepInPlugin(MOD_ID, "Custom Soundboard", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string MOD_ID = "dion_soundboard";

        private Harmony harmony;
        private bool initialized;

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
        }

        public void OnDisable()
        {
            On.RainWorld.OnModsInit -= RainWorld_OnModsInit;
            harmony?.UnpatchSelf();
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            if (initialized)
            {
                return;
            }
            initialized = true;

            harmony = new Harmony(MOD_ID);

            // SoundIDs must exist before the sound system loads, so this
            // has to happen here rather than later (e.g. on first menu open).
            SoundboardData.Initialize();

            EventHooks.Apply(harmony);

            MachineConnector.SetRegisteredOI(MOD_ID, new Options());
        }
    }
}
