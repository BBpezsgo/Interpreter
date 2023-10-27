using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LanguageCore.Brainfuck.Compiler
{
    using System.Diagnostics.CodeAnalysis;
    using BBCode.Compiler;
    using LanguageCore.Parser;
    using LanguageCore.Parser.Statement;
    using LanguageCore.Runtime;
    using LanguageCore.Tokenizing;
    using Literal = LanguageCore.Parser.Statement.Literal;

    readonly struct CleanupItem
    {
        /// <summary>
        /// The actual data size on the stack
        /// </summary>
        internal readonly int Size;
        /// <summary>
        /// The element count
        /// </summary>
        internal readonly int Count;

        public CleanupItem(int size, int count)
        {
            Size = size;
            Count = count;
        }
    }

    public partial class CodeGenerator : CodeGeneratorBase
    {
        readonly struct DebugInfoBlock : IDisposable
        {
            readonly int InstructionStart;
            readonly CompiledCode Code;
            readonly DebugInformation DebugInfo;
            readonly Position Position;

            public DebugInfoBlock(CompiledCode code, DebugInformation debugInfo, Position position)
            {
                InstructionStart = code.GetFinalCode().Length;
                Code = code;
                DebugInfo = debugInfo;
                Position = position;
            }

            public DebugInfoBlock(CompiledCode code, DebugInformation debugInfo, IThingWithPosition position)
            {
                InstructionStart = code.GetFinalCode().Length;
                Code = code;
                DebugInfo = debugInfo;
                Position = position.GetPosition();
            }

            public void Dispose()
            {
                int end = Code.GetFinalCode().Length;
                if (InstructionStart == end) return;
                DebugInfo.SourceCodeLocations.Add(new SourceCodeLocation()
                {
                    Instructions = (InstructionStart, end),
                    SourcePosition = Position,
                });
            }
        }

        static readonly string[] IllegalIdentifiers = Array.Empty<string>();

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

        readonly Settings GeneratorSettings;

        string? VariableCanBeDiscarded = null;

        readonly DebugInformation DebugInfo;

        #endregion

        public CodeGenerator(Compiler.Result compilerResult, Settings settings) : base(compilerResult)
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
        }

        public enum ValueType
        {
            Byte,
            Char,
            String,
        }

        public struct Result
        {
            public string Code;
            public int Optimizations;
            public Token[] Tokens;

            public Warning[] Warnings;
            public Error[] Errors;
            public DebugInformation DebugInfo;
        }

        public struct Settings
        {
            public bool ClearGlobalVariablesBeforeExit;
            public int StackStart;
            public int StackSize;
            public int HeapStart;
            public int HeapSize;

            public static Settings Default => new()
            {
                ClearGlobalVariablesBeforeExit = true,
                StackStart = 0,
                StackSize = 32,
                HeapStart = 32,
                HeapSize = 8,
            };
        }

        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        readonly struct ConstantVariable : ISearchable<string>
        {
            internal readonly string Name;
            internal readonly DataItem Value;

            public ConstantVariable(string name, DataItem value)
            {
                Name = name;
                Value = value;
            }

            public bool IsThis(string query) => query == Name;

            string GetDebuggerDisplay()
                => $"{Name}: {Value}";
        }

        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        struct Variable : ISearchable<string>
        {
            public readonly string Name;
            public readonly int Address;
            public readonly FunctionThingDefinition? Scope;
            public readonly bool HaveToClean;
            public readonly CompiledType Type;
            public readonly int Size;
            public bool IsDiscarded;
            public bool IsInitialValueSet;

            public readonly bool IsInitialized => Type.SizeOnStack > 0;

            public Variable(string name, int address, FunctionThingDefinition? scope, bool haveToClean, CompiledType type, int size)
            {
                Name = name;
                Address = address;
                Scope = scope;
                HaveToClean = haveToClean;
                Type = type;
                IsDiscarded = false;
                Size = size;
                IsInitialValueSet = false;
            }

            public readonly bool IsThis(string query)
                => query == Name;

            readonly string GetDebuggerDisplay()
                => $"{Type} {Name} ({Type.SizeOnStack} bytes at {Address})";
        }

        DebugInfoBlock DebugBlock(IThingWithPosition position) => new(Code, DebugInfo, position);

        protected override bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out CompiledType? type)
        {
            if (Variables.TryFind(symbolName, out Variable variable))
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
                    Variables.Pop();
                    Stack.Pop();
                }
            }
        }

        bool SafeToDiscardVariable(Statement statement, Variable variable)
        {
            int usages = 0;

            var statements = statement.GetStatements();
            foreach (var _statement in statements)
            {
                if (_statement == null) continue;

                if (_statement is Identifier identifier &&
                    Variables.TryFind(identifier.Content, out var _variable) &&
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
            if (statement is Literal literal)
            { return GetValueSize(literal); }

            if (statement is Identifier variable)
            { return GetValueSize(variable); }

            if (statement is OperatorCall expression)
            { return GetValueSize(expression); }

            if (statement is AddressGetter addressGetter)
            { return GetValueSize(addressGetter); }

            if (statement is Pointer pointer)
            { return GetValueSize(pointer); }

            if (statement is FunctionCall functionCall)
            { return GetValueSize(functionCall); }

            if (statement is TypeCast typeCast)
            { return GetValueSize(typeCast); }

            if (statement is NewInstance newInstance)
            { return GetValueSize(newInstance); }

            if (statement is Field field)
            { return GetValueSize(field); }

            if (statement is ConstructorCall constructorCall)
            { return GetValueSize(constructorCall); }

            if (statement is IndexCall indexCall)
            { return GetValueSize(indexCall); }

            if (statement is AnyCall anyCall)
            { return GetValueSize(anyCall); }

            throw new CompilerException($"Statement {statement.GetType().Name} does not have a size", statement, CurrentFile);
        }
        int GetValueSize(AnyCall anyCall)
        {
            if (anyCall.ToFunctionCall(out var functionCall))
            { return GetValueSize(functionCall); }

            CompiledType prevType = FindStatementType(anyCall.PrevStatement);

            if (!prevType.IsFunction)
            { throw new CompilerException($"This isn't a function", anyCall.PrevStatement, CurrentFile); }

            if (!prevType.Function.ReturnSomething)
            { throw new CompilerException($"Return value \"void\" does not have a size", anyCall.PrevStatement, CurrentFile); }

            return prevType.Function.ReturnType.SizeOnStack;
        }
        int GetValueSize(IndexCall indexCall)
        {
            CompiledType arrayType = FindStatementType(indexCall.PrevStatement);

            if (arrayType.IsStackArray)
            { return arrayType.StackArrayOf.SizeOnStack; }

            if (!arrayType.IsClass)
            { throw new CompilerException($"Index getter for type \"{arrayType.Name}\" not found", indexCall, CurrentFile); }

            if (!GetIndexGetter(arrayType, out CompiledFunction? indexer))
            {
                if (!GetIndexGetterTemplate(arrayType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index getter for class \"{arrayType.Class.Name}\" not found", indexCall, CurrentFile); }

                indexerTemplate = AddCompilable(indexerTemplate);
                indexer = indexerTemplate.Function;
            }

            return indexer.Type.SizeOnStack;
        }
        int GetValueSize(Field field)
        {
            CompiledType type = FindStatementType(field);
            return type.SizeOnStack;
        }
        int GetValueSize(NewInstance newInstance)
        {
            if (GetStruct(newInstance, out var @struct))
            {
                return @struct.Size;
            }

            if (GetClass(newInstance, out _))
            {
                return 1;
            }

            throw new CompilerException($"Type \"{newInstance.TypeName}\" not found", newInstance.TypeName, CurrentFile);
        }
        static int GetValueSize(Literal statement) => statement.Type switch
        {
            LiteralType.STRING => 1, // throw new NotSupportedException($"String literals not supported by brainfuck"),
            LiteralType.INT => 1,
            LiteralType.CHAR => 1,
            LiteralType.FLOAT => 1,
            LiteralType.BOOLEAN => 1,
            _ => throw new ImpossibleException($"Unknown literal type {statement.Type}"),
        };
        int GetValueSize(Identifier statement)
        {
            if (Variables.TryFind(statement.Content, out Variable variable))
            {
                if (!variable.IsInitialized)
                { throw new CompilerException($"Variable \"{variable.Name}\" not initialized", statement, CurrentFile); }

                return variable.Size;
            }
            else if (GetConstant(statement.Content, out _))
            {
                return 1;
            }
            else if (TryGetFunction(statement.Name, out _))
            {
                throw new NotSupportedException($"Function pointers not supported by the brainfuck compiler", statement, CurrentFile);
            }
            else
            { throw new CompilerException($"Variable or constant \"{statement}\" not found", statement, CurrentFile); }
        }
        int GetValueSize(ConstructorCall constructorCall)
        {
            if (!GetClass(constructorCall, out CompiledClass? @class))
            { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

            if (!GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), FunctionNames.Constructor, out CompiledGeneralFunction? constructor))
            {
                if (!GetConstructorTemplate(@class, constructorCall, out var compilableGeneralFunction))
                {
                    throw new CompilerException($"Function {constructorCall.ReadableID(FindStatementType)} not found", constructorCall.Keyword, CurrentFile);
                }
                else
                {
                    compilableGeneralFunction = AddCompilable(compilableGeneralFunction);
                    constructor = compilableGeneralFunction.Function;
                }
            }

            if (!constructor.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The \"{constructorCall.TypeName}\" constructor cannot be called due to its protection level", constructorCall.Keyword, CurrentFile));
            }

            return 1;
        }
        int GetValueSize(FunctionCall functionCall)
        {
            if (GetFunction(functionCall, out CompiledFunction? function))
            {
                if (!function.ReturnSomething)
                { return 0; }

                return function.Type.SizeOnStack;
            }

            if (GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
            {
                if (!compilableFunction.Function.ReturnSomething)
                { return 0; }

                return compilableFunction.Function.Type.SizeOnStack;
            }

            throw new CompilerException($"Function \"{functionCall.ReadableID(FindStatementType)}\" not found", functionCall, CurrentFile);
        }
        int GetValueSize(OperatorCall statement) => statement.Operator.Content switch
        {
            "==" => 1,
            "+" => 1,
            "-" => 1,
            "*" => 1,
            "/" => 1,
            "^" => 1,
            "%" => 1,
            "<" => 1,
            ">" => 1,
            "<=" => 1,
            ">=" => 1,
            "&" => 1,
            "|" => 1,
            "&&" => 1,
            "||" => 1,
            "!=" => 1,
            "<<" => 1,
            ">>" => 1,
            _ => throw new CompilerException($"Unknown operator \"{statement.Operator}\"", statement.Operator, CurrentFile),
        };
        static int GetValueSize(AddressGetter _) => 1;
        static int GetValueSize(Pointer _) => 1;
        int GetValueSize(TypeCast typeCast) => GetValueSize(typeCast.PrevStatement);
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

            if (!Variables.TryFind(arrayIdentifier.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{arrayIdentifier}\" not found", arrayIdentifier, CurrentFile); }

            if (variable.Type.IsStackArray)
            {
                size = variable.Type.StackArrayOf.SizeOnStack;
                address = variable.Address;

                if (size != 1)
                { throw new NotSupportedException($"In stack array only elements of size 1 are supported by brainfuck", index, CurrentFile); }

                if (TryCompute(index.Expression, RuntimeType.INT, out DataItem indexValue))
                {
                    address = variable.Address + (indexValue.ValueInt * 2 * variable.Type.StackArrayOf.SizeOnStack);
                    return true;
                }

                return false;
            }

            throw new CompilerException($"Variable is not an array", arrayIdentifier, CurrentFile);
        }

        bool TryGetAddress(Field field, out int address, out int size)
        {
            var type = FindStatementType(field.PrevStatement);

            if (type.IsStruct)
            {
                if (!TryGetAddress(field.PrevStatement, out int prevAddress, out _))
                {
                    address = default;
                    size = default;
                    return false;
                }

                var fieldType = FindStatementType(field);

                var @struct = type.Struct;

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
            if (!TryCompute(pointer.PrevStatement, null, out var addressToSet))
            { throw new NotSupportedException($"Runtime pointer address in not supported", pointer.PrevStatement, CurrentFile); }

            if (!DataItem.TryShrinkToByte(ref addressToSet))
            { throw new CompilerException($"Address value must be a byte (not {addressToSet.Type})", pointer.PrevStatement, CurrentFile); }

            address = addressToSet.ValueByte;
            size = 1;

            return true;
        }

        bool TryGetAddress(Identifier identifier, out int address, out int size)
        {
            if (!Variables.TryFind(identifier.Content, out Variable variable))
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
            if (!Variables.TryFind(identifier.Content, out Variable variable))
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

        Result GenerateCode(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            PrintCallback? printCallback = null)
        {
            foreach (CompiledFunction? function in CompiledFunctions)
            {
                if (IllegalIdentifiers.Contains(function.Identifier.Content))
                { throw new CompilerException($"Illegal function name \"{function.Identifier}\"", function.Identifier, CurrentFile); }
            }

            int constantCount = CompileConstants(compilerResult.TopLevelStatements);

            if (GeneratorSettings.ClearGlobalVariablesBeforeExit)
            { VariableCleanupStack.Push(PrecompileVariables(compilerResult.TopLevelStatements)); }
            else
            { PrecompileVariables(compilerResult.TopLevelStatements); }

            // Heap.Init();

            using (Code.Block($"Begin \"return\" block (depth: {ReturnTagStack.Count} (now its one more))"))
            {
                ReturnCount.Push(0);
                ReturnTagStack.Push(Stack.Push(1));
            }

            foreach (Statement statement in compilerResult.TopLevelStatements)
            { GenerateCodeForStatement(statement); }

            CompiledFunction? codeEntry = GetCodeEntry();

            if (codeEntry != null)
            { GenerateCodeForMacro(codeEntry, Array.Empty<StatementWithValue>(), null, codeEntry.Identifier); }

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

            return new Result()
            {
                Code = Code.ToString(),
                Optimizations = Optimizations,
                DebugInfo = DebugInfo,
                Tokens = compilerResult.Tokens,

                Warnings = this.Warnings.ToArray(),
                Errors = this.Errors.ToArray(),
            };
        }

        public static Result Generate(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            Settings generatorSettings,
            PrintCallback? printCallback = null)
        {
            CodeGenerator codeGenerator = new(compilerResult, generatorSettings);
            return codeGenerator.GenerateCode(
                compilerResult,
                settings,
                printCallback
                );
        }
    }
}