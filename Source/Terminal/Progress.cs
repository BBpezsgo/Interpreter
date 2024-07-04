namespace LanguageCore;

[ExcludeFromCodeCoverage]
public static class ConsoleProgress
{
    public static bool IsEnabled { get; private set; }
    public static ImmutableArray<char> SpinnerCharacters { get; } = ImmutableArray.Create('-', '\\', '|', '/');

    public static void SetProgramArguments(ProgramArguments arguments) => IsEnabled = arguments.ShowProgress;
}

[ExcludeFromCodeCoverage]
public readonly struct ConsoleSpinner
{
    const double Speed = 5d;

    readonly ImmutableArray<char> _characters;
    readonly double _time;

    public char Current => _characters[(int)((DateTime.UtcNow.TimeOfDay.TotalSeconds - _time) * Speed) % _characters.Length];

    public ConsoleSpinner(ImmutableArray<char> characters)
    {
        _characters = characters;
        _time = DateTime.UtcNow.TimeOfDay.TotalSeconds;
    }
}

[ExcludeFromCodeCoverage]
public struct ConsoleProgressBar : IDisposable
{
    readonly int _line;
    readonly ConsoleColor _color;
    readonly bool _show;
    readonly double _time;

    bool _isFast;
    float _progress;
    float _printedProgress;

    public ConsoleProgressBar(ConsoleColor color, bool show)
    {
        _line = 0;
        _color = color;
        _show = show && ConsoleProgress.IsEnabled;
        _progress = 0f;
        _printedProgress = 0f;
        _time = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        _isFast = true;

        if (!_show) return;

        _line = Console.GetCursorPosition().Top;
        Console.WriteLine();
    }

    public void Print(int iterator, int count) => Print((float)iterator / (float)count);
    public void Print(float progress)
    {
        _progress = progress;
        Print();
    }
    public void Print()
    {
        if (!_show) return;
        if (_isFast)
        {
            if (DateTime.UtcNow.TimeOfDay.TotalSeconds - _time > .2f)
            { _isFast = false; }
            else
            { return; }
        }

        if ((int)(_printedProgress * Console.WindowWidth) == (int)(_progress * Console.WindowWidth))
        { return; }

        (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

        Console.SetCursorPosition(0, _line);

        int width = Console.WindowWidth;
        Console.ForegroundColor = _color;
        for (int i = 0; i < width; i++)
        {
            float v = (float)(i + 1) / (float)width;
            if (v <= _progress)
            { Console.Write('═'); }
            else
            { Console.Write(' '); }
        }
        Console.ResetColor();

        Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top);

        _printedProgress = _progress;
    }

    public readonly void Dispose()
    {
        if (!_show) return;

        (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

        Console.SetCursorPosition(0, _line);

        int width = Console.WindowWidth;
        for (int i = 0; i < width; i++)
        { Console.Write(' '); }

        Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top - 1);
    }
}

[ExcludeFromCodeCoverage]
public struct ConsoleProgressLabel : IDisposable
{
    public string Label { get; set; }

    readonly int _line;
    readonly ConsoleColor _color;
    readonly bool _show;
    readonly ConsoleSpinner _spinner;
    readonly bool _showSpinner;
    readonly double _time;

    bool _isNotFirst;
    bool _isFast;

    public ConsoleProgressLabel(string label, ConsoleColor color, bool show, bool showSpinner = false)
    {
        Label = label;

        _line = 0;
        _color = color;
        _show = show && ConsoleProgress.IsEnabled;
        if (showSpinner)
        {
            _showSpinner = showSpinner;
            _spinner = new ConsoleSpinner(ConsoleProgress.SpinnerCharacters);
        }
        _time = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        _isFast = true;
        _isNotFirst = false;

        if (!_show) return;

        _line = Console.GetCursorPosition().Top;
        Console.WriteLine();
    }

    public void Print()
    {
        if (!_show) return;
        if (_isFast && _isNotFirst)
        {
            if (DateTime.UtcNow.TimeOfDay.TotalSeconds - _time > .2f)
            { _isFast = false; }
            else
            { return; }
        }
        _isNotFirst = true;

        (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

        Console.SetCursorPosition(0, _line);

        int width = Console.WindowWidth;
        Console.ForegroundColor = _color;

        if (_showSpinner && width > 2)
        {
            Console.Write(_spinner.Current);
            Console.Write(' ');
            width -= 2;
        }

        for (int i = 0; i < width; i++)
        {
            if (i < Label.Length)
            { Console.Write(Label[i]); }
            else
            { Console.Write(' '); }
        }
        Console.ResetColor();

        Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top);
    }

    public readonly void Dispose()
    {
        if (!_show) return;

        (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

        Console.SetCursorPosition(0, _line);

        int width = Console.WindowWidth;
        for (int i = 0; i < width; i++)
        { Console.Write(' '); }

        Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top - 1);
    }
}
