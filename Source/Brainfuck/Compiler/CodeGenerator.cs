using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

#nullable enable

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

    public class CodeGenerator : CodeGeneratorBase
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

        static readonly string[] IllegalIdentifiers = new string[]
        {

        };

        #region Fields

        CompiledCode Code;

        readonly Stack<Variable> Variables;
        readonly List<ConstantVariable> Constants;

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
            this.Constants = new List<ConstantVariable>();
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
        DebugInfoBlock DebugBlock(Position position) => new(Code, DebugInfo, position);

        protected override bool GetLocalSymbolType(string symbolName, [MaybeNullWhen(false)] out CompiledType? type)
        {
            if (Variables.TryFind(symbolName, out var variable))
            {
                type = variable.Type;
                return true;
            }

            if (Constants.TryFind(symbolName, out var constant))
            {
                type = new CompiledType(constant.Value.Type);
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

        #region Precompile
        void Precompile(Statement[] statements)
        {
            foreach (Statement statement in statements)
            { Precompile(statement); }
        }
        void Precompile(Statement statement)
        {
            if (statement is KeywordCall instruction)
            { Precompile(instruction); }
        }
        void Precompile(KeywordCall instruction)
        {
            switch (instruction.Identifier.Content)
            {
                case "const":
                    {
                        if (instruction.Parameters.Length != 2)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"const\" (required 2, passed {instruction.Parameters.Length})", instruction, CurrentFile); }

                        if (instruction.Parameters[0] is not Identifier constIdentifier)
                        { throw new CompilerException($"Wrong kind of parameter passed to \"const\" at index {0} (required identifier)", instruction.Parameters[0], CurrentFile); }

                        if (IllegalIdentifiers.Contains(constIdentifier.Content))
                        { throw new CompilerException($"Illegal constant name \"{constIdentifier}\"", constIdentifier, CurrentFile); }

                        if (instruction.Parameters[1] is not StatementWithValue constValue)
                        { throw new CompilerException($"Wrong kind of parameter passed to \"const\" at index {1} (required a value)", instruction.Parameters[1], CurrentFile); }

                        if (Constants.TryFind(constIdentifier.Content, out _))
                        { throw new CompilerException($"Constant \"{constIdentifier.Content}\" already defined", instruction.Parameters[0], CurrentFile); }

                        if (!TryCompute(constValue, null, out DataItem value))
                        { throw new CompilerException($"Constant must have a constant value", constValue, CurrentFile); }

                        Constants.Add(new ConstantVariable(constIdentifier.Content, value));

                        break;
                    }
                default:
                    break;
            }
        }
        void Precompile(CompiledFunction function)
        {
            if (IllegalIdentifiers.Contains(function.Identifier.Content))
            { throw new CompilerException($"Illegal function name \"{function.Identifier}\"", function.Identifier, CurrentFile); }

            Precompile(function.Statements);
        }
        #endregion

        #region PrecompileVariables
        int PrecompileVariables(Block block)
        { return PrecompileVariables(block.Statements.ToArray()); }
        int PrecompileVariables(Statement[] statements)
        {
            int result = 0;
            foreach (Statement statement in statements)
            { result += PrecompileVariables(statement); }
            return result;
        }
        int PrecompileVariables(Statement statement)
        {
            if (statement is not VariableDeclaration instruction)
            { return 0; }

            return PrecompileVariable(instruction);
        }
        int PrecompileVariable(VariableDeclaration variableDeclaration)
        {
            if (IllegalIdentifiers.Contains(variableDeclaration.VariableName.Content))
            { throw new CompilerException($"Illegal variable name \"{variableDeclaration.VariableName}\"", variableDeclaration.VariableName, CurrentFile); }

            if (Variables.TryFind(variableDeclaration.VariableName.Content, out _))
            { throw new CompilerException($"Variable \"{variableDeclaration.VariableName.Content}\" already defined", variableDeclaration.VariableName, CurrentFile); }

            if (Constants.TryFind(variableDeclaration.VariableName.Content, out _))
            { throw new CompilerException($"Variable \"{variableDeclaration.VariableName.Content}\" already defined", variableDeclaration.VariableName, CurrentFile); }

            CompiledType type;

            StatementWithValue? initialValue = variableDeclaration.InitialValue;

            if (variableDeclaration.Type == "var")
            {
                if (initialValue == null)
                { throw new CompilerException($"Variable with implicit type must have an initial value"); }

                type = FindStatementType(initialValue);
            }
            else
            {
                type = new CompiledType(variableDeclaration.Type, FindType, TryCompute);
            }

            return PrecompileVariable(Variables, variableDeclaration.VariableName.Content, type, initialValue);
        }
        int PrecompileVariable(Stack<Variable> variables, string name, CompiledType type, StatementWithValue? initialValue)
        {
            if (variables.TryFind(name, out _))
            { return 0; }

            FunctionThingDefinition? scope = (CurrentMacro.Count == 0) ? null : CurrentMacro[^1];

            if (initialValue != null)
            {
                CompiledType initialValueType = FindStatementType(initialValue, type);

                if (type.IsStackArray)
                {
                    if (type.StackArrayOf == Type.CHAR)
                    {
                        if (initialValue is not Literal literal)
                        { throw new InternalException(); }
                        if (literal.Type != LiteralType.STRING)
                        { throw new InternalException(); }
                        if (literal.Value.Length != type.StackArraySize)
                        { throw new InternalException(); }

                        int arraySize = type.StackArraySize;

                        int size = Snippets.ARRAY_SIZE(arraySize);

                        int address = Stack.PushVirtual(size);
                        variables.Push(new Variable(name, address, scope, true, type, size)
                        {
                            IsInitialValueSet = true
                        });

                        for (int i = 0; i < literal.Value.Length; i++)
                        { Code.ARRAY_SET_CONST(address, i, new DataItem(literal.Value[i])); }
                    }
                    else
                    { throw new NotImplementedException(); }
                }
                else
                {
                    if (!initialValueType.Equals(type))
                    { throw new CompilerException($"Variable initial value type ({initialValueType}) and variable type ({type}) mismatch", initialValue, CurrentFile); }

                    int address = Stack.PushVirtual(type.SizeOnStack);
                    variables.Push(new Variable(name, address, scope, true, type, type.SizeOnStack));
                }
            }
            else
            {
                if (type.IsStackArray)
                {
                    int arraySize = type.StackArraySize;

                    int size = Snippets.ARRAY_SIZE(arraySize);

                    int address = Stack.PushVirtual(size);
                    variables.Push(new Variable(name, address, scope, true, type, size));
                }
                else
                {
                    int address = Stack.PushVirtual(type.SizeOnStack);
                    variables.Push(new Variable(name, address, scope, true, type, type.SizeOnStack));
                }
            }

            return 1;
        }
        #endregion

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

            throw new CompilerException($"Statement {statement.GetType().Name} does not have a size", statement, CurrentFile);
        }
        int GetValueSize(IndexCall indexCall)
        {
            CompiledType arrayType = FindStatementType(indexCall.PrevStatement);

            if (arrayType.IsStackArray)
            { return arrayType.StackArrayOf.SizeOnStack; }

            if (!arrayType.IsClass)
            { throw new CompilerException($"Index getter for type \"{arrayType.Name}\" not found", indexCall, CurrentFile); }

            if (!GetIndexGetter(arrayType, out CompiledFunction indexer))
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
            return type.Size;
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
            LiteralType.STRING => throw new NotSupportedException($"String literals not supported by brainfuck"),
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
            else if (Constants.TryFind(statement.Content, out _))
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

            if (!GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), FunctionNames.Constructor, out CompiledGeneralFunction constructor))
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
            if (functionCall.Identifier == "Alloc" &&
                functionCall.IsMethodCall == false &&
                CompiledType.Equals(FindStatementTypes(functionCall.Parameters), new CompiledType(RuntimeType.INT)))
            { return 1; }

            if (functionCall.Identifier == "AllocFrom" &&
                functionCall.IsMethodCall == false &&
                CompiledType.Equals(FindStatementTypes(functionCall.Parameters), new CompiledType(RuntimeType.INT)))
            { return 1; }

            if (GetFunction(functionCall, out CompiledFunction function))
            {
                if (!function.ReturnSomething)
                { return 0; }

                return function.Type.Size;
            }

            if (GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
            {
                if (!compilableFunction.Function.ReturnSomething)
                { return 0; }

                return compilableFunction.Function.Type.Size;
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

        bool TryGetAddress(Statement statement, out int address, out int size)
        {
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
                { throw new NotSupportedException($"Only elements of size 1 are supported by brainfuck", index, CurrentFile); }

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
                size = fieldType.Size;
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

            int fieldOffset = type.Class.FieldOffsets[field.FieldName.Content];

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
            size = variable.Type.Class.Size;

            Code.CopyValue(variable.Address, pointerAddress);

            return true;
        }

        #endregion

        #region CompileSetter

        void CompileSetter(Statement statement, StatementWithValue value)
        {
            if (statement is Identifier variableIdentifier)
            {
                CompileSetter(variableIdentifier, value);

                return;
            }

            if (statement is Pointer pointerToSet)
            {
                CompileSetter(pointerToSet, value);

                return;
            }

            if (statement is IndexCall index)
            {
                CompileSetter(index, value);

                return;
            }

            if (statement is Field field)
            {
                CompileSetter(field, value);

                return;
            }

            throw new CompilerException($"Setter for statement {statement.GetType().Name} not implemented", statement, CurrentFile);
        }

        void CompileSetter(Identifier statement, StatementWithValue value)
        {
            if (Constants.TryFind(statement.Content, out _))
            { throw new CompilerException($"This is a constant so you can not modify it's value", statement, CurrentFile); }

            if (!Variables.TryFind(statement.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{statement}\" not found", statement, CurrentFile); }

            CompileSetter(variable, value);
        }

        void CompileSetter(Field field, StatementWithValue value)
        {
            if (TryGetRuntimeAddress(field, out int pointerAddress, out int size))
            {
                if (size != GetValueSize(value))
                { throw new CompilerException($"Field and value size mismatch", value, CurrentFile); }

                int valueAddress = Stack.NextAddress;
                Compile(value);

                int _pointerAddress = Stack.PushVirtual(1);
                Code.CopyValue(pointerAddress, _pointerAddress);

                for (int offset = 0; offset < size; offset++)
                {
                    Code.CopyValue(pointerAddress, _pointerAddress);
                    Code.AddValue(_pointerAddress, offset);

                    Heap.Set(_pointerAddress, valueAddress + offset);
                }

                Stack.Pop(); // _pointerAddress
                Stack.Pop(); // valueAddress
                Stack.Pop(); // pointerAddress

                return;
            }

            if (!TryGetAddress(field, out int address, out size))
            { throw new CompilerException($"Failed to get field address", field, CurrentFile); }

            if (size != GetValueSize(value))
            { throw new CompilerException($"Field and value size mismatch", value, CurrentFile); }

            CompileSetter(address, value);
        }

        void CompileSetter(Variable variable, StatementWithValue value)
        {
            if (value is Identifier _identifier &&
                Variables.TryFind(_identifier.Content, out Variable valueVariable))
            {
                if (variable.Address == valueVariable.Address)
                {
                    Optimizations++;
                    return;
                }

                if (valueVariable.IsDiscarded)
                { throw new CompilerException($"Variable \"{valueVariable.Name}\" is discarded", _identifier, CurrentFile); }

                if (variable.Size != valueVariable.Size)
                { throw new CompilerException($"Variable and value size mismatch ({variable.Size} != {valueVariable.Size})", value, CurrentFile); }

                UndiscardVariable(Variables, variable.Name);

                int tempAddress = Stack.NextAddress;

                int size = valueVariable.Size;
                for (int offset = 0; offset < size; offset++)
                {
                    int offsettedSource = valueVariable.Address + offset;
                    int offsettedTarget = variable.Address + offset;

                    Code.CopyValueWithTemp(offsettedSource, tempAddress, offsettedTarget);
                }

                Optimizations++;

                return;
            }

            if (SafeToDiscardVariable(value, variable))
            { VariableCanBeDiscarded = variable.Name; }

            using (Code.Block($"Set variable \"{variable.Name}\" (at {variable.Address}) to {value}"))
            {
                if (TryCompute(value, variable.Type.IsBuiltin ? variable.Type.RuntimeType : null, out var constantValue))
                {
                    if (variable.Type != constantValue.Type)
                    { throw new CompilerException($"Cannot set {constantValue.Type} to variable of type {variable.Type}", value, CurrentFile); }

                    Code.SetValue(variable.Address, constantValue);

                    /*
                    if (constantValue.Type == RuntimeType.String)
                    {
                        string v = (string)constantValue;
                        for (int i = 0; i < v.Length; i++)
                        {
                            Code.SetValue(variable.Address + i, v[i]);
                        }
                    }
                    */

                    Optimizations++;

                    VariableCanBeDiscarded = null;
                    return;
                }

                int valueSize = GetValueSize(value);

                if (variable.Type.IsStackArray)
                {
                    if (variable.Type.StackArrayOf == Type.CHAR)
                    {
                        if (value is not Literal literal)
                        { throw new InternalException(); }
                        if (literal.Type != LiteralType.STRING)
                        { throw new InternalException(); }
                        if (literal.Value.Length != variable.Type.StackArraySize)
                        { throw new InternalException(); }

                        int arraySize = variable.Type.StackArraySize;

                        int size = Snippets.ARRAY_SIZE(arraySize);

                        int tempAddress2 = Stack.Push(0);
                        int tempAddress3 = Stack.Push(0);

                        for (int i = 0; i < literal.Value.Length; i++)
                        {
                            Code.SetValue(tempAddress2, i);
                            Code.SetValue(tempAddress3, literal.Value[i]);
                            Code.ARRAY_SET(variable.Address, tempAddress2, tempAddress3, tempAddress3 + 1);
                        }

                        Stack.Pop();
                        Stack.Pop();

                        UndiscardVariable(Variables, variable.Name);

                        VariableCanBeDiscarded = null;

                        return;
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    if (valueSize != variable.Size)
                    { throw new CompilerException($"Variable and value size mismatch ({variable.Size} != {valueSize})", value, CurrentFile); }
                }

                using (Code.Block($"Compute value"))
                {
                    Compile(value);
                }

                using (Code.Block($"Store computed value (from {Stack.LastAddress}) to {variable.Address}"))
                { Stack.PopAndStore(variable.Address); }

                UndiscardVariable(Variables, variable.Name);

                VariableCanBeDiscarded = null;
            }
        }

        void CompileSetter(Pointer statement, StatementWithValue value)
        {
            int pointerAddress = Stack.NextAddress;
            Compile(statement.PrevStatement);

            {
                int checkResultAddress = Stack.PushVirtual(1);

                int maxSizeAddress = Stack.Push(GeneratorSettings.HeapSize);
                int pointerAddressCopy = Stack.PushVirtual(1);
                Code.CopyValue(pointerAddress, pointerAddressCopy);

                Code.LOGIC_MT(pointerAddressCopy, maxSizeAddress, checkResultAddress, checkResultAddress + 1, checkResultAddress + 2);
                Stack.PopVirtual();
                Stack.PopVirtual();

                Code.JumpStart(checkResultAddress);

                Code.OUT_STRING(checkResultAddress, "\nOut of memory range\n");

                Code.ClearValue(checkResultAddress);
                Code.JumpEnd(checkResultAddress);

                Stack.Pop();
            }

            if (GetValueSize(value) != 1)
            { throw new CompilerException($"size 1 bruh allowed on heap thingy", value, CurrentFile); }

            int valueAddress = Stack.NextAddress;
            Compile(value);

            Heap.Set(pointerAddress, valueAddress);

            Stack.PopVirtual();
            Stack.PopVirtual();

            /*
            if (!TryCompute(statement.Statement, out var addressToSet))
            { throw new NotSupportedException($"Runtime pointer address in not supported", statement.Statement); }

            if (addressToSet.Type != ValueType.Byte)
            { throw new CompilerException($"Address value must be a byte (not {addressToSet.Type})", statement.Statement); }

            CompileSetter((byte)addressToSet, value);
            */
        }

        void CompileSetter(int address, StatementWithValue value)
        {
            using (Code.Block($"Set value {value} to address {address}"))
            {
                if (TryCompute(value, null, out var constantValue))
                {
                    // if (constantValue.Size != 1)
                    // { throw new CompilerException($"Value size can be only 1", value, CurrentFile); }

                    Code.SetValue(address, constantValue.Byte ?? (byte)0);

                    Optimizations++;

                    return;
                }

                int stackSize = Stack.Size;

                using (Code.Block($"Compute value"))
                {
                    Compile(value);
                }

                int variableSize = Stack.Size - stackSize;

                if (variableSize != 1)
                { throw new CompilerException($"Value size can be only 1 (not {variableSize})", value, CurrentFile); }

                using (Code.Block($"Store computed value (from {Stack.LastAddress}) to {address}"))
                { Stack.PopAndStore(address); }
            }
        }

        void CompileSetter(IndexCall statement, StatementWithValue value)
        {
            if (statement.PrevStatement is not Identifier _variableIdentifier)
            { throw new NotSupportedException($"Only variable indexers supported for now", statement.PrevStatement, CurrentFile); }

            if (!Variables.TryFind(_variableIdentifier.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{_variableIdentifier}\" not found", _variableIdentifier, CurrentFile); }

            if (variable.IsDiscarded)
            { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", _variableIdentifier, CurrentFile); }

            using (Code.Block($"Set array (variable {variable.Name}) index ({statement.Expression}) (at {variable.Address}) to {value}"))
            {
                if (!variable.Type.IsStackArray)
                { throw new NotImplementedException(); }

                CompiledType elementType = variable.Type.StackArrayOf;
                CompiledType valueType = FindStatementType(value);

                if (elementType != valueType)
                { throw new CompilerException("Bruh", value, CurrentFile); }

                int elementSize = elementType.Size;

                if (elementSize != 1)
                { throw new CompilerException($"Array element size must be 1 :(", value, CurrentFile); }

                int indexAddress = Stack.NextAddress;
                using (Code.Block($"Compute index"))
                { Compile(statement.Expression); }

                int valueAddress = Stack.NextAddress;
                using (Code.Block($"Compute value"))
                { Compile(value); }

                int temp0 = Stack.PushVirtual(1);

                Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, temp0);

                Stack.Pop();
                Stack.Pop();
                Stack.Pop();
            }
        }

        #endregion

        #region Compile
        void Compile(Statement statement)
        {
            int start = Code.GetFinalCode().Length;

            if (statement is KeywordCall instructionStatement)
            { Compile(instructionStatement); }
            else if (statement is FunctionCall functionCall)
            { Compile(functionCall); }
            else if (statement is IfContainer @if)
            { Compile(@if.ToLinks()); }
            else if (statement is WhileLoop @while)
            { Compile(@while); }
            else if (statement is ForLoop @for)
            { Compile(@for); }
            else if (statement is Literal literal)
            { Compile(literal); }
            else if (statement is Identifier variable)
            { Compile(variable); }
            else if (statement is OperatorCall expression)
            { Compile(expression); }
            else if (statement is AddressGetter addressGetter)
            { Compile(addressGetter); }
            else if (statement is Pointer pointer)
            { Compile(pointer); }
            else if (statement is Assignment assignment)
            { Compile(assignment); }
            else if (statement is ShortOperatorCall shortOperatorCall)
            { Compile(shortOperatorCall); }
            else if (statement is CompoundAssignment compoundAssignment)
            { Compile(compoundAssignment); }
            else if (statement is VariableDeclaration variableDeclaration)
            { Compile(variableDeclaration); }
            else if (statement is TypeCast typeCast)
            { Compile(typeCast); }
            else if (statement is NewInstance newInstance)
            { Compile(newInstance); }
            else if (statement is ConstructorCall constructorCall)
            { Compile(constructorCall); }
            else if (statement is Field field)
            { Compile(field); }
            else if (statement is IndexCall indexCall)
            { Compile(indexCall); }
            else
            { throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile); }

            if (statement is FunctionCall statementWithValue &&
                !statementWithValue.SaveValue &&
                GetFunction(statementWithValue, out CompiledFunction? _f) &&
                _f.ReturnSomething)
            {
                Stack.Pop();
            }

            Code.SetPointer(0);

            if (InMacro.Count > 0 && InMacro.Last) return;

            int end = Code.GetFinalCode().Length;
            DebugInfo.SourceCodeLocations.Add(new SourceCodeLocation()
            {
                Instructions = (start, end),
                SourcePosition = statement.GetPosition(),
            });
        }
        void Compile(IndexCall indexCall)
        {
            CompiledType arrayType = FindStatementType(indexCall.PrevStatement);

            if (arrayType.IsStackArray)
            {
                if (!TryGetAddress(indexCall.PrevStatement, out int arrayAddress, out _))
                { throw new CompilerException($"Failed to get array address", indexCall.PrevStatement, CurrentFile); }

                CompiledType elementType = arrayType.StackArrayOf;

                int elementSize = elementType.Size;

                if (elementSize != 1)
                { throw new CompilerException($"Array element size must be 1 :(", indexCall, CurrentFile); }

                int resultAddress = Stack.PushVirtual(elementSize);

                int indexAddress = Stack.NextAddress;
                using (Code.Block($"Compute index"))
                { Compile(indexCall.Expression); }

                Code.ARRAY_GET(arrayAddress, indexAddress, resultAddress);

                Stack.Pop();

                return;
            }

            if (!arrayType.IsClass)
            { throw new CompilerException($"Index getter for type \"{arrayType.Name}\" not found", indexCall, CurrentFile); }

            Compile(new FunctionCall(
                indexCall.PrevStatement,
                Token.CreateAnonymous(FunctionNames.IndexerGet),
                indexCall.BracketLeft,
                new StatementWithValue[]
                {
                    indexCall.Expression,
                },
                indexCall.BracketRight));
        }
        void Compile(LinkedIf @if, bool linked = false)
        {
            using (Code.Block($"If ({@if.Condition})"))
            {
                int conditionAddress = Stack.NextAddress;
                using (Code.Block("Compute condition"))
                { Compile(@if.Condition); }

                Code.CommentLine($"Condition result at {conditionAddress}");

                Code.CommentLine($"Pointer: {Code.Pointer}");

                using (this.DebugBlock(@if.Keyword))
                {
                    Code.JumpStart(conditionAddress);
                }

                using (Code.Block("The if statements"))
                {
                    Compile(@if.Block);
                }

                Code.CommentLine($"Pointer: {Code.Pointer}");

                if (@if.NextLink == null)
                {
                    using (this.DebugBlock(@if.Block.BracketEnd))
                    using (Code.Block("Cleanup condition"))
                    {
                        Code.ClearValue(conditionAddress);
                        Code.JumpEnd(conditionAddress);
                        Stack.PopVirtual();
                    }
                }
                else
                {
                    using (Code.Block("Else"))
                    {
                        using (this.DebugBlock(@if.Keyword))
                        {
                            using (Code.Block("Finish if statement"))
                            {
                                Code.MoveValue(conditionAddress, conditionAddress + 1);
                                Code.JumpEnd(conditionAddress);
                            }
                            Code.MoveValue(conditionAddress + 1, conditionAddress);
                        }

                        using (this.DebugBlock(@if.NextLink.Keyword))
                        {
                            using (Code.Block($"Invert condition (at {conditionAddress}) result (to {conditionAddress + 1})"))
                            { Code.LOGIC_NOT(conditionAddress, conditionAddress + 1); }

                            Code.CommentLine($"Pointer: {Code.Pointer}");

                            int elseFlagAddress = conditionAddress + 1;

                            Code.CommentLine($"ELSE flag is at {elseFlagAddress}");

                            using (Code.Block("Set ELSE flag"))
                            { Code.SetValue(elseFlagAddress, 1); }

                            using (Code.Block("If previous \"if\" condition is true"))
                            {
                                Code.JumpStart(conditionAddress);

                                using (Code.Block("Reset ELSE flag"))
                                { Code.ClearValue(elseFlagAddress); }

                                using (Code.Block("Reset condition"))
                                { Code.ClearValue(conditionAddress); }

                                Code.JumpEnd(conditionAddress);
                            }

                            Code.MoveValue(elseFlagAddress, conditionAddress);

                            Code.CommentLine($"Pointer: {Code.Pointer}");
                        }

                        using (Code.Block($"If ELSE flag set (previous \"if\" condition is false)"))
                        {
                            Code.JumpStart(conditionAddress);

                            if (@if.NextLink is LinkedElse elseBlock)
                            {
                                using (Code.Block("Block (else)"))
                                { Compile(elseBlock.Block); }
                            }
                            else if (@if.NextLink is LinkedIf elseIf)
                            {
                                using (Code.Block("Block (else if)"))
                                { Compile(elseIf, true); }
                            }
                            else
                            { throw new ImpossibleException(); }

                            using (Code.Block($"Reset ELSE flag"))
                            { Code.ClearValue(conditionAddress); }

                            Code.JumpEnd(conditionAddress);
                            Stack.PopVirtual();
                        }

                        Code.CommentLine($"Pointer: {Code.Pointer}");
                    }
                }

                if (!linked)
                {
                    using (this.DebugBlock(@if.Block.BracketEnd))
                    {
                        ContinueReturnStatements();
                        ContinueBreakStatements();
                    }
                }

                Code.CommentLine($"Pointer: {Code.Pointer}");
            }
        }
        /*
        void Compile(IfContainer ifContainer)
        {
            Compile(ifContainer.Parts.ToArray(), 0, -1);
        }
        void Compile(BaseBranch[] branches, int index, int conditionAddress)
        {
            if (index >= branches.Length)
            { return; }

            BaseBranch branch = branches[index];

            if (branch is IfBranch @if)
            {
                using (Code.Block($"If ({@if.Condition})"))
                {
                    int _conditionAddress = Stack.NextAddress;
                    using (Code.Block("Compute condition"))
                    { Compile(@if.Condition); }

                    Code.CommentLine($"Condition result at {_conditionAddress}");

                    Code.JumpStart(_conditionAddress);

                    ReturnCount.Push(0);

                    using (Code.Block("The if statements"))
                    { Compile(@if.Block); }

                    FinishReturnStatements(ReturnCount.Pop());

                    if (index + 1 >= branches.Length)
                    {
                        using (Code.Block("Cleanup condition"))
                        {
                            Code.ClearValue(_conditionAddress);
                            Code.JumpEnd(_conditionAddress);
                            Stack.PopVirtual();
                        }
                    }
                    else
                    {
                        Compile(branches, index + 1, _conditionAddress);
                    }

                    Code.JumpStart(ReturnTagStack[^1]);
                    ReturnCount[^1]++;
                }
                return;
            }

            if (branch is ElseIfBranch @elseif)
            {
                FinishReturnStatements(ReturnCount.Pop());

                Code.LOGIC_NOT(conditionAddress, conditionAddress + 1);

                using (Code.Block("Else if"))
                {
                    using (Code.Block("Finish if statement"))
                    {
                        Code.MoveValue(conditionAddress, conditionAddress + 1);
                        Code.JumpEnd(conditionAddress);
                        Code.MoveValue(conditionAddress + 1, conditionAddress);
                    }

                    Stack.PopVirtual();

                    int elseFlag = conditionAddress + 1;

                    Code.CommentLine($"ELSE flag is at {elseFlag}");

                    using (Code.Block("Set ELSE flag"))
                    { Code.SetValue(elseFlag, 1); }

                    using (Code.Block("If condition is true"))
                    {
                        Code.JumpStart(conditionAddress);

                        using (Code.Block("Reset condition"))
                        { Code.ClearValue(conditionAddress); }

                        using (Code.Block("Reset ELSE flag"))
                        { Code.ClearValue(elseFlag); }

                        Code.JumpEnd(conditionAddress);
                    }

                    using (Code.Block($"If ELSE flag set"))
                    {
                        Code.JumpStart(elseFlag);

                        using (Code.Block("Reset ELSE flag"))
                        { Code.ClearValue(elseFlag); }

                        using (Code.Block($"Elseif ({@elseif.Condition})"))
                        {
                            int _conditionAddress = Stack.NextAddress;
                            using (Code.Block("Compute condition"))
                            { Compile(@elseif.Condition); }

                            Code.CommentLine($"Condition result at {_conditionAddress}");

                            Code.JumpStart(_conditionAddress);

                            using (Code.Block("The if statements"))
                            { Compile(@elseif.Block); }

                            if (index + 1 >= branches.Length)
                            {
                                using (Code.Block("Cleanup condition"))
                                {
                                    Code.ClearValue(_conditionAddress);
                                    Code.JumpEnd(_conditionAddress);
                                    Stack.PopVirtual();
                                }
                            }
                            else
                            {
                                Compile(branches, index + 1, _conditionAddress);
                            }
                        }

                        using (Code.Block($"Reset ELSE flag"))
                        { Code.ClearValue(elseFlag); }

                        Code.JumpEnd(elseFlag);
                    }

                    using (Code.Block("Reset ELSE flag"))
                    { Code.ClearValue(elseFlag); }
                }
                return;
            }

            if (branch is ElseBranch @else)
            {
                Code.LOGIC_NOT(conditionAddress, conditionAddress + 1);

                using (Code.Block("Else"))
                {
                    using (Code.Block("Finish if statement"))
                    {
                        Code.MoveValue(conditionAddress, conditionAddress + 1);
                        Code.JumpEnd(conditionAddress);
                        Code.MoveValue(conditionAddress + 1, conditionAddress);
                    }

                    Stack.PopVirtual();

                    int elseFlag = conditionAddress + 1;

                    Code.CommentLine($"ELSE flag is at {elseFlag}");

                    using (Code.Block("Set ELSE flag"))
                    { Code.SetValue(elseFlag, 1); }

                    using (Code.Block("If condition is true"))
                    {
                        Code.JumpStart(conditionAddress);

                        using (Code.Block("Reset condition"))
                        { Code.ClearValue(conditionAddress); }

                        using (Code.Block("Reset ELSE flag"))
                        { Code.ClearValue(elseFlag); }

                        Code.JumpEnd(conditionAddress);
                    }

                    using (Code.Block($"If ELSE flag set"))
                    {
                        Code.JumpStart(elseFlag);

                        using (Code.Block("Reset ELSE flag"))
                        { Code.ClearValue(elseFlag); }

                        using (Code.Block("Block"))
                        {
                            Compile(@else.Block);
                        }

                        using (Code.Block($"Reset ELSE flag"))
                        { Code.ClearValue(elseFlag); }

                        Code.JumpEnd(elseFlag);
                    }

                    using (Code.Block("Reset ELSE flag"))
                    { Code.ClearValue(elseFlag); }
                }
                return;
            }
        }
        */
        void Compile(WhileLoop @while)
        {
            using (Code.Block($"While ({@while.Condition})"))
            {
                int conditionAddress = Stack.NextAddress;
                using (Code.Block("Compute condition"))
                { Compile(@while.Condition); }

                Code.CommentLine($"Condition result at {conditionAddress}");

                BreakTagStack.Push(Stack.Push(1));

                Code.JumpStart(conditionAddress);

                using (Code.Block("The while statements"))
                {
                    Compile(@while.Block);
                }

                using (Code.Block("Compute condition again"))
                {
                    Compile(@while.Condition);
                    Stack.PopAndStore(conditionAddress);
                }

                {
                    int tempAddress = Stack.PushVirtual(1);

                    Code.CopyValue(ReturnTagStack[^1], tempAddress);
                    Code.LOGIC_NOT(tempAddress, tempAddress + 1);
                    Code.JumpStart(tempAddress);

                    Code.SetValue(conditionAddress, 0);

                    Code.ClearValue(tempAddress);
                    Code.JumpEnd(tempAddress);


                    Code.CopyValue(BreakTagStack[^1], tempAddress);
                    Code.LOGIC_NOT(tempAddress, tempAddress + 1);
                    Code.JumpStart(tempAddress);

                    Code.SetValue(conditionAddress, 0);

                    Code.ClearValue(tempAddress);
                    Code.JumpEnd(tempAddress);

                    Stack.PopVirtual();
                }

                Code.JumpEnd(conditionAddress);

                if (Stack.LastAddress != BreakTagStack.Pop())
                { throw new InternalException(); }
                Stack.Pop();

                Stack.Pop();

                ContinueReturnStatements();
                ContinueBreakStatements();
            }
        }
        void Compile(ForLoop @for)
        {
            using (Code.Block($"For"))
            {
                VariableCleanupStack.Push(PrecompileVariable(@for.VariableDeclaration));

                using (Code.Block("Variable Declaration"))
                { Compile(@for.VariableDeclaration); }

                int conditionAddress = Stack.NextAddress;
                using (Code.Block("Compute condition"))
                { Compile(@for.Condition); }

                Code.CommentLine($"Condition result at {conditionAddress}");

                BreakTagStack.Push(Stack.Push(1));

                Code.JumpStart(conditionAddress);

                using (Code.Block("The while statements"))
                {
                    Compile(@for.Block);
                }

                using (Code.Block("Compute expression"))
                {
                    Compile(@for.Expression);
                }

                using (Code.Block("Compute condition again"))
                {
                    Compile(@for.Condition);
                    Stack.PopAndStore(conditionAddress);
                }

                {
                    int tempAddress = Stack.PushVirtual(1);

                    Code.CopyValue(ReturnTagStack[^1], tempAddress);
                    Code.LOGIC_NOT(tempAddress, tempAddress + 1);
                    Code.JumpStart(tempAddress);

                    Code.SetValue(conditionAddress, 0);

                    Code.ClearValue(tempAddress);
                    Code.JumpEnd(tempAddress);


                    Code.CopyValue(BreakTagStack[^1], tempAddress);
                    Code.LOGIC_NOT(tempAddress, tempAddress + 1);
                    Code.JumpStart(tempAddress);

                    Code.SetValue(conditionAddress, 0);

                    Code.ClearValue(tempAddress);
                    Code.JumpEnd(tempAddress);

                    Stack.PopVirtual();
                }

                Code.JumpEnd(conditionAddress);

                if (Stack.LastAddress != BreakTagStack.Pop())
                { throw new InternalException(); }
                Stack.Pop();

                Stack.Pop();

                CleanupVariables(VariableCleanupStack.Pop());

                ContinueReturnStatements();
                ContinueBreakStatements();
            }
        }
        void Compile(KeywordCall statement)
        {
            switch (statement.Identifier.Content.ToLower())
            {
                case "return":
                    {
                        statement.Identifier.AnalyzedType = TokenAnalysedType.Statement;

                        if (statement.Parameters.Length != 0 &&
                            statement.Parameters.Length != 1)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 0 or 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        if (InMacro.Last)
                        { throw new NotImplementedException(); }

                        if (statement.Parameters.Length == 1)
                        {
                            if (!Variables.TryFind("@return", out Variable returnVariable))
                            { throw new CompilerException($"Can't return value for some reason :(", statement, CurrentFile); }

                            CompileSetter(returnVariable, statement.Parameters[0]);
                        }

                        Warnings.Add(new Warning($"This kind of control flow (return and break) is not fully tested. Expect a buggy behavior!", statement.Identifier, CurrentFile));

                        if (ReturnTagStack.Count <= 0)
                        { throw new CompilerException($"Can't return for some reason :(", statement.Identifier, CurrentFile); }

                        Code.SetValue(ReturnTagStack[^1], 0);

                        Code.SetPointer(Stack.NextAddress);
                        Code.ClearCurrent();
                        Code.JumpStart(Stack.NextAddress);

                        ReturnCount[^1]++;

                        break;
                    }

                case "break":
                    {
                        statement.Identifier.AnalyzedType = TokenAnalysedType.Statement;

                        if (statement.Parameters.Length != 0)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 0, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        if (BreakTagStack.Count <= 0)
                        { throw new CompilerException($"Looks like this \"{statement.Identifier}\" statement is not inside a loop. Am i wrong? Of course not! Haha", statement.Identifier, CurrentFile); }

                        Warnings.Add(new Warning($"This kind of control flow (return and break) is not fully tested. Expect a buggy behavior!", statement.Identifier, CurrentFile));

                        Code.SetValue(BreakTagStack[^1], 0);

                        Code.SetPointer(Stack.NextAddress);
                        Code.ClearCurrent();
                        Code.JumpStart(Stack.NextAddress);
                        BreakCount[^1]++;

                        break;
                    }

                /*
            case "outraw":
                {
                    if (statement.Parameters.Length <= 0)
                    { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required minimum 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                    foreach (StatementWithValue? value in statement.Parameters)
                    { CompileRawPrinter(value); }

                    break;
                }
            case "out":
                {
                    if (statement.Parameters.Length <= 0)
                    { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required minimum 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                    foreach (StatementWithValue valueToPrint in statement.Parameters)
                    { CompilePrinter(valueToPrint); }

                    break;
                }
                */

                case "delete":
                    {
                        statement.Identifier.AnalyzedType = TokenAnalysedType.Keyword;

                        if (statement.Parameters.Length != 1)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        var deletable = statement.Parameters[0];
                        var deletableType = FindStatementType(deletable);

                        if (deletableType.IsClass)
                        {
                            if (!TryGetRuntimeAddress(deletable, out int pointerAddress, out int size))
                            { throw new CompilerException($"Failed to get address", deletable, CurrentFile); }

                            int _pointerAddress = Stack.PushVirtual(1);

                            for (int offset = 0; offset < size; offset++)
                            {
                                Code.CopyValue(pointerAddress, _pointerAddress);
                                Code.AddValue(_pointerAddress, offset);

                                Heap.Set(_pointerAddress, 0);
                                // Heap.Free(_pointerAddress);
                            }

                            Stack.Pop();

                            Stack.Pop();
                            return;
                        }

                        if (deletableType.BuiltinType == Type.INT)
                        {
                            int pointerAddress = Stack.NextAddress;
                            Compile(deletable);

                            Heap.Set(pointerAddress, 0);
                            // Heap.Free(pointerAddress);

                            Stack.Pop();
                            return;
                        }

                        throw new CompilerException($"Bruh. This probably not stored in heap...", deletable, CurrentFile);
                    }

                default: throw new CompilerException($"Unknown instruction command \"{statement.Identifier}\"", statement.Identifier, CurrentFile);
            }
        }
        void Compile(Assignment statement)
        {
            if (statement.Operator.Content != "=")
            { throw new CompilerException($"Unknown assignment operator \'{statement.Operator}\'", statement.Operator, CurrentFile); }

            CompileSetter(statement.Left, statement.Right ?? throw new CompilerException($"Value is required for \'{statement.Operator}\' assignment", statement, CurrentFile));
        }
        void Compile(CompoundAssignment statement)
        {
            switch (statement.Operator.Content)
            {
                case "+=":
                    {
                        if (statement.Left is not Identifier variableIdentifier)
                        { throw new CompilerException($"Only variable supported :(", statement.Left, CurrentFile); }

                        if (!Variables.TryFind(variableIdentifier.Content, out Variable variable))
                        { throw new CompilerException($"Variable \"{variableIdentifier}\" not found", variableIdentifier, CurrentFile); }

                        if (variable.IsDiscarded)
                        { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", variableIdentifier, CurrentFile); }

                        if (variable.Size != 1)
                        { throw new CompilerException($"Bruh", statement.Left, CurrentFile); }

                        if (statement.Right == null)
                        { throw new CompilerException($"Value is required for '{statement.Operator}' assignment", statement, CurrentFile); }

                        if (TryCompute(statement.Right, variable.Type.IsBuiltin ? variable.Type.RuntimeType : null, out var constantValue))
                        {
                            if (variable.Type != constantValue.Type)
                            { throw new CompilerException($"Variable and value type mismatch ({variable.Type} != {constantValue.Type})", statement.Right, CurrentFile); }

                            switch (constantValue.Type)
                            {
                                case RuntimeType.BYTE:
                                    Code.AddValue(variable.Address, constantValue.ValueByte);
                                    break;
                                case RuntimeType.INT:
                                    Code.AddValue(variable.Address, constantValue.ValueInt);
                                    break;
                                case RuntimeType.FLOAT:
                                    throw new NotSupportedException($"Floats not supported by brainfuck :(", statement.Right, CurrentFile);
                                case RuntimeType.CHAR:
                                    Code.AddValue(variable.Address, constantValue.ValueChar);
                                    break;
                                default:
                                    throw new ImpossibleException();
                            }

                            Optimizations++;
                            return;
                        }

                        using (Code.Block($"Add {statement.Right} to variable {variable.Name} (at {variable.Address})"))
                        {
                            using (Code.Block($"Compute value"))
                            {
                                Compile(statement.Right);
                            }

                            using (Code.Block($"Set computed value to {variable.Address}"))
                            {
                                Stack.Pop(address => Code.MoveAddValue(address, variable.Address));
                            }
                        }

                        return;
                    }
                case "-=":
                    {
                        if (statement.Left is not Identifier variableIdentifier)
                        { throw new CompilerException($"Only variable supported :(", statement.Left, CurrentFile); }

                        if (!Variables.TryFind(variableIdentifier.Content, out Variable variable))
                        { throw new CompilerException($"Variable \"{variableIdentifier}\" not found", variableIdentifier, CurrentFile); }

                        if (variable.IsDiscarded)
                        { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", variableIdentifier, CurrentFile); }

                        if (variable.Size != 1)
                        { throw new CompilerException($"Bruh", variableIdentifier, CurrentFile); }

                        if (statement.Right == null)
                        { throw new CompilerException($"Value is required for '{statement.Operator}' assignment", statement, CurrentFile); }

                        if (TryCompute(statement.Right, variable.Type.IsBuiltin ? variable.Type.RuntimeType : null, out var constantValue))
                        {
                            if (variable.Type != constantValue.Type)
                            { throw new CompilerException($"Variable and value type mismatch ({variable.Type} != {constantValue.Type})", statement.Right, CurrentFile); }

                            switch (constantValue.Type)
                            {
                                case RuntimeType.BYTE:
                                    Code.AddValue(variable.Address, -constantValue.ValueByte);
                                    break;
                                case RuntimeType.INT:
                                    Code.AddValue(variable.Address, -constantValue.ValueInt);
                                    break;
                                case RuntimeType.FLOAT:
                                    throw new NotSupportedException($"Floats not supported by brainfuck :(", statement.Right, CurrentFile);
                                case RuntimeType.CHAR:
                                    Code.AddValue(variable.Address, -constantValue.ValueChar);
                                    break;
                                default:
                                    throw new ImpossibleException();
                            }

                            Optimizations++;
                            return;
                        }

                        using (Code.Block($"Add {statement.Right} to variable {variable.Name} (at {variable.Address})"))
                        {
                            using (Code.Block($"Compute value"))
                            {
                                Compile(statement.Right);
                            }

                            using (Code.Block($"Set computed value to {variable.Address}"))
                            {
                                Stack.Pop(address => Code.MoveSubValue(address, variable.Address));
                            }
                        }

                        return;
                    }
                default:
                    Compile(statement.ToAssignment());
                    break;
                    //throw new CompilerException($"Unknown compound assignment operator \'{statement.Operator}\'", statement.Operator);
            }
        }
        void Compile(ShortOperatorCall statement)
        {
            switch (statement.Operator.Content)
            {
                case "++":
                    {
                        if (statement.Left is not Identifier variableIdentifier)
                        { throw new CompilerException($"Only variable supported :(", statement.Left, CurrentFile); }

                        if (!Variables.TryFind(variableIdentifier.Content, out Variable variable))
                        { throw new CompilerException($"Variable \"{variableIdentifier}\" not found", variableIdentifier, CurrentFile); }

                        if (variable.IsDiscarded)
                        { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", variableIdentifier, CurrentFile); }

                        if (variable.Size != 1)
                        { throw new CompilerException($"Bruh", statement.Left, CurrentFile); }

                        using (Code.Block($"Increment variable {variable.Name} (at {variable.Address})"))
                        {
                            Code.AddValue(variable.Address, 1);
                        }

                        return;
                    }
                case "--":
                    {
                        if (statement.Left is not Identifier variableIdentifier)
                        { throw new CompilerException($"Only variable supported :(", statement.Left, CurrentFile); }

                        if (!Variables.TryFind(variableIdentifier.Content, out Variable variable))
                        { throw new CompilerException($"Variable \"{variableIdentifier}\" not found", variableIdentifier, CurrentFile); }

                        if (variable.IsDiscarded)
                        { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", variableIdentifier, CurrentFile); }

                        if (variable.Size != 1)
                        { throw new CompilerException($"Bruh", statement.Left, CurrentFile); }

                        using (Code.Block($"Decrement variable {variable.Name} (at {variable.Address})"))
                        {
                            Code.AddValue(variable.Address, -1);
                        }

                        return;
                    }
                default:
                    throw new CompilerException($"Unknown assignment operator \'{statement.Operator}\'", statement.Operator, CurrentFile);
            }
        }
        void Compile(VariableDeclaration statement)
        {
            if (statement.InitialValue == null) return;

            if (!Variables.TryFind(statement.VariableName.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{statement.VariableName.Content}\" not found", statement.VariableName, CurrentFile); }

            if (variable.IsInitialValueSet)
            { return; }

            CompileSetter(variable, statement.InitialValue);
        }
        void Compile(FunctionCall functionCall)
        {
            if (functionCall.Identifier == "Alloc" &&
                functionCall.IsMethodCall == false &&
                CompiledType.Equals(FindStatementTypes(functionCall.Parameters), new CompiledType(RuntimeType.INT)))
            { throw new NotSupportedException($"Heap is not supported :(", functionCall, CurrentFile); }

            if (functionCall.Identifier == "AllocFrom" &&
                functionCall.IsMethodCall == false &&
                CompiledType.Equals(FindStatementTypes(functionCall.Parameters), new CompiledType(RuntimeType.INT)))
            { throw new NotSupportedException($"Heap is not supported :(", functionCall, CurrentFile); }

            /*
            if (functionCall.Identifier == "Alloc" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 0)
            {
                int resultAddress = Stack.PushVirtual(1);
                // Heap.Allocate(resultAddress);
                throw new NotSupportedException($"Heap is not supported :(");
                return;
            }

            if (functionCall.Identifier == "AllocFrom" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 1 && (
                    FindStatementType(functionCall.Parameters[0]).BuiltinType == Type.BYTE ||
                    FindStatementType(functionCall.Parameters[0]).BuiltinType == Type.INT
                ))
            {
                int resultAddress = Stack.PushVirtual(1);

                int fromAddress = Stack.NextAddress;
                Compile(functionCall.Parameters[0]);

                // Heap.AllocateFrom(resultAddress, fromAddress);

                Stack.Pop();

                throw new NotSupportedException($"Heap is not supported :(");
                return;
            }
            */

            if (TryGetMacro(functionCall, out MacroDefinition? macro))
            {
                functionCall.Identifier.AnalyzedType = TokenAnalysedType.FunctionName;

                string prevFile = CurrentFile;
                CurrentFile = macro.FilePath;

                InMacro.Push(true);

                Statement inlinedMacro = InlineMacro(macro, functionCall.Parameters);

                if (inlinedMacro is Block inlinedMacroBlock)
                { Compile(inlinedMacroBlock); }
                else
                { Compile(inlinedMacro); }

                InMacro.Pop();

                CurrentFile = prevFile;
                return;
            }

            if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compilableFunction.Function;
            }

            functionCall.Identifier.AnalyzedType = TokenAnalysedType.FunctionName;

            // if (!function.Modifiers.Contains("macro"))
            // { throw new NotSupportedException($"Functions not supported by the brainfuck compiler, try using macros instead", functionCall, CurrentFile); }

            InlineMacro(compiledFunction, functionCall.MethodParameters, functionCall);
        }
        void Compile(ConstructorCall constructorCall)
        {
            var instanceType = FindType(constructorCall.TypeName);

            if (instanceType.IsStruct)
            { throw new NotImplementedException(); }

            if (!instanceType.IsClass)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }

            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.TypeName.Identifier, CurrentFile));

            if (!GetClass(constructorCall, out CompiledClass @class))
            { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

            if (!GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), FunctionNames.Constructor, out CompiledGeneralFunction constructor))
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
                return;
            }

            if (constructorCall.Parameters.Length != constructor.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to \"{constructorCall.TypeName}\" constructor: required {constructor.ParameterCount} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

            InlineMacro(constructor, constructorCall.Parameters, constructorCall);
        }
        void Compile(Literal statement)
        {
            using (Code.Block($"Set {statement} to address {Stack.NextAddress}"))
            {
                switch (statement.Type)
                {
                    case LiteralType.INT:
                        {
                            int value = int.Parse(statement.Value);
                            Stack.Push(value);
                            break;
                        }
                    case LiteralType.CHAR:
                        {
                            Stack.Push(statement.Value[0]);
                            break;
                        }
                    case LiteralType.BOOLEAN:
                        {
                            bool value = bool.Parse(statement.Value);
                            Stack.Push(value ? 1 : 0);
                            break;
                        }

                    case LiteralType.FLOAT:
                        throw new NotSupportedException($"Floats not supported by the brainfuck compiler", statement, CurrentFile);
                    case LiteralType.STRING:
                        throw new NotSupportedException($"String literals not supported by the brainfuck compiler", statement, CurrentFile);

                    default:
                        throw new CompilerException($"Unknown literal type {statement.Type}", statement, CurrentFile);
                }
            }
        }
        void Compile(Identifier statement)
        {
            /*
            if (statement.Content == "IN")
            {
                int address = Stack.PushVirtual(1);
                Code.MovePointer(address);
                Code += ',';
                Code.MovePointer(0);

                return;
            }
            */

            if (Variables.TryFind(statement.Content, out Variable variable))
            {
                if (!variable.IsInitialized)
                { throw new CompilerException($"Variable \"{variable.Name}\" not initialized", statement, CurrentFile); }

                if (variable.IsDiscarded)
                { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", statement, CurrentFile); }

                int variableSize = variable.Size;

                if (variableSize <= 0)
                { throw new CompilerException($"Can't load variable \"{variable.Name}\" because it's size is {variableSize} (bruh)", statement, CurrentFile); }

                int loadTarget = Stack.PushVirtual(variableSize);

                using (Code.Block($"Load variable \"{variable.Name}\" (from {variable.Address}) to {loadTarget}"))
                {
                    for (int offset = 0; offset < variableSize; offset++)
                    {
                        int offsettedSource = variable.Address + offset;
                        int offsettedTarget = loadTarget + offset;

                        if (VariableCanBeDiscarded != null && VariableCanBeDiscarded == variable.Name)
                        {
                            Code.MoveValue(offsettedSource, offsettedTarget);
                            DiscardVariable(Variables, variable.Name);
                            Optimizations++;
                        }
                        else
                        {
                            Code.CopyValue(offsettedSource, offsettedTarget);
                        }
                    }
                }

                return;
            }

            if (Constants.TryFind(statement.Content, out ConstantVariable constant))
            {
                using (Code.Block($"Load constant {variable.Name} (with value {constant.Value})"))
                {
                    Stack.Push(constant.Value);
                }

                return;
            }

            throw new CompilerException($"Variable or constant \"{statement}\" not found", statement, CurrentFile);
        }
        void Compile(OperatorCall statement)
        {
            using (Code.Block($"Expression {statement.Left} {statement.Operator} {statement.Right}"))
            {
                switch (statement.Operator.Content)
                {
                    case "==":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block("Compute equality"))
                            {
                                Code.LOGIC_EQ(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                            }

                            Stack.Pop();

                            break;
                        }
                    case "+":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Move & add right-side (from {rightAddress}) to left-side (to {leftAddress})"))
                            { Code.MoveAddValue(rightAddress, leftAddress); }

                            Stack.PopVirtual();

                            break;
                        }
                    case "-":
                        {
                            {
                                if (statement.Left is Identifier _left &&
                                    Variables.TryFind(_left.Content, out var left) &&
                                    !left.IsDiscarded &&
                                    TryCompute(statement.Right, null, out var right) &&
                                    right.Type == RuntimeType.BYTE)
                                {
                                    int resultAddress = Stack.PushVirtual(1);

                                    Code.CopyValueWithTemp(left.Address, Stack.NextAddress, resultAddress);

                                    Code.AddValue(resultAddress, -right.ValueByte);

                                    Optimizations++;

                                    return;
                                }
                            }

                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Move & sub right-side (from {rightAddress}) from left-side (to {leftAddress})"))
                            { Code.MoveSubValue(rightAddress, leftAddress); }

                            Stack.PopVirtual();

                            return;
                        }
                    case "*":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet MULTIPLY({leftAddress} {rightAddress})"))
                            {
                                Code.MULTIPLY(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                            }

                            Stack.Pop();

                            break;
                        }
                    case "/":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet DIVIDE({leftAddress} {rightAddress})"))
                            {
                                Code.MATH_DIV(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2, rightAddress + 3, rightAddress + 4);
                            }

                            Stack.Pop();

                            break;
                        }
                    case "%":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet MOD({leftAddress} {rightAddress})"))
                            {
                                Code.MATH_MOD(leftAddress, rightAddress, rightAddress + 1);
                            }

                            Stack.Pop();

                            break;
                        }
                    case "<":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet LT({leftAddress} {rightAddress})"))
                            {
                                Code.LOGIC_LT(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                            }

                            Stack.Pop();

                            break;
                        }
                    case ">":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet MT({leftAddress} {rightAddress})"))
                            {
                                Code.LOGIC_MT(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2, rightAddress + 3);
                            }

                            Stack.Pop();

                            Code.MoveValue(rightAddress + 1, leftAddress);

                            break;
                        }
                    case ">=":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet LTEQ({leftAddress} {rightAddress})"))
                            {
                                Code.LOGIC_LT(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                                Stack.Pop();
                                Code.SetPointer(leftAddress);
                                Code.LOGIC_NOT(leftAddress, rightAddress);
                            }

                            break;
                        }
                    case "<=":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet LTEQ({leftAddress} {rightAddress})"))
                            {
                                Code.LOGIC_LTEQ(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                            }

                            Stack.Pop();

                            break;
                        }
                    case "!=":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet NEQ({leftAddress} {rightAddress})"))
                            {
                                Code.LOGIC_NEQ(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                            }

                            Stack.Pop();

                            break;
                        }
                    case "&&":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int tempLeftAddress = Stack.PushVirtual(1);
                            Code.CopyValue(leftAddress, tempLeftAddress);

                            Code.JumpStart(tempLeftAddress);

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet AND({leftAddress} {rightAddress})"))
                            { Code.LOGIC_AND(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2); }

                            Stack.Pop(); // Pop rightAddress

                            Code.JumpEnd(tempLeftAddress, true);
                            Stack.PopVirtual(); // Pop tempLeftAddress

                            break;
                        }
                    case "||":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int tempLeftAddress = Stack.PushVirtual(1);
                            Code.CopyValue(leftAddress, tempLeftAddress);
                            Code.LOGIC_NOT(tempLeftAddress, tempLeftAddress + 1);

                            Code.JumpStart(tempLeftAddress);

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet AND({leftAddress} {rightAddress})"))
                            { Code.LOGIC_OR(leftAddress, rightAddress, rightAddress + 1); }

                            Stack.Pop(); // Pop rightAddress

                            Code.JumpEnd(tempLeftAddress, true);
                            Stack.PopVirtual(); // Pop tempLeftAddress

                            break;
                        }
                    default: throw new CompilerException($"Unknown operator \"{statement.Operator}\"", statement.Operator, CurrentFile);
                }
            }
        }
        void Compile(Block block)
        {
            using (this.DebugBlock(block.BracketStart))
            {
                VariableCleanupStack.Push(PrecompileVariables(block));

                if (ReturnTagStack.Count > 0)
                { ReturnCount.Push(0); }

                if (BreakTagStack.Count > 0)
                { BreakCount.Push(0); }
            }

            foreach (Statement statement in block.Statements)
            {
                VariableCanBeDiscarded = null;
                Compile(statement);
                VariableCanBeDiscarded = null;
            }

            using (this.DebugBlock(block.BracketEnd))
            {
                if (ReturnTagStack.Count > 0)
                { FinishReturnStatements(); }

                if (BreakTagStack.Count > 0)
                { FinishBreakStatements(); }

                CleanupVariables(VariableCleanupStack.Pop());
            }
        }
        void Compile(AddressGetter addressGetter)
        {
            throw new NotImplementedException();

            /*
            if (addressGetter.Statement is Identifier identifier)
            {
                if (!Variables.TryFind(identifier.Value.Content, out Variable variable))
                { throw new CompilerException($"Variable \"{identifier}\" not found", identifier); }

                if (variable.IsDiscarded)
                { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", identifier); }

                using (Code.Block($"Load variable address {variable.Name} ({variable.Address})"))
                {
                    int resultAddress = this.Stack.PushVirtual(1);
                    Code.SetValue(resultAddress, variable.Address);
                }

                return;
            }

            throw new CompilerException($"Invalid statement ({addressGetter.Statement.GetType().Name}) passed to address getter", addressGetter.Statement);
            */
        }
        void Compile(Pointer pointer)
        {
            int pointerAddress = Stack.NextAddress;
            Compile(pointer.PrevStatement);

            Heap.Get(pointerAddress, pointerAddress);

            /*
            if (pointer.Statement is Identifier identifier)
            {
                if (Constants.TryFind(identifier.Value.Content, out ConstantVariable constant))
                {
                    if (constant.Value.Type != ValueType.Byte)
                    { throw new CompilerException($"Address value must be a byte (not {constant.Value.Type})", identifier); }

                    byte address = (byte)constant.Value;
                    using (Code.Block($"Load value from address {address}"))
                    {
                        this.Stack.PushVirtual(1);

                        int nextAddress = Stack.NextAddress;

                        using (Code.Block($"Move {address} to {nextAddress} and {nextAddress + 1}"))
                        { Code.MoveValue(address, nextAddress, nextAddress + 1); }

                        using (Code.Block($"Move {nextAddress + 1} to {address}"))
                        { Code.MoveValue(nextAddress + 1, address); }
                    }

                    return;
                }
            }

            throw new NotSupportedException($"Runtime pointer address not supported", pointer.Statement);
            */
        }
        void Compile(NewInstance newInstance)
        {
            CompiledType instanceType = FindType(newInstance.TypeName);

            if (instanceType.IsStruct)
            {
                // newInstance.TypeName = newInstance.TypeName.Struct(instanceType.Struct);
                instanceType.Struct.References?.Add(new DefinitionReference(newInstance.TypeName.Identifier, CurrentFile));

                int address = Stack.PushVirtual(instanceType.Struct.Size);

                foreach (var field in instanceType.Struct.Fields)
                {
                    if (!field.Type.IsBuiltin)
                    { throw new NotSupportedException($"Not supported :(", field.Identifier, instanceType.Struct.FilePath); }

                    int offset = instanceType.Struct.FieldOffsets[field.Identifier.Content];

                    int offsettedAddress = address + offset;

                    switch (field.Type.BuiltinType)
                    {
                        case Type.BYTE:
                            Code.SetValue(offsettedAddress, (byte)0);
                            break;
                        case Type.INT:
                            Code.SetValue(offsettedAddress, (byte)0);
                            Warnings.Add(new Warning($"Integers not supported by the brainfuck compiler, so I converted it into byte", field.Identifier, instanceType.Struct.FilePath));
                            break;
                        case Type.CHAR:
                            Code.SetValue(offsettedAddress, (char)'\0');
                            break;
                        case Type.FLOAT:
                            throw new NotSupportedException($"Floats not supported by the brainfuck compiler", field.Identifier, instanceType.Struct.FilePath);
                        case Type.VOID:
                        case Type.UNKNOWN:
                        case Type.NONE:
                        default:
                            throw new CompilerException($"Unknown field type \"{field.Type}\"", field.Identifier, instanceType.Struct.FilePath);
                    }
                }
            }
            else if (instanceType.IsClass)
            {
                throw new NotSupportedException($"Not supported :(", newInstance, CurrentFile);
                /*
                newInstance.TypeName = newInstance.TypeName.Class(@class);
                @class.References?.Add(new DefinitionReference(newInstance.TypeName, CurrentFile));

                int pointerAddress = Stack.PushVirtual(1);

                {
                    int requiredSizeAddress = Stack.Push(@class.Size);
                    int tempAddressesStart = Stack.PushVirtual(1);

                    using (Code.Block($"Allocate (size: {@class.Size} (at {requiredSizeAddress}) result at: {pointerAddress})"))
                    {
                        Heap.Allocate(pointerAddress, requiredSizeAddress, tempAddressesStart);

                        using (Code.Block("Clear temps (5x pop)"))
                        {
                            Stack.Pop();
                            Stack.Pop();
                        }
                    }
                }

                using (Code.Block($"Generate fields"))
                {
                    int currentOffset = 0;
                    for (int fieldIndex = 0; fieldIndex < @class.Fields.Length; fieldIndex++)
                    {
                        CompiledField field = @class.Fields[fieldIndex];
                        using (Code.Block($"Field #{fieldIndex} (\"{field.Identifier.Content}\")"))
                        {
                            var initialValue = GetInitialValue(field.Type);

                            int fieldPointerAddress = Stack.PushVirtual(1);

                            using (Code.Block($"Compute field address (at {fieldPointerAddress})"))
                            {
                                Code.CopyValue(pointerAddress, fieldPointerAddress);
                                Code.AddValue(fieldPointerAddress, currentOffset);
                            }

                            int initialValueAddress;

                            using (Code.Block($"Push initial value"))
                            { initialValueAddress = Stack.Push(initialValue); }

                            using (Code.Block($"Heap.Set({fieldPointerAddress} {initialValueAddress})"))
                            { Heap.Set(fieldPointerAddress, initialValueAddress); }

                            using (Code.Block("Cleanup"))
                            {
                                Stack.Pop();
                                Stack.Pop();
                            }

                            currentOffset++;
                        }
                    }
                }
                */
            }
            else
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", newInstance.TypeName, CurrentFile); }
        }
        void Compile(Field field)
        {
            if (TryGetAddress(field, out int address, out int size))
            {
                using (Code.Block($"Load field {field} (from {address})"))
                {
                    if (size <= 0)
                    { throw new CompilerException($"Can't load field \"{field}\" because it's size is {size} (bruh)", field, CurrentFile); }

                    int loadTarget = Stack.PushVirtual(size);

                    for (int offset = 0; offset < size; offset++)
                    {
                        int offsettedSource = address + offset;
                        int offsettedTarget = loadTarget + offset;

                        Code.CopyValue(offsettedSource, offsettedTarget);
                    }
                }
            }
            else if (TryGetRuntimeAddress(field, out int pointerAddress, out size))
            {
                /*
                 *      pointerAddress
                 */

                Stack.PopVirtual();

                /*
                 *      pointerAddress (deleted)
                 */

                int resultAddress = Stack.PushVirtual(size);

                /*
                 *      pointerAddress (now resultAddress ... )
                 */

                {
                    int temp = Stack.PushVirtual(1);
                    Code.MoveValue(pointerAddress, temp);
                    pointerAddress = temp;
                }

                /*
                 *      resultAddress
                 *      ...
                 *      pointerAddress
                 */

                int _pointerAddress = Stack.PushVirtual(1);


                /*
                 *      resultAddress
                 *      ...
                 *      pointerAddress
                 *      _pointerAddress
                 */

                for (int offset = 0; offset < size; offset++)
                {
                    Code.CopyValue(pointerAddress, _pointerAddress);
                    Code.AddValue(_pointerAddress, offset);

                    Heap.Get(_pointerAddress, resultAddress + offset);
                }

                Stack.Pop(); // _pointerAddress
                Stack.Pop(); // pointerAddress
            }
            else
            { throw new CompilerException($"Failed to get field memory address", field, CurrentFile); }
        }
        void Compile(TypeCast typeCast)
        {
            Warnings.Add(new Warning($"Type-cast is not supported. I will ignore it and compile just the value", new Position(typeCast.Keyword, typeCast.Type), CurrentFile));

            Compile(typeCast.PrevStatement);
        }
        #endregion

        void CompilePrinter(StatementWithValue value)
        {
            if (TryCompute(value, null, out DataItem constantToPrint))
            {
                CompilePrinter(constantToPrint);
                return;
            }

            CompiledType valueType = FindStatementType(value);
            bool isString = valueType.IsReplacedType("string");

            if (value is Literal literal && isString)
            {
                CompilePrinter(literal.Value);
                return;
            }

            CompileValuePrinter(value, valueType);
        }
        void CompilePrinter(DataItem value)
        {
            if (value.Type == RuntimeType.CHAR)
            {
                int tempAddress = Stack.NextAddress;
                using (Code.Block($"Print character '{value.ValueChar}' (on address {tempAddress})"))
                {
                    Code.SetValue(tempAddress, value.ValueChar);
                    Code.SetPointer(tempAddress);
                    Code += '.';
                    Code.ClearValue(tempAddress);
                    Code.SetPointer(0);
                }
                return;
            }

            if (value.Type == RuntimeType.BYTE)
            {
                int tempAddress = Stack.NextAddress;
                using (Code.Block($"Print number {value.ValueByte} as text (on address {tempAddress})"))
                {
                    Code.SetValue(tempAddress, value.ValueByte);
                    Code.SetPointer(tempAddress);

                    using (Code.Block($"SNIPPET OUT_AS_STRING"))
                    { Code += Snippets.OUT_AS_STRING; }
                    Code.ClearValue(tempAddress);
                    Code.SetPointer(0);
                }
                return;
            }

            if (value.Type == RuntimeType.INT)
            {
                int tempAddress = Stack.NextAddress;
                using (Code.Block($"Print number {value.ValueInt} as text (on address {tempAddress})"))
                {
                    Code.SetValue(tempAddress, value.ValueInt);
                    Code.SetPointer(tempAddress);

                    using (Code.Block($"SNIPPET OUT_AS_STRING"))
                    { Code += Snippets.OUT_AS_STRING; }
                    Code.ClearValue(tempAddress);
                    Code.SetPointer(0);
                }
                return;
            }

            throw new NotImplementedException($"Unimplemented constant value type \"{value.Type}\"");
        }
        void CompilePrinter(string value)
        {
            using (Code.Block($"Print string value \"{value}\""))
            {
                int address = Stack.NextAddress;

                Code.ClearValue(address);

                byte prevValue = 0;
                for (int i = 0; i < value.Length; i++)
                {
                    Code.SetPointer(address);
                    byte charToPrint = CharCode.GetByte(value[i]);

                    while (prevValue > charToPrint)
                    {
                        Code += '-';
                        prevValue--;
                    }

                    while (prevValue < charToPrint)
                    {
                        Code += '+';
                        prevValue++;
                    }

                    prevValue = charToPrint;

                    Code += '.';
                }

                Code.ClearValue(address);
                Code.SetPointer(0);
            }
        }
        void CompileValuePrinter(StatementWithValue value)
            => CompileValuePrinter(value, FindStatementType(value));
        void CompileValuePrinter(StatementWithValue value, CompiledType valueType)
        {
            if (valueType.SizeOnStack != 1)
            { throw new NotSupportedException($"Only value of size 1 (not {valueType.SizeOnStack}) supported by the output printer in brainfuck", value, CurrentFile); }

            if (!valueType.IsBuiltin)
            { throw new NotSupportedException($"Only built-in types or string literals (not \"{valueType}\") supported by the output printer in brainfuck", value, CurrentFile); }

            using (Code.Block($"Print value {value} as text"))
            {
                int address = Stack.NextAddress;

                using (Code.Block($"Compute value"))
                { Compile(value); }

                Code.CommentLine($"Computed value is on {address}");

                Code.SetPointer(address);

                switch (valueType.BuiltinType)
                {
                    case Type.BYTE:
                        using (Code.Block($"SNIPPET OUT_AS_STRING"))
                        { Code += Snippets.OUT_AS_STRING; }
                        break;
                    case Type.INT:
                        using (Code.Block($"SNIPPET OUT_AS_STRING"))
                        { Code += Snippets.OUT_AS_STRING; }
                        break;
                    case Type.FLOAT:
                        using (Code.Block($"SNIPPET OUT_AS_STRING"))
                        { Code += Snippets.OUT_AS_STRING; }
                        break;
                    case Type.CHAR:
                        Code += '.';
                        break;
                    case Type.NONE:
                    case Type.VOID:
                    case Type.UNKNOWN:
                    default:
                        throw new CompilerException($"Invalid type {valueType.BuiltinType}");
                }

                using (Code.Block($"Clear address {address}"))
                { Code.ClearValue(address); }

                Stack.PopVirtual();

                Code.SetPointer(0);
            }
        }

        void CompileRawPrinter(StatementWithValue value)
        {
            if (TryCompute(value, null, out DataItem constantValue))
            {
                CompileRawPrinter(constantValue);
                return;
            }

            if (value is Identifier identifier && Variables.TryFind(identifier.Content, out Variable variable))
            {
                CompileRawPrinter(variable, identifier);
                return;
            }

            CompileRawValuePrinter(value);
        }
        void CompileRawPrinter(DataItem value)
        {
            if (value.Type == RuntimeType.BYTE)
            {
                using (Code.Block($"Print value {value.ValueByte}"))
                {
                    Code.SetPointer(Stack.NextAddress);
                    Code.ClearCurrent();
                    Code.AddValue(value.ValueByte);

                    Code += '.';

                    Code.ClearCurrent();
                    Code.SetPointer(0);
                }
                return;
            }

            if (value.Type == RuntimeType.INT)
            {
                using (Code.Block($"Print value {value.ValueInt}"))
                {
                    Code.SetPointer(Stack.NextAddress);
                    Code.ClearCurrent();
                    Code.AddValue(value.ValueInt);

                    Code += '.';

                    Code.ClearCurrent();
                    Code.SetPointer(0);
                }
                return;
            }

            if (value.Type == RuntimeType.CHAR)
            {
                using (Code.Block($"Print value '{value.ValueChar}'"))
                {
                    Code.SetPointer(Stack.NextAddress);
                    Code.ClearCurrent();
                    Code.AddValue(value.ValueChar);

                    Code += '.';

                    Code.ClearCurrent();
                    Code.SetPointer(0);
                }
                return;
            }

            throw new NotImplementedException($"Unimplemented constant value type \"{value.Type}\"");
        }
        void CompileRawPrinter(Variable variable, IThingWithPosition symbolPosition)
        {
            if (variable.IsDiscarded)
            { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", symbolPosition, CurrentFile); }

            using (Code.Block($"Print variable (\"{variable.Name}\") (from {variable.Address}) value"))
            {
                int size = variable.Size;
                for (int offset = 0; offset < size; offset++)
                {
                    int offsettedAddress = variable.Address + offset;
                    Code.SetPointer(offsettedAddress);
                    Code += '.';
                }
                Code.SetPointer(0);
            }
        }
        void CompileRawValuePrinter(StatementWithValue value)
        {
            using (Code.Block($"Print {value} as raw"))
            {
                using (Code.Block($"Compute value"))
                { Compile(value); }

                using (Code.Block($"Print computed value"))
                {
                    Stack.Pop(address =>
                    {
                        Code.SetPointer(address);
                        Code += '.';
                        Code.ClearCurrent();
                    });
                    Code.SetPointer(0);
                }
            }
        }

        /// <param name="callerPosition">
        /// Used for exceptions
        /// </param>
        void InlineMacro(CompiledFunction function, StatementWithValue[] parameters, IThingWithPosition callerPosition)
        {
            if (function.CompiledAttributes.HasAttribute("StandardOutput"))
            {
                foreach (StatementWithValue parameter in parameters)
                { CompilePrinter(parameter); }
                return;
            }

            // if (!function.Modifiers.Contains("macro"))
            // { throw new NotSupportedException($"Functions not supported by the brainfuck compiler, try using macros instead", callerPosition, CurrentFile); }

            for (int i = 0; i < CurrentMacro.Count; i++)
            {
                if (CurrentMacro[i] == function)
                { throw new CompilerException($"Recursive macro inlining is not allowed (The macro \"{function.Identifier}\" used recursively)", callerPosition, CurrentFile); }
            }

            if (function.Parameters.Length != parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to macro \"{function.Identifier}\" (required {function.Parameters.Length} passed {parameters.Length})", callerPosition, CurrentFile); }

            if (function.Block is null)
            { throw new CompilerException($"Function \"{function.ReadableID()}\" does not have any body definition", callerPosition, CurrentFile); }

            Variable? returnVariable = null;

            if (function.ReturnSomething)
            {
                var returnType = function.Type;
                returnVariable = new Variable("@return", Stack.PushVirtual(returnType.Size), function, false, returnType, returnType.Size);
            }

            Stack<Variable> compiledParameters = new();
            List<ConstantVariable> constantParameters = new();

            CurrentMacro.Push(function);
            InMacro.Push(false);

            for (int i = 0; i < parameters.Length; i++)
            {
                StatementWithValue passed = parameters[i];
                ParameterDefinition defined = function.Parameters[i];

                CompiledType passedType = FindStatementType(passed);
                CompiledType definedType = function.ParameterTypes[i];

                if (passedType != definedType &&
                    !definedType.IsGeneric)
                { throw new CompilerException($"Wrong type of argument passed to function \"{function.ReadableID()}\" at index {i}: Expected {definedType}, passed {passedType}", passed, CurrentFile); }

                if (IllegalIdentifiers.Contains(defined.Identifier.Content))
                { throw new CompilerException($"Illegal parameter name \"{defined}\"", defined.Identifier, CurrentFile); }

                if (compiledParameters.TryFind(defined.Identifier.Content, out _))
                { throw new CompilerException($"Parameter \"{defined}\" already defined as parameter", defined.Identifier, CurrentFile); }

                if (constantParameters.TryFind(defined.Identifier.Content, out _))
                { throw new CompilerException($"Parameter \"{defined}\" already defined as constant", defined.Identifier, CurrentFile); }

                if (defined.Modifiers.Contains("ref") && defined.Modifiers.Contains("const"))
                { throw new CompilerException($"Bruh", defined.Identifier, CurrentFile); }

                if (passed is ModifiedStatement modifiedStatement)
                {
                    if (!defined.Modifiers.Contains(modifiedStatement.Modifier.Content))
                    { throw new CompilerException($"Invalid modifier \"{modifiedStatement.Modifier.Content}\"", modifiedStatement.Modifier, CurrentFile); }

                    switch (modifiedStatement.Modifier.Content)
                    {
                        case "ref":
                            {
                                var modifiedVariable = (Identifier)modifiedStatement.Statement;

                                if (!Variables.TryFind(modifiedVariable.Content, out Variable v))
                                { throw new CompilerException($"Variable \"{modifiedVariable}\" not found", modifiedVariable, CurrentFile); }

                                if (v.Type != definedType &&
                                    !definedType.IsGeneric)
                                { throw new CompilerException($"Wrong type of argument passed to function \"{function.ReadableID()}\" at index {i}: Expected {definedType}, passed {v.Type}", passed, CurrentFile); }

                                compiledParameters.Push(new Variable(defined.Identifier.Content, v.Address, function, false, v.Type, v.Size));
                                continue;
                            }
                        case "const":
                            {
                                var valueStatement = modifiedStatement.Statement;
                                if (!TryCompute(valueStatement, null, out DataItem constValue))
                                { throw new CompilerException($"Constant parameter must have a constant value", valueStatement, CurrentFile); }

                                constantParameters.Add(new ConstantVariable(defined.Identifier.Content, constValue));
                                continue;
                            }
                        case "temp":
                            {
                                passed = modifiedStatement.Statement;
                                break;
                            }
                        default:
                            throw new CompilerException($"Unknown identifier modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, CurrentFile);
                    }
                }

                if (passed is StatementWithValue value)
                {
                    if (defined.Modifiers.Contains("ref"))
                    { throw new CompilerException($"You must pass the parameter \"{passed}\" with a \"{"ref"}\" modifier", passed, CurrentFile); }

                    if (defined.Modifiers.Contains("const"))
                    { throw new CompilerException($"You must pass the parameter \"{passed}\" with a \"{"const"}\" modifier", passed, CurrentFile); }

                    PrecompileVariable(compiledParameters, defined.Identifier.Content, new CompiledType(defined.Type, FindType), value);

                    if (!compiledParameters.TryFind(defined.Identifier.Content, out Variable variable))
                    { throw new CompilerException($"Parameter \"{defined}\" not found", defined.Identifier, CurrentFile); }

                    if (variable.Type != definedType &&
                        !definedType.IsGeneric)
                    { throw new CompilerException($"Wrong type of argument passed to function \"{function.ReadableID()}\" at index {i}: Expected {definedType}, passed {variable.Type}", passed, CurrentFile); }

                    using (Code.Block($"SET {defined.Identifier.Content} TO _something_"))
                    {
                        Compile(value);

                        using (Code.Block($"STORE LAST TO {variable.Address}"))
                        { Stack.PopAndStore(variable.Address); }
                    }
                    continue;
                }

                throw new NotImplementedException($"Unimplemented invocation parameter {passed.GetType().Name}");
            }



            int[] savedBreakTagStack = new int[BreakTagStack.Count];
            for (int i = 0; i < BreakTagStack.Count; i++)
            { savedBreakTagStack[i] = BreakTagStack[i]; }
            BreakTagStack.Clear();

            int[] savedBreakCount = new int[BreakCount.Count];
            for (int i = 0; i < BreakCount.Count; i++)
            { savedBreakCount[i] = BreakCount[i]; }
            BreakCount.Clear();

            Variable[] savedVariables = new Variable[Variables.Count];
            for (int i = 0; i < Variables.Count; i++)
            { savedVariables[i] = Variables[i]; }
            Variables.Clear();

            if (returnVariable.HasValue)
            { Variables.Push(returnVariable.Value); }

            for (int i = 0; i < compiledParameters.Count; i++)
            { Variables.Push(compiledParameters[i]); }




            ConstantVariable[] savedConstants = new ConstantVariable[Constants.Count];
            for (int i = 0; i < Constants.Count; i++)
            { savedConstants[i] = Constants[i]; }
            Constants.Clear();

            for (int i = 0; i < constantParameters.Count; i++)
            { Constants.Add(constantParameters[i]); }

            for (int i = 0; i < savedConstants.Length; i++)
            {
                if (Constants.TryFind(savedConstants[i].Name, out _))
                { continue; }
                Constants.Add(savedConstants[i]);
            }

            using (Code.Block($"Begin \"return\" block (depth: {ReturnTagStack.Count} (now its one more))"))
            {
                ReturnTagStack.Push(Stack.Push(1));
            }

            Compile(function.Block);

            {
                if (ReturnTagStack.Pop() != Stack.LastAddress)
                { throw new InternalException(); }
                Stack.Pop();
            }

            using (Code.Block($"Clean up macro variables ({Variables.Count})"))
            {
                int n = Variables.Count;
                for (int i = 0; i < n; i++)
                {
                    Variable variable = Variables.Pop();
                    if (!variable.HaveToClean) continue;
                    Stack.Pop();
                }
            }

            InMacro.Pop();
            CurrentMacro.Pop();

            Variables.Clear();
            for (int i = 0; i < savedVariables.Length; i++)
            { Variables.Push(savedVariables[i]); }

            Constants.Clear();
            for (int i = 0; i < savedConstants.Length; i++)
            { Constants.Add(savedConstants[i]); }

            if (BreakCount.Count > 0 ||
                BreakTagStack.Count > 0)
            { throw new InternalException(); }

            BreakCount.Clear();
            for (int i = 0; i < savedBreakCount.Length; i++)
            { BreakCount.Push(savedBreakCount[i]); }

            BreakTagStack.Clear();
            for (int i = 0; i < savedBreakTagStack.Length; i++)
            { BreakTagStack.Push(savedBreakTagStack[i]); }
        }

        /// <param name="callerPosition">
        /// Used for exceptions
        /// </param>
        void InlineMacro(CompiledGeneralFunction function, StatementWithValue[] parameters, IThingWithPosition callerPosition)
        {
            // if (!function.Modifiers.Contains("macro"))
            // { throw new NotSupportedException($"Functions not supported by the brainfuck compiler, try using macros instead", callerPosition, CurrentFile); }

            for (int i = 0; i < CurrentMacro.Count; i++)
            {
                if (CurrentMacro[i].Identifier.Content == function.Identifier.Content)
                { throw new CompilerException($"Recursive macro inlining is not allowed (The macro \"{function.Identifier}\" used recursively)", callerPosition, CurrentFile); }
            }

            if (function.Parameters.Length != parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to macro \"{function.Identifier}\" (required {function.Parameters.Length} passed {parameters.Length})", callerPosition, CurrentFile); }

            Variable? returnVariable = null;

            if (function.ReturnSomething)
            {
                CompiledType returnType = function.Type;
                returnVariable = new Variable("@return", Stack.PushVirtual(returnType.Size), function, false, returnType, returnType.Size);
            }

            Stack<Variable> compiledParameters = new();
            List<ConstantVariable> constantParameters = new();

            CurrentMacro.Push(function);
            InMacro.Push(false);

            for (int i = 0; i < parameters.Length; i++)
            {
                StatementWithValue passed = parameters[i];
                ParameterDefinition defined = function.Parameters[i];

                CompiledType passedType = FindStatementType(passed);
                CompiledType definedType = function.ParameterTypes[i];

                if (passedType != definedType &&
                    !definedType.IsGeneric)
                { throw new CompilerException($"Wrong type of argument passed to function \"{function.ReadableID()}\" at index {i}: Expected {definedType}, passed {passedType}", passed, CurrentFile); }

                if (IllegalIdentifiers.Contains(defined.Identifier.Content))
                { throw new CompilerException($"Illegal parameter name \"{defined}\"", defined.Identifier, CurrentFile); }

                if (compiledParameters.TryFind(defined.Identifier.Content, out _))
                { throw new CompilerException($"Parameter \"{defined}\" already defined as parameter", defined.Identifier, CurrentFile); }

                if (constantParameters.TryFind(defined.Identifier.Content, out _))
                { throw new CompilerException($"Parameter \"{defined}\" already defined as constant", defined.Identifier, CurrentFile); }

                if (defined.Modifiers.Contains("ref") && defined.Modifiers.Contains("const"))
                { throw new CompilerException($"Bruh", defined.Identifier, CurrentFile); }

                if (passed is ModifiedStatement modifiedStatement)
                {
                    if (!defined.Modifiers.Contains(modifiedStatement.Modifier.Content))
                    { throw new CompilerException($"Invalid modifier \"{modifiedStatement.Modifier.Content}\"", modifiedStatement.Modifier, CurrentFile); }

                    switch (modifiedStatement.Modifier.Content)
                    {
                        case "ref":
                            {
                                var modifiedVariable = (Identifier)modifiedStatement.Statement;

                                if (!Variables.TryFind(modifiedVariable.Content, out Variable v))
                                { throw new CompilerException($"Variable \"{modifiedVariable}\" not found", modifiedVariable, CurrentFile); }

                                if (v.Type != definedType &&
                                    !definedType.IsGeneric)
                                { throw new CompilerException($"Wrong type of argument passed to function \"{function.ReadableID()}\" at index {i}: Expected {definedType}, passed {v.Type}", passed, CurrentFile); }

                                compiledParameters.Push(new Variable(defined.Identifier.Content, v.Address, function, false, v.Type, v.Size));
                                break;
                            }
                        case "const":
                            {
                                var valueStatement = modifiedStatement.Statement;
                                if (!TryCompute(valueStatement, null, out DataItem value))
                                { throw new CompilerException($"Constant parameter must have a constant value", valueStatement, CurrentFile); }

                                constantParameters.Add(new ConstantVariable(defined.Identifier.Content, value));
                                break;
                            }
                        default:
                            throw new CompilerException($"Unknown identifier modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, CurrentFile);
                    }
                }
                else if (passed is StatementWithValue value)
                {
                    if (defined.Modifiers.Contains("ref"))
                    { throw new CompilerException($"You must pass the parameter \"{passed}\" with a \"{"ref"}\" modifier", passed, CurrentFile); }

                    if (defined.Modifiers.Contains("const"))
                    { throw new CompilerException($"You must pass the parameter \"{passed}\" with a \"{"const"}\" modifier", passed, CurrentFile); }

                    PrecompileVariable(compiledParameters, defined.Identifier.Content, new CompiledType(defined.Type, FindType), value);

                    if (!compiledParameters.TryFind(defined.Identifier.Content, out Variable variable))
                    { throw new CompilerException($"Parameter \"{defined}\" not found", defined.Identifier, CurrentFile); }

                    if (variable.Type != definedType &&
                        !definedType.IsGeneric)
                    { throw new CompilerException($"Wrong type of argument passed to function \"{function.ReadableID()}\" at index {i}: Expected {definedType}, passed {variable.Type}", passed, CurrentFile); }

                    using (Code.Block($"SET {defined.Identifier.Content} TO _something_"))
                    {
                        Compile(value);

                        using (Code.Block($"STORE LAST TO {variable.Address}"))
                        { Stack.PopAndStore(variable.Address); }
                    }
                }
                else
                {
                    throw new NotImplementedException($"Unimplemented invocation parameter {passed.GetType().Name}");
                }
            }



            int[] savedBreakTagStack = new int[BreakTagStack.Count];
            for (int i = 0; i < BreakTagStack.Count; i++)
            { savedBreakTagStack[i] = BreakTagStack[i]; }
            BreakTagStack.Clear();

            int[] savedBreakCount = new int[BreakCount.Count];
            for (int i = 0; i < BreakCount.Count; i++)
            { savedBreakCount[i] = BreakCount[i]; }
            BreakCount.Clear();

            Variable[] savedVariables = new Variable[Variables.Count];
            for (int i = 0; i < Variables.Count; i++)
            { savedVariables[i] = Variables[i]; }
            Variables.Clear();

            if (returnVariable.HasValue)
            { Variables.Push(returnVariable.Value); }

            for (int i = 0; i < compiledParameters.Count; i++)
            { Variables.Push(compiledParameters[i]); }




            ConstantVariable[] savedConstants = new ConstantVariable[Constants.Count];
            for (int i = 0; i < Constants.Count; i++)
            { savedConstants[i] = Constants[i]; }
            Constants.Clear();

            for (int i = 0; i < constantParameters.Count; i++)
            { Constants.Add(constantParameters[i]); }

            for (int i = 0; i < savedConstants.Length; i++)
            {
                if (Constants.TryFind(savedConstants[i].Name, out _))
                { continue; }
                Constants.Add(savedConstants[i]);
            }

            using (Code.Block($"Begin \"return\" block (depth: {ReturnTagStack.Count} (now its one more))"))
            {
                ReturnTagStack.Push(Stack.Push(1));
            }

            Compile(function.Block);

            {
                if (ReturnTagStack.Pop() != Stack.LastAddress)
                { throw new InternalException(); }
                Stack.Pop();
            }

            using (Code.Block($"Clean up macro variables ({Variables.Count})"))
            {
                int n = Variables.Count;
                for (int i = 0; i < n; i++)
                {
                    Variable variable = Variables.Pop();
                    if (!variable.HaveToClean) continue;
                    Stack.Pop();
                }
            }

            InMacro.Pop();
            CurrentMacro.Pop();

            Variables.Clear();
            for (int i = 0; i < savedVariables.Length; i++)
            { Variables.Push(savedVariables[i]); }

            Constants.Clear();
            for (int i = 0; i < savedConstants.Length; i++)
            { Constants.Add(savedConstants[i]); }

            if (BreakCount.Count > 0 ||
                BreakTagStack.Count > 0)
            { throw new InternalException(); }

            BreakCount.Clear();
            for (int i = 0; i < savedBreakCount.Length; i++)
            { BreakCount.Push(savedBreakCount[i]); }

            BreakTagStack.Clear();
            for (int i = 0; i < savedBreakTagStack.Length; i++)
            { BreakTagStack.Push(savedBreakTagStack[i]); }
        }

        void FinishReturnStatements()
        {
            int accumulatedReturnCount = ReturnCount.Pop();
            using (Code.Block($"Finish {accumulatedReturnCount} \"return\" statements"))
            {
                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.CommentLine($"Pointer: {Code.Pointer}");
                for (int i = 0; i < accumulatedReturnCount; i++)
                {
                    Code.JumpEnd();
                    Code.LineBreak();
                }
                Code.CommentLine($"Pointer: {Code.Pointer}");
            }
        }
        void ContinueReturnStatements()
        {
            if (ReturnTagStack.Count > 0)
            {
                using (Code.Block("Continue \"return\" statements"))
                {
                    Code.CopyValue(ReturnTagStack[^1], Stack.NextAddress);
                    Code.JumpStart(Stack.NextAddress);
                    ReturnCount[^1]++;
                }
            }
        }

        void FinishBreakStatements()
        {
            int accumulatedBreakCount = BreakCount.Pop();
            using (Code.Block($"Finish {accumulatedBreakCount} \"break\" statements"))
            {
                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.CommentLine($"Pointer: {Code.Pointer}");
                for (int i = 0; i < accumulatedBreakCount; i++)
                {
                    Code.JumpEnd();
                    Code.LineBreak();
                }
                Code.CommentLine($"Pointer: {Code.Pointer}");
            }
        }
        void ContinueBreakStatements()
        {
            if (BreakTagStack.Count > 0)
            {
                using (Code.Block("Continue \"break\" statements"))
                {
                    Code.CopyValue(BreakTagStack[^1], Stack.NextAddress);

                    Code.JumpStart(Stack.NextAddress);

                    BreakCount[^1]++;
                }
            }
        }

        Result GenerateCode(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            PrintCallback? printCallback = null)
        {
            this.Precompile(compilerResult.TopLevelStatements);

            foreach (CompiledFunction? function in CompiledFunctions)
            { Precompile(function); }

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
            { Compile(statement); }

            CompiledFunction codeEntry = GetCodeEntry();

            if (codeEntry != null)
            { InlineMacro(codeEntry, Array.Empty<StatementWithValue>(), codeEntry.Identifier); }

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