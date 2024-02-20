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

    readonly struct ConsoleProgressBar : IDisposable
    {
        readonly int Line;
        readonly ConsoleColor Color;
        readonly bool Show;

        public ConsoleProgressBar(ConsoleColor color, bool show)
        {
            Line = 0;
            Color = color;
            Show = show;

            if (!Show) return;

            Line = Console.GetCursorPosition().Top;
            Console.WriteLine();
        }

        public void Print(int iterator, int count) => Print((float)(iterator) / (float)count);
        public void Print(float progress)
        {
            if (!Show) return;

            (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

            Console.SetCursorPosition(0, Line);

            int width = Console.WindowWidth;
            Console.ForegroundColor = Color;
            for (int i = 0; i < width; i++)
            {
                float v = (float)(i + 1) / (float)(width);
                if (v <= progress)
                { Console.Write('═'); }
                else
                { Console.Write(' '); }
            }
            Console.ResetColor();

            Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top);
        }

        public void Clear()
        {
            if (!Show) return;

            (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

            Console.SetCursorPosition(0, Line);

            int width = Console.WindowWidth;
            for (int i = 0; i < width; i++)
            { Console.Write(' '); }

            Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top - 1);
        }

        public void Dispose() => Clear();
    }

    readonly struct ConsoleProgressLabel : IDisposable
    {
        readonly int Line;
        readonly string Label;
        readonly ConsoleColor Color;
        readonly bool Show;

        public ConsoleProgressLabel(string label, ConsoleColor color, bool show)
        {
            Line = 0;
            Label = label;
            Color = color;
            Show = show;

            if (!Show) return;

            Line = Console.GetCursorPosition().Top;
            Console.WriteLine();
        }

        public void Print()
        {
            if (!Show) return;

            (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

            Console.SetCursorPosition(0, Line);

            int width = Console.WindowWidth;
            Console.ForegroundColor = Color;
            for (int i = 0; i < width; i++)
            {
                if (i < Label.Length)
                { Console.Write(Label[i]); }
                else
                { Console.Write(' '); }
            }
            Console.ResetColor();

            Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top);
        }

        public void Clear()
        {
            if (!Show) return;

            (int Left, int Top) prevCursorPosition = Console.GetCursorPosition();

            Console.SetCursorPosition(0, Line);

            int width = Console.WindowWidth;
            for (int i = 0; i < width; i++)
            { Console.Write(' '); }

            Console.SetCursorPosition(prevCursorPosition.Left, prevCursorPosition.Top - 1);
        }

        public void Dispose() => Clear();
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
                    ShowProgress = false,
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

            public readonly StackCodeHelper Stack;
            public readonly BasicHeapCodeHelper Heap;

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

                Stack = v.Stack;
                Heap = v.Heap;

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

        struct GeneratorStackFrame
        {
            public TypeArguments? savedTypeArguments;
            public int[] savedBreakTagStack;
            public int[] savedBreakCount;
            public Variable[] savedVariables;
            public string? savedFilePath;
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
                { Variables[i] = new Variable(Variables[i].Name, Variables[i].Address, Variables[i].Scope, false, Variables[i].DeallocateOnClean, Variables[i].Type, Variables[i].Size); }
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

        readonly Stack<Variable> Variables;

        readonly StackCodeHelper Stack;
        readonly BasicHeapCodeHelper Heap;

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
            public readonly FunctionThingDefinition? Scope;

            public readonly bool HaveToClean;
            public readonly bool DeallocateOnClean;

            public readonly CompiledType Type;
            public readonly int Size;
            public bool IsDiscarded;
            public bool IsInitialValueSet;

            public readonly bool IsInitialized => Type.SizeOnStack > 0;

            public Variable(string name, int address, FunctionThingDefinition? scope, bool haveToClean, bool deallocateOnClean, CompiledType type, int size)
            {
                Name = name;
                Address = address;
                Scope = scope;

                HaveToClean = haveToClean;
                DeallocateOnClean = deallocateOnClean;

                Type = type;
                IsDiscarded = false;
                Size = size;
                IsInitialValueSet = false;
            }

            readonly string GetDebuggerDisplay() => $"{Type} {Name} ({Type.SizeOnStack} bytes at {Address})";
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
            using (Code.Block($"Clean up variables ({n})"))
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
                        variable.Type.InHEAP)
                    {
                        GenerateDeallocator(
                            new TypeCast(
                                new Identifier(
                                    Tokenizing.Token.CreateAnonymous(variable.Name, Tokenizing.TokenType.Identifier)
                                    ),
                                Tokenizing.Token.CreateAnonymous("as"),
                                new TypeInstancePointer(
                                    TypeInstanceSimple.CreateAnonymous("int", TypeDefinitionReplacer),
                                    Tokenizing.Token.CreateAnonymous("*", Tokenizing.TokenType.Operator))
                                )
                            );
                    }

                    Variables.Pop();
                    Stack.Pop();
                }
            }
        }

        bool SafeToDiscardVariable(Statement statement, Variable variable)
        {
            return false;

            int usages = 0;

            foreach (Statement _statement in statement)
            {
                if (_statement == null) continue;

                if (_statement is Identifier identifier &&
                    CodeGeneratorForBrainfuck.GetVariable(Variables, identifier.Content, out Variable _variable) &&
                    _variable.Name == variable.Name
                    )
                {
                    usages++;
                    if (usages > 1)
                    { return false; }
                }

                if (!SafeToDiscardVariable(_statement, variable))
                { return false; }
            }
            return usages <= 1;
        }

        #region GetValueSize
        int GetValueSize(StatementWithValue statement)
        {
            CompiledType statementType = FindStatementType(statement);

            if (statementType == Type.Void)
            { throw new CompilerException($"Statement \"{statement}\" (with type \"{statementType}\") does not have a size", statement, CurrentFile); }

            return statementType.SizeOnStack;
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
                size = variable.Type.StackArrayOf.SizeOnStack;
                address = variable.Address;

                if (size != 1)
                { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", index, CurrentFile); }

                if (TryCompute(index.Expression, RuntimeType.SInt32, out DataItem indexValue))
                {
                    address = variable.Address + (indexValue.ValueSInt32 * 2 * variable.Type.StackArrayOf.SizeOnStack);
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
                size = fieldType.SizeOnStack;
                return true;
            }

            address = default;
            size = default;
            return false;
        }

        bool TryGetAddress(Pointer pointer, out int address, out int size)
        {
            if (!TryCompute(pointer.PrevStatement, null, out DataItem addressToSet))
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

        #region TryGetRuntimeAddress

        bool TryGetRuntimeAddress(Statement statement, out int pointerAddress, out int size)
        {
            if (statement is Identifier identifier)
            { return TryGetRuntimeAddress(identifier, out pointerAddress, out size); }

            if (statement is Field field)
            { return TryGetRuntimeAddress(field, out pointerAddress, out size); }

            if (statement is ConstructorCall)
            { pointerAddress = default; size = default; return false; }

            if (statement is IndexCall)
            { pointerAddress = default; size = default; return false; }

            throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile);
        }

        bool TryGetRuntimeAddress(Field field, out int pointerAddress, out int size)
        {
            CompiledType type = FindStatementType(field.PrevStatement);

            if (!type.IsClass)
            {
                pointerAddress = default;
                size = default;
                return false;
            }

            if (!TryGetRuntimeAddress(field.PrevStatement, out pointerAddress, out _))
            {
                pointerAddress = default;
                size = default;
                return false;
            }

            CompiledType fieldType = FindStatementType(field);
            size = fieldType.SizeOnStack;

            if (!type.TryGetFieldOffsets(out IReadOnlyDictionary<string, int>? fieldOffsets))
            { throw new InternalException(); }

            int fieldOffset = fieldOffsets[field.FieldName.Content];

            Code.AddValue(pointerAddress, fieldOffset);

            return true;
        }

        bool TryGetRuntimeAddress(Identifier identifier, out int pointerAddress, out int size)
        {
            if (!CodeGeneratorForBrainfuck.GetVariable(Variables, identifier.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{identifier}\" not found", identifier, CurrentFile); }

            if (!variable.Type.IsClass)
            {
                pointerAddress = default;
                size = default;
                return false;
            }

            pointerAddress = Stack.PushVirtual(1);
            size = variable.Type.Size;

            Code.CopyValue(variable.Address, pointerAddress);

            return true;
        }

        #endregion

        BrainfuckGeneratorResult GenerateCode(CompilerResult compilerResult)
        {
            PrintCallback?.Invoke("  Precompiling ...", LogType.Debug);

            CurrentFile = compilerResult.File?.FullName;

            int constantCount = CompileConstants(compilerResult.TopLevelStatements);

            Variable returnVariable = new(ReturnVariableName, Stack.PushVirtual(1), null, false, false, new CompiledType(Type.Integer), 1);
            Variables.Add(returnVariable);

            if (GeneratorSettings.ClearGlobalVariablesBeforeExit)
            { VariableCleanupStack.Push(PrecompileVariables(compilerResult.TopLevelStatements)); }
            else
            { PrecompileVariables(compilerResult.TopLevelStatements); }

            Heap.Init();

            using (Code.Block($"Begin \"return\" block (depth: {ReturnTagStack.Count} (now its one more))"))
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

            {
                FinishReturnStatements();
                if (ReturnTagStack.Pop() != Stack.LastAddress)
                { throw new InternalException(); }
                Stack.Pop();

                if (ReturnCount.Count > 0 ||
                    ReturnTagStack.Count > 0 ||
                    BreakCount.Count > 0 ||
                    BreakTagStack.Count > 0)
                { throw new InternalException(); }
            }

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