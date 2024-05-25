﻿using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public readonly struct RuntimeContext
{
    public readonly Registers Registers;
    public readonly ImmutableArray<RuntimeValue> Memory;
    public readonly ImmutableArray<Instruction> Code;

    public readonly ImmutableArray<int> CallTrace;

    public RuntimeContext(
        Registers registers,
        ImmutableArray<RuntimeValue> memory,
        ImmutableArray<Instruction> code)
    {
        Registers = registers;
        Memory = memory;
        Code = code;

        CallTrace = ImmutableArray.Create(DebugUtils.TraceCalls(Memory, Registers.BasePointer));
    }
}

public struct BytecodeInterpreterSettings
{
    public int StackMaxSize;
    public int HeapSize;

    public static BytecodeInterpreterSettings Default => new()
    {
        StackMaxSize = 128,
        HeapSize = 2048,
    };
}

public class BytecodeProcessor
{
    public static readonly int StackDirection = -4;
    public const int StackSize = 256;

    Instruction CurrentInstruction => Code[Registers.CodePointer];
    public bool IsDone => Registers.CodePointer >= Code.Length;

    readonly BytecodeInterpreterSettings Settings;

    public Registers Registers;
    public readonly RuntimeValue[] Memory;
    public ImmutableArray<Instruction> Code;
    readonly FrozenDictionary<int, ExternalFunctionBase> ExternalFunctions;

    public int StackStart => Settings.HeapSize;

    public IEnumerable<RuntimeValue> GetStack()
    {
        if (StackDirection > 0)
        { return new ArraySegment<RuntimeValue>(Memory)[StackStart..Registers.StackPointer]; }
        else
        { return new ArraySegment<RuntimeValue>(Memory)[(Registers.StackPointer + 1)..].Reverse(); }
    }

    public Range<int> GetStackInterval(out bool isReversed)
    {
        if (StackDirection > 0)
        {
            isReversed = false;
            return new Range<int>(StackStart, Registers.StackPointer);
        }
        else
        {
            isReversed = true;
            return new Range<int>(Memory.Length - 1, Registers.StackPointer);
        }
    }

    public BytecodeProcessor(ImmutableArray<Instruction> code, FrozenDictionary<int, ExternalFunctionBase> externalFunctions, BytecodeInterpreterSettings settings)
    {
        Settings = settings;
        ExternalFunctions = externalFunctions;

        Code = code;

        Memory = new RuntimeValue[settings.HeapSize + StackSize];

        if (StackDirection > 0)
        { Registers.StackPointer = Settings.HeapSize; }
        else
        { Registers.StackPointer = Memory.Length - 1; }

        externalFunctions.SetInterpreter(this);
    }

    public RuntimeContext GetContext() => new(
        Registers,
        ImmutableCollectionsMarshal.AsImmutableArray(Memory),
        Code
    );

    void Step() => Registers.CodePointer++;
    void Step(int num) => Registers.CodePointer += num;

    public bool Tick()
    {
        if (IsDone) return false;

        try
        {
            Process();
        }
        catch (UserException error)
        {
            error.Context = GetContext();
            throw;
        }
        catch (RuntimeException error)
        {
            error.Context = GetContext();
            throw;
        }

        return true;
    }

