using Ansi = Win32.Console.Ansi;

namespace LanguageCore.Brainfuck.Generator;

using Compiler;
using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;

public partial class CodeGeneratorForBrainfuck : CodeGeneratorNonGeneratorBase
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
        if (CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, variableDeclaration.Identifier.Content, out _))
        { throw new CompilerException($"Variable \"{variableDeclaration.Identifier.Content}\" already defined", variableDeclaration.Identifier, CurrentFile); }

        GeneralType type;

        StatementWithValue? initialValue = variableDeclaration.InitialValue;

        if (variableDeclaration.Type == StatementKeywords.Var)
        {
            if (initialValue == null)
            { throw new CompilerException($"Variable with implicit type must have an initial value", variableDeclaration, CurrentFile); }

            type = FindStatementType(initialValue);
        }
        else
        {
            type = GeneralType.From(variableDeclaration.Type, FindType, TryCompute);
            variableDeclaration.Type.SetAnalyzedType(type);
        }

        return PrecompileVariable(CompiledVariables, variableDeclaration.Identifier.Content, type, initialValue, variableDeclaration.Modifiers.Contains(ModifierKeywords.Temp));
    }
    int PrecompileVariable(Stack<Variable> variables, string name, GeneralType type, StatementWithValue? initialValue, bool deallocateOnClean)
    {
        if (CodeGeneratorForBrainfuck.GetVariable(variables, name, out _))
        { return 0; }

        // FunctionThingDefinition? scope = (CurrentMacro.Count == 0) ? null : CurrentMacro[^1];

        if (initialValue != null)
        {
            GeneralType initialValueType = FindStatementType(initialValue, type);

            if (type is ArrayType arrayType)
            {
                if (arrayType.Of == BasicType.Char)
                {
                    if (initialValue is not Literal literal)
                    { throw new InternalException(); }
                    if (literal.Type != LiteralType.String)
                    { throw new InternalException(); }
                    if (literal.Value.Length != arrayType.Length)
                    { throw new InternalException(); }

                    using (DebugBlock(initialValue))
                    {
                        int arraySize = arrayType.Length;

                        int size = Snippets.ARRAY_SIZE(arraySize);

                        int address = Stack.PushVirtual(size);
                        variables.Push(new Variable(name, address, true, deallocateOnClean, type, size)
                        {
                            IsInitialized = true
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
                if (initialValueType.Size != type.Size)
                { throw new CompilerException($"Variable initial value type ({initialValueType}) and variable type ({type}) mismatch", initialValue, CurrentFile); }

                int address = Stack.PushVirtual(type.Size);
                variables.Push(new Variable(name, address, true, deallocateOnClean, type));
            }
        }
        else
        {
            if (type is ArrayType arrayType)
            {
                int arraySize = arrayType.Length;

                int size = Snippets.ARRAY_SIZE(arraySize);

                int address = Stack.PushVirtual(size);
                variables.Push(new Variable(name, address, true, deallocateOnClean, type, size));
            }
            else
            {
                int address = Stack.PushVirtual(type.Size);
                variables.Push(new Variable(name, address, true, deallocateOnClean, type));
            }
        }

        return 1;
    }
    #endregion

    #region GenerateCodeForSetter()

    void GenerateCodeForSetter(Statement statement, StatementWithValue value)
    {
        switch (statement)
        {
            case Identifier v: GenerateCodeForSetter(v, value); break;
            case Pointer v: GenerateCodeForSetter(v, value); break;
            case IndexCall v: GenerateCodeForSetter(v, value); break;
            case Field v: GenerateCodeForSetter(v, value); break;
            default: throw new CompilerException($"Setter for statement {statement.GetType().Name} not implemented", statement, CurrentFile);
        }
    }

    void GenerateCodeForSetter(Identifier statement, StatementWithValue value)
    {
        if (GetConstant(statement.Content, out _))
        { throw new CompilerException($"This is a constant so you can not modify it's value", statement, CurrentFile); }

        if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, statement.Content, out Variable variable))
        { throw new CompilerException($"Variable \"{statement}\" not found", statement, CurrentFile); }

        CompileSetter(variable, value);
    }

    void GenerateCodeForSetter(Field field, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(field.PrevStatement);
        GeneralType type = FindStatementType(field);

        if (prevType is PointerType pointerType)
        {
            using DebugInfoBlock debugBlock = DebugBlock(new Position(field, value));
            if (pointerType.To is not StructType structPointerType)
            { throw new CompilerException($"Could not get the field offsets of type {pointerType}", field.PrevStatement, CurrentFile); }

            if (!structPointerType.Struct.FieldOffsets.TryGetValue(field.Identifier.Content, out int fieldOffset))
            { throw new CompilerException($"Could not get the field offset of field \"{field.Identifier}\"", field.Identifier, CurrentFile); }

            if (type.Size != GetValueSize(value))
            { throw new CompilerException($"Field and value size mismatch", value, CurrentFile); }

            int _pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(field.PrevStatement);

            Code.AddValue(_pointerAddress, fieldOffset);

            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(value);

            Heap.Set(_pointerAddress, valueAddress);

            Stack.Pop(); // valueAddress
            Stack.Pop(); // _pointerAddress

            return;
        }

        if (!TryGetAddress(field, out int address, out int size))
        { throw new CompilerException($"Failed to get field address", field, CurrentFile); }

        if (size != GetValueSize(value))
        { throw new CompilerException($"Field and value size mismatch", value, CurrentFile); }

        CompileSetter(address, value);
    }

    void CompileSetter(Variable variable, StatementWithValue value)
    {
        if (value is Identifier _identifier &&
            CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, _identifier.Content, out Variable valueVariable))
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

            UndiscardVariable(CompiledVariables, variable.Name);

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

        if (VariableUses(value, variable) == 0)
        { VariableCanBeDiscarded = variable.Name; }

        using (Code.Block(this, $"Set variable \"{variable.Name}\" (at {variable.Address}) to {value}"))
        {
            if (TryCompute(value, out DataItem constantValue))
            {
                AssignTypeCheck(variable.Type, constantValue, value);

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

            if (variable.Type is ArrayType arrayType)
            {
                if (arrayType.Of == BasicType.Char)
                {
                    if (value is not Literal literal)
                    { throw new InternalException(); }
                    if (literal.Type != LiteralType.String)
                    { throw new InternalException(); }
                    if (literal.Value.Length != arrayType.Length)
                    { throw new InternalException(); }

                    int arraySize = arrayType.Length;

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

                    UndiscardVariable(CompiledVariables, variable.Name);

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

            using (Code.Block(this, $"Compute value"))
            {
                GenerateCodeForStatement(value);
            }

            using (Code.Block(this, $"Store computed value (from {Stack.LastAddress}) to {variable.Address}"))
            { Stack.PopAndStore(variable.Address); }

            UndiscardVariable(CompiledVariables, variable.Name);

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

            using (Code.ConditionalBlock(this, checkResultAddress))
            { Code.OUT_STRING(checkResultAddress, "\nOut of memory range\n"); }

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
        using (Code.Block(this, $"Set value {value} to address {address}"))
        {
            if (TryCompute(value, out DataItem constantValue))
            {
                // if (constantValue.Size != 1)
                // { throw new CompilerException($"Value size can be only 1", value, CurrentFile); }

                Code.SetValue(address, constantValue.Byte ?? (byte)0);

                Optimizations++;

                return;
            }

            int stackSize = Stack.Size;

            using (Code.Block(this, $"Compute value"))
            {
                GenerateCodeForStatement(value);
            }

            int variableSize = Stack.Size - stackSize;

            using (Code.Block(this, $"Store computed value (from {Stack.LastAddress}) to {address}"))
            { Stack.PopAndStore(address); }
        }
    }

    void GenerateCodeForSetter(IndexCall statement, StatementWithValue value)
    {
        GeneralType prevType = FindStatementType(statement.PrevStatement);
        GeneralType valueType = FindStatementType(value);

        Dictionary<string, GeneralType> typeArguments = new();
        if (!GetIndexSetter(prevType, valueType, out CompiledFunction? indexer, out _))
        {
            if (GetIndexSetterTemplate(prevType, valueType, out CompliableTemplate<CompiledFunction> indexerTemplate))
            {
                indexer = indexerTemplate.Function;
                typeArguments = indexerTemplate.TypeArguments;
            }
        }

        if (indexer is not null)
        {
            if (!indexer.CanUse(CurrentFile))
            {
                AnalysisCollection?.Errors.Add(new Error($"Function \"{indexer.ToReadable()}\" cannot be called due to its protection level", statement, CurrentFile));
                return;
            }

            typeArguments = Utils.ConcatDictionary(typeArguments, indexer.Context?.CurrentTypeArguments);

            GenerateCodeForFunction(indexer, new StatementWithValue[]
            {
                statement.PrevStatement,
                statement.Index,
                value,
            }, typeArguments, statement);

            if (!statement.SaveValue && indexer.ReturnSomething)
            { Stack.Pop(); }

            return;
        }

        if (prevType is PointerType pointerType)
        {
            int valueAddress = Stack.NextAddress;
            GenerateCodeForStatement(value);

            if (pointerType.To != valueType)
            { throw new CompilerException("Bruh", value, CurrentFile); }

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(statement.PrevStatement);

            GeneralType indexType = FindStatementType(statement.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", statement.Index, CurrentFile); }
            int indexAddress = Stack.NextAddress;
            GenerateCodeForStatement(statement.Index);

            if (pointerType.To.Size != 1)
            {
                int multiplierAddress = Stack.Push(pointerType.To.Size);
                Code.MULTIPLY(indexAddress, multiplierAddress, multiplierAddress + 1, multiplierAddress + 2);
                Stack.Pop(); // multiplierAddress
            }

            Stack.PopAndAdd(pointerAddress); // indexAddress

            Heap.Set(pointerAddress, valueAddress);

            Stack.Pop(); // pointerAddress
            Stack.Pop(); // valueAddress

            return;
        }

        if (statement.PrevStatement is not Identifier _variableIdentifier)
        { throw new NotSupportedException($"Only variable indexers supported for now", statement.PrevStatement, CurrentFile); }

        if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, _variableIdentifier.Content, out Variable variable))
        { throw new CompilerException($"Variable \"{_variableIdentifier}\" not found", _variableIdentifier, CurrentFile); }

        if (variable.IsDiscarded)
        { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", _variableIdentifier, CurrentFile); }

        using (Code.Block(this, $"Set array (variable {variable.Name}) index ({statement.Index}) (at {variable.Address}) to {value}"))
        {
            if (variable.Type is not ArrayType arrayType)
            { throw new CompilerException($"Index setter for type \"{variable.Type}\" not found", statement, CurrentFile); }

            GeneralType elementType = arrayType.Of;

            if (elementType != valueType)
            { throw new CompilerException("Bruh", value, CurrentFile); }

            int elementSize = elementType.Size;

            if (elementSize != 1)
            { throw new NotSupportedException($"I'm not smart enough to handle arrays with element sizes other than one (at least in brainfuck)", value, CurrentFile); }

            int indexAddress = Stack.NextAddress;
            using (Code.Block(this, $"Compute index"))
            { GenerateCodeForStatement(statement.Index); }

            int valueAddress = Stack.NextAddress;
            using (Code.Block(this, $"Compute value"))
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
        switch (statement)
        {
            case KeywordCall v: GenerateCodeForStatement(v); break;
            case FunctionCall v: GenerateCodeForStatement(v); break;
            case IfContainer v: GenerateCodeForStatement(v.ToLinks()); break;
            case WhileLoop v: GenerateCodeForStatement(v); break;
            case ForLoop v: GenerateCodeForStatement(v); break;
            case Literal v: GenerateCodeForStatement(v); break;
            case Identifier v: GenerateCodeForStatement(v); break;
            case BinaryOperatorCall v: GenerateCodeForStatement(v); break;
            case UnaryOperatorCall v: GenerateCodeForStatement(v); break;
            case AddressGetter v: GenerateCodeForStatement(v); break;
            case Pointer v: GenerateCodeForStatement(v); break;
            case Assignment v: GenerateCodeForStatement(v); break;
            case ShortOperatorCall v: GenerateCodeForStatement(v); break;
            case CompoundAssignment v: GenerateCodeForStatement(v); break;
            case VariableDeclaration v: GenerateCodeForStatement(v); break;
            case TypeCast v: GenerateCodeForStatement(v); break;
            case NewInstance v: GenerateCodeForStatement(v); break;
            case ConstructorCall v: GenerateCodeForStatement(v); break;
            case Field v: GenerateCodeForStatement(v); break;
            case IndexCall v: GenerateCodeForStatement(v); break;
            case AnyCall v: GenerateCodeForStatement(v); break;
            case ModifiedStatement v: GenerateCodeForStatement(v); break;
            case Block v: GenerateCodeForStatement(v); break;
            default: throw new CompilerException($"Unknown statement \"{statement.GetType().Name}\"", statement, CurrentFile);
        }
    }
    void GenerateCodeForStatement(ModifiedStatement modifiedStatement)
    {
        StatementWithValue statement = modifiedStatement.Statement;
        Token modifier = modifiedStatement.Modifier;

        if (modifier.Equals(ModifierKeywords.Ref))
        {
            throw new NotImplementedException();
        }

        if (modifier.Equals(ModifierKeywords.Temp))
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

        throw new NotSupportedException($"Function pointers not supported by brainfuck", anyCall.PrevStatement, CurrentFile);
    }
    void GenerateCodeForStatement(IndexCall indexCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(indexCall);

        GeneralType arrayType = FindStatementType(indexCall.PrevStatement);

        if (arrayType is ArrayType arrayType1)
        {
            if (!TryGetAddress(indexCall.PrevStatement, out int arrayAddress, out _))
            { throw new CompilerException($"Failed to get array address", indexCall.PrevStatement, CurrentFile); }

            GeneralType elementType = arrayType1.Of;

            int elementSize = elementType.Size;

            if (elementSize != 1)
            { throw new CompilerException($"Array element size must be 1 :(", indexCall, CurrentFile); }

            int resultAddress = Stack.PushVirtual(elementSize);

            int indexAddress = Stack.NextAddress;
            using (Code.Block(this, $"Compute index"))
            { GenerateCodeForStatement(indexCall.Index); }

            Code.ARRAY_GET(arrayAddress, indexAddress, resultAddress);

            Stack.Pop();

            return;
        }

        if (arrayType is PointerType pointerType)
        {
            int resultAddress = Stack.Push(0);

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexCall.PrevStatement);

            GeneralType indexType = FindStatementType(indexCall.Index);
            if (indexType is not BuiltinType)
            { throw new CompilerException($"Index type must be builtin (ie. \"int\") and not {indexType}", indexCall.Index, CurrentFile); }
            int indexAddress = Stack.NextAddress;
            GenerateCodeForStatement(indexCall.Index);

            {
                int multiplierAddress = Stack.Push(pointerType.To.Size);

                Code.MULTIPLY(indexAddress, multiplierAddress, multiplierAddress + 1, multiplierAddress + 2);

                Stack.Pop(); // multiplierAddress
            }

            Stack.PopAndAdd(pointerAddress); // indexAddress

            Heap.Get(pointerAddress, resultAddress);

            Stack.Pop(); // pointerAddress

            return;
        }

        GenerateCodeForStatement(new FunctionCall(
            indexCall.PrevStatement,
            Token.CreateAnonymous(BuiltinFunctionNames.IndexerGet),
            new StatementWithValue[]
            {
                indexCall.Index,
            },
            indexCall.Brackets));
    }
    void GenerateCodeForStatement(LinkedIf @if, bool linked = false)
    {
        if (TryCompute(@if.Condition, out DataItem computedCondition))
        {
            if (computedCondition)
            { GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block)); }
            else if (@if.NextLink is not null)
            { GenerateCodeForStatement(@if.NextLink); }
            return;
        }

        {
            if (@if.Condition is TypeCast _typeCast &&
                GeneralType.From(_typeCast.Type, FindType, TryCompute) is BuiltinType &&
                _typeCast.PrevStatement is Literal _literal &&
                _literal.Type == LiteralType.String)
            {
                GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block));
                return;
            }
        }

        {
            if (@if.Condition is Literal _literal &&
                _literal.Type == LiteralType.String)
            {
                GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block));
                return;
            }
        }

        using (Code.Block(this, $"If ({@if.Condition})"))
        {
            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@if.Condition); }

            using (DebugBlock(@if.Condition))
            { Code.LOGIC_MAKE_BOOL(conditionAddress, conditionAddress + 1); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            Code.CommentLine($"Pointer: {Code.Pointer}");

            using (DebugBlock(@if.Keyword))
            {
                Code.JumpStart(conditionAddress);
            }

            using (Code.Block(this, "The if statements"))
            {
                GenerateCodeForStatement(Block.CreateIfNotBlock(@if.Block));
            }

            Code.CommentLine($"Pointer: {Code.Pointer}");

            if (@if.NextLink == null)
            {
                // using (this.DebugBlock(@if.Block.BracketEnd))
                // {
                using (Code.Block(this, "Cleanup condition"))
                {
                    Code.ClearValue(conditionAddress);
                    Code.JumpEnd(conditionAddress);
                    Stack.PopVirtual();
                }
                // }
            }
            else
            {
                using (Code.Block(this, "Else"))
                {
                    // using (this.DebugBlock(@if.Block.BracketEnd))
                    // {
                    using (Code.Block(this, "Finish if statement"))
                    {
                        Code.MoveValue(conditionAddress, conditionAddress + 1);
                        Code.JumpEnd(conditionAddress);
                    }
                    Code.MoveValue(conditionAddress + 1, conditionAddress);
                    // }

                    using (DebugBlock(@if.NextLink.Keyword))
                    {
                        // using (Code.CommentBlock(this, $"Invert condition (at {conditionAddress}) result (to {conditionAddress + 1})"))
                        // { Code.LOGIC_NOT(conditionAddress, conditionAddress + 1); }

                        Code.CommentLine($"Pointer: {Code.Pointer}");

                        int elseFlagAddress = conditionAddress + 1;

                        Code.CommentLine($"ELSE flag is at {elseFlagAddress}");

                        using (Code.Block(this, "Set ELSE flag"))
                        { Code.SetValue(elseFlagAddress, 1); }

                        using (Code.Block(this, "If previous \"if\" condition is true"))
                        using (Code.ConditionalBlock(this, conditionAddress))
                        {
                            using (Code.Block(this, "Reset ELSE flag"))
                            { Code.ClearValue(elseFlagAddress); }
                        }

                        Code.MoveValue(elseFlagAddress, conditionAddress);

                        Code.CommentLine($"Pointer: {Code.Pointer}");
                    }

                    using (Code.Block(this, $"If ELSE flag set (previous \"if\" condition is false)"))
                    {
                        using (Code.LoopBlock(this, conditionAddress))
                        {
                            if (@if.NextLink is LinkedElse elseBlock)
                            {
                                using (Code.Block(this, "Block (else)"))
                                { GenerateCodeForStatement(Block.CreateIfNotBlock(elseBlock.Block)); }
                            }
                            else if (@if.NextLink is LinkedIf elseIf)
                            {
                                using (Code.Block(this, "Block (else if)"))
                                { GenerateCodeForStatement(elseIf, true); }
                            }
                            else
                            { throw new UnreachableException(); }

                            using (Code.Block(this, $"Reset ELSE flag"))
                            { Code.ClearValue(conditionAddress); }
                        }
                        Stack.PopVirtual();
                    }

                    Code.CommentLine($"Pointer: {Code.Pointer}");
                }
            }

            if (!linked)
            {
                using (DebugBlock(@if.Semicolon ?? @if.Block.Semicolon))
                {
                    ContinueControlFlowStatements(Returns, "return");
                    ContinueControlFlowStatements(Breaks, "break");
                }
            }

            Code.CommentLine($"Pointer: {Code.Pointer}");
        }
    }
    void GenerateCodeForStatement(WhileLoop @while)
    {
        using (Code.Block(this, $"While ({@while.Condition})"))
        {
            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@while.Condition); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            ControlFlowBlock? breakBlock = BeginBreakBlock(@while.Block.Brackets.Start, FindControlFlowUsage(@while.Block.Statements));

            using (Code.LoopBlock(this, conditionAddress))
            {
                using (Code.Block(this, "The while statements"))
                {
                    GenerateCodeForStatement(Block.CreateIfNotBlock(@while.Block));
                }

                using (Code.Block(this, "Compute condition again"))
                {
                    GenerateCodeForStatement(@while.Condition);
                    Stack.PopAndStore(conditionAddress);
                }

                {
                    int tempAddress = Stack.PushVirtual(1);

                    if (Returns.Count > 0)
                    {
                        Code.CopyValue(Returns.Last.FlagAddress, tempAddress);
                        Code.LOGIC_NOT(tempAddress, tempAddress + 1);
                        using (Code.ConditionalBlock(this, tempAddress))
                        { Code.SetValue(conditionAddress, 0); }
                    }

                    if (Breaks.Count > 0)
                    {
                        Code.CopyValue(Breaks.Last.FlagAddress, tempAddress);
                        Code.LOGIC_NOT(tempAddress, tempAddress + 1);
                        using (Code.ConditionalBlock(this, tempAddress))
                        { Code.SetValue(conditionAddress, 0); }
                    }

                    Stack.PopVirtual();
                }
            }

            if (breakBlock is not null)
            { FinishControlFlowStatements(Breaks.Pop(), true, "break"); }

            Stack.Pop(); // Pop Condition

            ContinueControlFlowStatements(Returns, "return");
        }
    }
    void GenerateCodeForStatement(ForLoop @for)
    {
        CodeSnapshot codeSnapshot = SnapshotCode();
        GeneratorSnapshot genSnapshot = Snapshot();
        int initialCodeLength = codeSnapshot.Code.Length;

        if (IsUnrollable(@for))
        {
            if (GenerateCodeForStatement(@for, true))
            {
                CodeSnapshot unrolledCode = SnapshotCode();

                int unrolledLength = unrolledCode.Code.Length - initialCodeLength;
                GeneratorSnapshot unrolledSnapshot = Snapshot();

                Restore(genSnapshot);
                RestoreCode(codeSnapshot);

                try
                {
                    GenerateCodeForStatement(@for, false);

                    CodeSnapshot notUnrolledCode = SnapshotCode();
                    int notUnrolledLength = notUnrolledCode.Code.Length - initialCodeLength;
                    GeneratorSnapshot notUnrolledSnapshot = Snapshot();

                    if (unrolledLength <= notUnrolledLength)
                    {
                        Restore(unrolledSnapshot);
                        RestoreCode(unrolledCode);
                    }
                    else
                    {
                        Restore(notUnrolledSnapshot);
                        RestoreCode(notUnrolledCode);
                    }
                    return;
                }
                catch (Exception)
                { }

                Restore(unrolledSnapshot);
                RestoreCode(unrolledCode);

                return;
            }
        }

        Restore(genSnapshot);
        RestoreCode(codeSnapshot);

        GenerateCodeForStatement(@for, false);
    }
    bool GenerateCodeForStatement(ForLoop @for, bool shouldUnroll)
    {
        if (shouldUnroll)
        {
            GeneratorSnapshot generatorSnapshot = Snapshot();
            CodeSnapshot codeSnapshot = SnapshotCode();

            try
            {
                Block[] unrolled = Unroll(@for, new Dictionary<StatementWithValue, DataItem>());

                for (int i = 0; i < unrolled.Length; i++)
                {
                    GenerateCodeForStatement(unrolled[i]);
                }

                return true;
            }
            catch (CompilerException)
            {
                Restore(generatorSnapshot);
                RestoreCode(codeSnapshot);
            }
        }

        using (Code.Block(this, $"For"))
        {
            VariableCleanupStack.Push(PrecompileVariable(@for.VariableDeclaration));

            using (Code.Block(this, "Variable Declaration"))
            { GenerateCodeForStatement(@for.VariableDeclaration); }

            int conditionAddress = Stack.NextAddress;
            using (Code.Block(this, "Compute condition"))
            { GenerateCodeForStatement(@for.Condition); }

            Code.CommentLine($"Condition result at {conditionAddress}");

            ControlFlowBlock? breakBlock = BeginBreakBlock(@for.Block.Brackets.Start, FindControlFlowUsage(@for.Block.Statements));

            using (Code.LoopBlock(this, conditionAddress))
            {
                using (Code.Block(this, "The while statements"))
                {
                    GenerateCodeForStatement(Block.CreateIfNotBlock(@for.Block));
                }

                using (Code.Block(this, "Compute expression"))
                {
                    GenerateCodeForStatement(@for.Expression);
                }

                using (Code.Block(this, "Compute condition again"))
                {
                    GenerateCodeForStatement(@for.Condition);
                    Stack.PopAndStore(conditionAddress);
                }

                {
                    int tempAddress = Stack.PushVirtual(1);

                    if (Returns.Count > 0)
                    {
                        Code.CopyValue(Returns.Last.FlagAddress, tempAddress);
                        Code.LOGIC_NOT(tempAddress, tempAddress + 1);
                        using (Code.ConditionalBlock(this, tempAddress))
                        { Code.SetValue(conditionAddress, 0); }
                    }

                    if (Breaks.Count > 0)
                    {
                        Code.CopyValue(Breaks.Last.FlagAddress, tempAddress);
                        Code.LOGIC_NOT(tempAddress, tempAddress + 1);
                        using (Code.ConditionalBlock(this, tempAddress))
                        { Code.SetValue(conditionAddress, 0); }
                    }

                    Stack.PopVirtual();
                }
            }

            if (breakBlock is not null)
            { FinishControlFlowStatements(Breaks.Pop(), true, "break"); }

            Stack.Pop();

            CleanupVariables(VariableCleanupStack.Pop());

            // ContinueReturnStatements();
            // ContinueBreakStatements();
        }

        return false;
    }
    void GenerateCodeForStatement(KeywordCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);
        switch (statement.Identifier.Content)
        {
            case StatementKeywords.Return:
            {
                statement.Identifier.AnalyzedType = TokenAnalyzedType.Statement;

                if (statement.Parameters.Length != 0 &&
                    statement.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 0 or 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                if (InMacro.Last)
                { throw new NotImplementedException(); }

                if (statement.Parameters.Length == 1)
                {
                    if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, ReturnVariableName, out Variable returnVariable))
                    { throw new CompilerException($"Can't return value for some reason :(", statement, CurrentFile); }

                    CompileSetter(returnVariable, statement.Parameters[0]);
                }

                if (Returns.Count == 0)
                { throw new CompilerException($"Can't return for some reason :(", statement.Identifier, CurrentFile); }

                Code.SetValue(Returns.Last.FlagAddress, 0);

                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.JumpStart(Stack.NextAddress);

                Returns.Last.PendingJumps.Last++;
                Returns.Last.Doings.Last = true;

                break;
            }

            case StatementKeywords.Break:
            {
                statement.Identifier.AnalyzedType = TokenAnalyzedType.Statement;

                if (statement.Parameters.Length != 0)
                { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 0, passed {statement.Parameters.Length})", statement, CurrentFile); }

                if (Breaks.Count == 0)
                { throw new CompilerException($"Looks like this \"{statement.Identifier}\" statement is not inside a loop", statement.Identifier, CurrentFile); }

                Code.SetValue(Breaks.Last.FlagAddress, 0);

                Code.SetPointer(Stack.NextAddress);
                Code.ClearCurrent();
                Code.JumpStart(Stack.NextAddress);

                Breaks.Last.PendingJumps.Last++;
                Breaks.Last.Doings.Last = true;

                break;
            }

            case StatementKeywords.Delete:
            {
                statement.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

                if (statement.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                StatementWithValue deletable = statement.Parameters[0];

                GenerateDeallocator(deletable);

                break;
            }

            case StatementKeywords.Throw:
            {
                if (statement.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to instruction \"{statement.Identifier}\" (required 1, passed {statement.Parameters.Length})", statement, CurrentFile); }
                GenerateCodeForPrinter(Ansi.Style(Ansi.BrightForegroundRed));
                GenerateCodeForPrinter(statement.Parameters[0]);
                GenerateCodeForPrinter(Ansi.Reset);
                Code.SetPointer(Stack.Push(1));
                Code += "[]";
                Stack.PopVirtual();
                break;
                throw new NotSupportedException($"How to make exceptions work in brainfuck? (idk)", statement.Identifier, CurrentFile);
            }

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
        {
            BinaryOperatorCall @operator = statement.GetOperatorCall();
            if (GetOperator(@operator, out _) || GetOperatorTemplate(@operator, out _))
            {
                GenerateCodeForStatement(statement.ToAssignment());
                return;
            }
        }

        using DebugInfoBlock debugBlock = DebugBlock(statement);

        switch (statement.Operator.Content)
        {
            case "+=":
            {
                if (statement.Left is not Identifier variableIdentifier)
                {
                    GenerateCodeForStatement(statement.ToAssignment());
                    return;
                    // throw new CompilerException($"Only variable supported :(", statement.Left, CurrentFile);
                }

                if (!GetVariable(CompiledVariables, variableIdentifier.Content, out Variable variable))
                { throw new CompilerException($"Variable \"{variableIdentifier}\" not found", variableIdentifier, CurrentFile); }

                if (variable.IsDiscarded)
                { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", variableIdentifier, CurrentFile); }

                if (variable.Size != 1)
                { throw new CompilerException($"Bruh", statement.Left, CurrentFile); }

                if (statement.Right == null)
                { throw new CompilerException($"Value is required for '{statement.Operator}' assignment", statement, CurrentFile); }

                if (TryCompute(statement.Right, out DataItem constantValue))
                {
                    if (variable.Type is BuiltinType builtinType)
                    { DataItem.TryCast(ref constantValue, builtinType.RuntimeType); }

                    if (variable.Type != constantValue.Type)
                    { throw new CompilerException($"Variable and value type mismatch ({variable.Type} != {constantValue.Type})", statement.Right, CurrentFile); }

                    Code.AddValue(variable.Address, constantValue);

                    Optimizations++;
                    return;
                }

                using (Code.Block(this, $"Add {statement.Right} to variable {variable.Name} (at {variable.Address})"))
                {
                    using (Code.Block(this, $"Compute value"))
                    {
                        GenerateCodeForStatement(statement.Right);
                    }

                    using (Code.Block(this, $"Set computed value to {variable.Address}"))
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

                if (!GetVariable(CompiledVariables, variableIdentifier.Content, out Variable variable))
                { throw new CompilerException($"Variable \"{variableIdentifier}\" not found", variableIdentifier, CurrentFile); }

                if (variable.IsDiscarded)
                { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", variableIdentifier, CurrentFile); }

                if (variable.Size != 1)
                { throw new CompilerException($"Bruh", variableIdentifier, CurrentFile); }

                if (statement.Right == null)
                { throw new CompilerException($"Value is required for '{statement.Operator}' assignment", statement, CurrentFile); }

                if (TryCompute(statement.Right, out DataItem constantValue))
                {
                    if (variable.Type is BuiltinType builtinType)
                    { DataItem.TryCast(ref constantValue, builtinType.RuntimeType); }

                    if (variable.Type != constantValue.Type)
                    { throw new CompilerException($"Variable and value type mismatch ({variable.Type} != {constantValue.Type})", statement.Right, CurrentFile); }

                    Code.AddValue(variable.Address, -constantValue);

                    Optimizations++;
                    return;
                }

                using (Code.Block(this, $"Add {statement.Right} to variable {variable.Name} (at {variable.Address})"))
                {
                    using (Code.Block(this, $"Compute value"))
                    {
                        GenerateCodeForStatement(statement.Right);
                    }

                    using (Code.Block(this, $"Set computed value to {variable.Address}"))
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
        {
            BinaryOperatorCall @operator = statement.GetOperatorCall();
            if (GetOperator(@operator, out _) || GetOperatorTemplate(@operator, out _))
            {
                GenerateCodeForStatement(statement.ToAssignment());
                return;
            }
        }

        using DebugInfoBlock debugBlock = DebugBlock(statement);
        switch (statement.Operator.Content)
        {
            case "++":
            {
                if (statement.Left is Identifier variableIdentifier)
                {
                    if (!GetVariable(CompiledVariables, variableIdentifier.Content, out Variable variable))
                    { throw new CompilerException($"Variable \"{variableIdentifier}\" not found", variableIdentifier, CurrentFile); }

                    if (variable.IsDiscarded)
                    { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", variableIdentifier, CurrentFile); }

                    if (variable.Size != 1)
                    { throw new CompilerException($"Bruh", statement.Left, CurrentFile); }

                    using (Code.Block(this, $"Increment variable {variable.Name} (at {variable.Address})"))
                    {
                        Code.AddValue(variable.Address, 1);
                    }

                    Optimizations++;
                    return;
                }

                GenerateCodeForStatement(statement.ToAssignment());
                return;
            }
            case "--":
            {
                if (statement.Left is Identifier variableIdentifier)
                {
                    if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, variableIdentifier.Content, out Variable variable))
                    { throw new CompilerException($"Variable \"{variableIdentifier}\" not found", variableIdentifier, CurrentFile); }

                    if (variable.IsDiscarded)
                    { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", variableIdentifier, CurrentFile); }

                    if (variable.Size != 1)
                    { throw new CompilerException($"Bruh", statement.Left, CurrentFile); }

                    using (Code.Block(this, $"Decrement variable {variable.Name} (at {variable.Address})"))
                    {
                        Code.AddValue(variable.Address, -1);
                    }

                    Optimizations++;
                    return;
                }

                GenerateCodeForStatement(statement.ToAssignment());
                return;
            }
            default:
                throw new CompilerException($"Unknown assignment operator \'{statement.Operator}\'", statement.Operator, CurrentFile);
        }
    }
    void GenerateCodeForStatement(VariableDeclaration statement)
    {
        if (statement.InitialValue == null) return;

        if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, statement.Identifier.Content, out Variable variable))
        { throw new CompilerException($"Variable \"{statement.Identifier.Content}\" not found", statement.Identifier, CurrentFile); }

        if (variable.IsInitialized)
        { return; }

        CompileSetter(variable, statement.InitialValue);
    }
    void GenerateCodeForStatement(FunctionCall functionCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(functionCall);

        if (functionCall.Identifier.Content == "sizeof")
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.Keyword;

            if (functionCall.Parameters.Length != 1)
            { throw new CompilerException($"Wrong number of parameters passed to \"sizeof\": required {1} passed {functionCall.Parameters.Length}", functionCall, CurrentFile); }

            StatementWithValue param0 = functionCall.Parameters[0];
            GeneralType param0Type = FindStatementType(param0);

            if (functionCall.SaveValue)
            { Stack.Push(param0Type.Size); }

            return;
        }

        if (TryGetMacro(functionCall, out MacroDefinition? macro))
        {
            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

            // Uri? prevFile = CurrentFile;
            // CurrentFile = macro.FilePath;

            InMacro.Push(true);

            if (!InlineMacro(macro, out Statement? inlinedMacro, functionCall.Parameters))
            { throw new CompilerException($"Failed to inline the macro", functionCall, CurrentFile); }

            if (inlinedMacro is Block inlinedMacroBlock)
            { GenerateCodeForStatement(inlinedMacroBlock); }
            else
            { GenerateCodeForStatement(inlinedMacro); }

            InMacro.Pop();

            // CurrentFile = prevFile;
            return;
        }

        Dictionary<string, GeneralType> typeArguments = new();

        if (!GetFunction(functionCall, out CompiledFunction? compiledFunction, out WillBeCompilerException? notFound))
        {
            if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
            { throw notFound.Instantiate(functionCall.Identifier, CurrentFile); }

            compiledFunction = compilableFunction.Function;
            typeArguments = compilableFunction.TypeArguments;
        }

        functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;

        if (!compiledFunction.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new Error($"Function \"{compiledFunction.ToReadable()}\" cannot be called due to its protection level", functionCall.Identifier, CurrentFile));
            return;
        }

        if (TryEvaluate(compiledFunction, functionCall.MethodParameters, out DataItem? returnValue, out Statement[]? runtimeStatements))
        {
            if (functionCall.SaveValue && returnValue.HasValue && runtimeStatements.Length == 0)
            {
                Stack.Push(returnValue.Value);
                return;
            }

            if (!functionCall.SaveValue)
            {
                Uri? savedFile = CurrentFile;
                CurrentFile = null;
                foreach (Statement runtimeStatement in runtimeStatements)
                { GenerateCodeForStatement(runtimeStatement); }
                CurrentFile = savedFile;
                return;
            }
        }

        typeArguments = Utils.ConcatDictionary(typeArguments, compiledFunction.Context?.CurrentTypeArguments);

        GenerateCodeForFunction(compiledFunction, functionCall.MethodParameters, typeArguments, functionCall);

        if (!functionCall.SaveValue && compiledFunction.ReturnSomething)
        { Stack.Pop(); }
    }
    void GenerateCodeForStatement(ConstructorCall constructorCall)
    {
        using DebugInfoBlock debugBlock = DebugBlock(constructorCall);

        GeneralType instanceType = FindType(constructorCall.Type);
        GeneralType[] parameters = FindStatementTypes(constructorCall.Parameters);

        if (instanceType is StructType structType)
        { structType.Struct?.References.Add((constructorCall.Type, CurrentFile, CurrentMacro.Last)); }

        Dictionary<string, GeneralType> typeArguments = new();

        if (!GetConstructor(instanceType, parameters, out CompiledConstructor? constructor, out WillBeCompilerException? notFound))
        {
            if (!GetConstructorTemplate(instanceType, parameters, out CompliableTemplate<CompiledConstructor> compilableGeneralFunction, out notFound))
            { throw notFound.Instantiate(constructorCall.Keyword, CurrentFile); }

            constructor = compilableGeneralFunction.Function;
            typeArguments = compilableGeneralFunction.TypeArguments;
        }

        constructor.References.Add((constructorCall, CurrentFile, CurrentMacro.Last));
        OnGotStatementType(constructorCall, constructor.Type);

        if (!constructor.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new Error($"Constructor {constructor.ToReadable()} could not be called due to its protection level", constructorCall.Type, CurrentFile));
            return;
        }

        typeArguments = Utils.ConcatDictionary(typeArguments, constructor.Context?.CurrentTypeArguments);

        GenerateCodeForFunction(constructor, constructorCall.Parameters.ToArray(), typeArguments, constructorCall);
    }
    void GenerateCodeForStatement(Literal statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        using (Code.Block(this, $"Set {statement} to address {Stack.NextAddress}"))
        {
            switch (statement.Type)
            {
                case LiteralType.Integer:
                {
                    int value = statement.GetInt();
                    Stack.Push(value);
                    break;
                }
                case LiteralType.Char:
                {
                    Stack.Push(statement.Value[0]);
                    break;
                }

                case LiteralType.Float:
                    throw new NotSupportedException($"Floats not supported by the brainfuck compiler", statement, CurrentFile);
                case LiteralType.String:
                {
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
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        if (GetVariable(CompiledVariables, statement.Content, out Variable variable))
        {
            if (variable.IsDiscarded)
            { throw new CompilerException($"Variable \"{variable.Name}\" is discarded", statement, CurrentFile); }

            int variableSize = variable.Size;

            if (variableSize <= 0)
            { throw new CompilerException($"Can't load variable \"{variable.Name}\" because it's size is {variableSize} (bruh)", statement, CurrentFile); }

            int loadTarget = Stack.PushVirtual(variableSize);

            using (Code.Block(this, $"Load variable \"{variable.Name}\" (from {variable.Address}) to {loadTarget}"))
            {
                for (int offset = 0; offset < variableSize; offset++)
                {
                    int offsettedSource = variable.Address + offset;
                    int offsettedTarget = loadTarget + offset;

                    if (VariableCanBeDiscarded != null && VariableCanBeDiscarded == variable.Name)
                    {
                        Code.MoveValue(offsettedSource, offsettedTarget);
                        DiscardVariable(CompiledVariables, variable.Name);
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

        if (GetConstant(statement.Content, out IConstant? constant))
        {
            using (Code.Block(this, $"Load constant {statement.Content} (with value {constant.Value})"))
            {
                Stack.Push(constant.Value);
            }

            return;
        }

        if (GetFunction(statement.Token.Content, out _, out _))
        { throw new NotSupportedException($"Function pointers not supported by brainfuck", statement, CurrentFile); }

        throw new CompilerException($"Symbol \"{statement}\" not found", statement, CurrentFile);
    }
    void GenerateCodeForStatement(BinaryOperatorCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        {
            Dictionary<string, GeneralType> typeArguments = new();

            if (!GetOperator(statement, out CompiledOperator? compiledOperator))
            {
                if (GetOperatorTemplate(statement, out CompliableTemplate<CompiledOperator> compilableFunction))
                {
                    compiledOperator = compilableFunction.Function;
                    typeArguments = compilableFunction.TypeArguments;
                }
            }

            if (compiledOperator is not null)
            {
                statement.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;

                if (!compiledOperator.CanUse(CurrentFile))
                {
                    AnalysisCollection?.Errors.Add(new Error($"Function \"{compiledOperator.ToReadable()}\" cannot be called due to its protection level", statement.Operator, CurrentFile));
                    return;
                }

                typeArguments = Utils.ConcatDictionary(typeArguments, compiledOperator.Context?.CurrentTypeArguments);

                GenerateCodeForFunction(compiledOperator, statement.Parameters.ToArray(), typeArguments, statement);

                if (!statement.SaveValue)
                { Stack.Pop(); }
                return;
            }
        }

        if (TryCompute(statement, out DataItem computed))
        {
            Stack.Push(computed);
            Optimizations++;
            return;
        }

        using (Code.Block(this, $"Expression {statement.Left} {statement.Operator} {statement.Right}"))
        {
            switch (statement.Operator.Content)
            {
                case "==":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, "Compute equality"))
                    {
                        Code.LOGIC_EQ(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                    }

                    Stack.Pop();

                    break;
                }
                case "+":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Move & add right-side (from {rightAddress}) to left-side (to {leftAddress})"))
                    { Code.MoveAddValue(rightAddress, leftAddress); }

                    Stack.PopVirtual();

                    break;
                }
                case "-":
                {
                    {
                        if (statement.Left is Identifier _left &&
                            CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, _left.Content, out Variable left) &&
                            !left.IsDiscarded &&
                            TryCompute(statement.Right, out DataItem right) &&
                            right.Type == RuntimeType.Byte)
                        {
                            int resultAddress = Stack.PushVirtual(1);

                            Code.CopyValueWithTemp(left.Address, Stack.NextAddress, resultAddress);

                            Code.AddValue(resultAddress, -right.VByte);

                            Optimizations++;

                            return;
                        }
                    }

                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Move & sub right-side (from {rightAddress}) from left-side (to {leftAddress})"))
                    { Code.MoveSubValue(rightAddress, leftAddress); }

                    Stack.PopVirtual();

                    return;
                }
                case "*":
                {
                    {
                        if (statement.Left is Identifier identifier1 &&
                            statement.Right is Identifier identifier2 &&
                            string.Equals(identifier1.Content, identifier2.Content))
                        {
                            int leftAddress_ = Stack.NextAddress;
                            using (Code.Block(this, "Compute left-side value (right-side is the same)"))
                            { GenerateCodeForStatement(statement.Left); }

                            using (Code.Block(this, $"Snippet MATH_MUL_SELF({leftAddress_})"))
                            {
                                Code.MATH_MUL_SELF(leftAddress_, leftAddress_ + 1, leftAddress_ + 2);
                                Optimizations++;
                                break;
                            }
                        }
                    }

                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet MULTIPLY({leftAddress} {rightAddress})"))
                    {
                        Code.MULTIPLY(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                    }

                    Stack.Pop();

                    break;
                }
                case "/":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet DIVIDE({leftAddress} {rightAddress})"))
                    {
                        Code.MATH_DIV(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2, rightAddress + 3, rightAddress + 4);
                    }

                    Stack.Pop();

                    break;
                }
                case "%":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet MOD({leftAddress} {rightAddress})"))
                    {
                        Code.MATH_MOD(leftAddress, rightAddress, rightAddress + 1);
                    }

                    Stack.Pop();

                    break;
                }
                case "<":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet LT({leftAddress} {rightAddress})"))
                    {
                        Code.LOGIC_LT(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                    }

                    Stack.Pop();

                    break;
                }
                case ">":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet MT({leftAddress} {rightAddress})"))
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
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet LTEQ({leftAddress} {rightAddress})"))
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
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet LTEQ({leftAddress} {rightAddress})"))
                    {
                        Code.LOGIC_LTEQ(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                    }

                    Stack.Pop();

                    break;
                }
                case "!=":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int rightAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute right-side value"))
                    { GenerateCodeForStatement(statement.Right); }

                    using (Code.Block(this, $"Snippet NEQ({leftAddress} {rightAddress})"))
                    {
                        Code.LOGIC_NEQ(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2);
                    }

                    Stack.Pop();

                    break;
                }
                case "&&":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int tempLeftAddress = Stack.PushVirtual(1);
                    Code.CopyValue(leftAddress, tempLeftAddress);

                    using (Code.ConditionalBlock(this, tempLeftAddress))
                    {
                        int rightAddress = Stack.NextAddress;
                        using (Code.Block(this, "Compute right-side value"))
                        { GenerateCodeForStatement(statement.Right); }

                        using (Code.Block(this, $"Snippet AND({leftAddress} {rightAddress})"))
                        { Code.LOGIC_AND(leftAddress, rightAddress, rightAddress + 1, rightAddress + 2); }

                        Stack.Pop(); // Pop rightAddress
                    }
                    Stack.PopVirtual(); // Pop tempLeftAddress

                    break;
                }
                case "||":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    int invertedLeftAddress = Stack.PushVirtual(1);
                    Code.CopyValue(leftAddress, invertedLeftAddress);
                    Code.LOGIC_NOT(invertedLeftAddress, invertedLeftAddress + 1);

                    using (Code.ConditionalBlock(this, invertedLeftAddress))
                    {
                        int rightAddress = Stack.NextAddress;
                        using (Code.Block(this, "Compute right-side value"))
                        { GenerateCodeForStatement(statement.Right); }

                        using (Code.Block(this, $"Snippet AND({leftAddress} {rightAddress})"))
                        { Code.LOGIC_OR(leftAddress, rightAddress, rightAddress + 1); }

                        Stack.Pop(); // Pop rightAddress
                    }

                    Stack.PopVirtual(); // Pop tempLeftAddress

                    break;
                }
                case "<<":
                {
                    int valueAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    if (!TryCompute(statement.Right, out DataItem offsetConst))
                    { throw new CompilerException($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, CurrentFile); }

                    if (offsetConst != 0)
                    {
                        if (!Utils.PowerOf2((int)offsetConst))
                        { throw new CompilerException($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, CurrentFile); }

                        int offsetAddress = Stack.Push((int)Math.Pow(2, (int)offsetConst));

                        using (Code.Block(this, $"Snippet MULTIPLY({valueAddress} {offsetAddress})"))
                        {
                            Code.MULTIPLY(valueAddress, offsetAddress, offsetAddress + 1, offsetAddress + 2);
                        }

                        Stack.Pop();
                    }

                    break;
                }
                case ">>":
                {
                    int valueAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    if (!TryCompute(statement.Right, out DataItem offsetConst))
                    { throw new CompilerException($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, CurrentFile); }

                    if (offsetConst != 0)
                    {
                        if (!Utils.PowerOf2((int)offsetConst))
                        { throw new CompilerException($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, CurrentFile); }

                        int offsetAddress = Stack.Push((int)Math.Pow(2, (int)offsetConst));

                        using (Code.Block(this, $"Snippet MATH_DIV({valueAddress} {offsetAddress})"))
                        {
                            Code.MATH_DIV(valueAddress, offsetAddress, offsetAddress + 1, offsetAddress + 2, offsetAddress + 3, offsetAddress + 4);
                        }

                        Stack.Pop();
                    }

                    break;
                }
                case "!":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    Code.LOGIC_NOT(leftAddress, leftAddress + 1);

                    break;
                }
                case "&":
                {
                    if (TryCompute(statement.Right, out DataItem right) && right == 1)
                    {
                        int leftAddress = Stack.NextAddress;
                        using (Code.Block(this, "Compute left-side value"))
                        { GenerateCodeForStatement(statement.Left); }

                        int rightAddress = Stack.Push(2);

                        using (Code.Block(this, $"Snippet MOD({leftAddress} {rightAddress})"))
                        {
                            Code.MATH_MOD(leftAddress, rightAddress, rightAddress + 1);
                        }

                        Stack.Pop();

                        break;
                    }
                    throw new CompilerException($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, CurrentFile);
                }
                default: throw new CompilerException($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, CurrentFile);
            }
        }
    }
    void GenerateCodeForStatement(UnaryOperatorCall statement)
    {
        using DebugInfoBlock debugBlock = DebugBlock(statement);

        {
            Dictionary<string, GeneralType> typeArguments = new();

            if (!GetOperator(statement, out CompiledOperator? compiledOperator))
            {
                if (GetOperatorTemplate(statement, out CompliableTemplate<CompiledOperator> compilableFunction))
                {
                    compiledOperator = compilableFunction.Function;
                    typeArguments = compilableFunction.TypeArguments;
                }
            }

            if (compiledOperator is not null)
            {
                statement.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;

                if (!compiledOperator.CanUse(CurrentFile))
                {
                    AnalysisCollection?.Errors.Add(new Error($"Function \"{compiledOperator.ToReadable()}\" cannot be called due to its protection level", statement.Operator, CurrentFile));
                    return;
                }

                typeArguments = Utils.ConcatDictionary(typeArguments, compiledOperator.Context?.CurrentTypeArguments);

                GenerateCodeForFunction(compiledOperator, statement.Parameters.ToArray(), typeArguments, statement);

                if (!statement.SaveValue)
                { Stack.Pop(); }
                return;
            }
        }

        if (TryCompute(statement, out DataItem computed))
        {
            Stack.Push(computed);
            Optimizations++;
            return;
        }

        using (Code.Block(this, $"Expression {statement.Left} {statement.Operator}"))
        {
            switch (statement.Operator.Content)
            {
                case "!":
                {
                    int leftAddress = Stack.NextAddress;
                    using (Code.Block(this, "Compute left-side value"))
                    { GenerateCodeForStatement(statement.Left); }

                    Code.LOGIC_NOT(leftAddress, leftAddress + 1);

                    break;
                }
                default: throw new CompilerException($"I can't make \"{statement.Operator}\" operators to work in brainfuck", statement.Operator, CurrentFile);
            }
        }
    }
    void GenerateCodeForStatement(Block block)
    {
        using ConsoleProgressBar progressBar = new(ConsoleColor.DarkGray, ShowProgress);

        using (DebugBlock(block.Brackets.Start))
        {
            VariableCleanupStack.Push(PrecompileVariables(block));

            if (Returns.Count > 0)
            {
                Returns.Last.PendingJumps.Push(0);
                Returns.Last.Doings.Push(false);
            }

            if (Breaks.Count > 0)
            {
                Breaks.Last.PendingJumps.Push(0);
                Breaks.Last.Doings.Push(false);
            }
        }

        int branchDepth = Code.BranchDepth;
        for (int i = 0; i < block.Statements.Length; i++)
        {
            if (Returns.Count > 0 && Returns.Last.Doings.Last)
            { break; }

            if (Breaks.Count > 0 && Breaks.Last.Doings.Last)
            { break; }

            progressBar.Print(i, block.Statements.Length);
            VariableCanBeDiscarded = null;
            GenerateCodeForStatement(block.Statements[i]);
            VariableCanBeDiscarded = null;
        }

        using (DebugBlock(block.Brackets.End))
        {
            if (Returns.Count > 0)
            { FinishControlFlowStatements(Returns.Last, false, "return"); }

            if (Breaks.Count > 0)
            { FinishControlFlowStatements(Breaks.Last, false, "break"); }

            CleanupVariables(VariableCleanupStack.Pop());
        }
        if (branchDepth != Code.BranchDepth)
        { throw new InternalException($"Unbalanced branches", block, CurrentFile); }
    }
    void GenerateCodeForStatement(AddressGetter addressGetter)
    {
        using DebugInfoBlock debugBlock = DebugBlock(addressGetter);

        GeneralType type = FindStatementType(addressGetter.PrevStatement);

        throw new CompilerException($"Type {type} isn't stored in the heap", addressGetter, CurrentFile);

        // GenerateCodeForStatement(addressGetter.PrevStatement);
    }
    void GenerateCodeForStatement(Pointer pointer)
    {
        using DebugInfoBlock debugBlock = DebugBlock(pointer);

        if (TryCompute(pointer, out DataItem computed))
        {
            Stack.Push(computed);
            return;
        }

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
                using (Code.CommentBlock(this, $"Load value from address {address}"))
                {
                    this.Stack.PushVirtual(1);

                    int nextAddress = Stack.NextAddress;

                    using (Code.CommentBlock(this, $"Move {address} to {nextAddress} and {nextAddress + 1}"))
                    { Code.MoveValue(address, nextAddress, nextAddress + 1); }

                    using (Code.CommentBlock(this, $"Move {nextAddress + 1} to {address}"))
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
        using DebugInfoBlock debugBlock = DebugBlock(newInstance);

        GeneralType instanceType = FindType(newInstance.Type);

        switch (instanceType)
        {
            case PointerType pointerType:
            {
                int pointerAddress = Stack.NextAddress;
                Allocate(pointerType.To.Size, newInstance);

                int temp = Stack.PushVirtual(1);
                Code.CopyValueWithTemp(pointerAddress, temp + 1, temp);

                for (int i = 0; i < pointerType.To.Size; i++)
                {
                    Heap.Set(temp, 0);
                    Code.AddValue(temp, 1);
                }

                Stack.Pop();

                if (!newInstance.SaveValue)
                { Stack.Pop(); }
                break;
            }

            case StructType structType:
            {
                structType.Struct.References.Add((newInstance.Type, CurrentFile, CurrentMacro.Last));

                int address = Stack.PushVirtual(structType.Struct.Size);

                foreach (CompiledField field in structType.Struct.Fields)
                {
                    if (field.Type is not BuiltinType builtinType)
                    { throw new NotSupportedException($"Not supported :(", field.Identifier, structType.Struct.FilePath); }
                    int offset = structType.Struct.FieldOffsets[field.Identifier.Content];

                    int offsettedAddress = address + offset;

                    switch (builtinType.Type)
                    {
                        case BasicType.Byte:
                            Code.SetValue(offsettedAddress, 0);
                            break;
                        case BasicType.Integer:
                            Code.SetValue(offsettedAddress, 0);
                            AnalysisCollection?.Warnings.Add(new Warning($"Integers not supported by the brainfuck compiler, so I converted it into byte", field.Identifier, structType.Struct.FilePath));
                            break;
                        case BasicType.Char:
                            Code.SetValue(offsettedAddress, '\0');
                            break;
                        case BasicType.Float:
                            throw new NotSupportedException($"Floats not supported by the brainfuck compiler", field.Identifier, structType.Struct.FilePath);
                        default:
                            throw new CompilerException($"Unknown field type \"{builtinType}\"", field.Identifier, structType.Struct.FilePath);
                    }
                }

                break;
            }

            default:
                throw new CompilerException($"Unknown type definition {instanceType}", newInstance.Type, CurrentFile);
        }
    }
    void GenerateCodeForStatement(Field field)
    {
        using DebugInfoBlock debugBlock = DebugBlock(field);

        GeneralType prevType = FindStatementType(field.PrevStatement);

        if (prevType is ArrayType arrayType && field.Identifier.Equals("Length"))
        {
            Stack.Push(arrayType.Length);
            return;
        }

        if (TryCompute(field, out DataItem computed))
        {
            Stack.Push(computed);
            return;
        }

        if (prevType is PointerType pointerType)
        {
            if (pointerType.To is not StructType structPointerType)
            { throw new CompilerException($"Could not get the field offsets of type {prevType}", field.PrevStatement, CurrentFile); }

            if (!structPointerType.Struct.FieldOffsets.TryGetValue(field.Identifier.Content, out int fieldOffset))
            { throw new CompilerException($"Could not get the field offset of field \"{field.Identifier}\"", field.Identifier, CurrentFile); }

            if (!structPointerType.Struct.GetField(field.Identifier.Content, out CompiledField? _field))
            { throw new CompilerException($"Could not get the field \"{field.Identifier}\"", field.Identifier, CurrentFile); }

            int resultAddress = Stack.Push(_field.Type.Size);

            int pointerAddress = Stack.NextAddress;
            GenerateCodeForStatement(field.PrevStatement);

            Code.AddValue(pointerAddress, fieldOffset);

            Heap.Get(pointerAddress, resultAddress, _field.Type.Size);

            Stack.Pop();

            return;
        }

        if (!TryGetAddress(field, out int address, out int size))
        { throw new CompilerException($"Failed to get field memory address", field, CurrentFile); }

        if (size <= 0)
        { throw new CompilerException($"Can't load field \"{field}\" because it's size is {size} (bruh)", field, CurrentFile); }

        using (Code.Block(this, $"Load field {field} (from {address})"))
        {
            int loadTarget = Stack.PushVirtual(size);

            for (int offset = 0; offset < size; offset++)
            {
                int offsettedSource = address + offset;
                int offsettedTarget = loadTarget + offset;

                Code.CopyValue(offsettedSource, offsettedTarget);
            }
        }
    }
    void GenerateCodeForStatement(TypeCast typeCast)
    {
        GeneralType statementType = FindStatementType(typeCast.PrevStatement);
        GeneralType targetType = GeneralType.From(typeCast.Type, FindType, TryCompute);
        typeCast.Type.SetAnalyzedType(targetType);
        OnGotStatementType(typeCast, targetType);

        if (statementType.Size != targetType.Size)
        { throw new CompilerException($"Can't modify the size of the value. You tried to convert from {statementType} (size of {statementType.Size}) to {targetType} (size of {targetType.Size})", new Position(typeCast.Keyword, typeCast.Type), CurrentFile); }

        // AnalysisCollection?.Warnings.Add(new Warning($"Type-cast is not supported. I will ignore it and compile just the value", new Position(typeCast.Keyword, typeCast.Type), CurrentFile));

        GenerateCodeForStatement(typeCast.PrevStatement);
    }
    #endregion

    #region GenerateCodeForPrinter()

    void GenerateCodeForPrinter(StatementWithValue value)
    {
        if (TryCompute(value, out DataItem constantToPrint))
        {
            GenerateCodeForPrinter(constantToPrint);
            return;
        }

        if (value is Literal literal &&
            literal.Type == LiteralType.String)
        {
            GenerateCodeForPrinter(literal.Value, literal.Position);
            return;
        }

        GeneralType valueType = FindStatementType(value);
        GenerateCodeForValuePrinter(value, valueType);
    }
    void GenerateCodeForPrinter(DataItem value)
    {
        int tempAddress = Stack.NextAddress;
        using (Code.Block(this, $"Print value {value.ToString(null)} (on address {tempAddress})"))
        {
            Code.SetValue(tempAddress, value);
            Code.SetPointer(tempAddress);
            Code += '.';
            Code.ClearValue(tempAddress);
            Code.SetPointer(0);
        }
    }
    void GenerateCodeForPrinter(string value) => GenerateCodeForPrinter(value, Position.UnknownPosition);
    void GenerateCodeForPrinter(string value, Position position)
    {
        using (Code.Block(this, $"Print string value \"{value}\""))
        {
            int address = Stack.NextAddress;

            Code.ClearValue(address);

            byte prevValue = 0;
            for (int i = 0; i < value.Length; i++)
            {
                using DebugInfoBlock debugBlock = DebugBlock(
                    (position == Position.UnknownPosition) ?
                    Position.UnknownPosition :
                    new Position(
                        new Range<SinglePosition>(
                            new SinglePosition(
                                position.Range.Start.Line,
                                position.Range.Start.Character + i
                            ),
                            new SinglePosition(
                                position.Range.Start.Line,
                                position.Range.Start.Character + i + 1
                            )
                        ),
                        new Range<int>(
                            position.AbsoluteRange.Start + i,
                            position.AbsoluteRange.Start + i + 1
                        )
                    ));

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
    void GenerateCodeForValuePrinter(StatementWithValue value, GeneralType valueType)
    {
        if (value is Literal literalValue &&
            literalValue.Type == LiteralType.String)
        {
            GenerateCodeForPrinter(literalValue.Value);
            return;
        }

        if (valueType.Size != 1)
        { throw new NotSupportedException($"Only value of size 1 (not {valueType.Size}) supported by the output printer in brainfuck", value, CurrentFile); }

        if (valueType is not BuiltinType builtinType)
        { throw new NotSupportedException($"Only built-in types or string literals (not \"{valueType}\") supported by the output printer in brainfuck", value, CurrentFile); }

        using (Code.Block(this, $"Print value {value} as text"))
        {
            int address = Stack.NextAddress;

            using (Code.Block(this, $"Compute value"))
            { GenerateCodeForStatement(value); }

            Code.CommentLine($"Computed value is on {address}");

            Code.SetPointer(address);

            switch (builtinType.Type)
            {
                case BasicType.Byte:
                case BasicType.Integer:
                case BasicType.Float:
                    using (Code.Block(this, "SNIPPET OUT_AS_STRING"))
                    { Code += Snippets.OUT_AS_STRING; }
                    break;
                case BasicType.Char:
                    Code += '.';
                    break;
                default:
                    throw new CompilerException($"Invalid type {valueType}", value, CurrentFile);
            }

            using (Code.Block(this, $"Clear address {address}"))
            { Code.ClearValue(address); }

            Stack.PopVirtual();

            Code.SetPointer(0);
        }
    }

    bool CanGenerateCodeForPrinter(StatementWithValue value)
    {
        if (TryCompute(value, out _)) return true;

        if (value is Literal literal && literal.Type == LiteralType.String) return true;

        GeneralType valueType = FindStatementType(value);
        return CanGenerateCodeForValuePrinter(valueType);
    }
    static bool CanGenerateCodeForValuePrinter(GeneralType valueType) =>
        valueType.Size == 1 &&
        valueType is BuiltinType;

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
            using (Code.CommentBlock(this, $"Print value {value.ValueByte}"))
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
            using (Code.CommentBlock(this, $"Print value {value.ValueInt}"))
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
            using (Code.CommentBlock(this, $"Print value '{value.ValueChar}'"))
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

        using (Code.CommentBlock(this, $"Print variable (\"{variable.Name}\") (from {variable.Address}) value"))
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
        using (Code.CommentBlock(this, $"Print {value} as raw"))
        {
            using (Code.CommentBlock(this, $"Compute value"))
            { Compile(value); }

            using (Code.CommentBlock(this, $"Print computed value"))
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

    void Allocate(int size, IPositioned position)
    {
        StatementWithValue[] parameters = new StatementWithValue[] { Literal.CreateAnonymous(LiteralType.Integer, size.ToString(CultureInfo.InvariantCulture), position) };

        if (!TryGetBuiltinFunction(BuiltinFunctions.Allocate, FindStatementTypes(parameters), out CompiledFunction? allocator))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] not found", position, CurrentFile); }
        if (!allocator.ReturnSomething)
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Allocate}\")] should return something", position, CurrentFile); }

        if (!allocator.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new Error($"Function \"{allocator.ToReadable()}\" cannot be called due to its protection level", position, CurrentFile));
            return;
        }

        if (TryEvaluate(allocator, parameters, out DataItem? returnValue, out Statement[]? runtimeStatements))
        {
            if (returnValue.HasValue && runtimeStatements.Length == 0)
            {
                Stack.Push(returnValue.Value);
                return;
            }
        }

        GenerateCodeForFunction(allocator, parameters, null, position);
        return;
    }

    void GenerateDeallocator(StatementWithValue value)
    {
        GeneralType deallocateableType = FindStatementType(value);

        StatementWithValue[] parameters = new StatementWithValue[] { value };
        GeneralType[] parameterTypes = FindStatementTypes(parameters);

        if (!TryGetBuiltinFunction(BuiltinFunctions.Free, parameterTypes, out CompiledFunction? deallocator))
        { throw new CompilerException($"Function with attribute [{AttributeConstants.BuiltinIdentifier}(\"{BuiltinFunctions.Free}\")] not found", value, CurrentFile); }

        if (deallocateableType is not PointerType)
        {
            AnalysisCollection?.Warnings.Add(new Warning($"The \"delete\" keyword-function is only working on pointers or pointer so I skip this", value, CurrentFile));
            return;
        }

        Dictionary<string, GeneralType> typeArguments = new();

        if (!GetGeneralFunction(deallocateableType, parameterTypes, BuiltinFunctionNames.Destructor, out CompiledGeneralFunction? destructor))
        {
            if (!GetGeneralFunctionTemplate(deallocateableType, parameterTypes, BuiltinFunctionNames.Destructor, out CompliableTemplate<CompiledGeneralFunction> destructorTemplate))
            {
                GenerateCodeForFunction(deallocator, parameters, null, value);
                return;
            }
            typeArguments = destructorTemplate.TypeArguments;
            destructor = destructorTemplate.Function;
        }

        if (!destructor.CanUse(CurrentFile))
        {
            AnalysisCollection?.Errors.Add(new Error($"Destructor for type {deallocateableType} cannot be called due to its protection level", value, CurrentFile));
            return;
        }

        typeArguments = Utils.ConcatDictionary(typeArguments, destructor.Context?.CurrentTypeArguments);

        GenerateCodeForFunction(destructor, new StatementWithValue[] { value }, typeArguments, value);

        if (destructor.ReturnSomething)
        { Stack.Pop(); }

        GenerateCodeForFunction(deallocator, parameters, null, value);
    }

    int GenerateCodeForLiteralString(Literal literal)
        => GenerateCodeForLiteralString(literal.Value, literal);
    int GenerateCodeForLiteralString(string literal, IPositioned position)
    {
        using DebugInfoBlock debugBlock = DebugBlock(position);

        using (Code.Block(this, $"Create String \"{literal}\""))
        {
            int pointerAddress = Stack.NextAddress;
            using (Code.Block(this, "Allocate String object {"))
            { Allocate(1 + literal.Length, position); }

            using (Code.Block(this, "Set string data {"))
            {
                for (int i = 0; i < literal.Length; i++)
                {
                    // Prepare value
                    int valueAddress = Stack.Push(literal[i]);
                    int pointerAddressCopy = valueAddress + 1;

                    // Calculate pointer
                    Code.CopyValue(pointerAddress, pointerAddressCopy);
                    Code.AddValue(pointerAddressCopy, i);

                    // Set value
                    Heap.Set(pointerAddressCopy, valueAddress);

                    Stack.Pop();
                }

                {
                    // Prepare value
                    int valueAddress = Stack.Push('\0');
                    int pointerAddressCopy = valueAddress + 1;

                    // Calculate pointer
                    Code.CopyValue(pointerAddress, pointerAddressCopy);
                    Code.AddValue(pointerAddressCopy, literal.Length);

                    // Set value
                    Heap.Set(pointerAddressCopy, valueAddress);

                    Stack.Pop();
                }
            }
            return pointerAddress;
        }
    }

    bool IsFunctionInlineable(FunctionThingDefinition function, StatementWithValue[] parameters)
    {
        if (function.Block is null ||
            !function.IsInlineable)
        { return false; }

        for (int i = 0; i < parameters.Length; i++)
        {
            if (TryCompute(parameters[i], out _))
            { continue; }
            if (parameters[i] is Literal)
            { continue; }
            return false;
        }

        return true;
    }

    void GenerateCodeForFunction(CompiledFunction function, StatementWithValue[] parameters, Dictionary<string, GeneralType>? typeArguments, IPositioned callerPosition)
    {
        if (!IsFunctionInlineable(function, parameters))
        {
            GenerateCodeForFunction_(function, parameters, typeArguments, callerPosition);
            return;
        }

        CodeSnapshot originalCode = SnapshotCode();
        GeneratorSnapshot originalSnapshot = Snapshot();
        int originalCodeLength = originalCode.Code.Length;

        CodeSnapshot? inlinedCode = null;
        int inlinedLength = default;
        GeneratorSnapshot inlinedSnapshot = default;

        try
        {
            if (InlineMacro(function, out Statement? inlined, parameters))
            {
                GenerateCodeForStatement(inlined);

                inlinedCode = SnapshotCode();
                inlinedLength = inlinedCode.Value.Code.Length - originalCodeLength;
                inlinedSnapshot = Snapshot();
            }
        }
        catch (Exception)
        { }

        Restore(originalSnapshot);
        RestoreCode(originalCode);

        GenerateCodeForFunction_(function, parameters, typeArguments, callerPosition);

        CodeSnapshot notInlinedCode = SnapshotCode();
        int notInlinedLength = notInlinedCode.Code.Length - originalCodeLength;
        GeneratorSnapshot notInlinedSnapshot = Snapshot();

        if (inlinedCode is not null &&
            inlinedLength <= notInlinedLength)
        {
            Restore(inlinedSnapshot);
            RestoreCode(inlinedCode.Value);
        }
        else
        {
            Restore(notInlinedSnapshot);
            RestoreCode(notInlinedCode);
        }
    }

    void GenerateCodeForParameterPassing<TFunction>(TFunction function, StatementWithValue[] parameters, Stack<Variable> compiledParameters, List<IConstant> constantParameters)
        where TFunction : ICompiledFunction, ISimpleReadable
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            StatementWithValue passed = parameters[i];
            ParameterDefinition defined = function.Parameters[i];

            GeneralType definedType = function.ParameterTypes[i];
            GeneralType passedType = FindStatementType(passed, definedType);

            if (passedType != definedType)
            { throw new CompilerException($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected {definedType}, passed {passedType}", passed, CurrentFile); }

            foreach (Variable compiledParameter in compiledParameters)
            {
                if (compiledParameter.Name == defined.Identifier.Content)
                { throw new CompilerException($"Parameter \"{defined}\" already defined as parameter", defined.Identifier, CurrentFile); }
            }

            foreach (IConstant constantParameter in constantParameters)
            {
                if (constantParameter.Identifier == defined.Identifier.Content)
                { throw new CompilerException($"Parameter \"{defined}\" already defined as constant", defined.Identifier, CurrentFile); }
            }

            if (defined.Modifiers.Contains(ModifierKeywords.Ref) && defined.Modifiers.Contains(ModifierKeywords.Const))
            { throw new CompilerException($"Bruh", defined.Identifier, CurrentFile); }

            bool deallocateOnClean = defined.Modifiers.Contains(ModifierKeywords.Temp);
            deallocateOnClean = deallocateOnClean && passedType is PointerType;

            if (StatementCanBeDeallocated(passed, out bool explicitDeallocate))
            {
                if (explicitDeallocate && !deallocateOnClean)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate this value: parameter definition does not have a \"{ModifierKeywords.Temp}\" modifier", passed, CurrentFile)); }
            }
            else
            {
                if (explicitDeallocate)
                { AnalysisCollection?.Warnings.Add(new Warning($"Can not deallocate {passedType}", passed, CurrentFile)); }
                deallocateOnClean = false;
            }

            if (passed is ModifiedStatement modifiedStatement)
            {
                if (!defined.Modifiers.Contains(modifiedStatement.Modifier.Content))
                { throw new CompilerException($"Invalid modifier \"{modifiedStatement.Modifier.Content}\"", modifiedStatement.Modifier, CurrentFile); }

                switch (modifiedStatement.Modifier.Content)
                {
                    case ModifierKeywords.Ref:
                    {
                        Identifier modifiedVariable = (Identifier)modifiedStatement.Statement;

                        if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, modifiedVariable.Content, out Variable v))
                        { throw new CompilerException($"Variable \"{modifiedVariable}\" not found", modifiedVariable, CurrentFile); }

                        if (v.Type != definedType)
                        { throw new CompilerException($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected {definedType}, passed {v.Type}", passed, CurrentFile); }

                        compiledParameters.Push(new Variable(defined.Identifier.Content, v.Address, false, false, v.Type, v.Size));
                        continue;
                    }
                    case ModifierKeywords.Const:
                    {
                        StatementWithValue valueStatement = modifiedStatement.Statement;
                        if (!TryCompute(valueStatement, out DataItem constValue))
                        { throw new CompilerException($"Constant parameter must have a constant value", valueStatement, CurrentFile); }

                        constantParameters.Add(new CompiledParameterConstant(constValue, defined));
                        continue;
                    }
                    case ModifierKeywords.Temp:
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
                if (defined.Modifiers.Contains(ModifierKeywords.Ref))
                { throw new CompilerException($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Ref}\" modifier", passed, CurrentFile); }

                if (defined.Modifiers.Contains(ModifierKeywords.Const))
                { throw new CompilerException($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Const}\" modifier", passed, CurrentFile); }

                PrecompileVariable(compiledParameters, defined.Identifier.Content, definedType, value, defined.Modifiers.Contains(ModifierKeywords.Temp) && deallocateOnClean);

                bool parameterFound = false;
                Variable compiledParameter = default;
                foreach (Variable compiledParameter_ in compiledParameters)
                {
                    if (compiledParameter_.Name == defined.Identifier.Content)
                    {
                        parameterFound = true;
                        compiledParameter = compiledParameter_;
                    }
                }

                if (!parameterFound)
                { throw new CompilerException($"Parameter \"{defined}\" not found", defined.Identifier, CurrentFile); }

                if (compiledParameter.Type != definedType)
                { throw new CompilerException($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected {definedType}, passed {compiledParameter.Type}", passed, CurrentFile); }

                using (Code.Block(this, $"SET {defined.Identifier.Content} TO _something_"))
                {
                    GenerateCodeForStatement(value);

                    using (Code.Block(this, $"STORE LAST TO {compiledParameter.Address}"))
                    { Stack.PopAndStore(compiledParameter.Address); }
                }
                continue;
            }

            throw new NotImplementedException($"Unimplemented invocation parameter {passed.GetType().Name}");
        }
    }

    void GenerateCodeForFunction_(CompiledFunction function, StatementWithValue[] parameters, Dictionary<string, GeneralType>? typeArguments, IPositioned callerPosition)
    {
        using DebugFunctionBlock<CompiledFunction> debugFunction = FunctionBlock(function, typeArguments);

        if (function.Attributes.HasAttribute(AttributeConstants.ExternalIdentifier, ExternalFunctionNames.StdOut))
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

        if (function.Attributes.HasAttribute(AttributeConstants.ExternalIdentifier, ExternalFunctionNames.StdIn))
        {
            int address = Stack.PushVirtual(1);
            Code.SetPointer(address);
            if (function.Type == BasicType.Void)
            {
                Code += ',';
                Code.ClearValue(address);
            }
            else
            {
                if (function.Type.Size != 1)
                {
                    throw new CompilerException($"Function with attribute \"[{AttributeConstants.ExternalIdentifier}(\"{ExternalFunctionNames.StdIn}\")]\" must have a return type with size of 1", (function as FunctionDefinition).Type, function.FilePath);
                }
                Code += ',';
            }

            return;
        }

        if (function.ParameterCount != parameters.Length)
        { throw new CompilerException($"Wrong number of parameters passed to macro \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition, CurrentFile); }

        if (function.Block is null)
        { throw new CompilerException($"Function \"{function.ToReadable()}\" does not have any body definition", callerPosition, CurrentFile); }

        using ConsoleProgressLabel progressLabel = new($"Generating macro \"{function.ToReadable(typeArguments)}\"", ConsoleColor.DarkGray, ShowProgress);

        progressLabel.Print();

        Variable? returnVariable = null;

        if (function.ReturnSomething)
        {
            GeneralType returnType = function.Type;
            returnVariable = new Variable(ReturnVariableName, Stack.PushVirtual(returnType.Size), false, false, returnType);
        }

        if (!DoRecursivityStuff(function, callerPosition))
        { return; }

        Stack<Variable> compiledParameters = new();
        List<IConstant> constantParameters = new();

        CurrentMacro.Push(function);
        InMacro.Push(false);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, constantParameters);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);
        CurrentFile = function.FilePath;
        CompiledConstants.PushRange(constantParameters);
        CompiledConstants.AddRangeIf(frame.savedConstants, v => !GetConstant(v.Identifier, out _));

        ControlFlowBlock? returnBlock = BeginReturnBlock(function.Block.Brackets.Start, FindControlFlowUsage(function.Block.Statements));

        GenerateCodeForStatement(function.Block);

        using (DebugBlock(function.Block.Brackets.End))
        {
            if (returnBlock is not null)
            {
                using (Code.Block(this, $"Finish \"return\" block"))
                {
                    if (Returns.Pop().FlagAddress != Stack.LastAddress)
                    { throw new InternalException(string.Empty, function.Block, function.FilePath); }
                    Stack.Pop();
                }
            }

            using (Code.Block(this, $"Deallocate macro variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    Variable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type is PointerType)
                    {
                        GenerateDeallocator(
                            new TypeCast(
                                new Identifier(Token.CreateAnonymous(variable.Name)),
                                Token.CreateAnonymous(StatementKeywords.As),
                                new TypeInstancePointer(TypeInstanceSimple.CreateAnonymous(TypeKeywords.Int), Token.CreateAnonymous("*", TokenType.Operator))
                                )
                            );
                    }
                }
            }

            using (Code.Block(this, $"Clean up macro variables ({CompiledVariables.Count})"))
            {
                int n = CompiledVariables.Count;
                for (int i = 0; i < n; i++)
                {
                    Variable variable = CompiledVariables.Pop();
                    if (!variable.HaveToClean) continue;
                    Stack.Pop();
                }
            }
        }

        PopStackFrame(frame);

        InMacro.Pop();
        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledOperator function, StatementWithValue[] parameters, Dictionary<string, GeneralType>? typeArguments, IPositioned callerPosition)
    {
        using DebugFunctionBlock<CompiledOperator> debugFunction = FunctionBlock(function, typeArguments);

        if (function.Attributes.HasAttribute("StandardOutput"))
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

        if (function.Attributes.HasAttribute("StandardInput"))
        {
            int address = Stack.PushVirtual(1);
            Code.SetPointer(address);
            if (function.Type == BasicType.Void)
            {
                Code += ',';
                Code.ClearValue(address);
            }
            else
            {
                if (function.Type.Size != 1)
                {
                    throw new CompilerException($"Function with attribute \"StandardInput\" must have a return type with size of 1", (function as FunctionDefinition).Type, function.FilePath);
                }
                Code += ',';
            }

            return;
        }

        if (function.ParameterCount != parameters.Length)
        { throw new CompilerException($"Wrong number of parameters passed to macro \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition, CurrentFile); }

        if (function.Block is null)
        { throw new CompilerException($"Function \"{function.ToReadable()}\" does not have any body definition", callerPosition, CurrentFile); }

        using ConsoleProgressLabel progressLabel = new($"Generating macro \"{function.ToReadable(typeArguments)}\"", ConsoleColor.DarkGray, ShowProgress);

        progressLabel.Print();

        Variable? returnVariable = null;

        if (true) // always returns something
        {
            GeneralType returnType = function.Type;
            returnVariable = new Variable(ReturnVariableName, Stack.PushVirtual(returnType.Size), false, false, returnType);
        }

        if (!DoRecursivityStuff(function, callerPosition))
        { return; }

        Stack<Variable> compiledParameters = new();
        List<IConstant> constantParameters = new();

        CurrentMacro.Push(function);
        InMacro.Push(false);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, constantParameters);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);
        CurrentFile = function.FilePath;
        CompiledConstants.PushRange(constantParameters);
        CompiledConstants.AddRangeIf(frame.savedConstants, v => !GetConstant(v.Identifier, out _));

        ControlFlowBlock? returnBlock = BeginReturnBlock(function.Block.Brackets.Start, FindControlFlowUsage(function.Block.Statements));

        GenerateCodeForStatement(function.Block);

        if (returnBlock is not null)
        {
            using (DebugBlock(function.Block.Brackets.End))
            {
                if (Returns.Pop().FlagAddress != Stack.LastAddress)
                { throw new InternalException(string.Empty, function.Block, function.FilePath); }
                Stack.Pop();
            }
        }

        using (DebugBlock((callerPosition is Statement statement && statement.Semicolon is not null) ? statement.Semicolon : function.Block.Brackets.End))
        {
            using (Code.Block(this, $"Deallocate macro variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    Variable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type is PointerType)
                    {
                        GenerateDeallocator(
                            new TypeCast(
                                new Identifier(Token.CreateAnonymous(variable.Name)),
                                Token.CreateAnonymous(StatementKeywords.As),
                                new TypeInstancePointer(TypeInstanceSimple.CreateAnonymous(TypeKeywords.Int), Token.CreateAnonymous("*", TokenType.Operator))
                                )
                            );
                    }
                }
            }

            using (Code.Block(this, $"Clean up macro variables ({CompiledVariables.Count})"))
            {
                int n = CompiledVariables.Count;
                for (int i = 0; i < n; i++)
                {
                    Variable variable = CompiledVariables.Pop();
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type is PointerType)
                    { }
                    Stack.Pop();
                }
            }
        }

        PopStackFrame(frame);

        InMacro.Pop();
        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledGeneralFunction function, StatementWithValue[] parameters, Dictionary<string, GeneralType>? typeArguments, IPositioned callerPosition)
    {
        using DebugFunctionBlock<CompiledOperator> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ParameterCount != parameters.Length)
        { throw new CompilerException($"Wrong number of parameters passed to macro \"{function.ToReadable()}\" (required {function.ParameterCount} passed {parameters.Length})", callerPosition, CurrentFile); }

        Variable? returnVariable = null;

        if (function.ReturnSomething)
        {
            GeneralType returnType = GeneralType.InsertTypeParameters(function.Type, typeArguments) ?? function.Type;
            returnVariable = new Variable(ReturnVariableName, Stack.PushVirtual(returnType.Size), false, false, returnType);
        }

        if (!DoRecursivityStuff(function, callerPosition))
        { return; }

        Stack<Variable> compiledParameters = new();
        List<IConstant> constantParameters = new();

        CurrentMacro.Push(function);
        InMacro.Push(false);

        GenerateCodeForParameterPassing(function, parameters, compiledParameters, constantParameters);

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushIf(returnVariable);
        CompiledVariables.PushRange(compiledParameters);
        CompiledConstants.PushRange(constantParameters);
        CompiledConstants.AddRangeIf(frame.savedConstants, v => !GetConstant(v.Identifier, out _));

        if (function.Block is null)
        { throw new CompilerException($"Function \"{function.ToReadable()}\" does not have a body", function, function.FilePath); }

        ControlFlowBlock? returnBlock = BeginReturnBlock(function.Block.Brackets.Start, FindControlFlowUsage(function.Block.Statements));

        GenerateCodeForStatement(function.Block ?? throw new CompilerException($"Function \"{function.ToReadable()}\" does not have a body", function, function.FilePath));

        if (returnBlock is not null)
        {
            if (Returns.Pop().FlagAddress != Stack.LastAddress)
            { throw new InternalException(); }
            Stack.Pop();
        }

        using (Code.Block(this, $"Clean up macro variables ({CompiledVariables.Count})"))
        {
            using (Code.Block(this, $"Deallocate macro variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    Variable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type is PointerType)
                    {
                        GenerateDeallocator(
                            new TypeCast(
                                new Identifier(Token.CreateAnonymous(variable.Name)),
                                Token.CreateAnonymous(StatementKeywords.As),
                                new TypeInstancePointer(TypeInstanceSimple.CreateAnonymous(TypeKeywords.Int), Token.CreateAnonymous("*", TokenType.Operator))
                                )
                            );
                    }
                }
            }

            int n = CompiledVariables.Count;
            for (int i = 0; i < n; i++)
            {
                Variable variable = CompiledVariables.Pop();
                if (!variable.HaveToClean) continue;
                Stack.Pop();
            }
        }

        PopStackFrame(frame);

        InMacro.Pop();
        CurrentMacro.Pop();
    }

    void GenerateCodeForFunction(CompiledConstructor function, StatementWithValue[] parameters, Dictionary<string, GeneralType>? typeArguments, ConstructorCall callerPosition)
    {
        using DebugFunctionBlock<CompiledOperator> debugFunction = FunctionBlock(function, typeArguments);

        if (function.ParameterCount - 1 != parameters.Length)
        { throw new CompilerException($"Wrong number of parameters passed to constructor \"{function.ToReadable()}\" (required {function.ParameterCount - 1} passed {parameters.Length})", callerPosition, CurrentFile); }

        if (function.Block is null)
        { throw new CompilerException($"Constructor \"{function.ToReadable()}\" does not have any body definition", callerPosition, CurrentFile); }

        using ConsoleProgressLabel progressLabel = new($"Generating macro \"{function.ToReadable(typeArguments)}\"", ConsoleColor.DarkGray, ShowProgress);

        progressLabel.Print();

        if (!DoRecursivityStuff(function, callerPosition))
        { return; }

        Stack<Variable> compiledParameters = new();
        List<IConstant> constantParameters = new();

        CurrentMacro.Push(function);
        InMacro.Push(false);

        NewInstance newInstance = callerPosition.ToInstantiation();

        int newInstanceAddress = Stack.NextAddress;
        GeneralType newInstanceType = FindStatementType(newInstance);
        GenerateCodeForStatement(newInstance);

        if (newInstanceType != function.ParameterTypes[0])
        { throw new CompilerException($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {0}: Expected {function.ParameterTypes[0]}, passed {newInstanceType}", newInstance, CurrentFile); }

        compiledParameters.Add(new Variable(function.Parameters[0].Identifier.Content, newInstanceAddress, false, false, newInstanceType));

        for (int i = 1; i < function.Parameters.Count; i++)
        {
            StatementWithValue passed = parameters[i - 1];
            ParameterDefinition defined = function.Parameters[i];

            GeneralType definedType = function.ParameterTypes[i];
            GeneralType passedType = FindStatementType(passed, definedType);

            if (passedType != definedType)
            { throw new CompilerException($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected {definedType}, passed {passedType}", passed, CurrentFile); }

            foreach (Variable compiledParameter in compiledParameters)
            {
                if (compiledParameter.Name == defined.Identifier.Content)
                { throw new CompilerException($"Parameter \"{defined}\" already defined as parameter", defined.Identifier, CurrentFile); }
            }

            foreach (IConstant constantParameter in constantParameters)
            {
                if (constantParameter.Identifier == defined.Identifier.Content)
                { throw new CompilerException($"Parameter \"{defined}\" already defined as constant", defined.Identifier, CurrentFile); }
            }

            if (defined.Modifiers.Contains(ModifierKeywords.Ref) && defined.Modifiers.Contains(ModifierKeywords.Const))
            { throw new CompilerException($"Bruh", defined.Identifier, CurrentFile); }

            bool deallocateOnClean = false;

            if (passed is ModifiedStatement modifiedStatement)
            {
                if (!defined.Modifiers.Contains(modifiedStatement.Modifier.Content))
                { throw new CompilerException($"Invalid modifier \"{modifiedStatement.Modifier.Content}\"", modifiedStatement.Modifier, CurrentFile); }

                switch (modifiedStatement.Modifier.Content)
                {
                    case ModifierKeywords.Ref:
                    {
                        Identifier modifiedVariable = (Identifier)modifiedStatement.Statement;

                        if (!CodeGeneratorForBrainfuck.GetVariable(CompiledVariables, modifiedVariable.Content, out Variable v))
                        { throw new CompilerException($"Variable \"{modifiedVariable}\" not found", modifiedVariable, CurrentFile); }

                        if (v.Type != definedType)
                        { throw new CompilerException($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected {definedType}, passed {v.Type}", passed, CurrentFile); }

                        compiledParameters.Push(new Variable(defined.Identifier.Content, v.Address, false, false, v.Type, v.Size));
                        continue;
                    }
                    case ModifierKeywords.Const:
                    {
                        StatementWithValue valueStatement = modifiedStatement.Statement;
                        if (!TryCompute(valueStatement, out DataItem constValue))
                        { throw new CompilerException($"Constant parameter must have a constant value", valueStatement, CurrentFile); }

                        constantParameters.Add(new CompiledParameterConstant(constValue, defined));
                        continue;
                    }
                    case ModifierKeywords.Temp:
                    {
                        deallocateOnClean = true;
                        passed = modifiedStatement.Statement;
                        break;
                    }
                    default:
                        throw new CompilerException($"Unknown identifier modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, CurrentFile);
                }
            }

            if (passed is StatementWithValue value)
            {
                if (defined.Modifiers.Contains(ModifierKeywords.Ref))
                { throw new CompilerException($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Ref}\" modifier", passed, CurrentFile); }

                if (defined.Modifiers.Contains(ModifierKeywords.Const))
                { throw new CompilerException($"You must pass the parameter \"{passed}\" with a \"{ModifierKeywords.Const}\" modifier", passed, CurrentFile); }

                PrecompileVariable(compiledParameters, defined.Identifier.Content, definedType, value, defined.Modifiers.Contains(ModifierKeywords.Temp) && deallocateOnClean);

                bool parameterFound = false;
                Variable compiledParameter = default;
                foreach (Variable compiledParameter_ in compiledParameters)
                {
                    if (compiledParameter_.Name == defined.Identifier.Content)
                    {
                        parameterFound = true;
                        compiledParameter = compiledParameter_;
                    }
                }

                if (!parameterFound)
                { throw new CompilerException($"Parameter \"{defined}\" not found", defined.Identifier, CurrentFile); }

                if (compiledParameter.Type != definedType)
                { throw new CompilerException($"Wrong type of argument passed to function \"{function.ToReadable()}\" at index {i}: Expected {definedType}, passed {compiledParameter.Type}", passed, CurrentFile); }

                using (Code.Block(this, $"SET {defined.Identifier.Content} TO _something_"))
                {
                    GenerateCodeForStatement(value);

                    using (Code.Block(this, $"STORE LAST TO {compiledParameter.Address}"))
                    { Stack.PopAndStore(compiledParameter.Address); }
                }
                continue;
            }

            throw new NotImplementedException($"Unimplemented invocation parameter {passed.GetType().Name}");
        }

        GeneratorStackFrame frame = PushStackFrame(typeArguments);
        CompiledVariables.PushRange(compiledParameters);
        CurrentFile = function.FilePath;
        CompiledConstants.PushRange(constantParameters);
        CompiledConstants.AddRangeIf(frame.savedConstants, v => !GetConstant(v.Identifier, out _));

        ControlFlowBlock? returnBlock = BeginReturnBlock(function.Block.Brackets.Start, FindControlFlowUsage(function.Block.Statements));

        GenerateCodeForStatement(function.Block);

        if (returnBlock is not null)
        {
            using (DebugBlock(function.Block.Brackets.End))
            using (Code.Block(this, $"Finish \"return\" block"))
            {
                if (Returns.Pop().FlagAddress != Stack.LastAddress)
                { throw new InternalException(string.Empty, function.Block, function.FilePath); }
                Stack.Pop();
            }
        }

        using (DebugBlock((callerPosition is Statement statement && statement.Semicolon is not null) ? statement.Semicolon : function.Block.Brackets.End))
        {
            using (Code.Block(this, $"Deallocate macro variables ({CompiledVariables.Count})"))
            {
                for (int i = 0; i < CompiledVariables.Count; i++)
                {
                    Variable variable = CompiledVariables[i];
                    if (!variable.HaveToClean) continue;
                    if (variable.DeallocateOnClean &&
                        variable.Type is PointerType)
                    {
                        GenerateDeallocator(
                            new TypeCast(
                                new Identifier(Token.CreateAnonymous(variable.Name)),
                                Token.CreateAnonymous(StatementKeywords.As),
                                new TypeInstancePointer(TypeInstanceSimple.CreateAnonymous(TypeKeywords.Int), Token.CreateAnonymous("*", TokenType.Operator))
                                )
                            );
                    }
                }
            }

            using (Code.Block(this, $"Clean up macro variables ({CompiledVariables.Count})"))
            {
                int n = CompiledVariables.Count;
                for (int i = 0; i < n; i++)
                {
                    Variable variable = CompiledVariables.Pop();
                    if (!variable.HaveToClean) continue;
                    Stack.Pop();
                }
            }
        }

        PopStackFrame(frame);

        InMacro.Pop();
        CurrentMacro.Pop();
    }

    /// <returns>
    /// Returns <see langword="true"/> if the function can be generated.
    /// </returns>
    bool DoRecursivityStuff(FunctionThingDefinition function, IPositioned callerPosition)
    {
        int depth = 0;
        for (int i = 0; i < CurrentMacro.Count; i++)
        {
            if (!object.ReferenceEquals(CurrentMacro[i], function))
            { continue; }
            depth++;

            if (MaxRecursiveDepth > 0)
            {
                if (MaxRecursiveDepth >= depth)
                {
                    return true;
                }
                else
                {
                    GenerateCodeForPrinter(Ansi.Style(Ansi.BrightForegroundRed));
                    GenerateCodeForPrinter($"Max recursivity depth ({MaxRecursiveDepth}) exceeded (\"{function.ToReadable()}\")");
                    GenerateCodeForPrinter(Ansi.Reset);
                    return false;
                }
            }

            throw new CompilerException($"Recursive macro inlining is not allowed (The macro \"{function.ToReadable()}\" used recursively)", callerPosition, CurrentFile);
        }

        return true;
    }

    void FinishControlFlowStatements(ControlFlowBlock block, bool popFlag, string kind)
    {
        int pendingJumps = block.PendingJumps.Pop();
        block.Doings.Pop();
        using (Code.Block(this, $"Finish {pendingJumps} \"{kind}\" statements"))
        {
            Code.ClearValue(Stack.NextAddress);
            Code.CommentLine($"Pointer: {Code.Pointer}");
            for (int i = 0; i < pendingJumps; i++)
            {
                Code.JumpEnd();
                Code.LineBreak();
            }
            Code.CommentLine($"Pointer: {Code.Pointer}");
        }

        if (!popFlag) return;

        using (Code.Block(this, $"Finish \"{kind}\" block"))
        {
            if (block.FlagAddress != Stack.LastAddress)
            { throw new InternalException(); }
            Stack.Pop();
        }
    }

    void ContinueControlFlowStatements(Stack<ControlFlowBlock> controlFlowBlocks, string kind)
    {
        if (controlFlowBlocks.Count == 0) return;

        using (Code.Block(this, $"Continue \"{kind}\" statements"))
        {
            Code.CopyValue(controlFlowBlocks.Last.FlagAddress, Stack.NextAddress);
            Code.JumpStart(Stack.NextAddress);
            controlFlowBlocks.Last.PendingJumps.Last++;
        }
    }

    ControlFlowBlock? BeginReturnBlock(IPositioned? positioned, ControlFlowUsage usage)
    {
        if ((usage & ControlFlowUsage.AnyReturn) == ControlFlowUsage.None)
        {
            Code.CommentLine("Doesn't begin \"return\" block");
            return null;
        }

        using (DebugBlock(positioned))
        using (Code.Block(this, $"Begin \"return\" block (depth: {Returns.Count} (now its one more))"))
        {
            int flagAddress = Stack.Push(1);
            Code.CommentLine($"Return flag is at {flagAddress}");
            ControlFlowBlock block = new(flagAddress);
            Returns.Push(block);
            return block;
        }
    }

    ControlFlowBlock? BeginBreakBlock(IPositioned? positioned, ControlFlowUsage usage)
    {
        if ((usage & ControlFlowUsage.Break) == ControlFlowUsage.None)
        {
            Code.CommentLine("Doesn't begin \"break\" block");
            return null;
        }

        using (DebugBlock(positioned))
        using (Code.Block(this, $"Begin \"break\" block (depth: {Breaks.Count} (now its one more))"))
        {
            int flagAddress = Stack.Push(1);
            Code.CommentLine($"Break flag is at {flagAddress}");
            ControlFlowBlock block = new(flagAddress);
            Breaks.Push(block);
            return block;
        }
    }
}
