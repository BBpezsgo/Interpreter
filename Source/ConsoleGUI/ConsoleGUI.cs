using Microsoft.Win32.SafeHandles;

using System;
using System.Collections.Generic;

namespace ConsoleGUI
{
    using ConsoleLib;

    using System.Timers;

    using static Core;

    internal class ConsoleGUI
    {
        const int TIMER_RESIZE_ELEMENTS = 1000;
        const int TIMER_AUTO_REFRESH_CONSOLE = 500;
        const int TIMER_REFRESH_CONSOLE = 100;

        internal static Character NullCharacter => new()
        {
            Char = ' ',
            Color = CharColors.BgBlack
        };

        internal List<IElement> Elements = new();
        internal IElement FilledElement = null;

        readonly SafeFileHandle ConsoleHandle;
        readonly bool DebugLogs;
        readonly Timer TimerRefreshConsole;
        readonly Timer TimerAutoRefreshConsole;
        readonly Timer TimerRefreshSizes;
        readonly Timer TimerOnStart;

        short Width;
        short Height;
        CharInfo[] ConsoleBuffer;
        SmallRect ConsoleRect;
        Position MousePosition;

        bool ResizeElements;
        internal bool NextRefreshConsole;

        internal static ConsoleGUI Instance = null;

        void Log(string message)
        {
            if (!DebugLogs) return;
            System.Diagnostics.Debug.WriteLine(message);
        }

        internal ConsoleGUI(bool DebugLogs = false)
        {
            Instance = this;
            this.DebugLogs = DebugLogs;

            Log("Setup timers");
            TimerAutoRefreshConsole = new Timer();
            TimerAutoRefreshConsole.Elapsed += (_, _) => RefreshConsole();
            TimerAutoRefreshConsole.Interval = TIMER_AUTO_REFRESH_CONSOLE;
            TimerAutoRefreshConsole.Enabled = true;

            TimerRefreshConsole = new Timer();
            TimerRefreshConsole.Elapsed += (_, _) =>
            {
                if (!NextRefreshConsole) return;
                RefreshConsole();
            };
            TimerRefreshConsole.Interval = TIMER_REFRESH_CONSOLE;
            TimerRefreshConsole.Enabled = true;

            TimerRefreshSizes = new Timer();
            TimerRefreshSizes.Elapsed += (_, _) => ResizeElements = true;
            TimerRefreshSizes.Interval = TIMER_RESIZE_ELEMENTS;
            TimerRefreshSizes.Enabled = true;

            TimerOnStart = new Timer();
            TimerOnStart.Elapsed += (_, _) =>
            {
                TimerOnStart.Stop();
                TimerOnStart.Dispose();

                foreach (var _element in Elements)
                {
                    if (_element is IElementWithEvents element) element.OnStart();
                }
                if (FilledElement is IElementWithEvents elementWithEvents) elementWithEvents?.OnStart();
            };
            TimerOnStart.Interval = 2000;
            TimerOnStart.Enabled = true;

            Log("Setup console");
            SetupConsole();

            ConsoleListener.MouseEvent += MouseEvent;
            ConsoleListener.KeyEvent += KeyEvent;
            ConsoleListener.WindowBufferSizeEvent += WindowBufferSizeEvent;

            Log("Start console listener");
            ConsoleListener.Start();

            Log("Setup console handler");
            ConsoleHandle = CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, System.IO.FileMode.Open, 0, IntPtr.Zero);

            Width = (short)Console.WindowWidth;
            Height = (short)Console.WindowHeight;

            Log("Start");
            Start();
        }

        internal void Destroy()
        {
            Clear();
            TimerAutoRefreshConsole?.Dispose();
            TimerRefreshSizes?.Dispose();
            TimerOnStart?.Dispose();
            Console.Clear();

            Console.WriteLine("Destroy std handler ...");
            ConsoleListener.Stop();
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

            ConsoleBuffer = new CharInfo[Width * Height];
            for (int i = 0; i < ConsoleBuffer.Length; i++)
            {
                ConsoleBuffer[i].Char.UnicodeChar = ' ';
            }
            ConsoleRect = new SmallRect() { Left = 0, Top = 0, Right = Width, Bottom = Height };
        }

