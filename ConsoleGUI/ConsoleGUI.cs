using Microsoft.Win32.SafeHandles;

using System;
using System.Collections.Generic;

namespace ConsoleGUI
{
    using ConsoleLib;

    using static ConsoleLib.NativeMethods;
    using static Core;

    internal class ConsoleGUI
    {
        internal static Character NullCharacter => new()
        {
            Char = ' ',
            Color = CharColors.BgBlack
        };

        internal List<BaseWindowElement> Elements = new();
        readonly SafeFileHandle ConsoleHandle;

        bool DebugLogs;

        short width;
        short height;
        CharInfo[] ConsoleBuffer;
        SmallRect ConsoleRect;

        MouseInfo Mouse;
        internal BaseWindowElement FilledElement;

        bool ResizeElements;

        void Log(string message)
        {
            if (!DebugLogs) return;
            // Console.WriteLine(message);
            System.Diagnostics.Debug.WriteLine(message);
        }

        internal ConsoleGUI(bool DebugLogs = false)
        {
            this.DebugLogs = DebugLogs;

            Log("Setup timers");
            System.Timers.Timer aTimer = new();
            aTimer.Elapsed += TimerElapsed;
            aTimer.Interval = 500;
            aTimer.Enabled = true;

            System.Timers.Timer bTimer = new();
            bTimer.Elapsed += BTimer_Elapsed;
            bTimer.Interval = 1000;
            bTimer.Enabled = true;

            System.Timers.Timer cTimer = new();
            cTimer.Elapsed += (_, _) =>
            {
                cTimer.Stop();
                cTimer.Dispose();

                foreach (var element in Elements)
                { element.OnStart(); }
                FilledElement?.OnStart();
            };
            cTimer.Interval = 2000;
            cTimer.Enabled = true;

            Log("Setup console");
            SetupConsole();

            ConsoleListener.MouseEvent += MouseEvent;
            ConsoleListener.KeyEvent += KeyEvent;
            ConsoleListener.WindowBufferSizeEvent += WindowBufferSizeEvent;

            Mouse = new MouseInfo();

            Log("Start console listener");
            ConsoleListener.Start();

            Log("Setup console handler");
            ConsoleHandle = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, System.IO.FileMode.Open, 0, IntPtr.Zero);

            width = (short)Console.WindowWidth;
            height = (short)Console.WindowHeight;

            Log("Start");
            Start();
        }

        internal void Start()
        {
            Clear();

            Log("Refresh elements size");
            RefreshElementsSize(true);

            Log("Refresh console");
            RefreshConsole();
        }

        void Clear()
        {
            Log("Clear console");

            ConsoleBuffer = new CharInfo[width * height];
            for (int i = 0; i < ConsoleBuffer.Length; i++)
            {
                ConsoleBuffer[i].Char.UnicodeChar = ' ';
            }
            ConsoleRect = new SmallRect() { Left = 0, Top = 0, Right = width, Bottom = height };
        }

