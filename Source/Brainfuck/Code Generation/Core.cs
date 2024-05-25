﻿namespace LanguageCore.Brainfuck.Generator;

using Compiler;
using Parser;
using Parser.Statement;
using Runtime;

public struct BrainfuckGeneratorResult
{
    public string Code;
    public int Optimizations;
    public int Precomputations;
    public int FunctionEvaluations;
    public DebugInformation? DebugInfo;
}

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
        readonly Uri? CurrentFile;

        public DebugInfoBlock(CodeHelper code, DebugInformation? debugInfo, Position position, Uri? currentFile)
        {
            Code = code;
            DebugInfo = debugInfo;
            Position = position;

            if (debugInfo is null) return;
            if (position == Position.UnknownPosition) return;

            InstructionStart = code.Length;
            CurrentFile = currentFile;
        }

        public DebugInfoBlock(CodeHelper code, DebugInformation? debugInfo, IPositioned? position, Uri? currentFile)
            : this(code, debugInfo, position?.Position ?? Position.UnknownPosition, currentFile)
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
                SourcePosition = Position,
                Uri = CurrentFile,
            });
        }
    }

    readonly struct DebugFunctionBlock<TFunction> : IDisposable
        where TFunction : FunctionThingDefinition
    {
        readonly int InstructionStart;
        readonly CodeHelper Code;
        readonly DebugInformation? DebugInfo;
        readonly Uri? Uri;
        readonly string Identifier;
        readonly string ReadableIdentifier;
        readonly Position Position;

        public DebugFunctionBlock(CodeHelper code, DebugInformation? debugInfo, Uri? uri, string identifier, string readableIdentifier, Position position)
        {
            Code = code;
            DebugInfo = debugInfo;
            Position = position;
            Uri = uri;
            Identifier = identifier;
            ReadableIdentifier = readableIdentifier;

            if (debugInfo is null) return;
            if (position == Position.UnknownPosition) return;

            InstructionStart = code.Length;
        }

        void IDisposable.Dispose()
        {
            if (DebugInfo is null) return;

            DebugInfo.FunctionInformation.Add(new FunctionInformation()
            {
                File = Uri,
                Identifier = Identifier,
                ReadableIdentifier = ReadableIdentifier,
                Instructions = (InstructionStart, Code.Length),
                IsValid = true,
                SourcePosition = Position,
            });
        }
    }

    readonly struct GeneratorSnapshot
    {
        public readonly ImmutableArray<BrainfuckVariable> Variables;
        public readonly ImmutableArray<int> VariableCleanupStack;
        public readonly ImmutableArray<ControlFlowBlock> Returns;
        public readonly ImmutableArray<ControlFlowBlock> Breaks;

        public readonly int Optimizations;
        public readonly int Precomputations;
        public readonly int FunctionEvaluations;

        public readonly ImmutableArray<ISameCheck> CurrentMacro;

        public readonly string? VariableCanBeDiscarded;

        public readonly DebugInformation? DebugInfo;

        public GeneratorSnapshot(CodeGeneratorForBrainfuck v)
        {
            Variables = v.CompiledVariables.ToImmutableArray();

            VariableCleanupStack = v.VariableCleanupStack.ToImmutableArray();
            Returns = v.Returns.ToImmutableArray();
            Breaks = v.Breaks.ToImmutableArray();

            Optimizations = v.Optimizations;
            Precomputations = v.Precomputations;
            FunctionEvaluations = v.FunctionEvaluations;

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

            v.Optimizations = Optimizations;
            v.Precomputations = Precomputations;
            v.FunctionEvaluations = FunctionEvaluations;

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

        public CodeSnapshot(CodeGeneratorForBrainfuck generator)
        {
            Code = generator.Code.Duplicate();
            Heap = new HeapCodeHelper(Code, generator.Heap.Start, generator.Heap.Size)
            {
                AddSmallComments = generator.Heap.AddSmallComments,
            };
            Stack = new StackCodeHelper(Code, generator.Stack);
        }

        public void Restore(CodeGeneratorForBrainfuck generator)
        {
            generator.Code = Code;
            generator.Heap = Heap;
            generator.Stack = Stack;
        }
    }

    readonly struct GeneratorStackFrame
    {
        public ImmutableDictionary<string, GeneralType>? SavedTypeArguments { get; init; }
        public ImmutableArray<ControlFlowBlock> SavedBreaks { get; init; }
        public ImmutableArray<BrainfuckVariable> SavedVariables { get; init; }
        public Uri? SavedFile { get; init; }
        public ImmutableArray<IConstant> SavedConstants { get; init; }
    }

    #region Fields

    CodeHelper Code;
    StackCodeHelper Stack;
    HeapCodeHelper Heap;
    CodeHelper IBrainfuckGenerator.Code => Code;

    new readonly Stack<BrainfuckVariable> CompiledVariables;

    readonly Stack<int> VariableCleanupStack;

    readonly Stack<ControlFlowBlock> Returns;
    readonly Stack<ControlFlowBlock> Breaks;

    readonly Stack<ISameCheck> CurrentMacro;

    readonly BrainfuckGeneratorSettings Settings;

    string? VariableCanBeDiscarded;

    bool ShowProgress => Settings.ShowProgress;

    readonly int MaxRecursiveDepth;

    #endregion

    public CodeGeneratorForBrainfuck(
        CompilerResult compilerResult,
        BrainfuckGeneratorSettings brainfuckSettings,
        AnalysisCollection? analysisCollection,
        PrintCallback? print) : base(compilerResult, analysisCollection, print)
    {
        CompiledVariables = new Stack<BrainfuckVariable>();
        Code = new CodeHelper()
        {
            AddSmallComments = brainfuckSettings.GenerateSmallComments,
            AddComments = brainfuckSettings.GenerateComments,
        };
        Stack = new StackCodeHelper(Code, 0, brainfuckSettings.StackSize);
        Heap = new HeapCodeHelper(Code, brainfuckSettings.HeapStart, brainfuckSettings.HeapSize)
        {
            AddSmallComments = brainfuckSettings.GenerateSmallComments,
        };
        CurrentMacro = new Stack<ISameCheck>();
        VariableCleanupStack = new Stack<int>();
        Returns = new Stack<ControlFlowBlock>();
        Breaks = new Stack<ControlFlowBlock>();
        DebugInfo = brainfuckSettings.GenerateDebugInformation ? new DebugInformation(compilerResult.Raw.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.Key, v.Value.Tokens))) : null;
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
            { CompiledVariables[i] = new BrainfuckVariable(CompiledVariables[i].Name, CompiledVariables[i].File, CompiledVariables[i].Address, false, CompiledVariables[i].DeallocateOnClean, CompiledVariables[i].Type, CompiledVariables[i].Size); }
        }

        Uri? savedFile = CurrentFile;

        ImmutableArray<IConstant> savedConstants = CompiledLocalConstants.ToImmutableArray();
        CompiledLocalConstants.Clear();

        return new GeneratorStackFrame()
        {
            SavedBreaks = savedBreaks,
            SavedVariables = savedVariables,
            SavedConstants = savedConstants,
            SavedFile = savedFile,
            SavedTypeArguments = savedTypeArguments?.ToImmutableDictionary(),
        };
    }
    void PopStackFrame(GeneratorStackFrame frame)
    {
        CurrentFile = frame.SavedFile;

        CompiledVariables.Set(frame.SavedVariables);

        CompiledLocalConstants.Set(frame.SavedConstants);

        if (Breaks.Count > 0)
        { throw new InternalException(); }

        Breaks.Set(frame.SavedBreaks);

        if (frame.SavedTypeArguments != null)
        { SetTypeArguments(frame.SavedTypeArguments); }
    }

    DebugInfoBlock DebugBlock(IPositioned? position) => new(Code, DebugInfo, position, CurrentFile);
    DebugInfoBlock DebugBlock(Position position) => new(Code, DebugInfo, position, CurrentFile);

    DebugFunctionBlock<CompiledFunction> FunctionBlock(CompiledFunction function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function.File,
        function.Identifier.Content,
        function.ToReadable(typeArguments),
        function.Position);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledOperator function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function.File,
        function.Identifier.Content,
        function.ToReadable(typeArguments),
        function.Position);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledGeneralFunction function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function.File,
        function.Identifier.Content,
        function.ToReadable(typeArguments),
        function.Position);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledConstructor function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function.File,
        function.Type.ToString(),
        function.ToReadable(typeArguments),
        function.Position);

    protected override bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out GeneralType? type)
    {
        if (GetVariable(symbolName, out BrainfuckVariable? variable))
        {
            type = variable.Type;
            return true;
        }

        if (GetConstant(symbolName, out IConstant? constant))
        {
            type = GeneralType.From(constant.Type);
            return true;
        }

        type = null;
        return false;
    }

    #region Addressing Helpers

    int GetDataOffset(StatementWithValue value, StatementWithValue? until = null) => value switch
    {
        IndexCall v => GetDataOffset(v, until),
        Field v => GetDataOffset(v, until),
        Identifier => 0,
        _ => throw new NotImplementedException()
    };
    int GetDataOffset(Field field, StatementWithValue? until = null)
    {
        if (field.PrevStatement == until) return 0;

        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType is not StructType structType)
        { throw new NotImplementedException(); }

        if (!structType.GetField(field.Identifier.Content, out _, out int fieldOffset))
        { throw new CompilerException($"Field \"{field.Identifier}\" not found in struct \"{structType.Struct.Identifier}\"", field.Identifier, CurrentFile); }

        int prevOffset = GetDataOffset(field.PrevStatement, until);
        return prevOffset + fieldOffset;
    }
    int GetDataOffset(IndexCall indexCall, StatementWithValue? until = null)
    {
        if (indexCall.PrevStatement == until) return 0;

        GeneralType prevType = FindStatementType(indexCall.PrevStatement);

        if (prevType is not ArrayType arrayType)
        { throw new CompilerException($"Only stack arrays supported by now and this is not one", indexCall.PrevStatement, CurrentFile); }

        if (!TryCompute(indexCall.Index, out CompiledValue index))
        { throw new CompilerException($"Can't compute the index value", indexCall.Index, CurrentFile); }

        int prevOffset = GetDataOffset(indexCall.PrevStatement, until);
        int offset = (int)index * 2 * arrayType.Of.Size;
        return prevOffset + offset;
    }

    StatementWithValue? NeedDerefernce(StatementWithValue value) => value switch
    {
        Identifier => null,
        Field v => NeedDerefernce(v),
        IndexCall v => NeedDerefernce(v),
        _ => throw new NotImplementedException()
    };
    StatementWithValue? NeedDerefernce(IndexCall indexCall)
    {
        if (FindStatementType(indexCall.PrevStatement) is PointerType)
        { return indexCall.PrevStatement; }

        return NeedDerefernce(indexCall.PrevStatement);
    }
    StatementWithValue? NeedDerefernce(Field field)
    {
        if (FindStatementType(field.PrevStatement) is PointerType)
        { return field.PrevStatement; }

        return NeedDerefernce(field.PrevStatement);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    bool TryGetAddress(Statement statement, out int address, out int size, StatementWithValue? until = null) => statement switch
    {
        IndexCall index => TryGetAddress(index, out address, out size, until),
        Pointer pointer => TryGetAddress(pointer, out address, out size, until),
        Identifier identifier => TryGetAddress(identifier, out address, out size, until),
        Field field => TryGetAddress(field, out address, out size, until),
        _ => throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile),
    };
    bool TryGetAddress(IndexCall index, out int address, out int size, StatementWithValue? until)
    {
        if (index.PrevStatement is not Identifier arrayIdentifier)
        { throw new CompilerException($"This must be an identifier", index.PrevStatement, CurrentFile); }

        if (!GetVariable(arrayIdentifier.Content, out BrainfuckVariable? variable))
        { throw new CompilerException($"Variable \"{arrayIdentifier}\" not found", arrayIdentifier, CurrentFile); }

        if (variable.Type is ArrayType arrayType)
        {
            size = arrayType.Of.Size;
            address = variable.Address;

            if (size != 1)
            { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index, CurrentFile); }

            if (TryCompute(index.Index, out CompiledValue indexValue))
            {
                address = variable.Address + ((int)indexValue * 2 * arrayType.Of.Size);
                return true;
            }

            return false;
        }

        throw new CompilerException($"Variable is not an array", arrayIdentifier, CurrentFile);
    }
    bool TryGetAddress(Field field, out int address, out int size, StatementWithValue? until)
    {
        GeneralType type = FindStatementType(field.PrevStatement);

        if (type is StructType structType)
        {
            if (!TryGetAddress(field.PrevStatement, out int prevAddress, out _, until))
            {
                address = default;
                size = default;
                return false;
            }

            if (!structType.GetField(field.Identifier.Content, out _, out int fieldOffset))
            { throw new CompilerException($"Field \"{field.Identifier}\" not found in struct \"{structType.Struct.Identifier}\"", field.Identifier, CurrentFile); }

            GeneralType fieldType = FindStatementType(field);

            address = fieldOffset + prevAddress;
            size = fieldType.Size;
            return true;
        }

        address = default;
        size = default;
        return false;
    }
    bool TryGetAddress(Pointer pointer, out int address, out int size, StatementWithValue? until)
    {
        if (!TryCompute(pointer.PrevStatement, out CompiledValue addressToSet))
        { throw new NotSupportedException($"Runtime pointer address in not supported", pointer.PrevStatement, CurrentFile); }

        if (!CompiledValue.TryShrinkTo8bit(ref addressToSet))
        { throw new CompilerException($"Address value must be a byte (not {addressToSet.Type})", pointer.PrevStatement, CurrentFile); }

        address = addressToSet.Byte;
        size = 1;
        return true;
    }
    bool TryGetAddress(Identifier identifier, out int address, out int size, StatementWithValue? until)
    {
        if (!GetVariable(identifier.Content, out BrainfuckVariable? variable))
        { throw new CompilerException($"Variable \"{identifier}\" not found", identifier, CurrentFile); }

        address = variable.Address;
        size = variable.Size;
        return true;
    }