    void Process()
    {
        switch (CurrentInstruction.Opcode)
        {
            case Opcode.NOP: break;

            case Opcode.Exit: EXIT(); break;

            case Opcode.Push: PUSH_VALUE(); break;
            case Opcode.Pop: POP_VALUE(); break;
            case Opcode.PopTo: POP_TO_VALUE(); break;

            case Opcode.Jump: JUMP_BY(); break;
            case Opcode.Throw: THROW(); break;

            case Opcode.JumpIfEqual: JumpIfEqual(); break;
            case Opcode.JumpIfNotEqual: JumpIfNotEqual(); break;
            case Opcode.JumpIfGreater: JumpIfGreater(); break;
            case Opcode.JumpIfGreaterOrEqual: JumpIfGreaterOrEqual(); break;
            case Opcode.JumpIfLess: JumpIfLess(); break;
            case Opcode.JumpIfLessOrEqual: JumpIfLessOrEqual(); break;

            case Opcode.Call: CALL(); break;
            case Opcode.Return: RETURN(); break;

            case Opcode.CallExternal: CALL_EXTERNAL(); break;

            case Opcode.MathAdd: MathAdd(); break;
            case Opcode.MathSub: MathSub(); break;
            case Opcode.MathMult: MathMult(); break;
            case Opcode.MathDiv: MathDiv(); break;
            case Opcode.MathMod: MathMod(); break;

            case Opcode.FMathAdd: FMathAdd(); break;
            case Opcode.FMathSub: FMathSub(); break;
            case Opcode.FMathMult: FMathMult(); break;
            case Opcode.FMathDiv: FMathDiv(); break;
            case Opcode.FMathMod: FMathMod(); break;

            case Opcode.Compare: Compare(); break;

            case Opcode.BitsShiftLeft: BitsShiftLeft(); break;
            case Opcode.BitsShiftRight: BitsShiftRight(); break;

            case Opcode.BitsAND: BitsAND(); break;
            case Opcode.BitsOR: BitsOR(); break;
            case Opcode.BitsXOR: BitsXOR(); break;
            case Opcode.BitsNOT: BitsNOT(); break;

            case Opcode.LogicOR: LogicOR(); break;
            case Opcode.LogicAND: LogicAND(); break;

            case Opcode.Move: Move(); break;

            case Opcode.FTo: FTo(); break;
            case Opcode.FFrom: FFrom(); break;

            default: throw new UnreachableException();
        }
    }

    #region Memory Manipulation

    public bool GetPointer(InstructionOperand operand, out int pointer)
    {
        switch (operand.Type)
        {
            case InstructionOperandType.Pointer:
                pointer = operand.Value.Int;
                return true;
            case InstructionOperandType.PointerBP:
                pointer = Registers.BasePointer + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerSP:
                pointer = Registers.StackPointer + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerEAX:
                pointer = Registers.EAX + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerEBX:
                pointer = Registers.EBX + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerECX:
                pointer = Registers.ECX + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerEDX:
                pointer = Registers.EDX + operand.Value.Int;
                return true;
            default:
                pointer = default;
                return false;
        }
    }

    // public void SetData<T>(int ptr, in T data) where T : unmanaged => MemoryMarshal.Write(Memory.AsSpan()[ptr..], in data);
    // public T GetData<T>(int ptr) where T : unmanaged => MemoryMarshal.Read<T>(Memory[ptr..]);

    public void SetData(int ptr, RuntimeValue data) => Memory[ptr] = data;
    public RuntimeValue GetData(int ptr) => Memory[ptr];

