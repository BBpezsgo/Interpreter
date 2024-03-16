namespace LanguageCore.ASM;

using Compiler;
using Runtime;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public readonly struct InstructionOperand
{
    readonly string value;

    InstructionOperand(string value) => this.value = value;

    string GetDebuggerDisplay() => value;
    public override string ToString() => value;

    public static implicit operator InstructionOperand(string v) => new(v);
    public static implicit operator InstructionOperand(int v) => new(v.ToString(CultureInfo.InvariantCulture));
    public static explicit operator InstructionOperand(ValueAddress v)
    {
        StringBuilder result = new();

        if (v.IsReference)
        { throw new NotImplementedException(); }

        switch (v.AddressingMode)
        {
            case AddressingMode.Absolute:
            {
                throw new NotImplementedException();
                // result.Append('[');
                // result.Append((v.Address + 1) * 4);
                // result.Append(']');
                // return new InstructionOperand(result.ToString());
            }
            case AddressingMode.Runtime:
                throw new NotImplementedException();
            case AddressingMode.BasePointerRelative:
            {
                result.Append('[');
                result.Append(Registers.BP);
                result.Append((v.Address < 0) ? '+' : '-');
                result.Append(Math.Abs(v.Address) * 4);
                result.Append(']');
                return new InstructionOperand(result.ToString());
            }
            case AddressingMode.StackRelative:
            {
                result.Append('[');
                result.Append(Registers.SP);
                result.Append((v.Address < 0) ? '+' : '-');
                result.Append(Math.Abs(v.Address) * 4);
                result.Append(']');
                return new InstructionOperand(result.ToString());
            }
            default:
                throw new UnreachableException();
        }
    }
    public static explicit operator InstructionOperand(DataItem v) => v.Type switch
    {
        RuntimeType.Byte => new InstructionOperand(v.VByte.ToString()),
        RuntimeType.Integer => new InstructionOperand(v.VInt.ToString()),
        RuntimeType.Char => new InstructionOperand(((ushort)v.VChar).ToString()),
        RuntimeType.Null => throw new InternalException($"Operand value is null"),
        RuntimeType.Single => throw new NotImplementedException(),
        _ => throw new UnreachableException(),
    };

    public static InstructionOperand Pointer(string register, int offset, string? size = null)
    {
        size = null;
        if (offset < 0) return new InstructionOperand($"{size}[{register}-{-offset}]");
        else if (offset > 0) return new InstructionOperand($"{size}[{register}+{offset}]");
        else return new InstructionOperand($"{size}[{register}]");
    }

    public static InstructionOperand Pointer(int address, string? size = null)
    {
        size = null;
        return new InstructionOperand($"{size}[{address}]");
    }
}
