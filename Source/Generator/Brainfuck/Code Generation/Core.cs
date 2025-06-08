﻿using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statements;
using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck.Generator;

[Flags]
public enum BrainfuckCompilerFlags
{
    None = 0b_0000,
    PrintCompiled = 0b_0001,
    WriteToFile = 0b_0010,
    PrintFinal = 0b_0100,
}

[Flags]
public enum BrainfuckPrintFlags
{
    None = 0b_0000,
    PrintExecutionTime = 0b_0001,
    PrintMemory = 0b_0010,
}

public struct BrainfuckGeneratorSettings
{
    public bool ClearGlobalVariablesBeforeExit;
    public int StackSize;
    public int HeapSize;
    public bool GenerateDebugInformation;
    public bool ShowProgress;
    public bool DontOptimize;
    public bool GenerateSmallComments;
    public bool GenerateComments;
    public bool CleanupHeap;

    public readonly int HeapStart => StackSize + 1;

    public static BrainfuckGeneratorSettings Default => new()
    {
        ClearGlobalVariablesBeforeExit = true,
        StackSize = 63,
        HeapSize = 64,
        GenerateDebugInformation = true,
        ShowProgress = true,
        DontOptimize = false,
        GenerateSmallComments = false,
        GenerateComments = false,
        CleanupHeap = false,
    };

    public BrainfuckGeneratorSettings(BrainfuckGeneratorSettings other)
    {
        ClearGlobalVariablesBeforeExit = other.ClearGlobalVariablesBeforeExit;
        StackSize = other.StackSize;
        HeapSize = other.HeapSize;
        GenerateDebugInformation = other.GenerateDebugInformation;
        ShowProgress = other.ShowProgress;
        DontOptimize = other.DontOptimize;
        GenerateSmallComments = other.GenerateSmallComments;
        GenerateComments = other.GenerateComments;
        CleanupHeap = other.CleanupHeap;
    }
}

public partial class CodeGeneratorForBrainfuck : CodeGenerator, IBrainfuckGenerator
{
    public static readonly CompilerSettings DefaultCompilerSettings = new()
    {
        PointerSize = 1,
        ArrayLengthType = BuiltinType.U8,
        BooleanType = BuiltinType.U8,
        ExitCodeType = BuiltinType.U8,
        SizeofStatementType = BuiltinType.U8,
        ExternalConstants = ImmutableArray<ExternalConstant>.Empty,
        ExternalFunctions = ImmutableArray.Create<IExternalFunction>(
            new ExternalFunctionStub(0, "stdin", 0, 1),
            new ExternalFunctionStub(1, "stdout", 1, 0)
        ),
        DontOptimize = false,
        SourceProviders = ImmutableArray.Create<ISourceProvider>(
            FileSourceProvider.Instance
        ),
        PreprocessorVariables = PreprocessorVariables.Brainfuck,
    };

    const string ReturnVariableName = "@return";

