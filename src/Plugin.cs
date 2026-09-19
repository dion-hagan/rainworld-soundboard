using BepInEx;
using HarmonyLib;
using Menu.Remix.MixedUI;

namespace SoundboardMod
{
    [BepInPlugin(MOD_ID, "Custom Soundboard", "1.1.1")]
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

            // Reads soundboard.yaml and starts loading the sound files. Nothing
            // here needs to happen before the game's own sound system is ready:
            // sounds are added to it whenever it is.
            SoundboardRuntime.Initialize(this);

            EventHooks.Apply(harmony);

            MachineConnector.SetRegisteredOI(MOD_ID, new Options());
        }
    }
}
