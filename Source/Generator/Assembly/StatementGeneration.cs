using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.Native.Generator;

/*
[ number ]
[ reg ]
[ reg + reg*scale ]      scale is 1, 2, 4, or 8 only
[ reg + number ]
[ reg + reg*scale + number ] 
*/

public enum Registers
{
    RAX, EAX, AX, AH, AL,
    RBX, EBX, BX, BH, BL,
    RCX, ECX, CX, CH, CL,
    RDX, EDX, DX, DH, DL,
    RSI, ESI, SI, SIL,
    RDI, EDI, DI, DIL,
    RSP, ESP, SP, SPL,
    RBP, EBP, BP, BPL,
    R8, R8D, R8W, R8B,
    R9, R9D, R9W, R9B,
    R10, R10D, R10W, R10B,
    R11, R11D, R11W, R11B,
    R12, R12D, R12W, R12B,
    R13, R13D, R13W, R13B,
    R14, R14D, R14W, R14B,
    R15, R15D, R15W, R15B,
}

public enum RegisterIdentifier : byte
{
    AX,
    BX,
    CX,
    DX,
    SI,
    DI,
    SP,
    BP,
    _8,
    _9,
    _10,
    _11,
    _12,
    _13,
    _14,
    _15,
}

public enum RegisterSlice : byte
{
    R,
    D,
    W,
    H,
    L,
}

