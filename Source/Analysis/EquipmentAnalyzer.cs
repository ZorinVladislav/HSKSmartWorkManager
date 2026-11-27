using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HSKSmartWorkManager
{
    public class GearContribution
    {
        public Thing thing;
        public StatDef stat;
        public float offset;
    }

    /// <summary>
    /// Вклад экипировки в статы. Используется ТОЛЬКО для объяснения: значение GetStatValue
    /// уже содержит этот вклад, прибавлять его повторно нельзя.
    /// </summary>
    public static class EquipmentAnalyzer
    {
        public static List<Thing> GetGear(Pawn pawn)
        {
            var gear = new List<Thing>();
            if (pawn?.apparel?.WornApparel != null)
                gear.AddRange(pawn.apparel.WornApparel);
            if (pawn?.equipment?.AllEquipmentListForReading != null)
                gear.AddRange(pawn.equipment.AllEquipmentListForReading);
            return gear;
        }

        public static List<GearContribution> Analyze(Pawn pawn, ResolvedProfile profile)
        {
            var result = new List<GearContribution>();
            if (pawn == null || profile == null)
                return result;

            List<Thing> gear = GetGear(pawn);
            foreach (ResolvedStat rs in profile.stats)
            {
                foreach (Thing thing in gear)
                {
                    float offset;
                    try
                    {
                        offset = StatWorker.StatOffsetFromGear(thing, rs.stat);
                    }
                    catch (Exception)
                    {
                        continue;
                    }

                    if (Math.Abs(offset) > 0.0001f)
                        result.Add(new GearContribution { thing = thing, stat = rs.stat, offset = offset });
                }
            }

            return result;
        }
    }
}
