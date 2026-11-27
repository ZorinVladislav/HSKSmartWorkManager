using System;
using System.Collections.Generic;
using Verse;

namespace HSKSmartWorkManager
{
    public class CapacityInfo
    {
        public string defName;
        public string label;
        public float level;
        public bool missing;
    }

    public class HealthInfo
    {
        public List<CapacityInfo> capacities = new List<CapacityInfo>();
        public List<string> conditions = new List<string>();
        public bool downed;
        public bool inMentalState;
        public string mentalState;
        public float summaryHealth = 1f;
    }

    /// <summary>
    /// Способности, болезни и недееспособность. Только для объяснения:
    /// влияние здоровья уже учтено в значениях статов.
    /// </summary>
    public static class HealthAnalyzer
    {
        private static readonly string[] CapacityNames = { "Consciousness", "Manipulation", "Sight", "Moving", "Talking" };

        public static HealthInfo Analyze(Pawn pawn)
        {
            var info = new HealthInfo();
            if (pawn?.health == null)
                return info;

            try
            {
                info.downed = pawn.Downed;
                info.inMentalState = pawn.InMentalState;
                info.mentalState = pawn.MentalStateDef?.label;
                info.summaryHealth = pawn.health.summaryHealth?.SummaryHealthPercent ?? 1f;
            }
            catch (Exception) { }

            foreach (string name in CapacityNames)
            {
                PawnCapacityDef def = DefDatabase<PawnCapacityDef>.GetNamedSilentFail(name);
                if (def == null)
                {
                    info.capacities.Add(new CapacityInfo { defName = name, label = name, missing = true });
                    continue;
                }

                float level = 0f;
                try
                {
                    level = pawn.health.capacities.GetLevel(def);
                }
                catch (Exception) { }

                info.capacities.Add(new CapacityInfo { defName = name, label = def.LabelCap, level = level });
            }

            try
            {
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff == null || !hediff.Visible)
                        continue;
                    if (!hediff.def.isBad && !hediff.def.makesSickThought)
                        continue;

                    string part = hediff.Part != null ? $" ({hediff.Part.LabelCap})" : "";
                    info.conditions.Add(hediff.LabelCap + part);
                }
            }
            catch (Exception) { }

            return info;
        }
    }
}
