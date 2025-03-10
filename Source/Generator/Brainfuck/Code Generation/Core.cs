﻿using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
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
        CleanupHeap = true,
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
        public ImmutableArray<IConstant> SavedConstants { get; init; }
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

    #endregion

    public CodeGeneratorForBrainfuck(
        CompilerResult compilerResult,
        BrainfuckGeneratorSettings brainfuckSettings,
        DiagnosticsCollection diagnostics,
        PrintCallback? print) : base(compilerResult, diagnostics, print)
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
        DebugInfo = brainfuckSettings.GenerateDebugInformation ? new DebugInformation(compilerResult.Raw.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.File, v.Tokens.Tokens))) : null;
        MaxRecursiveDepth = 4;
        Settings = brainfuckSettings;
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
            { CompiledVariables[i] = new BrainfuckVariable(CompiledVariables[i].Address, CompiledVariables[i].IsReference, false, CompiledVariables[i].DeallocateOnClean, CompiledVariables[i].Type, CompiledVariables[i].Size, CompiledVariables[i]); }
        }

        ImmutableArray<IConstant> savedConstants = CompiledLocalConstants.ToImmutableArray();
        CompiledLocalConstants.Clear();

        return new GeneratorStackFrame()
        {
            SavedBreaks = savedBreaks,
            SavedVariables = savedVariables,
            SavedConstants = savedConstants,
            SavedTypeArguments = savedTypeArguments?.ToImmutableDictionary(),
        };
    }
    void PopStackFrame(GeneratorStackFrame frame)
    {
        CompiledVariables.Set(frame.SavedVariables);

        CompiledLocalConstants.Set(frame.SavedConstants);

        if (Breaks.Count > 0)
        { throw new InternalExceptionWithoutContext(); }

        Breaks.Set(frame.SavedBreaks);

        if (frame.SavedTypeArguments != null)
        { SetTypeArguments(frame.SavedTypeArguments); }
    }

    DebugInfoBlock DebugBlock(ILocated location) => new(Code, DebugInfo, location.Location.Position, location.Location.File);
    DebugInfoBlock DebugBlock(IPositioned position, Uri file) => new(Code, DebugInfo, position, file);
    DebugInfoBlock DebugBlock(Position position, Uri file) => new(Code, DebugInfo, position, file);

    DebugFunctionBlock<CompiledFunction> FunctionBlock(CompiledFunction function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function,
        typeArguments);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledOperator function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function,
        typeArguments);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledGeneralFunction function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function,
        typeArguments);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledConstructor function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function,
        typeArguments);

    protected override bool GetLocalSymbolType(Identifier symbolName, [NotNullWhen(true)] out GeneralType? type)
    {
        if (GetVariable(symbolName, out BrainfuckVariable? variable, out _))
        {
            type = variable.Type;
            return true;
        }

        if (GetConstant(symbolName.Content, symbolName.File, out IConstant? constant, out _))
        {
            type = GeneralType.From(constant.Type);
            return true;
        }

        type = null;
        return false;
    }

    #region Addressing Helpers

    bool TryGetAddress(Statement statement, [NotNullWhen(true)] out Address? address, out int size)
    {
        switch (statement)
        {
            case IndexCall index: return TryGetAddress(index, out address, out size);
            case Pointer pointer: return TryGetAddress(pointer, out address, out size);
            case Identifier identifier: return TryGetAddress(identifier, out address, out size);
            case Field field: return TryGetAddress(field, out address, out size);
            default:
            {
                Diagnostics.Add(Diagnostic.Critical($"Unknown statement \"{statement.GetType().Name}\"", statement));
                address = default;
                size = default;
                return false;
            }
        }
    }
    bool TryGetAddress(IndexCall index, [NotNullWhen(true)] out Address? address, out int size)
    {
        if (FindStatementType(index.PrevStatement).Is(out PointerType? prevPointerType) && !(
            GetVariable(index.PrevStatement, out BrainfuckVariable? prevVariable, out _) &&
            prevVariable.IsReference
        ) && prevPointerType.To.Is(out ArrayType? arrayType))
        {
            size = arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement);

            if (size != 1)
            { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index); }

            if (TryCompute(index.Index, out CompiledValue indexValue))
            {
                address = new AddressRuntimePointer(index.PrevStatement) + ((int)indexValue * arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement));
                return true;
            }

            address = null;
            return false;
        }

        if (index.PrevStatement is not Identifier arrayIdentifier)
        {
            Diagnostics.Add(Diagnostic.Critical($"This must be an identifier", index.PrevStatement));
            address = default;
            size = default;
            return default;
        }

        if (!GetVariable(arrayIdentifier, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(arrayIdentifier));
            address = default;
            size = default;
            return false;
        }

        if (variable.Type.Is(out arrayType))
        {
            size = arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement);
            address = new AddressAbsolute(variable.Address);

            if (size != 1)
            { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index); }

            if (TryCompute(index.Index, out CompiledValue indexValue))
            {
                address += (int)indexValue * 2 * arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement);
                return true;
            }

            return false;
        }

        Diagnostics.Add(Diagnostic.Critical($"Variable is not an array", arrayIdentifier));
        address = default;
        size = default;
        return default;
    }
    bool TryGetAddress(Field field, [NotNullWhen(true)] out Address? address, out int size)
    {
        GeneralType type = FindStatementType(field.PrevStatement);

        if ((
            GetVariable(field.PrevStatement, out BrainfuckVariable? prevVariable, out _) &&
            prevVariable.IsReference &&
            prevVariable.Type.Is(out PointerType? prevStackPointerType) &&
            prevStackPointerType.To.Is(out StructType? structType)
        ) ||
            type.Is(out structType))
        {
            if (!TryGetAddress(field.PrevStatement, out Address? prevAddress, out _))
            {
                address = default;
                size = default;
                return false;
            }

            if (!structType.GetField(field.Identifier.Content, this, out _, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field.Identifier, field.File));
                address = default;
                size = default;
                return default;
            }

            GeneralType fieldType = FindStatementType(field);

            address = prevAddress + fieldOffset;
            size = fieldType.GetSize(this, Diagnostics, field);
            return true;
        }

        if (FindStatementType(field.PrevStatement).Is(out PointerType? prevPointerType) &&
            prevPointerType.To.Is(out structType))
        {
            if (!structType.GetField(field.Identifier.Content, this, out _, out int fieldOffset, out PossibleDiagnostic? error))
            {
                Diagnostics.Add(error.ToError(field.Identifier, field.File));
                address = default;
                size = default;
                return default;
            }

            GeneralType fieldType = FindStatementType(field);

            address = new AddressRuntimePointer(field.PrevStatement) + fieldOffset;
            size = fieldType.GetSize(this, Diagnostics, field);
            return true;
        }

        address = default;
        size = default;
        return false;
    }
    bool TryGetAddress(Pointer pointer, [NotNullWhen(true)] out Address? address, out int size)
    {
        if (!TryCompute(pointer.PrevStatement, out CompiledValue addressToSet))
        { throw new NotSupportedException($"Runtime pointer address in not supported", pointer.PrevStatement); }

        if (!CompiledValue.TryShrinkTo8bit(ref addressToSet))
        {
            Diagnostics.Add(Diagnostic.Critical($"Address value must be a byte (not \"{addressToSet.Type}\")", pointer.PrevStatement));
            address = default;
            size = default;
            return default;
        }

        address = new AddressAbsolute(addressToSet.U8);
        size = 1;
        return true;
    }
    bool TryGetAddress(Identifier identifier, [NotNullWhen(true)] out Address? address, out int size)
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

    bool GetVariable(string name, Uri file, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError) => GetVariable(CompiledVariables, name, file, out variable, out notFoundError);
    bool GetVariable(StatementWithValue name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError) => GetVariable(CompiledVariables, name, out variable, out notFoundError);

    static bool GetVariable(Stack<BrainfuckVariable> variables, string variableName, Uri relevantFile, [NotNullWhen(true)] out BrainfuckVariable? result, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        BrainfuckVariable? result_ = default;
        PossibleDiagnostic? error_ = null;

        GlobalVariablePerfectus perfectus = GlobalVariablePerfectus.None;

        static GlobalVariablePerfectus Max(GlobalVariablePerfectus a, GlobalVariablePerfectus b) => a > b ? a : b;

        bool HandleIdentifier(BrainfuckVariable variable)
        {
            if (variableName is not null &&
                variable.Identifier.Content != variableName)
            { return false; }

            perfectus = Max(perfectus, GlobalVariablePerfectus.Identifier);
            return true;
        }

        bool HandleFile(BrainfuckVariable variable)
        {
            if (relevantFile is null ||
                variable.File != relevantFile)
            {
                // Not in the same file
                return false;
            }

            if (perfectus >= GlobalVariablePerfectus.File)
            {
                error_ = new PossibleDiagnostic($"Global variable \"{variableName}\" not found: multiple variables matched in the same file");
                // Debugger.Break();
            }

            perfectus = GlobalVariablePerfectus.File;
            result_ = variable;
            return true;
        }

        foreach (BrainfuckVariable variable in variables)
        {
            if (!HandleIdentifier(variable))
            { continue; }

            // MATCHED --> Searching for most relevant global variable

            if (perfectus < GlobalVariablePerfectus.Good)
            {
                result_ = variable;
                perfectus = GlobalVariablePerfectus.Good;
            }

            if (!HandleFile(variable))
            { continue; }
        }

        if (result_ is not null && perfectus >= GlobalVariablePerfectus.Good)
        {
            result = result_;
            error = error_;
            return true;
        }

        error = error_ ?? new PossibleDiagnostic($"Global variable \"{variableName}\" not found");
        result = null;
        return false;
    }
    static bool GetVariable(Stack<BrainfuckVariable> variables, StatementWithValue name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError)
    {
        variable = null;
        notFoundError = null;
        return name switch
        {
            Identifier identifier => GetVariable(variables, identifier, out variable, out notFoundError),
            Pointer pointer => GetVariable(variables, pointer, out variable, out notFoundError),
            _ => false
        };
    }
    static bool GetVariable(Stack<BrainfuckVariable> variables, Identifier name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError) => GetVariable(variables, name.Content, name.File, out variable, out notFoundError);
    static bool GetVariable(Stack<BrainfuckVariable> variables, Pointer name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError)
    {
        if (name.PrevStatement is not Identifier identifier)
        {
            variable = null;
            notFoundError = new PossibleDiagnostic($"Only variables supported :(");
            return false;
        }

        for (int i = variables.Count - 1; i >= 0; i--)
        {
            if (variables[i].Name != identifier.Content) continue;

            variable = variables[i];

            if (!variables[i].IsReference)
            {
                notFoundError = new PossibleDiagnostic($"Variable \"{identifier.Content}\" isn't a reference");
                return false;
            }

            notFoundError = null;
            return true;
        }

        variable = null;
        notFoundError = new PossibleDiagnostic($"Variable \"{identifier.Content}\" not found");
        return false;
    }

    static void DiscardVariable(Stack<BrainfuckVariable> variables, string name)
    {
        for (int i = 0; i < variables.Count; i++)
        {
            if (variables[i].Name != name) continue;
            BrainfuckVariable v = variables[i];
            v.IsDiscarded = true;
            variables[i] = v;
            return;
        }
    }
    static void UndiscardVariable(Stack<BrainfuckVariable> variables, string name)
    {
        for (int i = 0; i < variables.Count; i++)
        {
            if (variables[i].Name != name) continue;
            BrainfuckVariable v = variables[i];
            v.IsDiscarded = false;
            variables[i] = v;
            return;
        }
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

                if (variable.DeallocateOnClean &&
                    variable.Type.Is<PointerType>())
                {
                    GenerateDestructor(new Identifier(Tokenizing.Token.CreateAnonymous(variable.Name), variable.File));
                }

                CompiledVariables.Pop();
                Stack.Pop();
            }
        }
    }

    int VariableUses(Statement statement, BrainfuckVariable variable)
    {
        int usages = 0;

        foreach (Statement _statement in statement.GetStatementsRecursively(true))
        {
            if (_statement is not Identifier identifier)
            { continue; }
            if (!GetVariable(identifier, out BrainfuckVariable? _variable, out _))
            { continue; }
            if (_variable.Name != variable.Name)
            { continue; }

            usages++;
        }

        return usages;
    }

    void GenerateTopLevelStatements(ImmutableArray<Statement> statements, Uri file)
    {
        if (statements.Length == 0) return;

        Print?.Invoke($"  Generating top level statements for file \"{file.ToString() ?? "null"}\" ...", LogType.Debug);

        ControlFlowBlock? returnBlock = BeginReturnBlock(new Location(statements[0].Position.Before(), file), FindControlFlowUsage(statements));

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
        Print?.Invoke("Generating code ...", LogType.Debug);
        Print?.Invoke("  Precompiling ...", LogType.Debug);

        foreach ((ImmutableArray<Statement> statements, _) in compilerResult.TopLevelStatements)
        {
            CompileGlobalConstants(statements);
        }

        VariableDeclaration implicitReturnValueVariable = new(
            Enumerable.Empty<Tokenizing.Token>(),
            new TypeInstanceSimple(Tokenizing.Token.CreateAnonymous("u8"), compilerResult.File),
            new Identifier(Tokenizing.Token.CreateAnonymous(ReturnVariableName), compilerResult.File),
            null,
            compilerResult.File
        );

        CompiledVariables.Add(new BrainfuckVariable(Stack.PushVirtual(1), false, false, false, ExitCodeType, ExitCodeType.GetSize(this), implicitReturnValueVariable));

        IEnumerable<VariableDeclaration> globalVariableDeclarations = compilerResult.TopLevelStatements
            .Select(v => v.Statements)
            .Aggregate(Enumerable.Empty<Statement>(), (a, b) => a.Concat(b))
            .Select(v => v as VariableDeclaration)
            .Where(v => v is not null)
            .Where(v => !v!.Modifiers.Contains(ModifierKeywords.Const))!;

        if (Settings.ClearGlobalVariablesBeforeExit)
        { VariableCleanupStack.Push(PrecompileVariables(globalVariableDeclarations, false)); }
        else
        { PrecompileVariables(globalVariableDeclarations, false); }

        for (int i = 0; i < compilerResult.TopLevelStatements.Length; i++)
        {
            (ImmutableArray<Statement> statements, Uri file) = compilerResult.TopLevelStatements[i];
            GenerateTopLevelStatements(statements, file);
        }

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

        Print?.Invoke($"Used stack size: {Stack.MaxUsedSize} bytes", LogType.Debug);

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
    => new CodeGeneratorForBrainfuck(
        compilerResult,
        brainfuckSettings,
        diagnostics,
        printCallback).GenerateCode(compilerResult);
}
