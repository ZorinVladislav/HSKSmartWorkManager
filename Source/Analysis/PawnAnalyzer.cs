using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HSKSmartWorkManager
{
    public class WorkReport
    {
        public WorkTypeDef workType;
        public ResolvedProfile profile;
        public bool disabled;

        /// <summary>Текущий приоритет: 0 — не назначено, -1 — у пешки нет настроек работ.</summary>
        public int priority;

        public List<SkillInfo> skills = new List<SkillInfo>();
        public List<StatInfo> stats = new List<StatInfo>();
        public List<GearContribution> gear = new List<GearContribution>();
        public string error;
    }

    public class PawnReport
    {
        public Pawn pawn;
        public string name;
        public bool canWork;
        public HealthInfo health;
        public List<Thing> gear = new List<Thing>();
        public List<WorkReport> works = new List<WorkReport>();
        public string error;
    }

    /// <summary>Собирает полный отчёт по колонисту.</summary>
    public static class PawnAnalyzer
    {
        public static PawnReport Analyze(Pawn pawn)
        {
            var report = new PawnReport
            {
                pawn = pawn,
                name = pawn?.LabelShortCap ?? "(null)"
            };
            if (pawn == null)
                return report;

            try
            {
                report.canWork = pawn.workSettings != null && pawn.workSettings.EverWork;
                report.health = HealthAnalyzer.Analyze(pawn);
                report.gear = EquipmentAnalyzer.GetGear(pawn);

                IEnumerable<WorkTypeDef> workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .OrderByDescending(w => w.naturalPriority);

                foreach (WorkTypeDef workType in workTypes)
                    report.works.Add(AnalyzeWork(pawn, workType, report.canWork));
            }
            catch (Exception e)
            {
                report.error = e.ToString();
            }

            return report;
        }

        public static WorkReport AnalyzeWork(Pawn pawn, WorkTypeDef workType, bool canWork)
        {
            var report = new WorkReport
            {
                workType = workType,
                profile = WorkProfileRegistry.GetProfile(workType)
            };

            try
            {
                report.disabled = pawn.WorkTypeIsDisabled(workType);
                report.priority = canWork ? pawn.workSettings.GetPriority(workType) : -1;
                report.skills = SkillAnalyzer.Analyze(pawn, workType);
                report.stats = StatAnalyzer.Analyze(pawn, report.profile);
                report.gear = EquipmentAnalyzer.Analyze(pawn, report.profile);
            }
            catch (Exception e)
            {
                report.error = e.GetType().Name + ": " + e.Message;
            }

            return report;
        }

        public static List<Pawn> GetColonists()
        {
            var result = new List<Pawn>();
            try
            {
                foreach (Pawn pawn in PawnsFinder.AllMaps_FreeColonists)
                {
                    if (pawn != null && !result.Contains(pawn))
                        result.Add(pawn);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[HSK Smart Work] Failed to enumerate colonists: {e.Message}");
            }
            return result.OrderBy(p => p.LabelShortCap).ToList();
        }
    }
}