    readonly struct DebugInfoBlock : IDisposable
    {
        readonly int InstructionStart;
        readonly CodeHelper Code;
        readonly DebugInformation? DebugInfo;
        readonly Position Position;
        readonly Uri File;

        public DebugInfoBlock(CodeHelper code, DebugInformation? debugInfo, Position position, Uri currentFile)
        {
            Code = code;
            DebugInfo = debugInfo;
            Position = position;
            InstructionStart = code.Length;
            File = currentFile;
        }

        public DebugInfoBlock(CodeHelper code, DebugInformation? debugInfo, IPositioned position, Uri currentFile)
            : this(code, debugInfo, position.Position, currentFile)
        { }

        void IDisposable.Dispose()
        {
            if (DebugInfo is null) return;
            if (Position == Position.UnknownPosition) return;

            int end = Code.Length;
            if (InstructionStart == end) return;
            DebugInfo.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (InstructionStart, end),
                Location = new Location(Position, File),
            });
        }
    }

    readonly struct DebugFunctionBlock<TFunction> : IDisposable
        where TFunction : FunctionThingDefinition
    {
        readonly int InstructionStart;
        readonly CodeHelper Code;
        readonly DebugInformation? DebugInfo;
        readonly FunctionThingDefinition Function;
        readonly Dictionary<string, GeneralType>? TypeArguments;

        public DebugFunctionBlock(CodeHelper code, DebugInformation? debugInfo, FunctionThingDefinition function, Dictionary<string, GeneralType>? typeArguments)
        {
            Code = code;
            DebugInfo = debugInfo;
            InstructionStart = code.Length;

            Function = function;
            TypeArguments = typeArguments;
        }

        void IDisposable.Dispose()
        {
            if (DebugInfo is null) return;

            DebugInfo.FunctionInformation.Add(new FunctionInformation()
            {
                Function = Function,
                Instructions = (InstructionStart, Code.Length),
                IsValid = true,
                TypeArguments = TypeArguments?.ToImmutableDictionary(),
            });
        }
    }

    readonly struct GeneratorSnapshot
    {
        public readonly ImmutableArray<BrainfuckVariable> Variables;
        public readonly ImmutableArray<int> VariableCleanupStack;
        public readonly ImmutableArray<ControlFlowBlock> Returns;
        public readonly ImmutableArray<ControlFlowBlock> Breaks;

        public readonly GeneratorStatistics Statistics;

        public readonly ImmutableArray<IDefinition> CurrentMacro;

        public readonly string? VariableCanBeDiscarded;

        public readonly DebugInformation? DebugInfo;

        public GeneratorSnapshot(CodeGeneratorForBrainfuck v)
        {
            Variables = v.CompiledVariables.ToImmutableArray();

            VariableCleanupStack = v.VariableCleanupStack.ToImmutableArray();
            Returns = v.Returns.ToImmutableArray();
            Breaks = v.Breaks.ToImmutableArray();

            Statistics = v._statistics;

            CurrentMacro = v.CurrentMacro.ToImmutableArray();

            VariableCanBeDiscarded = v.VariableCanBeDiscarded;

            DebugInfo = v.DebugInfo?.Duplicate();
        }

        public void Restore(CodeGeneratorForBrainfuck v)
        {
            v.CompiledVariables.Set(Variables);

            v.VariableCleanupStack.Set(VariableCleanupStack);
            v.Returns.Set(Returns);
            v.Breaks.Set(Breaks);

            v._statistics = Statistics;

            v.CurrentMacro.Set(CurrentMacro);

            v.VariableCanBeDiscarded = VariableCanBeDiscarded;

            v.DebugInfo = DebugInfo;
        }
    }

    readonly struct CodeSnapshot
    {
        public readonly CodeHelper Code;
        public readonly HeapCodeHelper Heap;
        public readonly StackCodeHelper Stack;
        public readonly DiagnosticsCollection Diagnostics;

        public CodeSnapshot(CodeGeneratorForBrainfuck generator)
        {
            Code = generator.Code.Duplicate();
            Heap = new HeapCodeHelper(Code, generator.Heap.Start, generator.Heap.Size)
            {
                AddSmallComments = generator.Heap.AddSmallComments,
            };
            Stack = new StackCodeHelper(Code, generator.Stack);
            Diagnostics = new DiagnosticsCollection(generator.Diagnostics);
        }

        public void Restore(CodeGeneratorForBrainfuck generator)
        {
            generator.Code = Code;
            generator.Heap = Heap;
            generator.Stack = Stack;
            if (Diagnostics is not null)
            {
                generator.Diagnostics.Clear();
                generator.Diagnostics.AddRange(Diagnostics.Diagnostics);
                generator.Diagnostics.AddRange(Diagnostics.DiagnosticsWithoutContext);
            }
        }
    }

    readonly struct GeneratorStackFrame
    {
        public ImmutableDictionary<string, GeneralType>? SavedTypeArguments { get; init; }
        public ImmutableArray<ControlFlowBlock> SavedBreaks { get; init; }
        public ImmutableArray<BrainfuckVariable> SavedVariables { get; init; }
    }

    #region Fields

    CodeHelper Code;
    StackCodeHelper Stack;
    HeapCodeHelper Heap;
    CodeHelper IBrainfuckGenerator.Code => Code;

    readonly Stack<BrainfuckVariable> CompiledVariables;

    readonly Stack<int> VariableCleanupStack;

    readonly Stack<ControlFlowBlock> Returns;
    readonly Stack<ControlFlowBlock> Breaks;

    readonly Stack<IDefinition> CurrentMacro;

    readonly BrainfuckGeneratorSettings Settings;

    string? VariableCanBeDiscarded;

    bool ShowProgress => Settings.ShowProgress;

    readonly int MaxRecursiveDepth;

    public override int PointerSize => 1;
    public override BuiltinType BooleanType => BuiltinType.U8;
    public override BuiltinType SizeofStatementType => BuiltinType.U8;
    public override BuiltinType ArrayLengthType => BuiltinType.U8;

    static readonly BuiltinType ExitCodeType = BuiltinType.U8;

    readonly ImmutableDictionary<ICompiledFunctionDefinition, CompiledStatement> FunctionBodies;

    #endregion

    public CodeGeneratorForBrainfuck(CompilerResult compilerResult, BrainfuckGeneratorSettings brainfuckSettings, DiagnosticsCollection diagnostics) : base(compilerResult, diagnostics)
    {
        CompiledVariables = new Stack<BrainfuckVariable>();
        Code = new CodeHelper()
        {
            AddSmallComments = brainfuckSettings.GenerateSmallComments,
            AddComments = brainfuckSettings.GenerateComments,
        };
        Stack = new StackCodeHelper(Code, 0, brainfuckSettings.StackSize, Diagnostics);
        Heap = new HeapCodeHelper(Code, brainfuckSettings.HeapStart, brainfuckSettings.HeapSize)
        {
            AddSmallComments = brainfuckSettings.GenerateSmallComments,
        };
        CurrentMacro = new Stack<IDefinition>();
        VariableCleanupStack = new Stack<int>();
        Returns = new Stack<ControlFlowBlock>();
        Breaks = new Stack<ControlFlowBlock>();
        DebugInfo = brainfuckSettings.GenerateDebugInformation ? new DebugInformation(compilerResult.RawTokens.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.File, v.Tokens.Tokens))) : null;
        MaxRecursiveDepth = 4;
        Settings = brainfuckSettings;

        FunctionBodies = compilerResult.Functions.Select(v => new KeyValuePair<ICompiledFunctionDefinition, CompiledStatement>(v.Function, v.Body)).ToImmutableDictionary();
    }

    GeneratorSnapshot Snapshot() => new(this);
    void Restore(GeneratorSnapshot snapshot) => snapshot.Restore(this);

    CodeSnapshot SnapshotCode() => new(this);
    void RestoreCode(CodeSnapshot snapshot) => snapshot.Restore(this);

    GeneratorStackFrame PushStackFrame(Dictionary<string, GeneralType>? typeArguments)
    {
        Dictionary<string, GeneralType>? savedTypeArguments = null;

        if (typeArguments != null)
        { SetTypeArguments(typeArguments, out savedTypeArguments); }

        ImmutableArray<ControlFlowBlock> savedBreaks = Breaks.ToImmutableArray();
        Breaks.Clear();

        ImmutableArray<BrainfuckVariable> savedVariables = CompiledVariables.ToImmutableArray();
        CompiledVariables.Clear();

        if (CurrentMacro.Count == 1)
        {
            CompiledVariables.PushRange(savedVariables);
            for (int i = 0; i < CompiledVariables.Count; i++)
            { CompiledVariables[i] = new BrainfuckVariable(CompiledVariables[i].Address, CompiledVariables[i].IsReference, false, CompiledVariables[i].Cleanup, CompiledVariables[i].Size, CompiledVariables[i]); }
        }

        return new GeneratorStackFrame()
        {
            SavedBreaks = savedBreaks,
            SavedVariables = savedVariables,
            SavedTypeArguments = savedTypeArguments?.ToImmutableDictionary(),
        };
    }
    void PopStackFrame(GeneratorStackFrame frame)
    {
        CompiledVariables.Set(frame.SavedVariables);

        if (Breaks.Count > 0)
        { throw new InternalExceptionWithoutContext(); }

        Breaks.Set(frame.SavedBreaks);

        if (frame.SavedTypeArguments != null)
        { SetTypeArguments(frame.SavedTypeArguments); }
    }

    DebugInfoBlock DebugBlock(ILocated location) => new(Code, DebugInfo, location.Location.Position, location.Location.File);
    DebugInfoBlock DebugBlock(IPositioned position, Uri file) => new(Code, DebugInfo, position, file);

    DebugFunctionBlock<CompiledFunctionDefinition> FunctionBlock(CompiledFunctionDefinition function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function,
        typeArguments);

    DebugFunctionBlock<CompiledOperatorDefinition> FunctionBlock(CompiledOperatorDefinition function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function,
        typeArguments);

    DebugFunctionBlock<CompiledOperatorDefinition> FunctionBlock(CompiledGeneralFunctionDefinition function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function,
        typeArguments);

    DebugFunctionBlock<CompiledOperatorDefinition> FunctionBlock(CompiledConstructorDefinition function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function,
        typeArguments);

    #region Addressing Helpers

    bool TryGetAddress(CompiledStatementWithValue statement, [NotNullWhen(true)] out Address? address, out int size)
    {
        switch (statement)
        {
            case CompiledIndexGetter index: return TryGetAddress(index, out address, out size);
            case CompiledPointer pointer: return TryGetAddress(pointer, out address, out size);
            case CompiledVariableGetter identifier: return TryGetAddress(identifier, out address, out size);
            case CompiledParameterGetter identifier: return TryGetAddress(identifier, out address, out size);
            case CompiledFieldGetter field: return TryGetAddress(field, out address, out size);
            default:
            {
                Diagnostics.Add(Diagnostic.Critical($"Unknown statement \"{statement.GetType().Name}\"", statement));
                address = default;
                size = default;
                return false;
            }
        }
    }
    bool TryGetAddress(CompiledIndexGetter index, [NotNullWhen(true)] out Address? address, out int size)
    {
        if (index.Base.Type.Is(out PointerType? prevPointerType) && !(
            GetVariable(index.Base, out BrainfuckVariable? prevVariable, out _) &&
            prevVariable.IsReference
        ) && prevPointerType.To.Is(out ArrayType? arrayType))
        {
            size = arrayType.Of.GetSize(this, Diagnostics, index.Base);

            if (size != 1)
            { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index); }

            if (index.Index is CompiledEvaluatedValue indexValue)
            {
                address = new AddressRuntimePointer(index.Base) + ((int)indexValue.Value * arrayType.Of.GetSize(this, Diagnostics, index.Base));
                return true;
            }

            address = null;
            return false;
        }

        if (!GetVariable(index.Base, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(index.Base));
            address = default;
            size = default;
            return false;
        }

        if (variable.Type.Is(out arrayType))
        {
            size = arrayType.Of.GetSize(this, Diagnostics, index.Base);
            address = new AddressAbsolute(variable.Address);

            if (size != 1)
            { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index); }

            if (index.Index is CompiledEvaluatedValue indexValue)
            {
                address += (int)indexValue.Value * 2 * arrayType.Of.GetSize(this, Diagnostics, index.Base);
                return true;
            }

            return false;
        }

        Diagnostics.Add(Diagnostic.Critical($"Variable is not an array", index.Base));
        address = default;
        size = default;
        return default;
    }
    bool TryGetAddress(CompiledFieldGetter field, [NotNullWhen(true)] out Address? address, out int size)
    {
        GeneralType type = field.Object.Type;

        if ((
            GetVariable(field.Object, out BrainfuckVariable? prevVariable, out _) &&
            prevVariable.IsReference &&
            prevVariable.Type.Is(out PointerType? prevStackPointerType) &&
            prevStackPointerType.To.Is(out StructType? structType)
        ) ||
            type.Is(out structType))
        {
            if (!TryGetAddress(field.Object, out Address? prevAddress, out _))
            {
                address = default;
                size = default;
                return false;
            }

            if (!structType.GetField(field.Field.Identifier.Content, this, out _, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field));
                address = default;
                size = default;
                return default;
            }

            GeneralType fieldType = field.Type;

            address = prevAddress + fieldOffset;
            size = fieldType.GetSize(this, Diagnostics, field);
            return true;
        }

        if (field.Object.Type.Is(out PointerType? prevPointerType) &&
            prevPointerType.To.Is(out structType))
        {
            if (!structType.GetField(field.Field.Identifier.Content, this, out _, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field));
                address = default;
                size = default;
                return default;
            }

            GeneralType fieldType = field.Type;

            address = new AddressRuntimePointer(field.Object) + fieldOffset;
            size = fieldType.GetSize(this, Diagnostics, field);
            return true;
        }

        address = default;
        size = default;
        return false;
    }
    bool TryGetAddress(CompiledPointer pointer, [NotNullWhen(true)] out Address? address, out int size)
    {
        if (pointer.To is not CompiledEvaluatedValue _addressToSet)
        { throw new NotSupportedException($"Runtime pointer address in not supported", pointer.To); }
        CompiledValue addressToSet = _addressToSet.Value;

        if (!CompiledValue.TryShrinkTo8bit(ref addressToSet))
        {
            Diagnostics.Add(Diagnostic.Critical($"Address value must be a byte (not \"{addressToSet.Type}\")", pointer.To));
            address = default;
            size = default;
            return default;
        }

        address = new AddressAbsolute(addressToSet.U8);
        size = 1;
        return true;
    }
    bool TryGetAddress(CompiledVariableGetter identifier, [NotNullWhen(true)] out Address? address, out int size)
    {
        if (!GetVariable(identifier, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(identifier));
            address = default;
            size = default;
            return false;
        }

        address = new AddressAbsolute(variable.Address);
        size = variable.Size;
        return true;
    }
    bool TryGetAddress(CompiledParameterGetter identifier, [NotNullWhen(true)] out Address? address, out int size)
    {
        if (!GetVariable(identifier, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(identifier));
            address = default;
            size = default;
            return false;
        }

        address = new AddressAbsolute(variable.Address);
        size = variable.Size;
        return true;
    }

    #endregion

    bool GetVariable(string name, Uri file, [NotNullWhen(true)] out BrainfuckVariable? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        BrainfuckVariable? result_ = default;
        PossibleDiagnostic? error_ = null;

        StatementCompiler.GlobalVariablePerfectus perfectus = StatementCompiler.GlobalVariablePerfectus.None;

        static StatementCompiler.GlobalVariablePerfectus Max(StatementCompiler.GlobalVariablePerfectus a, StatementCompiler.GlobalVariablePerfectus b) => a > b ? a : b;

        bool HandleIdentifier(BrainfuckVariable variable)
        {
            if (name is not null &&
                variable.Identifier != name)
            { return false; }

            perfectus = Max(perfectus, StatementCompiler.GlobalVariablePerfectus.Identifier);
            return true;
        }

        bool HandleFile(BrainfuckVariable variable)
        {
            if (file is null ||
                variable.File != file)
            {
                // Not in the same file
                return false;
            }

            if (perfectus >= StatementCompiler.GlobalVariablePerfectus.File)
            {
                error_ = new PossibleDiagnostic($"Global variable \"{name}\" not found: multiple variables matched in the same file");
                // Debugger.Break();
            }

            perfectus = StatementCompiler.GlobalVariablePerfectus.File;
            result_ = variable;
            return true;
        }

        foreach (BrainfuckVariable variable in CompiledVariables)
        {
            if (!HandleIdentifier(variable))
            { continue; }

            // MATCHED --> Searching for most relevant global variable

            if (perfectus < StatementCompiler.GlobalVariablePerfectus.Good)
            {
                result_ = variable;
                perfectus = StatementCompiler.GlobalVariablePerfectus.Good;
            }

            if (!HandleFile(variable))
            { continue; }
        }

        if (result_ is not null && perfectus >= StatementCompiler.GlobalVariablePerfectus.Good)
        {
            result = result_;
            error = error_;
            return true;
        }

        error = error_ ?? new PossibleDiagnostic($"Global variable \"{name}\" not found");
        result = null;
        return false;
    }
    bool GetVariable(CompiledStatementWithValue name, [NotNullWhen(true)] out BrainfuckVariable? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        result = null;
        error = null;
        return name switch
        {
            CompiledVariableGetter identifier => GetVariable(identifier, out result, out error),
            CompiledParameterGetter identifier => GetVariable(identifier, out result, out error),
            CompiledPointer pointer => GetVariable(pointer, out result, out error),
            _ => false
        };
    }
    bool GetVariable(CompiledVariableGetter name, [NotNullWhen(true)] out BrainfuckVariable? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        foreach (BrainfuckVariable v in CompiledVariables)
        {
            if (v.Declaration == name.Variable)
            {
                result = v;
                error = null;
                return true;
            }
        }

        return GetVariable(name.Variable.Identifier, name.Variable.Location.File, out result, out error);
    }
    bool GetVariable(CompiledParameterGetter name, [NotNullWhen(true)] out BrainfuckVariable? result, [NotNullWhen(false)] out PossibleDiagnostic? error) => GetVariable(name.Variable.Identifier.Content, name.Variable.File, out result, out error);
    bool GetVariable(CompiledPointer name, [NotNullWhen(true)] out BrainfuckVariable? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        switch (name.To)
        {
            case CompiledVariableGetter v:
                if (!GetVariable(v, out result, out error))
                {
                    result = null;
                    return false;
                }
                break;
            case CompiledParameterGetter v:
                if (!GetVariable(v, out result, out error))
                {
                    result = null;
                    return false;
                }
                break;
            default:
                result = null;
                error = new PossibleDiagnostic($"Only variables supported :(");
                return false;
        }

        if (!result.IsReference)
        {
            error = new PossibleDiagnostic($"Variable \"{result.Identifier}\" isn't a reference");
            return false;
        }

        error = null;
        return true;
    }

    void CleanupVariables(int n)
    {
        if (n == 0) return;
        using (Code.Block(this, $"Clean up variables ({n})"))
        {
            for (int i = 0; i < n; i++)
            {
                BrainfuckVariable variable = CompiledVariables.Last;

                if (!variable.HaveToClean)
                {
                    CompiledVariables.Pop();
                    continue;
                }

                if (variable.Cleanup is not null &&
                    variable.Type.Is<PointerType>())
                {
                    GenerateDestructor(variable);
                }

                CompiledVariables.Pop();
                Stack.Pop();
            }
        }
    }

    int VariableUses(CompiledStatement statement, BrainfuckVariable variable)
    {
        return 1;
    }

    void GenerateTopLevelStatements(ImmutableArray<CompiledStatement> statements)
    {
        if (statements.Length == 0) return;

        ControlFlowBlock? returnBlock = BeginReturnBlock(statements[0].Location.Before(), StatementCompiler.FindControlFlowUsage(statements));

        using (ConsoleProgressBar progressBar = new(ConsoleColor.DarkGray, ShowProgress))
        {
            for (int i = 0; i < statements.Length; i++)
            {
                progressBar.Print(i, statements.Length);
                GenerateCodeForStatement(statements[i]);
            }
        }

        if (returnBlock is not null)
        { FinishControlFlowStatements(Returns.Pop(), true, "return"); }

        if (Returns.Count > 0 ||
            Breaks.Count > 0)
        { throw new InternalExceptionWithoutContext(); }

        Code.SetPointer(0);
    }

    BrainfuckGeneratorResult GenerateCode(CompilerResult compilerResult)
    {
        VariableDeclaration implicitReturnValueVariable = new(
            Enumerable.Empty<AttributeUsage>(),
            Enumerable.Empty<Tokenizing.Token>(),
            new TypeInstanceSimple(Tokenizing.Token.CreateAnonymous("u8"), compilerResult.File),
            new Identifier(Tokenizing.Token.CreateAnonymous(ReturnVariableName), compilerResult.File),
            null,
            compilerResult.File
        );

        CompiledVariables.Add(new BrainfuckVariable(Stack.PushVirtual(1), false, false, null, ExitCodeType.GetSize(this), new CompiledVariableDeclaration()
        {
            Identifier = implicitReturnValueVariable.Identifier.Content,
            InitialValue = null,
            IsGlobal = true,
            Location = implicitReturnValueVariable.Location,
            Type = ExitCodeType,
            Cleanup = new CompiledCleanup()
            {
                Location = implicitReturnValueVariable.Location,
                TrashType = ExitCodeType,
            },
        }));

        IEnumerable<CompiledVariableDeclaration> globalVariableDeclarations = compilerResult.Statements.OfType<CompiledVariableDeclaration>();

        if (Settings.ClearGlobalVariablesBeforeExit)
        { VariableCleanupStack.Push(PrecompileVariables(globalVariableDeclarations, false)); }
        else
        { PrecompileVariables(globalVariableDeclarations, false); }

        GenerateTopLevelStatements(compilerResult.Statements);

        if (Settings.ClearGlobalVariablesBeforeExit)
        { CleanupVariables(VariableCleanupStack.Pop()); }

        if (Heap.IsUsed)
        {
            if (Settings.CleanupHeap)
            { Heap.Destroy(); }

            string? heapInit = Heap.LateInit(out PossibleDiagnostic? heapInitError);
            heapInitError?.Throw();
            Code.Insert(0, heapInit);
        }

        Code.SetPointer(0);

        if (Code.BranchDepth != 0)
        { throw new InternalExceptionWithoutContext($"Unbalanced branches"); }

        return new BrainfuckGeneratorResult()
        {
            Code = Code.ToString(),
            Statistics = _statistics,
            DebugInfo = DebugInfo,
        };
    }

    public static BrainfuckGeneratorResult Generate(
        CompilerResult compilerResult,
        BrainfuckGeneratorSettings brainfuckSettings,
        PrintCallback? printCallback,
        DiagnosticsCollection diagnostics)
        => new CodeGeneratorForBrainfuck(compilerResult, brainfuckSettings, diagnostics)
        .GenerateCode(compilerResult);
}
