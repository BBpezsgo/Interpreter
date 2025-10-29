using System.Diagnostics;

namespace LanguageCore.TUI;

class Renderer
{
    AnsiBuffer Buffer;

    public void Render(Element root)
    {
        int w;
        int h;

        if (Console.IsOutputRedirected)
        {
            w = 50;
            h = 50;
        }
        else
        {
            w = Console.WindowWidth;
            h = Console.WindowHeight;
        }

        if (Buffer.Width != w || Buffer.Height != h)
        {
            Buffer = new AnsiBuffer(new AnsiCharacter[w * h], w, h);
        }

        root.Render(new AnsiBufferSlice(Buffer, Buffer.Width, Buffer.Height, 0, 0));

        Console.Write("\e[H");
        int currentForeground = 0;
        int currentBackground = 0;
        for (int i = 0; i < Buffer.Buffer.Length; i++)
        {
            AnsiCharacter c = Buffer.Buffer[i];
            if (c.Char == '\0') c = ' ';
            int fg = c.Foreground == AnsiColor.Default ? 0 : (int)c.Foreground;
            int bg = c.Background == AnsiColor.Default ? 0 : (int)c.Background + 10;
            if (fg == 0 || bg == 0)
            {
                Console.Write($"\e[1;0m");
                currentForeground = 0;
                currentBackground = 0;
            }

            if (currentForeground != fg && currentBackground != bg)
            {
                Console.Write($"\e[2;{fg};{bg}m");
                currentForeground = fg;
                currentBackground = bg;
            }
            else if (currentForeground != fg)
            {
                Console.Write($"\e[1;{fg}m");
                currentForeground = fg;
            }
            else if (currentBackground != bg)
            {
                Console.Write($"\e[1;{bg}m");
                currentBackground = bg;
            }
            Console.Write(c.Char);
        }
    }
}
