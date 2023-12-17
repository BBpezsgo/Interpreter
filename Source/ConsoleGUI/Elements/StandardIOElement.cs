using System;
using System.Collections.Generic;
using Win32;

namespace ConsoleGUI
{
    public class StandardIOElement : InlineElement
    {
        readonly struct ConsoleText
        {
            public readonly string Text;
            public readonly byte Color;

            public ConsoleText(string text, byte color)
            {
                Text = text;
                Color = color;
            }
        }

        readonly ScrollBar ScrollBar;
        readonly List<ConsoleText> ConsoleTexts;

        public StandardIOElement() : base()
        {
            ConsoleTexts = new List<ConsoleText>();
            ScrollBar = new ScrollBar(GetScrollBarBounds, this);
        }

        private LanguageCore.Range<int> GetScrollBarBounds(Element element)
        {
            int totalLines = 0;
            for (int i = 0; i < ConsoleTexts.Count; i++)
            {
                string[] lines = ConsoleTexts[i].Text.Split('\n');
                totalLines += lines.Length - 1;

                for (int j = 0; j < lines.Length; j++)
                {
                    if (lines[j].Length >= DrawBuffer.Width) totalLines++;
                }
            }

            return new LanguageCore.Range<int>(0, Math.Max(1, totalLines));
        }

        public void Write(string text, byte color = Win32.ConsoleColor.Silver)
        {
            ConsoleTexts.Add(new ConsoleText(text, color));
        }

        public override void BeforeDraw()
        {
            base.BeforeDraw();

            DrawBuffer b = DrawBuffer;
            b.StepTo(0);

            b.ResetColor();

            int start = Math.Max(0, ScrollBar.Offset);

            int line = 0;
            for (int i = 0; i < ConsoleTexts.Count; i++)
            {
                ConsoleText consoleText = ConsoleTexts[i];
                string text = consoleText.Text;

                string[] lines = text.Split('\n');

                b.ForegroundColor = consoleText.Color;

                for (int j = 0; j < lines.Length; j++)
                {
                    if (line + j < start) continue;
                    if (j == lines.Length - 1 && string.IsNullOrEmpty(lines[j])) continue;

                    string line_ = lines[j];
                    b.AddText(line_.TrimEnd());
                    if (j < lines.Length - 1)
                    { b.FinishLine(Rect.Width); }
                }

                line += lines.Length - 1;
            }

            ScrollBar.Draw(b);
        }

        public override void OnKeyEvent(KeyEvent e)
        {
            base.OnKeyEvent(e);
            ScrollBar.FeedEvent(this, e);
        }

        public override void OnMouseEvent(MouseEvent e)
        {
            base.OnMouseEvent(e);
            ScrollBar.FeedEvent(this, e);
        }
    }
}
