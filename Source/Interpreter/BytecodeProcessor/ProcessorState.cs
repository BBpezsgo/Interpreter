namespace LanguageCore.Runtime;

#if UNITY
// [Unity.Burst.BurstCompile]
#endif
public ref partial struct ProcessorState
{
    public const int StackDirection = -1;

    readonly Instruction CurrentInstruction => Code[Registers.CodePointer];
    public readonly bool IsDone => Registers.CodePointer == Code.Length;
    public readonly int StackStart => StackDirection > 0 ? Settings.HeapSize : Settings.HeapSize + Settings.StackSize - 1;

    readonly BytecodeInterpreterSettings Settings;
    public Registers Registers;
    ExternalFunctionAsyncReturnChecker? PendingExternalFunction;

    public readonly Span<byte> Memory;
    public readonly ReadOnlySpan<Instruction> Code;
    public readonly ReadOnlySpan<IExternalFunction> ExternalFunctions;
    public readonly ReadOnlySpan<ExternalFunctionScopedSync> ScopedExternalFunctions;

    public ProcessorState(
        BytecodeInterpreterSettings settings,
        Registers registers,
        Span<byte> memory,
        ReadOnlySpan<Instruction> code,
        ReadOnlySpan<IExternalFunction> externalFunctions,
        ReadOnlySpan<ExternalFunctionScopedSync> scopedExternalFunctions)
    {
        Settings = settings;
        Registers = registers;
        Memory = memory;
        Code = code;
        ExternalFunctions = externalFunctions;
        PendingExternalFunction = null;
        ScopedExternalFunctions = scopedExternalFunctions;
    }

    public void Setup()
    {
        Registers.StackPointer = StackStart - StackDirection;
    }

#if UNITY
    public readonly RuntimeContext GetContext() => new(
        Registers,
        new Unity.Collections.NativeArray<byte>(Memory.ToArray(), Unity.Collections.Allocator.Temp),
        new Unity.Collections.NativeArray<Instruction>(Code.ToArray(), Unity.Collections.Allocator.Temp),
        StackStart
    );
#else
    public readonly RuntimeContext GetContext() => new(
        Registers,
        ImmutableArray.Create(Memory),
        ImmutableArray.Create(Code),
        StackStart
    );
#endif

#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    void Step() => Registers.CodePointer++;
#if UNITY
    [Unity.Burst.BurstCompile]
#endif
    void Step(int num) => Registers.CodePointer += num;

#if UNITY
    // [Unity.Burst.BurstCompile]
#endif
    public void Tick()
    {
        if (PendingExternalFunction != null)
        {
            if (PendingExternalFunction.Invoke(ref this, out ReadOnlySpan<byte> ret))
            {
                Push(ret);
                PendingExternalFunction = null;
            }
        }

        if (IsDone) return;

        try
        {
            Process();
        }
        catch (RuntimeException error)
        {
            error.Context = GetContext();
            throw;
        }
    }

#if UNITY
    // [Unity.Burst.BurstCompile]
#endif
    void Process()
    {
        switch (CurrentInstruction.Opcode)
        {
            case Opcode.NOP: break;

            case Opcode.Exit: EXIT(); break;

            case Opcode.Push: PUSH_VALUE(); break;
            case Opcode.Pop8: POP_VALUE(BitWidth._8); break;
            case Opcode.Pop16: POP_VALUE(BitWidth._16); break;
            case Opcode.Pop32: POP_VALUE(BitWidth._32); break;
            case Opcode.Pop64: POP_VALUE(BitWidth._64); break;
            case Opcode.PopTo8: POP_TO_VALUE(BitWidth._8); break;
            case Opcode.PopTo16: POP_TO_VALUE(BitWidth._16); break;
            case Opcode.PopTo32: POP_TO_VALUE(BitWidth._32); break;
            case Opcode.PopTo64: POP_TO_VALUE(BitWidth._64); break;

            case Opcode.Jump: JUMP_BY(); break;
            case Opcode.Crash: CRASH(); break;

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

            case Opcode.UMathAdd: UMathAdd(); break;
            case Opcode.UMathSub: UMathSub(); break;
            case Opcode.UMathMult: UMathMult(); break;
            case Opcode.UMathDiv: UMathDiv(); break;
            case Opcode.UMathMod: UMathMod(); break;

            case Opcode.FMathAdd: FMathAdd(); break;
            case Opcode.FMathSub: FMathSub(); break;
            case Opcode.FMathMult: FMathMult(); break;
            case Opcode.FMathDiv: FMathDiv(); break;
            case Opcode.FMathMod: FMathMod(); break;

            case Opcode.Compare: Compare(); break;
            case Opcode.CompareF: CompareF(); break;

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

    public readonly bool ResolveAddress(InstructionOperand operand, out int address)
    {
        switch (operand.Type)
        {
            case InstructionOperandType.Pointer8:
            case InstructionOperandType.Pointer16:
            case InstructionOperandType.Pointer32:
                address = operand.Int;
                return true;
            case InstructionOperandType.PointerBP8:
            case InstructionOperandType.PointerBP16:
            case InstructionOperandType.PointerBP32:
            case InstructionOperandType.PointerBP64:
                address = Registers.BasePointer + operand.Int;
                return true;
            case InstructionOperandType.PointerSP8:
            case InstructionOperandType.PointerSP16:
            case InstructionOperandType.PointerSP32:
                address = Registers.StackPointer + operand.Int;
                return true;
            case InstructionOperandType.PointerEAX8:
            case InstructionOperandType.PointerEAX16:
            case InstructionOperandType.PointerEAX32:
            case InstructionOperandType.PointerEAX64:
                address = Registers.EAX + operand.Int;
                return true;
            case InstructionOperandType.PointerEBX8:
            case InstructionOperandType.PointerEBX16:
            case InstructionOperandType.PointerEBX32:
            case InstructionOperandType.PointerEBX64:
                address = Registers.EBX + operand.Int;
                return true;
            case InstructionOperandType.PointerECX8:
            case InstructionOperandType.PointerECX16:
            case InstructionOperandType.PointerECX32:
            case InstructionOperandType.PointerECX64:
                address = Registers.ECX + operand.Int;
                return true;
            case InstructionOperandType.PointerEDX8:
            case InstructionOperandType.PointerEDX16:
            case InstructionOperandType.PointerEDX32:
            case InstructionOperandType.PointerEDX64:
                address = Registers.EDX + operand.Int;
                return true;

            // NOTE: There is no R_X registers so I used E_X
            case InstructionOperandType.PointerRAX8:
            case InstructionOperandType.PointerRAX16:
            case InstructionOperandType.PointerRAX32:
            case InstructionOperandType.PointerRAX64:
                address = Registers.EAX + operand.Int;
                return true;
            case InstructionOperandType.PointerRBX8:
            case InstructionOperandType.PointerRBX16:
            case InstructionOperandType.PointerRBX32:
            case InstructionOperandType.PointerRBX64:
                address = Registers.EBX + operand.Int;
                return true;
            case InstructionOperandType.PointerRCX8:
            case InstructionOperandType.PointerRCX16:
            case InstructionOperandType.PointerRCX32:
            case InstructionOperandType.PointerRCX64:
                address = Registers.ECX + operand.Int;
                return true;
            case InstructionOperandType.PointerRDX8:
            case InstructionOperandType.PointerRDX16:
            case InstructionOperandType.PointerRDX32:
            case InstructionOperandType.PointerRDX64:
                address = Registers.EDX + operand.Int;
                return true;
            default:
                address = default;
                return false;
        }
    }

    public readonly void SetData(int ptr, RuntimeValue data, BitWidth size) => Memory.SetData(ptr, data, size);
    public readonly RuntimeValue GetData(int ptr, BitWidth size) => Memory.GetData(ptr, size);

    public readonly void SetData8(int ptr, byte data) => Memory.Set(ptr, data);
    public readonly byte GetData8(int ptr) => Memory.Get<byte>(ptr);

    public readonly void SetData16(int ptr, char data) => Memory.Set(ptr, data);
    public readonly char GetData16(int ptr) => Memory.Get<char>(ptr);

    public readonly void SetData32(int ptr, int data) => Memory.Set(ptr, data);
    public readonly int GetData32(int ptr) => Memory.Get<int>(ptr);

    readonly RuntimeValue GetData(InstructionOperand operand) => operand.Type switch
    {
        InstructionOperandType.Immediate8 => operand.Value,
        InstructionOperandType.Immediate16 => operand.Value,
        InstructionOperandType.Immediate32 => operand.Value,
        InstructionOperandType.Immediate64 => operand.Value,
        InstructionOperandType.Pointer8 => GetData8(operand.Int),
        InstructionOperandType.Pointer16 => GetData16(operand.Int),
        InstructionOperandType.Pointer32 => GetData32(operand.Int),
        InstructionOperandType.PointerBP8 => GetData8(Registers.BasePointer + operand.Int),
        InstructionOperandType.PointerBP16 => GetData16(Registers.BasePointer + operand.Int),
        InstructionOperandType.PointerBP32 => GetData32(Registers.BasePointer + operand.Int),
        InstructionOperandType.PointerBP64 => throw new NotImplementedException(),
        InstructionOperandType.PointerSP8 => GetData8(Registers.StackPointer + operand.Int),
        InstructionOperandType.PointerSP16 => GetData16(Registers.StackPointer + operand.Int),
        InstructionOperandType.PointerSP32 => GetData32(Registers.StackPointer + operand.Int),

        InstructionOperandType.PointerEAX8 => GetData8(Registers.EAX + operand.Int),
        InstructionOperandType.PointerEAX16 => GetData16(Registers.EAX + operand.Int),
        InstructionOperandType.PointerEAX32 => GetData32(Registers.EAX + operand.Int),
        InstructionOperandType.PointerEAX64 => throw new NotImplementedException(),
        InstructionOperandType.PointerEBX8 => GetData8(Registers.EBX + operand.Int),
        InstructionOperandType.PointerEBX16 => GetData16(Registers.EBX + operand.Int),
        InstructionOperandType.PointerEBX32 => GetData32(Registers.EBX + operand.Int),
        InstructionOperandType.PointerEBX64 => throw new NotImplementedException(),
        InstructionOperandType.PointerECX8 => GetData8(Registers.ECX + operand.Int),
        InstructionOperandType.PointerECX16 => GetData16(Registers.ECX + operand.Int),
        InstructionOperandType.PointerECX32 => GetData32(Registers.ECX + operand.Int),
        InstructionOperandType.PointerECX64 => throw new NotImplementedException(),
        InstructionOperandType.PointerEDX8 => GetData8(Registers.EDX + operand.Int),
        InstructionOperandType.PointerEDX16 => GetData16(Registers.EDX + operand.Int),
        InstructionOperandType.PointerEDX32 => GetData32(Registers.EDX + operand.Int),
        InstructionOperandType.PointerEDX64 => throw new NotImplementedException(),

        // FIXME: Same
        InstructionOperandType.PointerRAX8 => GetData8(Registers.EAX + operand.Int),
        InstructionOperandType.PointerRAX16 => GetData16(Registers.EAX + operand.Int),
        InstructionOperandType.PointerRAX32 => GetData32(Registers.EAX + operand.Int),
        InstructionOperandType.PointerRAX64 => throw new NotImplementedException(),
        InstructionOperandType.PointerRBX8 => GetData8(Registers.EBX + operand.Int),
        InstructionOperandType.PointerRBX16 => GetData16(Registers.EBX + operand.Int),
        InstructionOperandType.PointerRBX32 => GetData32(Registers.EBX + operand.Int),
        InstructionOperandType.PointerRBX64 => throw new NotImplementedException(),
        InstructionOperandType.PointerRCX8 => GetData8(Registers.ECX + operand.Int),
        InstructionOperandType.PointerRCX16 => GetData16(Registers.ECX + operand.Int),
        InstructionOperandType.PointerRCX32 => GetData32(Registers.ECX + operand.Int),
        InstructionOperandType.PointerRCX64 => throw new NotImplementedException(),
        InstructionOperandType.PointerRDX8 => GetData8(Registers.EDX + operand.Int),
        InstructionOperandType.PointerRDX16 => GetData16(Registers.EDX + operand.Int),
        InstructionOperandType.PointerRDX32 => GetData32(Registers.EDX + operand.Int),
        InstructionOperandType.PointerRDX64 => throw new NotImplementedException(),
        InstructionOperandType.Register => (Register)operand.Int switch
        {
            Register.CodePointer => new RuntimeValue(Registers.CodePointer),
            Register.StackPointer => new RuntimeValue(Registers.StackPointer),
            Register.BasePointer => new RuntimeValue(Registers.BasePointer),
            Register.EAX => new RuntimeValue(Registers.EAX),
            Register.AX => new RuntimeValue(Registers.AX),
            Register.AH => new RuntimeValue(Registers.AH),
            Register.AL => new RuntimeValue(Registers.AL),
            Register.EBX => new RuntimeValue(Registers.EBX),
            Register.BX => new RuntimeValue(Registers.BX),
            Register.BH => new RuntimeValue(Registers.BH),
            Register.BL => new RuntimeValue(Registers.BL),
            Register.ECX => new RuntimeValue(Registers.ECX),
            Register.CX => new RuntimeValue(Registers.CX),
            Register.CH => new RuntimeValue(Registers.CH),
            Register.CL => new RuntimeValue(Registers.CL),
            Register.EDX => new RuntimeValue(Registers.EDX),
            Register.DX => new RuntimeValue(Registers.DX),
            Register.DH => new RuntimeValue(Registers.DH),
            Register.DL => new RuntimeValue(Registers.DL),
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
            case InstructionOperandType.Immediate64:
                throw new RuntimeException($"Can't set an immediate value");
            case InstructionOperandType.Pointer8: SetData(operand.Int, value, BitWidth._8); break;
            case InstructionOperandType.Pointer16: SetData(operand.Int, value, BitWidth._16); break;
            case InstructionOperandType.Pointer32: SetData(operand.Int, value, BitWidth._32); break;
            case InstructionOperandType.PointerBP8: SetData8(Registers.BasePointer + operand.Int, value.U8); break;
            case InstructionOperandType.PointerBP16: SetData16(Registers.BasePointer + operand.Int, value.U16); break;
            case InstructionOperandType.PointerBP32: SetData32(Registers.BasePointer + operand.Int, value.I32); break;
            case InstructionOperandType.PointerBP64: throw new NotImplementedException();
            case InstructionOperandType.PointerSP8: SetData8(Registers.StackPointer + operand.Int, value.U8); break;
            case InstructionOperandType.PointerSP16: SetData16(Registers.StackPointer + operand.Int, value.U16); break;
            case InstructionOperandType.PointerSP32: SetData32(Registers.StackPointer + operand.Int, value.I32); break;

            case InstructionOperandType.PointerEAX8: SetData8(Registers.EAX + operand.Int, value.U8); break;
            case InstructionOperandType.PointerEAX16: SetData16(Registers.EAX + operand.Int, value.U16); break;
            case InstructionOperandType.PointerEAX32: SetData32(Registers.EAX + operand.Int, value.I32); break;
            case InstructionOperandType.PointerEAX64: throw new NotImplementedException();
            case InstructionOperandType.PointerEBX8: SetData8(Registers.EBX + operand.Int, value.U8); break;
            case InstructionOperandType.PointerEBX16: SetData16(Registers.EBX + operand.Int, value.U16); break;
            case InstructionOperandType.PointerEBX32: SetData32(Registers.EBX + operand.Int, value.I32); break;
            case InstructionOperandType.PointerEBX64: throw new NotImplementedException();
            case InstructionOperandType.PointerECX8: SetData8(Registers.ECX + operand.Int, value.U8); break;
            case InstructionOperandType.PointerECX16: SetData16(Registers.ECX + operand.Int, value.U16); break;
            case InstructionOperandType.PointerECX32: SetData32(Registers.ECX + operand.Int, value.I32); break;
            case InstructionOperandType.PointerECX64: throw new NotImplementedException();
            case InstructionOperandType.PointerEDX8: SetData8(Registers.EDX + operand.Int, value.U8); break;
            case InstructionOperandType.PointerEDX16: SetData16(Registers.EDX + operand.Int, value.U16); break;
            case InstructionOperandType.PointerEDX32: SetData32(Registers.EDX + operand.Int, value.I32); break;
            case InstructionOperandType.PointerEDX64: throw new NotImplementedException();

            // FIXME: Same
            case InstructionOperandType.PointerRAX8: SetData8(Registers.EAX + operand.Int, value.U8); break;
            case InstructionOperandType.PointerRAX16: SetData16(Registers.EAX + operand.Int, value.U16); break;
            case InstructionOperandType.PointerRAX32: SetData32(Registers.EAX + operand.Int, value.I32); break;
            case InstructionOperandType.PointerRAX64: throw new NotImplementedException();
            case InstructionOperandType.PointerRBX8: SetData8(Registers.EBX + operand.Int, value.U8); break;
            case InstructionOperandType.PointerRBX16: SetData16(Registers.EBX + operand.Int, value.U16); break;
            case InstructionOperandType.PointerRBX32: SetData32(Registers.EBX + operand.Int, value.I32); break;
            case InstructionOperandType.PointerRBX64: throw new NotImplementedException();
            case InstructionOperandType.PointerRCX8: SetData8(Registers.ECX + operand.Int, value.U8); break;
            case InstructionOperandType.PointerRCX16: SetData16(Registers.ECX + operand.Int, value.U16); break;
            case InstructionOperandType.PointerRCX32: SetData32(Registers.ECX + operand.Int, value.I32); break;
            case InstructionOperandType.PointerRCX64: throw new NotImplementedException();
            case InstructionOperandType.PointerRDX8: SetData8(Registers.EDX + operand.Int, value.U8); break;
            case InstructionOperandType.PointerRDX16: SetData16(Registers.EDX + operand.Int, value.U16); break;
            case InstructionOperandType.PointerRDX32: SetData32(Registers.EDX + operand.Int, value.I32); break;
            case InstructionOperandType.PointerRDX64: throw new NotImplementedException();

            case InstructionOperandType.Register:
                switch ((Register)operand.Int)
                {
                    case Register.CodePointer:
                        Registers.CodePointer = value.I32;
                        break;
                    case Register.StackPointer:
                        Registers.StackPointer = value.I32;
                        break;
                    case Register.BasePointer:
                        Registers.BasePointer = value.I32;
                        break;
                    case Register.EAX:
                        Registers.EAX = value.I32;
                        break;
                    case Register.AX:
                        Registers.AX = value.U16;
                        break;
                    case Register.AH:
                        Registers.AH = value.U8;
                        break;
                    case Register.AL:
                        Registers.AL = value.U8;
                        break;
                    case Register.EBX:
                        Registers.EBX = value.I32;
                        break;
                    case Register.BX:
                        Registers.BX = value.U16;
                        break;
                    case Register.BH:
                        Registers.BH = value.U8;
                        break;
                    case Register.BL:
                        Registers.BL = value.U8;
                        break;
                    case Register.ECX:
                        Registers.ECX = value.I32;
                        break;
                    case Register.CX:
                        Registers.CX = value.U16;
                        break;
                    case Register.CH:
                        Registers.CH = value.U8;
                        break;
                    case Register.CL:
                        Registers.CL = value.U8;
                        break;
                    case Register.EDX:
                        Registers.EDX = value.I32;
                        break;
                    case Register.DX:
                        Registers.DX = value.U16;
                        break;
                    case Register.DH:
                        Registers.DH = value.U8;
                        break;
                    case Register.DL:
                        Registers.DL = value.U8;
                        break;
                    default: throw new UnreachableException();
                }
                break;
            default: throw new UnreachableException();
        }
    }

    void Push(RuntimeValue data, BitWidth size)
    {
        Registers.StackPointer += (int)size * StackDirection;
        SetData(Registers.StackPointer, data, size);

        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext(), null);
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext(), null);
    }

    RuntimeValue Pop(BitWidth size)
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext(), null);
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext(), null);

        RuntimeValue data = GetData(Registers.StackPointer, size);
        Registers.StackPointer -= (int)size * StackDirection;
        return data;
    }

    void Push(scoped ReadOnlySpan<byte> data)
    {
        Registers.StackPointer += data.Length * StackDirection;
        Memory.Set(Registers.StackPointer, data);

        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext(), null);
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext(), null);
    }

    Span<byte> Pop(int size)
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext(), null);
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext(), null);

        Span<byte> data = Memory.Get(Registers.StackPointer, size);
        Registers.StackPointer -= size * StackDirection;
        return data;
    }
}
