using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public readonly struct RuntimeContext
{
    public ImmutableArray<int> CallTrace { get; init; }
    public int CodePointer { get; init; }
    public ImmutableArray<Instruction> Code { get; init; }
    public IReadOnlyList<DataItem> Memory { get; init; }
    public int CodeSampleStart { get; init; }
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

public interface ISleep
{
    /// <summary>
    /// Executes every time <see cref="BytecodeProcessor.Tick"/> is called.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if it is sleeping, <see langword="false"/> otherwise.
    /// </returns>
    public bool Tick();
}

public class TickSleep : ISleep
{
    readonly int Ticks;
    int Elapsed;

    public TickSleep(int ticks)
    {
        Ticks = ticks;
    }

    public bool Tick()
    {
        Elapsed++;
        return Elapsed < Ticks;
    }
}

public class TimeSleep : ISleep
{
    readonly double Timeout;
    readonly double Started;

    public TimeSleep(double timeout)
    {
        Timeout = timeout;
        Started = DateTime.UtcNow.TimeOfDay.TotalSeconds;
    }

    public bool Tick()
    {
        double now = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        return now - Started < Timeout;
    }
}

public class BytecodeProcessor
{
    const int CodeSampleRange = 20;
    public static readonly int StackDirection = -1;
    public const int StackSize = 256;

    Instruction CurrentInstruction => Code[Registers.CodePointer];
    public bool IsDone => Registers.CodePointer >= Code.Length;

    readonly BytecodeInterpreterSettings Settings;

    ISleep? CurrentSleep;

    public Registers Registers;
    public readonly DataItem[] Memory;
    public ImmutableArray<Instruction> Code;
    readonly FrozenDictionary<int, ExternalFunctionBase> ExternalFunctions;

    public int StackStart => Settings.HeapSize;

    public IEnumerable<DataItem> GetStack()
    {
        ArraySegment<DataItem> stack = GetStack(out bool shouldReverse);
        if (shouldReverse)
        { return stack.Reverse(); }
        else
        { return stack; }
    }

    public ArraySegment<DataItem> GetStack(out bool shouldReverse)
    {
        if (StackDirection > 0)
        {
            shouldReverse = false;
            return new ArraySegment<DataItem>(Memory)[StackStart..Registers.StackPointer];
        }
        else
        {
            shouldReverse = true;
            return new ArraySegment<DataItem>(Memory)[(Registers.StackPointer + 1)..];
        }
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

        Memory = new DataItem[settings.HeapSize + StackSize];

        if (StackDirection > 0)
        { Registers.StackPointer = Settings.HeapSize; }
        else
        { Registers.StackPointer = Memory.Length - 1; }

        externalFunctions.SetInterpreter(this);
    }

    public void Sleep(ISleep sleep) => CurrentSleep = sleep;

    RuntimeContext GetContext() => new()
    {
        CallTrace = TraceCalls(Memory, Registers.BasePointer),
        CodePointer = Registers.CodePointer,
        Code = Code[Math.Max(Registers.CodePointer - CodeSampleRange, 0)..Math.Clamp(Registers.CodePointer + CodeSampleRange, 0, Code.Length - 1)],
        Memory = Memory,
        CodeSampleStart = Math.Max(Registers.CodePointer - CodeSampleRange, 0),
    };

    public static ImmutableArray<int> TraceCalls(IReadOnlyList<DataItem> stack, int basePointer)
    {
        static bool CanTraceCallsWith(IReadOnlyList<DataItem> stack, int basePointer)
        {
            int savedCodePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedCodePointerOffset * StackDirection);
            int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * StackDirection);

            if (savedCodePointerAddress < 0 || savedCodePointerAddress >= stack.Count) return false;
            if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Count) return false;

            return true;
        }

        static void TraceCalls(IReadOnlyList<DataItem> stack, List<int> callTrace, int basePointer)
        {
            if (!CanTraceCallsWith(stack, basePointer)) return;

            DataItem savedCodePointerD = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedCodePointerOffset * StackDirection)];
            DataItem savedBasePointerD = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * StackDirection)];

            int savedCodePointer = savedCodePointerD.Int;
            int savedBasePointer = savedBasePointerD.Int;

            callTrace.Add(savedCodePointer);

            if (savedBasePointer == basePointer) return;
            TraceCalls(stack, callTrace, savedBasePointer);
        }

        if (!CanTraceCallsWith(stack, basePointer))
        { return ImmutableArray.Create<int>(); }

        List<int> callTrace = new();

        TraceCalls(stack, callTrace, basePointer);

        int[] callTraceResult = callTrace.ToArray();
        Array.Reverse(callTraceResult);
        return callTraceResult.ToImmutableArray();
    }

    public static ImmutableArray<int> TraceBasePointers(IReadOnlyList<DataItem> stack, int basePointer)
    {
        static bool CanTraceBPsWith(IReadOnlyList<DataItem> stack, int basePointer)
        {
            int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * StackDirection);

            if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Count) return false;

            return true;
        }

        static void TraceBasePointers(List<int> result, IReadOnlyList<DataItem> stack, int basePointer)
        {
            if (!CanTraceBPsWith(stack, basePointer)) return;

            DataItem savedBasePointerD = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * StackDirection)];
            if (savedBasePointerD.Type != RuntimeType.Integer) return;
            int newBasePointer = savedBasePointerD.Int;
            result.Add(newBasePointer);
            if (newBasePointer == basePointer) return;
            TraceBasePointers(result, stack, newBasePointer);
        }

        if (!CanTraceBPsWith(stack, basePointer))
        { return ImmutableArray.Create<int>(); }

        List<int> result = new();
        TraceBasePointers(result, stack, basePointer);
        return result.ToImmutableArray();
    }

    void Step() => Registers.CodePointer++;
    void Step(int num) => Registers.CodePointer += num;

    public bool Tick()
    {
        if (CurrentSleep is not null)
        {
            if (CurrentSleep.Tick())
            { return true; }
            else
            { CurrentSleep = null; }
        }

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
        catch (Exception error)
        {
            throw new RuntimeException(error.Message, error, GetContext());
        }

        return true;
    }

    void Process()
    {
        switch (CurrentInstruction.Opcode)
        {
            case Opcode._: throw new InternalException("Unknown instruction");

            case Opcode.Exit: EXIT(); break;

            case Opcode.Push: PUSH_VALUE(); break;
            case Opcode.Pop: POP_VALUE(); break;

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

            case Opcode.Allocate: HEAP_ALLOC(); break;
            case Opcode.Free: HEAP_FREE(); break;

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

    DataItem GetData(InstructionOperand operand) => operand.Type switch
    {
        InstructionOperandType.Immediate => operand.Value,
        InstructionOperandType.Pointer => Memory[operand.Value.Int],
        InstructionOperandType.PointerBP => Memory[Registers.BasePointer + operand.Value.Int],
        InstructionOperandType.PointerSP => Memory[Registers.StackPointer + operand.Value.Int],
        InstructionOperandType.PointerEAX => Memory[Registers.EAX + operand.Value.Int],
        InstructionOperandType.PointerEBX => Memory[Registers.EBX + operand.Value.Int],
        InstructionOperandType.PointerECX => Memory[Registers.ECX + operand.Value.Int],
        InstructionOperandType.PointerEDX => Memory[Registers.EDX + operand.Value.Int],
        InstructionOperandType.Register => operand.Value.Int switch
        {
            RegisterIds.CodePointer => new DataItem(Registers.CodePointer),
            RegisterIds.StackPointer => new DataItem(Registers.StackPointer),
            RegisterIds.BasePointer => new DataItem(Registers.BasePointer),
            RegisterIds.EAX => new DataItem(Registers.EAX),
            RegisterIds.AX => new DataItem(Registers.AX),
            RegisterIds.AH => new DataItem(Registers.AH),
            RegisterIds.AL => new DataItem(Registers.AL),
            RegisterIds.EBX => new DataItem(Registers.EBX),
            RegisterIds.BX => new DataItem(Registers.BX),
            RegisterIds.BH => new DataItem(Registers.BH),
            RegisterIds.BL => new DataItem(Registers.BL),
            RegisterIds.ECX => new DataItem(Registers.ECX),
            RegisterIds.CX => new DataItem(Registers.CX),
            RegisterIds.CH => new DataItem(Registers.CH),
            RegisterIds.CL => new DataItem(Registers.CL),
            RegisterIds.EDX => new DataItem(Registers.EDX),
            RegisterIds.DX => new DataItem(Registers.DX),
            RegisterIds.DH => new DataItem(Registers.DH),
            RegisterIds.DL => new DataItem(Registers.DL),
            _ => throw new UnreachableException(),
        },
        _ => throw new UnreachableException(),
    };

    void SetData(InstructionOperand operand, DataItem value)
    {
        switch (operand.Type)
        {
            case InstructionOperandType.Immediate:
                throw new RuntimeException($"Can't set an immediate value");
            case InstructionOperandType.Pointer:
                Memory[operand.Value.Int] = value;
                break;
            case InstructionOperandType.PointerBP:
                Memory[Registers.BasePointer + operand.Value.Int] = value;
                break;
            case InstructionOperandType.PointerSP:
                Memory[Registers.StackPointer + operand.Value.Int] = value;
                break;
            case InstructionOperandType.PointerEAX:
                Memory[Registers.EAX + operand.Value.Int] = value;
                break;
            case InstructionOperandType.PointerEBX:
                Memory[Registers.EBX + operand.Value.Int] = value;
                break;
            case InstructionOperandType.PointerECX:
                Memory[Registers.ECX + operand.Value.Int] = value;
                break;
            case InstructionOperandType.PointerEDX:
                Memory[Registers.EDX + operand.Value.Int] = value;
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

    void Push(DataItem data)
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext());
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext());

        Memory[Registers.StackPointer] = data;
        Registers.StackPointer += StackDirection;
    }

    DataItem Pop()
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext());
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext());

        Registers.StackPointer -= StackDirection;
        return Memory[Registers.StackPointer];
    }

    #endregion

    #region Instruction Methods

    #region HEAP Operations

    void HEAP_ALLOC()
    {
        DataItem sizeData = GetData(CurrentInstruction.Operand2);
        int size = sizeData.Int;

        int ptr = HeapUtils.Allocate(Memory, size);

        SetData(CurrentInstruction.Operand1, ptr);

        Step();
    }

    void HEAP_FREE()
    {
        DataItem pointerData = GetData(CurrentInstruction.Operand1);
        int pointer = pointerData.Int;

        HeapUtils.Deallocate(Memory, pointer);

        Step();
    }

    void Move()
    {
        DataItem value = GetData(CurrentInstruction.Operand2);
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

        Push(new DataItem(Registers.CodePointer));

        Step(relativeAddress);
    }

    void RETURN()
    {
        DataItem codePointer = Pop();

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
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);

        ALU.Subtract(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        Step();
    }

    #endregion

    #region Logic Operations

    void LogicAND()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, (bool)dst && (bool)src);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void LogicOR()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, (bool)dst || (bool)src);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsShiftLeft()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int << src.Int);

        Step();
    }

    void BitsShiftRight()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int >> src.Int);

        Step();
    }

    void BitsOR()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int | src.Int);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsXOR()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Int ^ src.Int);

        Registers.Flags.SetSign(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.SetZero(dst.Int, CurrentInstruction.BitWidth);
        Registers.Flags.Set(Flags.Carry, false);

        Step();
    }

    void BitsNOT()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        SetData(CurrentInstruction.Operand1, ~dst.Int);

        Step();
    }

    void BitsAND()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
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
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);

        dst = ALU.Add(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathDiv()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new DataItem((byte)(dst.Byte / src.Byte)),
            BitWidth._16 => new DataItem((char)(dst.Char / src.Char)),
            BitWidth._32 => new DataItem((int)(dst.Int / src.Int)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathSub()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);

        dst = ALU.Subtract(dst, src, CurrentInstruction.BitWidth, ref Registers.Flags);

        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    void MathMult()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new DataItem((byte)(dst.Byte * src.Byte)),
            BitWidth._16 => new DataItem((char)(dst.Char * src.Char)),
            BitWidth._32 => new DataItem((int)(dst.Int * src.Int)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Registers.Flags.SetCarry(dst.Int, CurrentInstruction.BitWidth);

        Step();
    }

    void MathMod()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);

        dst = CurrentInstruction.BitWidth switch
        {
            BitWidth._8 => new DataItem((byte)(dst.Byte / src.Byte)),
            BitWidth._16 => new DataItem((char)(dst.Char / src.Char)),
            BitWidth._32 => new DataItem((int)(dst.Int / src.Int)),
            _ => throw new UnreachableException(),
        };
        SetData(CurrentInstruction.Operand1, dst);

        Step();
    }

    #endregion

    #region Float Math Operations

    void FMathAdd()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Single + src.Single);

        Step();
    }

    void FMathDiv()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Single / src.Single);

        Step();
    }

    void FMathSub()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Single - src.Single);

        Step();
    }

    void FMathMult()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Single * src.Single);

        Step();
    }

    void FMathMod()
    {
        DataItem dst = GetData(CurrentInstruction.Operand1);
        DataItem src = GetData(CurrentInstruction.Operand2);
        SetData(CurrentInstruction.Operand1, dst.Single % src.Single);

        Step();
    }

    #endregion

    #region Stack Operations

    void PUSH_VALUE()
    {
        DataItem v = GetData(CurrentInstruction.Operand1);
        Push(v);

        Step();
    }

    void POP_VALUE()
    {
        DataItem v = Pop();
        if (!CurrentInstruction.Operand1.Value.IsNull)
        { SetData(CurrentInstruction.Operand1, v); }

        Step();
    }

    #endregion

    #region Utility Operations

    void FTo()
    {
        DataItem data = GetData(CurrentInstruction.Operand2);
        switch (data.Type)
        {
            case RuntimeType.Null:
                SetData(CurrentInstruction.Operand1, DataItem.Null);
                break;
            case RuntimeType.Single:
                SetData(CurrentInstruction.Operand1, data);
                break;
            default:
                SetData(CurrentInstruction.Operand1, (float)data);
                break;
        }

        Step();
    }

    void FFrom()
    {
        DataItem data = GetData(CurrentInstruction.Operand2);
        switch (data.Type)
        {
            case RuntimeType.Null:
            case RuntimeType.Byte:
            case RuntimeType.Char:
            case RuntimeType.Integer:
                SetData(CurrentInstruction.Operand1, data);
                break;
            case RuntimeType.Single:
                SetData(CurrentInstruction.Operand1, (int)data.Single);
                break;
        }

        Step();
    }

    #endregion

    #region External Calls

    void CALL_EXTERNAL()
    {
        int functionId = GetData(CurrentInstruction.Operand1).Int;

        if (!ExternalFunctions.TryGetValue(functionId, out ExternalFunctionBase? function))
        { throw new RuntimeException($"Undefined external function {functionId}"); }

        DataItem[] parameters = new DataItem[function.Parameters.Length];
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
                DataItem returnValue = simpleFunction.Call(this, ImmutableArray.Create(parameters));
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
    public static DataItem Add(DataItem a, DataItem b, BitWidth bitWidth, ref Flags flags)
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
            BitWidth._8 => new DataItem(unchecked((byte)result)),
            BitWidth._16 => new DataItem(unchecked((char)result)),
            BitWidth._32 => new DataItem(unchecked((int)result)),
            _ => throw new UnreachableException(),
        };
    }

    public static DataItem Subtract(DataItem a, DataItem b, BitWidth bitWidth, ref Flags flags)
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
            BitWidth._8 => new DataItem(unchecked((byte)result)),
            BitWidth._16 => new DataItem(unchecked((char)result)),
            BitWidth._32 => new DataItem(unchecked((int)result)),
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
