using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1822 // Mark members as static

namespace LanguageCore.IL.Generator
{
    using Compiler;
    using Parser;
    using Parser.Statement;
    using Tokenizing;
    using Literal = Parser.Statement.Literal;
    using Pointer = Parser.Statement.Pointer;

    public struct ILGeneratorResult
    {
        public Assembly Assembly;

        [RequiresUnreferencedCode("Dynamically calling methods")]
        public readonly void Invoke() => Assembly?.GetType("Program")?.GetMethod("Main")?.Invoke(null, Array.Empty<object>());

        public Warning[] Warnings;
        public Error[] Errors;
    }

    public struct ILGeneratorSettings
    {

    }

    [RequiresDynamicCode("Generating IL code")]
    public class CodeGeneratorForIL : CodeGeneratorNonGeneratorBase
    {
        #region Fields

        readonly ILGeneratorSettings GeneratorSettings;
        readonly string AssemblyName = "Bruh";

        #endregion

        public CodeGeneratorForIL(CompilerResult compilerResult, ILGeneratorSettings settings) : base(compilerResult)
        {
            this.GeneratorSettings = settings;
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
            Precompile(function.Block?.Statements);
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

        #region Compile
        void Compile(Statement statement, ILGenerator generator)
        {
            if (statement is KeywordCall instructionStatement)
            { Compile(instructionStatement, generator); }
            else if (statement is FunctionCall functionCall)
            { Compile(functionCall, generator); }
            else if (statement is IfContainer @if)
            { Compile(@if.ToLinks(), generator); }
            else if (statement is WhileLoop @while)
            { Compile(@while, generator); }
            else if (statement is Literal literal)
            { Compile(literal, generator); }
            else if (statement is Identifier variable)
            { Compile(variable, generator); }
            else if (statement is OperatorCall expression)
            { Compile(expression, generator); }
            else if (statement is AddressGetter addressGetter)
            { Compile(addressGetter, generator); }
            else if (statement is Pointer pointer)
            { Compile(pointer, generator); }
            else if (statement is Assignment assignment)
            { Compile(assignment, generator); }
            else if (statement is ShortOperatorCall shortOperatorCall)
            { Compile(shortOperatorCall, generator); }
            else if (statement is CompoundAssignment compoundAssignment)
            { Compile(compoundAssignment, generator); }
            else if (statement is VariableDeclaration variableDeclaration)
            { Compile(variableDeclaration, generator); }
            else if (statement is TypeCast typeCast)
            { Compile(typeCast, generator); }
            else if (statement is NewInstance newInstance)
            { Compile(newInstance, generator); }
            else if (statement is ConstructorCall constructorCall)
            { Compile(constructorCall, generator); }
            else if (statement is Field field)
            { Compile(field, generator); }
            else if (statement is IndexCall indexCall)
            { Compile(indexCall, generator); }
            else if (statement is AnyCall anyCall)
            { Compile(anyCall, generator); }
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
        void Compile(AnyCall anyCall, ILGenerator generator)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
            {
                Compile(functionCall, generator);
                return;
            }

            throw new NotImplementedException();
        }
        void Compile(IndexCall indexCall, ILGenerator generator)
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
                Token.CreateAnonymous(BuiltinFunctionNames.IndexerGet),
                indexCall.BracketLeft,
                new StatementWithValue[]
                {
                    indexCall.Expression,
                },
                indexCall.BracketRight), generator);
        }
        void Compile(LinkedIf @if, ILGenerator generator)
        {
            throw new NotImplementedException();
        }
        void Compile(WhileLoop @while, ILGenerator generator)
        {
            throw new NotImplementedException();
        }
        void Compile(KeywordCall statement, ILGenerator generator)
        {
            switch (statement.Identifier.Content.ToLowerInvariant())
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
        void Compile(Assignment statement, ILGenerator generator)
        {
            if (statement.Operator.Content != "=")
            { throw new CompilerException($"Unknown assignment operator \'{statement.Operator}\'", statement.Operator, CurrentFile); }

            CompileSetter(statement.Left, statement.Right ?? throw new CompilerException($"Value is required for \'{statement.Operator}\' assignment", statement, CurrentFile));
        }
        void Compile(CompoundAssignment statement, ILGenerator generator)
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
                    Compile(statement.ToAssignment(), generator);
                    break;
            }
        }
        void Compile(ShortOperatorCall statement, ILGenerator generator)
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
        void Compile(VariableDeclaration statement, ILGenerator generator)
        {
            if (statement.InitialValue == null) return;

            throw new NotImplementedException();
        }
        void Compile(FunctionCall functionCall, ILGenerator generator)
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
                const string SystemMethodType = $"{"System"}.{nameof(Console)}";
                const string SystemMethodName = nameof(Console.Write);

                System.Type? consoleType = System.Type.GetType($"{SystemMethodType}, {SystemMethodType}", true);

                StatementWithValue parameter = functionCall.Parameters[0];
                System.Type parameterType = FindStatementType(parameter).SystemType;

                MethodInfo? methodInfo = consoleType?.GetMethod(SystemMethodName, new System.Type[]
                { parameterType, });

                if (methodInfo == null)
                { throw new CompilerException($"System function \"{typeof(void)} {SystemMethodType}.{SystemMethodName}({parameterType})\""); }

                Compile(parameter, generator);
                generator.Emit(OpCodes.Call, methodInfo);
                return;
            }

            throw new NotImplementedException();
        }
        void Compile(ConstructorCall constructorCall, ILGenerator generator)
        {
            CompiledType instanceType = FindType(constructorCall.TypeName);

            if (instanceType.IsStruct)
            { throw new NotImplementedException(); }

            if (!instanceType.IsClass)
            { throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", constructorCall.TypeName, CurrentFile); }

            instanceType.Class.References?.Add(new DefinitionReference(constructorCall.TypeName, CurrentFile));

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
        void Compile(Literal statement, ILGenerator generator)
        {
            switch (statement.Type)
            {
                case LiteralType.Integer:
                    generator.Emit(OpCodes.Ldc_I4, statement.GetInt());
                    break;
                case LiteralType.Float:
                    generator.Emit(OpCodes.Ldc_R4, statement.GetFloat());
                    break;
                case LiteralType.Boolean:
                    generator.Emit(OpCodes.Ldc_I4_S, bool.Parse(statement.Value) ? 1 : 0);
                    break;
                case LiteralType.String:
                    generator.Emit(OpCodes.Ldstr, statement.Value);
                    break;
                case LiteralType.Char:
                    generator.Emit(OpCodes.Ldc_I4, statement.Value[0]);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
        void Compile(Identifier statement, ILGenerator generator)
        {
            throw new NotImplementedException();
        }
        void Compile(OperatorCall statement, ILGenerator generator)
        {
            if (GetOperator(statement, out _))
            {
                throw new NotImplementedException();
            }

            switch (statement.Operator.Content)
            {
                case "==":
                {
                    throw new NotImplementedException();
                }
                case "+":
                {
                    Compile(statement.Left, generator);
                    Compile(statement.Right!, generator);
                    generator.Emit(OpCodes.Add);
                    break;
                }
                case "-":
                {
                    Compile(statement.Left, generator);
                    Compile(statement.Right!, generator);
                    generator.Emit(OpCodes.Sub);
                    break;
                }
                case "*":
                {
                    Compile(statement.Left, generator);
                    Compile(statement.Right!, generator);
                    generator.Emit(OpCodes.Mul);
                    break;
                }
                case "/":
                {
                    Compile(statement.Left, generator);
                    Compile(statement.Right!, generator);
                    generator.Emit(OpCodes.Div);
                    break;
                }
                case "^":
                {
                    Compile(statement.Left, generator);
                    Compile(statement.Right!, generator);
                    generator.Emit(OpCodes.Xor);
                    break;
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
        void Compile(Block block, ILGenerator generator)
        {
            foreach (Statement statement in block.Statements)
            {
                Compile(statement, generator);
            }
        }
        void Compile(AddressGetter addressGetter, ILGenerator generator)
        {
            throw new NotImplementedException();
        }
        void Compile(Pointer pointer, ILGenerator generator)
        {
            throw new NotImplementedException();
        }
        void Compile(NewInstance newInstance, ILGenerator generator)
        {
            throw new NotImplementedException();
        }
        void Compile(Field field, ILGenerator generator)
        {
            throw new NotImplementedException();
        }
        void Compile(TypeCast typeCast, ILGenerator generator)
        {
            Warnings.Add(new Warning($"Type-cast is not supported. I will ignore it and compile just the value", new Position(typeCast.Keyword, typeCast.Type), CurrentFile));

            Compile(typeCast.PrevStatement, generator);
        }
        #endregion

        void GenerateCodeForTopLevelStatements(Statement[] statements, TypeBuilder type)
        {
            MethodBuilder methodBuilder = type.DefineMethod("Main", MethodAttributes.Public | MethodAttributes.Static);

            ILGenerator il = methodBuilder.GetILGenerator();

            foreach (Statement statement in statements)
            {
                Compile(statement, il);
            }

            il.Emit(OpCodes.Ret);
        }

        void GenerateCodeForFunction(CompiledFunction function, TypeBuilder type)
        {

        }

        ILGeneratorResult GenerateCode(
            CompilerResult compilerResult,
            CompilerSettings settings,
            PrintCallback? printCallback = null)
        {
            // (this.CompiledFunctions, this.CompiledOperators, this.CompiledGeneralFunctions) = UnusedFunctionManager.RemoveUnusedFunctions(compilerResult, settings.RemoveUnusedFunctionsMaxIterations, printCallback, CompileLevel.Minimal);

            AssemblyName assemblyName = new($"{AssemblyName}Assembly");

            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);

            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule($"{AssemblyName}Module");

            TypeBuilder typeBuilder = moduleBuilder.DefineType("Program", TypeAttributes.Public);

            GenerateCodeForTopLevelStatements(compilerResult.TopLevelStatements, typeBuilder);

            System.Type type = typeBuilder.CreateType()!;
            Assembly assembly = Assembly.GetAssembly(type)!;

            return new ILGeneratorResult()
            {
                Assembly = assembly,

                Warnings = this.Warnings.ToArray(),
                Errors = this.Errors.ToArray(),
            };
        }

        public static ILGeneratorResult Generate(
            CompilerResult compilerResult,
            CompilerSettings settings,
            ILGeneratorSettings generatorSettings,
            PrintCallback? printCallback = null)
            => new CodeGeneratorForIL(compilerResult, generatorSettings)
            .GenerateCode(
                compilerResult,
                settings,
                printCallback);
    }
}
