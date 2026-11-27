using System.Collections.Generic;
using Verse;

namespace HSKSmartWorkManager
{
    /// <summary>Важность вида работ для колонии. Определяет правило распределения на этапе III.</summary>
    public enum WorkCategory
    {
        Critical,
        Primary,
        Auxiliary
    }

    /// <summary>Стат, влияющий на эффективность работы. Ссылка хранится строкой и разрешается при запуске.</summary>
    public class StatWeight
    {
        public string stat;
        public float weight = 1f;

        /// <summary>true, если меньшее значение лучше (например, FoodPoisonChance).</summary>
        public bool inverted;
    }

    /// <summary>
    /// Профиль вида работ, задаётся в XML (Defs/). Ссылки на WorkTypeDef и StatDef хранятся строками,
    /// поэтому отсутствующий DLC или мод не вызывает ошибок загрузки.
    /// </summary>
    public class WorkProfileDef : Def
    {
        public string workType;
        public WorkCategory category = WorkCategory.Primary;
        public List<StatWeight> stats = new List<StatWeight>();
        public float skillWeight = 0.6f;
        public float statWeight = 0.4f;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
                yield return error;

            if (workType.NullOrEmpty())
                yield return "workType is not set";
            if (skillWeight < 0f || statWeight < 0f)
                yield return "skillWeight and statWeight must be non-negative";
            if (stats != null)
            {
                foreach (StatWeight sw in stats)
                {
                    if (sw == null || sw.stat.NullOrEmpty())
                        yield return "stats contains an entry without a stat name";
                }
            }
        }
    }
}
