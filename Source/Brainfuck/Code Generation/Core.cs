namespace LanguageCore.Brainfuck.Generator;

using Compiler;
using Parser;
using Parser.Statement;
using Runtime;

readonly struct CleanupItem
{
    /// <summary>
    /// The actual data size on the stack
    /// </summary>
    public readonly int Size;
    /// <summary>
    /// The element count
    /// </summary>
    public readonly int Count;

    public CleanupItem(int size, int count)
    {
        Size = size;
        Count = count;
    }
}

public struct BrainfuckGeneratorResult
{
    public string Code;
    public int Optimizations;
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

    public readonly int HeapStart => StackSize + 1;

    public static BrainfuckGeneratorSettings Default => new()
    {
        ClearGlobalVariablesBeforeExit = true,
        StackSize = 63,
        HeapSize = 64,
        GenerateDebugInformation = true,
        ShowProgress = true,
        DontOptimize = false,
    };

    public BrainfuckGeneratorSettings(BrainfuckGeneratorSettings other)
    {
        ClearGlobalVariablesBeforeExit = other.ClearGlobalVariablesBeforeExit;
        StackSize = other.StackSize;
        HeapSize = other.HeapSize;
        GenerateDebugInformation = other.GenerateDebugInformation;
        ShowProgress = other.ShowProgress;
        DontOptimize = other.DontOptimize;
    }
}

public partial class CodeGeneratorForBrainfuck : CodeGeneratorNonGeneratorBase, IBrainfuckGenerator
{
    const string ReturnVariableName = "@return";

    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    struct Variable
    {
        public readonly string Name;
        public readonly int Address;

        public readonly bool HaveToClean;
        public readonly bool DeallocateOnClean;

        public readonly GeneralType Type;
        public readonly int Size;

        public bool IsDiscarded;
        public bool IsInitialized;

        public Variable(string name, int address, bool haveToClean, bool deallocateOnClean, GeneralType type)
            : this(name, address, haveToClean, deallocateOnClean, type, type.Size) { }
        public Variable(string name, int address, bool haveToClean, bool deallocateOnClean, GeneralType type, int size)
        {
            Name = name;
            Address = address;

            HaveToClean = haveToClean;
            DeallocateOnClean = deallocateOnClean;

            Type = type;
            IsDiscarded = false;
            Size = size;
            IsInitialized = false;
        }

        readonly string GetDebuggerDisplay() => $"{Type} {Name} ({Type.Size} bytes at {Address})";
    }

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

        public void Dispose()
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

