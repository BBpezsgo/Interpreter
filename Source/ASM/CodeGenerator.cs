using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.ASM.Generator
{
    using System.Diagnostics.CodeAnalysis;
    using BBCode.Generator;
    using Compiler;
    using Parser;
    using Parser.Statement;
    using Runtime;
    using Tokenizing;
    using LiteralStatement = Parser.Statement.Literal;
    using ParameterCleanupItem = (int Size, bool CanDeallocate, Compiler.CompiledType Type);

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

    public struct AsmGeneratorSettings
    {

    }

    public struct AsmGeneratorResult
    {
        public Warning[] Warnings;
        public Error[] Errors;

        public string AssemblyCode;
    }

    public class CodeGeneratorForAsm : CodeGenerator
    {
        #region Fields

        readonly AsmGeneratorSettings GeneratorSettings;
        readonly AssemblyCode Builder;

        readonly List<(CompiledFunction Function, string Label)> FunctionLabels;
        readonly List<CompiledFunction> GeneratedFunctions;
        readonly Stack<int> FunctionFrameSize;

        #endregion

        public CodeGeneratorForAsm(CompilerResult compilerResult, AsmGeneratorSettings settings) : base()
        {
            this.GeneratorSettings = settings;
            this.CompiledFunctions = compilerResult.Functions;
            this.CompiledOperators = compilerResult.Operators;
            this.CompiledClasses = compilerResult.Classes;
            this.CompiledStructs = compilerResult.Structs;
            this.CompiledEnums = compilerResult.Enums;
            this.CompiledMacros = compilerResult.Macros;
            this.Builder = new AssemblyCode();

            this.FunctionLabels = new List<(CompiledFunction Function, string Label)>();
            this.GeneratedFunctions = new List<CompiledFunction>();
            this.FunctionFrameSize = new Stack<int>();
        }

        #region Memory Helpers

        bool TryGetFunctionLabel(CompiledFunction function, [NotNullWhen(true)] out string? label)
        {
            for (int i = 0; i < FunctionLabels.Count; i++)
            {
                if (ReferenceEquals(FunctionLabels[i].Function, function))
                {
                    label = FunctionLabels[i].Label;
                    return true;
                }
            }
            label = null;
            return false;
        }

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
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, $"DWORD[{Registers.EBP}-{(address.Address + 1) * 4}]");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    break;

                case AddressingMode.RELATIVE:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EAX, $"DWORD[{Registers.ESP}+{(address.Address + 1) * 4}]");
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

            switch (address.AddressingMode)
            {
                case AddressingMode.ABSOLUTE:
                case AddressingMode.BASEPOINTER_RELATIVE:
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, $"DWORD[{Registers.EBP}-{(address.Address + 1) * 4}]".Replace("--", "+"), Registers.EAX);
                    break;
                case AddressingMode.RELATIVE:
                case AddressingMode.POP:
                case AddressingMode.RUNTIME:
                    throw new NotImplementedException();
                default: throw new ImpossibleException();
            }
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

        public int ReturnValueOffset => -(ParametersSize + 3);

        protected override ValueAddress GetBaseAddress(CompiledParameter parameter)
        {
            int offset = -(2 + ParametersSizeBefore(parameter.Index));
            return new ValueAddress(parameter, offset);
        }
        protected override ValueAddress GetBaseAddress(CompiledParameter parameter, int offset)
        {
            int _offset = -(2 + ParametersSizeBefore(parameter.Index) - offset);
            return new ValueAddress(parameter, _offset);
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
                { sum += CompiledVariables[i].Type.SizeOnStack; }
                return sum;
            }
        }

        CleanupItem GenerateCodeForVariable(VariableDeclaration newVariable)
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

            int offset = VariablesSize;

            CompiledVariable compiledVariable = CompileVariable(newVariable, offset);

            CompiledVariables.Add(compiledVariable);

            newVariable.Type.SetAnalyzedType(compiledVariable.Type);

            Builder.CodeBuilder.AppendCommentLine($"{compiledVariable.Type} {compiledVariable.VariableName.Content}");

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

            if (FunctionFrameSize.Count > 0)
            { FunctionFrameSize.Last += size; }

            return new CleanupItem(size, newVariable.Modifiers.Contains("temp"), compiledVariable.Type);
        }
        CleanupItem GenerateCodeForVariable(Statement st)
        {
            if (st is VariableDeclaration newVariable)
            { return GenerateCodeForVariable(newVariable); }
            return CleanupItem.Null;
        }
        CleanupItem[] GenerateCodeForVariable(Statement[] sts)
        {
            List<CleanupItem> result = new();
            for (int i = 0; i < sts.Length; i++)
            {
                CleanupItem item = GenerateCodeForVariable(sts[i]);
                if (item.SizeOnStack == 0) continue;

                result.Add(item);
            }
            return result.ToArray();
        }

        #endregion

        #region GenerateCodeForSetter

        void GenerateCodeForSetter(Statement statement, StatementWithValue value)
        {
            if (statement is Identifier variableIdentifier)
            { GenerateCodeForSetter(variableIdentifier, value); }
            else if (statement is Pointer pointerToSet)
            { GenerateCodeForSetter(pointerToSet, value); }
            else if (statement is IndexCall index)
            { GenerateCodeForSetter(index, value); }
            else if (statement is Field field)
            { GenerateCodeForSetter(field, value); }
            else
            { throw new CompilerException($"Setter for statement {statement.GetType().Name} not implemented", statement, CurrentFile); }
        }

        void GenerateCodeForSetter(Identifier statement, StatementWithValue value)
        {
            if (GetConstant(statement.Content, out _))
            { throw new CompilerException($"Can not set constant value: it is readonly", statement, CurrentFile); }

            if (GetParameter(statement.Content, out CompiledParameter? parameter))
            {
                CompiledType valueType = FindStatementType(value, parameter.Type);

                if (parameter.Type != valueType)
                { throw new CompilerException($"Can not set a \"{valueType.Name}\" type value to the \"{parameter.Type.Name}\" type parameter.", value, CurrentFile); }

                GenerateCodeForStatement(value);

                if (parameter.IsRef)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    ValueAddress offset = GetBaseAddress(parameter);
                    StackStore(offset);
                }

                throw new NotImplementedException();
            }
            else if (GetVariable(statement.Name.Content, out CompiledVariable? variable))
            {
                statement.Name.AnalyzedType = TokenAnalyzedType.VariableName;

                GenerateCodeForStatement(value);

                StackStore(new ValueAddress(variable), variable.Type.SizeOnStack);
            }
            else
            {
                throw new CompilerException($"Symbol \"{statement.Content}\" not found", statement, CurrentFile);
            }
        }

        void GenerateCodeForSetter(Field field, StatementWithValue value)
        {
            throw new NotImplementedException();
        }

        void GenerateCodeForSetter(Pointer statement, StatementWithValue value)
        {
            throw new NotImplementedException();
        }

        void GenerateCodeForSetter(IndexCall statement, StatementWithValue value)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region GenerateCodeForStatement()

        void Call(string label)
        {
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, label);
        }

        void Return()
        {
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.RET);
        }

        Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(FunctionCall functionCall, CompiledFunction compiledFunction)
        {
            Stack<ParameterCleanupItem> parameterCleanup = new();

            if (functionCall.PrevStatement != null)
            {
                StatementWithValue passedParameter = functionCall.PrevStatement;
                CompiledType passedParameterType = FindStatementType(passedParameter);
                GenerateCodeForStatement(functionCall.PrevStatement);
                parameterCleanup.Push((passedParameterType.SizeOnStack, false, passedParameterType));
            }

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                StatementWithValue passedParameter = functionCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
                // CompiledType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

                bool canDeallocate = definedParameter.Modifiers.Contains("temp");

                canDeallocate = canDeallocate && (passedParameterType.InHEAP || passedParameterType == Type.Integer);

                if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
                {
                    if (explicitDeallocate && !canDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
                }
                else
                {
                    if (explicitDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                    canDeallocate = false;
                }

                GenerateCodeForStatement(passedParameter); // TODO: expectedType = definedParameterType

                parameterCleanup.Push((passedParameterType.SizeOnStack, canDeallocate, passedParameterType));
            }

            return parameterCleanup;
        }

        Stack<ParameterCleanupItem> GenerateCodeForParameterPassing(OperatorCall functionCall, CompiledOperator compiledFunction)
        {
            Stack<ParameterCleanupItem> parameterCleanup = new();

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                StatementWithValue passedParameter = functionCall.Parameters[i];
                CompiledType passedParameterType = FindStatementType(passedParameter);
                ParameterDefinition definedParameter = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];
                // CompiledType definedParameterType = compiledFunction.ParameterTypes[compiledFunction.IsMethod ? (i + 1) : i];

                bool canDeallocate = definedParameter.Modifiers.Contains("temp");

                canDeallocate = canDeallocate && (passedParameterType.InHEAP || passedParameterType == Type.Integer);

                if (StatementCanBeDeallocated(passedParameter, out bool explicitDeallocate))
                {
                    if (explicitDeallocate && !canDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{"temp"}\" modifier", passedParameter, CurrentFile)); }
                }
                else
                {
                    if (explicitDeallocate)
                    { Warnings.Add(new Warning($"Can not deallocate this value", passedParameter, CurrentFile)); }
                    canDeallocate = false;
                }

                GenerateCodeForStatement(passedParameter); // TODO: expectedType = definedParameterType

                parameterCleanup.Push((passedParameterType.SizeOnStack, canDeallocate, passedParameterType));
            }

            return parameterCleanup;
        }

        void GenerateCodeForParameterCleanup(Stack<ParameterCleanupItem> parameterCleanup)
        {
            while (parameterCleanup.Count > 0)
            {
                ParameterCleanupItem passedParameter = parameterCleanup.Pop();

                if (passedParameter.CanDeallocate && passedParameter.Size == 1)
                { throw new NotImplementedException(); }

                for (int i = 0; i < passedParameter.Size; i++)
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                }
            }
        }

        void GenerateCodeForFunctionCall_Function(FunctionCall functionCall, CompiledFunction compiledFunction)
        {
            if (!compiledFunction.CanUse(CurrentFile))
            {
                Errors.Add(new Error($"The {compiledFunction.ReadableID()} function could not be called due to its protection level", functionCall.Identifier, CurrentFile));
                return;
            }

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to function {compiledFunction.ReadableID()}: required {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall, CurrentFile); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new CompilerException($"You called the {(compiledFunction.IsMethod ? "method" : "function")} \"{functionCall.FunctionName}\" as {(functionCall.IsMethodCall ? "method" : "function")}", functionCall, CurrentFile); }

            if (compiledFunction.CompiledAttributes.HasAttribute("StandardOutput"))
            {
                StatementWithValue valueToPrint = functionCall.Parameters[0];
                // CompiledType valueToPrintType = FindStatementType(valueToPrint);

                if (valueToPrint is LiteralStatement literal)
                {
                    string dataLabel = Builder.DataBuilder.NewString(literal.Value);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EBP, Registers.ESP);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.SUB, Registers.ESP, 4.ToString());

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (-11).ToString());
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, "_GetStdHandle@4");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EBX, Registers.EAX);

                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, 0.ToString());
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.LEA, Registers.EAX, $"[{Registers.EBP}-{4}]");
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, literal.Value.Length.ToString());
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, dataLabel);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EBX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.CALL, "_WriteFile@20");
                    return;
                }
            }

            if (compiledFunction.IsMacro)
            { Warnings.Add(new Warning($"I can not inline macros because of lack of intelligence so I will treat this macro as a normal function.", functionCall, CurrentFile)); }

            Stack<ParameterCleanupItem> parameterCleanup;

            int returnValueSize = 0;
            if (compiledFunction.ReturnSomething)
            {
                returnValueSize = GenerateInitialValue(compiledFunction.Type);
            }

            if (compiledFunction.IsExternal)
            {
                switch (compiledFunction.ExternalFunctionName)
                {
                    case "stdout":
                    default:
                        throw new NotImplementedException();
                }
            }

            parameterCleanup = GenerateCodeForParameterPassing(functionCall, compiledFunction);

            if (!TryGetFunctionLabel(compiledFunction, out string? label))
            {
                label = Builder.CodeBuilder.NewLabel($"f_{compiledFunction.Identifier}");
                FunctionLabels.Add((compiledFunction, label));
            }

            Call(label);

            GenerateCodeForParameterCleanup(parameterCleanup);

            if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
            {
                for (int i = 0; i < returnValueSize; i++)
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.RAX);
                }
            }
        }

        void GenerateCodeForStatement(Statement statement)
        {
            if (statement is KeywordCall instructionStatement)
            { GenerateCodeForStatement(instructionStatement); }
            else if (statement is FunctionCall functionCall)
            { GenerateCodeForStatement(functionCall); }
            else if (statement is IfContainer @if)
            { GenerateCodeForStatement(@if); }
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
            else if (statement is Block block)
            { GenerateCodeForStatement(block); }
            else if (statement is ModifiedStatement modifiedStatement)
            { GenerateCodeForStatement(modifiedStatement); }
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
        void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
        {
            StatementWithValue statement = modifiedStatement.Statement;
            Token modifier = modifiedStatement.Modifier;

            if (modifier == "ref")
            {
                ValueAddress address = GetDataAddress(statement);

                if (address.InHeap)
                { throw new CompilerException($"This value is stored in the heap and not in the stack", statement, CurrentFile); }

                if (address.IsReference)
                {
                    StackLoad(address);
                }
                else
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, $"[rbp+{address.Address}]");
                }
                throw new NotImplementedException();
            }

            if (modifier == "temp")
            {
                GenerateCodeForStatement(statement);
                return;
            }

            throw new NotImplementedException();
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
        void GenerateCodeForStatement(IfContainer @if)
        {
            string endLabel = Builder.CodeBuilder.NewLabel("if_end");
            for (int i = 0; i < @if.Parts.Length; i++)
            {
                BaseBranch part = @if.Parts[i];

                if (part is IfBranch ifBranch)
                {
                    string nextLabel = Builder.CodeBuilder.NewLabel("if_next");
                    GenerateCodeForStatement(ifBranch.Condition);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.TEST, Registers.EAX, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JZ, nextLabel);
                    GenerateCodeForStatement(ifBranch.Block);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, endLabel);
                    Builder.CodeBuilder.AppendLabel(nextLabel);
                }
                else if (part is ElseIfBranch elseIfBranch)
                {
                    string nextLabel = Builder.CodeBuilder.NewLabel("if_next");
                    GenerateCodeForStatement(elseIfBranch.Condition);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.TEST, Registers.EAX, Registers.EAX);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JZ, nextLabel);
                    GenerateCodeForStatement(elseIfBranch.Block);
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, endLabel);
                    Builder.CodeBuilder.AppendLabel(nextLabel);
                }
                else if (part is ElseBranch elseBranch)
                {
                    GenerateCodeForStatement(elseBranch.Block);
                }
                else
                {
                    throw new ImpossibleException();
                }
            }
            Builder.CodeBuilder.AppendLabel(endLabel);
        }
        void GenerateCodeForStatement(WhileLoop @while)
        {
            Builder.CodeBuilder.AppendCommentLine($"while ({@while.Condition}) {{");
            Builder.CodeBuilder.Indent += SectionBuilder.IndentIncrement;

            string startLabel = Builder.CodeBuilder.NewLabel("while_start");
            string endLabel = Builder.CodeBuilder.NewLabel("while_end");

            Builder.CodeBuilder.AppendLabel(startLabel);

            Builder.CodeBuilder.AppendCommentLine($"Condition {{");
            using (Builder.CodeBuilder.Block())
            { GenerateCodeForStatement(@while.Condition); }
            Builder.CodeBuilder.AppendCommentLine($"}}");

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.TEST, Registers.EAX, Registers.EAX);
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JZ, endLabel);

            Builder.CodeBuilder.AppendCommentLine($"{{");
            using (Builder.CodeBuilder.Block())
            { GenerateCodeForStatement(@while.Block); }
            Builder.CodeBuilder.AppendCommentLine($"}}");

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.JMP, startLabel);

            Builder.CodeBuilder.AppendLabel(endLabel);

            Builder.CodeBuilder.Indent -= SectionBuilder.IndentIncrement;
            Builder.CodeBuilder.AppendCommentLine($"}}");
        }
        void GenerateCodeForStatement(KeywordCall statement)
        {
            switch (statement.Identifier.Content.ToLower())
            {
                case "return":
                    {
                        if (statement.Parameters.Length > 1)
                        { throw new CompilerException($"Wrong number of parameters passed to \"return\": required {0} or {1} passed {statement.Parameters.Length}", statement, CurrentFile); }

                        if (statement.Parameters.Length == 1)
                        {
                            StatementWithValue returnValue = statement.Parameters[0];
                            CompiledType returnValueType = FindStatementType(returnValue);

                            GenerateCodeForStatement(returnValue);

                            int offset = ReturnValueOffset;
                            StackStore(new ValueAddress(offset, true, false, false), returnValueType.SizeOnStack);
                        }

                        if (InFunction)
                        { Builder.CodeBuilder.AppendInstruction(ASM.Instruction.ADD, Registers.ESP, (FunctionFrameSize.Last * 4).ToString()); }

                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBP);
                        Return();
                        break;
                    }

                case "break":
                    {
                        if (statement.Parameters.Length != 0)
                        { throw new CompilerException($"Wrong number of parameters passed to \"break\": required {0}, passed {statement.Parameters.Length}", statement, CurrentFile); }

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

            GenerateCodeForSetter(statement.Left, statement.Right ?? throw new CompilerException($"Value is required for \'{statement.Operator}\' assignment", statement, CurrentFile));
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

            GenerateCodeForSetter(new Identifier(statement.VariableName), statement.InitialValue);

            variable.IsInitialized = true;
        }
        void GenerateCodeForStatement(FunctionCall functionCall)
        {
            if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compilableFunction.Function;
            }

            GenerateCodeForFunctionCall_Function(functionCall, compiledFunction);
        }
        void GenerateCodeForStatement(ConstructorCall constructorCall)
        {
            CompiledType instanceType = FindType(constructorCall.TypeName);

            if (instanceType.IsStruct)
            { throw new NotImplementedException(); }

            if (!instanceType.IsClass)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }

            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.Position.Range, CurrentFile));

            if (!GetClass(constructorCall, out CompiledClass? @class))
            { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

            if (!GetGeneralFunction(@class, FindStatementTypes(constructorCall.Parameters), BuiltinFunctionNames.Constructor, out CompiledGeneralFunction? constructor))
            {
                if (!GetConstructorTemplate(@class, constructorCall, out CompliableTemplate<CompiledGeneralFunction> compilableGeneralFunction))
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
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, (bool.Parse(statement.Value) ? 1 : 0).ToString());
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

            if (GetParameter(statement.Content, out CompiledParameter? compiledParameter))
            {
                statement.Name.AnalyzedType = TokenAnalyzedType.ParameterName;
                ValueAddress address = GetBaseAddress(compiledParameter);
                StackLoad(address, compiledParameter.Type.SizeOnStack);
                return;
            }

            if (GetVariable(statement.Content, out CompiledVariable? val))
            {
                statement.Name.AnalyzedType = TokenAnalyzedType.VariableName;
                StackLoad(new ValueAddress(val), val.Type.SizeOnStack);
                return;
            }

            if (GetFunction(statement.Name, expectedType, out _))
            {
                throw new NotImplementedException();
            }

            throw new CompilerException($"Variable \"{statement.Content}\" not found", statement, CurrentFile);
        }
        void GenerateCodeForStatement(OperatorCall statement)
        {
            if (GetOperator(statement, out _))
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
                        throw new NotImplementedException();
                    case Opcode.LOGIC_MT:
                        throw new NotImplementedException();
                    case Opcode.LOGIC_LTEQ:
                        throw new NotImplementedException();
                    case Opcode.LOGIC_MTEQ:
                        throw new NotImplementedException();
                    case Opcode.LOGIC_OR:
                        throw new NotImplementedException();
                    case Opcode.LOGIC_AND:
                        throw new NotImplementedException();
                    case Opcode.LOGIC_EQ:
                        throw new NotImplementedException();
                    case Opcode.LOGIC_NEQ:
                        throw new NotImplementedException();
                    case Opcode.LOGIC_NOT:
                        throw new NotImplementedException();

                    case Opcode.BITS_AND:
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PAND, Registers.EAX, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                        break;
                    case Opcode.BITS_OR:
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POR, Registers.EAX, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                        break;
                    case Opcode.BITS_XOR:
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.XOR, Registers.EAX, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                        break;

                    case Opcode.BITS_SHIFT_LEFT:
                        throw new NotImplementedException();
                    case Opcode.BITS_SHIFT_RIGHT:
                        throw new NotImplementedException();

                    case Opcode.MATH_ADD:
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.ADD, Registers.EAX, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                        break;
                    case Opcode.MATH_SUB:
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.SUB, Registers.EBX, Registers.EAX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EBX);
                        break;
                    case Opcode.MATH_MULT:
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MUL, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                        break;
                    case Opcode.MATH_DIV:
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction("cdq");
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EAX);
                        Builder.CodeBuilder.AppendInstruction("cdq");
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.IDIV, Registers.EBX);
                        Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EAX);
                        break;
                    case Opcode.MATH_MOD:
                        throw new NotImplementedException();
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            { throw new CompilerException($"Unknown operator \"{statement.Operator.Content}\"", statement.Operator, CurrentFile); }
        }
        void GenerateCodeForStatement(Block block)
        {
            CleanupItem[] cleanup = GenerateCodeForVariable(block.Statements);
            foreach (Statement statement in block.Statements)
            {
                GenerateCodeForStatement(statement);
            }
            CleanupVariables(cleanup);
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
                for (int j = 0; j < item.SizeOnStack; j++)
                {
                    Builder.CodeBuilder.AppendInstruction(ASM.Instruction.ADD, Registers.ESP, 4.ToString());
                }
            }
        }

        void GenerateCodeForTopLevelStatements(Statement[] statements)
        {
            Builder.CodeBuilder.AppendCommentLine(null);
            Builder.CodeBuilder.AppendCommentLine("Top level statements");
            Builder.CodeBuilder.AppendCommentLine(null);

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EBP, Registers.ESP);

            Builder.CodeBuilder.AppendCommentLine("Variables:");
            CleanupItem[] cleanup = GenerateCodeForVariable(statements);
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

        void CompileParameters(ParameterDefinition[] parameters)
        {
            int paramIndex = 0;
            int paramsSize = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                paramIndex++;
                CompiledType parameterType = new(parameters[i].Type, FindType);
                parameters[i].Type.SetAnalyzedType(parameterType);

                this.CompiledParameters.Add(new CompiledParameter(paramIndex, paramsSize, parameterType, parameters[i]));

                paramsSize += parameterType.SizeOnStack;
            }
        }

        void GenerateCodeForFunction(FunctionThingDefinition function)
        {
            if (LanguageConstants.Keywords.Contains(function.Identifier.Content))
            { throw new CompilerException($"The identifier \"{function.Identifier.Content}\" is reserved as a keyword. Do not use it as a function name", function.Identifier, function.FilePath); }

            function.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

            if (function is CompiledFunction compiledFunction1) GeneratedFunctions.Add(compiledFunction1);

            if (function is FunctionDefinition functionDefinition)
            {
                for (int i = 0; i < functionDefinition.Attributes.Length; i++)
                {
                    if (functionDefinition.Attributes[i].Identifier == "External")
                    { return; }
                }
            }

            if (function.Block == null)
            { return; }

            string? label;

            Builder.CodeBuilder.Indent = 0;
            Builder.CodeBuilder.AppendTextLine();

            if (function is CompiledFunction compiledFunction2)
            {
                if (!TryGetFunctionLabel(compiledFunction2, out label))
                {
                    label = Builder.CodeBuilder.NewLabel($"f_{compiledFunction2.Identifier}");
                    FunctionLabels.Add((compiledFunction2, label));
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            Builder.CodeBuilder.AppendLabel(label);

            Builder.CodeBuilder.Indent += SectionBuilder.IndentIncrement;

            FunctionFrameSize.Push(0);
            InFunction = true;

            CompiledParameters.Clear();

            CompileParameters(function.Parameters);

            CurrentFile = function.FilePath;

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.PUSH, Registers.EBP);
            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.MOV, Registers.EBP, Registers.ESP);

            GenerateCodeForStatement(function.Block);

            Builder.CodeBuilder.AppendInstruction(ASM.Instruction.POP, Registers.EBP);

            CurrentFile = null;

            Return();

            CompiledParameters.Clear();

            InFunction = false;
            FunctionFrameSize.Pop(0);
            Builder.CodeBuilder.Indent = 0;
        }

        AsmGeneratorResult GenerateCode(CompilerResult compilerResult, PrintCallback? printCallback = null)
        {
            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements);

            while (true)
            {
                bool shouldExit = true;
                (CompiledFunction Function, string Label)[] functionLabels = FunctionLabels.ToArray();

                for (int i = functionLabels.Length - 1; i >= 0; i--)
                {
                    CompiledFunction function = functionLabels[i].Function;
                    if (!GeneratedFunctions.Any(other => ReferenceEquals(function, other)))
                    {
                        shouldExit = false;
                        GenerateCodeForFunction(function);
                    }
                }

                if (shouldExit) break;
            }

            return new AsmGeneratorResult()
            {
                AssemblyCode = Builder.Make(new AssemblyHeader()
                {
                    Externs = new List<string>()
                    {
                        // "_GetStdHandle@4",
                        // "_WriteFile@20",
                        "_ExitProcess@4",
                    },
                }),

                Warnings = Warnings.ToArray(),
                Errors = Errors.ToArray(),
            };
        }

        public static AsmGeneratorResult Generate(CompilerResult compilerResult, AsmGeneratorSettings generatorSettings, PrintCallback? printCallback = null)
            => new CodeGeneratorForAsm(compilerResult, generatorSettings).GenerateCode(compilerResult, printCallback);
    }
}