        private void BTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) => BTimer_Elapsed();
        private void BTimer_Elapsed() => ResizeElements = true;

        private void WindowBufferSizeEvent(WINDOW_BUFFER_SIZE_RECORD r) => ResizeElements = true;

        void RefreshElementsSize(bool Force = false)
        {
            if (ResizeElements || Force)
            {
                width = (short)Console.WindowWidth;
                height = (short)Console.WindowHeight;

                Clear();

                ResizeElements = false;
                foreach (var Element in Elements) Element.RefreshSize();
                if (FilledElement != null)
                {
                    FilledElement.Rect = new System.Drawing.Rectangle(0, 0, Console.WindowWidth - 1, Console.WindowHeight - 1);
                    FilledElement.RefreshSize();
                }
            }
        }

        void RefreshConsole()
        {
            if (ConsoleHandle.IsInvalid)
            {
                Log("Console handler is invalid");
                return;
            }

            RefreshElementsSize();

            foreach (var Element in Elements) Element.BeforeDraw();
            FilledElement?.BeforeDraw();

            /*
            for (int i = 0; i < ConsoleBuffer.Length; i++)
            {
                int X = i % width;
                int Y = i / width;
                Character chr = '-'.Details();

                ConsoleBuffer[i].Attributes = (short)chr.Color;
                ConsoleBuffer[i].Char.UnicodeChar = chr.Char;
            }
            */

            if (FilledElement != null)
            {
                DrawElement(FilledElement, true);
            }
            else
            {
                foreach (var Element in Elements)
                {
                    DrawElement(Element);
                }
            }

            WriteConsole(ref ConsoleRect);
        }

        static Character DrawElement(BaseWindowElement Element, int X, int Y)
        {
            if (Element.HasBorder)
            {
                if (Element.Rect.Top == Y)
                {
                    return Element.OnDrawBorder(X, Y);
                }
                else if (Element.Rect.Left == X)
                {
                    return Element.OnDrawBorder(X, Y);
                }
                else if (Element.Rect.Bottom == Y)
                {
                    return Element.OnDrawBorder(X, Y);
                }
                else if (Element.Rect.Right == X)
                {
                    return Element.OnDrawBorder(X, Y);
                }

                return Element.OnDrawContent(X - Element.Rect.Left - 1, Y - Element.Rect.Top - 1);
            }
            return Element.OnDrawContent(X - Element.Rect.Left - 1, Y - Element.Rect.Top - 1);
        }
        void DrawElement(BaseWindowElement Element, bool IsFilled = false)
        {
            for (int x = Element.Rect.Left; x <= Element.Rect.Right; x++)
            {
                for (int y = Element.Rect.Top; y <= Element.Rect.Bottom; y++)
                {
                    int i = y * width + x;
                    if (i >= ConsoleBuffer.Length) continue;
                    if (i < 0) continue;

                    Character chr;
                    if (IsFilled)
                    {
                        chr = Element.OnDrawContent(x, y);
                    }
                    else
                    {
                        chr = DrawElement(Element, x, y);
                    }
                    ConsoleBuffer[i].Attributes = (short)chr.Color;
                    ConsoleBuffer[i].Char.UnicodeChar = chr.Char;
                }
            }
        }

        void WriteConsole(ref SmallRect rect)
        {
            if (ConsoleHandle.IsInvalid)
            {
                System.Diagnostics.Debug.Fail("Console handle is invalid");
                return;
            }
            if (ConsoleHandle.IsClosed)
            {
                System.Diagnostics.Debug.Fail("Console handle is closed");
                return;
            }

            WriteConsoleOutputW(ConsoleHandle, ConsoleBuffer,
                new Coord() { X = (short)Console.WindowWidth, Y = (short)Console.WindowHeight },
                new Coord() { X = 0, Y = 0 },
                ref rect);
        }
        void KeyEvent(KEY_EVENT_RECORD e)
        {
            if (!e.bKeyDown)
            {
                if (e.AsciiChar == 27)
                {
                    ConsoleListener.Stop();
                    return;
                }
            }
            else
            {
#if false

                if (e.wVirtualKeyCode == 37)
                {
                    CurrentIndex--;
                    SetPositionToOkay();
                }
                else if (e.wVirtualKeyCode == 38)
                {
                    CurrentIndex = PositionToIndex(CurrentPosition[0], Math.Max(0, CurrentPosition[1] - 1));
                    SetPositionToOkay();
                }
                else if (e.wVirtualKeyCode == 39)
                {
                    CurrentIndex++;
                    SetPositionToOkay();
                }
                else if (e.wVirtualKeyCode == 40)
                {
                    CurrentIndex = PositionToIndex(CurrentPosition[0], Math.Min(Text.Split('\n').Length, CurrentPosition[1] + 1));
                    SetPositionToOkay();
                }
                else if (e.AsciiChar > 50 && e.AsciiChar < 150 || e.AsciiChar == 32)
                {
                    Text = Text.Insert(CurrentIndex, e.UnicodeChar.ToString());
                    CurrentIndex++;
                }
                else if (e.AsciiChar == 8)
                {
                    if (CurrentIndex > 0)
                    {
                        Text = Text.Remove(CurrentIndex - 1, 1);
                        CurrentIndex--;
                    }
                }
                else if (e.AsciiChar == 46)
                {
                    if (CurrentIndex < Text.Length)
                    {
                        Text = Text.Remove(CurrentIndex, 1);
                    }
                }
                else if (e.AsciiChar == 13)
                {
                    Text = Text.Insert(CurrentIndex, "\n");
                    CurrentIndex++;
                }
                else
                {
                    Debug.WriteLine($"{e.wVirtualKeyCode} {e.AsciiChar}");
                }
#endif
            }

            for (int i = Elements.Count - 1; i >= 0; i--)
            {
                if (!Elements[i].Contains(Mouse.X, Mouse.Y) && !Elements[i].IsFocused) continue;
                Elements[i].OnKeyEvent(e);
                Elements[i].BeforeDraw();
                DrawElement(Elements[i]);
            }
            if (FilledElement != null)
            {
                FilledElement.OnKeyEvent(e);
                FilledElement.BeforeDraw();
                DrawElement(FilledElement);
            }

            // Console.WriteLine($"'{e.UnicodeChar}'             {e.AsciiChar} {e.bKeyDown} {e.dwControlKeyState} {e.wRepeatCount}");
        }

        void MouseEvent(MOUSE_EVENT_RECORD e)
        {
            Mouse.X = e.dwMousePosition.X;
            Mouse.Y = e.dwMousePosition.Y;
            Mouse.ButtonState = (MouseInfo.ButtonStateEnum)e.dwButtonState;
            Mouse.ControlKeyState = e.dwControlKeyState;
            Mouse.Flags = e.dwEventFlags;

            for (int i = Elements.Count - 1; i >= 0; i--)
            {
                if (!Elements[i].Contains(Mouse.X, Mouse.Y) && !Elements[i].IsFocused) continue;
                Elements[i].OnMouseEvent(Mouse);
                Elements[i].BeforeDraw();
                DrawElement(Elements[i]);

                Elements[i].OnMouseEventBase(Mouse);
            }
            if (FilledElement != null)
            {
                FilledElement.OnMouseEvent(Mouse);
                FilledElement.BeforeDraw();
                DrawElement(FilledElement);
            }

            if (Mouse.ButtonState == MouseInfo.ButtonStateEnum.Left)
            {
                // CurrentIndex = PositionToIndex(Mouse.X, Mouse.Y);
            }

            // Console.WriteLine($"{e.dwButtonState} {e.dwControlKeyState} {e.dwEventFlags} {e.dwMousePosition.X} {e.dwMousePosition.Y}");
        }

        void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e) => RefreshConsole();
    }
}
