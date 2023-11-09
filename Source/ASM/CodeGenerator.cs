using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0060 // Remove unused parameter

namespace LanguageCore.ASM.Compiler
{
    using BBCode.Compiler;
    using LanguageCore.Parser;
    using LanguageCore.Parser.Statement;
    using LanguageCore.Runtime;
    using LanguageCore.Tokenizing;
    using LiteralStatement = Parser.Statement.Literal;

    public class ImportedAsmFunction
    {
        public readonly string Name;
        public readonly int ParameterSizeBytes;

        public ImportedAsmFunction(string name, int parameterSizeBytes)
        {
            Name = name;
            ParameterSizeBytes = parameterSizeBytes;
        }

        public override string ToString() => $"_{Name}@{ParameterSizeBytes}";
    }

    public class CodeGenerator : BBCode.Compiler.CodeGenerator
    {
        #region Fields

        readonly Settings GeneratorSettings;
        readonly AssemblyCode Builder;

        #endregion

        public CodeGenerator(Compiler.Result compilerResult, Settings settings) : base()
        {
            this.GeneratorSettings = settings;
            this.CompiledFunctions = compilerResult.Functions;
            this.CompiledOperators = compilerResult.Operators;
            this.CompiledClasses = compilerResult.Classes;
            this.CompiledStructs = compilerResult.Structs;
            this.CompiledEnums = compilerResult.Enums;
            this.CompiledMacros = compilerResult.Macros;
            this.Builder = new AssemblyCode();
        }

        public struct Result
        {
            public Token[] Tokens;

            public Warning[] Warnings;
            public Error[] Errors;

            public string AssemblyCode;
        }

        public struct Settings
        {

        }

        #region Memory Helpers

        protected override void StackLoad(ValueAddress address)
        {
            if (address.IsReference)
            { throw new NotImplementedException(); }

            if (address.InHeap)
            { throw new NotImplementedException(); }

            switch (address.AddressingMode)
            {
                case AddressingMode.ABSOLUTE:
                case AddressingMode.BASEPOINTER_RELATIVE:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, $"DWORD[{Registers.BasePointer}-{(address.Address + 1) * 4}]");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;

                case AddressingMode.RELATIVE:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, $"DWORD[{Registers.StackPointer}+{(address.Address + 1) * 4}]");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    throw new NotImplementedException();

                case AddressingMode.POP:
                    throw new NotImplementedException();

