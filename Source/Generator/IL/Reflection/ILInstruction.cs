using System.Reflection.Emit;
using System.Reflection;

namespace LanguageCore.IL.Reflection;

public abstract class ILInstruction
{
    public int Offset { get; }
    public OpCode OpCode { get; }

    public ILInstruction(int offset, OpCode opCode)
    {
        Offset = offset;
        OpCode = opCode;
    }

    public abstract override string ToString();

    protected string ToString(string operand) => $"IL_{Offset:x4}: {OpCode.Name,-10} {operand}";
}

public class InlineNoneInstruction : ILInstruction
{
    public InlineNoneInstruction(int offset, OpCode opCode)
        : base(offset, opCode) { }

    public override string ToString() => $"IL_{Offset:x4}: {OpCode.Name,-10}";
}

public class InlineBrTargetInstruction : ILInstruction
{
    public int Delta { get; }
    public int TargetOffset => Offset + Delta + 1 + 4;

    public InlineBrTargetInstruction(int offset, OpCode opCode, int delta)
        : base(offset, opCode)
    {
        Delta = delta;
    }

    public override string ToString() => ToString($"IL_{TargetOffset:x4}");
}

public class InlineLabelInstruction : ILInstruction
{
    public Label Label { get; }

    public InlineLabelInstruction(int offset, OpCode opCode, Label label)
        : base(offset, opCode)
    {
        Label = label;
    }

    public override string ToString() => ToString(Label.Id.ToString());
}

public class InlineLocalInstruction : ILInstruction
{
    public LocalBuilder Local { get; }

    public InlineLocalInstruction(int offset, OpCode opCode, LocalBuilder local)
        : base(offset, opCode)
    {
        Local = local;
    }

    public override string ToString() => ToString(Local.LocalIndex.ToString());
}

public class ShortInlineBrTargetInstruction : ILInstruction
{
    public ShortInlineBrTargetInstruction(int offset, OpCode opCode, sbyte delta)
        : base(offset, opCode)
    {
        Delta = delta;
    }

    public sbyte Delta { get; }
    public int TargetOffset => Offset + Delta + 1 + 1;

    public override string ToString() => ToString($"IL_{TargetOffset:x4}");
}

public class InlineSwitchInstruction : ILInstruction
{
    readonly int[] m_deltas;
    int[]? m_targetOffsets;

    public int[] Deltas => (int[])m_deltas.Clone();
    public int[] TargetOffsets
    {
        get
        {
            if (m_targetOffsets != null) return m_targetOffsets;

            int cases = m_deltas.Length;
            int itself = 1 + 4 + (4 * cases);
            m_targetOffsets = new int[cases];
            for (int i = 0; i < cases; i++)
            {
                m_targetOffsets[i] = Offset + m_deltas[i] + itself;
            }
            return m_targetOffsets;
        }
    }

    public InlineSwitchInstruction(int offset, OpCode opCode, int[] deltas)
        : base(offset, opCode)
    {
        m_deltas = deltas;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        int length = TargetOffsets.Length;
        for (int i = 0; i < length; i++)
        {
            if (i == 0) sb.Append('(');
            else sb.Append(", ");
            sb.Append($"IL_{TargetOffsets[i]:x4}");
        }
        sb.Append(')');
        return ToString(sb.ToString());
    }
}

public class InlineIInstruction : ILInstruction
{
    public int Int32 { get; }

    public InlineIInstruction(int offset, OpCode opCode, int value)
        : base(offset, opCode)
    {
        Int32 = value;
    }

    public override string ToString() => ToString(Int32.ToString());
}

public class InlineI8Instruction : ILInstruction
{
    public long Int64 { get; }

    public InlineI8Instruction(int offset, OpCode opCode, long value)
        : base(offset, opCode)
    {
        Int64 = value;
    }

    public override string ToString() => ToString(Int64.ToString());
}

public class ShortInlineIInstruction : ILInstruction
{
    public byte Byte { get; }

    public ShortInlineIInstruction(int offset, OpCode opCode, byte value)
        : base(offset, opCode)
    {
        Byte = value;
    }

    public override string ToString() => ToString(Byte.ToString());
}

public class InlineRInstruction : ILInstruction
{
    public double Double { get; }

    public InlineRInstruction(int offset, OpCode opCode, double value)
        : base(offset, opCode)
    {
        Double = value;
    }

    public override string ToString() => ToString(Double.ToString());
}

public class ShortInlineRInstruction : ILInstruction
{
    public float Single { get; }

    public ShortInlineRInstruction(int offset, OpCode opCode, float value)
        : base(offset, opCode)
    {
        Single = value;
    }

    public override string ToString() => ToString(Single.ToString());
}

public class InlineFieldInstruction : ILInstruction
{
    readonly ITokenResolver? m_resolver;
    FieldInfo? m_field;

    public FieldInfo Field => m_field ??= m_resolver?.AsField(Token) ?? throw new NullReferenceException();
    public int Token { get; }

    public InlineFieldInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
        : base(offset, opCode)
    {
        m_resolver = resolver;
        Token = token;
    }

