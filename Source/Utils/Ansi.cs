namespace LanguageCore;

public static class Ansi
{
    public const char ESC = '\x1B';
    public const char CSI = '[';

    const string _ESC = "\x1B";
    const string _CSI = "[";

    public const string Reset = $"{_ESC}{_CSI}0m";

    #region Colors

    public const int ForegroundBlack = 30;
    public const int ForegroundRed = 31;
    public const int ForegroundGreen = 32;
    public const int ForegroundYellow = 33;
    public const int ForegroundBlue = 34;
    public const int ForegroundMagenta = 35;
    public const int ForegroundCyan = 36;
    public const int ForegroundWhite = 37;
    public const int ForegroundExtended = 38;
    public const int ForegroundDefault = 39;

    public const int BackgroundBlack = 40;
    public const int BackgroundRed = 41;
    public const int BackgroundGreen = 42;
    public const int BackgroundYellow = 43;
    public const int BackgroundBlue = 44;
    public const int BackgroundMagenta = 45;
    public const int BackgroundCyan = 46;
    public const int BackgroundWhite = 47;
    public const int BackgroundExtended = 48;
    public const int BackgroundDefault = 49;

    public const int BrightForegroundBlack = 90;
    public const int BrightForegroundRed = 91;
    public const int BrightForegroundGreen = 92;
    public const int BrightForegroundYellow = 93;
    public const int BrightForegroundBlue = 94;
    public const int BrightForegroundMagenta = 95;
    public const int BrightForegroundCyan = 96;
    public const int BrightForegroundWhite = 97;

    public const int BrightBackgroundBlack = 100;
    public const int BrightBackgroundRed = 101;
    public const int BrightBackgroundGreen = 102;
    public const int BrightBackgroundYellow = 103;
    public const int BrightBackgroundBlue = 104;
    public const int BrightBackgroundMagenta = 105;
    public const int BrightBackgroundCyan = 106;
    public const int BrightBackgroundWhite = 107;

    #endregion

    public static StringBuilder SetGraphics(this StringBuilder builder, params uint[] modes)
    {
        builder.Append(ESC);
        builder.Append(CSI);
        builder.AppendJoin(';', modes);
        builder.Append('m');
        return builder;
    }

    public static StringBuilder SetGraphics(this StringBuilder builder, uint mode)
    {
        builder.Append(ESC);
        builder.Append(CSI);
        builder.Append(mode);
        builder.Append('m');
        return builder;
    }

    public static StringBuilder SetForegroundColor(this StringBuilder builder, int r, int g, int b)
    {
        builder.Append(ESC);
        builder.Append(CSI);
        builder.Append('3');
        builder.Append('8');
        builder.Append(';');
        builder.Append('2');
        builder.Append(';');
        builder.Append(r);
        builder.Append(';');
        builder.Append(g);
        builder.Append(';');
        builder.Append(b);
        builder.Append('m');
        return builder;
    }

    public static StringBuilder SetBackgroundColor(this StringBuilder builder, int r, int g, int b)
    {
        builder.Append(ESC);
        builder.Append(CSI);
        builder.Append('4');
        builder.Append('8');
        builder.Append(';');
        builder.Append('2');
        builder.Append(';');
        builder.Append(r);
        builder.Append(';');
        builder.Append(g);
        builder.Append(';');
        builder.Append(b);
        builder.Append('m');
        return builder;
    }

    public static StringBuilder SetForegroundColor(this StringBuilder builder, byte colorCode)
    {
        builder.Append(ESC);
        builder.Append(CSI);
        builder.Append('3');
        builder.Append('8');
        builder.Append(';');
        builder.Append('5');
        builder.Append(';');
        builder.Append(colorCode);
        builder.Append('m');
        return builder;
    }

    public static StringBuilder SetBackgroundColor(this StringBuilder builder, byte colorCode)
    {
        builder.Append(ESC);
        builder.Append(CSI);
        builder.Append('4');
        builder.Append('8');
        builder.Append(';');
        builder.Append('5');
        builder.Append(';');
        builder.Append(colorCode);
        builder.Append('m');
        return builder;
    }

    public static StringBuilder ResetStyle(this StringBuilder builder)
    {
        builder.Append(Reset);
        return builder;
    }

    public static string StyleText(int code, string text) => $"{ESC}{CSI}{code}m{text}{Reset}";
    public static string Style(int code) => $"{ESC}{CSI}{code}m";
}
