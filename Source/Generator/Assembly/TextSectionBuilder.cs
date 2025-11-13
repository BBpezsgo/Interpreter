namespace LanguageCore.Native.Generator;

[ExcludeFromCodeCoverage]
public class TextSectionBuilder : SectionBuilder
{
    public readonly HashSet<string> Imports;

    public TextSectionBuilder() : base()
    {
        Imports = new HashSet<string>();
    }

    public void AppendLabel(string label)
    {
        AppendText(' ', Indent);
        AppendTextLine($"{label}:");
    }

    public static string StringifyInstruction(Runtime.Opcode instruction) => (instruction switch
    {
        Runtime.Opcode.Move => "mov",
        Runtime.Opcode.Push => "push",
        Runtime.Opcode.PopTo8 => "pop",
        Runtime.Opcode.PopTo16 => "pop",
        Runtime.Opcode.PopTo32 => "pop",
        Runtime.Opcode.PopTo64 => "pop",

        Runtime.Opcode.MathAdd => "add",
        Runtime.Opcode.MathSub => "sub",
        Runtime.Opcode.Compare => "cmp",
        Runtime.Opcode.CompareF => "cmp",
        Runtime.Opcode.MathMultS => "imul",
        Runtime.Opcode.MathDivS => "idiv",
        Runtime.Opcode.MathMultU => "mul",
        Runtime.Opcode.MathDivU => "div",
        Runtime.Opcode.BitsAND => "and",
        Runtime.Opcode.BitsXOR => "xor",
        Runtime.Opcode.BitsOR => "or",
        Runtime.Opcode.BitsShiftRight => "shr",
        Runtime.Opcode.BitsShiftLeft => "shl",
        Runtime.Opcode.LogicOR => "or",
        Runtime.Opcode.LogicAND => "and",
        Runtime.Opcode.BitsNOT => "not",

        Runtime.Opcode.MathModS => throw new NotImplementedException(),
        Runtime.Opcode.MathModU => throw new NotImplementedException(),
        Runtime.Opcode.FMathAdd => throw new NotImplementedException(),
        Runtime.Opcode.FMathSub => throw new NotImplementedException(),
        Runtime.Opcode.FMathMult => throw new NotImplementedException(),
        Runtime.Opcode.FMathDiv => throw new NotImplementedException(),
        Runtime.Opcode.FMathMod => throw new NotImplementedException(),

        Runtime.Opcode.Call => "call",
        Runtime.Opcode.Return => "ret",
        Runtime.Opcode.Exit => "hlt",
        Runtime.Opcode.CallExternal => throw new NotImplementedException(),
        Runtime.Opcode.Crash => throw new NotImplementedException(),

        Runtime.Opcode.Jump => "jmp",
        Runtime.Opcode.JumpIfEqual => "je",
        Runtime.Opcode.JumpIfNotEqual => "jne",
        Runtime.Opcode.JumpIfGreaterOrEqualS => throw new NotImplementedException(),
        Runtime.Opcode.JumpIfGreaterS => throw new NotImplementedException(),
        Runtime.Opcode.JumpIfLessOrEqualS => throw new NotImplementedException(),
        Runtime.Opcode.JumpIfLessS => throw new NotImplementedException(),
        Runtime.Opcode.JumpIfGreaterOrEqualU => throw new NotImplementedException(),
        Runtime.Opcode.JumpIfGreaterU => throw new NotImplementedException(),
        Runtime.Opcode.JumpIfLessOrEqualU => throw new NotImplementedException(),
        Runtime.Opcode.JumpIfLessU => throw new NotImplementedException(),

        Runtime.Opcode.NOP => "nop",

        Runtime.Opcode.FTo => throw new NotImplementedException(),
        Runtime.Opcode.FFrom => throw new NotImplementedException(),

        _ => throw new UnreachableException(),
    }).ToUpperInvariant();

    void AppendInstructionNoEOL(string keyword)
    {
        AppendText(' ', Indent);
        AppendText(keyword);
    }

    public void AppendInstruction(string keyword)
    {
        AppendInstructionNoEOL(keyword);
        AppendText(Environment.NewLine);
    }

    public void AppendInstructionNoEOL(string keyword, params string[] operands)
    {
        AppendInstructionNoEOL(keyword);
        if (operands.Length > 0)
        {
            AppendText(' ');
            for (int i = 0; i < operands.Length; i++)
            {
                string operand = operands[i];
                if (i > 0)
                { AppendText(", "); }
                AppendText(operand);
            }
        }
    }

    public void AppendInstruction(string keyword, params string[] operands)
    {
        AppendInstructionNoEOL(keyword, operands);
        AppendText(Environment.NewLine);
    }
}
