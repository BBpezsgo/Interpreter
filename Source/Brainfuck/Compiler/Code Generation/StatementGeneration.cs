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

    public partial class CodeGenerator : CodeGeneratorBase
    {
        #region PrecompileVariables
        int PrecompileVariables(Block block)
        { return PrecompileVariables(block.Statements); }
        int PrecompileVariables(IEnumerable<Statement>? statements)
        {
            if (statements == null) return 0;

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

                        using (DebugBlock(initialValue))
                        {
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

        #region GenerateCodeForSetter()

        void GenerateCodeForSetter(Statement statement, StatementWithValue value)
        {
            if (statement is Identifier variableIdentifier)
            {
                GenerateCodeForSetter(variableIdentifier, value);

                return;
            }

            if (statement is Pointer pointerToSet)
            {
                GenerateCodeForSetter(pointerToSet, value);

                return;
            }

            if (statement is IndexCall index)
            {
                GenerateCodeForSetter(index, value);

                return;
            }

            if (statement is Field field)
            {
                GenerateCodeForSetter(field, value);

                return;
            }

            throw new CompilerException($"Setter for statement {statement.GetType().Name} not implemented", statement, CurrentFile);
        }

        void GenerateCodeForSetter(Identifier statement, StatementWithValue value)
        {
            if (GetConstant(statement.Content, out _))
            { throw new CompilerException($"This is a constant so you can not modify it's value", statement, CurrentFile); }

            if (!Variables.TryFind(statement.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{statement}\" not found", statement, CurrentFile); }

            CompileSetter(variable, value);
        }

        void GenerateCodeForSetter(Field field, StatementWithValue value)
        {
            if (TryGetRuntimeAddress(field, out int pointerAddress, out int size))
            {
                if (size != GetValueSize(value))
                { throw new CompilerException($"Field and value size mismatch", value, CurrentFile); }

                int valueAddress = Stack.NextAddress;
                GenerateCodeForStatement(value);

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
                    GenerateCodeForStatement(value);
                }

                using (Code.Block($"Store computed value (from {Stack.LastAddress}) to {variable.Address}"))
                { Stack.PopAndStore(variable.Address); }

                UndiscardVariable(Variables, variable.Name);

                VariableCanBeDiscarded = null;
            }
        }

        void GenerateCodeForSetter(Pointer statement, StatementWithValue value)
        {
            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(statement.PrevStatement);

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
            GenerateCodeForStatement(value);

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
                    GenerateCodeForStatement(value);
                }

                int variableSize = Stack.Size - stackSize;

                using (Code.Block($"Store computed value (from {Stack.LastAddress}) to {address}"))
                { Stack.PopAndStore(address); }
            }
        }

        void GenerateCodeForSetter(IndexCall statement, StatementWithValue value)
        {
            if (statement.PrevStatement is not Identifier _variableIdentifier)
            { throw new NotSupportedException($"Only variable indexers supported for now", statement.PrevStatement, CurrentFile); }

            if (!Variables.TryFind(_variableIdentifier.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{_variableIdentifier}\" not found", _variableIdentifier, CurrentFile); }

            if (variable.IsDiscarded)
            { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", _variableIdentifier, CurrentFile); }

            using (Code.Block($"Set array (variable {variable.Name}) index ({statement.Expression}) (at {variable.Address}) to {value}"))
            {
                CompiledType valueType = FindStatementType(value);

                if (!variable.Type.IsStackArray)
                {
                    if (!variable.Type.IsClass)
                    { throw new CompilerException($"Index setter for type \"{variable.Type.Name}\" not found", statement, CurrentFile); }

                    if (!GetIndexSetter(variable.Type, valueType, out CompiledFunction? indexer))
                    {
                        if (!GetIndexSetterTemplate(variable.Type, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                        { throw new CompilerException($"Index setter for class \"{variable.Type.Class.Name}\" not found", statement, CurrentFile); }

                        indexerTemplate = AddCompilable(indexerTemplate);
                        indexer = indexerTemplate.Function;
                    }

                    GenerateCodeForStatement(new FunctionCall(
                            _variableIdentifier,
                            Token.CreateAnonymous(FunctionNames.IndexerSet),
                            statement.BracketLeft,
                            new StatementWithValue[]
                            {
                                statement.Expression,
                                value,
                            },
                            statement.BracketRight
                        ));

                    return;
                }

                CompiledType elementType = variable.Type.StackArrayOf;

                if (elementType != valueType)
                { throw new CompilerException("Bruh", value, CurrentFile); }

                int elementSize = elementType.Size;

                if (elementSize != 1)
                { throw new NotSupportedException($"Array element size must be 1 :(", value, CurrentFile); }

                int indexAddress = Stack.NextAddress;
                using (Code.Block($"Compute index"))
                { GenerateCodeForStatement(statement.Expression); }

                int valueAddress = Stack.NextAddress;
                using (Code.Block($"Compute value"))
                { GenerateCodeForStatement(value); }

                int temp0 = Stack.PushVirtual(1);

                Code.ARRAY_SET(variable.Address, indexAddress, valueAddress, temp0);

                Stack.Pop();
                Stack.Pop();
                Stack.Pop();
            }
        }

        #endregion

        #region GenerateCodeForStatement()
        void GenerateCodeForStatement(Statement statement)
        {
            int start = Code.GetFinalCode().Length;

            if (statement is KeywordCall instructionStatement)
            { GenerateCodeForStatement(instructionStatement); }
            else if (statement is FunctionCall functionCall)
            { GenerateCodeForStatement(functionCall); }
            else if (statement is IfContainer @if)
            { GenerateCodeForStatement(@if.ToLinks()); }
            else if (statement is WhileLoop @while)
            { GenerateCodeForStatement(@while); }
            else if (statement is ForLoop @for)
            { GenerateCodeForStatement(@for); }
            else if (statement is Literal literal)
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
            else if (statement is ModifiedStatement modifiedStatement)
            { GenerateCodeForStatement(modifiedStatement); }
            else
            { throw new CompilerException($"Unknown statement \"{statement.GetType().Name}\"", statement, CurrentFile); }

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
        void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
        {
            StatementWithValue statement = modifiedStatement.Statement;
            Token modifier = modifiedStatement.Modifier;

            if (modifier == "ref")
            {
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
            if (anyCall.ToFunctionCall(out var functionCall))
            {
                GenerateCodeForStatement(functionCall);
                return;
            }

            throw new NotSupportedException($"Function pointers not supported by brainfuck", anyCall.PrevStatement, CurrentFile);
        }
        void GenerateCodeForStatement(IndexCall indexCall)
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
                { GenerateCodeForStatement(indexCall.Expression); }

                Code.ARRAY_GET(arrayAddress, indexAddress, resultAddress);

                Stack.Pop();

                return;
            }

            if (!arrayType.IsClass)
            { throw new CompilerException($"Index getter for type \"{arrayType.Name}\" not found", indexCall, CurrentFile); }

            GenerateCodeForStatement(new FunctionCall(
                indexCall.PrevStatement,
                Token.CreateAnonymous(FunctionNames.IndexerGet),
                indexCall.BracketLeft,
                new StatementWithValue[]
                {
                    indexCall.Expression,
                },
                indexCall.BracketRight));
        }
        void GenerateCodeForStatement(LinkedIf @if, bool linked = false)
        {
            using (Code.Block($"If ({@if.Condition})"))
            {
                int conditionAddress = Stack.NextAddress;
                using (Code.Block("Compute condition"))
                { GenerateCodeForStatement(@if.Condition); }

                using (this.DebugBlock(@if.Condition))
                { Code.LOGIC_MAKE_BOOL(conditionAddress, conditionAddress + 1); }

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
                    {
                        using (Code.Block("Cleanup condition"))
                        {
                            Code.ClearValue(conditionAddress);
                            Code.JumpEnd(conditionAddress);
                            Stack.PopVirtual();
                        }
                    }
                }
                else
                {
                    using (Code.Block("Else"))
                    {
                        using (this.DebugBlock(@if.Block.BracketEnd))
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
                            // using (Code.Block($"Invert condition (at {conditionAddress}) result (to {conditionAddress + 1})"))
                            // { Code.LOGIC_NOT(conditionAddress, conditionAddress + 1); }

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
                                { GenerateCodeForStatement(elseIf, true); }
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
                    using (this.DebugBlock(@if.Semicolon ?? @if.Block.Semicolon ?? @if.Block.BracketEnd))
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
        void GenerateCodeForStatement(WhileLoop @while)
        {
            using (Code.Block($"While ({@while.Condition})"))
            {
                int conditionAddress = Stack.NextAddress;
                using (Code.Block("Compute condition"))
                { GenerateCodeForStatement(@while.Condition); }

                Code.CommentLine($"Condition result at {conditionAddress}");

                BreakTagStack.Push(Stack.Push(1));

                Code.JumpStart(conditionAddress);

                using (Code.Block("The while statements"))
                {
                    Compile(@while.Block);
                }

                using (Code.Block("Compute condition again"))
                {
                    GenerateCodeForStatement(@while.Condition);
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
        void GenerateCodeForStatement(ForLoop @for)
        {
            using (Code.Block($"For"))
            {
                VariableCleanupStack.Push(PrecompileVariable(@for.VariableDeclaration));

                using (Code.Block("Variable Declaration"))
                { GenerateCodeForStatement(@for.VariableDeclaration); }

                int conditionAddress = Stack.NextAddress;
                using (Code.Block("Compute condition"))
                { GenerateCodeForStatement(@for.Condition); }

                Code.CommentLine($"Condition result at {conditionAddress}");

                BreakTagStack.Push(Stack.Push(1));

                Code.JumpStart(conditionAddress);

                using (Code.Block("The while statements"))
                {
                    Compile(@for.Block);
                }

                using (Code.Block("Compute expression"))
                {
                    GenerateCodeForStatement(@for.Expression);
                }

                using (Code.Block("Compute condition again"))
                {
                    GenerateCodeForStatement(@for.Condition);
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
        void GenerateCodeForStatement(KeywordCall statement)
        {
            switch (statement.Identifier.Content.ToLower())
            {
                case "return":
                    {
                        statement.Identifier.AnalyzedType = TokenAnalyzedType.Statement;

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
                        statement.Identifier.AnalyzedType = TokenAnalyzedType.Statement;

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
                        statement.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

                        if (statement.Parameters.Length != 1)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        var deletable = statement.Parameters[0];
                        var deletableType = FindStatementType(deletable);

                        if (deletableType.IsClass)
                        {
                            if (!TryGetRuntimeAddress(deletable, out int pointerAddress, out int size))
                            { throw new CompilerException($"Failed to get address of statement \"{deletable}\"", deletable, CurrentFile); }

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
                            if (!TryGetBuiltinFunction("free", out CompiledFunction? function))
                            { throw new CompilerException($"Function with attribute [Builtin(\"free\")] not found", statement, CurrentFile); }

                            GenerateCodeForMacro(function, statement.Parameters, null, statement);
                            return;
                        }

                        throw new CompilerException($"Bruh. This probably not stored in heap...", deletable, CurrentFile);
                    }

                case "throw": throw new NotSupportedException($"Exceptions not supported by brainfuck");

                default: throw new CompilerException($"Unknown keyword-call \"{statement.Identifier}\"", statement.Identifier, CurrentFile);
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
                                GenerateCodeForStatement(statement.Right);
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
                                GenerateCodeForStatement(statement.Right);
                            }

                            using (Code.Block($"Set computed value to {variable.Address}"))
                            {
                                Stack.Pop(address => Code.MoveSubValue(address, variable.Address));
                            }
                        }

                        return;
                    }
                default:
                    GenerateCodeForStatement(statement.ToAssignment());
                    break;
                    //throw new CompilerException($"Unknown compound assignment operator \'{statement.Operator}\'", statement.Operator);
            }
        }
        void GenerateCodeForStatement(ShortOperatorCall statement)
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
        void GenerateCodeForStatement(VariableDeclaration statement)
        {
            if (statement.InitialValue == null) return;

            if (!Variables.TryFind(statement.VariableName.Content, out Variable variable))
            { throw new CompilerException($"Variable \"{statement.VariableName.Content}\" not found", statement.VariableName, CurrentFile); }

            if (variable.IsInitialValueSet)
            { return; }

            CompileSetter(variable, statement.InitialValue);
        }
        void GenerateCodeForStatement(FunctionCall functionCall)
        {
            /*
            if (false &&functionCall.Identifier == "Alloc" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 0)
            {
                int resultAddress = Stack.PushVirtual(1);
                // Heap.Allocate(resultAddress);
                throw new NotSupportedException($"Heap is not supported :(");
                return;
            }

            if (false &&functionCall.Identifier == "AllocFrom" &&
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

            if (functionCall.FunctionName == "sizeof")
            {
                functionCall.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

                if (functionCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": required {1} passed {functionCall.Parameters.Length}", functionCall, CurrentFile); }

                StatementWithValue param0 = functionCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                if (functionCall.SaveValue)
                { Stack.Push(param0Type.SizeOnStack); }

                return;
            }

            if (TryGetMacro(functionCall, out MacroDefinition? macro))
            {
                functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

                string? prevFile = CurrentFile;
                CurrentFile = macro.FilePath;

                InMacro.Push(true);

                Statement inlinedMacro = InlineMacro(macro, functionCall.Parameters);

                if (inlinedMacro is Block inlinedMacroBlock)
                { Compile(inlinedMacroBlock); }
                else
                { GenerateCodeForStatement(inlinedMacro); }

                InMacro.Pop();

                CurrentFile = prevFile;
                return;
            }

            TypeArguments typeArguments = new();

            if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compilableFunction.Function;
                typeArguments = compilableFunction.TypeArguments;
            }

            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

            // if (!function.Modifiers.Contains("macro"))
            // { throw new NotSupportedException($"Functions not supported by the brainfuck compiler, try using macros instead", functionCall, CurrentFile); }

            typeArguments = Utils.ConcatDictionary(typeArguments, compiledFunction.Context?.CurrentTypeArguments);

            GenerateCodeForMacro(compiledFunction, functionCall.MethodParameters, typeArguments, functionCall);
        }
        void GenerateCodeForStatement(ConstructorCall constructorCall)
        {
            var instanceType = FindType(constructorCall.TypeName);

            if (instanceType.IsStruct)
            { throw new NotImplementedException(); }

            if (!instanceType.IsClass)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }

            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.TypeName, CurrentFile));

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
                return;
            }

            if (constructorCall.Parameters.Length != constructor.ParameterCount)
            { throw new CompilerException($"Wrong number of parameters passed to \"{constructorCall.TypeName}\" constructor: required {constructor.ParameterCount} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

            TypeArguments typeArguments = new();
            if (@class.TemplateInfo != null &&
                constructorCall.TypeName is TypeInstanceSimple instanceTypeSimple &&
                instanceTypeSimple.GenericTypes != null)
            { typeArguments = @class.TemplateInfo.ToDictionary(CompiledType.FromArray(instanceTypeSimple.GenericTypes, FindType)); }

            GenerateCodeForMacro(constructor, constructorCall.Parameters, typeArguments, constructorCall);
        }
        void GenerateCodeForStatement(Literal statement)
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
                        {
                            // throw new NotSupportedException($"String literals not supported by the brainfuck compiler", statement, CurrentFile);
                            GenerateCodeForLiteralString(statement);
                            break;
                        }

                    default:
                        throw new CompilerException($"Unknown literal type {statement.Type}", statement, CurrentFile);
                }
            }
        }
        void GenerateCodeForStatement(Identifier statement)
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

            if (GetConstant(statement.Content, out DataItem constant))
            {
                using (Code.Block($"Load constant {statement.Content} (with value {constant})"))
                {
                    Stack.Push(constant);
                }

                return;
            }

            throw new CompilerException($"Variable or constant \"{statement}\" not found", statement, CurrentFile);
        }
        void GenerateCodeForStatement(OperatorCall statement)
        {
            using (Code.Block($"Expression {statement.Left} {statement.Operator} {statement.Right}"))
            {
                switch (statement.Operator.Content)
                {
                    case "==":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

                            using (Code.Block($"Move & sub right-side (from {rightAddress}) from left-side (to {leftAddress})"))
                            { Code.MoveSubValue(rightAddress, leftAddress); }

                            Stack.PopVirtual();

                            return;
                        }
                    case "*":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int tempLeftAddress = Stack.PushVirtual(1);
                            Code.CopyValue(leftAddress, tempLeftAddress);

                            Code.JumpStart(tempLeftAddress);

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

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
                            { GenerateCodeForStatement(statement.Left); }

                            int tempLeftAddress = Stack.PushVirtual(1);
                            Code.CopyValue(leftAddress, tempLeftAddress);
                            Code.LOGIC_NOT(tempLeftAddress, tempLeftAddress + 1);

                            Code.JumpStart(tempLeftAddress);

                            int rightAddress = Stack.NextAddress;
                            using (Code.Block("Compute right-side value"))
                            { GenerateCodeForStatement(statement.Right!); }

                            using (Code.Block($"Snippet AND({leftAddress} {rightAddress})"))
                            { Code.LOGIC_OR(leftAddress, rightAddress, rightAddress + 1); }

                            Stack.Pop(); // Pop rightAddress

                            Code.JumpEnd(tempLeftAddress, true);
                            Stack.PopVirtual(); // Pop tempLeftAddress

                            break;
                        }
                    case "<<":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            if (TryCompute(statement.Right, null, out var rightConst) && rightConst.Integer.HasValue)
                            {
                                Code.SetValue(rightAddress, (int)Math.Pow(2, rightConst.Integer.Value));
                            }
                            else
                            {
                                using (Code.Block("Compute right-side value"))
                                { GenerateCodeForStatement(statement.Right!); }

                                int valueTwoAddress = Stack.Push(2);

                                Code.MATH_POW(valueTwoAddress, rightAddress, valueTwoAddress + 1, valueTwoAddress + 2, valueTwoAddress + 3);

                                Stack.PopAndStore(rightAddress);
                            }

                            using (Code.Jump(rightAddress))
                            {
                                using (Code.Block($"Snippet BITSHIFT_LEFT({leftAddress} {rightAddress})"))
                                {
                                    Code.MULTIPLY(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                                }
                            }

                            Stack.Pop();

                            break;
                        }
                    case ">>":
                        {
                            int leftAddress = Stack.NextAddress;
                            using (Code.Block("Compute left-side value"))
                            { GenerateCodeForStatement(statement.Left); }

                            int rightAddress = Stack.NextAddress;
                            if (TryCompute(statement.Right, null, out var rightConst) && rightConst.Integer.HasValue)
                            {
                                Code.SetValue(rightAddress, (int)Math.Pow(2, rightConst.Integer.Value));
                            }
                            else
                            {
                                using (Code.Block("Compute right-side value"))
                                { GenerateCodeForStatement(statement.Right!); }

                                int valueTwoAddress = Stack.Push(2);

                                Code.MATH_POW(valueTwoAddress, rightAddress, valueTwoAddress + 1, valueTwoAddress + 2, valueTwoAddress + 3);

                                Stack.PopAndStore(rightAddress);
                            }

                            using (Code.Jump(rightAddress))
                            {
                                using (Code.Block($"Snippet BITSHIFT_LEFT({leftAddress} {rightAddress})"))
                                {
                                    Code.MATH_DIV(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2, rightAddress + 3, rightAddress + 4);
                                }
                            }

                            Stack.Pop();

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
                GenerateCodeForStatement(statement);
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
        void GenerateCodeForStatement(AddressGetter addressGetter)
        {
            if (addressGetter.PrevStatement is Identifier identifier)
            {
                CompiledType type = FindStatementType(identifier);

                if (!type.InHEAP)
                { throw new CompilerException($"Type {type} isn't stored in the heap", addressGetter, CurrentFile); }

                GenerateCodeForStatement(identifier);
                return;
            }

            throw new NotImplementedException();
        }
        void GenerateCodeForStatement(Pointer pointer)
        {
            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(pointer.PrevStatement);

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
        void GenerateCodeForStatement(NewInstance newInstance)
        {
            if (newInstance.TypeName is not TypeInstanceSimple instanceTypeSimple)
            { throw new NotImplementedException(); }

            CompiledType instanceType = FindType(instanceTypeSimple);

            if (instanceType.IsStruct)
            {
                instanceType.Struct.References?.Add(new DefinitionReference(instanceTypeSimple, CurrentFile));

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
                instanceType.Class.References?.Add(new DefinitionReference(instanceTypeSimple, CurrentFile));

                if (instanceType.Class.TemplateInfo != null)
                {
                    if (instanceTypeSimple.GenericTypes is null)
                    { throw new CompilerException($"No type arguments specified for class instance \"{instanceType}\"", instanceTypeSimple, CurrentFile); }

                    if (instanceType.Class.TemplateInfo.TypeParameters.Length != instanceTypeSimple.GenericTypes.Length)
                    { throw new CompilerException($"Wrong number of type arguments specified for class instance \"{instanceType}\": require {instanceType.Class.TemplateInfo.TypeParameters.Length} specified {instanceTypeSimple.GenericTypes.Length}", instanceTypeSimple, CurrentFile); }

                    CompiledType[] genericParameters = instanceTypeSimple.GenericTypes!.Select(v => new CompiledType(v, FindType)).ToArray();
                    instanceType.Class.AddTypeArguments(genericParameters);
                }
                else
                {
                    if (instanceTypeSimple.GenericTypes is not null)
                    { throw new CompilerException($"You should not specify type arguments for class instance \"{instanceType}\"", instanceTypeSimple, CurrentFile); }
                }

                int pointerAddress = Stack.NextAddress;
                Allocate(instanceType.Class.Size, newInstance);

                /*
                int currentOffset = 0;
                for (int fieldIndex = 0; fieldIndex < instanceType.Class.Fields.Length; fieldIndex++)
                {
                    CompiledField field = instanceType.Class.Fields[fieldIndex];
                    CompiledType? fieldType = field.Type;
                    if (fieldType.IsGeneric && !instanceType.Class.CurrentTypeArguments.TryGetValue(fieldType.Name, out fieldType))
                    { throw new CompilerException($"Type argument \"{fieldType?.Name}\" not found", field, instanceType.Class.FilePath); }

                    using (Code.Block($"Create Field '{field.Identifier.Content}' ({fieldIndex})"))
                    {
                        GenerateInitialValue(fieldType, j =>
                        {
                            AddComment($"Save Chunk {j}:");
                            AddInstruction(Opcode.PUSH_VALUE, currentOffset);
                            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -3);
                            AddInstruction(Opcode.MATH_ADD);
                            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
                            currentOffset++;
                        });
                    }
                }
                */

                instanceType.Class.ClearTypeArguments();
                // throw new NotSupportedException($"Not supported :(", newInstance, CurrentFile);

                if (!newInstance.SaveValue)
                { Stack.Pop(); }

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
        void GenerateCodeForStatement(Field field)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsStackArray && field.FieldName == "Length")
            {
                Stack.Push(prevType.StackArraySize);
                return;
            }

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
        void GenerateCodeForStatement(TypeCast typeCast)
        {
            Warnings.Add(new Warning($"Type-cast is not supported. I will ignore it and compile just the value", new Position(typeCast.Keyword, typeCast.Type), CurrentFile));

            GenerateCodeForStatement(typeCast.PrevStatement);
        }
        #endregion

        #region GenerateCodeForPrinter()

        void GenerateCodeForPrinter(StatementWithValue value)
        {
            if (TryCompute(value, null, out DataItem constantToPrint))
            {
                GenerateCodeForPrinter(constantToPrint);
                return;
            }

            CompiledType valueType = FindStatementType(value);
            bool isString = valueType.IsReplacedType("string");

            if (value is Literal literal && isString)
            {
                GenerateCodeForPrinter(literal.Value);
                return;
            }

            GenerateCodeForValuePrinter(value, valueType);
        }
        void GenerateCodeForPrinter(DataItem value)
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
        void GenerateCodeForPrinter(string value)
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
        void GenerateCodeForValuePrinter(StatementWithValue value, CompiledType valueType)
        {
            if (valueType.SizeOnStack != 1)
            { throw new NotSupportedException($"Only value of size 1 (not {valueType.SizeOnStack}) supported by the output printer in brainfuck", value, CurrentFile); }

            if (!valueType.IsBuiltin)
            { throw new NotSupportedException($"Only built-in types or string literals (not \"{valueType}\") supported by the output printer in brainfuck", value, CurrentFile); }

            using (Code.Block($"Print value {value} as text"))
            {
                int address = Stack.NextAddress;

                using (Code.Block($"Compute value"))
                { GenerateCodeForStatement(value); }

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

        bool CanGenerateCodeForPrinter(StatementWithValue value)
        {
            if (TryCompute(value, null, out _)) return true;

            CompiledType valueType = FindStatementType(value);
            bool isString = valueType.IsReplacedType("string");

            if (value is Literal && isString) return true;

            return CanGenerateCodeForValuePrinter(valueType);
        }
        static bool CanGenerateCodeForValuePrinter(CompiledType valueType) =>
            valueType.SizeOnStack == 1 &&
            valueType.IsBuiltin;

        /*
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
        */

        #endregion

        void Allocate(int size, IThingWithPosition position)
        {
            if (!TryGetBuiltinFunction("alloc", out CompiledFunction? allocator))
            { throw new CompilerException($"Function with attribute [Builtin(\"alloc\")] not found", position, CurrentFile); }

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForMacro(allocator, new StatementWithValue[] { Literal.CreateAnonymous(LiteralType.INT, size.ToString(), position) }, null, position);
        }

        int GenerateCodeForLiteralString(Literal literal)
            => GenerateCodeForLiteralString(literal.Value, literal);
        int GenerateCodeForLiteralString(string literal, IThingWithPosition position)
        {
            using (Code.Block($"Create String \"{literal}\""))
            {
                int pointerAddress = Stack.NextAddress;
                using (Code.Block("Allocate String object {"))
                { Allocate(1 + literal.Length, position); }

                using (Code.Block("Set String.length {"))
                {
                    int valueAddress = Stack.Push(literal.Length);
                    int pointerAddressCopy = valueAddress + 1;

                    Code.CopyValue(pointerAddress, pointerAddressCopy);

                    Heap.Set(pointerAddressCopy, valueAddress);

                    Stack.Pop();
                }

                using (Code.Block("Set string data {"))
                {
                    for (int i = 0; i < literal.Length; i++)
                    {
                        // Prepare value
                        int valueAddress = Stack.Push(literal[i]);
                        int pointerAddressCopy = valueAddress + 1;

                        // Calculate pointer
                        Code.CopyValue(pointerAddress, pointerAddressCopy);
                        Code.AddValue(pointerAddressCopy, 1 + i);

                        // Set value
                        Heap.Set(pointerAddressCopy, valueAddress);

                        Stack.Pop();
                    }
                }
                return pointerAddress;
            }
        }

        /// <param name="callerPosition">
        /// Used for exceptions
        /// </param>
        void GenerateCodeForMacro(CompiledFunction function, StatementWithValue[] parameters, TypeArguments? typeArguments, IThingWithPosition callerPosition)
        {
            if (function.CompiledAttributes.HasAttribute("StandardOutput"))
            {
                bool canPrint = true;

                foreach (StatementWithValue parameter in parameters)
                {
                    if (!CanGenerateCodeForPrinter(parameter))
                    {
                        canPrint = false;
                        break;
                    }
                }

                if (canPrint)
                {
                    foreach (StatementWithValue parameter in parameters)
                    { GenerateCodeForPrinter(parameter); }
                    return;
                }
            }

            if (function.CompiledAttributes.HasAttribute("StandardInput"))
            {
                int address = Stack.PushVirtual(1);
                Code.SetPointer(address);
                if (function.Type == Type.VOID)
                {
                    Code += ',';
                    Code.ClearValue(address);
                }
                else
                {
                    if (function.Type.SizeOnStack != 1)
                    {
                        throw new CompilerException($"Function with \"{"StandardInput"}\" must have a return type with size of 1", (function as FunctionDefinition).Type, function.FilePath);
                    }
                    Code += ',';
                }
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
            List<CompiledConstant> constantParameters = new();

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

                                constantParameters.Add(new CompiledParameterConstant(defined, constValue));
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

                    PrecompileVariable(compiledParameters, defined.Identifier.Content, new CompiledType(defined.Type, (typeName) =>
                    {
                        if (typeArguments != null && typeArguments.TryGetValue(typeName, out var type))
                        { return type; }
                        return FindType(typeName, defined.Type);
                    }), value);

                    if (!compiledParameters.TryFind(defined.Identifier.Content, out Variable variable))
                    { throw new CompilerException($"Parameter \"{defined}\" not found", defined.Identifier, CurrentFile); }

                    if (variable.Type != definedType &&
                        !definedType.IsGeneric)
                    { throw new CompilerException($"Wrong type of argument passed to function \"{function.ReadableID()}\" at index {i}: Expected {definedType}, passed {variable.Type}", passed, CurrentFile); }

                    using (Code.Block($"SET {defined.Identifier.Content} TO _something_"))
                    {
                        GenerateCodeForStatement(value);

                        using (Code.Block($"STORE LAST TO {variable.Address}"))
                        { Stack.PopAndStore(variable.Address); }
                    }
                    continue;
                }

                throw new NotImplementedException($"Unimplemented invocation parameter {passed.GetType().Name}");
            }

            TypeArguments? savedTypeArguments = null;
            if (typeArguments != null)
            { SetTypeArguments(typeArguments, out savedTypeArguments); }

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




            CompiledConstant[] savedConstants = CompiledConstants.ToArray();
            CompiledConstants.Clear();

            for (int i = 0; i < constantParameters.Count; i++)
            { CompiledConstants.Push(constantParameters[i]); }

            for (int i = 0; i < savedConstants.Length; i++)
            {
                if (GetConstant(savedConstants[i].Identifier, out _))
                { continue; }
                CompiledConstants.Push(savedConstants[i]);
            }

            using (DebugBlock(function.Block.BracketStart))
            {
                using (Code.Block($"Begin \"return\" block (depth: {ReturnTagStack.Count} (now its one more))"))
                {
                    ReturnTagStack.Push(Stack.Push(1));
                }
            }

            Compile(function.Block);

            using (DebugBlock(function.Block.BracketEnd))
            {
                if (ReturnTagStack.Pop() != Stack.LastAddress)
                { throw new InternalException(); }
                Stack.Pop();
            }

            using (DebugBlock((callerPosition is Statement statement && statement.Semicolon is not null) ? statement.Semicolon : function.Block.BracketEnd))
            {
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
            }

            InMacro.Pop();
            CurrentMacro.Pop();

            Variables.Clear();
            for (int i = 0; i < savedVariables.Length; i++)
            { Variables.Push(savedVariables[i]); }

            CompiledConstants.Clear();
            for (int i = 0; i < savedConstants.Length; i++)
            { CompiledConstants.Push(savedConstants[i]); }

            if (BreakCount.Count > 0 ||
                BreakTagStack.Count > 0)
            { throw new InternalException(); }

            BreakCount.Clear();
            for (int i = 0; i < savedBreakCount.Length; i++)
            { BreakCount.Push(savedBreakCount[i]); }

            BreakTagStack.Clear();
            for (int i = 0; i < savedBreakTagStack.Length; i++)
            { BreakTagStack.Push(savedBreakTagStack[i]); }

            if (savedTypeArguments != null)
            { SetTypeArguments(savedTypeArguments); }
        }

        /// <param name="callerPosition">
        /// Used for exceptions
        /// </param>
        void GenerateCodeForMacro(CompiledGeneralFunction function, StatementWithValue[] parameters, TypeArguments? typeArguments, IThingWithPosition callerPosition)
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
            List<CompiledConstant> constantParameters = new();

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

                                constantParameters.Add(new CompiledParameterConstant(defined, value));
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

                    PrecompileVariable(compiledParameters, defined.Identifier.Content, new CompiledType(defined.Type, (typeName) =>
                    {
                        if (typeArguments != null && typeArguments.TryGetValue(typeName, out var type))
                        { return type; }
                        return FindType(typeName, defined.Type);
                    }), value);

                    if (!compiledParameters.TryFind(defined.Identifier.Content, out Variable variable))
                    { throw new CompilerException($"Parameter \"{defined}\" not found", defined.Identifier, CurrentFile); }

                    if (variable.Type != definedType &&
                        !definedType.IsGeneric)
                    { throw new CompilerException($"Wrong type of argument passed to function \"{function.ReadableID()}\" at index {i}: Expected {definedType}, passed {variable.Type}", passed, CurrentFile); }

                    using (Code.Block($"SET {defined.Identifier.Content} TO _something_"))
                    {
                        GenerateCodeForStatement(value);

                        using (Code.Block($"STORE LAST TO {variable.Address}"))
                        { Stack.PopAndStore(variable.Address); }
                    }
                }
                else
                {
                    throw new NotImplementedException($"Unimplemented invocation parameter {passed.GetType().Name}");
                }
            }

            TypeArguments? savedTypeArguments = null;
            if (typeArguments != null)
            { SetTypeArguments(typeArguments, out savedTypeArguments); }

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




            CompiledConstant[] savedConstants = CompiledConstants.ToArray();
            CompiledConstants.Clear();

            for (int i = 0; i < constantParameters.Count; i++)
            { CompiledConstants.Push(constantParameters[i]); }

            for (int i = 0; i < savedConstants.Length; i++)
            {
                if (GetConstant(savedConstants[i].Identifier, out _))
                { continue; }
                CompiledConstants.Push(savedConstants[i]);
            }

            using (Code.Block($"Begin \"return\" block (depth: {ReturnTagStack.Count} (now its one more))"))
            {
                ReturnTagStack.Push(Stack.Push(1));
            }

            Compile(function.Block ?? throw new CompilerException($"Function \"{function.ReadableID()}\" does not have a body"));

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

            CompiledConstants.Clear();
            for (int i = 0; i < savedConstants.Length; i++)
            { CompiledConstants.Push(savedConstants[i]); }

            if (BreakCount.Count > 0 ||
                BreakTagStack.Count > 0)
            { throw new InternalException(); }

            BreakCount.Clear();
            for (int i = 0; i < savedBreakCount.Length; i++)
            { BreakCount.Push(savedBreakCount[i]); }

            BreakTagStack.Clear();
            for (int i = 0; i < savedBreakTagStack.Length; i++)
            { BreakTagStack.Push(savedBreakTagStack[i]); }

            if (savedTypeArguments != null)
            { SetTypeArguments(savedTypeArguments); }
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
    }
}