#pragma warning restore IDE0060 // Remove unused parameter

    #endregion

    bool GetVariable(string name, [NotNullWhen(true)] out BrainfuckVariable? variable) => GetVariable(CompiledVariables, name, out variable);
    static bool GetVariable(Stack<BrainfuckVariable> variables, string name, [NotNullWhen(true)] out BrainfuckVariable? variable)
    {
        for (int i = variables.Count - 1; i >= 0; i--)
        {
            if (variables[i].Name == name)
            {
                variable = variables[i];
                return true;
            }
        }

        variable = null;
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
                    variable.Type is PointerType)
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
            if (!GetVariable(identifier.Content, out BrainfuckVariable? _variable))
            { continue; }
            if (_variable.Name != variable.Name)
            { continue; }

            usages++;
        }

        return usages;
    }

    void GenerateTopLevelStatements(ImmutableArray<Statement> statements, Uri file, bool isImported)
    {
        Print?.Invoke($"  Generating top level statements for file {file.ToString() ?? "null"} ...", LogType.Debug);

        CurrentFile = file;

        if (!isImported)
        { CompiledVariables.Add(new BrainfuckVariable(ReturnVariableName, file, Stack.PushVirtual(1), false, false, new BuiltinType(BasicType.Integer))); }

        if (Settings.ClearGlobalVariablesBeforeExit)
        { VariableCleanupStack.Push(PrecompileVariables(statements)); }
        else
        { PrecompileVariables(statements); }

        ControlFlowBlock? returnBlock = BeginReturnBlock(null, FindControlFlowUsage(statements));

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
        { throw new InternalException(); }

        if (Settings.ClearGlobalVariablesBeforeExit)
        { CleanupVariables(VariableCleanupStack.Pop()); }

        Code.SetPointer(0);

        CurrentFile = null;
    }

    BrainfuckGeneratorResult GenerateCode(CompilerResult compilerResult)
    {
        Print?.Invoke("Generating code ...", LogType.Debug);
        Print?.Invoke("  Precompiling ...", LogType.Debug);

        foreach ((ImmutableArray<Statement> statements, _) in compilerResult.TopLevelStatements)
        {
            CompileGlobalConstants(statements);
        }

        for (int i = 0; i < compilerResult.TopLevelStatements.Length; i++)
        {
            (ImmutableArray<Statement> statements, Uri file) = compilerResult.TopLevelStatements[i];
            GenerateTopLevelStatements(statements, file, i < compilerResult.TopLevelStatements.Length - 1);
        }

        if (Heap.IsUsed)
        {
            Heap.Destroy();

            string? heapInit = Heap.LateInit();
            Code.Insert(0, heapInit);
        }

        if (Code.BranchDepth != 0)
        { throw new InternalException($"Unbalanced branches", compilerResult.File); }

        Print?.Invoke($"Used stack size: {Stack.MaxUsedSize} bytes", LogType.Debug);

        if (Stack.WillOverflow)
        { AnalysisCollection?.Warnings.Add(new Warning($"Stack will probably overflow", Position.UnknownPosition, null)); }

        return new BrainfuckGeneratorResult()
        {
            Code = Code.ToString(),
            Optimizations = Optimizations,
            Precomputations = Precomputations,
            FunctionEvaluations = FunctionEvaluations,
            DebugInfo = DebugInfo,
        };
    }

    public static BrainfuckGeneratorResult Generate(
        CompilerResult compilerResult,
        BrainfuckGeneratorSettings brainfuckSettings,
        PrintCallback? printCallback = null,
        AnalysisCollection? analysisCollection = null)
    => new CodeGeneratorForBrainfuck(
        compilerResult,
        brainfuckSettings,
        analysisCollection,
        printCallback).GenerateCode(compilerResult);
}
