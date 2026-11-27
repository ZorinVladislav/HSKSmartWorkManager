using System;
using HarmonyLib;
using Verse;

namespace HSKSmartWorkManager
{
    [StaticConstructorOnStartup]
    public static class ModInitializer
    {
        public const string HarmonyId = "Mioko.HSKSmartWorkManager";

        static ModInitializer()
        {
            try
            {
                new Harmony(HarmonyId).PatchAll();
            }
            catch (Exception e)
            {
                Log.Error($"[HSK Smart Work] Harmony patching failed: {e}");
            }

            try
            {
                WorkProfileRegistry.Build();
                int explicitCount = 0;
                int fallbackCount = 0;
                foreach (ResolvedProfile profile in WorkProfileRegistry.AllProfiles)
                {
                    if (profile.isFallback)
                        fallbackCount++;
                    else
                        explicitCount++;
                }
                Log.Message($"[HSK Smart Work] Loaded. Work profiles: {explicitCount} explicit, {fallbackCount} fallback, " +
                            $"{WorkProfileRegistry.MissingWorkTypes.Count} with missing work type.");
            }
            catch (Exception e)
            {
                Log.Error($"[HSK Smart Work] Profile registry build failed: {e}");
            }
        }
    }
}
