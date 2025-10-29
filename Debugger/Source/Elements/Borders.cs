using System.Diagnostics;

namespace LanguageCore.TUI;

class Borders : Element
{
    public Element Content;
    public string? Label;

    [DebuggerStepThrough]
    public Borders(Element content, string? label, ElementSize size) : base(size)
    {
        Content = content;
        Label = label;
    }

    public override void Render(in AnsiBufferSlice buffer)
    {
        if (buffer.Width < 2 || buffer.Height < 2) return;

        for (int x = 1; x < buffer.Width - 1; x++)
        {
            buffer[x, 0] = '─';
            //buffer[x, buffer.Height - 1] = '─';

            if (Label is not null)
            {
                if (x == 2)
                {
                    buffer[x, 0] = '╴';
                }
                else if (x == 3 + Label.Length)
                {
                    buffer[x, 0] = '╶';
                }
                else if (x >= 3 && x < 3 + Label.Length)
                {
                    buffer[x, 0] = Label[x - 3];
                }
            }
        }
        //for (int y = 1; y < buffer.Height - 1; y++)
        //{
        //    buffer[0, y] = '│';
        //    buffer[buffer.Width - 1, y] = '│';
        //}

        buffer[0, 0] = '╶'; //'╭';
        //buffer[0, buffer.Height - 1] = '╰';
        buffer[buffer.Width - 1, 0] = '╴'; //'╮';
        //buffer[buffer.Width - 1, buffer.Height - 1] = '╯';

        Content.Render(new AnsiBufferSlice(buffer, buffer.Width - 0, buffer.Height - 1, 0, 1));
    }
}
