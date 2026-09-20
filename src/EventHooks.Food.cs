namespace SoundboardMod
{
    public static partial class EventHooks
    {
        /// <summary>
        /// The PlayerEat&lt;Food&gt; event for an eaten non-creature food, or null if it isn't one of the
        /// foods EdibleFoods knows. The food's own object type is used rather than its class because they
        /// differ (the Bubble Fruit class is SwollenWaterNut but its type is WaterNut) and the type is the
        /// name the game uses everywhere. Read at eat time, ObjectEaten runs before the food is destroyed,
        /// so its abstract object is still there.
        /// </summary>
        private static string FoodEventKey(IPlayerEdible edible)
        {
            string typeName = (edible as PhysicalObject)?.abstractPhysicalObject?.type?.value;
            return EdibleFoods.EventFor(typeName);
        }
    }
}