    RuntimeValue GetData(InstructionOperand operand) => operand.Type switch
    {
        InstructionOperandType.Immediate8 => operand.Value,
        InstructionOperandType.Immediate16 => operand.Value,
        InstructionOperandType.Immediate32 => operand.Value,
        InstructionOperandType.Pointer => GetData(operand.Value.Int),
        InstructionOperandType.PointerBP => GetData(Registers.BasePointer + operand.Value.Int),
        InstructionOperandType.PointerSP => GetData(Registers.StackPointer + operand.Value.Int),
        InstructionOperandType.PointerEAX => GetData(Registers.EAX + operand.Value.Int),
        InstructionOperandType.PointerEBX => GetData(Registers.EBX + operand.Value.Int),
        InstructionOperandType.PointerECX => GetData(Registers.ECX + operand.Value.Int),
        InstructionOperandType.PointerEDX => GetData(Registers.EDX + operand.Value.Int),
        InstructionOperandType.Register => operand.Value.Int switch
        {
            RegisterIds.CodePointer => new RuntimeValue(Registers.CodePointer),
            RegisterIds.StackPointer => new RuntimeValue(Registers.StackPointer),
            RegisterIds.BasePointer => new RuntimeValue(Registers.BasePointer),
            RegisterIds.EAX => new RuntimeValue(Registers.EAX),
            RegisterIds.AX => new RuntimeValue(Registers.AX),
            RegisterIds.AH => new RuntimeValue(Registers.AH),
            RegisterIds.AL => new RuntimeValue(Registers.AL),
            RegisterIds.EBX => new RuntimeValue(Registers.EBX),
            RegisterIds.BX => new RuntimeValue(Registers.BX),
            RegisterIds.BH => new RuntimeValue(Registers.BH),
            RegisterIds.BL => new RuntimeValue(Registers.BL),
            RegisterIds.ECX => new RuntimeValue(Registers.ECX),
            RegisterIds.CX => new RuntimeValue(Registers.CX),
            RegisterIds.CH => new RuntimeValue(Registers.CH),
            RegisterIds.CL => new RuntimeValue(Registers.CL),
            RegisterIds.EDX => new RuntimeValue(Registers.EDX),
            RegisterIds.DX => new RuntimeValue(Registers.DX),
            RegisterIds.DH => new RuntimeValue(Registers.DH),
            RegisterIds.DL => new RuntimeValue(Registers.DL),
            _ => throw new UnreachableException(),
        },
        _ => throw new UnreachableException(),
    };

    void SetData(InstructionOperand operand, RuntimeValue value)
    {
        switch (operand.Type)
        {
            case InstructionOperandType.Immediate8:
            case InstructionOperandType.Immediate16:
            case InstructionOperandType.Immediate32:
                throw new RuntimeException($"Can't set an immediate value");
            case InstructionOperandType.Pointer:
                SetData(operand.Value.Int, value);
                break;
            case InstructionOperandType.PointerBP:
                SetData(Registers.BasePointer + operand.Value.Int, value);
                break;
            case InstructionOperandType.PointerSP:
                SetData(Registers.StackPointer + operand.Value.Int, value);
                break;
            case InstructionOperandType.PointerEAX:
                SetData(Registers.EAX + operand.Value.Int, value);
                break;
            case InstructionOperandType.PointerEBX:
                SetData(Registers.EBX + operand.Value.Int, value);
                break;
            case InstructionOperandType.PointerECX:
                SetData(Registers.ECX + operand.Value.Int, value);
                break;
            case InstructionOperandType.PointerEDX:
                SetData(Registers.EDX + operand.Value.Int, value);
                break;
            case InstructionOperandType.Register:
                switch (operand.Value.Int)
                {
                    case RegisterIds.CodePointer:
                        Registers.CodePointer = value.Int;
                        break;
                    case RegisterIds.StackPointer:
                        Registers.StackPointer = value.Int;
                        break;
                    case RegisterIds.BasePointer:
                        Registers.BasePointer = value.Int;
                        break;
                    case RegisterIds.EAX:
                        Registers.EAX = value.Int;
                        break;
                    case RegisterIds.AX:
                        Registers.AX = value.Char;
                        break;
                    case RegisterIds.AH:
                        Registers.AH = value.Byte;
                        break;
                    case RegisterIds.AL:
                        Registers.AL = value.Byte;
                        break;
                    case RegisterIds.EBX:
                        Registers.EBX = value.Int;
                        break;
                    case RegisterIds.BX:
                        Registers.BX = value.Char;
                        break;
                    case RegisterIds.BH:
                        Registers.BH = value.Byte;
                        break;
                    case RegisterIds.BL:
                        Registers.BL = value.Byte;
                        break;
                    case RegisterIds.ECX:
                        Registers.ECX = value.Int;
                        break;
                    case RegisterIds.CX:
                        Registers.CX = value.Char;
                        break;
                    case RegisterIds.CH:
                        Registers.CH = value.Byte;
                        break;
                    case RegisterIds.CL:
                        Registers.CL = value.Byte;
                        break;
                    case RegisterIds.EDX:
                        Registers.EDX = value.Int;
                        break;
                    case RegisterIds.DX:
                        Registers.DX = value.Char;
                        break;
                    case RegisterIds.DH:
                        Registers.DH = value.Byte;
                        break;
                    case RegisterIds.DL:
                        Registers.DL = value.Byte;
                        break;
                    default: throw new UnreachableException();
                }
                break;
            default: throw new UnreachableException();
        }
    }

