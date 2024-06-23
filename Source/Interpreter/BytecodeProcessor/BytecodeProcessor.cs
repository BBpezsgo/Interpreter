using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public partial class BytecodeProcessor
{
    public static int StackDirection => -1;
    public static int StackPointerOffset => 1;
    public static int PointerSize => 1;

    Instruction CurrentInstruction => Code[Registers.CodePointer];
    public bool IsDone => Registers.CodePointer >= Code.Length;
    public int StackStart => StackDirection > 0 ? Settings.HeapSize : Memory.Length - 1;

    readonly BytecodeInterpreterSettings Settings;
    public Registers Registers;
    public readonly RuntimeValue[] Memory;
    public ImmutableArray<Instruction> Code;
    readonly FrozenDictionary<int, ExternalFunctionBase> ExternalFunctions;

    public IEnumerable<RuntimeValue> GetStack()
    {
        if (StackDirection > 0)
        { return new ArraySegment<RuntimeValue>(Memory)[StackStart..Registers.StackPointer]; }
        else
        { return new ArraySegment<RuntimeValue>(Memory)[(Registers.StackPointer + 1)..].Reverse(); }
    }

    public Range<int> GetStackInterval(out bool isReversed)
    {
        isReversed = StackDirection <= 0;
        return new Range<int>(StackStart, Registers.StackPointer);
    }

    public BytecodeProcessor(ImmutableArray<Instruction> code, FrozenDictionary<int, ExternalFunctionBase> externalFunctions, BytecodeInterpreterSettings settings)
    {
        Settings = settings;
        ExternalFunctions = externalFunctions;

        Code = code;

        Memory = new RuntimeValue[settings.HeapSize + settings.StackSize];

        Registers.StackPointer = StackStart;

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
            case Opcode.Pop8: POP_VALUE(BitWidth._32); break;
            case Opcode.Pop16: POP_VALUE(BitWidth._32); break;
            case Opcode.Pop32: POP_VALUE(BitWidth._32); break;
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

    public bool ResolveAddress(InstructionOperand operand, out int address)
    {
        switch (operand.Type)
        {
            case InstructionOperandType.Pointer8:
            case InstructionOperandType.Pointer16:
            case InstructionOperandType.Pointer32:
                address = operand.Value.Int;
                return true;
            case InstructionOperandType.PointerBP8:
            case InstructionOperandType.PointerBP16:
            case InstructionOperandType.PointerBP32:
                address = Registers.BasePointer + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerSP8:
            case InstructionOperandType.PointerSP16:
            case InstructionOperandType.PointerSP32:
                address = Registers.StackPointer + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerEAX8:
            case InstructionOperandType.PointerEAX16:
            case InstructionOperandType.PointerEAX32:
                address = Registers.EAX + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerEBX8:
            case InstructionOperandType.PointerEBX16:
            case InstructionOperandType.PointerEBX32:
                address = Registers.EBX + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerECX8:
            case InstructionOperandType.PointerECX16:
            case InstructionOperandType.PointerECX32:
                address = Registers.ECX + operand.Value.Int;
                return true;
            case InstructionOperandType.PointerEDX8:
            case InstructionOperandType.PointerEDX16:
            case InstructionOperandType.PointerEDX32:
                address = Registers.EDX + operand.Value.Int;
                return true;
            default:
                address = default;
                return false;
        }
    }

    // public void SetDat(int ptr, in RuntimeValue data) => MemoryMarshal.Write(Memory.AsSpan()[ptr..], in data);
    // public RuntimeValue GetData(int ptr) => MemoryMarshal.Read<RuntimeValue>(Memory.AsSpan()[ptr..]);

    public void SetData(int ptr, RuntimeValue data) => Memory[ptr] = data;
    public RuntimeValue GetData(int ptr) => Memory[ptr];

    public void SetData8(int ptr, RuntimeValue data)
    {
        Memory[ptr + 0] = data.Byte0;
    }
    public RuntimeValue GetData8(int ptr) => new(
        Memory[ptr + 0].Byte0,
        default,
        default,
        default
    );

    public void SetData16(int ptr, RuntimeValue data)
    {
        Memory[ptr + 0] = data.Byte0;
        Memory[ptr + 1] = data.Byte1;
    }
    public RuntimeValue GetData16(int ptr) => new(
        Memory[ptr + 0].Byte0,
        Memory[ptr + 1].Byte0,
        default,
        default
    );

    public void SetData32(int ptr, RuntimeValue data)
    {
        Memory[ptr + 0] = data.Byte0;
        Memory[ptr + 1] = data.Byte0;
        Memory[ptr + 2] = data.Byte0;
        Memory[ptr + 3] = data.Byte0;
    }
    public RuntimeValue GetData32(int ptr) => new(
         Memory[ptr + 0].Byte0,
         Memory[ptr + 1].Byte0,
         Memory[ptr + 2].Byte0,
         Memory[ptr + 3].Byte0
     );

    RuntimeValue GetData(InstructionOperand operand) => operand.Type switch
    {
        InstructionOperandType.Immediate8 => operand.Value,
        InstructionOperandType.Immediate16 => operand.Value,
        InstructionOperandType.Immediate32 => operand.Value,
        InstructionOperandType.Pointer8 => GetData(operand.Value.Int),
        InstructionOperandType.Pointer16 => GetData(operand.Value.Int),
        InstructionOperandType.Pointer32 => GetData(operand.Value.Int),
        InstructionOperandType.PointerBP8 => GetData(Registers.BasePointer + operand.Value.Int),
        InstructionOperandType.PointerBP16 => GetData(Registers.BasePointer + operand.Value.Int),
        InstructionOperandType.PointerBP32 => GetData(Registers.BasePointer + operand.Value.Int),
        InstructionOperandType.PointerSP8 => GetData(Registers.StackPointer + operand.Value.Int),
        InstructionOperandType.PointerSP16 => GetData(Registers.StackPointer + operand.Value.Int),
        InstructionOperandType.PointerSP32 => GetData(Registers.StackPointer + operand.Value.Int),
        InstructionOperandType.PointerEAX8 => GetData(Registers.EAX + operand.Value.Int),
        InstructionOperandType.PointerEAX16 => GetData(Registers.EAX + operand.Value.Int),
        InstructionOperandType.PointerEAX32 => GetData(Registers.EAX + operand.Value.Int),
        InstructionOperandType.PointerEBX8 => GetData(Registers.EBX + operand.Value.Int),
        InstructionOperandType.PointerEBX16 => GetData(Registers.EBX + operand.Value.Int),
        InstructionOperandType.PointerEBX32 => GetData(Registers.EBX + operand.Value.Int),
        InstructionOperandType.PointerECX8 => GetData(Registers.ECX + operand.Value.Int),
        InstructionOperandType.PointerECX16 => GetData(Registers.ECX + operand.Value.Int),
        InstructionOperandType.PointerECX32 => GetData(Registers.ECX + operand.Value.Int),
        InstructionOperandType.PointerEDX8 => GetData(Registers.EDX + operand.Value.Int),
        InstructionOperandType.PointerEDX16 => GetData(Registers.EDX + operand.Value.Int),
        InstructionOperandType.PointerEDX32 => GetData(Registers.EDX + operand.Value.Int),
        InstructionOperandType.Register => (Register)operand.Value.Int switch
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
                throw new RuntimeException($"Can't set an immediate value");
            case InstructionOperandType.Pointer8: SetData(operand.Value.Int, value); break;
            case InstructionOperandType.Pointer16: SetData(operand.Value.Int, value); break;
            case InstructionOperandType.Pointer32: SetData(operand.Value.Int, value); break;
            case InstructionOperandType.PointerBP8: SetData(Registers.BasePointer + operand.Value.Int, value); break;
            case InstructionOperandType.PointerBP16: SetData(Registers.BasePointer + operand.Value.Int, value); break;
            case InstructionOperandType.PointerBP32: SetData(Registers.BasePointer + operand.Value.Int, value); break;
            case InstructionOperandType.PointerSP8: SetData(Registers.StackPointer + operand.Value.Int, value); break;
            case InstructionOperandType.PointerSP16: SetData(Registers.StackPointer + operand.Value.Int, value); break;
            case InstructionOperandType.PointerSP32: SetData(Registers.StackPointer + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEAX8: SetData(Registers.EAX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEAX16: SetData(Registers.EAX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEAX32: SetData(Registers.EAX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEBX8: SetData(Registers.EBX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEBX16: SetData(Registers.EBX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEBX32: SetData(Registers.EBX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerECX8: SetData(Registers.ECX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerECX16: SetData(Registers.ECX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerECX32: SetData(Registers.ECX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEDX8: SetData(Registers.EDX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEDX16: SetData(Registers.EDX + operand.Value.Int, value); break;
            case InstructionOperandType.PointerEDX32: SetData(Registers.EDX + operand.Value.Int, value); break;
            case InstructionOperandType.Register:
                switch ((Register)operand.Value.Int)
                {
                    case Register.CodePointer:
                        Registers.CodePointer = value.Int;
                        break;
                    case Register.StackPointer:
                        Registers.StackPointer = value.Int;
                        break;
                    case Register.BasePointer:
                        Registers.BasePointer = value.Int;
                        break;
                    case Register.EAX:
                        Registers.EAX = value.Int;
                        break;
                    case Register.AX:
                        Registers.AX = value.Char;
                        break;
                    case Register.AH:
                        Registers.AH = value.Byte;
                        break;
                    case Register.AL:
                        Registers.AL = value.Byte;
                        break;
                    case Register.EBX:
                        Registers.EBX = value.Int;
                        break;
                    case Register.BX:
                        Registers.BX = value.Char;
                        break;
                    case Register.BH:
                        Registers.BH = value.Byte;
                        break;
                    case Register.BL:
                        Registers.BL = value.Byte;
                        break;
                    case Register.ECX:
                        Registers.ECX = value.Int;
                        break;
                    case Register.CX:
                        Registers.CX = value.Char;
                        break;
                    case Register.CH:
                        Registers.CH = value.Byte;
                        break;
                    case Register.CL:
                        Registers.CL = value.Byte;
                        break;
                    case Register.EDX:
                        Registers.EDX = value.Int;
                        break;
                    case Register.DX:
                        Registers.DX = value.Char;
                        break;
                    case Register.DH:
                        Registers.DH = value.Byte;
                        break;
                    case Register.DL:
                        Registers.DL = value.Byte;
                        break;
                    default: throw new UnreachableException();
                }
                break;
            default: throw new UnreachableException();
        }
    }

    void Push(RuntimeValue data, BitWidth size = BitWidth._32)
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext());
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext());

        Registers.StackPointer += StackDirection;
        SetData(Registers.StackPointer, data);
    }

    RuntimeValue Pop(BitWidth size = BitWidth._32)
    {
        if (Registers.StackPointer >= Memory.Length) throw new RuntimeException("Stack overflow", GetContext());
        if (Registers.StackPointer < 0) throw new RuntimeException("Stack underflow", GetContext());

        RuntimeValue data = GetData(Registers.StackPointer);
        Registers.StackPointer -= StackDirection;
        return data;
    }
}
