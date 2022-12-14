using System;
using System.Diagnostics;

namespace ConsoleGUI
{
    internal class TextViewer : BaseWindowElement
    {
        public string Text;
        int Scroll;
        Character[] buf;

        public TextViewer() : base()
        {
            ClearBuffer();
        }

        void ClearBuffer() => buf = new Character[Rect.Width * Rect.Height];

        internal override void BeforeDraw()
        {
            if (buf.Length == 0) ClearBuffer();

            CharColors Color;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= buf.Length) return false;

                buf[BufferIndex].Color = Color;
                buf[BufferIndex].Char = data;

                BufferIndex++;
                if (BufferIndex >= buf.Length) return false;

                return true;
            }
            void AddText(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (!AddChar(text[i])) break;
                }
            }
            void AddSpace(int to)
            {
                while (BufferIndex % Rect.Width < to)
                {
                    if (!AddChar(' ')) break;
                }
            }

            Color = CharColors.FgDefault;

            void Line(string lineNumber, string lineText)
            {
                AddText(" ".Repeat(4 - lineNumber.Length));
                Color = CharColors.FgGray;
                AddText(lineNumber);
                Color = CharColors.FgDefault;
                AddSpace(5);
                AddText(lineText);
            }

            string[] Lines = Text.Split('\n');

            float scrollPercent = Scroll / (float)(Lines.Length - Rect.Height + 1);

            for (int lineNumber = Scroll; lineNumber < Lines.Length; lineNumber++)
            {
                string lineNumberText = (lineNumber + 1).ToString();

                if (Lines[lineNumber].Length == 0)
                {
                    Line(lineNumberText, "");
                    goto Final;
                }

                int remaingSpace = Rect.Width - 7;
                string remaingText = Lines[lineNumber];
                while (remaingText.Length > 0)
                {
                    if (remaingText.Length > remaingSpace)
                    {
                        Line(lineNumberText, remaingText[..remaingSpace]);
                        lineNumberText = "";
                        remaingText = remaingText[remaingSpace..];
                    }
                    else
                    {
                        Line(lineNumberText, remaingText);
                        break;
                    }
                }

            Final:

                BufferIndex++;
                if (BufferIndex >= buf.Length) { return; }

                AddSpace(Rect.Width - 2);

                {
                    int i = lineNumber - Scroll;
                    float heightPercent = i / (float)(Rect.Height - 1);

                    if (((Text.Split('\n').Length - Rect.Height + 1) > 0) && Math.Abs(scrollPercent - heightPercent) < 0.1f)
                    {
                        AddText("█ ");
                    }
                    else if (((Text.Split('\n').Length - Rect.Height + 1) > 0) && Math.Abs(scrollPercent - heightPercent) < 0.15f)
                    {
                        AddText("▓ ");
                    }
                    else
                    {
                        AddText("  ");
                    }
                }
            }
        }

        internal override Character OnDrawContent(int X, int Y) => buf[X + (Y * Rect.Width)];

        internal override void OnMouseEvent(MouseInfo mouse)
        {
            if (mouse.ButtonState == MouseInfo.ButtonStateEnum.ScrollUp)
            {
                ClearBuffer();
                ScrollTo(Scroll - 1);
            }
            else if (mouse.ButtonState == MouseInfo.ButtonStateEnum.ScrollDown)
            {
                ClearBuffer();
                ScrollTo(Scroll + 1);
            }
        }

        void ScrollTo(int value) => Scroll = Math.Clamp(value, 0, Text.Split('\n').Length - Rect.Height + 1);
    }
}
