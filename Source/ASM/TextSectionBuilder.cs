namespace LanguageCore.ASM;

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

    public static string StringifyInstruction(Instruction instruction) => (instruction switch
    {
        Instruction.Move => "mov",
        Instruction.Push => "push",
        Instruction.Pop => "pop",
        Instruction.LoadEA => "lea",

        Instruction.MathAdd => "add",
        Instruction.MathSub => "sub",
        Instruction.Compare => "cmp",
        Instruction.MathMult => "mul",
        Instruction.MathDiv => "div",
        Instruction.IMathMult => "imul",
        Instruction.IMathDiv => "idiv",
        Instruction.Test => "test",
        Instruction.BitsAND => "and",
        Instruction.BitsXOR => "xor",
        Instruction.BitsOR => "or",
        Instruction.BitsShiftRight => "shr",
        Instruction.BitsShiftLeft => "shl",

        Instruction.Call => "call",
        Instruction.Return => "ret",

        Instruction.Jump => "jmp",
        Instruction.JumpIfZero => "jz",
        Instruction.JumpIfNotEQ => "jne",
        Instruction.JumpIfGEQ => "jge",
        Instruction.JumpIfG => "jg",
        Instruction.JumpIfLEQ => "jle",
        Instruction.JumpIfL => "jl",
        Instruction.JumpIfEQ => "je",

        Instruction.ConvertByteToWord => "cbw",
        Instruction.ConvertWordToDoubleword => "cwd",
        Instruction.Halt => "hlt",

        _ => throw new UnreachableException(),
    }).ToUpperInvariant();

    void AppendInstructionNoEOL(Instruction keyword)
    {
        AppendText(' ', Indent);
        AppendText(StringifyInstruction(keyword));
    }

    public void AppendInstruction(Instruction keyword)
    {
        AppendInstructionNoEOL(keyword);
        AppendText(EOL);
    }

    public void AppendInstructionNoEOL(Instruction keyword, params string[] operands)
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

    public void AppendInstruction(Instruction keyword, params string[] operands)
    {
        AppendInstructionNoEOL(keyword, operands);
        AppendText(EOL);
    }

    public void AppendInstructionNoEOL(Instruction keyword, params InstructionOperand[] operands)
    {
        AppendInstructionNoEOL(keyword);
        if (operands.Length > 0)
        {
            AppendText(' ');
            for (int i = 0; i < operands.Length; i++)
            {
                InstructionOperand operand = operands[i];
                if (i > 0)
                { AppendText(", "); }
                AppendText(operand.ToString());
            }
        }
    }

    public void AppendInstruction(Instruction keyword, params InstructionOperand[] operands)
    {
        AppendInstructionNoEOL(keyword, operands);
        AppendText(EOL);
    }

    public void Import(string label)
    {
        Imports.Add(label);
    }

    #region Call_cdecl

    /// <inheritdoc cref="Call_cdecl(string, int, string?[])"/>
    public void Call_cdecl(string label, int parametersSize, params InstructionOperand[] parameters)
    {
        string[] parametersString = new string[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        { parametersString[i] = parameters[i].ToString(); }
        Call_cdecl(label, parametersSize, parametersString);
    }

    /// <summary>
    /// Return value: <see cref="Registers.AX"/>
    /// </summary>
    public void Call_cdecl(string label, int parametersSize, params string?[] parameters)
    {
        if (label.StartsWith('_') && label.Contains('@'))
        { Import(label); }

        if (parameters.Length > 0)
        {
            AppendCommentLine("Arguments (in reverse)");

            for (int i = parameters.Length - 1; i >= 0; i--)
            {
                AppendInstruction(Instruction.Push, parameters[i] ?? throw new ArgumentNullException(nameof(parameters), $"The {i}th parameter is null"));
            }
        }

        AppendInstruction(Instruction.Call, label);

        AppendCommentLine("Clear arguments");

        AppendInstructionNoEOL(Instruction.MathAdd, Registers.SP, parametersSize);
        AppendComment("Remove call arguments from frame");
        AppendText(EOL);
    }

    /// <inheritdoc cref="Call_cdecl(string, int, string?[])"/>
    public void Call_cdecl(string label, int parametersSize)
    {
        if (label.StartsWith('_') && label.Contains('@'))
        { Import(label); }

        AppendInstruction(Instruction.Call, label);

        AppendCommentLine("Clear arguments");

        AppendInstructionNoEOL(Instruction.MathAdd, Registers.SP, parametersSize);
        AppendComment("Remove call arguments from frame");
        AppendText(EOL);
    }

    #endregion

    #region Call_stdcall

    /// <inheritdoc cref="Call_stdcall(string, string?[])"/>
    public void Call_stdcall(string label, params InstructionOperand[] parameters)
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
    public void Call_stdcall(string label, params string?[] parameters)
    {
        if (label.StartsWith('_') && label.Contains('@'))
        { Import(label); }

        AppendCommentLine("Arguments (in reverse)");

        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            AppendInstruction(Instruction.Push, parameters[i] ?? throw new ArgumentNullException(nameof(parameters), $"The {i}th parameter is null"));
        }

        AppendInstruction(Instruction.Call, label);
    }

    /// <inheritdoc cref="Call_stdcall(string, string?[])"/>
    public void Call_stdcall(string label)
    {
        if (label.StartsWith('_') && label.Contains('@'))
        { Import(label); }

        AppendInstruction(Instruction.Call, label);
    }

    #endregion
}
