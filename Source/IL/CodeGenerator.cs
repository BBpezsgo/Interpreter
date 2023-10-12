using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace LanguageCore.IL.Compiler
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

        #endregion

        public CodeGenerator(Compiler.Result compilerResult, Settings settings) : base()
        {
            this.GeneratorSettings = settings;
        }

        public struct Result
        {
            public Token[] Tokens;

            public Warning[] Warnings;
            public Error[] Errors;
            public Assembly Assembly;
        }

        public struct Settings
        {

        }

        protected override bool GetLocalSymbolType(string symbolName, out CompiledType type)
        {
            type = null;
            return false;
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

                        break;
                    }
                default:
                    break;
            }
        }
        void Precompile(CompiledFunction function)
        {
            Precompile(function.Statements);
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
                throw new NotImplementedException();
                var elementType = arrayType.TypeParameters[0];
                return elementType.SizeOnStack;
            }

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
            LiteralType.STRING => statement.Value.Length,
            LiteralType.INT => 1,
            LiteralType.CHAR => 1,
            LiteralType.FLOAT => 1,
            LiteralType.BOOLEAN => 1,
            _ => throw new ImpossibleException($"Unknown literal type {statement.Type}"),
        };
        int GetValueSize(Identifier statement)
        {
            { throw new CompilerException($"Variable or constant \"{statement}\" not found", statement, CurrentFile); }
        }
        int GetValueSize(ConstructorCall constructorCall)
        {
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

            if (@class.CompiledAttributes.HasAttribute("Define", "array"))
            {
                throw new NotImplementedException();

                if (constructorCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"array\" constructor: requied {1} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

                var t = FindStatementType(constructorCall.Parameters[0]);
                if (t != Type.INT)
                { throw new CompilerException($"Wrong type of parameter passed to \"array\" constructor: requied {Type.INT} passed {t}", constructorCall.Parameters[0], CurrentFile); }

                throw new NotImplementedException();
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
            else if (statement is VariableDeclaration variableDeclaretion)
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
                GetFunction(statementWithValue, out CompiledFunction _f) &&
                _f.ReturnSomething)
            {
                throw new NotImplementedException();
            }
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
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"return\" (requied 0 or 1, passed {statement.Parameters.Length})", statement, CurrentFile); }

                        throw new NotImplementedException();
                    }

                case "break":
                    {
                        if (statement.Parameters.Length != 0)
                        { throw new CompilerException($"Wrong number of parameters passed to instruction \"break\" (requied 0, passed {statement.Parameters.Length})", statement, CurrentFile); }

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

                        throw new NotImplementedException();
                    }
                case "-=":
                    {
                        if (statement.Left is not Identifier variableIdentifier)
                        { throw new CompilerException($"Only variable supported :(", statement.Left, CurrentFile); }

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
                        if (statement.Left is not Identifier variableIdentifier)
                        { throw new CompilerException($"Only variable supported :(", statement.Left, CurrentFile); }

                        throw new NotImplementedException();
                    }
                case "--":
                    {
                        if (statement.Left is not Identifier variableIdentifier)
                        { throw new CompilerException($"Only variable supported :(", statement.Left, CurrentFile); }

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
            if (false &&
                functionCall.Identifier == "Alloc" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 0)
            {
                throw new NotImplementedException();
            }

            if (false &&
                functionCall.Identifier == "AllocFrom" &&
                functionCall.IsMethodCall == false &&
                functionCall.Parameters.Length == 1 && (
                    FindStatementType(functionCall.Parameters[0]).BuiltinType == Type.BYTE ||
                    FindStatementType(functionCall.Parameters[0]).BuiltinType == Type.INT
                ))
            {
                throw new NotImplementedException();
            }

            if (!GetFunction(functionCall, out CompiledFunction compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compilableFunction))
                { throw new CompilerException($"Function {functionCall.ReadableID(FindStatementType)} not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compilableFunction.Function;
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

            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.TypeName.Identifier, CurrentFile));

            if (!GetClass(constructorCall, out CompiledClass @class))
            { throw new CompilerException($"Class definition \"{constructorCall.TypeName}\" not found", constructorCall, CurrentFile); }

            if (@class.CompiledAttributes.HasAttribute("Define", "array"))
            {
                throw new NotImplementedException();

                if (constructorCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to \"array\" constructor: requied {1} passed {constructorCall.Parameters.Length}", constructorCall, CurrentFile); }

                var t = FindStatementType(constructorCall.Parameters[0]);
                if (t != Type.INT)
                { throw new CompilerException($"Wrong type of parameter passed to \"array\" constructor: requied {Type.INT} passed {t}", constructorCall.Parameters[0], CurrentFile); }

                throw new NotImplementedException();
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
            CompiledType instanceType = FindType(newInstance.TypeName);

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

        Result GenerateCode(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            Action<string, LogType> printCallback = null)
        {
            string name = "bruh";

            AssemblyName assemblyName = new()
            { Name = $"{name}_dynamic_assembly" };

            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule($"{name}_module");

            TypeBuilder typeBuilder = moduleBuilder.DefineType("Program", TypeAttributes.Public);

            MethodBuilder methodBuilder = typeBuilder.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static);

            System.Type[] parameterTypes = new System.Type[]
            {
                typeof(System.String),
            };

            System.Type consoleType = System.Type.GetType("System.Console, System.Console", true);

            MethodInfo methodInfo = consoleType.GetMethod(nameof(Console.WriteLine), parameterTypes);

            ILGenerator il = methodBuilder.GetILGenerator();

            il.Emit(OpCodes.Ldstr, "Hello World!");
            il.Emit(OpCodes.Call, methodInfo!);
            il.Emit(OpCodes.Ret);

            System.Type type = typeBuilder.CreateType();
            Assembly assembly = Assembly.GetAssembly(type!);

            return new Result()
            {
                Tokens = compilerResult.Tokens,
                Assembly = assembly,

                Warnings = this.Warnings.ToArray(),
                Errors = this.Errors.ToArray(),
            };
        }

        public static Result Generate(
            Compiler.Result compilerResult,
            Compiler.CompilerSettings settings,
            Settings generatorSettings,
            Action<string, LogType> printCallback = null)
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
