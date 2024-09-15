namespace LanguageCore.ASM;

[ExcludeFromCodeCoverage]
public class TextSectionBuilder : SectionBuilder
{
    readonly List<string> Labels;
    public readonly HashSet<string> Imports;

    public TextSectionBuilder() : base()
    {
        Labels = new List<string>();
        Imports = new HashSet<string>();
    }

    bool HasLabel(string dataLabel)
    {
        for (int i = 0; i < Labels.Count; i++)
        {
            if (string.Equals(Labels[i], dataLabel, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public string NewLabel(string? name = null, int labelLength = 16)
    {
        string label = AssemblyCode.GenerateLabel("t_" + name + "_", labelLength, HasLabel);
        Labels.Add(label);
        return label;
    }

    public void AppendLabel(string label)
    {
        AppendText(' ', Indent);
        AppendTextLine($"{label}:");
    }

    public static string StringifyInstruction(OpCode instruction) => (instruction switch
    {
        OpCode.Move => "mov",
        OpCode.Push => "push",
        OpCode.Pop => "pop",
        OpCode.LoadEA => "lea",

        OpCode.MathAdd => "add",
        OpCode.MathSub => "sub",
        OpCode.Compare => "cmp",
        OpCode.MathMult => "mul",
        OpCode.MathDiv => "div",
        OpCode.IMathMult => "imul",
        OpCode.IMathDiv => "idiv",
        OpCode.Test => "test",
        OpCode.BitsAND => "and",
        OpCode.BitsXOR => "xor",
        OpCode.BitsOR => "or",
        OpCode.BitsShiftRight => "shr",
        OpCode.BitsShiftLeft => "shl",

        OpCode.SystemCall => "syscall",
        OpCode.Call => "call",
        OpCode.Return => "ret",

        OpCode.Jump => "jmp",
        OpCode.JumpIfZero => "jz",
        OpCode.JumpIfNotEQ => "jne",
        OpCode.JumpIfGEQ => "jge",
        OpCode.JumpIfG => "jg",
        OpCode.JumpIfLEQ => "jle",
        OpCode.JumpIfL => "jl",
        OpCode.JumpIfEQ => "je",

        OpCode.ConvertByteToWord => "cbw",
        OpCode.ConvertWordToDoubleword => "cwd",
        OpCode.Halt => "hlt",

        _ => throw new UnreachableException(),
    }).ToUpperInvariant();

    public static string StringifyInstruction(Runtime.Opcode instruction) => (instruction switch
    {
        Runtime.Opcode.Move => "mov",
        Runtime.Opcode.Push => "push",
        Runtime.Opcode.Pop8 => "pop",
        Runtime.Opcode.Pop16 => "pop",
        Runtime.Opcode.Pop32 => "pop",
        Runtime.Opcode.PopTo8 => "pop",
        Runtime.Opcode.PopTo16 => "pop",
        Runtime.Opcode.PopTo32 => "pop",
        Runtime.Opcode.PopTo64 => "pop",

        Runtime.Opcode.MathAdd => "add",
        Runtime.Opcode.MathSub => "sub",
        Runtime.Opcode.Compare => "cmp",
        Runtime.Opcode.MathMult => "mul",
        Runtime.Opcode.MathDiv => "div",
        Runtime.Opcode.BitsAND => "and",
        Runtime.Opcode.BitsXOR => "xor",
        Runtime.Opcode.BitsOR => "or",
        Runtime.Opcode.BitsShiftRight => "shr",
        Runtime.Opcode.BitsShiftLeft => "shl",

        Runtime.Opcode.Call => "call",
        Runtime.Opcode.Return => "ret",
        Runtime.Opcode.Exit => "hlt",

        Runtime.Opcode.Jump => "jmp",
        Runtime.Opcode.JumpIfEqual => "je",
        Runtime.Opcode.JumpIfNotEqual => "jne",
        Runtime.Opcode.JumpIfGreaterOrEqual => "jge",
        Runtime.Opcode.JumpIfGreater => "jg",
        Runtime.Opcode.JumpIfLessOrEqual => "jle",
        Runtime.Opcode.JumpIfLess => "jl",

        _ => throw new UnreachableException(),
    }).ToUpperInvariant();

    void AppendInstructionNoEOL(OpCode keyword)
    {
        AppendText(' ', Indent);
        AppendText(StringifyInstruction(keyword));
    }

    public void AppendInstruction(OpCode keyword)
    {
        AppendInstructionNoEOL(keyword);
        AppendText(Environment.NewLine);
    }

    public void AppendInstructionNoEOL(OpCode keyword, params ReadOnlySpan<string> operands)
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

    public void AppendInstruction(OpCode keyword, params ReadOnlySpan<string> operands)
    {
        AppendInstructionNoEOL(keyword, operands);
        AppendText(Environment.NewLine);
    }

    public void AppendInstructionNoEOL(OpCode keyword, InstructionOperand parameterA = default, InstructionOperand parameterB = default)
    {
        AppendInstructionNoEOL(new Instruction(keyword, parameterA, parameterB));
    }

    public void AppendInstruction(OpCode keyword, InstructionOperand parameterA = default, InstructionOperand parameterB = default)
    {
        AppendInstruction(new Instruction(keyword, parameterA, parameterB));
    }

    public void AppendInstruction(Instruction instruction)
    {
        AppendText(' ', Indent);
        AppendText(instruction.ToString());
        AppendText(Environment.NewLine);
    }

    public void AppendInstructionNoEOL(Instruction instruction)
    {
        AppendText(' ', Indent);
        AppendText(instruction.ToString());
    }

    public void Import(string label)
    {
        Imports.Add(label);
    }

    #region Call_cdecl

    /// <inheritdoc cref="Call_cdecl(string, int, ReadOnlySpan{string?})"/>
    public void Call_cdecl(string label, int parametersSize, ReadOnlySpan<InstructionOperand> parameters)
    {
        string[] parametersString = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        { parametersString[i] = parameters[i].ToString(); }
        Call_cdecl(label, parametersSize, parametersString);
    }

    /// <summary>
    /// Return value: <see cref="Registers.AX"/>
    /// </summary>
    void Call_cdecl(string label, int parametersSize, ReadOnlySpan<string?> parameters)
    {
        if (label.StartsWith('_') && label.Contains('@'))
        { Import(label); }

        if (parameters.Length > 0)
        {
            AppendCommentLine("Arguments (in reverse)");

            for (int i = parameters.Length - 1; i >= 0; i--)
            {
                AppendInstruction(OpCode.Push, parameters[i] ?? throw new ArgumentNullException(nameof(parameters), $"The {i}th parameter is null"));
            }
        }

        AppendInstruction(OpCode.Call, label);

        AppendCommentLine("Clear arguments");

        AppendInstructionNoEOL(OpCode.MathAdd, Intel.Register.SP, parametersSize);
        AppendComment("Remove call arguments from frame");
        AppendText(Environment.NewLine);
    }

    /// <inheritdoc cref="Call_cdecl(string, int, ReadOnlySpan{string?})"/>
    public void Call_cdecl(string label, int parametersSize)
    {
        if (label.StartsWith('_') && label.Contains('@'))
        { Import(label); }

        AppendInstruction(OpCode.Call, label);

        AppendCommentLine("Clear arguments");

        AppendInstructionNoEOL(OpCode.MathAdd, Intel.Register.SP, parametersSize);
        AppendComment("Remove call arguments from frame");
        AppendText(Environment.NewLine);
    }

    #endregion

    #region Call_stdcall

    /// <inheritdoc cref="Call_stdcall(string, ReadOnlySpan{string?})"/>
    public void Call_stdcall(string label, params ReadOnlySpan<InstructionOperand> parameters)
    {
        string[] parametersString = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        { parametersString[i] = parameters[i].ToString(); }
        Call_stdcall(label, parametersString);
    }

    /// <summary>
    /// <para>
    /// Return value: <see cref="Registers.AX"/>
    /// </para>
    /// </summary>
    void Call_stdcall(string label, params ReadOnlySpan<string?> parameters)
    {
        if (label.StartsWith('_') && label.Contains('@'))
        { Import(label); }

        AppendCommentLine("Arguments (in reverse)");

        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            AppendInstruction(OpCode.Push, parameters[i] ?? throw new ArgumentNullException(nameof(parameters), $"The {i}th parameter is null"));
        }

        AppendInstruction(OpCode.Call, label);
    }

    /// <inheritdoc cref="Call_stdcall(string, ReadOnlySpan{string?})"/>
    public void Call_stdcall(string label)
    {
        if (label.StartsWith('_') && label.Contains('@'))
        { Import(label); }

        AppendInstruction(OpCode.Call, label);
    }

    #endregion
}
