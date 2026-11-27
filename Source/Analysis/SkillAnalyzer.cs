using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HSKSmartWorkManager
{
    public class SkillInfo
    {
        public SkillDef skill;
        public int level;
        public Passion passion;
        public bool disabled;
        public bool missing;
    }

    /// <summary>Навыки и страсти, связанные с видом работы (WorkTypeDef.relevantSkills).</summary>
    public static class SkillAnalyzer
    {
        public static List<SkillInfo> Analyze(Pawn pawn, WorkTypeDef workType)
        {
            var result = new List<SkillInfo>();
            if (workType?.relevantSkills == null)
                return result;

            foreach (SkillDef skill in workType.relevantSkills)
            {
                if (skill == null)
                    continue;

                SkillRecord record = pawn?.skills?.GetSkill(skill);
                if (record == null)
                {
                    result.Add(new SkillInfo { skill = skill, missing = true, disabled = true });
                    continue;
                }

                bool disabled = record.TotallyDisabled;
                result.Add(new SkillInfo
                {
                    skill = skill,
                    level = disabled ? 0 : record.Level,
                    passion = record.passion,
                    disabled = disabled
                });
            }

            return result;
        }
    }
}
