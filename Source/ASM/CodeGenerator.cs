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
    using LanguageCore.Tokenizing;
    using Literal = Parser.Statement.Literal;
    using Pointer = Parser.Statement.Pointer;

    public class CodeGenerator : CodeGeneratorBase
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

        protected override bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out CompiledType? type)
        {
            type = null;
            return false;
        }

        #region Precompile
        void Precompile(IEnumerable<Statement>? statements)
        {
            if (statements == null) return;
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
                        break;
                    }
                default:
                    break;
            }
        }
        void Precompile(CompiledFunction function)
        {
            if (function.Block == null) return;
            Precompile(function.Block?.Statements);
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

            if (!GetIndexGetter(arrayType, out CompiledFunction? indexer))
            {
                if (!GetIndexGetterTemplate(arrayType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index getter for class \"{arrayType.Class.Name}\" not found", indexCall, CurrentFile); }

                indexerTemplate = AddCompilable(indexerTemplate);
                indexer = indexerTemplate.Function;
            }

            throw new NotImplementedException();
        }
        int GetValueSize(Field field)
        {
            CompiledType type = FindStatementType(field);
            throw new NotImplementedException();
        }
        int GetValueSize(NewInstance newInstance)
        {
            if (GetStruct(newInstance, out var @struct))
            {
                throw new NotImplementedException();
            }

            if (GetClass(newInstance, out _))
            {
                throw new NotImplementedException();
            }

            throw new CompilerException($"Type \"{newInstance.TypeName}\" not found", newInstance.TypeName, CurrentFile);
        }
        static int GetValueSize(Literal statement)
        {
            throw new NotImplementedException();
            return statement.Type switch
            {
                LiteralType.String => statement.Value.Length,
                LiteralType.Integer => 1,
                LiteralType.Char => 1,
                LiteralType.Float => 1,
                LiteralType.Boolean => 1,
                _ => throw new ImpossibleException($"Unknown literal type {statement.Type}"),
            };
        }
        int GetValueSize(Identifier statement)
        {
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

            throw new NotImplementedException();
        }
        int GetValueSize(FunctionCall functionCall)
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

            if (GetFunction(functionCall, out CompiledFunction? function))
            {
                if (!function.ReturnSomething)
                { return 0; }

                throw new NotImplementedException();
            }

            if (GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
            {
                if (!compilableFunction.Function.ReturnSomething)
                { return 0; }

                throw new NotImplementedException();
            }

            throw new CompilerException($"Function \"{functionCall.ReadableID(FindStatementType)}\" not found", functionCall, CurrentFile);
        }
        int GetValueSize(OperatorCall statement)
        {
            throw new NotImplementedException();
            return statement.Operator.Content switch
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
        }
        static int GetValueSize(AddressGetter _)
        {
            throw new NotImplementedException();
        }
        static int GetValueSize(Pointer _)
        {
            throw new NotImplementedException();
        }
        int GetValueSize(TypeCast typeCast) => GetValueSize(typeCast.PrevStatement);
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

        #region Compile
        void Compile(Statement statement)
        {
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
            else if (statement is AnyCall anyCall)
            { Compile(anyCall); }
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
        void Compile(AnyCall anyCall)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
            {
                Compile(functionCall);
                return;
            }

            throw new NotImplementedException();
        }
        void Compile(IndexCall indexCall)
        {
            CompiledType arrayType = FindStatementType(indexCall.PrevStatement);

            if (!arrayType.IsClass)
            { throw new CompilerException($"Index getter for type \"{arrayType.Name}\" not found", indexCall, CurrentFile); }

            if (arrayType.Class.CompiledAttributes.HasAttribute("Define", "array"))
            {
                throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
        void Compile(WhileLoop @while)
        {
            throw new NotImplementedException();
        }
        void Compile(KeywordCall statement)
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
                        throw new NotImplementedException();
                    }
                case "-=":
                    {
                        throw new NotImplementedException();
                    }
                default:
                    Compile(statement.ToAssignment());
                    break;
            }
        }
        void Compile(ShortOperatorCall statement)
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
        void Compile(VariableDeclaration statement)
        {
            if (statement.InitialValue == null) return;

            throw new NotImplementedException();
        }
        void Compile(FunctionCall functionCall)
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

                if (valueToPrint is Literal literal)
                {
                    string label = Builder.DataBuilder.NewString(literal.Value);
                    Builder.CodeBuilder.AppendInstruction("mov", "ebp", "esp");
                    Builder.CodeBuilder.AppendInstruction("sub", "esp", "4");
                    
                    Builder.CodeBuilder.AppendInstruction("push", "-11");
                    Builder.CodeBuilder.AppendInstruction("call", "_GetStdHandle@4");
                    Builder.CodeBuilder.AppendInstruction("mov", "ebx", "eax");

                    Builder.CodeBuilder.AppendInstruction("push", "0");
                    Builder.CodeBuilder.AppendInstruction("lea", "eax", "[ebp-4]");
                    Builder.CodeBuilder.AppendInstruction("push", "eax");
                    Builder.CodeBuilder.AppendInstruction("push", literal.Value.Length.ToString());
                    Builder.CodeBuilder.AppendInstruction("push", label);
                    Builder.CodeBuilder.AppendInstruction("push", "ebx");
                    Builder.CodeBuilder.AppendInstruction("call", "_WriteFile@20");
                    return;
                }

                throw new NotImplementedException();
            }

            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                Compile(functionCall.Parameters[i]);
            }

            throw new NotImplementedException();
        }
        void Compile(ConstructorCall constructorCall)
        {
            var instanceType = FindType(constructorCall.TypeName);

            if (instanceType.IsStruct)
            { throw new NotImplementedException(); }

            if (!instanceType.IsClass)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }

            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.Position.Range, CurrentFile));

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

            throw new NotImplementedException();
        }
        void Compile(Literal statement)
        {
            throw new NotImplementedException();
        }
        void Compile(Identifier statement)
        {
            throw new NotImplementedException();
        }
        void Compile(OperatorCall statement)
        {
            switch (statement.Operator.Content)
            {
                case "==":
                    {
                        throw new NotImplementedException();
                    }
                case "+":
                    {
                        throw new NotImplementedException();
                    }
                case "-":
                    {
                        throw new NotImplementedException();
                    }
                case "*":
                    {
                        throw new NotImplementedException();
                    }
                case "/":
                    {
                        throw new NotImplementedException();
                    }
                case "^":
                    {
                        throw new NotImplementedException();
                    }
                case "%":
                    {
                        throw new NotImplementedException();
                    }
                case "<":
                    {
                        throw new NotImplementedException();
                    }
                case ">":
                    {
                        throw new NotImplementedException();
                    }
                case ">=":
                    {
                        throw new NotImplementedException();
                    }
                case "<=":
                    {
                        throw new NotImplementedException();
                    }
                case "!=":
                    {
                        throw new NotImplementedException();
                    }
                case "&&":
                    {
                        throw new NotImplementedException();
                    }
                case "||":
                    {
                        throw new NotImplementedException();
                    }
                default: throw new CompilerException($"Unknown operator \"{statement.Operator}\"", statement.Operator, CurrentFile);
            }
        }
        void Compile(Block block)
        {
            foreach (Statement statement in block.Statements)
            {
                Compile(statement);
            }
        }
        void Compile(AddressGetter addressGetter)
        {
            throw new NotImplementedException();
        }
        void Compile(Pointer pointer)
        {
            throw new NotImplementedException();
        }
        void Compile(NewInstance newInstance)
        {
            throw new NotImplementedException();
        }
        void Compile(Field field)
        {
            throw new NotImplementedException();
        }
        void Compile(TypeCast typeCast)
        {
            Warnings.Add(new Warning($"Type-cast is not supported. I will ignore it and compile just the value", new Position(typeCast.Keyword, typeCast.Type), CurrentFile));

            Compile(typeCast.PrevStatement);
        }
        #endregion

        void GenerateCodeForTopLevelStatements(Statement[] statements)
        {
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
                        Builder.CodeBuilder.AppendInstruction("call", "_ExitProcess@4");
                        Builder.CodeBuilder.AppendInstruction("hlt");
                    }
                    return;
                }
                Compile(statements[i]);
            }

            Builder.CodeBuilder.AppendInstruction("push", "0");
            Builder.CodeBuilder.AppendInstruction("call", "_ExitProcess@4");
            Builder.CodeBuilder.AppendInstruction("hlt");
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