        private void WindowBufferSizeEvent(WindowBufferSizeEvent e) => ResizeElements = true;

        void RefreshElementsSize(bool Force = false)
        {
            if (ResizeElements || Force)
            {
                Width = (short)Console.WindowWidth;
                Height = (short)Console.WindowHeight;

                Clear();

                ResizeElements = false;
                foreach (var Element in Elements) Element.RefreshSize();
                if (FilledElement != null)
                {
                    FilledElement.Rect = new System.Drawing.Rectangle(0, 0, Width - 1, Height - 1);
                    FilledElement.RefreshSize();
                }
            }
        }

        void RefreshConsole()
        {
            NextRefreshConsole = false;

            if (ConsoleHandle.IsInvalid)
            {
                Log("Console handler is invalid");
                return;
            }

            for (int i = 0; i < ConsoleBuffer.Length; i++)
            {
                ConsoleBuffer[i].Char.UnicodeChar = ' ';
                ConsoleBuffer[i].Attributes = (short)CharColors.BgMagenta;
            }

            try
            {
                RefreshElementsSize();

                if (FilledElement != null)
                {
                    FilledElement?.BeforeDraw();
                    DrawElement(FilledElement, true);
                }
                else
                {
                    Elements.BeforeDraw();
                    foreach (var Element in Elements)
                    {
                        DrawElement(Element);
                    }
                }
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception.ToString());
                return;
            }

            WriteConsole(ref ConsoleRect);
        }

        void DrawElement(IElement Element, bool IsFilled = false)
        {
            for (int x = Element.Rect.Left; x <= Element.Rect.Right; x++)
            {
                for (int y = Element.Rect.Top; y <= Element.Rect.Bottom; y++)
                {
                    int i = y * Width + x;
                    if (ConsoleBuffer.IsOutside(i)) continue;

                    Character chr = IsFilled ? Element.DrawContent(x, y) : Element.DrawContentWithBorders(x, y);
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
                new Coord((short)Console.WindowWidth, (short)Console.WindowHeight),
                new Coord(0, 0),
                ref rect);
        }
        void KeyEvent(KeyEvent e)
        {
            if (!e.KeyDown)
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
                if (Elements[i] is not IElementWithEvents element) continue;
                if (!element.Contains(MousePosition.X, MousePosition.Y)) continue;
                element.OnKeyEvent(e);
                element.BeforeDraw();
                DrawElement(element);
            }
            if (FilledElement is IElementWithEvents elementWithEvents)
            {
                elementWithEvents.OnKeyEvent(e);
                elementWithEvents.BeforeDraw();
                DrawElement(elementWithEvents);
            }
        }

        void MouseEvent(MouseEvent e)
        {
            MousePosition = e.Position;

            for (int i = Elements.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (Elements[i] is not IElementWithEvents element) continue;
                    if (!element.Contains(e.X, e.Y)) continue;
                    element.OnMouseEvent(e);
                    element.BeforeDraw();
                    DrawElement(element);

                    if (element is WindowElement wat) wat.OnMouseEventBase(e);
                }
                catch (Exception exception)
                { System.Diagnostics.Debug.WriteLine(exception.ToString()); }
            }
            if (FilledElement is IElementWithEvents elementWithEvents)
            {
                if (FilledElement is IElementWithSubelements elementWithSubelements)
                {
                    for (int i = 0; i < elementWithSubelements.Elements.Length; i++)
                    {
                        try
                        {
                            if (elementWithSubelements.Elements[i] is not IElementWithEvents element) continue;
                            if (!element.Contains(e.X, e.Y)) continue;
                            element.OnMouseEvent(e);
                        }
                        catch (Exception exception)
                        { System.Diagnostics.Debug.WriteLine(exception.ToString()); }
                    }
                }
                try
                {
                    elementWithEvents.OnMouseEvent(e);
                    elementWithEvents.BeforeDraw();
                    DrawElement(elementWithEvents);
                }
                catch (Exception exception)
                { System.Diagnostics.Debug.WriteLine(exception.ToString()); }
            }
        }
    }
}
