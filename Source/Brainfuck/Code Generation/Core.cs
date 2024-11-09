﻿using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;

namespace LanguageCore.Brainfuck.Generator;

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
                SourcePosition = Position,
                Uri = File,
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

        public readonly ImmutableArray<IDefinition> CurrentMacro;

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

    new readonly Stack<BrainfuckVariable> CompiledVariables;

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
        DebugInfo = brainfuckSettings.GenerateDebugInformation ? new DebugInformation(compilerResult.Raw.Select(v => new KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>(v.Key, v.Value.Tokens.Tokens))) : null;
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
            { CompiledVariables[i] = new BrainfuckVariable(CompiledVariables[i].Name, CompiledVariables[i].File, CompiledVariables[i].Address, CompiledVariables[i].IsReference, false, CompiledVariables[i].DeallocateOnClean, CompiledVariables[i].Type, CompiledVariables[i].Size); }
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
        if (GetVariable(symbolName.Content, out BrainfuckVariable? variable, out _))
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

        if (!prevType.Is(out StructType? structType))
        { throw new NotImplementedException(); }

        if (!structType.GetField(field.Identifier.Content, this, out _, out int fieldOffset, out PossibleDiagnostic? error))
        {
            Diagnostics.Add(error.ToError(field.Identifier, field.File));
            return default;
        }

        int prevOffset = GetDataOffset(field.PrevStatement, until);
        return prevOffset + fieldOffset;
    }
    int GetDataOffset(IndexCall indexCall, StatementWithValue? until = null)
    {
        if (indexCall.PrevStatement == until) return 0;

        GeneralType prevType = FindStatementType(indexCall.PrevStatement);

        if (!prevType.Is(out ArrayType? arrayType))
        {
            Diagnostics.Add(Diagnostic.Critical($"Only stack arrays supported by now and this is not one", indexCall.PrevStatement));
            return default;
        }

        if (!TryCompute(indexCall.Index, out CompiledValue index))
        {
            Diagnostics.Add(Diagnostic.Critical($"Can't compute the index value", indexCall.Index));
            return default;
        }

        int prevOffset = GetDataOffset(indexCall.PrevStatement, until);
        int offset = (int)index * 2 * arrayType.Of.GetSize(this, Diagnostics, indexCall.PrevStatement);
        return prevOffset + offset;
    }

    StatementWithValue? NeedDereference(StatementWithValue value) => value switch
    {
        Identifier => null,
        Field v => NeedDereference(v),
        IndexCall v => NeedDereference(v),
        _ => throw new NotImplementedException()
    };
    StatementWithValue? NeedDereference(IndexCall indexCall)
    {
        if (FindStatementType(indexCall.PrevStatement).Is<PointerType>())
        { return indexCall.PrevStatement; }

        return NeedDereference(indexCall.PrevStatement);
    }
    StatementWithValue? NeedDereference(Field field)
    {
        if (FindStatementType(field.PrevStatement).Is<PointerType>())
        { return field.PrevStatement; }

        return NeedDereference(field.PrevStatement);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    bool TryGetAddress(Statement statement, out int address, out int size, StatementWithValue? until = null)
    {
        switch (statement)
        {
            case IndexCall index: return TryGetAddress(index, out address, out size, until);
            case Pointer pointer: return TryGetAddress(pointer, out address, out size, until);
            case Identifier identifier: return TryGetAddress(identifier, out address, out size, until);
            case Field field: return TryGetAddress(field, out address, out size, until);
            default:
            {
                Diagnostics.Add(Diagnostic.Critical($"Unknown statement \"{statement.GetType().Name}\"", statement));
                address = default;
                size = default;
                return false;
            }
        }
    }
    bool TryGetAddress(IndexCall index, out int address, out int size, StatementWithValue? until)
    {
        if (index.PrevStatement is not Identifier arrayIdentifier)
        {
            Diagnostics.Add(Diagnostic.Critical($"This must be an identifier", index.PrevStatement));
            address = default;
            size = default;
            return default;
        }

        if (!GetVariable(arrayIdentifier.Content, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(arrayIdentifier));
            address = default;
            size = default;
            return false;
        }

        if (variable.Type.Is(out ArrayType? arrayType))
        {
            size = arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement);
            address = variable.Address;

            if (size != 1)
            { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index); }

            if (TryCompute(index.Index, out CompiledValue indexValue))
            {
                address = variable.Address + ((int)indexValue * 2 * arrayType.Of.GetSize(this, Diagnostics, index.PrevStatement));
                return true;
            }

            return false;
        }

        Diagnostics.Add(Diagnostic.Critical($"Variable is not an array", arrayIdentifier));
        address = default;
        size = default;
        return default;
    }
    bool TryGetAddress(Field field, out int address, out int size, StatementWithValue? until)
    {
        GeneralType type = FindStatementType(field.PrevStatement);

        if (type.Is(out StructType? structType))
        {
            if (!TryGetAddress(field.PrevStatement, out int prevAddress, out _, until))
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

            address = fieldOffset + prevAddress;
            size = fieldType.GetSize(this, Diagnostics, field);
            return true;
        }

        address = default;
        size = default;
        return false;
    }
    bool TryGetAddress(Pointer pointer, out int address, out int size, StatementWithValue? until)
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

        address = addressToSet.U8;
        size = 1;
        return true;
    }
    bool TryGetAddress(Identifier identifier, out int address, out int size, StatementWithValue? until)
    {
        if (!GetVariable(identifier.Content, out BrainfuckVariable? variable, out PossibleDiagnostic? notFoundError))
        {
            Diagnostics.Add(notFoundError.ToError(identifier));
            address = default;
            size = default;
            return false;
        }

        address = variable.Address;
        size = variable.Size;
        return true;
    }