    void Push(RuntimeValue data)
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext());
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext());

        SetData(Registers.StackPointer, data);
        Registers.StackPointer += StackDirection;
    }

    RuntimeValue Pop()
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext());
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext());

        Registers.StackPointer -= StackDirection;
        RuntimeValue data = Memory[Registers.StackPointer];
        return data;
    }

    #endregion

    #region Instruction Methods

    #region Memory Operations

    void Move()
    {
        RuntimeValue value = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, value);

        Step();
    }

    #endregion

    #region Flow Control

    void THROW()
    {
        int pointer = GetData(CurrentInstruction.Operand1).Int;
        string? value = HeapUtils.GetString(Memory, pointer);
        throw new UserException(value ?? "null");
    }

    void CALL()
    {
        int relativeAddress = GetData(CurrentInstruction.Operand1).Int;

        Push(new RuntimeValue(Registers.CodePointer));

        Step(relativeAddress);
    }

    void RETURN()
    {
        RuntimeValue codePointer = Pop();

        Registers.CodePointer = codePointer.Int;
    }

    void JUMP_BY()
    {
        int relativeAddress = GetData(CurrentInstruction.Operand1).Int;

        Step(relativeAddress);
    }

    void EXIT()
    {
        Registers.CodePointer = Code.Length;
    }

    void JumpIfEqual()
    {
        if (Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfNotEqual()
    {
        if (!Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfGreater()
    {
        if ((!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))) && !Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfGreaterOrEqual()
    {
        if (!(Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfLess()
    {
        if (Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    void JumpIfLessOrEqual()
    {
        if ((Registers.Flags.Get(Flags.Sign) ^ Registers.Flags.Get(Flags.Overflow)) || Registers.Flags.Get(Flags.Zero))
        { Step(GetData(CurrentInstruction.Operand1).Int); }
        else
        { Step(); }
    }

    #endregion

    #region Comparison Operations

    void Compare()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        ALU.Subtract(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        Step();
    }

    #endregion

    #region Logic Operations

    void LogicAND()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((dst.Int != 0) && (src.Int != 0)) ? 1 : 0);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void LogicOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, ((dst.Int != 0) || (src.Int != 0)) ? 1 : 0);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsShiftLeft()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int << src.Int);

        Step();
    }

    void BitsShiftRight()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int >> src.Int);

        Step();
    }

    void BitsOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int | src.Int);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsXOR()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int ^ src.Int);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsNOT()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        SetData(CurrentInstruction.Operand1, ~dst.Int);

        Step();
    }

    void BitsAND()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int & src.Int);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    #endregion

    #region Math Operations

    void MathAdd()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = ALU.Add(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathDiv()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(dst.Byte / src.Byte)),
            BitWidth._16 => new RuntimeValue((char)(dst.Char / src.Char)),
            BitWidth._32 => new RuntimeValue((int)(dst.Int / src.Int)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathSub()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = ALU.Subtract(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathMult()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(dst.Byte * src.Byte)),
            BitWidth._16 => new RuntimeValue((char)(dst.Char * src.Char)),
            BitWidth._32 => new RuntimeValue((int)(dst.Int * src.Int)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Registers.Flags.SetCarry(dst.Int, CurrentInstruction.BitWidth);

        Step();
    }

    void MathMod()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new RuntimeValue((byte)(dst.Byte / src.Byte)),
            BitWidth._16 => new RuntimeValue((char)(dst.Char / src.Char)),
            BitWidth._32 => new RuntimeValue((int)(dst.Int / src.Int)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    #endregion

    #region Float Math Operations

    void FMathAdd()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single + src.Single));

        Step();
    }

    void FMathDiv()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single / src.Single));

        Step();
    }

    void FMathSub()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single - src.Single));

        Step();
    }

    void FMathMult()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single * src.Single));

        Step();
    }

    void FMathMod()
    {
        RuntimeValue dst = GetData(CurrentInstruction.Operand1);
        RuntimeValue src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue(dst.Single % src.Single));

        Step();
    }

    #endregion

    #region Stack Operations

    void PUSH_VALUE()
    {
        RuntimeValue v = GetData(CurrentInstruction.Operand1);
        Push(v);

        Step();
    }

    void POP_VALUE()
    {
        Pop();

        Step();
    }

    void POP_TO_VALUE()
    {
        RuntimeValue v = Pop();
        SetData(CurrentInstruction.Operand1, v);

        Step();
    }

    #endregion

    #region Utility Operations

    void FTo()
    {
        RuntimeValue data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, new RuntimeValue((float)data.Int));

        Step();
    }

    void FFrom()
    {
        RuntimeValue data = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, (int)data.Single);

        Step();
    }

    #endregion

    #region External Calls

    void CALL_EXTERNAL()
    {
        int functionId = GetData(CurrentInstruction.Operand1).Int;

        if (!ExternalFunctions.TryGetValue(functionId, out ExternalFunctionBase? function))
        { throw new RuntimeException($"Undefined external function {functionId}"); }

        RuntimeValue[] parameters = new RuntimeValue[function.Parameters.Length];
        for (int i = 0; i < function.Parameters.Length; i++)
        {
            parameters[parameters.Length - 1 - i] = Memory[Registers.StackPointer - ((1 + i) * StackDirection)];
        }

        if (function is ExternalFunctionManaged managedFunction)
        {
            managedFunction.OnReturn = Push;
            managedFunction.Callback(ImmutableArray.Create(parameters));
        }
        else if (function is ExternalFunctionSimple simpleFunction)
        {
            if (function.ReturnSomething)
            {
                RuntimeValue returnValue = simpleFunction.Call(this, ImmutableArray.Create(parameters));
                Push(returnValue);
            }
            else
            {
                simpleFunction.Call(this, ImmutableArray.Create(parameters));
            }
        }

        Step();
    }

    #endregion

    #endregion
}

