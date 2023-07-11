using ConsoleDrawer;

using Microsoft.Win32.SafeHandles;

using System;
using System.Collections.Generic;

namespace ConsoleGUI
{
    internal delegate void MainThreadTimerCallback();
    internal delegate void MainThreadEventCallback<T>(T e);

    internal static class MainThreadExtensions
    {
        public static void Tick(this IMainThreadThing[] mainThreadThings, double deltaTime)
        {
            for (int i = 0; i < mainThreadThings.Length; i++)
            { mainThreadThings[i]?.Tick(deltaTime); }
        }
    }

    public interface IMainThreadThing
    {
        public void Tick(double deltaTime);
    }

    internal class MainThreadTimer : IMainThreadThing
    {
        double Timer;
        readonly double Interval;

        internal bool Enabled;
        internal event MainThreadTimerCallback Elapsed;

        public MainThreadTimer(double interval)
        {
            this.Interval = interval;
            this.Enabled = false;
        }

        public void Tick(double deltaTime)
        {
            if (!this.Enabled) return;

            this.Timer += deltaTime;
            if (this.Timer >= this.Interval)
            {
                this.Elapsed?.Invoke();
            }
        }

        public void Stop()
        {
            this.Timer = 0d;
            this.Enabled = false;
        }
    }

    internal class MainThreadEvents<T> : IMainThreadThing
    {
        readonly Queue<T> EventQueue;

        public int MaxCallbacksPerTick = int.MaxValue;
        public event MainThreadEventCallback<T> Callback;

        public MainThreadEvents()
        {
            EventQueue = new Queue<T>();
        }

        public MainThreadEvents(int maxCallbacksPerTick) : this()
        {
            MaxCallbacksPerTick = maxCallbacksPerTick;
        }

        public void Tick(double _)
        {
            int limit = MaxCallbacksPerTick;
            while (EventQueue.Count > 0 && limit-- > 0)
            {
                T parameter = EventQueue.Dequeue();
                Callback?.Invoke(parameter);
            }
        }

        public void Clear() => EventQueue.Clear();
        public void Add(T parameter) => EventQueue.Enqueue(parameter);
    }

    internal class ConsoleGUI
    {
        const int TIMER_RESIZE_ELEMENTS = 1000;
        const int TIMER_AUTO_REFRESH_CONSOLE = 500;
        const int TIMER_REFRESH_CONSOLE = 100;

        internal static Character NullCharacter => new()
        {
            Char = ' ',
            ForegroundColor = ForegroundColor.Black,
            BackgroundColor = BackgroundColor.Black,
        };

        internal IElement[] Elements = Array.Empty<IElement>();
        internal IElement FilledElement = null;

        readonly SafeFileHandle ConsoleHandle;
        readonly bool DebugLogs;
        readonly MainThreadTimer TimerRefreshConsole;
        readonly MainThreadTimer TimerAutoRefreshConsole;
        readonly MainThreadTimer TimerRefreshSizes;
        readonly MainThreadTimer TimerOnStart;

        readonly MainThreadEvents<KeyEvent> KeyEvents;
        readonly MainThreadEvents<MouseEvent> MouseEvents;

        readonly IMainThreadThing[] MainThreadThings;

        short Width;
        short Height;
        CharInfo[] ConsoleBuffer;
        SmallRect ConsoleRect;
        Coord MousePosition;

        bool ResizeElements;
        internal bool NextRefreshConsole;

        internal static ConsoleGUI Instance = null;

        double LastTick;
        internal bool Destroyed { get; private set; }

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

            TimerAutoRefreshConsole = new MainThreadTimer(TIMER_AUTO_REFRESH_CONSOLE);
            TimerAutoRefreshConsole.Elapsed += RefreshConsole;
            TimerAutoRefreshConsole.Enabled = true;

            TimerRefreshConsole = new MainThreadTimer(TIMER_REFRESH_CONSOLE);
            TimerRefreshConsole.Elapsed += () =>
            {
                if (!NextRefreshConsole) return;
                RefreshConsole();
            };
            TimerRefreshConsole.Enabled = true;

