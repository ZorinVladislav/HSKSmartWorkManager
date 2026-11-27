using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HSKSmartWorkManager
{
    public class StatInfo
    {
        public ResolvedStat profileStat;
        public float value;
        public string valueText;
        public bool disabled;
        public string error;
    }

    /// <summary>
    /// Значения статов профиля. Источник истины — pawn.GetStatValue: значение уже включает
    /// экипировку, здоровье и глобальную скорость работы.
    /// </summary>
    public static class StatAnalyzer
    {
        public static List<StatInfo> Analyze(Pawn pawn, ResolvedProfile profile)
        {
            var result = new List<StatInfo>();
            if (pawn == null || profile == null)
                return result;

            foreach (ResolvedStat rs in profile.stats)
                result.Add(Evaluate(pawn, rs));

            return result;
        }

        public static StatInfo Evaluate(Pawn pawn, ResolvedStat rs)
        {
            var info = new StatInfo { profileStat = rs };
            try
            {
                if (rs.stat.Worker != null && rs.stat.Worker.IsDisabledFor(pawn))
                {
                    info.disabled = true;
                    info.valueText = "—";
                    return info;
                }

                info.value = pawn.GetStatValue(rs.stat);
                info.valueText = SafeToString(rs.stat, info.value);
            }
            catch (Exception e)
            {
                // StatWorker из модов может выбросить исключение.
                info.error = e.GetType().Name + ": " + e.Message;
                info.valueText = "ошибка";
            }
            return info;
        }

        public static string SafeToString(StatDef stat, float value)
        {
            try
            {
                return stat.ValueToString(value, ToStringNumberSense.Absolute);
            }
            catch
            {
                return value.ToString("0.###");
            }
        }
    }
}