[Flags]
public enum Flags
{
    _ = 0b_0000000000000000,
    Carry = 0b_0000000000000001,
    // Parity = 0b_0000000000000100,
    // AuxiliaryCarry = 0b_0000000000010000,
    Zero = 0b_0000000001000000,
    Sign = 0b_0000000010000000,

    // Trap = 0b_0000000100000000,
    // InterruptEnable = 0b_0000001000000000,
    // Direction = 0b_0000010000000000,
    Overflow = 0b_0000100000000000,
}

[StructLayout(LayoutKind.Explicit)]
public struct Registers
{
    [FieldOffset(0)] public int CodePointer;
    [FieldOffset(4)] public int StackPointer;
    [FieldOffset(8)] public int BasePointer;

    [FieldOffset(12)] public int EAX;
    [FieldOffset(14)] public ushort AX;
    [FieldOffset(14)] public byte AH;
    [FieldOffset(15)] public byte AL;

    [FieldOffset(16)] public int EBX;
    [FieldOffset(18)] public ushort BX;
    [FieldOffset(18)] public byte BH;
    [FieldOffset(19)] public byte BL;

    [FieldOffset(20)] public int ECX;
    [FieldOffset(22)] public ushort CX;
    [FieldOffset(22)] public byte CH;
    [FieldOffset(23)] public byte CL;

    [FieldOffset(24)] public int EDX;
    [FieldOffset(26)] public ushort DX;
    [FieldOffset(26)] public byte DH;
    [FieldOffset(27)] public byte DL;

    [FieldOffset(28)] public Flags Flags;
}

