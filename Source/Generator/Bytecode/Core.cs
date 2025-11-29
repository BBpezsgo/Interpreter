using LanguageCore.Compiler;
using LanguageCore.IL.Generator;
using LanguageCore.Runtime;

namespace LanguageCore.BBLang.Generator;

public struct InstructionLabel : IEquatable<InstructionLabel>
{
    public static readonly InstructionLabel Invalid = new(-2, default);

    public int Index;
    readonly int Id;

    public InstructionLabel(int index, int id)
    {
        Index = index;
        Id = id;
    }

    public override readonly bool Equals(object? obj) => obj is InstructionLabel other && Id == other.Id;
    public readonly bool Equals(InstructionLabel other) => Id == other.Id;

    public static bool operator ==(InstructionLabel left, InstructionLabel right) => left.Id == right.Id;
    public static bool operator !=(InstructionLabel left, InstructionLabel right) => left.Id != right.Id;

    public override readonly int GetHashCode() => Id;

    public readonly PreparationInstructionOperand Relative(int additionalOffset = 0) => new(this, false, additionalOffset);
    public readonly PreparationInstructionOperand Absolute(int additionalOffset = 0) => new(this, true, additionalOffset);

    public override string ToString() => Id.ToString();
}

record struct CompiledScope(ImmutableArray<CompiledCleanup> Variables, bool IsFunction);

class GeneratedInstructionLabel : IHaveInstructionOffset
{
    public int InstructionOffset { get; set; }
}

class GeneratedVariable
{
    public int MemoryAddress { get; set; }
    public bool IsInitialized { get; set; }
}

public partial class CodeGeneratorForMain : CodeGenerator
{
    class RegisterUsage
    {
        public readonly struct Auto : IDisposable, IEquatable<Auto>
        {
            readonly CodeGeneratorForMain generator;
            readonly GeneralPurposeRegister register;

            public Register Register => register;

            public Auto(CodeGeneratorForMain generator, GeneralPurposeRegister register)
            {
                this.generator = generator;
                this.register = register;

                this.generator.Registers.UsedRegisters.Add(this.register);
            }

            public void Dispose()
            {
                generator.Registers.UsedRegisters.Remove(register);
                generator.Code.FinishUsingRegister(register);
            }

            public override string ToString() => register.ToString();

            public override bool Equals(object? obj) => obj is Auto other && register == other.register;
            public bool Equals(Auto other) => register == other.register;
            public override int GetHashCode() => register.GetHashCode();
            public static bool operator ==(Auto left, Auto right) => left.register == right.register;
            public static bool operator !=(Auto left, Auto right) => left.register != right.register;
        }

        readonly HashSet<GeneralPurposeRegister> UsedRegisters = new();
        readonly CodeGeneratorForMain generator;

        public RegisterUsage(CodeGeneratorForMain generator)
        {
            this.generator = generator;
        }

        public bool IsFree(GeneralPurposeRegister register)
        {
            foreach (GeneralPurposeRegister item in UsedRegisters)
            {
                if (item.Identifier != register.Identifier) continue;
                if (item.Slice is RegisterSlice.R or RegisterSlice.D or RegisterSlice.W) return false;
                if (register.Slice is RegisterSlice.R or RegisterSlice.D or RegisterSlice.W) return false;
                if (register.Slice == item.Slice) return false;
                return true;
            }
            return true;
        }

        public Auto GetFree(BitWidth bitWidth)
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
                foreach (RegisterSlice slice in registerSlices)
                {
                    GeneralPurposeRegister reg = new(identifier, slice);
                    if (IsFree(reg))
                    {
                        return new Auto(generator, reg);
                    }
                }
            }

