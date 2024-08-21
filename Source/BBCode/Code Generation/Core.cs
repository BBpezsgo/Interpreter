namespace LanguageCore.BBLang.Generator;

using Compiler;
using Runtime;

class RegisterUsage
{
    public readonly struct Auto : IDisposable, IEquatable<Auto>
    {
        RegisterUsage Registers { get; }
        public Register Register { get; }
        public Register Register64 => Register switch
        {
            Register.EAX => Register.RAX,
            Register.EBX => Register.RBX,
            Register.ECX => Register.RCX,
            Register.EDX => Register.RDX,
            _ => throw new UnreachableException(),
        };
        public Register Register16 => Register switch
        {
            Register.EAX => Register.AX,
            Register.EBX => Register.BX,
            Register.ECX => Register.CX,
            Register.EDX => Register.DX,
            _ => throw new UnreachableException(),
        };
        public Register Register8H => Register switch
        {
            Register.EAX => Register.AH,
            Register.EBX => Register.BH,
            Register.ECX => Register.CH,
            Register.EDX => Register.DH,
            _ => throw new UnreachableException(),
        };
        public Register Register8L => Register switch
        {
            Register.EAX => Register.AL,
            Register.EBX => Register.BL,
            Register.ECX => Register.CL,
            Register.EDX => Register.DL,
            _ => throw new UnreachableException(),
        };

        public Auto(RegisterUsage registers, Register register)
        {
            Registers = registers;
            Register = register;

            Registers._isUsed[Register] = true;
        }

        public Register Get(BitWidth bitWidth) => bitWidth switch
        {
            BitWidth._8 => Register8L,
            BitWidth._16 => Register16,
            BitWidth._32 => Register,
            BitWidth._64 => Register64,
            _ => throw new UnreachableException(),
        };

        public void Dispose() => Registers._isUsed[Register] = false;

        public override string ToString() => Register.ToString();

        public override bool Equals(object? obj) => obj is Auto other && Register == other.Register;
        public bool Equals(Auto other) => Register == other.Register;
        public override int GetHashCode() => Register.GetHashCode();
        public static bool operator ==(Auto left, Auto right) => left.Register == right.Register;
        public static bool operator !=(Auto left, Auto right) => left.Register != right.Register;
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

public partial class CodeGeneratorForMain : CodeGenerator
{
    #region Fields

    readonly ImmutableDictionary<int, ExternalFunctionBase> ExternalFunctions;

    readonly Stack<ImmutableArray<CleanupItem>> CleanupStack;
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
    public override BuiltinType BooleanType => BuiltinType.Byte;
    public override BuiltinType SizeofStatementType => BuiltinType.Integer;
    public override BuiltinType ArrayLengthType => BuiltinType.Integer;

    #endregion

    public CodeGeneratorForMain(CompilerResult compilerResult, MainGeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, analysisCollection, print)
    {
        ExternalFunctions = compilerResult.ExternalFunctions.ToImmutableDictionary();
        GeneratedCode = new List<PreparationInstruction>();
        CleanupStack = new Stack<ImmutableArray<CleanupItem>>();
        ReturnInstructions = new Stack<List<int>>();
        BreakInstructions = new Stack<List<int>>();
        UndefinedFunctionOffsets = new List<UndefinedOffset<CompiledFunction>>();
        UndefinedOperatorFunctionOffsets = new List<UndefinedOffset<CompiledOperator>>();
        UndefinedGeneralFunctionOffsets = new List<UndefinedOffset<CompiledGeneralFunction>>();
        UndefinedConstructorOffsets = new List<UndefinedOffset<CompiledConstructor>>();
        Settings = settings;
        Registers = new RegisterUsage();
        PointerSize = settings.PointerSize;
        DebugInfo = new DebugInformation(compilerResult.Raw.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.Key, v.Value.Tokens)))
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
                    throw generalFunction.Identifier.Content switch
                    {
                        BuiltinFunctionIdentifiers.Destructor => new InternalException($"Destructor for {generalFunction.Context} does not have instruction offset", item.CallerPosition, item.CallerFile),
                        BuiltinFunctionIdentifiers.IndexerGet => new InternalException($"Index getter for {generalFunction.Context} does not have instruction offset", item.CallerPosition, item.CallerFile),
                        BuiltinFunctionIdentifiers.IndexerSet => new InternalException($"Index setter for {generalFunction.Context} does not have instruction offset", item.CallerPosition, item.CallerFile),
                        _ => new NotImplementedException(),
                    };
                }

                string thingName = item.Called switch
                {
                    CompiledOperator => "Operator",
                    CompiledConstructor => "Constructor",
                    _ => "Function",
                };

                if (item.Called is ISimpleReadable simpleReadable)
                { throw new InternalException($"{thingName} {simpleReadable.ToReadable()} does not have instruction offset", item.CallerPosition, item.CallerFile); }

                throw new InternalException($"{thingName} {item.Called} does not have instruction offset", item.CallerPosition, item.CallerFile);
            }

            int offset = item.IsAbsoluteAddress ? item.Called.InstructionOffset : item.Called.InstructionOffset - item.InstructionIndex;
            GeneratedCode[item.InstructionIndex].Operand1 = offset;
        }
    }

    public static BBLangGeneratorResult Generate(
        CompilerResult compilerResult,
        MainGeneratorSettings settings,
        PrintCallback? printCallback = null,
        AnalysisCollection? analysisCollection = null)
    {
        CodeGeneratorForMain generator = new(compilerResult, settings, analysisCollection, printCallback);
        return generator.GenerateCode(compilerResult, settings);
    }
}
