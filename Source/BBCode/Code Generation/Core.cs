using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

class RegisterUsage
{
    public readonly struct Auto : IDisposable, IEquatable<Auto>
    {
        readonly RegisterUsage _registers;
        readonly Register _register;

        public Register Register32 => _register switch
        {
            0 => default,
            Register.EAX => Register.EAX,
            Register.EBX => Register.EBX,
            Register.ECX => Register.ECX,
            Register.EDX => Register.EDX,
            _ => throw new UnreachableException(),
        };
        public Register Register64 => _register switch
        {
            0 => default,
            Register.EAX => Register.RAX,
            Register.EBX => Register.RBX,
            Register.ECX => Register.RCX,
            Register.EDX => Register.RDX,
            _ => throw new UnreachableException(),
        };
        public Register Register16 => _register switch
        {
            0 => default,
            Register.EAX => Register.AX,
            Register.EBX => Register.BX,
            Register.ECX => Register.CX,
            Register.EDX => Register.DX,
            _ => throw new UnreachableException(),
        };
        public Register Register8H => _register switch
        {
            0 => default,
            Register.EAX => Register.AH,
            Register.EBX => Register.BH,
            Register.ECX => Register.CH,
            Register.EDX => Register.DH,
            _ => throw new UnreachableException(),
        };
        public Register Register8L => _register switch
        {
            0 => default,
            Register.EAX => Register.AL,
            Register.EBX => Register.BL,
            Register.ECX => Register.CL,
            Register.EDX => Register.DL,
            _ => throw new UnreachableException(),
        };

        public Auto(RegisterUsage registers, Register register)
        {
            _registers = registers;
            _register = register;

            _registers._isUsed[_register] = true;
        }

        public Register Get(BitWidth bitWidth) => bitWidth switch
        {
            0 => default,
            BitWidth._8 => Register8L,
            BitWidth._16 => Register16,
            BitWidth._32 => Register32,
            BitWidth._64 => Register64,
            _ => throw new UnreachableException(),
        };

        public void Dispose() => _registers._isUsed[_register] = false;

        public override string ToString() => _register.ToString();

        public override bool Equals(object? obj) => obj is Auto other && _register == other._register;
        public bool Equals(Auto other) => _register == other._register;
        public override int GetHashCode() => _register.GetHashCode();
        public static bool operator ==(Auto left, Auto right) => left._register == right._register;
        public static bool operator !=(Auto left, Auto right) => left._register != right._register;
    }

    readonly Dictionary<Register, bool> _isUsed;

    static readonly ImmutableArray<Register> GeneralRegisters32 = ImmutableArray.Create(
        Register.EAX,
        Register.EBX,
        Register.ECX,
        Register.EDX
    );

    public RegisterUsage()
    {
        _isUsed = new Dictionary<Register, bool>();
    }

    public bool IsFree(Register register)
    {
        if (!_isUsed.TryGetValue(register, out bool isUsed))
        { return true; }
        return !isUsed;
    }

    public Auto GetFree()
    {
        foreach (Register reg in GeneralRegisters32)
        {
            if (IsFree(reg))
            { return new Auto(this, reg); }
        }
        throw new NotImplementedException();
    }
}

record struct Scope(ImmutableArray<CleanupItem> Variables, bool IsFunction);

public partial class CodeGeneratorForMain : CodeGenerator
{
    #region Fields

    readonly ImmutableArray<IExternalFunction> ExternalFunctions;

    readonly Stack<Scope> CleanupStack;
    IDefinition? CurrentContext;

    readonly Stack<List<int>> ReturnInstructions;
    readonly Stack<List<int>> BreakInstructions;

    readonly List<PreparationInstruction> GeneratedCode;

    readonly List<UndefinedOffset<CompiledFunction>> UndefinedFunctionOffsets;
    readonly List<UndefinedOffset<CompiledOperator>> UndefinedOperatorFunctionOffsets;
    readonly List<UndefinedOffset<CompiledGeneralFunction>> UndefinedGeneralFunctionOffsets;
    readonly List<UndefinedOffset<CompiledConstructor>> UndefinedConstructorOffsets;

    readonly RegisterUsage Registers;

    GeneralType? CurrentReturnType;
    [MemberNotNullWhen(true, nameof(CurrentReturnType))]
    bool CanReturn => CurrentReturnType is not null;