public static class ALU
{
    public static RuntimeValue Add(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags)
    {
        long _a = bitWidth switch
        {
            BitWidth._8 => a.Byte,
            BitWidth._16 => a.Char,
            BitWidth._32 => a.Int,
            _ => throw new UnreachableException(),
        };
        long _b = bitWidth switch
        {
            BitWidth._8 => b.Byte,
            BitWidth._16 => b.Char,
            BitWidth._32 => b.Int,
            _ => throw new UnreachableException(),
        };
        long result = _a + _b;

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x80) != (long)0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x8000) != (long)0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x80000000) != (long)0);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Zero, (result & (long)0xFF) == (long)0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Zero, (result & (long)0xFFFF) == (long)0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Zero, (result & (long)0xFFFFFFFF) == (long)0);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Carry, result > (long)0xFF);
                break;
            case BitWidth._16:
                flags.Set(Flags.Carry, result > (long)0xFFFF);
                break;
            case BitWidth._32:
                flags.Set(Flags.Carry, result > (long)0xFFFFFFFF);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x80) == (long)0x80);
                break;
            case BitWidth._16:
                flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x8000) == (long)0x8000);
                break;
            case BitWidth._32:
                flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x80000000) == (long)0x80000000);
                break;
        }

        return bitWidth switch
        {
            BitWidth._8 => new RuntimeValue(unchecked((byte)result)),
            BitWidth._16 => new RuntimeValue(unchecked((char)result)),
            BitWidth._32 => new RuntimeValue(unchecked((int)result)),
            _ => throw new UnreachableException(),
        };
    }

    public static RuntimeValue Subtract(RuntimeValue a, RuntimeValue b, BitWidth bitWidth, ref Flags flags)
    {
        long _a = bitWidth switch
        {
            BitWidth._8 => a.Byte,
            BitWidth._16 => a.Char,
            BitWidth._32 => a.Int,
            _ => throw new UnreachableException(),
        };
        long _b = bitWidth switch
        {
            BitWidth._8 => b.Byte,
            BitWidth._16 => b.Char,
            BitWidth._32 => b.Int,
            _ => throw new UnreachableException(),
        };
        long result = _a - _b;

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x80) != (long)0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x8000) != (long)0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Sign, unchecked((long)result & (long)0x80000000) != (long)0);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Zero, (result & (long)0xFF) == (long)0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Zero, (result & (long)0xFFFF) == (long)0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Zero, (result & (long)0xFFFFFFFF) == (long)0);
                break;
        }

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Carry, result > (long)0xFF);
                break;
            case BitWidth._16:
                flags.Set(Flags.Carry, result > (long)0xFFFF);
                break;
            case BitWidth._32:
                flags.Set(Flags.Carry, result > (long)0xFFFFFFFF);
                break;
        }

        // switch (bitWidth)
        // {
        //     case BitWidth._8:
        //         flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x80) == (long)0x80);
        //         break;
        //     case BitWidth._16:
        //         flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x8000) == (long)0x8000);
        //         break;
        //     case BitWidth._32:
        //         flags.Set(Flags.Overflow, ((result ^ _a) & (result ^ _b) & (long)0x80000000) == (long)0x80000000);
        //         break;
        // }

        return bitWidth switch
        {
            BitWidth._8 => new RuntimeValue(unchecked((byte)result)),
            BitWidth._16 => new RuntimeValue(unchecked((char)result)),
            BitWidth._32 => new RuntimeValue(unchecked((int)result)),
            _ => throw new UnreachableException(),
        };
    }
}

public static class FlagExtensions
{
    public static void Set(ref this Flags flags, Flags flag, bool value)
    {
        if (value) flags |= flag;
        else flags &= ~flag;
    }

    public static bool Get(ref this Flags flags, Flags flag) => (flags & flag) != 0;

