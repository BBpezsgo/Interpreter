using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace LanguageCore.Brainfuck.Generator
{
    using System.Linq;
    using BBCode.Generator;
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

        public DebugInformation DebugInfo;
    }

    [Flags]
    public enum BrainfuckCompilerFlags
    {
        None = 0b_0000,

        PrintCompiled = 0b_0001,
        PrintCompiledMinimized = 0b_0010,
        WriteToFile = 0b_0100,

        PrintFinal = 0b_1000,
    }

    [Flags]
    public enum BrainfuckPrintFlags
    {
        None = 0b_0000,
        PrintResultLabel = 0b_0001,
        PrintExecutionTime = 0b_0010,
        PrintMemory = 0b_0100,
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
                    GenerateDebugInformation = false,
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

    public partial class CodeGeneratorForBrainfuck : CodeGeneratorNonGeneratorBase
    {
        const string ReturnVariableName = "@return";

        public readonly struct GeneratorCodeBlock : IDisposable
        {
            readonly CodeGeneratorForBrainfuck Generator;

            public GeneratorCodeBlock(CodeGeneratorForBrainfuck generator)
            {
                this.Generator = generator;
            }

            public void Dispose()
            {
                this.Generator.Code.EndBlock();
            }
        }

        public readonly struct GeneratorJumpBlock : IDisposable
        {
            readonly CodeGeneratorForBrainfuck Generator;
            readonly int ConditionAddress;

            public GeneratorJumpBlock(CodeGeneratorForBrainfuck generator, int conditionAddress)
            {
                this.Generator = generator;
                this.ConditionAddress = conditionAddress;
            }

            public void Dispose()
            {
                this.Generator.Code.JumpEnd(this.ConditionAddress);
            }
        }

        readonly struct DebugInfoBlock : IDisposable
        {
            readonly int InstructionStart;
            readonly CompiledCode Code;
            readonly DebugInformation? DebugInfo;
            readonly Position Position;

            public DebugInfoBlock(CompiledCode code, DebugInformation? debugInfo, Position position)
            {
                Code = code;
                DebugInfo = debugInfo;
                Position = position;

                if (debugInfo == null) return;
                if (position == Position.UnknownPosition) return;

                InstructionStart = code.GetFinalCode().Length;
            }

            public DebugInfoBlock(CompiledCode code, DebugInformation? debugInfo, IPositioned? position)
                : this(code, debugInfo, position?.Position ?? Position.UnknownPosition)
            {

            }

            public void Dispose()
            {
                if (DebugInfo == null) return;
                if (Position == Position.UnknownPosition) return;

                int end = Code.GetFinalCode().Length;
                if (InstructionStart == end) return;
                DebugInfo.SourceCodeLocations.Add(new SourceCodeLocation()
                {
                    Instructions = (InstructionStart, end),
                    SourcePosition = Position,
                });
            }
        }

        struct GeneratorSnapshot
        {
            public readonly Stack<Variable> Variables;

            public readonly Stack<int> VariableCleanupStack;
            public readonly Stack<int> ReturnCount;
            public readonly Stack<int> BreakCount;
            public readonly Stack<int> ReturnTagStack;
            public readonly Stack<int> BreakTagStack;
            public readonly Stack<bool> InMacro;

            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public int Optimizations;

            public readonly Stack<FunctionThingDefinition> CurrentMacro;

            public string? VariableCanBeDiscarded;

            public readonly DebugInformation DebugInfo;

            public GeneratorSnapshot(CodeGeneratorForBrainfuck v)
            {
                Variables = new Stack<Variable>(v.Variables);

                VariableCleanupStack = new Stack<int>(v.VariableCleanupStack);
                ReturnCount = new Stack<int>(v.ReturnCount);
                BreakCount = new Stack<int>(v.BreakCount);
                ReturnTagStack = new Stack<int>(v.ReturnTagStack);
                BreakTagStack = new Stack<int>(v.BreakTagStack);
                InMacro = new Stack<bool>(v.InMacro);

                Optimizations = v.Optimizations;

                CurrentMacro = new Stack<FunctionThingDefinition>(v.CurrentMacro);

                VariableCanBeDiscarded = new string(v.VariableCanBeDiscarded);

                DebugInfo = v.DebugInfo.Duplicate();
            }
        }

        readonly struct CodeSnapshot
        {
            public readonly CompiledCode Code;
            public readonly BasicHeapCodeHelper Heap;
            public readonly StackCodeHelper Stack;

            public CodeSnapshot(CodeGeneratorForBrainfuck generator)
            {
                Code = generator.Code.Duplicate();
                Heap = new BasicHeapCodeHelper(Code, generator.Heap.Start, generator.Heap.Size);
                if (generator.Heap.IsInitialized) Heap.InitVirtual();
                Stack = new StackCodeHelper(Code, generator.Stack);
            }
        }

        struct GeneratorStackFrame
        {
            public TypeArguments? savedTypeArguments;
            public int[] savedBreakTagStack;
            public int[] savedBreakCount;
            public Variable[] savedVariables;
            public Uri? savedFilePath;
            public CompiledConstant[] savedConstants;
        }

        [SuppressMessage("Style", "IDE0017")]
        GeneratorStackFrame PushStackFrame(TypeArguments? typeArguments)
        {
            GeneratorStackFrame newFrame = new();

            newFrame.savedTypeArguments = null;
            if (typeArguments != null)
            { SetTypeArguments(typeArguments, out newFrame.savedTypeArguments); }

            newFrame.savedBreakTagStack = BreakTagStack.ToArray();
            BreakTagStack.Clear();

            newFrame.savedBreakCount = BreakCount.ToArray();
            BreakCount.Clear();

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

            if (BreakCount.Count > 0 ||
                BreakTagStack.Count > 0)
            { throw new InternalException(); }

            BreakCount.Set(frame.savedBreakCount);

            BreakTagStack.Set(frame.savedBreakTagStack);

            if (frame.savedTypeArguments != null)
            { SetTypeArguments(frame.savedTypeArguments); }
        }

        #region Fields

        CompiledCode Code;
        StackCodeHelper Stack;
        BasicHeapCodeHelper Heap;

        readonly Stack<Variable> Variables;

        readonly Stack<int> VariableCleanupStack;
        readonly Stack<int> ReturnCount;
        readonly Stack<int> BreakCount;
        /// <summary> Contains the "return tag" address </summary>
        readonly Stack<int> ReturnTagStack;
        /// <summary> Contains the "break tag" address </summary>
        readonly Stack<int> BreakTagStack;
        readonly Stack<bool> InMacro;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        int Optimizations;

        readonly Stack<FunctionThingDefinition> CurrentMacro;

        readonly BrainfuckGeneratorSettings GeneratorSettings;

        string? VariableCanBeDiscarded;

        DebugInformation DebugInfo;

        readonly bool ShowProgress;

        readonly PrintCallback? PrintCallback;

        readonly bool GenerateDebugInformation;

        readonly int MaxRecursiveDepth;

        #endregion

        public CodeGeneratorForBrainfuck(CompilerResult compilerResult, BrainfuckGeneratorSettings settings, PrintCallback? printCallback, AnalysisCollection? analysisCollection) : base(compilerResult, LanguageCore.Compiler.GeneratorSettings.Default, analysisCollection)
        {
            this.Variables = new Stack<Variable>();
            this.Code = new CompiledCode();
            this.Stack = new StackCodeHelper(this.Code, settings.StackStart, settings.StackSize);
            this.Heap = new BasicHeapCodeHelper(this.Code, settings.HeapStart, settings.HeapSize);
            this.CurrentMacro = new Stack<FunctionThingDefinition>();
            this.VariableCleanupStack = new Stack<int>();
            this.GeneratorSettings = settings;
            this.ReturnCount = new Stack<int>();
            this.ReturnTagStack = new Stack<int>();
            this.BreakCount = new Stack<int>();
            this.BreakTagStack = new Stack<int>();
            this.InMacro = new Stack<bool>();
            this.DebugInfo = new DebugInformation();
            this.PrintCallback = printCallback;
            this.GenerateDebugInformation = settings.GenerateDebugInformation;
            this.ShowProgress = settings.ShowProgress;
            this.MaxRecursiveDepth = 4;
        }

        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        struct Variable
        {
            public readonly string Name;
            public readonly int Address;

            public readonly bool HaveToClean;
            public readonly bool DeallocateOnClean;

            public readonly CompiledType Type;
            public readonly int Size;

            public bool IsDiscarded;
            public bool IsInitialized;

            public Variable(string name, int address, bool haveToClean, bool deallocateOnClean, CompiledType type)
                : this(name, address, haveToClean, deallocateOnClean, type, type.Size) { }
            public Variable(string name, int address, bool haveToClean, bool deallocateOnClean, CompiledType type, int size)
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

        GeneratorSnapshot Snapshot() => new(this);
        void Restore(GeneratorSnapshot snapshot)
        {
            Variables.Clear();
            Variables.AddRange(snapshot.Variables);

            // Stack = snapshot.Stack;
            // Heap = snapshot.Heap;

            VariableCleanupStack.Clear();
            VariableCleanupStack.AddRange(snapshot.VariableCleanupStack);

            ReturnCount.Clear();
            ReturnCount.AddRange(snapshot.ReturnCount);

            BreakCount.Clear();
            BreakCount.AddRange(snapshot.BreakCount);

            ReturnTagStack.Clear();
            ReturnTagStack.AddRange(snapshot.ReturnTagStack);

            BreakTagStack.Clear();
            BreakTagStack.AddRange(snapshot.BreakTagStack);

            InMacro.Clear();
            InMacro.AddRange(snapshot.InMacro);

            Optimizations = snapshot.Optimizations;

            CurrentMacro.Clear();
            CurrentMacro.AddRange(snapshot.CurrentMacro);

            VariableCanBeDiscarded = new string(snapshot.VariableCanBeDiscarded);

            DebugInfo = snapshot.DebugInfo.Duplicate();
        }

        CodeSnapshot SnapshotCode() => new(this);
        void RestoreCode(CodeSnapshot snapshot)
        {
            Code = snapshot.Code.Duplicate();
            Heap = new BasicHeapCodeHelper(Code, snapshot.Heap.Start, snapshot.Heap.Size);
            if (snapshot.Heap.IsInitialized) Heap.InitVirtual();
            Stack = new StackCodeHelper(Code, snapshot.Stack);
        }

        /// <summary>
        /// <b>Pointer:</b> <paramref name="conditionAddress"/>
        /// </summary>
        public GeneratorJumpBlock JumpBlock(int conditionAddress)
        {
            Code.JumpStart(conditionAddress);
            return new GeneratorJumpBlock(this, conditionAddress);
        }

        public GeneratorCodeBlock CommentBlock()
        {
            Code.StartBlock();
            return new GeneratorCodeBlock(this);
        }
        public GeneratorCodeBlock CommentBlock(string label)
        {
            Code.StartBlock(label);
            return new GeneratorCodeBlock(this);
        }

        DebugInfoBlock DebugBlock(IPositioned? position) => new(Code, GenerateDebugInformation ? DebugInfo : null, position);

        protected override bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out CompiledType? type)
        {
            if (CodeGeneratorForBrainfuck.GetVariable(Variables, symbolName, out Variable variable))
            {
                type = variable.Type;
                return true;
            }

            if (GetConstant(symbolName, out DataItem constant))
            {
                type = new CompiledType(constant.Type);
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
            using (CommentBlock($"Clean up variables ({n})"))
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
                        variable.Type.IsPointer)
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
            CompiledType statementType = FindStatementType(statement);

            if (statementType == Type.Void)
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

            if (variable.Type.IsStackArray)
            {
                size = variable.Type.StackArrayOf.Size;
                address = variable.Address;

                if (size != 1)
                { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index, CurrentFile); }

                if (TryCompute(index.Expression, out DataItem indexValue))
                {
                    address = variable.Address + ((int)indexValue * 2 * variable.Type.StackArrayOf.Size);
                    return true;
                }

                return false;
            }

            throw new CompilerException($"Variable is not an array", arrayIdentifier, CurrentFile);
        }

        bool TryGetAddress(Field field, out int address, out int size)
        {
            CompiledType type = FindStatementType(field.PrevStatement);

            if (type.IsStruct)
            {
                if (!TryGetAddress(field.PrevStatement, out int prevAddress, out _))
                {
                    address = default;
                    size = default;
                    return false;
                }

                CompiledType fieldType = FindStatementType(field);

                CompiledStruct @struct = type.Struct;

                address = @struct.FieldOffsets[field.FieldName.Content] + prevAddress;
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

            if (!DataItem.TryShrinkToByte(ref addressToSet))
            { throw new CompilerException($"Address value must be a byte (not {addressToSet.Type})", pointer.PrevStatement, CurrentFile); }

            address = addressToSet.ValueUInt8;
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
            PrintCallback?.Invoke("  Precompiling ...", LogType.Debug);

            CurrentFile = compilerResult.File;

            int constantCount = CompileConstants(compilerResult.TopLevelStatements);

            Variable returnVariable = new(ReturnVariableName, Stack.PushVirtual(1), false, false, new CompiledType(Type.Integer));
            Variables.Add(returnVariable);

            if (GeneratorSettings.ClearGlobalVariablesBeforeExit)
            { VariableCleanupStack.Push(PrecompileVariables(compilerResult.TopLevelStatements)); }
            else
            { PrecompileVariables(compilerResult.TopLevelStatements); }

            Heap.Init();

            using (CommentBlock($"Begin \"return\" block (depth: {ReturnTagStack.Count} (now its one more))"))
            {
                ReturnCount.Push(0);
                ReturnTagStack.Push(Stack.Push(1));
            }

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

            FinishReturnStatements();

            using (CommentBlock($"Finish \"return\" block"))
            {
                if (ReturnTagStack.Pop() != Stack.LastAddress)
                { throw new InternalException(); }
                Stack.Pop();
            }

            if (ReturnCount.Count > 0 ||
                ReturnTagStack.Count > 0 ||
                BreakCount.Count > 0 ||
                BreakTagStack.Count > 0)
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
            analysisCollection
        ).GenerateCode(compilerResult);
    }
}