public readonly struct Register : IEquatable<Register>
{
    public static readonly Register EAX = new(RegisterIdentifier.AX, RegisterSlice.D);
    public static readonly Register EBX = new(RegisterIdentifier.BX, RegisterSlice.D);
    public static readonly Register ECX = new(RegisterIdentifier.CX, RegisterSlice.D);
    public static readonly Register EDX = new(RegisterIdentifier.DX, RegisterSlice.D);
    public static readonly Register EBP = new(RegisterIdentifier.BP, RegisterSlice.D);
    public static readonly Register ESP = new(RegisterIdentifier.SP, RegisterSlice.D);

    public readonly RegisterIdentifier Identifier;
    public readonly RegisterSlice Slice;

    public Register(RegisterIdentifier identifier, RegisterSlice slice)
    {
        Identifier = identifier;
        Slice = slice;
    }

    public override bool Equals(object? obj) => obj is Register other && Equals(other);
    public bool Equals(Register other) => Identifier == other.Identifier && Slice == other.Slice;
    public override int GetHashCode() => HashCode.Combine(Identifier, Slice);

    public static bool operator ==(Register left, Register right) => left.Equals(right);
    public static bool operator !=(Register left, Register right) => !left.Equals(right);

    public override string ToString()
    {
        return Identifier switch
        {
            RegisterIdentifier.AX => Slice switch
            {
                RegisterSlice.R => "RAX",
                RegisterSlice.D => "EAX",
                RegisterSlice.W => "AX",
                RegisterSlice.H => "AH",
                RegisterSlice.L => "AL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.BX => Slice switch
            {
                RegisterSlice.R => "RBX",
                RegisterSlice.D => "EBX",
                RegisterSlice.W => "BX",
                RegisterSlice.H => "BH",
                RegisterSlice.L => "BL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.CX => Slice switch
            {
                RegisterSlice.R => "RCX",
                RegisterSlice.D => "ECX",
                RegisterSlice.W => "CX",
                RegisterSlice.H => "CH",
                RegisterSlice.L => "CL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.DX => Slice switch
            {
                RegisterSlice.R => "RDX",
                RegisterSlice.D => "EDX",
                RegisterSlice.W => "DX",
                RegisterSlice.H => "DH",
                RegisterSlice.L => "DL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.SI => Slice switch
            {
                RegisterSlice.R => "RSI",
                RegisterSlice.D => "ESI",
                RegisterSlice.W => "SI",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "SIL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.DI => Slice switch
            {
                RegisterSlice.R => "RDI",
                RegisterSlice.D => "EDI",
                RegisterSlice.W => "DI",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "DIL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.SP => Slice switch
            {
                RegisterSlice.R => "RSP",
                RegisterSlice.D => "ESP",
                RegisterSlice.W => "SP",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "SPL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier.BP => Slice switch
            {
                RegisterSlice.R => "RBP",
                RegisterSlice.D => "EBP",
                RegisterSlice.W => "BP",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "BPL",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier._8 => Slice switch
            {
                RegisterSlice.R => "R8",
                RegisterSlice.D => "R8D",
                RegisterSlice.W => "R8W",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "R8B",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier._9 => Slice switch
            {
                RegisterSlice.R => "R9",
                RegisterSlice.D => "R9D",
                RegisterSlice.W => "R9W",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "R9B",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier._10 => Slice switch
            {
                RegisterSlice.R => "R10",
                RegisterSlice.D => "R10D",
                RegisterSlice.W => "R10W",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "R10B",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier._11 => Slice switch
            {
                RegisterSlice.R => "R11",
                RegisterSlice.D => "R11D",
                RegisterSlice.W => "R11W",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "R11B",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier._12 => Slice switch
            {
                RegisterSlice.R => "R12",
                RegisterSlice.D => "R12D",
                RegisterSlice.W => "R12W",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "R12B",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier._13 => Slice switch
            {
                RegisterSlice.R => "R13",
                RegisterSlice.D => "R13D",
                RegisterSlice.W => "R13W",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "R13B",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier._14 => Slice switch
            {
                RegisterSlice.R => "R14",
                RegisterSlice.D => "R14D",
                RegisterSlice.W => "R14W",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "R14B",
                _ => throw new UnreachableException(),
            },
            RegisterIdentifier._15 => Slice switch
            {
                RegisterSlice.R => "R15",
                RegisterSlice.D => "R15D",
                RegisterSlice.W => "R15W",
                RegisterSlice.H => throw new InvalidOperationException(),
                RegisterSlice.L => "R15B",
                _ => throw new UnreachableException(),
            },
            _ => throw new UnreachableException(),
        };
    }

    public bool Overlaps(Register other)
    {
        if (Identifier != other.Identifier)
        {
            return false;
        }

        return Slice switch
        {
            RegisterSlice.R => true,
            RegisterSlice.D => true,
            RegisterSlice.W => true,
            RegisterSlice.H => other.Slice is not RegisterSlice.L,
            RegisterSlice.L => other.Slice is not RegisterSlice.H,
            _ => throw new UnreachableException(),
        };
    }
}

public readonly struct InstructionOperand
{
    readonly string _v;

    InstructionOperand(string v) => _v = v;

    public static implicit operator InstructionOperand(string v) => new(v);
    public static implicit operator InstructionOperand(Register v) => new(v.ToString());
    public static implicit operator InstructionOperand(int v) => new(v.ToString());
    public static implicit operator InstructionOperand(CompiledValue v) => new(v.ToStringValue() ?? throw new NullReferenceException());

    public static implicit operator string(InstructionOperand operand) => operand._v;

    public override string ToString() => _v;
}

abstract class ValueLocation
{

}

sealed class ValueRegisterLocation : ValueLocation
{
    public Register Register;

    public ValueRegisterLocation(Register register)
    {
        Register = register;
    }
}

sealed class ValueStackLocation : ValueLocation
{

}

sealed class ValueVirtualLocation : ValueLocation
{
    public CompiledValue Value;

    public ValueVirtualLocation(CompiledValue value)
    {
        Value = value;
    }
}

public partial class CodeGeneratorForNative : CodeGenerator
{
    protected override RuntimeInfo RuntimeInfo => new()
    {
        PointerSize = 4,
    };
    readonly TextSectionBuilder Code = new();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int JitFn();

    readonly HashSet<Register> UsedRegisters = new();
    bool DidReturn;

    class StackFrame
    {
        public readonly Stack<(CompiledVariableDefinition Variable, int Offset)> Variables = new();
    }

    readonly Stack<StackFrame> Frames = new();

    readonly struct AllocatedRegister : IDisposable
    {
        readonly HashSet<Register> UsedRegisters;
        readonly Register Register;

        public AllocatedRegister(HashSet<Register> usedRegisters, Register register)
        {
            UsedRegisters = usedRegisters;
            Register = register;
        }

        public void Dispose()
        {
            UsedRegisters.Remove(Register);
        }

        public override string ToString() => Register.ToString();

        public static implicit operator Register(AllocatedRegister reg) => reg.Register;
    }

    AllocatedRegister AllocateRegister(BitWidth bitWidth)
    {
        if (TryAllocateRegister(bitWidth, out AllocatedRegister register))
        {
            return register;
        }

        throw new InvalidOperationException("No registers available");
    }
    AllocatedRegister AllocateRegister(Register reg)
    {
        if (TryAllocateRegister(reg, out AllocatedRegister register))
        {
            return register;
        }

        throw new InvalidOperationException($"Failed to allocate register {reg}");
    }

    bool TryAllocateRegister(BitWidth bitWidth, out AllocatedRegister register)
    {
        ReadOnlySpan<RegisterSlice> registerSlices = bitWidth switch
        {
            BitWidth._8 => stackalloc[] { RegisterSlice.L, RegisterSlice.H },
            BitWidth._16 => stackalloc[] { RegisterSlice.W },
            BitWidth._32 => stackalloc[] { RegisterSlice.D },
            BitWidth._64 => stackalloc[] { RegisterSlice.R },
            _ => throw new UnreachableException(),
        };

        foreach (RegisterIdentifier identifier in Enum.GetValues(typeof(RegisterIdentifier)))
        {
            for (int i = 0; i < registerSlices.Length; i++)
            {
                Register reg = new(identifier, registerSlices[i]);
                if (TryAllocateRegister(reg, out register))
                {
                    return true;
                }
            }
        }

        register = default;
        return false;
    }
    bool TryAllocateRegister(Register reg, out AllocatedRegister register)
    {
        if (UsedRegisters.Add(reg))
        {
            register = new AllocatedRegister(UsedRegisters, reg);
            return true;
        }

        register = default;
        return false;
    }

    ValueRegisterLocation PutValueIntoRegister(ValueLocation value, Register register)
    {
        if (value is ValueRegisterLocation registerLocation)
        {
            if (registerLocation.Register != register)
            {
                Code.AppendInstruction("mov", register, registerLocation.Register);
            }
            return new ValueRegisterLocation(register);
        }
        else if (value is ValueStackLocation)
        {
            Code.AppendInstruction("pop", register);
            return new ValueRegisterLocation(register);
        }
        else if (value is ValueVirtualLocation virtualLocation)
        {
            Code.AppendInstruction("mov", register, virtualLocation.Value.ToStringValue()!);
            return new ValueRegisterLocation(register);
        }
        else
        {
            throw new UnreachableException();
        }
    }

    ValueStackLocation PushValue(ValueLocation value)
    {
        if (value is ValueRegisterLocation registerLocation)
        {
            Code.AppendInstruction("push", registerLocation.Register);
            return new ValueStackLocation();
        }
        else if (value is ValueStackLocation)
        {
            return new ValueStackLocation();
        }
        else if (value is ValueVirtualLocation virtualLocation)
        {
            Code.AppendInstruction("push", virtualLocation.Value.ToStringValue()!);
            return new ValueStackLocation();
        }
        else
        {
            throw new UnreachableException();
        }
    }

    ValueLocation EmitExpression(CompiledConstantValue statement, Register? dest)
    {
        if (dest.HasValue)
        {
            Code.AppendInstruction("mov", dest.Value, statement.Value);
            return new ValueRegisterLocation(dest.Value);
        }
        return new ValueVirtualLocation(statement.Value);

        //else if (TryAllocateRegister(statement.Value.BitWidth, out AllocatedRegister reg))
        //{
        //    using (reg)
        //    {
        //        Code.AppendInstruction("mov", reg, statement.Value);
        //        return new ValueRegisterLocation(reg);
        //    }
        //}
        //else
        //{
        //    Code.AppendInstruction("push", statement.Value);
        //    return new ValueStackLocation();
        //}
    }
    ValueLocation EmitExpression(CompiledVariableAccess statement, Register? dest)
    {
        (CompiledVariableDefinition Variable, int Offset) variable = Frames.Last.Variables.FirstOrDefault(v => Utils.ReferenceEquals(v.Variable, statement.Variable));

        if (variable.Variable is null)
        {
            throw new InternalExceptionWithoutContext();
        }

        int variableSize = FindSize(variable.Variable.Type, variable.Variable.TypeExpression);
        int bpRelativeAddress = variable.Offset + 8 + variableSize;

        if (dest.HasValue)
        {
            Code.AppendInstruction("mov", dest.Value, $"{variableSize switch
            {
                1 => "byte",
                2 => "word",
                4 => "dword",
                8 => "qword",
                _ => throw new NotImplementedException(),
            }} [ebp-{bpRelativeAddress}]");
            return new ValueRegisterLocation(dest.Value);
        }
        else
        {
            Code.AppendInstruction("push", $"{variableSize switch
            {
                1 => "byte",
                2 => "word",
                4 => "dword",
                8 => "qword",
                _ => throw new NotImplementedException(),
            }} [ebp-{bpRelativeAddress}]");
            return new ValueStackLocation();
        }
    }
    ValueLocation EmitExpression(CompiledBinaryOperatorCall statement, Register? dest)
    {
        Register left;
        Register right;

        if (dest.HasValue)
        {
            EmitExpression(statement.Left, dest.Value);

            using AllocatedRegister r = AllocateRegister(FindBitWidth(statement.Right.Type, statement.Left));
            EmitExpression(statement.Right, r);

            left = dest.Value;
            right = r;
        }
        else
        {
            using AllocatedRegister l = AllocateRegister(FindBitWidth(statement.Left.Type, statement.Left));
            EmitExpression(statement.Left, l);

            using AllocatedRegister r = AllocateRegister(FindBitWidth(statement.Right.Type, statement.Left));
            EmitExpression(statement.Right, r);

            left = l;
            right = r;
        }

        switch (statement.Operator)
        {
            case CompiledBinaryOperatorCall.Addition:
                Code.AppendInstruction("add", left, right);
                return new ValueRegisterLocation(left);
            case CompiledBinaryOperatorCall.Subtraction:
                Code.AppendInstruction("sub", left, right);
                return new ValueRegisterLocation(left);
            case CompiledBinaryOperatorCall.Multiplication:
                Code.AppendInstruction("mul", left, right);
                return new ValueRegisterLocation(left);
            default:
                throw new NotImplementedException($"Binary operator `{statement.Operator}` not implemented");
        }
    }

    ValueLocation EmitExpression(CompiledExpression statement, Register? dest = null) => statement switch
    {
        CompiledConstantValue v => EmitExpression(v, dest),
        CompiledVariableAccess v => EmitExpression(v, dest),
        CompiledBinaryOperatorCall v => EmitExpression(v, dest),
        _ => throw new NotImplementedException($"Expression of type {statement.GetType().Name} is not implemented"),
    };

    void EmitStatement(CompiledReturn statement)
    {
        if (statement.Value is not null)
        {
            using AllocatedRegister dest = AllocateRegister(Register.EAX);
            ValueLocation value = EmitExpression(statement.Value, dest);
            PutValueIntoRegister(value, dest);
        }
        CleanupFrame(Frames.Last);
        Code.AppendInstruction("pop", Register.EBP);
        Code.AppendInstruction("ret");
        DidReturn = true;
    }

    void EmitStatement(CompiledVariableDefinition statement)
    {
        int offset = Frames.Last.Variables.Sum(v => v.Offset);
        if (statement.InitialValue is not null)
        {
            ValueLocation valueLocation = EmitExpression(statement.InitialValue);
            PushValue(valueLocation);
        }
        else
        {
            Code.AppendInstruction("sub", Register.EBP, FindSize(statement.Type, statement));
        }
        Frames.Last.Variables.Add((statement, offset));
    }

    void EmitStatement(CompiledStatement statement)
    {
        switch (statement)
        {
            case CompiledReturn v: EmitStatement(v); break;
            case CompiledExpression v: EmitExpression(v); break;
            case CompiledVariableDefinition v: EmitStatement(v); break;
            default:
                throw new NotImplementedException($"Statement of type {statement.GetType().Name} is not implemented");
        }
    }

    void CleanupFrame(StackFrame frame)
    {
        while (frame.Variables.Count > 0)
        {
            (CompiledVariableDefinition v, _) = frame.Variables.Pop();
            Code.AppendInstruction("add", Register.ESP, FindSize(v.Type, v.TypeExpression));
        }
    }

#if NET
    [SupportedOSPlatform("linux")]
#endif
    NativeFunction GenerateImpl(DiagnosticsCollection diagnostics)
    {
        Frames.Push(new());
        Code.AppendInstruction("push", Register.EBP);
        Code.AppendInstruction("mov", Register.EBP, Register.ESP);

        foreach (CompiledStatement item in TopLevelStatements)
        {
            EmitStatement(item);
        }

        if (!DidReturn)
        {
            Code.AppendInstruction("mov", Register.EAX, 0);
            CleanupFrame(Frames.Last);
            Code.AppendInstruction("pop", Register.EBP);
            Code.AppendInstruction("ret");
        }

        Frames.Pop();

        string assembly = $"BITS 32\n{Code.Builder}";

        Console.WriteLine(assembly);

        byte[] code = Assembler.Assemble(assembly, diagnostics);

        if (code.Length == 0)
        {
            return default;
        }

        NativeFunction func = NativeFunction.Allocate(code);

        return func;
    }

#if NET
    [SupportedOSPlatform("linux")]
#endif
    public static NativeFunction Generate(CompilerResult compilerResult, DiagnosticsCollection diagnostics)
        => new CodeGeneratorForNative(compilerResult, diagnostics)
        .GenerateImpl(diagnostics);
}
