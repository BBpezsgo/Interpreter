using System.Runtime.Versioning;
using Win32.Console;

namespace ConsoleGUI;

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

internal sealed class MainThreadTimer : IMainThreadThing
{
    double Timer;
    readonly double Interval;

    public bool Enabled;
    public event MainThreadTimerCallback? Elapsed;

    public MainThreadTimer(double interval)
    {
        Interval = interval;
        Enabled = false;
    }

    public void Tick(double deltaTime)
    {
        if (!Enabled) return;

        Timer += deltaTime;
        if (Timer >= Interval)
        {
            Elapsed?.Invoke();
        }
    }

    public void Stop()
    {
        Timer = 0d;
        Enabled = false;
    }
}

internal sealed class MainThreadEvents<T> : IMainThreadThing
{
    readonly Queue<T> EventQueue;

    public int MaxCallbacksPerTick = int.MaxValue;
    public event MainThreadEventCallback<T>? Callback;

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

[SupportedOSPlatform("windows")]
internal sealed class ConsoleGUI : ConsoleRenderer, IDisposable
{
    const int TIMER_RESIZE_ELEMENTS = 1000;
    const int TIMER_AUTO_REFRESH_CONSOLE = 1000;
    const int TIMER_REFRESH_CONSOLE = 100;

    public static ConsoleGUI? Instance { get; set; }

    public Element[] Elements { get; set; } = Array.Empty<Element>();
    public Element? FilledElement { get; set; }
    public bool NextRefreshConsole { get; set; }
    public bool IsDisposed { get; private set; }

    readonly MainThreadTimer TimerRefreshConsole;
    readonly MainThreadTimer TimerAutoRefreshConsole;
    readonly MainThreadTimer TimerRefreshSizes;
    readonly MainThreadTimer TimerOnStart;

    readonly MainThreadEvents<KeyEvent> KeyEvents;
    readonly MainThreadEvents<MouseEvent> MouseEvents;

    readonly IMainThreadThing[] MainThreadThings;

    bool ResizeElements;

    double LastTick;

    public ConsoleGUI()
    {
        Instance = this;

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

            foreach (Element element in Elements)
            { element.OnStart(); }
            FilledElement?.OnStart();
        };
        TimerOnStart.Enabled = true;

        LastTick = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;

        KeyEvents = new MainThreadEvents<KeyEvent>(32);
        MouseEvents = new MainThreadEvents<MouseEvent>(8);

        KeyEvents.Callback += KeyEvent;
        MouseEvents.Callback += MouseEvent;

        MainThreadThings = new IMainThreadThing[]
        {
            TimerAutoRefreshConsole,
            TimerRefreshConsole,
            TimerRefreshSizes,
            TimerOnStart,
            KeyEvents,
            MouseEvents,
        };

        Terminal.Setup();

        ConsoleListener.MouseEvent += MouseEventThread;
        ConsoleListener.KeyEvent += KeyEventThread;
        ConsoleListener.WindowBufferSizeEvent += WindowBufferSizeEvent;

        ConsoleListener.Start();

        Clear();
        RefreshElementsSize(true);
        RefreshConsole();
    }

    void RefreshElementsSize(bool Force = false)
    {
        if (ResizeElements || Force)
        {
            RefreshBufferSize();

            Clear();

            ResizeElements = false;
            foreach (Element Element in Elements) Element.RefreshSize();
            if (FilledElement != null)
            {
                FilledElement.Rect = new System.Drawing.Rectangle(0, 0, Width - 1, Height - 1);
                FilledElement.RefreshSize();
            }
        }
    }

    public void Tick()
    {
        double now = DateTime.UtcNow.TimeOfDay.TotalMilliseconds;
        double deltaTime = now - LastTick;
        LastTick = now;

        MainThreadThings.Tick(deltaTime);

        if (FilledElement != null)
        {
            FilledElement.Tick(deltaTime);
        }
        else
        {
            Elements.Tick(deltaTime);
        }
    }

    public override void Render()
    {
        NextRefreshConsole = false;

        Fill(new ConsoleChar((char)0x2591, CharColor.Gray, CharColor.Black));

        try
        {
            RefreshElementsSize();

            if (FilledElement != null)
            {
                FilledElement.BeforeDraw();
                DrawElement(FilledElement, true);
            }
            else
            {
                Elements.BeforeDraw();
                foreach (Element Element in Elements)
                {
                    DrawElement(Element);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.ToString());
            return;
        }

        base.Render();
    }

    void RefreshConsole()
    {
        ConsoleMouse.Tick();
        ConsoleKeyboard.Tick();
        Render();
    }

    void DrawElement(Element element, bool isFilled = false)
    {
        for (int x = element.Rect.Left; x <= element.Rect.Right; x++)
        {
            for (int y = element.Rect.Top; y <= element.Rect.Bottom; y++)
            {
                if (!IsVisible(x, y)) continue;

                this[x, y] = isFilled ? element.DrawContent(x, y) : element.DrawContentWithBorders(x, y);
            }
        }
    }

    void KeyEventThread(KeyEvent e) => KeyEvents.Add(e);
    void MouseEventThread(MouseEvent e) => MouseEvents.Add(e);

    void WindowBufferSizeEvent(WindowBufferSizeEvent e) => ResizeElements = true;
    void KeyEvent(KeyEvent e)
    {
        ConsoleKeyboard.Feed(e);

        if (e.IsDown == 0 &&
            e.VirtualKeyCode == Win32.VirtualKeyCode.Escape)
        {
            for (int i = Elements.Length - 1; i >= 0; i--)
            { Elements[i].OnDestroy(); }

            FilledElement?.OnDestroy();

            Dispose();
            return;
        }

        for (int i = Elements.Length - 1; i >= 0; i--)
        {
            Element element = Elements[i];
            if (!element.Contains(ConsoleMouse.RecordedConsolePosition)) continue;
            element.OnKeyEvent(e);
        }

        FilledElement?.OnKeyEvent(e);
    }
    void MouseEvent(MouseEvent e)
    {
        ConsoleMouse.Feed(e);

        for (int i = Elements.Length - 1; i >= 0; i--)
        {
            Element element = Elements[i];
            if (!element.Contains(e.MousePosition.X, e.MousePosition.Y)) continue;
            element.OnMouseEvent(e);

            if (element is WindowElement windowElement) windowElement.OnMouseEventBase(e);
        }

        if (FilledElement is not null)
        {
            if (FilledElement is IElementWithSubelements elementWithSubelements)
            {
                for (int i = 0; i < elementWithSubelements.Elements.Length; i++)
                {
                    try
                    {
                        if (elementWithSubelements.Elements[i] is not Element element) continue;
                        if (!element.Contains(e.MousePosition.X, e.MousePosition.Y)) continue;
                        element.OnMouseEvent(e);
                    }
                    catch (Exception exception)
                    { Debug.WriteLine(exception.ToString()); }
                }
            }

            FilledElement.OnMouseEvent(e);
        }
    }

    private void Dispose(bool disposing)
    {
        if (IsDisposed) return;

        if (disposing)
        {
            TimerAutoRefreshConsole?.Stop();
            TimerRefreshSizes?.Stop();
            TimerOnStart?.Stop();

            ConsoleListener.Stop();

            Terminal.Restore();
        }

        IsDisposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
