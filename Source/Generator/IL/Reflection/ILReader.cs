using System.Reflection;
using System.Reflection.Emit;

namespace LanguageCore.IL.Reflection;

public sealed class ILReader : IEnumerable<ILInstruction>, IEnumerable
{
    static readonly OpCode[] s_OneByteOpCodes;
    static readonly OpCode[] s_TwoByteOpCodes;

    static ILReader()
    {
        s_OneByteOpCodes = new OpCode[0x100];
        s_TwoByteOpCodes = new OpCode[0x100];

        foreach (FieldInfo fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            OpCode opCode = (OpCode)fi.GetValue(null)!;
            ushort value = (ushort)opCode.Value;
            if (value < 0x100)
            {
                s_OneByteOpCodes[value] = opCode;
            }
            else if ((value & 0xff00) == 0xfe00)
            {
                s_TwoByteOpCodes[value & 0xff] = opCode;
            }
        }
    }

    int _position;
    readonly ITokenResolver _resolver;
    readonly byte[] _byteArray;

    public ILReader(byte[] il, ITokenResolver tokenResolver)
    {
        _resolver = tokenResolver;
        _byteArray = il;
        _position = 0;
    }

    public IEnumerator<ILInstruction> GetEnumerator()
    {
        while (_position < _byteArray.Length)
            yield return Next();

        _position = 0;
        yield break;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    ILInstruction Next()
    {
        int offset = _position;
        OpCode opCode;
        int token;

        // read first 1 or 2 bytes as opCode
        byte code = ReadByte();
        if (code != 0xFE)
        {
            opCode = s_OneByteOpCodes[code];
        }
        else
        {
            code = ReadByte();
            opCode = s_TwoByteOpCodes[code];
        }

        switch (opCode.OperandType)
        {
            case OperandType.InlineNone:
                return new InlineNoneInstruction(offset, opCode);

            //The operand is an 8-bit integer branch target.
            case OperandType.ShortInlineBrTarget:
                sbyte shortDelta = ReadSByte();
                return new ShortInlineBrTargetInstruction(offset, opCode, shortDelta);

            //The operand is a 32-bit integer branch target.
            case OperandType.InlineBrTarget:
                int delta = ReadInt32();
                return new InlineBrTargetInstruction(offset, opCode, delta);

            //The operand is an 8-bit integer: 001F  ldc.i4.s, FE12  unaligned.
            case OperandType.ShortInlineI:
                byte int8 = ReadByte();
                return new ShortInlineIInstruction(offset, opCode, int8);

            //The operand is a 32-bit integer.
            case OperandType.InlineI:
                int int32 = ReadInt32();
                return new InlineIInstruction(offset, opCode, int32);

            //The operand is a 64-bit integer.
            case OperandType.InlineI8:
                long int64 = ReadInt64();
                return new InlineI8Instruction(offset, opCode, int64);

            //The operand is a 32-bit IEEE floating point number.
            case OperandType.ShortInlineR:
                float float32 = ReadSingle();
                return new ShortInlineRInstruction(offset, opCode, float32);

            //The operand is a 64-bit IEEE floating point number.
            case OperandType.InlineR:
                double float64 = ReadDouble();
                return new InlineRInstruction(offset, opCode, float64);

            //The operand is an 8-bit integer containing the ordinal of a local variable or an argument
            case OperandType.ShortInlineVar:
                byte index8 = ReadByte();
                return new ShortInlineVarInstruction(offset, opCode, index8);

            //The operand is 16-bit integer containing the ordinal of a local variable or an argument.
            case OperandType.InlineVar:
                ushort index16 = ReadUInt16();
                return new InlineVarInstruction(offset, opCode, index16);

            //The operand is a 32-bit metadata string token.
            case OperandType.InlineString:
                token = ReadInt32();
                return new InlineStringInstruction(offset, opCode, token, _resolver);

            //The operand is a 32-bit metadata signature token.
            case OperandType.InlineSig:
                token = ReadInt32();
                return new InlineSigInstruction(offset, opCode, token, _resolver);

            //The operand is a 32-bit metadata token.
            case OperandType.InlineMethod:
                token = ReadInt32();
                return new InlineMethodInstruction(offset, opCode, token, _resolver);

            //The operand is a 32-bit metadata token.
            case OperandType.InlineField:
                token = ReadInt32();
                return new InlineFieldInstruction(offset, opCode, token, _resolver);

            //The operand is a 32-bit metadata token.
            case OperandType.InlineType:
                token = ReadInt32();
                return new InlineTypeInstruction(offset, opCode, token, _resolver);

            //The operand is a FieldRef, MethodRef, or TypeRef token.
            case OperandType.InlineTok:
                token = ReadInt32();
                return new InlineTokInstruction(offset, opCode, token, _resolver);

            //The operand is the 32-bit integer argument to a switch instruction.
            case OperandType.InlineSwitch:
                int cases = ReadInt32();
                int[] deltas = new int[cases];
                for (int i = 0; i < cases; i++)
                    deltas[i] = ReadInt32();
                return new InlineSwitchInstruction(offset, opCode, deltas);

            default:
                throw new BadImageFormatException($"Unexpected OperandType {opCode.OperandType}");
        }
    }

    public static ILInstruction Create(int offset, OpCode opCode, object operand)
    {
        switch (opCode.OperandType)
        {
            case OperandType.InlineNone:
                return new InlineNoneInstruction(offset, opCode);

            //The operand is an 8-bit integer branch target.
            case OperandType.ShortInlineBrTarget:
                sbyte shortDelta = (sbyte)operand;
                return new ShortInlineBrTargetInstruction(offset, opCode, shortDelta);

            //The operand is a 32-bit integer branch target.
            case OperandType.InlineBrTarget:
                int delta = (int)operand;
                return new InlineBrTargetInstruction(offset, opCode, delta);

            //The operand is an 8-bit integer: 001F  ldc.i4.s, FE12  unaligned.
            case OperandType.ShortInlineI:
                byte int8 = (byte)operand;
                return new ShortInlineIInstruction(offset, opCode, int8);

            //The operand is a 32-bit integer.
            case OperandType.InlineI:
                int int32 = (int)operand;
                return new InlineIInstruction(offset, opCode, int32);

            //The operand is a 64-bit integer.
            case OperandType.InlineI8:
                long int64 = (long)operand;
                return new InlineI8Instruction(offset, opCode, int64);

            //The operand is a 32-bit IEEE floating point number.
            case OperandType.ShortInlineR:
                float float32 = (float)operand;
                return new ShortInlineRInstruction(offset, opCode, float32);

            //The operand is a 64-bit IEEE floating point number.
            case OperandType.InlineR:
                double float64 = (double)operand;
                return new InlineRInstruction(offset, opCode, float64);

            //The operand is an 8-bit integer containing the ordinal of a local variable or an argument
            case OperandType.ShortInlineVar:
                byte index8 = (byte)operand;
                return new ShortInlineVarInstruction(offset, opCode, index8);

            //The operand is 16-bit integer containing the ordinal of a local variable or an argument.
            case OperandType.InlineVar:
                ushort index16 = (ushort)operand;
                return new InlineVarInstruction(offset, opCode, index16);

            //The operand is a 32-bit metadata string token.
            case OperandType.InlineString:
                return new InlineStringInstruction(offset, opCode, (string)operand);

            //The operand is a 32-bit metadata signature token.
            case OperandType.InlineSig:
                return new InlineSigInstruction(offset, opCode, (byte[])operand);

            //The operand is a 32-bit metadata token.
            case OperandType.InlineMethod:
                return new InlineMethodInstruction(offset, opCode, (MethodBase)operand);

            //The operand is a 32-bit metadata token.
            case OperandType.InlineField:
                return new InlineFieldInstruction(offset, opCode, (FieldInfo)operand);

            //The operand is a 32-bit metadata token.
            case OperandType.InlineType:
                return new InlineTypeInstruction(offset, opCode, (Type)operand);

            //The operand is a FieldRef, MethodRef, or TypeRef token.
            case OperandType.InlineTok:
                return new InlineTokInstruction(offset, opCode, (MemberInfo)operand);

            //The operand is the 32-bit integer argument to a switch instruction.
            case OperandType.InlineSwitch:
                throw new NotImplementedException();

            default:
                throw new BadImageFormatException($"Unexpected OperandType {opCode.OperandType}");
        }
    }

    byte ReadByte()
    {
        return (byte)_byteArray[_position++];
    }

    sbyte ReadSByte()
    {
        return (sbyte)ReadByte();
    }

    ushort ReadUInt16()
    {
        int pos = _position;
        _position += 2;
        return BitConverter.ToUInt16(_byteArray, pos);
    }

    uint ReadUInt32()
    {
        int pos = _position;
        _position += 4;
        return BitConverter.ToUInt32(_byteArray, pos);
    }
    ulong ReadUInt64()
    {
        int pos = _position;
        _position += 8;
        return BitConverter.ToUInt64(_byteArray, pos);
    }

    int ReadInt32()
    {
        int pos = _position;
        _position += 4;
        return BitConverter.ToInt32(_byteArray, pos);
    }
    long ReadInt64()
    {
        int pos = _position;
        _position += 8;
        return BitConverter.ToInt64(_byteArray, pos);
    }

    float ReadSingle()
    {
        int pos = _position;
        _position += 4;
        return BitConverter.ToSingle(_byteArray, pos);
    }
    double ReadDouble()
    {
        int pos = _position;
        _position += 8;
        return BitConverter.ToDouble(_byteArray, pos);
    }
}
