using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HSKSmartWorkManager
{
    /// <summary>Стат профиля после разрешения ссылки.</summary>
    public class ResolvedStat
    {
        public string statName;
        public StatDef stat;
        public float weight;
        public bool inverted;
    }

    /// <summary>Профиль, привязанный к реальному WorkTypeDef.</summary>
    public class ResolvedProfile
    {
        public WorkTypeDef workType;
        public WorkProfileDef source;
        public WorkCategory category;
        public float skillWeight;
        public float statWeight;
        public bool isFallback;
        public List<ResolvedStat> stats = new List<ResolvedStat>();
        public List<string> missingStats = new List<string>();
    }

    /// <summary>Профиль, у которого не найден вид работы.</summary>
    public class MissingWorkTypeEntry
    {
        public string profileDefName;
        public string workTypeName;
        public string modName;
    }

    /// <summary>Профиль, отключённый из-за отсутствия нужного мода.</summary>
    public class InactiveProfileEntry
    {
        public string profileDefName;
        public string workTypeName;
        public string requiredMods;
    }

    /// <summary>Сопоставляет WorkProfileDef с загруженными WorkTypeDef и создаёт запасные профили.</summary>
    public static class WorkProfileRegistry
    {
        private static readonly Dictionary<WorkTypeDef, ResolvedProfile> profiles = new Dictionary<WorkTypeDef, ResolvedProfile>();
        private static readonly List<MissingWorkTypeEntry> missingWorkTypes = new List<MissingWorkTypeEntry>();
        private static readonly List<string> overriddenProfiles = new List<string>();
        private static readonly List<InactiveProfileEntry> inactiveProfiles = new List<InactiveProfileEntry>();

        public static bool Built { get; private set; }

        public static IReadOnlyList<MissingWorkTypeEntry> MissingWorkTypes => missingWorkTypes;

        /// <summary>Профили, заменённые другим профилем для того же вида работ (например, профилем HSK).</summary>
        public static IReadOnlyList<string> OverriddenProfiles => overriddenProfiles;

        /// <summary>Профили, не применённые, потому что нужный мод не активен.</summary>
        public static IReadOnlyList<InactiveProfileEntry> InactiveProfiles => inactiveProfiles;

        public static IEnumerable<ResolvedProfile> AllProfiles =>
            profiles.Values.OrderBy(p => p.workType.naturalPriority).Reverse();

        public static void Build()
        {
            profiles.Clear();
            missingWorkTypes.Clear();
            overriddenProfiles.Clear();
            inactiveProfiles.Clear();

            // Побеждает профиль с наибольшим priority, при равенстве — загруженный позже.
            foreach (WorkProfileDef def in DefDatabase<WorkProfileDef>.AllDefsListForReading)
            {
                try
                {
                    WorkTypeDef workType = def.workType.NullOrEmpty()
                        ? null
                        : DefDatabase<WorkTypeDef>.GetNamedSilentFail(def.workType);

                    if (workType == null)
                    {
                        missingWorkTypes.Add(new MissingWorkTypeEntry
                        {
                            profileDefName = def.defName,
                            workTypeName = def.workType ?? "(null)",
                            modName = def.modContentPack?.Name ?? "?"
                        });
                        continue;
                    }

                    if (!RequiredModsActive(def))
                    {
                        inactiveProfiles.Add(new InactiveProfileEntry
                        {
                            profileDefName = def.defName,
                            workTypeName = workType.defName,
                            requiredMods = string.Join(", ", def.requiresMods)
                        });
                        continue;
                    }

                    if (profiles.TryGetValue(workType, out ResolvedProfile previous) && previous.source != null)
                    {
                        if (previous.source.priority > def.priority)
                        {
                            overriddenProfiles.Add($"{def.defName} → {previous.source.defName} ({workType.defName})");
                            continue;
                        }
                        overriddenProfiles.Add($"{previous.source.defName} → {def.defName} ({workType.defName})");
                    }

                    profiles[workType] = Resolve(def, workType);
                }
                catch (Exception e)
                {
                    Log.Warning($"[HSK Smart Work] Failed to resolve profile {def?.defName}: {e}");
                }
            }

            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (!profiles.ContainsKey(workType))
                    profiles[workType] = CreateFallback(workType);
            }

            Built = true;
        }

        public static ResolvedProfile GetProfile(WorkTypeDef workType)
        {
            if (!Built)
                Build();
            if (workType == null)
                return null;
            if (!profiles.TryGetValue(workType, out ResolvedProfile profile))
            {
                profile = CreateFallback(workType);
                profiles[workType] = profile;
            }
            return profile;
        }

        private static bool RequiredModsActive(WorkProfileDef def)
        {
            if (def.requiresMods == null || def.requiresMods.Count == 0)
                return true;

            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                foreach (string required in def.requiresMods)
                {
                    if (!required.NullOrEmpty() &&
                        (string.Equals(mod.PackageId, required, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(mod.PackageIdPlayerFacing, required, StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
            }
            return false;
        }

        private static ResolvedProfile Resolve(WorkProfileDef def, WorkTypeDef workType)
        {
            var profile = new ResolvedProfile
            {
                workType = workType,
                source = def,
                category = def.category,
                skillWeight = def.skillWeight,
                statWeight = def.statWeight,
                isFallback = false
            };

            if (def.stats != null)
            {
                foreach (StatWeight sw in def.stats)
                {
                    if (sw == null || sw.stat.NullOrEmpty())
                        continue;

                    StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(sw.stat);
                    if (stat == null)
                    {
                        profile.missingStats.Add(sw.stat);
                        continue;
                    }

                    profile.stats.Add(new ResolvedStat
                    {
                        statName = sw.stat,
                        stat = stat,
                        weight = sw.weight,
                        inverted = sw.inverted
                    });
                }
            }

            return profile;
        }

        private static ResolvedProfile CreateFallback(WorkTypeDef workType)
        {
            var profile = new ResolvedProfile
            {
                workType = workType,
                source = null,
                category = WorkCategory.Primary,
                isFallback = true
            };

            bool hasSkills = workType.relevantSkills != null && workType.relevantSkills.Count > 0;
            profile.skillWeight = hasSkills ? 0.6f : 0f;
            profile.statWeight = hasSkills ? 0.4f : 1f;

            StatDef globalSpeed = StatDefOf.WorkSpeedGlobal;
            if (globalSpeed != null)
            {
                profile.stats.Add(new ResolvedStat
                {
                    statName = globalSpeed.defName,
                    stat = globalSpeed,
                    weight = 1f,
                    inverted = false
                });
            }

            return profile;
        }
    }
}