    readonly Stack<ScopeInformation> CurrentScopeDebug = new();
    CompileLevel CompileLevel => Settings.CompileLevel;
    readonly MainGeneratorSettings Settings;

    public override int PointerSize { get; }
    public override BuiltinType BooleanType => BuiltinType.U8;
    public override BuiltinType SizeofStatementType => BuiltinType.I32;
    public override BuiltinType ArrayLengthType => BuiltinType.I32;
    readonly Stack<int> ScopeSizes = new();

    #endregion

    public CodeGeneratorForMain(CompilerResult compilerResult, MainGeneratorSettings settings, DiagnosticsCollection diagnostics, PrintCallback? print) : base(compilerResult, diagnostics, print)
    {
        ExternalFunctions = compilerResult.ExternalFunctions;
        GeneratedCode = new List<PreparationInstruction>();
        CleanupStack = new Stack<Scope>();
        ReturnInstructions = new Stack<List<int>>();
        BreakInstructions = new Stack<List<int>>();
        UndefinedFunctionOffsets = new List<UndefinedOffset<CompiledFunction>>();
        UndefinedOperatorFunctionOffsets = new List<UndefinedOffset<CompiledOperator>>();
        UndefinedGeneralFunctionOffsets = new List<UndefinedOffset<CompiledGeneralFunction>>();
        UndefinedConstructorOffsets = new List<UndefinedOffset<CompiledConstructor>>();
        Settings = settings;
        Registers = new RegisterUsage();
        PointerSize = settings.PointerSize;
        DebugInfo = new DebugInformation(compilerResult.Raw.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.File, v.Tokens.Tokens)))
        {
            StackOffsets = new StackOffsets()
            {
                SavedBasePointer = SavedBasePointerOffset,
                SavedCodePointer = SavedCodePointerOffset,
            }
        };
    }

    void SetUndefinedFunctionOffsets<TFunction>(IEnumerable<UndefinedOffset<TFunction>> undefinedOffsets)
        where TFunction : IHaveInstructionOffset
    {
        foreach (UndefinedOffset<TFunction> item in undefinedOffsets)
        {
            if (item.Called.InstructionOffset == InvalidFunctionAddress)
            {
                if (item.Called is Parser.GeneralFunctionDefinition generalFunction)
                {
                    Diagnostics.Add(generalFunction.Identifier.Content switch
                    {
                        BuiltinFunctionIdentifiers.Destructor => Diagnostic.Internal($"Destructor for \"{generalFunction.Context}\" does not have instruction offset (the compiler is messed up)", item.CallerLocation),
                        BuiltinFunctionIdentifiers.IndexerGet => Diagnostic.Internal($"Index getter for \"{generalFunction.Context}\" does not have instruction offset (the compiler is messed up)", item.CallerLocation),
                        BuiltinFunctionIdentifiers.IndexerSet => Diagnostic.Internal($"Index setter for \"{generalFunction.Context}\" does not have instruction offset (the compiler is messed up)", item.CallerLocation),
                        _ => throw new NotImplementedException(),
                    });
                    continue;
                }

                string thingName = item.Called switch
                {
                    CompiledOperator => "Operator",
                    CompiledConstructor => "Constructor",
                    _ => "Function",
                };

                if (item.Called is ISimpleReadable simpleReadable)
                {
                    Diagnostics.Add(Diagnostic.Internal($"{thingName} \"{simpleReadable.ToReadable()}\" does not have instruction offset (the compiler is messed up)", item.CallerLocation));
                    continue;
                }

                Diagnostics.Add(Diagnostic.Internal($"{thingName} \"{item.Called}\" does not have instruction offset (the compiler is messed up)", item.CallerLocation));
                continue;
            }

            int offset = item.IsAbsoluteAddress ? item.Called.InstructionOffset : item.Called.InstructionOffset - item.InstructionIndex;
            GeneratedCode[item.InstructionIndex].Operand1 = offset;
        }
    }

    public static BBLangGeneratorResult Generate(
        CompilerResult compilerResult,
        MainGeneratorSettings settings,
        PrintCallback? printCallback,
        DiagnosticsCollection diagnostics)
    {
        CodeGeneratorForMain generator = new(compilerResult, settings, diagnostics, printCallback);
        return generator.GenerateCode(compilerResult, settings);
    }
}