            TimerRefreshSizes = new MainThreadTimer(TIMER_RESIZE_ELEMENTS);
            TimerRefreshSizes.Elapsed += () => ResizeElements = true;
            TimerRefreshSizes.Enabled = true;

            TimerOnStart = new MainThreadTimer(2000);
            TimerOnStart.Elapsed += () =>
            {
                TimerOnStart.Stop();

                foreach (var _element in Elements)
                {
                    if (_element is IElementWithEvents element) element.OnStart();
                }
                if (FilledElement is IElementWithEvents elementWithEvents) elementWithEvents?.OnStart();
            };
            TimerOnStart.Enabled = true;

            this.LastTick = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;

            this.KeyEvents = new MainThreadEvents<KeyEvent>(32);
            this.MouseEvents = new MainThreadEvents<MouseEvent>(8);

            this.KeyEvents.Callback += KeyEvent;
            this.MouseEvents.Callback += MouseEvent;

            this.MainThreadThings = new IMainThreadThing[]
            {
                this.TimerAutoRefreshConsole,
                this.TimerRefreshConsole,
                this.TimerRefreshSizes,
                this.TimerOnStart,
                this.KeyEvents,
                this.MouseEvents,
            };

            Log("Setup console");
            ConsoleHandler.SetupConsole();

            ConsoleListener.MouseEvent += MouseEventThread;
            ConsoleListener.KeyEvent += KeyEventThread;
            ConsoleListener.WindowBufferSizeEvent += WindowBufferSizeEvent;

            Log("Start console listener");
            ConsoleListener.Start();

            Log("Setup console handler");
            ConsoleHandle = Kernel32.CreateFile("CONOUT$", 0x40000000, 2, IntPtr.Zero, System.IO.FileMode.Open, 0, IntPtr.Zero);

            Width = (short)Console.WindowWidth;
            Height = (short)Console.WindowHeight;

            Log("Start");
            Start();
        }

        internal void Destroy()
        {
            if (Destroyed) return;
            Destroyed = true;
            TimerAutoRefreshConsole?.Stop();
            TimerRefreshSizes?.Stop();
            TimerOnStart?.Stop();

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

        internal void Tick()
        {
            double now = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
            double deltaTime = now - this.LastTick;
            this.LastTick = now;

            this.MainThreadThings.Tick(deltaTime);

            if (FilledElement != null)
            {
                FilledElement?.Tick(deltaTime);
            }
            else
            {
                Elements.Tick(deltaTime);
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
                ConsoleBuffer[i].Attributes = (short)BackgroundColor.Magenta;
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
                    ConsoleBuffer[i].Attributes = (short)((short)chr.ForegroundColor | (short)chr.BackgroundColor);
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

            Kernel32.WriteConsoleOutputW(ConsoleHandle, ConsoleBuffer,
                new Coord((short)Console.WindowWidth, (short)Console.WindowHeight),
                new Coord(0, 0),
                ref rect);
        }

        void KeyEventThread(KeyEvent e) => KeyEvents.Add(e);
        void MouseEventThread(MouseEvent e) => MouseEvents.Add(e);

        void WindowBufferSizeEvent(WindowBufferSizeEvent e) => ResizeElements = true;
        void KeyEvent(KeyEvent e)
        {
            if (!e.KeyDown)
            {
                if (e.AsciiChar == 27)
                {
                    for (int i = Elements.Length - 1; i >= 0; i--)
                    {
                        if (Elements[i] is not IElementWithEvents element) continue;
                        element.OnDestroy();
                    }

                    if (FilledElement is IElementWithEvents elementWithEvents)
                    {
                        elementWithEvents.OnDestroy();
                    }

                    ConsoleListener.Stop();
                    this.Destroy();
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

            {
                for (int i = Elements.Length - 1; i >= 0; i--)
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
        }
        void MouseEvent(MouseEvent e)
        {
            MousePosition = e.Position;

            for (int i = Elements.Length - 1; i >= 0; i--)
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