            throw new InternalExceptionWithoutContext("No registers avaliable");
        }
    }

    readonly struct UndefinedOffset
    {
        public InstructionLabel Label { get; }
        public Location CallerLocation { get; }
        public IHaveInstructionOffset Called { get; }

        public UndefinedOffset(InstructionLabel label, ILocated caller, IHaveInstructionOffset called)
        {
            Label = label;
            CallerLocation = caller.Location;
            Called = called;
        }

        public UndefinedOffset(InstructionLabel label, Location callerLocation, IHaveInstructionOffset called)
        {
            Label = label;
            CallerLocation = callerLocation;
            Called = called;
        }
    }

    class ControlFlowFrame
    {
        public InstructionLabel Label;
        public bool IsSkipping;

        public ControlFlowFrame(InstructionLabel label)
        {
            Label = label;
        }
    }

    public static readonly CompilerSettings DefaultCompilerSettings = new()
    {
        PointerSize = 4,
        ArrayLengthType = BuiltinType.I32,
        BooleanType = BuiltinType.U8,
        ExitCodeType = BuiltinType.I32,
        SizeofStatementType = BuiltinType.I32,
        Optimizations = OptimizationSettings.All,
        ExternalConstants = ImmutableArray<ExternalConstant>.Empty,
        ExternalFunctions = ImmutableArray<IExternalFunction>.Empty,
        PreprocessorVariables = PreprocessorVariables.Normal,
        SourceProviders = ImmutableArray.Create<ISourceProvider>(
            FileSourceProvider.Instance
        ),
    };
    readonly CodeGeneratorForIL? ILGenerator;

    #region Fields

    readonly ImmutableArray<IExternalFunction> ExternalFunctions;
    readonly List<(ExternalFunctionScopedSync Function, ExternalFunctionScopedSyncCallback Reference)> GeneratedUnmanagedFunctions = new();
    readonly Dictionary<CompiledLabelDeclaration, GeneratedInstructionLabel> GeneratedInstructionLabels = new();
    readonly Dictionary<CompiledVariableDefinition, GeneratedVariable> GeneratedVariables = new();
    readonly HashSet<ICompiledFunctionDefinition> GeneratedFunctions = new();

    readonly Stack<CompiledScope> CleanupStack2;
    ICompiledFunctionDefinition? CurrentContext;

    readonly Stack<ControlFlowFrame> ReturnInstructions;
    readonly Stack<ControlFlowFrame> BreakInstructions;

    readonly BytecodeEmitter Code;

    readonly List<UndefinedOffset> UndefinedFunctionOffsets;
    readonly List<UndefinedOffset> UndefinedInstructionLabels;

    readonly RegisterUsage Registers;

    GeneralType? CurrentReturnType;
    [MemberNotNullWhen(true, nameof(CurrentReturnType))]
    bool CanReturn => CurrentReturnType is not null;

    readonly Stack<ScopeInformation> CurrentScopeDebug = new();
    readonly MainGeneratorSettings Settings;

    public override int PointerSize { get; }
    public override BuiltinType BooleanType => BuiltinType.U8;
    public override BuiltinType SizeofStatementType => BuiltinType.I32;
    public override BuiltinType ArrayLengthType => BuiltinType.I32;
    readonly Stack<int> ScopeSizes = new();

    #endregion

    public static readonly ImmutableDictionary<string, (Register Register, BuiltinType Type)> RegisterKeywords = new Dictionary<string, (Register Register, BuiltinType Type)>()
    {
        { "IP", (Register.CodePointer, BuiltinType.I32) },
        { "SP", (Register.CodePointer, BuiltinType.I32) },
        { "BP", (Register.CodePointer, BuiltinType.I32) },

        { "EAX", (Register.EAX, BuiltinType.I32) },
        { "EBX", (Register.EBX, BuiltinType.I32) },
        { "ECX", (Register.ECX, BuiltinType.I32) },
        { "EDX", (Register.EDX, BuiltinType.I32) },

        { "AX", (Register.AX, BuiltinType.I16) },
        { "BX", (Register.BX, BuiltinType.I16) },
        { "CX", (Register.CX, BuiltinType.I16) },
        { "DX", (Register.DX, BuiltinType.I16) },

        { "AH", (Register.AH, BuiltinType.I8) },
        { "BH", (Register.BH, BuiltinType.I8) },
        { "CH", (Register.CH, BuiltinType.I8) },
        { "DH", (Register.DH, BuiltinType.I8) },

        { "AL", (Register.AL, BuiltinType.I8) },
        { "BL", (Register.BL, BuiltinType.I8) },
        { "CL", (Register.CL, BuiltinType.I8) },
        { "DL", (Register.DL, BuiltinType.I8) },
    }.ToImmutableDictionary();

    public CodeGeneratorForMain(CompilerResult compilerResult, MainGeneratorSettings settings, DiagnosticsCollection diagnostics) : base(compilerResult, diagnostics)
    {
        ILGenerator = settings.ILGeneratorSettings.HasValue ? new CodeGeneratorForIL(compilerResult, new DiagnosticsCollection(), settings.ILGeneratorSettings.Value, null) : null;
        ExternalFunctions = compilerResult.ExternalFunctions;
        CleanupStack2 = new();
        ReturnInstructions = new();
        BreakInstructions = new();
        UndefinedFunctionOffsets = new();
        UndefinedInstructionLabels = new();
        Settings = settings;
        Registers = new(this);
        PointerSize = settings.PointerSize;
        DebugInfo = new(compilerResult.RawTokens.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.File, v.Tokens.Tokens)))
        {
            StackOffsets = new()
            {
                SavedBasePointer = SavedBasePointerOffset,
                SavedCodePointer = SavedCodePointerOffset,
            }
        };
        Code = new BytecodeEmitter()
        {
            Optimizations = settings.Optimizations,
            DebugInfo = DebugInfo,
        };
    }

    void SetUndefinedFunctionOffsets(IEnumerable<UndefinedOffset> undefinedOffsets, bool addDiagnostics)
    {
        foreach (UndefinedOffset item in undefinedOffsets)
        {
            if (item.Called.InstructionOffset == InvalidFunctionAddress)
            {
                if (addDiagnostics)
                {
                    string thingName = item.Called switch
                    {
                        CompiledOperatorDefinition => "Operator",
                        CompiledConstructorDefinition => "Constructor",
                        CompiledFunctionDefinition => "Function",
                        CompiledGeneralFunctionDefinition v => v.Identifier.Content switch
                        {
                            BuiltinFunctionIdentifiers.Destructor => "Destructor",
                            BuiltinFunctionIdentifiers.IndexerGet => "Index getter",
                            BuiltinFunctionIdentifiers.IndexerSet => "Index setter",
                            _ => "???",
                        },
                        GeneratedInstructionLabel v => $"Label",
                        _ => "???",
                    };

                    Diagnostics.Add(Diagnostic.Internal($"{thingName} \"{(item.Called is ISimpleReadable readable ? readable.ToReadable() : item.Called.ToString())}\" does not have instruction offset (the compiler is messed up)", item.CallerLocation));
                }
                continue;
            }

            Code.MarkLabel(item.Label);
        }
    }

    public static BBLangGeneratorResult Generate(
        CompilerResult compilerResult,
        MainGeneratorSettings settings,
        PrintCallback? printCallback,
        DiagnosticsCollection diagnostics)
        => new CodeGeneratorForMain(compilerResult, settings, diagnostics)
        .GenerateCode(compilerResult, settings);
}
