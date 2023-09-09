using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

#nullable enable

namespace ProgrammingLanguage.Brainfuck.Compiler
{
    using BBCode;
    using BBCode.Compiler;
    using BBCode.Parser;
    using BBCode.Parser.Statement;
    using Bytecode;
    using Core;
    using Errors;
    using Literal = BBCode.Parser.Statement.Literal;

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

    public class DebugInfo
    {
        internal Position Position;
        internal int InstructionStart;
        internal int InstructionEnd;
    }

    public class CodeGenerator : CodeGeneratorBase
    {
        static readonly string[] IllegalIdentifiers = new string[]
        {
            "IN",
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

        int Optimalizations;

        readonly Stack<FunctionThingDefinition> CurrentMacro;

        readonly Settings GeneratorSettings;

        string? VariableCanBeDiscated = null;

        readonly List<DebugInfo> DebugInfo;

        #endregion

        public CodeGenerator(Compiler.Result compilerResult, Settings settings) : base()
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
            this.DebugInfo = new List<DebugInfo>();
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
            public int Optimalizations;
            public Token[] Tokens;

            public Warning[] Warnings;
            public Error[] Errors;
            public List<DebugInfo> DebugInfo;
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
            public bool IsDiscarted;

            public readonly bool IsInitialized => Type.SizeOnStack > 0;

            public Variable(string name, int address, FunctionThingDefinition? scope, bool haveToClean, CompiledType type, int size)
            {
                Name = name;
                Address = address;
                Scope = scope;
                HaveToClean = haveToClean;
                Type = type;
                IsDiscarted = false;
                Size = size;
            }

            public readonly bool IsThis(string query)
                => query == Name;

            readonly string GetDebuggerDisplay()
                => $"{Name}: *{Address} ({Type.SizeOnStack} bytes)";
        }

        protected override bool GetLocalSymbolType(string symbolName, out CompiledType type)
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

#pragma warning disable CS8625
            type = null;
#pragma warning restore CS8625
            return false;
        }

        static void DiscardVariable(Stack<Variable> variables, string name)
        {
            for (int i = 0; i < variables.Count; i++)
            {
                if (variables[i].Name != name) continue;
                Variable v = variables[i];
                v.IsDiscarted = true;
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
                v.IsDiscarted = false;
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
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"const\" (requied 2, passed {instruction.Parameters.Length})", instruction, CurrentFile); }

                        if (instruction.Parameters[0] is not Identifier constIdentifier)
                        { throw new CompilerException($"Wrong kind of parameter passed to \"const\" at index {0} (requied identifier)", instruction.Parameters[0], CurrentFile); }

                        if (IllegalIdentifiers.Contains(constIdentifier.Content))
                        { throw new CompilerException($"Illegal constant name \"{constIdentifier}\"", constIdentifier, CurrentFile); }

                        if (instruction.Parameters[1] is not StatementWithValue constValue)
                        { throw new CompilerException($"Wrong kind of parameter passed to \"const\" at index {1} (requied a value)", instruction.Parameters[1], CurrentFile); }

                        if (Constants.TryFind(constIdentifier.Content, out _))
                        { throw new CompilerException($"Constant \"{constIdentifier.Content}\" already defined", instruction.Parameters[0], CurrentFile); }

                        if (!TryCompute(constValue, out DataItem value))
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
            // if (!function.Modifiers.Contains("macro"))
            // { return; }

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
            if (statement is not VariableDeclaretion instruction)
            { return 0; }

