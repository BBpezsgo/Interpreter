namespace LanguageCore.Runtime;

public partial class BytecodeProcessor
{
    public const int StackDirection = -1;

    public Instruction? NextInstruction
    {
        get
        {
            if (Registers.CodePointer < 0 || Registers.CodePointer >= Code.Length) return null;
            return Code[Registers.CodePointer];
        }
    }
    public bool IsDone => Registers.CodePointer == Code.Length;
    public int StackStart => StackDirection > 0 ? Settings.HeapSize : Settings.HeapSize + Settings.StackSize - 1;

    readonly BytecodeInterpreterSettings Settings;
    public Registers Registers;
    public readonly byte[] Memory;
    public ImmutableArray<Instruction> Code;
    readonly FrozenDictionary<int, IExternalFunction> ExternalFunctions;

    public BytecodeProcessor(ImmutableArray<Instruction> code, byte[]? memory, FrozenDictionary<int, IExternalFunction> externalFunctions, BytecodeInterpreterSettings settings)
    {
        Settings = settings;
        ExternalFunctions = externalFunctions;
        Code = code;
        Memory = memory ?? new byte[settings.HeapSize + settings.StackSize];
        Registers.StackPointer = StackStart - StackDirection;
    }

    public void Tick()
    {
        ProcessorState state = new(
            Settings,
            Registers,
            Memory,
            Code.AsSpan(),
            ExternalFunctions.Values.AsSpan()
        );

        state.Tick();

        Registers = state.Registers;
    }

    public bool ResolveAddress(InstructionOperand operand, out int address)
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

#if UNITY
    public RuntimeContext GetContext() => new(
        Registers,
        new Unity.Collections.NativeArray<byte>(Memory.ToArray(), Unity.Collections.Allocator.Temp),
        new Unity.Collections.NativeArray<Instruction>(Code.ToArray(), Unity.Collections.Allocator.Temp),
        StackStart
    );
#else
    public RuntimeContext GetContext() => new(
        Registers,
        ImmutableArray.Create(Memory),
        Code,
        StackStart
    );
#endif
}
