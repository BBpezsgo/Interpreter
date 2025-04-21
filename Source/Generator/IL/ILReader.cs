
using System.Reflection;
using System.Reflection.Emit;

namespace LanguageCore.IL.Generator;

public class ILReader
: IEnumerable<ILInstruction>
{
    readonly byte[] Bytes;
    int Position;
    readonly MethodBase EnclosingMethod;

    static readonly OpCode[] OneByteOpCodes;
    static readonly OpCode[] TwoByteOpCodes;

    static ILReader()
    {
        OneByteOpCodes = new OpCode[0x100];
        TwoByteOpCodes = new OpCode[0x100];

        foreach (FieldInfo fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            OpCode opCode = (OpCode)fi.GetValue(null);
            ushort value = (ushort)opCode.Value;
            if (value < 0x100)
            {
                OneByteOpCodes[value] = opCode;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                TwoByteOpCodes[value & 0xff] = opCode;
            }
        }
    }

    public ILReader(MethodBase enclosingMethod)
    {
        EnclosingMethod = enclosingMethod;
        MethodBody? methodBody = EnclosingMethod.GetMethodBody();
        Bytes = (methodBody == null) ? Array.Empty<byte>() : (methodBody.GetILAsByteArray() ?? Array.Empty<byte>());
        Position = 0;
    }

    public IEnumerator<ILInstruction> GetEnumerator()
    {
        while (Position < Bytes.Length) yield return Next();
        Position = 0;
        yield break;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    ILInstruction Next()
    {
        int offset = Position;
        OpCode opCode;
        int token;

        byte code = ReadByte();
        if (code != 0xFE)
        {
            opCode = OneByteOpCodes[code];
        }
        else
        {
            code = ReadByte();
            opCode = TwoByteOpCodes[code];
        }

        switch (opCode.OperandType)
        {
            case OperandType.InlineNone:
                return new ArgumentlessInstruction(EnclosingMethod, offset, opCode);
            case OperandType.ShortInlineBrTarget:
                sbyte shortDelta = ReadSByte();
                return new ArgumentisedInstruction<sbyte>(EnclosingMethod, offset, opCode, shortDelta);
            case OperandType.InlineBrTarget:
                int delta = ReadInt32();
                return new ArgumentisedInstruction<int>(EnclosingMethod, offset, opCode, delta);
            case OperandType.ShortInlineI:
                byte int8 = ReadByte();
                return new ArgumentisedInstruction<byte>(EnclosingMethod, offset, opCode, int8);
            case OperandType.InlineI:
                int int32 = ReadInt32();
                return new ArgumentisedInstruction<int>(EnclosingMethod, offset, opCode, int32);
            case OperandType.InlineI8:
                long int64 = ReadInt64();
                return new ArgumentisedInstruction<long>(EnclosingMethod, offset, opCode, int64);
            case OperandType.ShortInlineR:
                float float32 = ReadSingle();
                return new ArgumentisedInstruction<float>(EnclosingMethod, offset, opCode, float32);
            case OperandType.InlineR:
                double float64 = ReadDouble();
                return new ArgumentisedInstruction<double>(EnclosingMethod, offset, opCode, float64);
            case OperandType.ShortInlineVar:
                byte index8 = ReadByte();
                return new ArgumentisedInstruction<byte>(EnclosingMethod, offset, opCode, index8);
            case OperandType.InlineVar:
                ushort index16 = ReadUInt16();
                return new ArgumentisedInstruction<ushort>(EnclosingMethod, offset, opCode, index16);
            case OperandType.InlineString:
                token = ReadInt32();
                return new ArgumentisedInstruction<string>(EnclosingMethod, offset, opCode, EnclosingMethod.Module.ResolveString(token));
            case OperandType.InlineSig:
                token = ReadInt32();
                return new ArgumentisedInstruction<byte[]>(EnclosingMethod, offset, opCode, EnclosingMethod.Module.ResolveSignature(token));
            case OperandType.InlineField:
                token = ReadInt32();
                return new ArgumentisedInstruction<FieldInfo>(EnclosingMethod, offset, opCode, EnclosingMethod.Module.ResolveField(token));
            case OperandType.InlineType:
                token = ReadInt32();
                return new ArgumentisedInstruction<Type>(EnclosingMethod, offset, opCode, EnclosingMethod.Module.ResolveType(token));
            case OperandType.InlineTok:
                token = ReadInt32();
                return new ArgumentisedInstruction<object>(EnclosingMethod, offset, opCode, token);
            case OperandType.InlineMethod:
                token = ReadInt32();
                return new ArgumentisedInstruction<MethodBase>(EnclosingMethod, offset, opCode, EnclosingMethod.Module.ResolveMethod(token));
            case OperandType.InlineSwitch:
                int cases = ReadInt32();
                int[] deltas = new int[cases];
                for (int i = 0; i < cases; i++) deltas[i] = ReadInt32();
                return new ArgumentisedInstruction<int[]>(EnclosingMethod, offset, opCode, deltas);
            default:
                throw new BadImageFormatException($"Unexpected OperandType {opCode.OperandType}");
        }
    }
    byte ReadByte() => (byte)Bytes[Position++];

    sbyte ReadSByte() => (sbyte)ReadByte();

    ushort ReadUInt16()
    {
        Position += 2;
        return BitConverter.ToUInt16(Bytes,
        Position - 2);
    }

    int ReadInt32()
    {
        Position += 4;
        return BitConverter.ToInt32(Bytes, Position - 4);
    }

    long ReadInt64()
    {
        Position += 8;
        return BitConverter.ToInt64(Bytes, Position - 8);
    }

    float ReadSingle()
    {
        Position += 4;
        return BitConverter.ToSingle(Bytes, Position - 4);
    }

    double ReadDouble()
    {
        Position += 8;
        return BitConverter.ToDouble(Bytes, Position - 8);
    }
}

public class ArgumentisedInstruction<TArgument> : ILInstruction
{
    public readonly TArgument Argument;

    public ArgumentisedInstruction(MethodBase enclosingMethod, int offset, OpCode opCode, TArgument argument) : base(offset, opCode, enclosingMethod)
    {
        Argument = argument;
    }

    public override string ToString() => $"{OpCode} {Argument}";
}

public class ArgumentlessInstruction : ILInstruction
{
    public ArgumentlessInstruction(MethodBase enclosingMethod, int offset, OpCode opCode) : base(offset, opCode, enclosingMethod)
    {

    }

    public override string ToString() => $"{OpCode}";
}

public class ILInstruction
{
    public readonly int Offset;
    public readonly OpCode OpCode;
    public readonly MethodBase EnclosingMethod;

    public ILInstruction(int offset, OpCode opCode, MethodBase enclosingMethod)
    {
        Offset = offset;
        OpCode = opCode;
        EnclosingMethod = enclosingMethod;
    }
}
