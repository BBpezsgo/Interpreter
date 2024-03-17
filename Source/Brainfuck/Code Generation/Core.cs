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
    public int StackStart;
    public int StackSize;
    public int HeapStart;
    public int HeapSize;
    public bool GenerateDebugInformation;
    public bool ShowProgress;

    public static BrainfuckGeneratorSettings Default
    {
        get
        {
            BrainfuckGeneratorSettings result = new()
            {
                ClearGlobalVariablesBeforeExit = true,
                StackStart = 0,
                HeapStart = 64,
                HeapSize = 64,
                GenerateDebugInformation = true,
                ShowProgress = true,
            };
            result.StackSize = result.HeapStart - 1;
            return result;
        }
    }

    public BrainfuckGeneratorSettings(BrainfuckGeneratorSettings other)
    {
        ClearGlobalVariablesBeforeExit = other.ClearGlobalVariablesBeforeExit;
        StackStart = other.StackStart;
        StackSize = other.StackSize;
        HeapStart = other.HeapStart;
        HeapSize = other.HeapSize;
        GenerateDebugInformation = other.GenerateDebugInformation;
        ShowProgress = other.ShowProgress;
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
        readonly CompiledCode Code;
        readonly DebugInformation? DebugInfo;
        readonly Position Position;
        readonly Uri? CurrentFile;

        public DebugInfoBlock(CompiledCode code, DebugInformation? debugInfo, Position position, Uri? currentFile)
        {
            Code = code;
            DebugInfo = debugInfo;
            Position = position;

            if (debugInfo is null) return;
            if (position == Position.UnknownPosition) return;

            InstructionStart = code.Code.Length;
            CurrentFile = currentFile;
        }

        public DebugInfoBlock(CompiledCode code, DebugInformation? debugInfo, IPositioned? position, Uri? currentFile)
            : this(code, debugInfo, position?.Position ?? Position.UnknownPosition, currentFile)
        { }

        public void Dispose()
        {
            if (DebugInfo is null) return;
            if (Position == Position.UnknownPosition) return;

            int end = Code.Code.Length;
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
        readonly CompiledCode Code;
        readonly DebugInformation? DebugInfo;
        readonly Uri? Uri;
        readonly string Identifier;
        readonly string ReadableIdentifier;
        readonly Position Position;

        public DebugFunctionBlock(CompiledCode code, DebugInformation? debugInfo, Uri? uri, string identifier, string readableIdentifier, Position position)
        {
            Code = code;
            DebugInfo = debugInfo;
            Position = position;
            Uri = uri;
            Identifier = identifier;
            ReadableIdentifier = readableIdentifier;

            if (debugInfo is null) return;
            if (position == Position.UnknownPosition) return;

            InstructionStart = code.Code.Length;
        }

        public void Dispose()
        {
            if (DebugInfo is null) return;

            DebugInfo.FunctionInformations.Add(new FunctionInformations()
            {
                File = Uri,
                Identifier = Identifier,
                ReadableIdentifier = ReadableIdentifier,
                Instructions = (InstructionStart, Code.Code.Length),
                IsMacro = false,
                IsValid = true,
                SourcePosition = Position,
            });
        }
    }

    struct GeneratorSnapshot
    {
        public readonly Stack<Variable> Variables;

        public readonly Stack<int> VariableCleanupStack;
        public readonly Stack<ControlFlowBlock> Returns;
        public readonly Stack<ControlFlowBlock> Breaks;
        public readonly Stack<bool> InMacro;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public int Optimizations;

        public readonly Stack<ISameCheck> CurrentMacro;

        public string? VariableCanBeDiscarded;

        public readonly DebugInformation? DebugInfo;

        public GeneratorSnapshot(CodeGeneratorForBrainfuck v)
        {
            Variables = new Stack<Variable>(v.Variables);

            VariableCleanupStack = new Stack<int>(v.VariableCleanupStack);
            Returns = new Stack<ControlFlowBlock>(v.Returns.Duplicate());
            Breaks = new Stack<ControlFlowBlock>(v.Breaks.Duplicate());
            InMacro = new Stack<bool>(v.InMacro);

            Optimizations = v.Optimizations;

            CurrentMacro = new Stack<ISameCheck>(v.CurrentMacro);

            VariableCanBeDiscarded = new string(v.VariableCanBeDiscarded);

            DebugInfo = v.DebugInfo?.Duplicate();
        }
    }

    readonly struct CodeSnapshot
    {
        public readonly CompiledCode Code;
        public readonly HeapCodeHelper Heap;
        public readonly StackCodeHelper Stack;

        public CodeSnapshot(CodeGeneratorForBrainfuck generator)
        {
            Code = generator.Code.Duplicate();
            Heap = new HeapCodeHelper(Code, generator.Heap.Start, generator.Heap.Size);
            if (generator.Heap.IsInitialized) Heap.InitVirtual();
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

        newFrame.savedVariables = Variables.ToArray();
        Variables.Clear();

        if (CurrentMacro.Count == 1)
        {
            Variables.PushRange(newFrame.savedVariables);
            for (int i = 0; i < Variables.Count; i++)
            { Variables[i] = new Variable(Variables[i].Name, Variables[i].Address, false, Variables[i].DeallocateOnClean, Variables[i].Type, Variables[i].Size); }
        }

        newFrame.savedFilePath = CurrentFile;

        newFrame.savedConstants = CompiledConstants.ToArray();
        CompiledConstants.Clear();

        return newFrame;
    }
    void PopStackFrame(GeneratorStackFrame frame)
    {
        CurrentFile = frame.savedFilePath;

        Variables.Set(frame.savedVariables);

        CompiledConstants.Set(frame.savedConstants);

        if (Breaks.Count > 0)
        { throw new InternalException(); }

        Breaks.Set(frame.savedBreaks);

        if (frame.savedTypeArguments != null)
        { SetTypeArguments(frame.savedTypeArguments); }
    }

    #region Fields

    CompiledCode Code;
    StackCodeHelper Stack;
    HeapCodeHelper Heap;
    CompiledCode IBrainfuckGenerator.Code => Code;

    readonly Stack<Variable> Variables;

    readonly Stack<int> VariableCleanupStack;

    readonly Stack<ControlFlowBlock> Returns;
    readonly Stack<ControlFlowBlock> Breaks;

    readonly Stack<bool> InMacro;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    int Optimizations;

    readonly Stack<ISameCheck> CurrentMacro;

    readonly BrainfuckGeneratorSettings GeneratorSettings;

    string? VariableCanBeDiscarded;

    DebugInformation? DebugInfo;

    readonly bool ShowProgress;

    readonly PrintCallback? PrintCallback;

    readonly int MaxRecursiveDepth;

    #endregion

    public CodeGeneratorForBrainfuck(CompilerResult compilerResult, BrainfuckGeneratorSettings settings, PrintCallback? printCallback, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, LanguageCore.Compiler.GeneratorSettings.Default, analysisCollection, print)
    {
        this.Variables = new Stack<Variable>();
        this.Code = new CompiledCode();
        this.Stack = new StackCodeHelper(this.Code, settings.StackStart, settings.StackSize);
        this.Heap = new HeapCodeHelper(this.Code, settings.HeapStart, settings.HeapSize);
        this.CurrentMacro = new Stack<ISameCheck>();
        this.VariableCleanupStack = new Stack<int>();
        this.GeneratorSettings = settings;
        this.Returns = new Stack<ControlFlowBlock>();
        this.Breaks = new Stack<ControlFlowBlock>();
        this.InMacro = new Stack<bool>();
        this.DebugInfo = settings.GenerateDebugInformation ? new DebugInformation(compilerResult.Tokens) : null;
        this.PrintCallback = printCallback;
        this.ShowProgress = settings.ShowProgress;
        this.MaxRecursiveDepth = 4;
    }

    GeneratorSnapshot Snapshot() => new(this);
    void Restore(GeneratorSnapshot snapshot)
    {
        Variables.Clear();
        Variables.AddRange(snapshot.Variables);

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
        if (snapshot.Heap.IsInitialized) Heap.InitVirtual();
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
        function.Identifier.ToString(),
        function.ToReadable(typeArguments),
        function.Position);

    DebugFunctionBlock<CompiledOperator> FunctionBlock(MacroDefinition function) => new(
        Code,
        DebugInfo,
        function.FilePath,
        function.Identifier.ToString(),
        function.ToReadable(),
        function.Position);

    protected override bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out GeneralType? type)
    {
        if (CodeGeneratorForBrainfuck.GetVariable(Variables, symbolName, out Variable variable))
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
                Variable variable = Variables.Last;

                if (!variable.HaveToClean)
                {
                    Variables.Pop();
                    continue;
                }

                if (variable.DeallocateOnClean &&
                    variable.Type is PointerType)
                {
                    GenerateDeallocator(
                        new TypeCast(
                            new Identifier(
                                Tokenizing.Token.CreateAnonymous(variable.Name, Tokenizing.TokenType.Identifier)
                                ),
                            Tokenizing.Token.CreateAnonymous("as"),
                            new TypeInstancePointer(
                                TypeInstanceSimple.CreateAnonymous("int"),
                                Tokenizing.Token.CreateAnonymous("*", Tokenizing.TokenType.Operator))
                            )
                        );
                }

                Variables.Pop();
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
            if (!CodeGeneratorForBrainfuck.GetVariable(Variables, identifier.Content, out Variable _variable))
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

        if (!CodeGeneratorForBrainfuck.GetVariable(Variables, arrayIdentifier.Content, out Variable variable))
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

            GeneralType fieldType = FindStatementType(field);

            address = structType.Struct.FieldOffsets[field.Identifier.Content] + prevAddress;
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
        if (!CodeGeneratorForBrainfuck.GetVariable(Variables, identifier.Content, out Variable variable))
        { throw new CompilerException($"Variable \"{identifier}\" not found", identifier, CurrentFile); }

        address = variable.Address;
        size = variable.Size;
        return true;
    }

    #endregion

    BrainfuckGeneratorResult GenerateCode(CompilerResult compilerResult)
    {
        PrintCallback?.Invoke("Generating code ...", LogType.Debug);
        PrintCallback?.Invoke("  Precompiling ...", LogType.Debug);

        CurrentFile = compilerResult.File;

        int constantCount = CompileConstants(compilerResult.TopLevelStatements);

        Variable returnVariable = new(ReturnVariableName, Stack.PushVirtual(1), false, false, new BuiltinType(BasicType.Integer));
        Variables.Add(returnVariable);

        if (GeneratorSettings.ClearGlobalVariablesBeforeExit)
        { VariableCleanupStack.Push(PrecompileVariables(compilerResult.TopLevelStatements)); }
        else
        { PrecompileVariables(compilerResult.TopLevelStatements); }

        Heap.Init();

        ControlFlowBlock? returnBlock = BeginReturnBlock(null, FindControlFlowUsage(compilerResult.TopLevelStatements));

        {
            InMacro.Push(false);
            PrintCallback?.Invoke("  Generating top level statements ...", LogType.Debug);

            using ConsoleProgressBar progressBar = new(ConsoleColor.DarkGray, ShowProgress);

            for (int i = 0; i < compilerResult.TopLevelStatements.Length; i++)
            {
                progressBar.Print(i, compilerResult.TopLevelStatements.Length);
                GenerateCodeForStatement(compilerResult.TopLevelStatements[i]);
            }
            InMacro.Pop();
        }

        PrintCallback?.Invoke("  Finishing up ...", LogType.Debug);

        if (returnBlock is not null)
        { FinishReturnStatements(Returns.Pop(), true); }

        if (Returns.Count > 0 ||
            Breaks.Count > 0)
        { throw new InternalException(); }

        if (GeneratorSettings.ClearGlobalVariablesBeforeExit)
        { CleanupVariables(VariableCleanupStack.Pop()); }

        CompiledConstants.Pop(constantCount);

        // Heap.Destroy();

        Code.SetPointer(0);

        if (Code.BranchDepth != 0)
        { throw new InternalException($"Unbalanced branches", CurrentFile); }

        CurrentFile = null;

        return new BrainfuckGeneratorResult()
        {
            Code = Code.ToString(),
            Optimizations = Optimizations,
            DebugInfo = DebugInfo,
        };
    }

    public static BrainfuckGeneratorResult Generate(
        CompilerResult compilerResult,
        BrainfuckGeneratorSettings generatorSettings,
        PrintCallback? printCallback = null,
        AnalysisCollection? analysisCollection = null)
    => new CodeGeneratorForBrainfuck(
        compilerResult,
        generatorSettings,
        printCallback,
        analysisCollection,
        printCallback).GenerateCode(compilerResult);
}