        public void Dispose()
        {
            if (DebugInfo is null) return;

            DebugInfo.FunctionInformations.Add(new FunctionInformations()
            {
                File = Uri,
                Identifier = Identifier,
                ReadableIdentifier = ReadableIdentifier,
                Instructions = (InstructionStart, Code.Length),
                IsMacro = false,
                IsValid = true,
                SourcePosition = Position,
            });
        }
    }

    struct GeneratorSnapshot
    {
        public readonly Variable[] Variables;
        public readonly int[] VariableCleanupStack;
        public readonly ControlFlowBlock[] Returns;
        public readonly ControlFlowBlock[] Breaks;
        public readonly bool[] InMacro;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int Optimizations;

        public readonly ISameCheck[] CurrentMacro;

        public string? VariableCanBeDiscarded;

        public readonly DebugInformation? DebugInfo;

        public GeneratorSnapshot(CodeGeneratorForBrainfuck v)
        {
            Variables = v.CompiledVariables.ToArray();

            VariableCleanupStack = v.VariableCleanupStack.ToArray();
            Returns = v.Returns.Duplicate().ToArray();
            Breaks = v.Breaks.Duplicate().ToArray();
            InMacro = v.InMacro.ToArray();

            Optimizations = v.Optimizations;

            CurrentMacro = v.CurrentMacro.ToArray();

            VariableCanBeDiscarded = v.VariableCanBeDiscarded;

            DebugInfo = v.DebugInfo?.Duplicate();
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
            Heap = new HeapCodeHelper(Code, generator.Heap.Start, generator.Heap.Size);
            Stack = new StackCodeHelper(Code, generator.Stack);
        }
    }

    struct GeneratorStackFrame
    {
        public Dictionary<string, GeneralType>? savedTypeArguments;
        public ControlFlowBlock[] savedBreaks;
        public Variable[] savedVariables;
        public Uri? savedFilePath;
        public IConstant[] savedConstants;
    }

    [SuppressMessage("Style", "IDE0017")]
    GeneratorStackFrame PushStackFrame(Dictionary<string, GeneralType>? typeArguments)
    {
        GeneratorStackFrame newFrame = new();

        newFrame.savedTypeArguments = null;
        if (typeArguments != null)
        { SetTypeArguments(typeArguments, out newFrame.savedTypeArguments); }

        newFrame.savedBreaks = Breaks.Duplicate().ToArray();
        Breaks.Clear();

        newFrame.savedVariables = CompiledVariables.ToArray();
        CompiledVariables.Clear();

        if (CurrentMacro.Count == 1)
        {
            CompiledVariables.PushRange(newFrame.savedVariables);
            for (int i = 0; i < CompiledVariables.Count; i++)
            { CompiledVariables[i] = new Variable(CompiledVariables[i].Name, CompiledVariables[i].Address, false, CompiledVariables[i].DeallocateOnClean, CompiledVariables[i].Type, CompiledVariables[i].Size); }
        }

        newFrame.savedFilePath = CurrentFile;

        newFrame.savedConstants = CompiledLocalConstants.ToArray();
        CompiledLocalConstants.Clear();

        return newFrame;
    }
    void PopStackFrame(GeneratorStackFrame frame)
    {
        CurrentFile = frame.savedFilePath;

        CompiledVariables.Set(frame.savedVariables);

        CompiledLocalConstants.Set(frame.savedConstants);

        if (Breaks.Count > 0)
        { throw new InternalException(); }

        Breaks.Set(frame.savedBreaks);

        if (frame.savedTypeArguments != null)
        { SetTypeArguments(frame.savedTypeArguments); }
    }

    #region Fields

    CodeHelper Code;
    StackCodeHelper Stack;
    HeapCodeHelper Heap;
    CodeHelper IBrainfuckGenerator.Code => Code;

    new readonly Stack<Variable> CompiledVariables;

    readonly Stack<int> VariableCleanupStack;

    readonly Stack<ControlFlowBlock> Returns;
    readonly Stack<ControlFlowBlock> Breaks;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    int Optimizations;

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
        CompiledVariables = new Stack<Variable>();
        Code = new CodeHelper();
        Stack = new StackCodeHelper(Code, 0, brainfuckSettings.StackSize);
        Heap = new HeapCodeHelper(Code, brainfuckSettings.HeapStart, brainfuckSettings.HeapSize);
        CurrentMacro = new Stack<ISameCheck>();
        VariableCleanupStack = new Stack<int>();
        Returns = new Stack<ControlFlowBlock>();
        Breaks = new Stack<ControlFlowBlock>();
        DebugInfo = brainfuckSettings.GenerateDebugInformation ? new DebugInformation(compilerResult.Raw.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.Key, v.Value.Tokens))) : null;
        MaxRecursiveDepth = 4;
        Settings = brainfuckSettings;
    }

    GeneratorSnapshot Snapshot() => new(this);
    void Restore(GeneratorSnapshot snapshot)
    {
        CompiledVariables.Clear();
        CompiledVariables.AddRange(snapshot.Variables);

        // Stack = snapshot.Stack;
        // Heap = snapshot.Heap;

        VariableCleanupStack.Clear();
        VariableCleanupStack.AddRange(snapshot.VariableCleanupStack);

        Returns.Clear();
        Returns.AddRange(snapshot.Returns);

        Breaks.Clear();
        Breaks.AddRange(snapshot.Breaks);

        InMacro.Clear();
        InMacro.AddRange(snapshot.InMacro);

        Optimizations = snapshot.Optimizations;

        CurrentMacro.Clear();
        CurrentMacro.AddRange(snapshot.CurrentMacro);

        VariableCanBeDiscarded = new string(snapshot.VariableCanBeDiscarded);

        DebugInfo = snapshot.DebugInfo?.Duplicate();
    }

    CodeSnapshot SnapshotCode() => new(this);
    void RestoreCode(CodeSnapshot snapshot)
    {
        Code = snapshot.Code.Duplicate();
        Heap = new HeapCodeHelper(Code, snapshot.Heap.Start, snapshot.Heap.Size);
        Stack = new StackCodeHelper(Code, snapshot.Stack);
    }

    DebugInfoBlock DebugBlock(IPositioned? position) => new(Code, DebugInfo, position, CurrentFile);
    DebugInfoBlock DebugBlock(Position position) => new(Code, DebugInfo, position, CurrentFile);

    DebugFunctionBlock<CompiledFunction> FunctionBlock(CompiledFunction function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function.FilePath,
        function.Identifier.Content,
        function.ToReadable(typeArguments),
        function.Position);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledOperator function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function.FilePath,
        function.Identifier.Content,
        function.ToReadable(typeArguments),
        function.Position);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledGeneralFunction function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function.FilePath,
        function.Identifier.Content,
        function.ToReadable(typeArguments),
        function.Position);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(CompiledConstructor function, Dictionary<string, GeneralType>? typeArguments) => new(
        Code,
        DebugInfo,
        function.FilePath,
        function.Type.ToString(),
        function.ToReadable(typeArguments),
        function.Position);

    protected override bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out GeneralType? type)
    {
        if (CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, symbolName, out Variable variable))
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

    protected override ValueAddress GetGlobalVariableAddress(CompiledVariable variable)
    {
        throw new NotImplementedException();
    }

    static bool GetVariable(IEnumerable<Variable> variables, string name, out Variable variable)
        => GetVariable(variables.ToArray(), name, out variable);

    static bool GetVariable(Variable[] variables, string name, out Variable variable)
    {
        for (int i = variables.Length - 1; i >= 0; i--)
        {
            if (variables[i].Name == name)
            {
                variable = variables[i];
                return true;
            }
        }
        variable = default;
        return false;
    }

    static void DiscardVariable(Stack<Variable> variables, string name)
    {
        for (int i = 0; i < variables.Count; i++)
        {
            if (variables[i].Name != name) continue;
            Variable v = variables[i];
            v.IsDiscarded = true;
            variables[i] = v;
            return;
        }
    }
    static void UndiscardVariable(Stack<Variable> variables, string name)
    {
        for (int i = 0; i < variables.Count; i++)
        {
            if (variables[i].Name != name) continue;
            Variable v = variables[i];
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
                Variable variable = CompiledVariables.Last;

                if (!variable.HaveToClean)
                {
                    CompiledVariables.Pop();
                    continue;
                }

                if (variable.DeallocateOnClean &&
                    variable.Type is PointerType)
                {
                    GenerateDestructor(
                        new TypeCast(
                            new Identifier(
                                Tokenizing.Token.CreateAnonymous(variable.Name, Tokenizing.TokenType.Identifier),
                                null
                                ),
                            Tokenizing.Token.CreateAnonymous("as"),
                            new TypeInstancePointer(
                                TypeInstanceSimple.CreateAnonymous("int"),
                                Tokenizing.Token.CreateAnonymous("*", Tokenizing.TokenType.Operator))
                            )
                        );
                }

                CompiledVariables.Pop();
                Stack.Pop();
            }
        }
    }

    int VariableUses(Statement statement, Variable variable)
    {
        int usages = 0;

        foreach (Statement _statement in statement.GetStatementsRecursively(true))
        {
            if (_statement is not Identifier identifier)
            { continue; }
            if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, identifier.Content, out Variable _variable))
            { continue; }
            if (_variable.Name != variable.Name)
            { continue; }

            usages++;
        }

        return usages;
    }

    #region GetValueSize
    int GetValueSize(StatementWithValue statement)
    {
        GeneralType statementType = FindStatementType(statement);

        if (statementType == BasicType.Void)
        { throw new CompilerException($"Statement \"{statement}\" (with type \"{statementType}\") does not have a size", statement, CurrentFile); }

        return statementType.Size;
    }
    #endregion

    #region TryGetAddress

    bool TryGetAddress(Statement? statement, out int address, out int size)
    {
        if (statement is null)
        {
            address = 0;
            size = 0;
            return false;
        }

        if (statement is IndexCall index)
        { return TryGetAddress(index, out address, out size); }

        if (statement is Pointer pointer)
        { return TryGetAddress(pointer, out address, out size); }

        if (statement is Identifier identifier)
        { return TryGetAddress(identifier, out address, out size); }

        if (statement is Field field)
        { return TryGetAddress(field, out address, out size); }

        throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile);
    }

    bool TryGetAddress(IndexCall index, out int address, out int size)
    {
        if (index.PrevStatement is not Identifier arrayIdentifier)
        { throw new CompilerException($"This must be an identifier", index.PrevStatement, CurrentFile); }

        if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, arrayIdentifier.Content, out Variable variable))
        { throw new CompilerException($"Variable \"{arrayIdentifier}\" not found", arrayIdentifier, CurrentFile); }

        if (variable.Type is ArrayType arrayType)
        {
            size = arrayType.Of.Size;
            address = variable.Address;

            if (size != 1)
            { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index, CurrentFile); }

            if (TryCompute(index.Index, out DataItem indexValue))
            {
                address = variable.Address + ((int)indexValue * 2 * arrayType.Of.Size);
                return true;
            }

            return false;
        }

        throw new CompilerException($"Variable is not an array", arrayIdentifier, CurrentFile);
    }

    bool TryGetAddress(Field field, out int address, out int size)
    {
        GeneralType type = FindStatementType(field.PrevStatement);

        if (type is StructType structType)
        {
            if (!TryGetAddress(field.PrevStatement, out int prevAddress, out _))
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

    bool TryGetAddress(Pointer pointer, out int address, out int size)
    {
        if (!TryCompute(pointer.PrevStatement, out DataItem addressToSet))
        { throw new NotSupportedException($"Runtime pointer address in not supported", pointer.PrevStatement, CurrentFile); }

        if (!DataItem.TryShrinkTo8bit(ref addressToSet))
        { throw new CompilerException($"Address value must be a byte (not {addressToSet.Type})", pointer.PrevStatement, CurrentFile); }

        address = addressToSet.VByte;
        size = 1;

        return true;
    }

    bool TryGetAddress(Identifier identifier, out int address, out int size)
    {
        if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, identifier.Content, out Variable variable))
        { throw new CompilerException($"Variable \"{identifier}\" not found", identifier, CurrentFile); }

        address = variable.Address;
        size = variable.Size;
        return true;
    }

    #endregion

    void GenerateTopLevelStatements(ImmutableArray<Statement> statements, Uri? file, bool isImported)
    {
        Print?.Invoke($"  Generating top level statements for file {file?.ToString() ?? "null"} ...", LogType.Debug);

        CurrentFile = file;

        if (!isImported)
        { CompiledVariables.Add(new Variable(ReturnVariableName, Stack.PushVirtual(1), false, false, new BuiltinType(BasicType.Integer))); }

        if (Settings.ClearGlobalVariablesBeforeExit)
        { VariableCleanupStack.Push(PrecompileVariables(statements)); }
        else
        { PrecompileVariables(statements); }

        ControlFlowBlock? returnBlock = BeginReturnBlock(null, FindControlFlowUsage(statements));

        {
            InMacro.Push(false);

            using ConsoleProgressBar progressBar = new(ConsoleColor.DarkGray, ShowProgress);

            for (int i = 0; i < statements.Length; i++)
            {
                progressBar.Print(i, statements.Length);
                GenerateCodeForStatement(statements[i]);
            }

            InMacro.Pop();
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
            (ImmutableArray<Statement> statements, Uri? file) = compilerResult.TopLevelStatements[i];
            GenerateTopLevelStatements(statements, file, i < compilerResult.TopLevelStatements.Length - 1);
        }

        // Heap.Destroy();

        if (Heap.IsUsed)
        {
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