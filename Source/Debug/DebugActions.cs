using System;
using System.IO;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace HSKSmartWorkManager
{
    public static class DebugActions
    {
        public const string Category = "HSK Smart Work";

        [DebugAction(Category, "Анализ колонистов", allowedGameStates = AllowedGameStates.Playing)]
        public static void AnalyzeColonists()
        {
            RunAnalysis();
        }

        [DebugAction(Category, "Пересобрать профили", allowedGameStates = AllowedGameStates.Entry | AllowedGameStates.Playing)]
        public static void RebuildProfiles()
        {
            WorkProfileRegistry.Build();
            Messages.Message("HSK Smart Work: профили пересобраны.", MessageTypeDefOf.NeutralEvent, false);
        }

        public static void RunAnalysis()
        {
            string report;
            try
            {
                report = ReportFormatter.BuildFullReport();
            }
            catch (Exception e)
            {
                Log.Error("[HSK Smart Work] Failed to build report: " + e);
                return;
            }

            string path = WriteReport(report);
            Find.WindowStack.Add(new Window_AnalysisReport(report, path));
        }

        private static string WriteReport(string report)
        {
            try
            {
                string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "HSKSmartWorkManager");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "analysis.txt");
                File.WriteAllText(path, report, new UTF8Encoding(true));
                Log.Message("[HSK Smart Work] Report written to " + path);
                return path;
            }
            catch (Exception e)
            {
                Log.Warning("[HSK Smart Work] Failed to write report file: " + e.Message);
                return null;
            }
        }
    }
}