            return PrecompileVariable(instruction);
        }
        int PrecompileVariable(VariableDeclaretion variableDeclaretion)
        {
            if (IllegalIdentifiers.Contains(variableDeclaretion.VariableName.Content))
            { throw new CompilerException($"Illegal variable name \"{variableDeclaretion.VariableName}\"", variableDeclaretion.VariableName, CurrentFile); }

            StatementWithValue? initialValue = variableDeclaretion.InitialValue;

            if (Variables.TryFind(variableDeclaretion.VariableName.Content, out _))
            { throw new CompilerException($"Variable \"{variableDeclaretion.VariableName.Content}\" already defined", variableDeclaretion.VariableName, CurrentFile); }

            if (Constants.TryFind(variableDeclaretion.VariableName.Content, out _))
            { throw new CompilerException($"Variable \"{variableDeclaretion.VariableName.Content}\" already defined", variableDeclaretion.VariableName, CurrentFile); }

            return PrecompileVariable(Variables, variableDeclaretion.VariableName.Content, initialValue);
        }
        int PrecompileVariable(Stack<Variable> variables, string name, StatementWithValue? initialValue)
        {
            if (variables.TryFind(name, out _))
            { return 0; }

            FunctionThingDefinition? scope = (CurrentMacro.Count == 0) ? null : CurrentMacro[^1];

            if (initialValue != null)
            {
                var initialValueType = FindStatementType(initialValue);

                if (initialValueType.IsClass &&
                    initialValueType.Class.CompiledAttributes.HasAttribute("Define", "array") &&
                    initialValue is ConstructorCall constructorCall)
                {
                    if (constructorCall.Parameters.Length != 1)
                    { throw new CompilerException($"Expected 1 parameters, {constructorCall.Parameters.Length} passed", constructorCall, CurrentFile); }

                    if (!TryCompute(constructorCall.Parameters[0], out var arraySize))
                    { throw new CompilerException($"Array size have to be precompiled", constructorCall.Parameters[0], CurrentFile); }

                    if (arraySize.Type != RuntimeType.BYTE)
                    { throw new CompilerException($"Expected byte as array size (not {arraySize.Type})", constructorCall.Parameters[0], CurrentFile); }

                    int size = Snippets.ARRAY_SIZE(arraySize.ValueByte);

                    int address = Stack.PushVirtual(size);
                    variables.Push(new Variable(name, address, scope, true, initialValueType, size));
                }
                else
                {
                    int size = GetValueSize(initialValue);
                    int address = Stack.PushVirtual(size);
                    variables.Push(new Variable(name, address, scope, true, FindStatementType(initialValue), size));
                }
            }
            else
            {
                int size = 1;
                int address = Stack.PushVirtual(size);
                variables.Push(new Variable(name, address, scope, true, new CompiledType(Type.BYTE), size));
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

            if (!arrayType.IsClass)
            { throw new CompilerException($"Index getter for type \"{arrayType.Name}\" not found", indexCall, CurrentFile); }

            if (arrayType.Class.CompiledAttributes.HasAttribute("Defines", "array"))
            {
                var elementType = arrayType.TypeParameters[0];
                return elementType.SizeOnStack;
            }

            if (!GetIndexGetter(arrayType, out CompiledFunction indexer))
            {
                if (!GetIndexGetterTemplate(arrayType, out CompileableTemplate<CompiledFunction> indexerTemplate))
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
            LiteralType.STRING => statement.Value.Length,
            LiteralType.INT => 1,
            LiteralType.CHAR => 1,
            LiteralType.FLOAT => 1,
            LiteralType.BOOLEAN => 1,
            _ => throw new ImpossibleException($"Unknown literal type {statement.Type}"),
        };
        int GetValueSize(Identifier statement)
        {
            if (statement.Content == "IN")
            { return 1; }

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

            if (@class.CompiledAttributes.HasAttribute("Define", "array"))
            {
                if (constructorCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"array\" constructor: requied {1} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

                var t = FindStatementType(constructorCall.Parameters[0]);
                if (t != Type.INT)
                { throw new CompilerException($"Wrong type of parameter passed to \"array\" constructor: requied {Type.INT} passed {t}", constructorCall.Parameters[0], CurrentFile); }

                if (!TryCompute(constructorCall.Parameters[0], out DataItem value))
                { throw new CompilerException($"This must be a constant :(", constructorCall.Parameters[0], CurrentFile); }

                if (value.Type != RuntimeType.BYTE)
                { throw new CompilerException($"Something isn't right with this :(", constructorCall.Parameters[0], CurrentFile); }

                return Snippets.ARRAY_SIZE(value.ValueByte);
                // return ((byte)value) * new CompiledType(constructorCall.TypeName.GenericTypes[0], FindType).SizeOnStack;
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
                functionCall.Parameters.Length == 0)
            { return 1; }

            if (functionCall.Identifier == "AllocFrom" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 1 && (
                    FindStatementType(functionCall.Parameters[0]).BuiltinType == Type.BYTE ||
                    FindStatementType(functionCall.Parameters[0]).BuiltinType == Type.INT
                ))
            { return 1; }

            if (GetFunction(functionCall, out CompiledFunction function))
            {
                if (!function.ReturnSomething)
                { return 0; }

                return function.Type.Size;
            }

            if (GetFunctionTemplate(functionCall, out CompileableTemplate<CompiledFunction> compilableFunction))
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

            if (!variable.Type.IsClass || !variable.Type.Class.CompiledAttributes.HasAttribute("Define", "array"))
            { throw new CompilerException($"Variable is not an array", arrayIdentifier, CurrentFile); }

            address = variable.Address;
            size = variable.Size;
            return true;
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
            if (!TryCompute(pointer.PrevStatement, out var addressToSet))
            { throw new CompilerException($"Runtime pointer address in not supported", pointer.PrevStatement, CurrentFile); }

            if (addressToSet.Type != RuntimeType.BYTE)
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
            if (value is FunctionCall functionCall &&
                functionCall.Identifier == "array")
            { return; }

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
                    Optimalizations++;
                    return;
                }

                if (valueVariable.IsDiscarted)
                { throw new CompilerException($"Variable \"{valueVariable.Name}\" is discarted", _identifier, CurrentFile); }

                if (variable.Size != valueVariable.Size)
                { throw new CompilerException($"Variable and value size mistach ({variable.Size} != {valueVariable.Size})", value, CurrentFile); }

                UndiscardVariable(Variables, variable.Name);

                int tempAddress = Stack.NextAddress;

                int size = valueVariable.Size;
                for (int offset = 0; offset < size; offset++)
                {
                    int offsettedSource = valueVariable.Address + offset;
                    int offsettedTarget = variable.Address + offset;

                    Code.CopyValueWithTemp(offsettedSource, tempAddress, offsettedTarget);
                }

                Optimalizations++;

                return;
            }

            if (SafeToDiscardVariable(value, variable))
            { VariableCanBeDiscated = variable.Name; }

            using (Code.Block($"Set variable {variable.Name} (at {variable.Address}) to {value}"))
            {
                if (TryCompute(value, out var constantValue))
                {
                    if (variable.Type != constantValue.Type)
                    { throw new CompilerException($"Cannot set {constantValue.GetTypeText()} to variable of type {variable.Type}", value, CurrentFile); }

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

                    Optimalizations++;

                    VariableCanBeDiscated = null;
                    return;
                }

                int valueSize = GetValueSize(value);

                if (valueSize != variable.Size)
                { throw new CompilerException($"Variable and value size mistach ({variable.Size} != {valueSize})", value, CurrentFile); }

                using (Code.Block($"Compute value"))
                {
                    Compile(value);
                }

                using (Code.Block($"Store computed value (from {Stack.LastAddress}) to {variable.Address}"))
                { Stack.PopAndStore(variable.Address); }

                UndiscardVariable(Variables, variable.Name);

                VariableCanBeDiscated = null;
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

                Code.PRINT(checkResultAddress, "\nOut of memory range\n");

                Code.ClearValue(checkResultAddress);
                Code.JumpEnd(checkResultAddress);

                Stack.Pop();
            }

            if (GetValueSize(value) != 1)
            { throw new CompilerException($"size 1 bruh alloved on heap thingy", value, CurrentFile); }

            int valueAddress = Stack.NextAddress;
            Compile(value);

            Heap.Set(pointerAddress, valueAddress);

            Stack.PopVirtual();
            Stack.PopVirtual();

            /*
            if (!TryCompute(statement.Statement, out var addressToSet))
            { throw new CompilerException($"Runtime pointer address in not supported", statement.Statement); }

            if (addressToSet.Type != ValueType.Byte)
            { throw new CompilerException($"Address value must be a byte (not {addressToSet.Type})", statement.Statement); }

            CompileSetter((byte)addressToSet, value);
            */
        }

        void CompileSetter(int address, StatementWithValue value)
        {
            using (Code.Block($"Set value {value} to address {address}"))
            {
                if (TryCompute(value, out var constantValue))
                {
                    // if (constantValue.Size != 1)
                    // { throw new CompilerException($"Value size can be only 1", value, CurrentFile); }

                    Code.SetValue(address, constantValue.Byte ?? 0);

                    Optimalizations++;

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
            { throw new CompilerException($"This must be an identifier", statement.PrevStatement, CurrentFile); }

            if (!Variables.TryFind(_variableIdentifier.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{_variableIdentifier}\" not found", _variableIdentifier, CurrentFile); }

            if (variable.IsDiscarted)
            { throw new CompilerException($"Variable \"{variable.Name}\" is discarted", _variableIdentifier, CurrentFile); }

            using (Code.Block($"Set array (variable {variable.Name}) index ({statement.Expression}) (at {variable.Address}) to {value}"))
            {
                if (variable.Type.IsClass && variable.Type.Class.CompiledAttributes.HasAttribute("Define", "array"))
                {
                    CompiledType elementType = variable.Type.TypeParameters[0];
                    CompiledType valueType = FindStatementType(value);

                    if (elementType != valueType)
                    { throw new CompilerException("AAAAAAAAAAAaaafasfsdfsd", value, CurrentFile); }

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

                    return;
                }
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
            else if (statement is VariableDeclaretion variableDeclaretion)
            { Compile(variableDeclaretion); }
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

            int end = Code.GetFinalCode().Length;
            DebugInfo.Add(new DebugInfo()
            {
                InstructionStart = start,
                InstructionEnd = end,
                Position = statement.GetPosition(),
            });
        }
        void Compile(IndexCall indexCall)
        {
            CompiledType arrayType = FindStatementType(indexCall.PrevStatement);

            if (!arrayType.IsClass)
            { throw new CompilerException($"Index getter for type \"{arrayType.Name}\" not found", indexCall, CurrentFile); }

            if (arrayType.Class.CompiledAttributes.HasAttribute("Define", "array"))
            {
                if (!TryGetAddress(indexCall.PrevStatement, out int arrayAddress, out _))
                { throw new CompilerException($"Failed to get array address", indexCall.PrevStatement, CurrentFile); }

                CompiledType elementType = arrayType.TypeParameters[0];

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
        void Compile(LinkedIf @if)
        {
            using (Code.Block($"If ({@if.Condition})"))
            {
                int conditionAddress = Stack.NextAddress;
                using (Code.Block("Compute condition"))
                { Compile(@if.Condition); }

                Code.CommentLine($"Condition result at {conditionAddress}");

                Code.CommentLine($"Pointer: {Code.Pointer}");

                Code.JumpStart(conditionAddress);

                using (Code.Block("The if statements"))
                {
                    Compile(@if.Block);
                }

                Code.CommentLine($"Pointer: {Code.Pointer}");

                if (@if.NextLink == null)
                {
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
                        using (Code.Block("Finish if statement"))
                        {
                            Code.MoveValue(conditionAddress, conditionAddress + 1);
                            Code.JumpEnd(conditionAddress);
                            Stack.PopVirtual();
                        }

                        Code.MoveValue(conditionAddress + 1, conditionAddress);

                        using (Code.Block($"Invert condition (at {conditionAddress}) result (to {conditionAddress + 1})"))
                        { Code.LOGIC_NOT(conditionAddress + 1, conditionAddress + 2); }

                        int elseFlagAddress = conditionAddress + 1;

                        Code.CommentLine($"Pointer: {Code.Pointer}");

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

                        Code.CommentLine($"Pointer: {Code.Pointer}");

                        using (Code.Block($"If ELSE flag set (previous \"if\" condition is false)"))
                        {
                            Code.JumpStart(elseFlagAddress);

                            using (Code.Block("Reset ELSE flag"))
                            { Code.ClearValue(elseFlagAddress); }

                            if (@if.NextLink is LinkedElse elseBlock)
                            {
                                using (Code.Block("Block (else)"))
                                { Compile(elseBlock.Block); }
                            }
                            else if (@if.NextLink is LinkedIf elseIf)
                            {
                                using (Code.Block("Block (else if)"))
                                { Compile(elseIf); }
                            }
                            else
                            { throw new System.Exception(); }

                            using (Code.Block($"Reset ELSE flag"))
                            { Code.ClearValue(elseFlagAddress); }

                            Code.JumpEnd(elseFlagAddress);
                        }

                        Code.CommentLine($"Pointer: {Code.Pointer}");
                    }
                }

                ContinueReturnStatements();
                ContinueBreakStatements();

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
                { throw new System.Exception(); }
                Stack.Pop();

                Stack.Pop();

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
                        if (statement.Parameters.Length != 0 &&
                            statement.Parameters.Length != 1)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"return\" (requied 0 or 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        if (statement.Parameters.Length == 1)
                        {
                            // if (GetValueSize(statement.Parameters[0]) != 1)
                            // { throw new CompilerException($"Return value can be only 1 byte", statement.Parameters[0], CurrentFile); }

                            if (!Variables.TryFind("@return", out Variable returnVariable))
                            { throw new CompilerException($"Can't return value for some reason :(", statement, CurrentFile); }

                            CompileSetter(returnVariable, statement.Parameters[0]);
                        }

                        Warnings.Add(new Warning($"This kind of control flow (return and break) is not fully tested. Expect a buggy behaviour!", statement.Identifier, CurrentFile));

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
                        if (statement.Parameters.Length != 0)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"break\" (requied 0, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        if (BreakTagStack.Count <= 0)
                        { throw new CompilerException($"Looks like this \"break\" statement is not inside a loop. Am i wrong? Of course not! Haha", statement.Identifier, CurrentFile); }

                        Warnings.Add(new Warning($"This kind of control flow (return and break) is not fully tested. Expect a buggy behaviour!", statement.Identifier, CurrentFile));

                        Code.SetValue(BreakTagStack[^1], 0);

                        Code.SetPointer(Stack.NextAddress);
                        Code.ClearCurrent();
                        Code.JumpStart(Stack.NextAddress);
                        BreakCount[^1]++;

                        break;
                    }

                case "var":
                    {
                        if (statement.Parameters[0] is Identifier variableIdentifier && statement.Parameters.Length > 1)
                        {
                            CompileSetter(variableIdentifier, statement.Parameters[1]);
                        }
                        return;
                    }
                case "const":
                    return;

                case "outraw":
                    {
                        if (statement.Parameters.Length <= 0)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"outraw\" (requied minimum 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        foreach (var value in statement.Parameters)
                        {
                            if (TryCompute(value, out DataItem constantValue))
                            {
                                if (constantValue.Type == RuntimeType.BYTE)
                                {
                                    using (Code.Block($"Print value {constantValue.ValueByte}"))
                                    {
                                        Code.SetPointer(Stack.NextAddress);
                                        Code.ClearCurrent();
                                        Code.AddValue(constantValue.ValueByte);

                                        Code += ".";

                                        Code.ClearCurrent();
                                        Code.SetPointer(0);
                                    }
                                    continue;
                                }

                                if (constantValue.Type == RuntimeType.INT)
                                {
                                    using (Code.Block($"Print value {constantValue.ValueInt}"))
                                    {
                                        Code.SetPointer(Stack.NextAddress);
                                        Code.ClearCurrent();
                                        Code.AddValue(constantValue.ValueInt);

                                        Code += ".";

                                        Code.ClearCurrent();
                                        Code.SetPointer(0);
                                    }
                                    continue;
                                }

                                if (constantValue.Type == RuntimeType.CHAR)
                                {
                                    using (Code.Block($"Print value '{constantValue.ValueChar}'"))
                                    {
                                        Code.SetPointer(Stack.NextAddress);
                                        Code.ClearCurrent();
                                        Code.AddValue(constantValue.ValueChar);

                                        Code += ".";

                                        Code.ClearCurrent();
                                        Code.SetPointer(0);
                                    }
                                    continue;
                                }

                                /*
                                if (constantValue.Type == ValueType.String)
                                {
                                    using (Code.Block($"Print value \"{(string)constantValue}\""))
                                    {
                                        Code.ClearValue(Stack.NextAddress);

                                        string valueToPrint = (string)constantValue;
                                        byte prevValue = 0;
                                        for (int i = 0; i < valueToPrint.Length; i++)
                                        {
                                            Code.SetPointer(Stack.NextAddress);
                                            byte charToPrint = CharCode.GetByte(valueToPrint[i]);

                                            while (prevValue > charToPrint)
                                            {
                                                Code += "-";
                                                prevValue--;
                                            }

                                            while (prevValue < charToPrint)
                                            {
                                                Code += "+";
                                                prevValue++;
                                            }

                                            prevValue = charToPrint;

                                            Code += ".";
                                        }

                                        Code.ClearCurrent();
                                        Code.SetPointer(0);
                                    }
                                    continue;
                                }
                                */

                                throw new CompilerException($"Value failed to compile", value, CurrentFile);
                            }

                            if (value is Identifier identifier && Variables.TryFind(identifier.Content, out Variable variable))
                            {
                                if (variable.IsDiscarted)
                                { throw new CompilerException($"Variable \"{variable.Name}\" is discarted", identifier, CurrentFile); }

                                using (Code.Block($"Print variable (\"{variable.Name}\") (from {variable.Address}) value"))
                                {
                                    int size = variable.Size;
                                    for (int offset = 0; offset < size; offset++)
                                    {
                                        int offsettedAddress = variable.Address + offset;
                                        Code.SetPointer(offsettedAddress);
                                        Code += ".";
                                    }
                                    Code.SetPointer(0);
                                }
                                continue;
                            }

                            using (Code.Block($"Print {value}"))
                            {
                                using (Code.Block($"Compute value"))
                                {
                                    Compile(value);
                                }

                                using (Code.Block($"Print computed value"))
                                {
                                    Stack.Pop(address =>
                                    {
                                        Code.SetPointer(address);
                                        Code += ".";
                                        Code.ClearCurrent();
                                    });
                                    Code.SetPointer(0);
                                }
                            }
                        }
                        break;
                    }
                case "out":
                    {
                        if (statement.Parameters.Length <= 0)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"out\" (requied minimum 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        foreach (StatementWithValue valueToPrint in statement.Parameters)
                        {
                            if (TryCompute(valueToPrint, out DataItem constantToPrint))
                            {
                                if (constantToPrint.Type == RuntimeType.CHAR)
                                {
                                    int tempAddress = Stack.NextAddress;
                                    using (Code.Block($"Print character '{constantToPrint.ValueChar}' (on address {tempAddress})"))
                                    {
                                        Code.SetValue(tempAddress, constantToPrint.ValueChar);
                                        Code.SetPointer(tempAddress);
                                        Code += ".";
                                        Code.ClearValue(tempAddress);
                                        Code.SetPointer(0);
                                    }
                                    continue;
                                }

                                if (constantToPrint.Type == RuntimeType.BYTE)
                                {
                                    int tempAddress = Stack.NextAddress;
                                    using (Code.Block($"Print number {constantToPrint.ValueByte} as text (on address {tempAddress})"))
                                    {
                                        Code.SetValue(tempAddress, constantToPrint.ValueByte);
                                        Code.SetPointer(tempAddress);

                                        using (Code.Block($"SNIPPET OUT_AS_STRING"))
                                        { Code.Code += Snippets.OUT_AS_STRING; }
                                        Code.ClearValue(tempAddress);
                                        Code.SetPointer(0);
                                    }
                                    continue;
                                }

                                if (constantToPrint.Type == RuntimeType.INT)
                                {
                                    int tempAddress = Stack.NextAddress;
                                    using (Code.Block($"Print number {constantToPrint.ValueInt} as text (on address {tempAddress})"))
                                    {
                                        Code.SetValue(tempAddress, constantToPrint.ValueInt);
                                        Code.SetPointer(tempAddress);

                                        using (Code.Block($"SNIPPET OUT_AS_STRING"))
                                        { Code.Code += Snippets.OUT_AS_STRING; }
                                        Code.ClearValue(tempAddress);
                                        Code.SetPointer(0);
                                    }
                                    continue;
                                }

                                /*
                                if (constantToPrint.Type == ValueType.String)
                                {
                                    string v = constantToPrint;
                                    int tempAddress = Stack.NextAddress;
                                    using (Code.Block($"Print string \"{v}\" (on address {tempAddress})"))
                                    {
                                        Code.ClearValue(tempAddress);

                                        byte prevValue = 0;
                                        for (int i = 0; i < v.Length; i++)
                                        {
                                            byte charToPrint = CharCode.GetByte(v[i]);

                                            while (prevValue > charToPrint)
                                            {
                                                Code += "-";
                                                prevValue--;
                                            }

                                            while (prevValue < charToPrint)
                                            {
                                                Code += "+";
                                                prevValue++;
                                            }

                                            prevValue = charToPrint;

                                            Code += ".";
                                        }

                                        Code.ClearValue(tempAddress);
                                        Code.SetPointer(0);
                                    }
                                    continue;
                                }
                                */

                                throw new System.Exception();
                            }

                            var valueType = FindStatementType(valueToPrint);

                            if (valueType.SizeOnStack != 1)
                            { throw new CompilerException($"The \"{statement.Identifier.Content.ToLower()}\" instruction only accepts value of size 1 (not {valueType.SizeOnStack})", valueToPrint, CurrentFile); }

                            if (!valueType.IsBuiltin)
                            { throw new CompilerException($"The \"{statement.Identifier.Content.ToLower()}\" instruction only accepts value of a builtin type (not {valueType})", valueToPrint, CurrentFile); }

                            using (Code.Block($"Print value {valueToPrint} as text"))
                            {
                                int address = Stack.NextAddress;

                                using (Code.Block($"Compute value"))
                                {
                                    Compile(valueToPrint);
                                }

                                Code.CommentLine($"Computed value is on {address}");

                                Code.SetPointer(address);

                                switch (valueType.BuiltinType)
                                {
                                    case Type.BYTE:
                                        using (Code.Block($"SNIPPET OUT_AS_STRING"))
                                        { Code.Code += Snippets.OUT_AS_STRING; }
                                        break;
                                    case Type.INT:
                                        using (Code.Block($"SNIPPET OUT_AS_STRING"))
                                        { Code.Code += Snippets.OUT_AS_STRING; }
                                        break;
                                    case Type.FLOAT:
                                        using (Code.Block($"SNIPPET OUT_AS_STRING"))
                                        { Code.Code += Snippets.OUT_AS_STRING; }
                                        break;
                                    case Type.CHAR:
                                        Code.Code += ".";
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
                        break;
                    }

                case "delete":
                    {
                        if (statement.Parameters.Length != 1)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"delete\" (requied 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

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

                        throw new CompilerException($"Bruh. This propably not stored in heap...", deletable, CurrentFile);
                    }

                default: throw new CompilerException($"Unknown instruction command \"{statement.Identifier}\"", statement.Identifier, CurrentFile);
            }
        }
        void Compile(Assignment statement)
        {
            if (statement.Operator.Content != "=")
            { throw new CompilerException($"Unknown assignment operator \'{statement.Operator}\'", statement.Operator, CurrentFile); }

            CompileSetter(statement.Left, statement.Right ?? throw new CompilerException($"Value is requied for \'{statement.Operator}\' assignment", statement, CurrentFile));
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

                        if (variable.IsDiscarted)
                        { throw new CompilerException($"Variable \"{variable.Name}\" is discarted", variableIdentifier, CurrentFile); }

                        if (variable.Size != 1)
                        { throw new CompilerException($"Bruhhh", statement.Left, CurrentFile); }

                        if (statement.Right == null)
                        { throw new CompilerException($"Value is requied for '{statement.Operator}' assignment", statement, CurrentFile); }

                        if (TryCompute(statement.Right, out var constantValue))
                        {
                            if (variable.Type != constantValue.Type)
                            { throw new CompilerException($"Variable and value type mistach ({variable.Type} != {constantValue.GetTypeText()})", statement.Right, CurrentFile); }

                            switch (constantValue.Type)
                            {
                                case RuntimeType.BYTE:
                                    Code.AddValue(variable.Address, constantValue.ValueByte);
                                    break;
                                case RuntimeType.INT:
                                    Code.AddValue(variable.Address, constantValue.ValueInt);
                                    break;
                                case RuntimeType.FLOAT:
                                    throw new CompilerException($"Floats not supported by brainfuck :(", statement.Right, CurrentFile);
                                case RuntimeType.CHAR:
                                    Code.AddValue(variable.Address, constantValue.ValueChar);
                                    break;
                                default:
                                    throw new ImpossibleException();
                            }

                            Optimalizations++;
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

                        if (variable.IsDiscarted)
                        { throw new CompilerException($"Variable \"{variable.Name}\" is discarted", variableIdentifier, CurrentFile); }

                        if (variable.Size != 1)
                        { throw new CompilerException($"Bruhhh", variableIdentifier, CurrentFile); }

                        if (statement.Right == null)
                        { throw new CompilerException($"Value is requied for '{statement.Operator}' assignment", statement, CurrentFile); }

                        if (TryCompute(statement.Right, out var constantValue))
                        {
                            if (variable.Type != constantValue.Type)
                            { throw new CompilerException($"Variable and value type mistach ({variable.Type} != {constantValue.GetTypeText()})", statement.Right, CurrentFile); }

                            switch (constantValue.Type)
                            {
                                case RuntimeType.BYTE:
                                    Code.AddValue(variable.Address, -constantValue.ValueByte);
                                    break;
                                case RuntimeType.INT:
                                    Code.AddValue(variable.Address, -constantValue.ValueInt);
                                    break;
                                case RuntimeType.FLOAT:
                                    throw new CompilerException($"Floats not supported by brainfuck :(", statement.Right, CurrentFile);
                                case RuntimeType.CHAR:
                                    Code.AddValue(variable.Address, -constantValue.ValueChar);
                                    break;
                                default:
                                    throw new ImpossibleException();
                            }

                            Optimalizations++;
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

                        if (variable.IsDiscarted)
                        { throw new CompilerException($"Variable \"{variable.Name}\" is discarted", variableIdentifier, CurrentFile); }

                        if (variable.Size != 1)
                        { throw new CompilerException($"Bruhhh", statement.Left, CurrentFile); }

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

                        if (variable.IsDiscarted)
                        { throw new CompilerException($"Variable \"{variable.Name}\" is discarted", variableIdentifier, CurrentFile); }

                        if (variable.Size != 1)
                        { throw new CompilerException($"Bruhhh", statement.Left, CurrentFile); }

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
        void Compile(VariableDeclaretion statement)
        {
            if (statement.InitialValue == null) return;

            if (!Variables.TryFind(statement.VariableName.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{statement.VariableName.Content}\" not found", statement.VariableName, CurrentFile); }

            CompileSetter(variable, statement.InitialValue);
        }
        void Compile(FunctionCall functionCall)
        {
            if (false &&
                functionCall.Identifier == "Alloc" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 0)
            {
                int resultAddress = Stack.PushVirtual(1);
                // Heap.Allocate(resultAddress);

                return;
            }

            if (false &&
                functionCall.Identifier == "AllocFrom" &&
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

                return;
            }

            if (!GetFunction(functionCall, out CompiledFunction compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompileableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compilableFunction.Function;
            }

            // if (!function.Modifiers.Contains("macro"))
            // { throw new CompilerException($"Functions not supported by the brainfuck compiler, try using macros instead", functionCall, CurrentFile); }

            InlineMacro(compiledFunction, functionCall.Parameters, functionCall);
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

            if (@class.CompiledAttributes.HasAttribute("Define", "array"))
            {
                if (constructorCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"array\" constructor: requied {1} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

                var t = FindStatementType(constructorCall.Parameters[0]);
                if (t != Type.INT)
                { throw new CompilerException($"Wrong type of parameter passed to \"array\" constructor: requied {Type.INT} passed {t}", constructorCall.Parameters[0], CurrentFile); }

                if (!TryCompute(constructorCall.Parameters[0], out DataItem value))
                { throw new CompilerException($"This must be a constant :(", constructorCall.Parameters[0], CurrentFile); }

                if (value.Type != RuntimeType.BYTE)
                { throw new CompilerException($"Something isn't right with this :(", constructorCall.Parameters[0], CurrentFile); }

                CompiledType arrayElementType = new(constructorCall.TypeName.GenericTypes[0], FindType);
                if (arrayElementType == Type.INT)
                { Warnings.Add(new Warning($"Integers are not supported by brainfuck so I will threat this as a byte", constructorCall.TypeName.GenericTypes[0], CurrentFile)); }

                Stack.PushVirtual(Snippets.ARRAY_SIZE(value.ValueByte));
                // Stack.PushVirtual(((byte)value) * new CompiledType(constructorCall.TypeName.GenericTypes[0], FindType).SizeOnStack);
                return;
            }

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
            { throw new CompilerException($"Wrong number of parameters passed to \"{constructorCall.TypeName}\" constructor: requied {constructor.ParameterCount} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

            InlineMacro(constructor, constructorCall.Parameters, constructorCall);
        }
        void Compile(Literal statement)
        {
            using (Code.Block($"Set address {Stack.NextAddress} to literal {statement}"))
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
                        throw new CompilerException($"Floats not supported by the brainfuck compiler", statement, CurrentFile);
                    case LiteralType.STRING:
                        throw new CompilerException($":(", statement, CurrentFile);

                    default:
                        throw new CompilerException($"Unknown literal type {statement.Type}", statement, CurrentFile);
                }
            }
        }
        void Compile(Identifier statement)
        {
            if (statement.Content == "IN")
            {
                int address = Stack.PushVirtual(1);
                Code.MovePointer(address);
                Code += ",";
                Code.MovePointer(0);

                return;
            }

            if (Variables.TryFind(statement.Content, out Variable variable))
            {
                if (!variable.IsInitialized)
                { throw new CompilerException($"Variable \"{variable.Name}\" not initialized", statement, CurrentFile); }

                if (variable.IsDiscarted)
                { throw new CompilerException($"Variable \"{variable.Name}\" is discarted", statement, CurrentFile); }

                using (Code.Block($"Load variable {variable.Name} (from {variable.Address})"))
                {
                    int variableSize = variable.Size;

                    if (variableSize <= 0)
                    { throw new CompilerException($"Can't load variable \"{variable.Name}\" becouse it's size is {variableSize} (bruh)", statement, CurrentFile); }

                    int loadTarget = Stack.PushVirtual(variableSize);

                    for (int offset = 0; offset < variableSize; offset++)
                    {
                        int offsettedSource = variable.Address + offset;
                        int offsettedTarget = loadTarget + offset;

                        if (VariableCanBeDiscated != null && VariableCanBeDiscated == variable.Name)
                        {
                            Code.MoveValue(offsettedSource, offsettedTarget);
                            DiscardVariable(Variables, variable.Name);
                            Optimalizations++;
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
                                    !left.IsDiscarted &&
                                    TryCompute(statement.Right, out var right) &&
                                    right.Type == RuntimeType.BYTE)
                                {
                                    int resultAddress = Stack.PushVirtual(1);

                                    Code.CopyValueWithTemp(left.Address, Stack.NextAddress, resultAddress);

                                    Code.AddValue(resultAddress, -right.ValueByte);

                                    Optimalizations++;

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
                                Code.DIVIDE(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2, rightAddress + 3, rightAddress + 4);
                            }

                            Stack.Pop();

                            break;
                        }
                    case "^":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { Compile(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { Compile(statement.Right); }

                            using (Code.Block($"Snippet POWER({leftAddress} {rightAddress})"))
                            {
                                Code.POWER(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2, rightAddress + 3);
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
                                Code.MOD(leftAddress, rightAddress, rightAddress + 1);
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
            VariableCleanupStack.Push(PrecompileVariables(block));

            if (ReturnTagStack.Count > 0)
            { ReturnCount.Push(0); }

            if (BreakTagStack.Count > 0)
            { BreakCount.Push(0); }

            foreach (Statement statement in block.Statements)
            {
                VariableCanBeDiscated = null;
                Compile(statement);
                VariableCanBeDiscated = null;
            }

            if (ReturnTagStack.Count > 0)
            { FinishReturnStatements(); }

            if (BreakTagStack.Count > 0)
            { FinishBreakStatements(); }

            CleanupVariables(VariableCleanupStack.Pop());
        }
        void Compile(AddressGetter addressGetter)
        {
            throw new NotImplementedException();

            /*
            if (addressGetter.Statement is Identifier identifier)
            {
                if (!Variables.TryFind(identifier.Value.Content, out Variable variable))
                { throw new CompilerException($"Variable \"{identifier}\" not found", identifier); }

                if (variable.IsDiscarted)
                { throw new CompilerException($"Variable \"{variable.Name}\" is discarted", identifier); }

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

            throw new CompilerException($"Runtime pointer address not supported", pointer.Statement);
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
                    { throw new CompilerException($"Not supported :(", field.Identifier, instanceType.Struct.FilePath); }

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
                            throw new CompilerException($"Floats not supported by the brainfuck compiler", field.Identifier, instanceType.Struct.FilePath);
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
                throw new CompilerException($"Not supported :(", newInstance, CurrentFile);
                /*
                newInstance.TypeName = newInstance.TypeName.Class(@class);
                @class.References?.Add(new DefinitionReference(newInstance.TypeName, CurrentFile));

                int pointerAddress = Stack.PushVirtual(1);

                {
                    int requiedSizeAddress = Stack.Push(@class.Size);
                    int tempAddressesStart = Stack.PushVirtual(1);

                    using (Code.Block($"Allocate (size: {@class.Size} (at {requiedSizeAddress}) result at: {pointerAddress})"))
                    {
                        Heap.Allocate(pointerAddress, requiedSizeAddress, tempAddressesStart);

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
                    { throw new CompilerException($"Can't load field \"{field}\" becouse it's size is {size} (bruh)", field, CurrentFile); }

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

        /// <param name="callerPosition">
        /// Used for exceptions
        /// </param>
        void InlineMacro(CompiledFunction function, StatementWithValue[] parameters, IThingWithPosition callerPosition)
        {
            if (function.CompiledAttributes.HasAttribute("StandardOutput"))
            {
                Compile(new KeywordCall(
                    new Token(TokenType.IDENTIFIER, "out", true)
                    {
                        AbsolutePosition = callerPosition.GetPosition().AbsolutePosition,
                        Position = callerPosition.GetPosition().Range,
                    },
                    parameters));
                return;
            }

            // if (!function.Modifiers.Contains("macro"))
            // { throw new CompilerException($"Functions not supported by the brainfuck compiler, try using macros instead", callerPosition, CurrentFile); }

            for (int i = 0; i < CurrentMacro.Count; i++)
            {
                if (CurrentMacro[i] == function)
                { throw new CompilerException($"Recursive macro inlining is not alloved (The macro \"{function.Identifier}\" used recursively)", callerPosition, CurrentFile); }
            }

            if (function.Parameters.Length != parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to macro \"{function.Identifier}\" (requied {function.Parameters.Length} passed {parameters.Length})", callerPosition, CurrentFile); }

            Variable? returnVariable = null;

            if (function.ReturnSomething)
            {
                var returnType = function.Type;
                returnVariable = new Variable("@return", Stack.PushVirtual(returnType.Size), function, false, returnType, returnType.Size);
            }

            Stack<Variable> compiledParameters = new();
            List<ConstantVariable> constantParameters = new();

            this.CurrentMacro.Push(function);

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
                                if (!TryCompute(valueStatement, out DataItem value))
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

                    PrecompileVariable(compiledParameters, defined.Identifier.Content, value);

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
                { throw new System.Exception(); }
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

            this.CurrentMacro.Pop();

            Variables.Clear();
            for (int i = 0; i < savedVariables.Length; i++)
            { Variables.Push(savedVariables[i]); }

            Constants.Clear();
            for (int i = 0; i < savedConstants.Length; i++)
            { Constants.Add(savedConstants[i]); }

            if (BreakCount.Count > 0 ||
                BreakTagStack.Count > 0)
            { throw new System.Exception(); }

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
            // { throw new CompilerException($"Functions not supported by the brainfuck compiler, try using macros instead", callerPosition, CurrentFile); }

            for (int i = 0; i < CurrentMacro.Count; i++)
            {
                if (CurrentMacro[i].Identifier.Content == function.Identifier.Content)
                { throw new CompilerException($"Recursive macro inlining is not alloved (The macro \"{function.Identifier}\" used recursively)", callerPosition, CurrentFile); }
            }

            if (function.Parameters.Length != parameters.Length)
            { throw new CompilerException($"Wrong number of parameters passed to macro \"{function.Identifier}\" (equied {function.Parameters.Length} passed {parameters.Length})", callerPosition, CurrentFile); }

            Variable? returnVariable = null;

            if (function.ReturnSomething)
            {
                CompiledType returnType = function.Type;
                returnVariable = new Variable("@return", Stack.PushVirtual(returnType.Size), function, false, returnType, returnType.Size);
            }

            Stack<Variable> compiledParameters = new();
            List<ConstantVariable> constantParameters = new();

            this.CurrentMacro.Push(function);

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
                                if (!TryCompute(valueStatement, out DataItem value))
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

                    PrecompileVariable(compiledParameters, defined.Identifier.Content, value);

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
                { throw new System.Exception(); }
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

            this.CurrentMacro.Pop();

            Variables.Clear();
            for (int i = 0; i < savedVariables.Length; i++)
            { Variables.Push(savedVariables[i]); }

            Constants.Clear();
            for (int i = 0; i < savedConstants.Length; i++)
            { Constants.Add(savedConstants[i]); }

            if (BreakCount.Count > 0 ||
                BreakTagStack.Count > 0)
            { throw new System.Exception(); }

            BreakCount.Clear();
            for (int i = 0; i < savedBreakCount.Length; i++)
            { BreakCount.Push(savedBreakCount[i]); }

            BreakTagStack.Clear();
            for (int i = 0; i < savedBreakTagStack.Length; i++)
            { BreakTagStack.Push(savedBreakTagStack[i]); }
        }

        void FinishReturnStatements()
        {
            int accumlatedReturnCount = ReturnCount.Pop();
            using (Code.Block($"Finish {accumlatedReturnCount} \"return\" statements"))
            {
                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.CommentLine($"Pointer: {Code.Pointer}");
                for (int i = 0; i < accumlatedReturnCount; i++)
                {
                    Code += "]";
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
            int accumlatedBreakCount = BreakCount.Pop();
            using (Code.Block($"Finish {accumlatedBreakCount} \"break\" statements"))
            {
                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.CommentLine($"Pointer: {Code.Pointer}");
                for (int i = 0; i < accumlatedBreakCount; i++)
                {
                    Code += "]";
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
            Output.PrintCallback? printCallback = null)
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

            {
                FinishReturnStatements();
                if (ReturnTagStack.Pop() != Stack.LastAddress)
                { throw new System.Exception(); }
                Stack.Pop();

                if (ReturnCount.Count > 0 ||
                    ReturnTagStack.Count > 0 ||
                    BreakCount.Count > 0 ||
                    BreakTagStack.Count > 0)
                { throw new System.Exception(); }
            }

            if (GeneratorSettings.ClearGlobalVariablesBeforeExit)
            { CleanupVariables(VariableCleanupStack.Pop()); }

            // Heap.Destroy();

            Code.SetPointer(0);

            return new Result()
            {
                Code = Code.ToString(),
                Optimalizations = Optimalizations,
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
            Output.PrintCallback? printCallback = null)
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