using System.Runtime.Versioning;
using Win32;

namespace LanguageCore.Brainfuck;

public partial class InterpreterCompact
{
    [SupportedOSPlatform("windows")]
    protected override void DrawCode(Renderer<ConsoleChar> renderer, Range<int> range, int x, int y, int width)
    {
        for (int i = range.Start; i <= range.End; i++)
        {
            byte bg = (i == _codePointer) ? CharColor.Silver : CharColor.Black;

            string code;

            switch (Code[i].OpCode)
            {
                case OpCodesCompact.POINTER_R: code = ">"; break;
                case OpCodesCompact.POINTER_L: code = "<"; break;
                case OpCodesCompact.ADD: code = "+"; break;
                case OpCodesCompact.SUB: code = "-"; break;
                case OpCodesCompact.BRANCH_START: code = "["; break;
                case OpCodesCompact.BRANCH_END: code = "]"; break;
                case OpCodesCompact.OUT: code = "."; break;
                case OpCodesCompact.IN: code = ","; break;
                case OpCodesCompact.CLEAR: code = "[-]"; break;
                case OpCodesCompact.MOVE:
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

                renderer[x, y] = new ConsoleChar(c, fg, bg);
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
            renderer[x, y] = new ConsoleChar(' ');
            x++;
        }
    }
}