                case AddressingMode.RUNTIME:
                    throw new NotImplementedException();
                default: throw new ImpossibleException();
            }
        }
        protected override void StackStore(ValueAddress address)
        {
            if (address.IsReference)
            { throw new NotImplementedException(); }

            if (address.InHeap)
            { throw new NotImplementedException(); }

            throw new NotImplementedException();
            /*
            switch (address.AddressingMode)
            {
                case AddressingMode.ABSOLUTE:
                    Builder.CodeBuilder.AppendInstruction(Opcode.STORE_VALUE, AddressingMode.ABSOLUTE, address.Address);
                    break;
                case AddressingMode.BASEPOINTER_RELATIVE:
                    Builder.CodeBuilder.AppendInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, address.Address);
                    break;
                case AddressingMode.RELATIVE:
                    Builder.CodeBuilder.AppendInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, address.Address);
                    break;
                case AddressingMode.POP:
                    Builder.CodeBuilder.AppendInstruction(Opcode.STORE_VALUE, AddressingMode.POP);
                    break;
                case AddressingMode.RUNTIME:
                    throw new NotImplementedException();
                default: throw new ImpossibleException();
            }
            */
        }

        #endregion

        #region Addressing Helpers

        int ParametersSize
        {
            get
            {
                int sum = 0;

                for (int i = 0; i < CompiledParameters.Count; i++)
                {
                    sum += CompiledParameters[i].Type.SizeOnStack;
                }

                return sum;
            }
        }
        int ParametersSizeBefore(int beforeThis)
        {
            int sum = 0;

            for (int i = 0; i < CompiledParameters.Count; i++)
            {
                if (CompiledParameters[i].Index < beforeThis) continue;

                sum += CompiledParameters[i].Type.SizeOnStack;
            }

            return sum;
        }

        public int ReturnValueOffset => -(ParametersSize + 1);

        protected override ValueAddress GetBaseAddress(CompiledParameter parameter)
        {
            int address = -(ParametersSizeBefore(parameter.Index));
            return new ValueAddress(parameter, address);
        }
        protected override ValueAddress GetBaseAddress(CompiledParameter parameter, int offset)
        {
            int address = -(ParametersSizeBefore(parameter.Index) - offset);
            return new ValueAddress(parameter, address);
        }

        #endregion

        #region GenerateInitialValue
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="CompilerException"/>
        /// <exception cref="InternalException"/>
        int GenerateInitialValue(TypeInstance type)
        {
            if (type is TypeInstanceFunction)
            {
                throw new NotImplementedException();
            }

            if (type is TypeInstanceStackArray)
            {
                throw new NotImplementedException();
            }

            if (type is TypeInstanceSimple simpleType)
            {
                if (LanguageConstants.BuiltinTypeMap3.TryGetValue(simpleType.Identifier.Content, out Type builtinType))
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, GetInitialValue(builtinType));
                    return 1;
                }

                CompiledType instanceType = FindType(simpleType);

                if (instanceType.IsStruct)
                {
                    int size = 0;
                    foreach (FieldDefinition field in instanceType.Struct.Fields)
                    {
                        size++;
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, GetInitialValue(field.Type));
                    }
                    throw new NotImplementedException();
                }

                if (instanceType.IsClass)
                {
                    throw new NotImplementedException();
                }

                if (instanceType.IsEnum)
                {
                    if (instanceType.Enum.Members.Length == 0)
                    { throw new CompilerException($"Could not get enum \"{instanceType.Enum.Identifier.Content}\" initial value: enum has no members", instanceType.Enum.Identifier, instanceType.Enum.FilePath); }

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, instanceType.Enum.Members[0].ComputedValue);
                    return 1;
                }

                if (instanceType.IsFunction)
                {
                    throw new NotImplementedException();
                }

                throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", simpleType, CurrentFile);
            }

            throw new ImpossibleException();
        }
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="CompilerException"/>
        /// <exception cref="InternalException"/>
        int GenerateInitialValue(CompiledType type)
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size += GenerateInitialValue(field.Type);
                }
                throw new NotImplementedException();
            }

            if (type.IsClass)
            {
                throw new NotImplementedException();
            }

            if (type.IsStackArray)
            {
                int stackSize = type.StackArraySize;

                int size = 0;
                for (int i = 0; i < stackSize; i++)
                {
                    size += GenerateInitialValue(type.StackArrayOf);
                }
                throw new NotImplementedException();
            }

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, GetInitialValue(type));
            return 1;
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, Action<int> afterValue)
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size++;
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, GetInitialValue(field.Type));
                    afterValue?.Invoke(size);
                }
                throw new NotImplementedException();
            }

            if (type.IsClass)
            {
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0.ToString());
                afterValue?.Invoke(0);
                throw new NotImplementedException();
            }

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, GetInitialValue(type));
            afterValue?.Invoke(0);
            return 1;
        }

        #endregion

        #region GenerateCodeForVariable
        int VariablesSize
        {
            get
            {
                int sum = 0;

                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    CompiledVariable variable = CompiledVariables[i];
                    sum += variable.Type.SizeOnStack;
                }

                return sum;
            }
        }

        int LocalVariablesSize
        {
            get
            {
                int sum = 0;

                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    CompiledVariable variable = CompiledVariables[i];

                    if (variable.IsGlobal) continue;

                    sum += variable.Type.SizeOnStack;
                }

                return sum;
            }
        }

        CleanupItem GenerateCodeForVariable(VariableDeclaration newVariable, bool isGlobal)
        {
            if (newVariable.Modifiers.Contains("const")) return CleanupItem.Null;

            newVariable.VariableName.AnalyzedType = TokenAnalyzedType.VariableName;

            for (int i = 0; i < CompiledVariables.Count; i++)
            {
                if (CompiledVariables[i].VariableName.Content == newVariable.VariableName.Content)
                {
                    Warnings.Add(new Warning($"Variable \"{CompiledVariables[i].VariableName}\" already defined", CompiledVariables[i].VariableName, CurrentFile));
                    return CleanupItem.Null;
                }
            }

            int offset = 0;
            if (isGlobal)
            { offset += VariablesSize; }
            else
            { offset += LocalVariablesSize; }

            CompiledVariable compiledVariable = CompileVariable(newVariable, offset, isGlobal);

            CompiledVariables.Add(compiledVariable);

            newVariable.Type.SetAnalyzedType(compiledVariable.Type);

            int size;

            if (TryCompute(newVariable.InitialValue, null, out DataItem computedInitialValue))
            {
                size = 1;

                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, computedInitialValue);
                compiledVariable.IsInitialized = true;

                if (size <= 0)
                { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }
            }
            else
            {

                size = GenerateInitialValue(compiledVariable.Type);

                if (size <= 0)
                { throw new CompilerException($"Variable has a size of {size}", newVariable, CurrentFile); }
            }

            if (size != compiledVariable.Type.SizeOnStack)
            { throw new InternalException($"Variable size ({compiledVariable.Type.SizeOnStack}) and initial value size ({size}) mismatch"); }

            return new CleanupItem(size, newVariable.Modifiers.Contains("temp"), compiledVariable.Type);
        }
        CleanupItem GenerateCodeForVariable(Statement st, bool isGlobal)
        {
            if (st is VariableDeclaration newVariable)
            { return GenerateCodeForVariable(newVariable, isGlobal); }
            return CleanupItem.Null;
        }
        CleanupItem[] GenerateCodeForVariable(Statement[] sts, bool isGlobal)
        {
            List<CleanupItem> result = new();
            for (int i = 0; i < sts.Length; i++)
            {
                CleanupItem item = GenerateCodeForVariable(sts[i], isGlobal);
                if (item.Size == 0) continue;

                result.Add(item);
            }
            return result.ToArray();
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
            throw new NotImplementedException();
        }

        void CompileSetter(Field field, StatementWithValue value)
        {
            throw new NotImplementedException();
        }

        void CompileSetter(Pointer statement, StatementWithValue value)
        {
            throw new NotImplementedException();
        }

        void CompileSetter(int address, StatementWithValue value)
        {
            throw new NotImplementedException();
        }

        void CompileSetter(IndexCall statement, StatementWithValue value)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region GenerateCodeForStatement()
        void GenerateCodeForStatement(Statement statement)
        {
            if (statement is KeywordCall instructionStatement)
            { GenerateCodeForStatement(instructionStatement); }
            else if (statement is FunctionCall functionCall)
            { GenerateCodeForStatement(functionCall); }
            else if (statement is IfContainer @if)
            { GenerateCodeForStatement(@if.ToLinks()); }
            else if (statement is WhileLoop @while)
            { GenerateCodeForStatement(@while); }
            else if (statement is LiteralStatement literal)
            { GenerateCodeForStatement(literal); }
            else if (statement is Identifier variable)
            { GenerateCodeForStatement(variable); }
            else if (statement is OperatorCall expression)
            { GenerateCodeForStatement(expression); }
            else if (statement is AddressGetter addressGetter)
            { GenerateCodeForStatement(addressGetter); }
            else if (statement is Pointer pointer)
            { GenerateCodeForStatement(pointer); }
            else if (statement is Assignment assignment)
            { GenerateCodeForStatement(assignment); }
            else if (statement is ShortOperatorCall shortOperatorCall)
            { GenerateCodeForStatement(shortOperatorCall); }
            else if (statement is CompoundAssignment compoundAssignment)
            { GenerateCodeForStatement(compoundAssignment); }
            else if (statement is VariableDeclaration variableDeclaration)
            { GenerateCodeForStatement(variableDeclaration); }
            else if (statement is TypeCast typeCast)
            { GenerateCodeForStatement(typeCast); }
            else if (statement is NewInstance newInstance)
            { GenerateCodeForStatement(newInstance); }
            else if (statement is ConstructorCall constructorCall)
            { GenerateCodeForStatement(constructorCall); }
            else if (statement is Field field)
            { GenerateCodeForStatement(field); }
            else if (statement is IndexCall indexCall)
            { GenerateCodeForStatement(indexCall); }
            else if (statement is AnyCall anyCall)
            { GenerateCodeForStatement(anyCall); }
            else
            { throw new CompilerException($"Unknown statement {statement.GetType().Name}", statement, CurrentFile); }

            if (statement is FunctionCall statementWithValue &&
                !statementWithValue.SaveValue &&
                GetFunction(statementWithValue, out CompiledFunction? _f) &&
                _f.ReturnSomething)
            {
                throw new NotImplementedException();
            }
        }
        void GenerateCodeForStatement(AnyCall anyCall)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
            {
                GenerateCodeForStatement(functionCall);
                return;
            }

            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(IndexCall indexCall)
        {
            CompiledType arrayType = FindStatementType(indexCall.PrevStatement);

            if (!arrayType.IsClass)
            { throw new CompilerException($"Index getter for type \"{arrayType.Name}\" not found", indexCall, CurrentFile); }

            if (arrayType.Class.CompiledAttributes.HasAttribute("Define", "array"))
            {
                throw new NotImplementedException();
            }

            GenerateCodeForStatement(new FunctionCall(
                indexCall.PrevStatement,
                Token.CreateAnonymous(BuiltinFunctionNames.IndexerGet),
                indexCall.BracketLeft,
                new StatementWithValue[]
                {
                    indexCall.Expression,
                },
                indexCall.BracketRight));
        }
        void GenerateCodeForStatement(LinkedIf @if)
        {
            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(WhileLoop @while)
        {
            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(KeywordCall statement)
        {
            switch (statement.Identifier.Content.ToLower())
            {
                case "return":
                    {
                        if (statement.Parameters.Length != 0 &&
                            statement.Parameters.Length != 1)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"return\" (required 0 or 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        throw new NotImplementedException();
                    }

                case "break":
                    {
                        if (statement.Parameters.Length != 0)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"break\" (required 0, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        throw new NotImplementedException();
                    }

                case "delete":
                    {
                        throw new NotImplementedException();
                    }

                default: throw new CompilerException($"Unknown instruction command \"{statement.Identifier}\"", statement.Identifier, CurrentFile);
            }
        }
        void GenerateCodeForStatement(Assignment statement)
        {
            if (statement.Operator.Content != "=")
            { throw new CompilerException($"Unknown assignment operator \'{statement.Operator}\'", statement.Operator, CurrentFile); }

            CompileSetter(statement.Left, statement.Right ?? throw new CompilerException($"Value is required for \'{statement.Operator}\' assignment", statement, CurrentFile));
        }
        void GenerateCodeForStatement(CompoundAssignment statement)
        {
            switch (statement.Operator.Content)
            {
                case "+=":
                    {
                        throw new NotImplementedException();
                    }
                case "-=":
                    {
                        throw new NotImplementedException();
                    }
                default:
                    GenerateCodeForStatement(statement.ToAssignment());
                    break;
            }
        }
        void GenerateCodeForStatement(ShortOperatorCall statement)
        {
            switch (statement.Operator.Content)
            {
                case "++":
                    {
                        throw new NotImplementedException();
                    }
                case "--":
                    {
                        throw new NotImplementedException();
                    }
                default:
                    throw new CompilerException($"Unknown assignment operator \'{statement.Operator}\'", statement.Operator, CurrentFile);
            }
        }
        void GenerateCodeForStatement(VariableDeclaration statement)
        {
            if (statement.InitialValue == null) return;

            if (!GetVariable(statement.VariableName.Content, out CompiledVariable? variable))
            { throw new InternalException($"Variable \"{statement.VariableName.Content}\" not found", CurrentFile); }

            if (variable.IsInitialized) return;

            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(FunctionCall functionCall)
        {
            if (functionCall.Identifier == "Alloc" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 0)
            {
                throw new NotImplementedException();
            }

            if (functionCall.Identifier == "AllocFrom" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 1 && (
                    FindStatementType(functionCall.Parameters[0]).BuiltinType == Type.Byte ||
                    FindStatementType(functionCall.Parameters[0]).BuiltinType == Type.Integer
                ))
            {
                throw new NotImplementedException();
            }

            if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compilableFunction.Function;
            }

            if (compiledFunction.CompiledAttributes.HasAttribute("StandardOutput"))
            {
                StatementWithValue valueToPrint = functionCall.Parameters[0];
                CompiledType valueToPrintType = FindStatementType(valueToPrint);

                if (valueToPrint is LiteralStatement literal)
                {
                    string label = Builder.DataBuilder.NewString(literal.Value);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.BasePointer, Registers.StackPointer);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.SUB, Registers.StackPointer, 4.ToString());

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (-11).ToString());
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, "_GetStdHandle@4");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EBX, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0.ToString());
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.LEA, Registers.EAX, $"[{Registers.BasePointer}-{4}]");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, literal.Value.Length.ToString());
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, label);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, "_WriteFile@20");
                    return;
                }

                throw new NotImplementedException();
            }

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                GenerateCodeForStatement(functionCall.Parameters[i]);
            }

            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(ConstructorCall constructorCall)
        {
            var instanceType = FindType(constructorCall.TypeName);

            if (instanceType.IsStruct)
            { throw new NotImplementedException(); }

            if (!instanceType.IsClass)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }

            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.Position.Range, CurrentFile));

            if (!GetClass(constructorCall, out CompiledClass? @class))
            { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

            if (!GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), BuiltinFunctionNames.Constructor, out CompiledGeneralFunction? constructor))
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

            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(LiteralStatement statement)
        {
            switch (statement.Type)
            {
                case LiteralType.Integer:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, statement.Value);
                    break;
                case LiteralType.Float:
                    throw new NotImplementedException();
                case LiteralType.Boolean:
                    break;
                case LiteralType.String:
                    throw new NotImplementedException();
                case LiteralType.Char:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, ((int)statement.Value[0]).ToString());
                    break;
                default:
                    throw new ImpossibleException();
            }
        }
        void GenerateCodeForStatement(Identifier statement, CompiledType? expectedType = null)
        {
            if (GetConstant(statement.Content, out DataItem constant))
            {
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, constant);
                return;
            }

            if (GetParameter(statement.Content, out CompiledParameter? param))
            {
                throw new NotImplementedException();
            }

            if (GetVariable(statement.Content, out CompiledVariable? val))
            {
                statement.Name.AnalyzedType = TokenAnalyzedType.VariableName;
                StackLoad(new ValueAddress(val), val.Type.SizeOnStack);
                return;
            }

            if (GetFunction(statement.Name, expectedType, out CompiledFunction? compiledFunction))
            {
                throw new NotImplementedException();
            }

            throw new CompilerException($"Variable \"{statement.Content}\" not found", statement, CurrentFile);
        }
        void GenerateCodeForStatement(OperatorCall statement)
        {
            if (GetOperator(statement, out CompiledOperator? operatorDefinition))
            {
                throw new NotImplementedException();
            }
            else if (LanguageConstants.Operators.OpCodes.TryGetValue(statement.Operator.Content, out Opcode opcode))
            {
                if (LanguageConstants.Operators.ParameterCounts[statement.Operator.Content] != statement.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator \"{statement.Operator.Content}\": required {LanguageConstants.Operators.ParameterCounts[statement.Operator.Content]} passed {statement.ParameterCount}", statement.Operator, CurrentFile); }

                GenerateCodeForStatement(statement.Left);

                if (statement.Right != null)
                {
                    GenerateCodeForStatement(statement.Right);
                }

                switch (opcode)
                {
                    case Opcode.LOGIC_LT:
                        break;
                    case Opcode.LOGIC_MT:
                        break;
                    case Opcode.LOGIC_LTEQ:
                        break;
                    case Opcode.LOGIC_MTEQ:
                        break;
                    case Opcode.LOGIC_OR:
                        break;
                    case Opcode.LOGIC_AND:
                        break;
                    case Opcode.LOGIC_EQ:
                        break;
                    case Opcode.LOGIC_NEQ:
                        break;
                    case Opcode.LOGIC_NOT:
                        break;

                    case Opcode.BITS_AND:
                        break;
                    case Opcode.BITS_OR:
                        break;
                    case Opcode.BITS_XOR:
                        break;

                    case Opcode.BITSHIFT_LEFT:
                        break;
                    case Opcode.BITSHIFT_RIGHT:
                        break;

                    case Opcode.MATH_ADD:
                        {
                            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.ADD, Registers.EAX, Registers.EBX);
                            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                            break;
                        }
                    case Opcode.MATH_SUB:
                        break;
                    case Opcode.MATH_MULT:
                        break;
                    case Opcode.MATH_DIV:
                        break;
                    case Opcode.MATH_MOD:
                        break;
                    default:
                        break;
                }
            }
            else
            { throw new CompilerException($"Unknown operator \"{statement.Operator.Content}\"", statement.Operator, CurrentFile); }
        }
        void Compile(Block block)
        {
            foreach (Statement statement in block.Statements)
            {
                GenerateCodeForStatement(statement);
            }
        }
        void GenerateCodeForStatement(AddressGetter addressGetter)
        {
            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(Pointer pointer)
        {
            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(NewInstance newInstance)
        {
            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(Field field)
        {
            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(TypeCast typeCast)
        {
            Warnings.Add(new Warning($"Type-cast is not supported. I will ignore it and compile just the value", new Position(typeCast.Keyword, typeCast.Type), CurrentFile));

            GenerateCodeForStatement(typeCast.PrevStatement);
        }

        #endregion

        void CleanupVariables(CleanupItem[] cleanup)
        {
            for (int i = 0; i < cleanup.Length; i++)
            {
                CleanupItem item = cleanup[i];
                for (int j = 0; j < item.Size; j++)
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.ADD, Registers.StackPointer, 4.ToString());
                }
            }
        }

        void GenerateCodeForTopLevelStatements(Statement[] statements)
        {
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.BasePointer, Registers.StackPointer);

            var cleanup = GenerateCodeForVariable(statements, true);
            bool hasExited = false;

            for (int i = 0; i < statements.Length; i++)
            {
                if (statements[i] is KeywordCall keywordCall &&
                    keywordCall.Identifier == "return")
                {
                    if (keywordCall.Parameters.Length != 0 &&
                        keywordCall.Parameters.Length != 1)
                    { throw new CompilerException($"Wrong number of parameters passed to instruction \"return\" (required 0 or 1, passed {keywordCall.Parameters.Length})", keywordCall, CurrentFile); }

                    if (keywordCall.Parameters.Length == 1)
                    {
                        GenerateCodeForStatement(keywordCall.Parameters[0]);
                        /*
                        if (keywordCall.Parameters[0] is not Literal literal)
                        {
                            throw new NotImplementedException();
                        }
                        if (literal.Type != LiteralType.Integer)
                        {
                            throw new NotImplementedException();
                        }
                        int exitCode = literal.GetInt();
                        Builder.CodeBuilder.AppendInstruction("push", exitCode.ToString());
                        */
                        hasExited = true;
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, "_ExitProcess@4");
                        break;
                    }
                }
                GenerateCodeForStatement(statements[i]);
            }

            CleanupVariables(cleanup);

            if (!hasExited)
            {
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0.ToString());
                Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, "_ExitProcess@4");
            }

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.HALT);
        }

        Result GenerateCode(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            PrintCallback? printCallback = null)
        {
            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements);

            return new Result()
            {
                Tokens = compilerResult.Tokens,

                AssemblyCode = Builder.Make(new AssemblyHeader()
                {
                    Externs = new List<string>()
                    {
                        "_GetStdHandle@4",
                        "_WriteFile@20",
                        "_ExitProcess@4",
                    },
                }),

                Warnings = this.Warnings.ToArray(),
                Errors = this.Errors.ToArray(),
            };
        }

        public static Result Generate(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            Settings generatorSettings,
            PrintCallback? printCallback = null)
        => new CodeGenerator(compilerResult, generatorSettings).GenerateCode(
            compilerResult,
            settings,
            printCallback
        );
    }
}
