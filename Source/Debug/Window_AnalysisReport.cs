using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace HSKSmartWorkManager
{
    /// <summary>
    /// Окно с отчётом. Текст рисуется построчно и только видимая часть:
    /// один большой Label в Unity упирается в лимит вершин и обрезается.
    /// </summary>
    public class Window_AnalysisReport : Window
    {
        private const float ButtonHeight = 32f;
        private const float ContentWidth = 2400f;

        private readonly string text;
        private readonly string[] lines;
        private readonly string filePath;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1200f, UI.screenWidth - 50f), Mathf.Min(850f, UI.screenHeight - 50f));

        public Window_AnalysisReport(string text, string filePath)
        {
            this.text = text ?? "";
            this.filePath = filePath;
            lines = this.text.Replace("\r", "").Split('\n');

            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            draggable = true;
            resizeable = true;
            forcePause = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            Rect header = new Rect(inRect.x, inRect.y, inRect.width, ButtonHeight);
            Rect copyRect = new Rect(header.x, header.y, 160f, ButtonHeight);
            if (Widgets.ButtonText(copyRect, "Копировать"))
            {
                GUIUtility.systemCopyBuffer = text;
                Messages.Message("Отчёт скопирован в буфер обмена.", MessageTypeDefOf.TaskCompletion, false);
            }

            Rect refreshRect = new Rect(copyRect.xMax + 10f, header.y, 160f, ButtonHeight);
            if (Widgets.ButtonText(refreshRect, "Обновить"))
            {
                Close();
                DebugActions.RunAnalysis();
                return;
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Rect pathRect = new Rect(refreshRect.xMax + 15f, header.y, header.width - refreshRect.xMax - 15f, ButtonHeight);
            Widgets.Label(pathRect, filePath.NullOrEmpty() ? "Файл не записан." : "Файл: " + filePath);
            Text.Anchor = TextAnchor.UpperLeft;

            Rect outRect = new Rect(inRect.x, header.yMax + 8f, inRect.width, inRect.height - header.height - 8f);
            float lineHeight = Text.LineHeight;
            Rect viewRect = new Rect(0f, 0f, ContentWidth, lines.Length * lineHeight + 10f);

            bool wordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            try
            {
                int first = Mathf.Max(0, (int)(scrollPosition.y / lineHeight) - 1);
                int last = Mathf.Min(lines.Length - 1, first + (int)(outRect.height / lineHeight) + 3);
                for (int i = first; i <= last; i++)
                    Widgets.Label(new Rect(4f, i * lineHeight, ContentWidth - 8f, lineHeight), lines[i]);
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[HSK Smart Work] Report window error: " + e, 0x4853_4B01);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.WordWrap = wordWrap;
            }
        }
    }
}
