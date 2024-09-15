using Win32;
using Win32.Console;

namespace LanguageCore.Brainfuck;

public partial class InterpreterCompact
{
    [ExcludeFromCodeCoverage]
    protected override void DrawCode(IOnlySetterRenderer<ConsoleChar> renderer, Range<int> range, int x, int y, int width)
    {
        for (int i = range.Start; i <= range.End; i++)
        {
            byte bg = (i == _codePointer) ? CharColor.Silver : CharColor.Black;

            string code;

            switch (Code[i].OpCode)
            {
                case OpCodesCompact.PointerRight: code = ">"; break;
                case OpCodesCompact.PointerLeft: code = "<"; break;
                case OpCodesCompact.Add: code = "+"; break;
                case OpCodesCompact.Sub: code = "-"; break;
                case OpCodesCompact.BranchStart: code = "["; break;
                case OpCodesCompact.BranchEnd: code = "]"; break;
                case OpCodesCompact.Out: code = "."; break;
                case OpCodesCompact.In: code = ","; break;
                case OpCodesCompact.Break: code = "$"; break;
                case OpCodesCompact.Clear: code = "[-]"; break;
                case OpCodesCompact.Move:
                {
                    StringBuilder result = new();
                    result.Append('(');
                    result.Append('M');

                    if (Code[i].Arg1 != 0) result.Append($"{Code[i].Arg1};");
                    if (Code[i].Arg2 != 0) result.Append($"{Code[i].Arg2};");
                    if (Code[i].Arg3 != 0) result.Append($"{Code[i].Arg3};");
                    if (Code[i].Arg4 != 0) result.Append($"{Code[i].Arg4};");

                    result.Append(')');
                    code = result.ToString();
                    break;
                }
                default: continue;
            }

            for (int x2 = 0; x2 < code.Length; x2++)
            {
                char c = code[x2];

                byte fg = c switch
                {
                    '>' or '<' => CharColor.BrightRed,
                    '+' or '-' => CharColor.BrightBlue,
                    '[' or ']' => CharColor.BrightGreen,
                    '.' or ',' => CharColor.BrightMagenta,
                    _ => CharColor.Silver,
                };

                renderer.Set(x, y, new ConsoleChar(c, fg, bg));
                x++;
                if (x >= width) return;
            }

            if (Code[i].Count != 1)
            {
                renderer.Text(ref x, y, Code[i].Count.ToString(CultureInfo.InvariantCulture), CharColor.BrightYellow, bg);
                if (x >= width) return;
            }
        }

        while (x < width)
        {
            renderer.Set(x, y, new ConsoleChar(' '));
            x++;
        }
    }
}