    public static void SetSign(ref this Flags flags, int v, BitWidth bitWidth)
    {
        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Sign, unchecked((uint)v & (uint)0x80) != 0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Sign, unchecked((uint)v & (uint)0x8000) != 0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Sign, v < 0);
                // flags.Set(Flags.Sign, unchecked((uint)v & (uint)0x80000000) != 0);
                break;
        }
    }

    public static void SetZero(ref this Flags flags, int v, BitWidth bitWidth)
    {
        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Zero, (v & 0xFF) == 0);
                break;
            case BitWidth._16:
                flags.Set(Flags.Zero, (v & 0xFFFF) == 0);
                break;
            case BitWidth._32:
                flags.Set(Flags.Zero, (v & 0xFFFFFFFF) == 0);
                break;
        }
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetCarry(ref this Flags flags, long result, BitWidth bitWidth)
    {
        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Carry, result > 0xFF);
                break;
            case BitWidth._16:
                flags.Set(Flags.Carry, result > 0xFFFF);
                break;
            case BitWidth._32:
                flags.Set(Flags.Carry, result > 0xFFFFFFFF);
                break;
        }
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetOverflowAfterAdd(ref this Flags flags, int source, int destination, BitWidth bitWidth)
    {
        long result = source + destination;

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Overflow, ((result ^ source) & (result ^ destination) & 0x80) == 0x80);
                break;
            case BitWidth._16:
                flags.Set(Flags.Overflow, ((result ^ source) & (result ^ destination) & 0x8000) == 0x8000);
                break;
            case BitWidth._32:
                flags.Set(Flags.Overflow, ((result ^ source) & (result ^ destination) & 0x80000000) == 0x80000000);
                break;
        }
    }

    /// <summary>
    /// https://github.com/amensch/e8086/blob/master/e8086/i8086/ConditionalRegister.cs
    /// </summary>
    public static void SetOverflowAfterSub(ref this Flags flags, int source, int destination, BitWidth bitWidth)
    {
        long result = destination - source;

        switch (bitWidth)
        {
            case BitWidth._8:
                flags.Set(Flags.Overflow, ((result ^ destination) & (source ^ destination) & 0x80) == 0x80);
                break;
            case BitWidth._16:
                flags.Set(Flags.Overflow, ((result ^ destination) & (source ^ destination) & 0x8000) == 0x8000);
                break;
            case BitWidth._32:
                flags.Set(Flags.Overflow, ((result ^ destination) & (source ^ destination) & 0x80000000) == 0x80000000);
                break;
        }
    }
}

public enum Register
{
    CodePointer = 0b_00_00_001,
    StackPointer = 0b_00_00_010,
    BasePointer = 0b_00_00_011,

    EAX = 0b_00_00_100,
    AX = 0b_00_01_100,
    AH = 0b_00_10_100,
    AL = 0b_00_11_100,

    EBX = 0b_01_00_100,
    BX = 0b_01_01_100,
    BH = 0b_01_10_100,
    BL = 0b_01_11_100,

    ECX = 0b_10_00_100,
    CX = 0b_10_01_100,
    CH = 0b_10_10_100,
    CL = 0b_10_11_100,

    EDX = 0b_11_00_100,
    DX = 0b_11_01_100,
    DH = 0b_11_10_100,
    DL = 0b_11_11_100,
}

public static class RegisterIds
{
    public const int CodePointer = 0b_00_00_001;
    public const int StackPointer = 0b_00_00_010;
    public const int BasePointer = 0b_00_00_011;

    public const int EAX = 0b_00_00_100;
    public const int AX = 0b_00_01_100;
    public const int AH = 0b_00_10_100;
    public const int AL = 0b_00_11_100;

    public const int EBX = 0b_01_00_100;
    public const int BX = 0b_01_01_100;
    public const int BH = 0b_01_10_100;
    public const int BL = 0b_01_11_100;

    public const int ECX = 0b_10_00_100;
    public const int CX = 0b_10_01_100;
    public const int CH = 0b_10_10_100;
    public const int CL = 0b_10_11_100;

    public const int EDX = 0b_11_00_100;
    public const int DX = 0b_11_01_100;
    public const int DH = 0b_11_10_100;
    public const int DL = 0b_11_11_100;
}
