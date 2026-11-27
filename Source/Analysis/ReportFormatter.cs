using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace HSKSmartWorkManager
{
    /// <summary>Собирает текстовый отчёт этапа I.</summary>
    public static class ReportFormatter
    {
        private const string Rule = "================================================================";

        public static string BuildFullReport()
        {
            var sb = new StringBuilder();
            Section(sb, "HSK Smart Work Manager — отчёт анализатора (этап I)", () => AppendHeader(sb));
            Section(sb, "1. Активные моды (порядок загрузки)", () => AppendMods(sb));
            Section(sb, "2. Виды работ (WorkTypeDef)", () => AppendWorkTypes(sb));
            Section(sb, "3. Проблемы профилей", () => AppendProfileProblems(sb));
            Section(sb, "4. Статы из сторонних модов (не Core и не DLC)", () => AppendThirdPartyStats(sb));
            Section(sb, "5. Колонисты", () => AppendColonists(sb));
            return sb.ToString();
        }

        private static void Section(StringBuilder sb, string title, Action body)
        {
            sb.AppendLine(Rule);
            sb.AppendLine(title);
            sb.AppendLine(Rule);
            try
            {
                body();
            }
            catch (Exception e)
            {
                sb.AppendLine("!! Ошибка при формировании раздела: " + e);
            }
            sb.AppendLine();
        }

        private static void AppendHeader(StringBuilder sb)
        {
            sb.AppendLine("Дата: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Версия игры: " + Safe(() => VersionControl.CurrentVersionStringWithRev));
            sb.AppendLine("Ручные приоритеты (числа) включены: " + Safe(() => Find.PlaySettings.useWorkPriorities ? "да" : "нет"));
            sb.AppendLine("Обозначения приоритета: 1–4 — приоритет, 0 — не назначено, «откл.» — работа недоступна колонисту.");
        }

        private static void AppendMods(StringBuilder sb)
        {
            int index = 0;
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                index++;
                sb.AppendLine($"{index,3}. {mod.Name}  [{mod.PackageId}]");
            }
        }

        private static void AppendWorkTypes(StringBuilder sb)
        {
            sb.AppendLine("defName | название | мод | naturalPriority | relevantSkills | профиль");
            sb.AppendLine("----------------------------------------------------------------");
            foreach (WorkTypeDef wt in DefDatabase<WorkTypeDef>.AllDefsListForReading.OrderByDescending(w => w.naturalPriority))
            {
                ResolvedProfile profile = WorkProfileRegistry.GetProfile(wt);
                string skills = wt.relevantSkills == null || wt.relevantSkills.Count == 0
                    ? "—"
                    : string.Join(", ", wt.relevantSkills.Where(s => s != null).Select(s => s.defName));
                string profileText = profile == null
                    ? "нет"
                    : profile.isFallback
                        ? "ЗАПАСНОЙ (Primary, WorkSpeedGlobal)"
                        : $"{profile.source.defName} ({profile.category}, мод: {profile.source.modContentPack?.Name ?? "?"})";
                sb.AppendLine($"{wt.defName} | {wt.labelShort ?? wt.label} | {ModName(wt)} | {wt.naturalPriority} | {skills} | {profileText}");
            }
        }

        private static void AppendProfileProblems(StringBuilder sb)
        {
            bool any = false;

            foreach (MissingWorkTypeEntry entry in WorkProfileRegistry.MissingWorkTypes)
            {
                any = true;
                sb.AppendLine($"Вид работы не найден: {entry.workTypeName} (профиль {entry.profileDefName}, мод {entry.modName})");
            }

            foreach (ResolvedProfile profile in WorkProfileRegistry.AllProfiles)
            {
                foreach (string stat in profile.missingStats)
                {
                    any = true;
                    sb.AppendLine($"Стат не найден: {stat} (профиль {profile.source?.defName}, работа {profile.workType.defName})");
                }
            }

            foreach (string overridden in WorkProfileRegistry.OverriddenProfiles)
            {
                any = true;
                sb.AppendLine("Профиль перекрыт: " + overridden);
            }

            if (!any)
                sb.AppendLine("Проблем не найдено.");
        }

        private static void AppendThirdPartyStats(StringBuilder sb)
        {
            var stats = DefDatabase<StatDef>.AllDefsListForReading
                .Where(s => s.modContentPack == null || !s.modContentPack.IsOfficialMod)
                .GroupBy(s => s.modContentPack?.Name ?? "(неизвестный мод)")
                .OrderBy(g => g.Key);

            int total = 0;
            foreach (IGrouping<string, StatDef> group in stats)
            {
                sb.AppendLine($"[{group.Key}]");
                foreach (StatDef stat in group.OrderBy(s => s.category?.defName).ThenBy(s => s.defName))
                {
                    total++;
                    sb.AppendLine($"  {stat.defName} | {stat.label} | категория: {stat.category?.defName ?? "—"}");
                }
            }
            sb.AppendLine($"Всего: {total}");
        }

        private static void AppendColonists(StringBuilder sb)
        {
            List<Pawn> colonists = PawnAnalyzer.GetColonists();
            if (colonists.Count == 0)
            {
                sb.AppendLine("Колонисты не найдены (запустите анализ в загруженной игре).");
                return;
            }

            foreach (Pawn pawn in colonists)
            {
                AppendPawn(sb, PawnAnalyzer.Analyze(pawn));
                sb.AppendLine();
            }
        }

        public static void AppendPawn(StringBuilder sb, PawnReport report)
        {
            sb.AppendLine("################################################################");
            sb.AppendLine($"### {report.name}");
            sb.AppendLine("################################################################");

            if (!report.error.NullOrEmpty())
                sb.AppendLine("!! Ошибка анализа: " + report.error);

            if (!report.canWork)
                sb.AppendLine("У пешки нет настроек работ (workSettings отсутствует или EverWork = false).");

            HealthInfo health = report.health;
            if (health != null)
            {
                sb.AppendLine($"Здоровье: {health.summaryHealth.ToStringPercent()}" +
                              (health.downed ? " | НЕДЕЕСПОСОБЕН" : "") +
                              (health.inMentalState ? $" | срыв: {health.mentalState}" : ""));
                sb.AppendLine("Способности: " + string.Join(", ", health.capacities.Select(c =>
                    c.missing ? $"{c.defName}: не найдено" : $"{c.label} {c.level.ToStringPercent()}")));
                sb.AppendLine("Состояния: " + (health.conditions.Count == 0 ? "—" : string.Join("; ", health.conditions)));
            }

            sb.AppendLine("Экипировка: " + (report.gear.Count == 0
                ? "—"
                : string.Join("; ", report.gear.Select(t => $"{t.LabelCap} [{t.def.defName}]"))));
            sb.AppendLine();

            foreach (WorkReport work in report.works)
                AppendWork(sb, work);
        }

        private static void AppendWork(StringBuilder sb, WorkReport work)
        {
            string priority = work.disabled
                ? "откл."
                : work.priority < 0 ? "—" : work.priority.ToString();
            ResolvedProfile profile = work.profile;
            string profileText = profile == null ? "нет профиля"
                : profile.isFallback ? "запасной профиль"
                : $"{profile.category}, навыки×{profile.skillWeight:0.##}, статы×{profile.statWeight:0.##}";

            sb.AppendLine($"  [{work.workType.defName}] {work.workType.labelShort ?? work.workType.label} — приоритет: {priority} ({profileText})");

            if (!work.error.NullOrEmpty())
                sb.AppendLine("      !! Ошибка: " + work.error);

            if (work.disabled)
                return;

            if (work.skills.Count > 0)
            {
                sb.AppendLine("      Навыки: " + string.Join(", ", work.skills.Select(s =>
                    s.missing ? $"{s.skill.defName}: нет"
                    : s.disabled ? $"{s.skill.defName}: отключён"
                    : $"{s.skill.defName} {s.level} ({PassionText(s.passion)})")));
            }

            if (work.stats.Count > 0)
            {
                sb.AppendLine("      Статы: " + string.Join(", ", work.stats.Select(s =>
                {
                    string mark = s.profileStat.inverted ? " [меньше — лучше]" : "";
                    string weight = Math.Abs(s.profileStat.weight - 1f) > 0.001f ? $" ×{s.profileStat.weight:0.##}" : "";
                    if (!s.error.NullOrEmpty())
                        return $"{s.profileStat.statName}: ошибка ({s.error})";
                    if (s.disabled)
                        return $"{s.profileStat.statName}: отключён";
                    return $"{s.profileStat.statName} = {s.valueText}{weight}{mark}";
                })));
            }

            if (work.gear.Count > 0)
            {
                sb.AppendLine("      Вклад экипировки (уже учтён в статах): " + string.Join("; ", work.gear.Select(g =>
                    $"{g.thing.LabelShortCap} → {g.stat.defName} {(g.offset >= 0 ? "+" : "")}{StatAnalyzer.SafeToString(g.stat, g.offset)}")));
            }
        }

        private static string PassionText(Passion passion)
        {
            switch ((int)passion)
            {
                case 0: return "без страсти";
                case 1: return "малая страсть";
                case 2: return "большая страсть";
                default: return "страсть: " + passion;
            }
        }

        private static string ModName(Def def)
        {
            return def?.modContentPack?.Name ?? "?";
        }

        private static string Safe(Func<string> getter)
        {
            try
            {
                return getter();
            }
            catch (Exception)
            {
                return "?";
            }
        }
    }
}
