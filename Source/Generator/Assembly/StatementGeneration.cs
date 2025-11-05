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

enum Registers
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

enum RegisterIdentifier : byte
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

enum RegisterSlice : byte
{
    R,
    D,
    W,
    H,
    L,
}

readonly struct Register : IEquatable<Register>
{
    public static readonly Register EAX = new(RegisterIdentifier.AX, RegisterSlice.D);
    public static readonly Register EBX = new(RegisterIdentifier.BX, RegisterSlice.D);
    public static readonly Register ECX = new(RegisterIdentifier.CX, RegisterSlice.D);
    public static readonly Register EDX = new(RegisterIdentifier.DX, RegisterSlice.D);

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

public partial class CodeGeneratorForNative : CodeGenerator
{
    public override int PointerSize => 4;
    public override BuiltinType BooleanType => BuiltinType.U8;
    public override BuiltinType SizeofStatementType => BuiltinType.I32;
    public override BuiltinType ArrayLengthType => BuiltinType.I32;

    readonly TextSectionBuilder Code = new();

    protected override bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = PointerSize;
        error = null;
        return true;
    }

    protected override bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = PointerSize;
        error = null;
        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int JitFn();

    readonly List<(Register Register, CompiledStatementWithValue Expression)> ExpressionInRegisters = new();
    readonly HashSet<Register> UsedRegisters = new();
    bool DidReturn;

    class StackFrame
    {
        public readonly Stack<(CompiledVariableDeclaration Variable, int Offset)> Variables = new();
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

    void SaveExpression(CompiledStatementWithValue value, Register register)
    {
        for (int i = 0; i < ExpressionInRegisters.Count; i++)
        {
            if (ExpressionInRegisters[i].Register.Overlaps(register))
            {
                ExpressionInRegisters[i] = (register, value);
                return;
            }
        }

        ExpressionInRegisters.Add((register, value));
    }

    AllocatedRegister AllocateRegister(BitWidth bitWidth)
    {
        if (TryAllocateRegister(bitWidth, out AllocatedRegister register))
        {
            return register;
        }

        throw new InvalidOperationException("No registers available");
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
        bool isUsed = false;

        foreach ((Register usedRegister, _) in ExpressionInRegisters)
        {
            if (reg.Overlaps(usedRegister))
            {
                isUsed = true;
                break;
            }
        }

        if (!isUsed && UsedRegisters.Add(reg))
        {
            for (int i = 0; i < ExpressionInRegisters.Count; i++)
            {
                if (ExpressionInRegisters[i].Register.Overlaps(reg))
                {
                    ExpressionInRegisters.RemoveAt(i--);
                }
            }
            register = new AllocatedRegister(UsedRegisters, reg);
            return true;
        }

        register = default;
        return false;
    }

    AllocatedRegister PutExpressionIntoRegister(CompiledStatementWithValue expression)
    {
        foreach ((Register register, CompiledStatementWithValue? _expression) in ExpressionInRegisters)
        {
            if (expression == _expression)
            {
                return new AllocatedRegister(UsedRegisters, register);
            }
        }

        if (!expression.Type.Is(out BuiltinType? builtinType))
        {
            throw new NotImplementedException("Only builtin types are supported");
        }

        AllocatedRegister result = AllocateRegister(builtinType.GetBitWidth(this));
        Code.AppendInstruction("pop", result.ToString());
        return result;
    }

    void PutExpressionIntoRegister(CompiledStatementWithValue expression, Register register)
    {
        foreach ((Register _register, CompiledStatementWithValue? _expression) in ExpressionInRegisters)
        {
            if (expression == _expression)
            {
                if (_register != register)
                {
                    Code.AppendInstruction("mov", register.ToString(), _register.ToString());
                }
                return;
            }
        }

        Code.AppendInstruction("pop", register.ToString());
    }

    void PushExpressionOnStack(CompiledStatementWithValue expression)
    {
        foreach ((Register _register, CompiledStatementWithValue? _expression) in ExpressionInRegisters)
        {
            if (expression == _expression)
            {
                Code.AppendInstruction("push", _register.ToString());
                return;
            }
        }
    }

    void EmitExpression(CompiledEvaluatedValue statement)
    {
        if (TryAllocateRegister(statement.Value.BitWidth, out AllocatedRegister reg))
        {
            using (reg)
            {
                Code.AppendInstruction("mov", reg.ToString(), statement.Value.ToStringValue()!);
                SaveExpression(statement, reg);
            }
        }
        else
        {
            Code.AppendInstruction("push", statement.Value.ToStringValue()!);
        }
    }

    void EmitExpression(CompiledVariableGetter statement)
    {
        var variable = Frames.Last.Variables.FirstOrDefault(v => v.Variable == statement.Variable);

        if (variable.Variable is null)
        {
            throw new InternalExceptionWithoutContext();
        }

        int bpRelativeAddress = variable.Offset + 8 + variable.Variable.Type.GetSize(this);

        Code.AppendInstruction("push", $"{variable.Variable.Type.GetSize(this) switch
        {
            1 => "byte",
            2 => "word",
            4 => "dword",
            8 => "qword",
            _ => throw new NotImplementedException(),
        }} [ebp-{bpRelativeAddress}]");
    }

    void EmitExpression(CompiledStatementWithValue statement)
    {
        switch (statement)
        {
            case CompiledEvaluatedValue v: EmitExpression(v); break;
            case CompiledVariableGetter v: EmitExpression(v); break;
            default:
                throw new NotImplementedException($"Expression of type {statement.GetType().Name} is not implemented");
        }
    }

    void EmitStatement(CompiledReturn statement)
    {
        if (statement.Value is not null)
        {
            EmitExpression(statement.Value);
            PutExpressionIntoRegister(statement.Value, Register.EAX);
        }
        CleanupFrame(Frames.Last);
        Code.AppendInstruction("pop", "ebp");
        Code.AppendInstruction("ret");
        DidReturn = true;
    }

    void EmitStatement(CompiledVariableDeclaration statement)
    {
        int offset = Frames.Last.Variables.Sum(v => v.Offset);
        if (statement.InitialValue is not null)
        {
            EmitExpression(statement.InitialValue);
            PushExpressionOnStack(statement.InitialValue);
        }
        else
        {
            Code.AppendInstruction("sub", "EBP", statement.Type.GetSize(this).ToString());
        }
        Frames.Last.Variables.Add((statement, offset));
    }

    void EmitStatement(CompiledStatement statement)
    {
        switch (statement)
        {
            case CompiledReturn v: EmitStatement(v); break;
            case CompiledStatementWithValue v: EmitExpression(v); break;
            case CompiledVariableDeclaration v: EmitStatement(v); break;
            default:
                throw new NotImplementedException($"Statement of type {statement.GetType().Name} is not implemented");
        }
    }

    void CleanupFrame(StackFrame frame)
    {
        while (frame.Variables.Count > 0)
        {
            (CompiledVariableDeclaration v, _) = frame.Variables.Pop();
            Code.AppendInstruction("add", "esp", v.Type.GetSize(this).ToString());
        }
    }

#if NET
    [SupportedOSPlatform("linux")]
#endif
    NativeFunction GenerateImpl(DiagnosticsCollection diagnostics)
    {
        Frames.Push(new());
        Code.AppendInstruction("push", "ebp");
        Code.AppendInstruction("mov", "ebp", "esp");

        foreach (CompiledStatement item in TopLevelStatements)
        {
            EmitStatement(item);
        }

        if (!DidReturn)
        {
            Code.AppendInstruction("mov", Registers.EAX.ToString(), "0");
            CleanupFrame(Frames.Last);
            Code.AppendInstruction("pop", "ebp");
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