    public InlineFieldInstruction(int offset, OpCode opCode, FieldInfo field)
        : base(offset, opCode)
    {
        m_field = field;
    }

    public override string ToString()
    {
        string field;
        try
        {
            field = Field + "/" + Field.DeclaringType;
        }
        catch (Exception ex)
        {
            field = "!" + ex.Message + "!";
        }
        return ToString(field);
    }
}

public class InlineMethodInstruction : ILInstruction
{
    readonly ITokenResolver? m_resolver;
    MethodBase? m_method;

    public MethodBase Method => m_method ??= m_resolver?.AsMethod(Token) ?? throw new NullReferenceException();
    public int Token { get; }

    public InlineMethodInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
        : base(offset, opCode)
    {
        m_resolver = resolver;
        Token = token;
    }

    public InlineMethodInstruction(int offset, OpCode opCode, MethodBase method)
        : base(offset, opCode)
    {
        m_method = method;
    }

    public override string ToString()
    {
        string method;
        try
        {
            method = Method + "/" + Method.DeclaringType;
        }
        catch (Exception ex)
        {
            method = "!" + ex.Message + "!";
        }
        return ToString(method);
    }
}

public class InlineTypeInstruction : ILInstruction
{
    readonly ITokenResolver? m_resolver;
    Type? m_type;

    public Type Type => m_type ??= m_resolver?.AsType(Token) ?? throw new NullReferenceException();
    public int Token { get; }

    public InlineTypeInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
        : base(offset, opCode)
    {
        m_resolver = resolver;
        Token = token;
    }

    public InlineTypeInstruction(int offset, OpCode opCode, Type type)
        : base(offset, opCode)
    {
        m_type = type;
    }

    public override string ToString()
    {
        string type;
        try
        {
            type = Type.ToString();
        }
        catch (Exception ex)
        {
            type = "!" + ex.Message + "!";
        }
        return ToString(type);
    }
}

public class InlineSigInstruction : ILInstruction
{
    readonly ITokenResolver? m_resolver;
    byte[]? m_signature;

    public byte[] Signature => m_signature ??= m_resolver?.AsSignature(Token) ?? throw new NullReferenceException();
    public int Token { get; }

    public InlineSigInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
        : base(offset, opCode)
    {
        m_resolver = resolver;
        Token = token;
    }

    public InlineSigInstruction(int offset, OpCode opCode, byte[] signature)
        : base(offset, opCode)
    {
        m_signature = signature;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append("SIG [");
        for (int i = 0; i < Signature.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Signature[i].ToString("X2"));
        }
        sb.Append(']');
        return ToString(sb.ToString());
    }
}

public class InlineTokInstruction : ILInstruction
{
    readonly ITokenResolver? m_resolver;
    MemberInfo? m_member;

    public MemberInfo Member => m_member ??= m_resolver?.AsMember(Token) ?? throw new NullReferenceException();
    public int Token { get; }

    public InlineTokInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
        : base(offset, opCode)
    {
        m_resolver = resolver;
        Token = token;
    }

    public InlineTokInstruction(int offset, OpCode opCode, MemberInfo memberInfo)
        : base(offset, opCode)
    {
        m_member = memberInfo;
    }

    public override string ToString()
    {
        string member;
        try
        {
            member = Member + "/" + Member.DeclaringType;
        }
        catch (Exception ex)
        {
            member = "!" + ex.Message + "!";
        }
        return ToString(member);
    }
}

public class InlineStringInstruction : ILInstruction
{
    readonly ITokenResolver? m_resolver;
    string? m_string;

    public string String => m_string ??= m_resolver?.AsString(Token) ?? throw new NullReferenceException();
    public int Token { get; }

    public InlineStringInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
        : base(offset, opCode)
    {
        m_resolver = resolver;
        Token = token;
    }

    public InlineStringInstruction(int offset, OpCode opCode, string @string)
        : base(offset, opCode)
    {
        m_string = @string;
    }

    public override string ToString()
    {
        StringBuilder sb = new(String.Length * 2);
        for (int i = 0; i < String.Length; i++)
        {
            char ch = String[i];
            switch (ch)
            {
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append('\\');
                    break;
                case < (char)0x20 or >= (char)0x7f:
                    sb.AppendFormat("\\u{0:x4}", (int)ch);
                    break;
                default:
                    sb.Append(ch);
                    break;
            }
        }
        return ToString($"\"{sb}\"");
    }
}

public class InlineVarInstruction : ILInstruction
{
    public ushort Ordinal { get; }

    public InlineVarInstruction(int offset, OpCode opCode, ushort ordinal)
        : base(offset, opCode)
    {
        Ordinal = ordinal;
    }

    public override string ToString() => ToString($"V_{Ordinal}");
}

public class ShortInlineVarInstruction : ILInstruction
{
    public byte Ordinal { get; }

    public ShortInlineVarInstruction(int offset, OpCode opCode, byte ordinal)
        : base(offset, opCode)
    {
        Ordinal = ordinal;
    }

    public override string ToString() => ToString($"V_{Ordinal}");
}