#pragma warning restore IDE0060 // Remove unused parameter

    #endregion

    bool GetVariable(string name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError) => GetVariable(CompiledVariables, name, out variable, out notFoundError);
    bool GetVariable(StatementWithValue name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError) => GetVariable(CompiledVariables, name, out variable, out notFoundError);

    static bool GetVariable(Stack<BrainfuckVariable> variables, string name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError)
    {
        for (int i = variables.Count - 1; i >= 0; i--)
        {
            if (variables[i].Name != name) continue;

            variable = variables[i];
            notFoundError = null;
            return true;
        }

        variable = null;
        notFoundError = new PossibleDiagnostic($"Variable \"{name}\" not found");
        return false;
    }
    bool GetVariable(Stack<BrainfuckVariable> variables, StatementWithValue name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError)
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
    bool GetVariable(Stack<BrainfuckVariable> variables, Identifier name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError) => GetVariable(variables, name.Content, out variable, out notFoundError);
    bool GetVariable(Stack<BrainfuckVariable> variables, Pointer name, [NotNullWhen(true)] out BrainfuckVariable? variable, [NotNullWhen(false)] out PossibleDiagnostic? notFoundError)
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
            if (!GetVariable(identifier.Content, out BrainfuckVariable? _variable, out _))
            { continue; }
            if (_variable.Name != variable.Name)
            { continue; }

            usages++;
        }

        return usages;
    }

    void GenerateTopLevelStatements(ImmutableArray<Statement> statements, Uri file, bool isImported)
    {
        if (statements.Length == 0) return;

        Print?.Invoke($"  Generating top level statements for file \"{file.ToString() ?? "null"}\" ...", LogType.Debug);

        if (!isImported)
        { CompiledVariables.Add(new BrainfuckVariable(ReturnVariableName, file, Stack.PushVirtual(1, new Location(statements[0].Position.Before(), statements[0].File)), false, false, false, ExitCodeType, ExitCodeType.GetSize(this))); }

        if (Settings.ClearGlobalVariablesBeforeExit)
        { VariableCleanupStack.Push(PrecompileVariables(statements)); }
        else
        { PrecompileVariables(statements); }

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

        if (Settings.ClearGlobalVariablesBeforeExit)
        { CleanupVariables(VariableCleanupStack.Pop()); }

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

        for (int i = 0; i < compilerResult.TopLevelStatements.Length; i++)
        {
            (ImmutableArray<Statement> statements, Uri file) = compilerResult.TopLevelStatements[i];
            GenerateTopLevelStatements(statements, file, i < compilerResult.TopLevelStatements.Length - 1);
        }

        if (Heap.IsUsed)
        {
            if (Settings.CleanupHeap)
            { Heap.Destroy(); }

            string? heapInit = Heap.LateInit(out PossibleDiagnostic? heapInitError);
            heapInitError?.Throw();
            Code.Insert(0, heapInit);
        }

        if (Code.BranchDepth != 0)
        { throw new InternalExceptionWithoutContext($"Unbalanced branches"); }

        Print?.Invoke($"Used stack size: {Stack.MaxUsedSize} bytes", LogType.Debug);

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
        PrintCallback? printCallback,
        DiagnosticsCollection diagnostics)
    => new CodeGeneratorForBrainfuck(
        compilerResult,
        brainfuckSettings,
        diagnostics,
        printCallback).GenerateCode(compilerResult);
}
