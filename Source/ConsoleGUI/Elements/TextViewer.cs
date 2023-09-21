using ConsoleDrawer;

using System;
using System.Diagnostics;
using Win32;

namespace ConsoleGUI
{
    internal class TextViewer : WindowElement
    {
        public string Text = "";
        int Scroll;
        Character[] buf;

        public TextViewer() : base()
        {
            ClearBuffer();
        }

        new void ClearBuffer() => buf = new Character[Rect.Width * Rect.Height];

        public override void BeforeDraw()
        {
            if (buf.Length == 0) ClearBuffer();

            ForegroundColor Color;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= buf.Length) return false;

                buf[BufferIndex].ForegroundColor = Color;
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

            Color = ForegroundColor.Default;

            void Line(string lineNumber, string lineText)
            {
                AddText(new string(' ', 4 - lineNumber.Length));
                Color = ForegroundColor.Gray;
                AddText(lineNumber);
                Color = ForegroundColor.Default;
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

        public override Character DrawContent(int X, int Y) => buf[X + (Y * Rect.Width)];

        public override void OnMouseEvent(MouseEvent mouse)
        {
            if (mouse.ButtonState == (uint)MouseButtonState.ScrollUp)
            {
                ClearBuffer();
                ScrollTo(Scroll - 1);
            }
            else if (mouse.ButtonState == (uint)MouseButtonState.ScrollDown)
            {
                ClearBuffer();
                ScrollTo(Scroll + 1);
            }
        }

        void ScrollTo(int value) => Scroll = Math.Clamp(value, 0, Text.Split('\n').Length - Rect.Height + 1);
    }
}
