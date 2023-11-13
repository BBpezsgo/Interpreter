using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace LanguageCore.BBCode.Compiler
{
    using ConsoleGUI;
    using Parser;
    using Parser.Statement;
    using Runtime;
    using Tokenizing;
    using LiteralStatement = Parser.Statement.Literal;

    public readonly struct BuiltinFunctionNames
    {
        public const string Destructor = "destructor";
        public const string Cloner = "clone";
        public const string Constructor = "constructor";
        public const string IndexerGet = "indexer_get";
        public const string IndexerSet = "indexer_set";
    }

    public abstract class CodeGenerator
    {
        protected readonly struct CompliableTemplate<T> where T : IDuplicatable<T>
        {
            public readonly T OriginalFunction;
            public readonly T Function;
            public readonly TypeArguments TypeArguments;

            public CompliableTemplate(T function, TypeArguments typeArguments)
            {
                OriginalFunction = function;
                Function = function.Duplicate();
                TypeArguments = typeArguments;

                FinishInitialization();
            }

            void FinishInitialization()
            {
                foreach (KeyValuePair<string, CompiledType> pair in TypeArguments)
                {
                    if (pair.Value.IsGeneric)
                    { throw new InternalException($"{nameof(pair.Value.IsGeneric)} is {true}"); }
                }

                if (Function is CompiledFunction compiledFunction)
                {
                    FinishInitialization(compiledFunction);
                }
                else if (Function is CompiledGeneralFunction compiledGeneralFunction)
                {
                    FinishInitialization(compiledGeneralFunction);
                }
            }
            void FinishInitialization(CompiledFunction compiledFunction)
            {
                for (int i = 0; i < compiledFunction.ParameterTypes.Length; i++)
                {
                    var parameterType = compiledFunction.ParameterTypes[i];

                    if (parameterType.IsGeneric)
                    {
                        if (!TypeArguments.TryGetValue(parameterType.Name, out CompiledType? bruh))
                        { throw new InternalException(); }

                        compiledFunction.ParameterTypes[i] = bruh;
                        continue;
                    }

                    if (parameterType.IsClass && parameterType.Class.TemplateInfo != null)
                    {
                        Token[] classTypeParameters = parameterType.Class.TemplateInfo.TypeParameters;

                        CompiledType[] classTypeParameterValues = new CompiledType[classTypeParameters.Length];

                        foreach (KeyValuePair<string, CompiledType> item in this.TypeArguments)
                        {
                            if (parameterType.Class.TryGetTypeArgumentIndex(item.Key, out int j))
                            { classTypeParameterValues[j] = item.Value; }
                        }

                        for (int j = 0; j < classTypeParameterValues.Length; j++)
                        {
                            if (classTypeParameterValues[j] is null ||
                                classTypeParameterValues[j].IsGeneric)
                            { throw new InternalException(); }
                        }

                        compiledFunction.ParameterTypes[i] = new CompiledType(parameterType.Class, classTypeParameterValues);
                        continue;
                    }
                }

                if (compiledFunction.Type.IsGeneric)
                {
                    if (!TypeArguments.TryGetValue(compiledFunction.Type.Name, out CompiledType? bruh))
                    { throw new InternalException(); }

                    compiledFunction.Type = bruh;
                }

                if (compiledFunction.Context != null && compiledFunction.Context.TemplateInfo is not null)
                {
                    compiledFunction.Context = compiledFunction.Context.Duplicate();
                    compiledFunction.Context.AddTypeArguments(TypeArguments);
                }
            }
            void FinishInitialization(CompiledGeneralFunction compiledGeneralFunction)
            {
                for (int i = 0; i < compiledGeneralFunction.ParameterTypes.Length; i++)
                {
                    CompiledType parameterType = compiledGeneralFunction.ParameterTypes[i];

                    if (parameterType.IsGeneric)
                    {
                        if (!TypeArguments.TryGetValue(parameterType.Name, out CompiledType? bruh))
                        { throw new InternalException(); }

                        compiledGeneralFunction.ParameterTypes[i] = bruh;
                        continue;
                    }

                    if (compiledGeneralFunction.Type.IsGeneric)
                    {
                        if (!TypeArguments.TryGetValue(compiledGeneralFunction.Type.Name, out CompiledType? bruh))
                        { throw new InternalException(); }

                        compiledGeneralFunction.Type = bruh;
                    }

                    if (parameterType.IsClass && parameterType.Class.TemplateInfo != null)
                    {
                        Token[] classTypeParameters = parameterType.Class.TemplateInfo.TypeParameters;

                        CompiledType[] classTypeParameterValues = new CompiledType[classTypeParameters.Length];

                        foreach (KeyValuePair<string, CompiledType> item in this.TypeArguments)
                        {
                            if (parameterType.Class.TryGetTypeArgumentIndex(item.Key, out int j))
                            { classTypeParameterValues[j] = item.Value; }
                        }

                        for (int j = 0; j < classTypeParameterValues.Length; j++)
                        {
                            if (classTypeParameterValues[j] is null ||
                                classTypeParameterValues[j].IsGeneric)
                            { throw new InternalException(); }
                        }

                        compiledGeneralFunction.ParameterTypes[i] = new CompiledType(parameterType.Class, classTypeParameterValues);
                        continue;
                    }
                }

                if (compiledGeneralFunction.Context != null && compiledGeneralFunction.Context.TemplateInfo is not null)
                {
                    compiledGeneralFunction.Context = compiledGeneralFunction.Context.Duplicate();
                    compiledGeneralFunction.Context.AddTypeArguments(TypeArguments);
                }
            }

            public override string ToString() => Function?.ToString() ?? "null";
        }

        protected delegate void BuiltinFunctionCompiler(params StatementWithValue[] parameters);

        protected CompiledStruct[] CompiledStructs;
        protected CompiledClass[] CompiledClasses;
        protected CompiledFunction[] CompiledFunctions;
        protected MacroDefinition[] CompiledMacros;
        protected CompiledOperator[] CompiledOperators;
        protected CompiledEnum[] CompiledEnums;
        protected CompiledGeneralFunction[] CompiledGeneralFunctions;

        protected readonly Stack<CompiledConstant> CompiledConstants;
        protected readonly Stack<int> ConstantsStack;

        protected readonly List<CompiledParameter> CompiledParameters;
        protected readonly List<CompiledVariable> CompiledVariables;

        protected IReadOnlyList<CompliableTemplate<CompiledFunction>> CompilableFunctions => compilableFunctions;
        protected IReadOnlyList<CompliableTemplate<CompiledOperator>> CompilableOperators => compilableOperators;
        protected IReadOnlyList<CompliableTemplate<CompiledGeneralFunction>> CompilableGeneralFunctions => compilableGeneralFunctions;

        readonly List<CompliableTemplate<CompiledFunction>> compilableFunctions = new();
        readonly List<CompliableTemplate<CompiledOperator>> compilableOperators = new();
        readonly List<CompliableTemplate<CompiledGeneralFunction>> compilableGeneralFunctions = new();

        protected readonly TypeArguments TypeArguments;

        protected readonly List<Error> Errors;
        protected readonly List<Warning> Warnings;
        protected readonly List<Hint> Hints;

        protected string? CurrentFile;
        protected bool InFunction;

        protected CodeGenerator()
        {
            CompiledStructs = Array.Empty<CompiledStruct>();
            CompiledClasses = Array.Empty<CompiledClass>();
            CompiledFunctions = Array.Empty<CompiledFunction>();
            CompiledMacros = Array.Empty<MacroDefinition>();
            CompiledOperators = Array.Empty<CompiledOperator>();
            CompiledGeneralFunctions = Array.Empty<CompiledGeneralFunction>();
            CompiledEnums = Array.Empty<CompiledEnum>();

            Errors = new List<Error>();
            Warnings = new List<Warning>();
            Hints = new List<Hint>();

            CurrentFile = null;
            InFunction = false;

            TypeArguments = new TypeArguments();

            CompiledConstants = new Stack<CompiledConstant>();
            ConstantsStack = new Stack<int>();

            CompiledVariables = new List<CompiledVariable>();
            CompiledParameters = new List<CompiledParameter>();

            compilableFunctions = new List<CompliableTemplate<CompiledFunction>>();
            compilableOperators = new List<CompliableTemplate<CompiledOperator>>();
            compilableGeneralFunctions = new List<CompliableTemplate<CompiledGeneralFunction>>();
        }

        protected CodeGenerator(Compiler.Result compilerResult) : this()
        {
            CompiledStructs = compilerResult.Structs;
            CompiledClasses = compilerResult.Classes;
            CompiledFunctions = compilerResult.Functions;
            CompiledMacros = compilerResult.Macros;
            CompiledOperators = compilerResult.Operators;
            CompiledGeneralFunctions = compilerResult.GeneralFunctions;
            CompiledEnums = compilerResult.Enums;
        }

        #region Helper Functions

        protected int CompileConstants(IEnumerable<Statement> statements)
        {
            int count = 0;
            foreach (Statement statement in statements)
            {
                if (statement is not VariableDeclaration variableDeclaration ||
                    !variableDeclaration.Modifiers.Contains("const"))
                { continue; }

                if (variableDeclaration.InitialValue == null)
                { throw new CompilerException($"Constant value must have initial value", variableDeclaration, variableDeclaration.FilePath); }
                RuntimeType? expectedType = null;

                if (variableDeclaration.Type != "var")
                {
                    CompiledType constantType = new(variableDeclaration.Type, FindType, TryCompute);

                    if (!constantType.IsBuiltin)
                    { throw new NotSupportedException($"Only builtin types supported as a constant value"); }

                    expectedType = constantType.RuntimeType;
                }

                if (!TryCompute(variableDeclaration.InitialValue, expectedType, out DataItem constantValue))
                { throw new CompilerException($"Constant value must be evaluated at compile-time", variableDeclaration.InitialValue, variableDeclaration.FilePath); }

                if (GetConstant(variableDeclaration.VariableName.Content, out _))
                { throw new CompilerException($"Constant \"{variableDeclaration.VariableName}\" already defined", variableDeclaration.VariableName, variableDeclaration.FilePath); }

                CompiledConstants.Push(new CompiledVariableConstant(variableDeclaration, constantValue));
                count++;
            }
            ConstantsStack.Push(count);
            return count;
        }
        protected void CleanupConstants()
        {
            int count = ConstantsStack.Pop();
            CompiledConstants.Pop(count);
        }

        protected bool GetConstant(string identifier, out DataItem value)
        {
            bool success = false;
            value = default;

            foreach (CompiledConstant constant in CompiledConstants)
            {
                if (constant.Identifier == identifier)
                {
                    if (success)
                    { throw new CompilerException($"Constant \"{constant.Identifier}\" defined more than once", constant, constant.FilePath); }

                    value = constant.Value;
                    success = true;
                }
            }

            return success;
        }

        protected bool StatementCanBeDeallocated(StatementWithValue statement)
            => StatementCanBeDeallocated(statement, out _);
        protected bool StatementCanBeDeallocated(StatementWithValue statement, out bool explicitly)
        {
            if (statement is ModifiedStatement modifiedStatement &&
                modifiedStatement.Modifier == "temp")
            {
                if (modifiedStatement.Statement is LiteralStatement ||
                    modifiedStatement.Statement is OperatorCall)
                {
                    Hints.Add(new Hint($"Unnecessary explicit temp modifier (this kind of statements (\"{modifiedStatement.Statement.GetType().Name}\") are implicitly deallocated)", modifiedStatement.Modifier, CurrentFile));
                }

                explicitly = true;
                return true;
            }

            if (statement is LiteralStatement)
            {
                explicitly = false;
                return true;
            }

            if (statement is OperatorCall)
            {
                explicitly = false;
                return true;
            }

            explicitly = default;
            return false;
        }

        protected CompliableTemplate<CompiledFunction> AddCompilable(CompliableTemplate<CompiledFunction> compilable)
        {
            for (int i = 0; i < compilableFunctions.Count; i++)
            {
                if (compilableFunctions[i].Function.IsSame(compilable.Function))
                { return compilableFunctions[i]; }
            }
            compilableFunctions.Add(compilable);
            return compilable;
        }

        protected CompliableTemplate<CompiledOperator> AddCompilable(CompliableTemplate<CompiledOperator> compilable)
        {
            for (int i = 0; i < compilableOperators.Count; i++)
            {
                if (compilableOperators[i].Function.IsSame(compilable.Function))
                { return compilableOperators[i]; }
            }
            compilableOperators.Add(compilable);
            return compilable;
        }

        protected CompliableTemplate<CompiledGeneralFunction> AddCompilable(CompliableTemplate<CompiledGeneralFunction> compilable)
        {
            for (int i = 0; i < compilableGeneralFunctions.Count; i++)
            {
                if (compilableGeneralFunctions[i].Function.IsSame(compilable.Function))
                { return compilableGeneralFunctions[i]; }
            }
            compilableGeneralFunctions.Add(compilable);
            return compilable;
        }

        protected void SetTypeArguments(TypeArguments typeArguments)
        {
            TypeArguments.Clear();
            foreach (KeyValuePair<string, CompiledType> typeArgument in typeArguments)
            { TypeArguments.Add(typeArgument.Key, typeArgument.Value); }
        }

        protected void SetTypeArguments(TypeArguments typeArguments, out TypeArguments replaced)
        {
            replaced = new TypeArguments(TypeArguments);
            TypeArguments.Clear();
            foreach (KeyValuePair<string, CompiledType> typeArgument in typeArguments)
            { TypeArguments.Add(typeArgument.Key, typeArgument.Value); }
        }

        public static bool SameType(CompiledEnum @enum, CompiledType type)
        {
            if (!type.IsBuiltin) return false;
            RuntimeType runtimeType;
            try
            { runtimeType = type.RuntimeType; }
            catch (NotImplementedException)
            { return false; }

            for (int i = 0; i < @enum.Members.Length; i++)
            {
                if (@enum.Members[i].ComputedValue.Type != runtimeType)
                { return false; }
            }

            return true;
        }

        /// <param name="position">
        /// Used for exceptions
        /// </param>
        /// <exception cref="CompilerException"/>
        protected bool TryFindReplacedType(string builtinName, [NotNullWhen(true)] out CompiledType? type)
        {
            type = null;

            string? replacedName = TypeDefinitionReplacer(builtinName);

            if (replacedName == null)
            { return false; }

            try { type = FindType(replacedName, null); }
            catch (Exception) { }

            return type is not null;
        }

        /// <param name="position">
        /// Used for exceptions
        /// </param>
        /// <exception cref="CompilerException"/>
        protected CompiledType FindReplacedType(string builtinName, IThingWithPosition position)
        {
            string? replacedName = TypeDefinitionReplacer(builtinName);

            if (replacedName == null)
            { throw new CompilerException($"Type replacer \"{builtinName}\" not found. Define a type with an attribute [Define(\"{builtinName}\")] to use it as a {builtinName}", position, CurrentFile); }

            return FindType(replacedName, position);
        }

        protected string? TypeDefinitionReplacer(string? typeName)
        {
            foreach (CompiledStruct @struct in CompiledStructs)
            {
                if (@struct.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @struct.Name.Content;
                    }
                }
            }
            foreach (CompiledClass @class in CompiledClasses)
            {
                if (@class.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @class.Name.Content;
                    }
                }
            }
            foreach (CompiledEnum @enum in CompiledEnums)
            {
                if (@enum.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                {
                    if (definedType == typeName)
                    {
                        return @enum.Identifier.Content;
                    }
                }
            }
            return null;
        }

        protected bool GetEnum(string name, [NotNullWhen(true)] out CompiledEnum? @enum)
            => CodeGenerator.GetEnum(CompiledEnums, name, out @enum);
        public static bool GetEnum(CompiledEnum?[] enums, string name, [NotNullWhen(true)] out CompiledEnum? @enum)
        {
            for (int i = 0; i < enums.Length; i++)
            {
                CompiledEnum? @enum_ = enums[i];
                if (@enum_ == null) continue;
                if (@enum_.Identifier.Content == name)
                {
                    @enum = @enum_;
                    return true;
                }
            }
            @enum = null;
            return false;
        }

        protected virtual bool GetLocalSymbolType(string symbolName, [NotNullWhen(true)] out CompiledType? type)
        {
            if (GetVariable(symbolName, out CompiledVariable? variable))
            {
                type = variable.Type;
                return true;
            }

            if (GetParameter(symbolName, out CompiledParameter? parameter))
            {
                type = parameter.Type;
                return true;
            }

            type = null;
            return false;
        }

        protected bool GetFunctionByPointer(FunctionType functionType, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            bool found = false;
            compiledFunction = null;

            foreach (CompiledFunction function in CompiledFunctions)
            {
                if (function is null) continue;

                if (function.IsTemplate) continue;

                if (!functionType.Equals(function)) continue;

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {function} are the same", function.Identifier, function.FilePath); }

                compiledFunction = function;
                found = true;
            }

            foreach (CompliableTemplate<CompiledFunction> function in compilableFunctions)
            {
                if (function.Function is null) continue;

                if (!functionType.Equals(function.Function)) continue;

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {function} are the same", function.Function.Identifier, function.Function.FilePath); }

                compiledFunction = function.Function;
                found = true;
            }

            return found;
        }

        protected bool TryGetMacro(FunctionCall functionCallStatement, [NotNullWhen(true)] out MacroDefinition? macro)
            => TryGetMacro(functionCallStatement.Identifier.Content, functionCallStatement.MethodParameters.Length, out macro);

        protected bool GetFunction(FunctionCall functionCallStatement, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            Token functionIdentifier = functionCallStatement.Identifier;
            StatementWithValue[] passedParameters = functionCallStatement.MethodParameters;

            if (TryGetFunction(functionIdentifier, passedParameters.Length, out CompiledFunction? possibleFunction))
            {
                return GetFunction(functionIdentifier.Content, FindStatementTypes(passedParameters, possibleFunction.ParameterTypes), out compiledFunction);
            }
            else
            {
                return GetFunction(functionIdentifier.Content, FindStatementTypes(passedParameters), out compiledFunction);
            }
        }

        protected bool GetFunction(string name, CompiledType[] parameters, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            bool found = false;
            compiledFunction = null;

            foreach (CompiledFunction function in CompiledFunctions)
            {
                if (function is null) continue;

                if (function.IsTemplate) continue;

                if (function.Identifier.Content != name) continue;

                if (!CompiledType.Equals(function.ParameterTypes, parameters)) continue;

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {function} are the same", function.Identifier, function.FilePath); }

                compiledFunction = function;
                found = true;
            }

            foreach (CompliableTemplate<CompiledFunction> function in compilableFunctions)
            {
                if (function.Function is null) continue;

                if (function.Function.Identifier.Content != name) continue;

                if (!CompiledType.Equals(function.Function.ParameterTypes, parameters)) continue;

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {function} are the same", function.Function.Identifier, function.Function.FilePath); }

                compiledFunction = function.Function;
                found = true;
            }

            return found;
        }

        protected bool GetFunctionTemplate(FunctionCall functionCallStatement, out CompliableTemplate<CompiledFunction> compiledFunction)
        {
            CompiledType[] parameters = FindStatementTypes(functionCallStatement.MethodParameters);

            bool found = false;
            compiledFunction = default;

            foreach (CompiledFunction element in CompiledFunctions)
            {
                if (element is null) continue;

                if (!element.IsTemplate) continue;

                if (element.Identifier != functionCallStatement.FunctionName) continue;

                if (!CompiledType.TryGetTypeParameters(element.ParameterTypes, parameters, out TypeArguments? typeParameters)) continue;

                // if (element.Context != null && element.Context.TemplateInfo != null)
                // { CollectTypeParameters(FindStatementType(functionCallStatement.PrevStatement), element.Context.TemplateInfo.TypeParameters, typeParameters); }

                compiledFunction = new CompliableTemplate<CompiledFunction>(element, typeParameters);

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {compiledFunction} and {element} are the same", element.Identifier, element.FilePath); }

                found = true;
            }

            return found;
        }

        protected bool GetConstructorTemplate(CompiledClass @class, ConstructorCall constructorCall, out CompliableTemplate<CompiledGeneralFunction> compiledGeneralFunction)
        {
            bool found = false;
            compiledGeneralFunction = default;

            CompiledType[] parameters = FindStatementTypes(constructorCall.Parameters);

            foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
            {
                if (!function.IsTemplate) continue;

                if (function.Identifier.Content != BuiltinFunctionNames.Constructor) continue;
                if (function.Context != @class) continue;
                if (function.ParameterCount != parameters.Length) continue;

                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, out var typeParameters)) continue;

                MapTypeParameters(constructorCall.TypeName, @class.TemplateInfo!.TypeParameters, typeParameters);

                compiledGeneralFunction = new CompliableTemplate<CompiledGeneralFunction>(function, typeParameters);

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {function} are the same", function.Identifier, function.FilePath); }

                found = true;
            }

            return found;
        }

        protected bool GetIndexGetter(CompiledType prevType, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            if (!prevType.IsClass)
            {
                compiledFunction = null;
                return false;
            }
            CompiledClass context = prevType.Class;

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (function.IsTemplate) continue;
                if (function.Context != context) continue;
                if (function.Identifier.Content != BuiltinFunctionNames.IndexerGet) continue;

                if (function.ParameterTypes.Length != 2)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.Integer)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (!function.ReturnSomething)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should return something", function.TypeToken, function.FilePath); }

                compiledFunction = function;
                return true;
            }

            for (int i = 0; i < compilableFunctions.Count; i++)
            {
                CompiledFunction function = compilableFunctions[i].Function;

                if (function.Context != context) continue;
                if (function.Identifier.Content != BuiltinFunctionNames.IndexerGet) continue;

                if (function.ParameterTypes.Length != 2)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.Integer)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (!function.ReturnSomething)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should return something", function.TypeToken, function.FilePath); }

                compiledFunction = function;
                return true;
            }

            compiledFunction = null;
            return false;
        }

        protected bool GetIndexSetter(CompiledType prevType, CompiledType elementType, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            if (!prevType.IsClass)
            {
                compiledFunction = null;
                return false;
            }
            CompiledClass context = prevType.Class;

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (function.IsTemplate) continue;
                if (function.Context != context) continue;
                if (function.Identifier.Content != BuiltinFunctionNames.IndexerSet) continue;

                if (function.ParameterTypes.Length < 3)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[2] != elementType)
                { continue; }

                if (function.ParameterTypes.Length > 3)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.Integer)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ReturnSomething)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should not return anything", function.TypeToken, function.FilePath); }

                compiledFunction = function;
                return true;
            }

            for (int i = 0; i < compilableFunctions.Count; i++)
            {
                CompiledFunction function = compilableFunctions[i].Function;

                if (function.Context != context) continue;
                if (function.Identifier.Content != BuiltinFunctionNames.IndexerSet) continue;

                if (function.ParameterTypes.Length < 3)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[2] != elementType)
                { continue; }

                if (function.ParameterTypes.Length > 3)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.Integer)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ReturnSomething)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should not return anything", function.TypeToken, function.FilePath); }

                compiledFunction = function;
                return true;
            }

            compiledFunction = null;
            return false;
        }

        protected bool GetIndexGetterTemplate(CompiledType prevType, out CompliableTemplate<CompiledFunction> compiledFunction)
        {
            if (!prevType.IsClass)
            {
                compiledFunction = default;
                return false;
            }

            CompiledClass context = prevType.Class;

            context.AddTypeArguments(prevType.TypeParameters);
            context.AddTypeArguments(TypeArguments);

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (!function.IsTemplate) continue;
                if (function.Context != context) continue;
                if (function.Identifier.Content != BuiltinFunctionNames.IndexerGet) continue;

                if (function.ParameterTypes.Length != 2)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.Integer)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (!function.ReturnSomething)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerGet}\" should return something", function.TypeToken, function.FilePath); }

                TypeArguments typeParameters = new(context.CurrentTypeArguments);

                compiledFunction = new CompliableTemplate<CompiledFunction>(function, typeParameters);
                context.ClearTypeArguments();
                return true;
            }

            compiledFunction = default;
            context.ClearTypeArguments();
            return false;
        }

        protected bool GetIndexSetterTemplate(CompiledType prevType, CompiledType elementType, out CompliableTemplate<CompiledFunction> compiledFunction)
        {
            if (!prevType.IsClass)
            {
                compiledFunction = default;
                return false;
            }

            CompiledClass context = prevType.Class;

            context.AddTypeArguments(prevType.TypeParameters);
            context.AddTypeArguments(TypeArguments);

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (!function.IsTemplate) continue;
                if (function.Context != context) continue;
                if (function.Identifier.Content != BuiltinFunctionNames.IndexerSet) continue;

                if (function.ParameterTypes.Length < 3)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (!function.ParameterTypes[2].IsGeneric && function.ParameterTypes[2] != elementType)
                { continue; }

                if (function.ParameterTypes.Length > 3)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.Integer)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ReturnSomething)
                { throw new CompilerException($"Method \"{BuiltinFunctionNames.IndexerSet}\" should not return anything", function.TypeToken, function.FilePath); }

                TypeArguments typeParameters = new(context.CurrentTypeArguments);

                compiledFunction = new CompliableTemplate<CompiledFunction>(function, typeParameters);
                context.ClearTypeArguments();
                return true;
            }

            compiledFunction = default;
            context.ClearTypeArguments();
            return false;
        }

        bool TryGetFunction(string name, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.Identifier != name) continue;

                if (compiledFunction is not null)
                { return false; }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }
        protected bool TryGetFunction(Token name, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
            => TryGetFunction(name.Content, out compiledFunction);

        bool TryGetFunction(string name, int parameterCount, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.Identifier != name) continue;

                if (function.ParameterCount != parameterCount) continue;

                if (compiledFunction is not null)
                { return false; }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }
        protected bool TryGetFunction(Token name, int parameterCount, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
            => TryGetFunction(name.Content, parameterCount, out compiledFunction);

        protected bool TryGetBuiltinFunction(string builtinName, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.BuiltinFunctionName != builtinName) continue;

                if (compiledFunction is not null)
                { return false; }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }

        bool TryGetMacro(string name, int parameterCount, [NotNullWhen(true)] out MacroDefinition? macro)
        {
            macro = null;

            for (int i = 0; i < this.CompiledMacros.Length; i++)
            {
                MacroDefinition _macro = this.CompiledMacros[i];

                if (_macro.Identifier != name) continue;

                if (_macro.ParameterCount != parameterCount) continue;

                if (macro is not null)
                { return false; }

                macro = _macro;
            }

            return macro is not null;
        }
        protected bool TryGetMacro(Token name, int parameterCount, [NotNullWhen(true)] out MacroDefinition? macro)
            => TryGetMacro(name.Content, parameterCount, out macro);

        protected bool GetFunction(FunctionType type, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (!CompiledType.Equals(function.ParameterTypes, type.Parameters)) continue;
                if (!function.Type.Equals(type.ReturnType)) continue;

                if (compiledFunction is not null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ReadableID()} (at {compiledFunction.Identifier.Position.ToStringRange()}) ; {function.ReadableID()} (at {function.Identifier.Position.ToStringRange()}) ; (and possibly more)", CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }

        bool GetFunction(string name, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.Identifier != name) continue;

                if (compiledFunction is not null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ReadableID()} (at {compiledFunction.Identifier.Position.ToStringRange()}) ; {function.ReadableID()} (at {function.Identifier.Position.ToStringRange()}) ; (and possibly more)", CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }

        protected bool GetFunction(Token name, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.Identifier != name.Content) continue;

                if (compiledFunction is not null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ReadableID()} (at {compiledFunction.Identifier.Position.ToStringRange()}) ; {function.ReadableID()} (at {function.Identifier.Position.ToStringRange()}) ; (and possibly more)", name, CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }

        protected bool GetFunction(Token name, CompiledType? type, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            if (type is null || !type.IsFunction)
            { return GetFunction(name, out compiledFunction); }
            return GetFunction(name, type.Function, out compiledFunction);
        }
        protected bool GetFunction(Token name, FunctionType? type, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            if (type is null)
            { return GetFunction(name, out compiledFunction); }

            compiledFunction = null;
            bool success = true;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.Identifier != name.Content) continue;

                if (type.ReturnType.Equals(function.Type) &&
                    CompiledType.Equals(function.ParameterTypes, type.Parameters))
                {
                    compiledFunction = function;
                    return true;
                }

                if (compiledFunction is not null)
                {
                    success = false;
                }

                compiledFunction = function;
            }

            return success && compiledFunction is not null;
        }

        protected bool GetOperator(OperatorCall @operator, [NotNullWhen(true)] out CompiledOperator? compiledOperator)
        {
            CompiledType[] parameters = FindStatementTypes(@operator.Parameters);

            bool found = false;
            compiledOperator = null;

            foreach (CompiledOperator function in CompiledOperators)
            {
                if (function.IsTemplate) continue;
                if (function.Identifier.Content != @operator.Operator.Content) continue;
                if (!CompiledType.Equals(function.ParameterTypes, parameters)) continue;

                if (found)
                { throw new CompilerException($"Duplicated operator definitions: {found} and {function} are the same", function.Identifier, function.FilePath); }

                compiledOperator = function;
                found = true;
            }

            return found;
        }

        protected bool GetOperatorTemplate(OperatorCall @operator, out CompliableTemplate<CompiledOperator> compiledOperator)
        {
            CompiledType[] parameters = FindStatementTypes(@operator.Parameters);

            bool found = false;
            compiledOperator = default;

            foreach (CompiledOperator function in CompiledOperators)
            {
                if (!function.IsTemplate) continue;
                if (function.Identifier.Content != @operator.Operator.Content) continue;
                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, out TypeArguments? typeParameters)) continue;

                if (found)
                { throw new CompilerException($"Duplicated operator definitions: {compiledOperator} and {function} are the same", function.Identifier, function.FilePath); }

                compiledOperator = new CompliableTemplate<CompiledOperator>(function, typeParameters);

                found = true;
            }

            return found;
        }

        protected bool GetGeneralFunction(CompiledClass @class, string name, [NotNullWhen(true)] out CompiledGeneralFunction? generalFunction)
            => GetGeneralFunction(@class, Array.Empty<CompiledType>(), name, out generalFunction);
        protected bool GetGeneralFunction(CompiledClass @class, CompiledType[] parameters, string name, [NotNullWhen(true)] out CompiledGeneralFunction? generalFunction)
        {
            for (int i = 0; i < CompiledGeneralFunctions.Length; i++)
            {
                CompiledGeneralFunction function = CompiledGeneralFunctions[i];

                if (function.IsTemplate) continue;
                if (function.Identifier != name) continue;
                if (function.Context != @class) continue;
                if (!CompiledType.Equals(function.ParameterTypes, parameters)) continue;

                generalFunction = function;
                return true;
            }

            for (int i = 0; i < compilableGeneralFunctions.Count; i++)
            {
                CompiledGeneralFunction function = compilableGeneralFunctions[i].Function;

                if (function.Identifier.Content != name) continue;
                if (function.Context != @class) continue;
                if (function.ParameterCount != parameters.Length) continue;

                bool not = false;
                for (int j = 0; j < function.ParameterTypes.Length; j++)
                {
                    if (parameters[j] != function.ParameterTypes[j])
                    {
                        not = true;
                        break;
                    }
                }
                if (not) continue;

                generalFunction = function;
                return true;
            }

            generalFunction = null;
            return false;
        }

        protected bool GetGeneralFunctionTemplate(CompiledClass @class, string name, out CompliableTemplate<CompiledGeneralFunction> compiledGeneralFunction)
            => GetGeneralFunctionTemplate(@class, Array.Empty<CompiledType>(), name, out compiledGeneralFunction);
        protected bool GetGeneralFunctionTemplate(CompiledClass @class, CompiledType[] parameters, string name, out CompliableTemplate<CompiledGeneralFunction> compiledGeneralFunction)
        {
            bool found = false;
            compiledGeneralFunction = default;

            foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
            {
                if (!function.IsTemplate) continue;
                if (function.Identifier != name) continue;
                if (function.Context != @class) continue;
                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, out TypeArguments? typeParameters)) continue;

                compiledGeneralFunction = new CompliableTemplate<CompiledGeneralFunction>(function, typeParameters);

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {compiledGeneralFunction.OriginalFunction} and {function} are the same", function.Identifier, function.FilePath); }
                found = true;
            }

            return found;
        }

        protected bool GetOutputWriter(CompiledType type, [NotNullWhen(true)] out CompiledFunction? function)
        {
            foreach (CompiledFunction _function in CompiledFunctions)
            {
                if (!_function.CompiledAttributes.TryGetAttribute("StandardOutput"))
                { continue; }

                if (!_function.CanUse(CurrentFile))
                { continue; }

                if (_function.Parameters.Length != 1)
                { continue; }

                if (type != _function.ParameterTypes[0])
                { continue; }

                function = _function;
                return true;
            }

            function = null;
            return false;
        }

        protected bool GetField(Field field, [NotNullWhen(true)] out CompiledField? compiledField)
        {
            compiledField = null;

            CompiledType type = FindStatementType(field.PrevStatement);
            if (type is null) return false;

            if (type.IsClass)
            {
                CompiledClass @class = type.Class;
                for (int i = 0; i < @class.Fields.Length; i++)
                {
                    if (@class.Fields[i].Identifier.Content != field.FieldName.Content) continue;

                    compiledField = @class.Fields[i];
                    return true;
                }
                return false;
            }

            if (type.IsStruct)
            {
                CompiledStruct @struct = type.Struct;
                for (int i = 0; i < @struct.Fields.Length; i++)
                {
                    if (@struct.Fields[i].Identifier.Content != field.FieldName.Content) continue;

                    compiledField = @struct.Fields[i];
                    return true;
                }
                return false;
            }

            return false;
        }

        #endregion

        #region GetStruct()

        protected bool GetStruct(NewInstance newStructStatement, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
            => GetStruct(newStructStatement.TypeName, out compiledStruct);

        protected bool GetStruct(TypeInstanceSimple type, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
        {
            if (type.GenericTypes is null)
            { return GetStruct(type.Identifier.Content, out compiledStruct); }
            else
            {
                compiledStruct = null;
                return false;
            }
        }

        protected bool GetStruct(TypeInstance type, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
        {
            if (type is not TypeInstanceSimple typeSimple)
            {
                compiledStruct = null;
                return false;
            }
            return GetStruct(typeSimple, out compiledStruct);
        }

        protected bool GetStruct(string structName, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
        {
            for (int i = 0; i < CompiledStructs.Length; i++)
            {
                var @struct = CompiledStructs[i];

                if (@struct.Name.Content != structName) continue;

                compiledStruct = @struct;
                return true;
            }

            compiledStruct = null;
            return false;
        }

        public static bool GetStruct(CompiledStruct?[] structs, string structName, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
        {
            for (int i = 0; i < structs.Length; i++)
            {
                CompiledStruct? @struct = structs[i];
                if (@struct == null) continue;

                if (@struct.Name.Content == structName)
                {
                    compiledStruct = @struct;
                    return true;
                }
            }
            compiledStruct = null;
            return false;
        }

        #endregion

        #region GetClass()

        protected bool GetClass(NewInstance newClassStatement, [NotNullWhen(true)] out CompiledClass? compiledClass)
            => GetClass(newClassStatement.TypeName, out compiledClass);

        protected bool GetClass(ConstructorCall constructorCall, [NotNullWhen(true)] out CompiledClass? compiledClass)
            => GetClass(constructorCall.TypeName, out compiledClass);

        protected bool GetClass(TypeInstanceSimple type, [NotNullWhen(true)] out CompiledClass? compiledClass)
        {
            if (type.GenericTypes is null)
            { return GetClass(type.Identifier.Content, out compiledClass); }
            else
            { return GetClass(type.Identifier.Content, type.GenericTypes.Length, out compiledClass); }
        }

        protected bool GetClass(TypeInstance type, [NotNullWhen(true)] out CompiledClass? compiledClass)
        {
            if (type is not TypeInstanceSimple typeSimple)
            {
                compiledClass = null;
                return false;
            }
            return GetClass(typeSimple, out compiledClass);
        }

        protected bool GetClass(string className, [NotNullWhen(true)] out CompiledClass? compiledClass)
            => CodeGenerator.GetClass(CompiledClasses, className, 0, out compiledClass);
        protected bool GetClass(string className, int typeParameterCount, [NotNullWhen(true)] out CompiledClass? compiledClass)
            => CodeGenerator.GetClass(CompiledClasses, className, typeParameterCount, out compiledClass);

        public static bool GetClass(CompiledClass?[] classes, string className, [NotNullWhen(true)] out CompiledClass? compiledClass)
            => GetClass(classes, className, 0, out compiledClass);
        public static bool GetClass(CompiledClass?[] classes, string className, int typeParameterCount, [NotNullWhen(true)] out CompiledClass? compiledClass)
        {
            for (int i = 0; i < classes.Length; i++)
            {
                CompiledClass? @class = classes[i];
                if (@class == null) continue;

                if (@class.Name.Content != className) continue;
                if (typeParameterCount > 0 && @class.TemplateInfo != null)
                { if (@class.TemplateInfo.TypeParameters.Length != typeParameterCount) continue; }

                compiledClass = @class;
                return true;
            }

            compiledClass = null;
            return false;
        }

        #endregion

        #region FindType()

        /// <exception cref="CompilerException"/>
        protected CompiledType FindType(Token name) => FindType(name.Content, name);

        /// <exception cref="CompilerException"/>
        protected CompiledType FindType(string name, IThingWithPosition? position) => FindType(name, position?.Position ?? Position.UnknownPosition);

        /// <exception cref="CompilerException"/>
        protected CompiledType FindType(string name) => FindType(name, Position.UnknownPosition);

        /// <param name="position">Used for exceptions</param>
        /// <exception cref="CompilerException"/>
        CompiledType FindType(string name, Position position)
        {
            if (GetStruct(name, out CompiledStruct? @struct)) return new CompiledType(@struct);
            if (GetClass(name, out CompiledClass? @class)) return new CompiledType(@class);
            if (GetEnum(name, out CompiledEnum? @enum)) return new CompiledType(@enum);

            if (TypeArguments.TryGetValue(name, out CompiledType? typeArgument))
            { return typeArgument; }

            if (GetFunction(name, out CompiledFunction? function))
            { return new CompiledType(new FunctionType(function)); }

            throw new CompilerException($"Type \"{name}\" not found", position, CurrentFile);
        }

        /// <exception cref="InternalException"/>
        protected CompiledType FindType(TypeInstance name)
            => new(name, FindType);

        #endregion

        #region Memory Helpers

        protected virtual void StackStore(ValueAddress address, int size)
        {
            for (int i = size - 1; i >= 0; i--)
            { StackStore(address + i); }
        }
        protected virtual void StackLoad(ValueAddress address, int size)
        {
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            { StackLoad(address + currentOffset); }
        }

        protected abstract void StackLoad(ValueAddress address);
        protected abstract void StackStore(ValueAddress address);

        #endregion

        protected bool GetVariable(string variableName, [NotNullWhen(true)] out CompiledVariable? compiledVariable)
        {
            foreach (CompiledVariable compiledVariable_ in CompiledVariables)
            {
                if (compiledVariable_.VariableName.Content == variableName)
                {
                    compiledVariable = compiledVariable_;
                    return true;
                }
            }
            compiledVariable = null;
            return false;
        }

        protected bool GetParameter(string parameterName, [NotNullWhen(true)] out CompiledParameter? parameter)
        {
            foreach (CompiledParameter compiledParameter_ in CompiledParameters)
            {
                if (compiledParameter_.Identifier.Content == parameterName)
                {
                    parameter = compiledParameter_;
                    return true;
                }
            }
            parameter = null;
            return false;
        }

        #region Addressing Helpers

        protected ValueAddress GetDataAddress(StatementWithValue value)
        {
            if (value is IndexCall indexCall)
            { return GetDataAddress(indexCall); }

            if (value is Identifier identifier)
            { return GetDataAddress(identifier); }

            if (value is Field field)
            { return GetDataAddress(field); }

            throw new NotImplementedException();
        }
        protected ValueAddress GetDataAddress(Identifier variable)
        {
            if (GetConstant(variable.Content, out _))
            { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

            if (GetParameter(variable.Content, out CompiledParameter? param))
            {
                return GetBaseAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable? val))
            {
                return new ValueAddress(val);
            }

            throw new CompilerException($"Local symbol \"{variable.Content}\" not found", variable, CurrentFile);
        }
        protected ValueAddress GetDataAddress(Field field)
        {
            ValueAddress address = GetBaseAddress(field);
            if (address.IsReference)
            { throw new NotImplementedException(); }
            int offset = GetDataOffset(field);
            return new ValueAddress(address.Address + offset, address.BasepointerRelative, address.IsReference, address.InHeap);
        }
        protected ValueAddress GetDataAddress(IndexCall indexCall)
        {
            ValueAddress address = GetBaseAddress(indexCall.PrevStatement!);
            if (address.IsReference)
            { throw new NotImplementedException(); }
            int currentOffset = GetDataOffset(indexCall);
            return new ValueAddress(address.Address + currentOffset, address.BasepointerRelative, address.IsReference, address.InHeap);
        }

        protected int GetDataOffset(StatementWithValue value)
        {
            if (value is IndexCall indexCall)
            { return GetDataOffset(indexCall); }

            if (value is Field field)
            { return GetDataOffset(field); }

            if (value is Identifier)
            { return 0; }

            throw new NotImplementedException();
        }
        protected int GetDataOffset(Field field)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            IReadOnlyDictionary<string, int> fieldOffsets;

            if (prevType.IsStruct)
            {
                fieldOffsets = prevType.Struct.FieldOffsets;
            }
            else if (prevType.IsClass)
            {
                prevType.Class.AddTypeArguments(TypeArguments);
                prevType.Class.AddTypeArguments(prevType.TypeParameters);

                fieldOffsets = prevType.Class.FieldOffsets;

                prevType.Class.ClearTypeArguments();
            }
            else
            { throw new NotImplementedException(); }

            if (!fieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
            { throw new InternalException($"Field \"{field.FieldName}\" does not have an offset value", CurrentFile); }

            int prevOffset = GetDataOffset(field.PrevStatement);
            return prevOffset + fieldOffset;
        }
        protected int GetDataOffset(IndexCall indexCall)
        {
            CompiledType prevType = FindStatementType(indexCall.PrevStatement);

            if (!prevType.IsStackArray)
            { throw new CompilerException($"Only stack arrays supported by now and this is not one", indexCall.PrevStatement, CurrentFile); }

            if (!TryCompute(indexCall.Expression, RuntimeType.SInt32, out DataItem index))
            { throw new CompilerException($"Can't compute the index value", indexCall.Expression, CurrentFile); }

            int prevOffset = GetDataOffset(indexCall.PrevStatement);
            int offset = index.ValueSInt32 * prevType.StackArrayOf.SizeOnStack;
            return prevOffset + offset;
        }

        protected ValueAddress GetBaseAddress(StatementWithValue statement)
        {
            if (statement is Identifier identifier)
            { return GetBaseAddress(identifier); }

            if (statement is Field field)
            { return GetBaseAddress(field); }

            if (statement is IndexCall indexCall)
            { return GetBaseAddress(indexCall); }

            throw new NotImplementedException();
        }
        protected abstract ValueAddress GetBaseAddress(CompiledParameter parameter);
        protected abstract ValueAddress GetBaseAddress(CompiledParameter parameter, int offset);
        protected ValueAddress GetBaseAddress(Identifier variable)
        {
            if (GetConstant(variable.Content, out _))
            { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

            if (GetParameter(variable.Content, out CompiledParameter? param))
            {
                return GetBaseAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable? val))
            {
                return new ValueAddress(val);
            }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
        }
        protected ValueAddress GetBaseAddress(Field statement)
        {
            ValueAddress address = GetBaseAddress(statement.PrevStatement);
            bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement).InHEAP;
            return new ValueAddress(address.Address, address.BasepointerRelative, address.IsReference, inHeap);
        }
        protected ValueAddress GetBaseAddress(IndexCall statement)
        {
            ValueAddress address = GetBaseAddress(statement.PrevStatement!);
            bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement).InHEAP;
            return new ValueAddress(address.Address, address.BasepointerRelative, address.IsReference, inHeap);
        }

        protected bool IsItInHeap(StatementWithValue value)
        {
            if (value is Identifier)
            { return false; }

            if (value is Field field)
            { return IsItInHeap(field); }

            if (value is IndexCall indexCall)
            { return IsItInHeap(indexCall); }

            throw new NotImplementedException();
        }
        protected bool IsItInHeap(IndexCall indexCall)
        {
            return IsItInHeap(indexCall.PrevStatement!) || FindStatementType(indexCall.PrevStatement).InHEAP;
        }
        protected bool IsItInHeap(Field field)
        {
            return IsItInHeap(field.PrevStatement) || FindStatementType(field.PrevStatement).InHEAP;
        }

        #endregion

        /// <summary>
        /// Collects the type parameters from <paramref name="type"/> with names got from <paramref name="typeParameterNames"/> and puts the result to <paramref name="typeParameters"/>
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        void MapTypeParameters(TypeInstance type, Token[] typeParameterNames, TypeArguments typeParameters)
            => MapTypeParameters(new CompiledType(type, FindType), typeParameterNames, typeParameters);

        /// <summary>
        /// Collects the type parameters from <paramref name="type"/> with names got from <paramref name="typeParameterNames"/> and puts the result to <paramref name="typeParameters"/>
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        static void MapTypeParameters(CompiledType type, Token[] typeParameterNames, TypeArguments typeParameters)
        {
            if (type.TypeParameters.Length != typeParameterNames.Length)
            { throw new NotImplementedException($"There should be the same number of type parameter values as type parameter names"); }

            LanguageCore.Utils.Map(typeParameterNames, type.TypeParameters, typeParameters);
        }

        protected CompiledVariable CompileVariable(VariableDeclaration newVariable, int memoryOffset, bool isGlobal)
        {
            if (LanguageConstants.Keywords.Contains(newVariable.VariableName.Content))
            { throw new CompilerException($"Illegal variable name '{newVariable.VariableName.Content}'", newVariable.VariableName, CurrentFile); }

            CompiledType type;
            if (newVariable.Type == "var")
            {
                if (newVariable.InitialValue == null)
                { throw new CompilerException($"Initial value for variable declaration with implicit type is required", newVariable, newVariable.FilePath); }

                type = FindStatementType(newVariable.InitialValue);
            }
            else
            {
                type = new CompiledType(newVariable.Type, FindType, TryCompute);
            }

            if (!type.AllGenericsDefined())
            { throw new InternalException($"Failed to qualify all generics in variable \"{newVariable.VariableName}\" type \"{type}\"", newVariable.FilePath); }

            return new CompiledVariable(
                memoryOffset,
                type,
                isGlobal,
                newVariable);
        }

        protected CompiledFunction? GetCodeEntry()
        {
            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                for (int j = 0; j < function.Attributes.Length; j++)
                {
                    if (function.Attributes[j].Identifier.Content != "CodeEntry") continue;

                    if (function.IsTemplate)
                    { throw new CompilerException($"Code entry can not be a template function", function.TemplateInfo, function.FilePath); }

                    return function;
                }
            }

            return null;
        }

        #region GetInitialValue()

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem GetInitialValue(Type type)
            => type switch
            {
                Type.Byte => new DataItem((byte)0),
                Type.Integer => new DataItem((int)0),
                Type.Float => new DataItem((float)0f),
                Type.Char => new DataItem((char)'\0'),

                _ => throw new InternalException($"Initial value for type \"{type}\" is unimplemented"),
            };

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem[] GetInitialValue(CompiledStruct @struct)
        {
            List<DataItem> result = new();

            foreach (CompiledField field in @struct.Fields)
            { result.Add(GetInitialValue(field.Type)); }

            if (result.Count != @struct.Size)
            { throw new NotImplementedException(); }

            return result.ToArray();
        }

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem GetInitialValue(TypeInstance type)
            => type.ToString() switch
            {
                "int" => new DataItem((int)0),
                "byte" => new DataItem((byte)0),
                "float" => new DataItem((float)0f),
                "char" => new DataItem((char)'\0'),

                "var" => throw new CompilerException("Undefined type", type, null),
                "void" => throw new CompilerException("Invalid type", type, null),
                _ => throw new InternalException($"Initial value for type \"{type}\" is unimplemented"),
            };

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem GetInitialValue(CompiledType type)
        {
            if (type.IsGeneric)
            { throw new NotImplementedException($"Initial value for type arguments is bruh moment"); }

            if (type.IsStruct)
            { throw new NotImplementedException($"Initial value for structs is not implemented"); }

            if (type.IsClass)
            { return new DataItem(0); }

            if (type.IsEnum)
            {
                if (type.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{type.Enum.Identifier.Content}\" initial value: enum has no members", type.Enum.Identifier, type.Enum.FilePath); }

                return type.Enum.Members[0].ComputedValue;
            }

            if (type.IsFunction)
            {
                return new DataItem(int.MaxValue);
            }

            if (type.IsBuiltin)
            {
                return GetInitialValue(type.BuiltinType);
            }

            throw new NotImplementedException();
        }

        #endregion

        #region FindStatementType()

        protected CompiledType FindStatementType(AnyCall anyCall)
        {
            if (anyCall.ToFunctionCall(out var functionCall))
            { return FindStatementType(functionCall); }

            CompiledType prevType = FindStatementType(anyCall.PrevStatement);

            if (!prevType.IsFunction)
            { throw new CompilerException($"This isn't a function", anyCall.PrevStatement, CurrentFile); }

            return prevType.Function.ReturnType;
        }
        protected CompiledType FindStatementType(KeywordCall keywordCall)
        {
            if (keywordCall.FunctionName == "return") return new CompiledType(Type.Void);

            if (keywordCall.FunctionName == "throw") return new CompiledType(Type.Void);

            if (keywordCall.FunctionName == "break") return new CompiledType(Type.Void);

            if (keywordCall.FunctionName == "sizeof") return new CompiledType(Type.Integer);

            if (keywordCall.FunctionName == "delete") return new CompiledType(Type.Void);

            if (keywordCall.FunctionName == "clone")
            {
                if (keywordCall.Parameters.Length != 1)
                { throw new CompilerException($"Wrong number of parameters passed to keyword-function \"clone\": required {1}, passed {keywordCall.Parameters.Length}", keywordCall, CurrentFile); }

                return FindStatementType(keywordCall.Parameters[0]);
            }

            throw new CompilerException($"Unknown keyword-function \"{keywordCall.FunctionName}\"", keywordCall.Identifier, CurrentFile);
        }
        protected CompiledType FindStatementType(IndexCall index)
        {
            CompiledType prevType = FindStatementType(index.PrevStatement);

            if (prevType.IsStackArray)
            { return prevType.StackArrayOf; }

            if (!prevType.IsClass)
            { throw new CompilerException($"Index getter for type \"{prevType}\" not found", index, CurrentFile); }

            if (!GetIndexGetter(prevType, out CompiledFunction? indexer))
            {
                if (!GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index getter for type \"{prevType}\" not found", index, CurrentFile); }
                indexer = indexerTemplate.Function;
            }

            return indexer.Type;
        }
        protected CompiledType FindStatementType(FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "sizeof") return new CompiledType(Type.Integer);

            if (TryGetMacro(functionCall, out MacroDefinition? macro))
            { return FindMacroType(macro, functionCall.Parameters); }

            if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out var compiledFunctionTemplate))
                { throw new CompilerException($"Function \"{functionCall.ReadableID(FindStatementType)}\" not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compiledFunctionTemplate.Function;
            }

            return compiledFunction.Type;
        }

        protected CompiledType FindStatementType(OperatorCall @operator, CompiledType? expectedType)
        {
            if (LanguageConstants.Operators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
            {
                if (LanguageConstants.Operators.ParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {LanguageConstants.Operators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }
            }
            else
            { opcode = Opcode.UNKNOWN; }

            if (opcode == Opcode.UNKNOWN)
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }

            if (GetOperator(@operator, out CompiledOperator? operatorDefinition))
            { return operatorDefinition.Type; }

            CompiledType leftType = FindStatementType(@operator.Left);
            if (@operator.Right == null)
            { return leftType; }

            CompiledType rightType = FindStatementType(@operator.Right);

            if (!leftType.CanBeBuiltin || !rightType.CanBeBuiltin || leftType.BuiltinType == Type.Void || rightType.BuiltinType == Type.Void)
            { throw new CompilerException($"Unknown operator {leftType} {@operator.Operator.Content} {rightType}", @operator.Operator, CurrentFile); }

            if ((expectedType is not null && expectedType.IsBuiltin) ?
                TryCompute(@operator, expectedType.RuntimeType, out DataItem predictedValue) :
                TryCompute(@operator, null, out predictedValue))
            {

            }
            else
            {
                DataItem leftValue = GetInitialValue(leftType);
                DataItem rightValue = GetInitialValue(rightType);

                predictedValue = Compute(@operator.Operator.Content, leftValue, rightValue);
            }

            CompiledType result = new(predictedValue.Type);

            if (expectedType is not null && CanConvertImplicitly(result, expectedType)) return expectedType;

            return result;
        }
        protected CompiledType FindStatementType(LiteralStatement literal, CompiledType? expectedType)
        {
            switch (literal.Type)
            {
                case LiteralType.Integer:
                    if (expectedType == Type.Byte &&
                        int.TryParse(literal.Value, out int value) &&
                        value >= byte.MinValue && value <= byte.MaxValue)
                    { return new CompiledType(Type.Byte); }
                    return new CompiledType(Type.Integer);
                case LiteralType.Float:
                    return new CompiledType(Type.Float);
                case LiteralType.Boolean:
                    return FindReplacedType("boolean", literal);
                case LiteralType.String:
                    CompiledType stringType = FindReplacedType("string", literal);
                    if (stringType.IsClass && expectedType == Type.Integer)
                    { return expectedType; }
                    return stringType;
                case LiteralType.Char:
                    return new CompiledType(Type.Char);
                default:
                    throw new ImpossibleException($"Unknown literal type {literal.Type}");
            }
        }
        protected CompiledType FindStatementType(Identifier identifier, CompiledType? expectedType = null)
        {
            if (identifier.Content == "nullptr")
            { return new CompiledType(Type.Integer); }

            if (GetConstant(identifier.Content, out DataItem constant))
            { return new CompiledType(constant.Type); }

            if (GetLocalSymbolType(identifier.Content, out CompiledType? type))
            { return type; }

            if (GetEnum(identifier.Content, out var @enum))
            { return new CompiledType(@enum); }

            if (GetFunction(identifier.Name, expectedType, out var function))
            { return new CompiledType(function); }

            try
            { return FindType(identifier.Name); }
            catch (CompilerException)
            { }

            throw new CompilerException($"Symbol \"{identifier.Content}\" not found", identifier, CurrentFile);
        }
        protected static CompiledType FindStatementType(AddressGetter _) => new(Type.Integer);
        protected static CompiledType FindStatementType(Pointer _) => new(Type.Unknown);
        protected CompiledType FindStatementType(NewInstance newInstance) => new(newInstance.TypeName, FindType);
        protected CompiledType FindStatementType(ConstructorCall constructorCall) => new(constructorCall.TypeName, FindType);
        protected CompiledType FindStatementType(Field field)
        {
            CompiledType prevStatementType = FindStatementType(field.PrevStatement);

            if (prevStatementType.IsStackArray && field.FieldName == "Length")
            {
                return new CompiledType(Type.Integer);
            }

            if (prevStatementType.IsStruct)
            {
                for (int i = 0; i < prevStatementType.Struct.Fields.Length; i++)
                {
                    CompiledField definedField = prevStatementType.Struct.Fields[i];

                    if (definedField.Identifier.Content != field.FieldName.Content) continue;

                    if (definedField.Type.IsGeneric)
                    { throw new NotSupportedException($"Struct templates not supported :(", definedField, prevStatementType.Struct.FilePath); }

                    return definedField.Type;
                }

                throw new CompilerException($"Field definition \"{prevStatementType}\" not found in struct \"{prevStatementType.Struct.Name.Content}\"", field.FieldName, CurrentFile);
            }

            if (prevStatementType.IsClass)
            {
                for (int i = 0; i < prevStatementType.Class.Fields.Length; i++)
                {
                    CompiledField definedField = prevStatementType.Class.Fields[i];

                    if (definedField.Identifier.Content != field.FieldName.Content) continue;

                    if (definedField.Type.IsGeneric)
                    {
                        if (this.TypeArguments.TryGetValue(definedField.Type.Name, out CompiledType? typeParameter))
                        { return typeParameter; }

                        if (!prevStatementType.Class.TryGetTypeArgumentIndex(definedField.Type.Name, out int j))
                        { throw new CompilerException($"Type argument \"{definedField.Type.Name}\" not found", definedField, prevStatementType.Class.FilePath); }

                        if (prevStatementType.TypeParameters.Length <= j)
                        { throw new NotImplementedException(); }

                        return prevStatementType.TypeParameters[j];
                    }

                    CompiledType result = new(definedField.Type);

                    for (int j = 0; j < result.TypeParameters.Length; j++)
                    {
                        if (result.TypeParameters[j].IsGeneric)
                        {
                            if (TypeArguments.TryGetValue(result.TypeParameters[j].Name, out CompiledType? genericType))
                            {
                                result.TypeParameters[j] = genericType;
                            }
                            else if (prevStatementType.Class.TryGetTypeArgumentIndex(result.TypeParameters[j].Name, out int k))
                            {
                                if (result.TypeParameters.Length <= k)
                                { throw new NotImplementedException(); }

                                result.TypeParameters[j] = prevStatementType.TypeParameters[k];
                            }
                            else
                            { throw new CompilerException($"Type argument \"{result.TypeParameters[j].Name}\" not found", definedField, CurrentFile); }
                        }
                    }

                    return result;
                }

                throw new CompilerException($"Field definition \"{prevStatementType}\" not found in class \"{prevStatementType.Class.Name.Content}\"", field.FieldName, CurrentFile);
            }

            if (prevStatementType.IsEnum)
            {
                foreach (CompiledEnumMember enumMember in prevStatementType.Enum.Members)
                {
                    if (enumMember.Identifier.Content == field.FieldName.Content)
                    { return new CompiledType(enumMember.ComputedValue.Type); }
                }

                throw new CompilerException($"Enum member \"{prevStatementType}\" not found in enum \"{prevStatementType.Enum.Identifier.Content}\"", field.FieldName, CurrentFile);
            }

            throw new CompilerException($"Class/struct/enum definition \"{prevStatementType}\" not found", field, CurrentFile);
        }
        protected CompiledType FindStatementType(TypeCast @as) => new(@as.Type, FindType);
        protected CompiledType FindStatementType(ModifiedStatement modifiedStatement, CompiledType? expectedType)
        {
            if (modifiedStatement.Modifier == "ref")
            {
                return FindStatementType(modifiedStatement.Statement, expectedType);
            }

            if (modifiedStatement.Modifier == "temp")
            {
                return FindStatementType(modifiedStatement.Statement, expectedType);
            }

            throw new CompilerException($"Unimplemented modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, CurrentFile);
        }

        protected CompiledType FindStatementType(StatementWithValue? statement)
            => FindStatementType(statement, null);
        protected CompiledType FindStatementType(StatementWithValue? statement, CompiledType? expectedType)
        {
            if (statement is FunctionCall functionCall)
            { return FindStatementType(functionCall); }

            if (statement is OperatorCall @operator)
            { return FindStatementType(@operator, expectedType); }

            if (statement is LiteralStatement literal)
            { return FindStatementType(literal, expectedType); }

            if (statement is Identifier variable)
            { return FindStatementType(variable, expectedType); }

            if (statement is AddressGetter memoryAddressGetter)
            { return FindStatementType(memoryAddressGetter); }

            if (statement is Pointer memoryAddressFinder)
            { return FindStatementType(memoryAddressFinder); }

            if (statement is NewInstance newStruct)
            { return FindStatementType(newStruct); }

            if (statement is ConstructorCall constructorCall)
            { return FindStatementType(constructorCall); }

            if (statement is Field field)
            { return FindStatementType(field); }

            if (statement is TypeCast @as)
            { return FindStatementType(@as); }

            if (statement is KeywordCall keywordCall)
            { return FindStatementType(keywordCall); }

            if (statement is IndexCall index)
            { return FindStatementType(index); }

            if (statement is ModifiedStatement modifiedStatement)
            { return FindStatementType(modifiedStatement, expectedType); }

            if (statement is AnyCall anyCall)
            { return FindStatementType(anyCall); }

            throw new CompilerException($"Statement {(statement is null ? "null" : statement.GetType().Name)} does not have a type", statement, CurrentFile);
        }

        protected CompiledType[] FindStatementTypes(StatementWithValue[] statements)
        {
            CompiledType[] result = new CompiledType[statements.Length];
            for (int i = 0; i < statements.Length; i++)
            { result[i] = FindStatementType(statements[i]); }
            return result;
        }

        protected CompiledType[] FindStatementTypes(StatementWithValue[] statements, CompiledType[] expectedTypes)
        {
            CompiledType[] result = new CompiledType[statements.Length];
            for (int i = 0; i < statements.Length; i++)
            {
                CompiledType? expectedType = null;
                if (i < expectedTypes.Length) expectedType = expectedTypes[i];
                result[i] = FindStatementType(statements[i], expectedType);
            }
            return result;
        }

        protected CompiledType FindMacroType(MacroDefinition macro, params StatementWithValue[] parameters)
        {
            Statement inlinedMacro = InlineMacro(macro, parameters);

            if (inlinedMacro is StatementWithValue statementWithValue)
            { return FindStatementType(statementWithValue); }

            List<CompiledType> result = new();
            StatementFinder.GetAllStatement(inlinedMacro, (statement) =>
            {
                if (statement is KeywordCall keywordCall &&
                    keywordCall.Identifier == "return")
                {
                    if (keywordCall.Parameters.Length == 0)
                    { result.Add(new CompiledType(Type.Void)); }
                    else
                    { result.Add(FindStatementType(keywordCall.Parameters[0])); }
                }
                return false;
            });

            if (result.Count == 0)
            { return new CompiledType(Type.Void); }

            for (int i = 1; i < result.Count; i++)
            {
                if (!result[i].Equals(result[0]))
                { throw new CompilerException($"Macro \"{macro.ReadableID()}\" returns more than one type of value", macro.Block, macro.FilePath); }
            }

            return result[0];
        }

        #endregion

        #region InlineMacro()

        protected bool TryInlineMacro(StatementWithValue statement, [NotNullWhen(true)] out Statement? inlined)
        {
            if (statement is AnyCall anyCall)
            { return TryInlineMacro(anyCall, out inlined); }

            inlined = null;
            return false;
        }
        protected bool TryInlineMacro(AnyCall anyCall, [NotNullWhen(true)] out Statement? inlined)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
            { return TryInlineMacro(functionCall, out inlined); }

            inlined = null;
            return false;
        }
        protected bool TryInlineMacro(FunctionCall functionCall, [NotNullWhen(true)] out Statement? inlined)
        {
            if (TryGetMacro(functionCall, out MacroDefinition? macro))
            {
                inlined = InlineMacro(macro, functionCall.MethodParameters);
                return true;
            }

            inlined = null;
            return false;
        }

        protected Statement InlineMacro(MacroDefinition macro, params StatementWithValue[] parameters)
        {
            Dictionary<string, StatementWithValue> _parameters = new();
            LanguageCore.Utils.Map(macro.Parameters, parameters, _parameters);

            return InlineMacro(macro, _parameters);
        }

        protected Statement InlineMacro(MacroDefinition macro, Dictionary<string, StatementWithValue> parameters)
        {
            Statement result;

            if (macro.Block.Statements.Length == 0)
            { throw new CompilerException($"Macro \"{macro.ReadableID()}\" has no statements", macro.Block, macro.FilePath); }
            else if (macro.Block.Statements.Length == 1)
            { result = InlineMacro(macro.Block.Statements[0], parameters); }
            else
            { result = InlineMacro(macro.Block, parameters); }

            result = Collapse(result, parameters);

            if (result is KeywordCall keywordCall &&
                keywordCall.Identifier == "return" &&
                keywordCall.Parameters.Length == 1)
            {
                result = keywordCall.Parameters[0];
            }

            return result;
        }

        protected static Block InlineMacro(Block block, Dictionary<string, StatementWithValue> parameters)
        {
            Statement[] statements = new Statement[block.Statements.Length];
            for (int i = 0; i < block.Statements.Length; i++)
            {
                Statement statement = block.Statements[i];
                statements[i] = InlineMacro(statement, parameters);

                if (statement is KeywordCall keywordCall &&
                    keywordCall.Identifier == "return")
                { break; }
            }
            return new Block(block.BracketStart, statements, block.BracketEnd);
        }

        protected static OperatorCall InlineMacro(OperatorCall operatorCall, Dictionary<string, StatementWithValue> parameters)
        {
            var left = InlineMacro(operatorCall.Left, parameters);
            var right = InlineMacro(operatorCall.Right, parameters);
            return new OperatorCall(operatorCall.Operator, left, right);
        }

        protected static KeywordCall InlineMacro(KeywordCall keywordCall, Dictionary<string, StatementWithValue> parameters)
        {
            StatementWithValue[] _parameters = InlineMacro(keywordCall.Parameters, parameters);
            return new KeywordCall(keywordCall.Identifier, _parameters);
        }

        protected static FunctionCall InlineMacro(FunctionCall functionCall, Dictionary<string, StatementWithValue> parameters)
        {
            StatementWithValue[] _parameters = InlineMacro(functionCall.Parameters, parameters);
            StatementWithValue? prevStatement = functionCall.PrevStatement;
            if (prevStatement != null)
            { prevStatement = InlineMacro(prevStatement, parameters); }
            return new FunctionCall(prevStatement, functionCall.Identifier, functionCall.BracketLeft, _parameters, functionCall.BracketRight);
        }

        protected static AnyCall InlineMacro(AnyCall anyCall, Dictionary<string, StatementWithValue> parameters)
        {
            StatementWithValue[] _parameters = InlineMacro(anyCall.Parameters, parameters);
            StatementWithValue prevStatement = anyCall.PrevStatement;
            prevStatement = InlineMacro(prevStatement, parameters);
            return new AnyCall(prevStatement, anyCall.BracketLeft, _parameters, anyCall.BracketRight);
        }

        protected static StatementWithValue[] InlineMacro(StatementWithValue[] statements, Dictionary<string, StatementWithValue> parameters)
        {
            StatementWithValue[] _parameters = new StatementWithValue[statements.Length];
            for (int i = 0; i < _parameters.Length; i++)
            {
                _parameters[i] = InlineMacro(statements[i], parameters);
            }
            return _parameters;
        }
        protected static Statement[] InlineMacro(Statement[] statements, Dictionary<string, StatementWithValue> parameters)
        {
            Statement[] _parameters = new Statement[statements.Length];
            for (int i = 0; i < _parameters.Length; i++)
            {
                _parameters[i] = InlineMacro(statements[i], parameters);
            }
            return _parameters;
        }

        protected static Statement InlineMacro(Statement statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement is Block block)
            { return InlineMacro(block, parameters); }

            if (statement is StatementWithValue statementWithValue)
            { return InlineMacro(statementWithValue, parameters); }

            return statement;
        }

        protected static Pointer InlineMacro(Pointer statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new Pointer(statement.OperatorToken, InlineMacro(statement.PrevStatement, parameters))
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        }

        protected static AddressGetter InlineMacro(AddressGetter statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new AddressGetter(statement.OperatorToken, InlineMacro(statement.PrevStatement, parameters))
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        }

        protected static StatementWithValue InlineMacro(StatementWithValue? statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement is Identifier identifier)
            {
                if (parameters.TryGetValue(identifier.Content, out StatementWithValue? inlinedStatement))
                { return inlinedStatement; }
                return new Identifier(identifier.Name);
            }

            if (statement is OperatorCall operatorCall)
            { return InlineMacro(operatorCall, parameters); }

            if (statement is KeywordCall keywordCall)
            { return InlineMacro(keywordCall, parameters); }

            if (statement is FunctionCall functionCall)
            { return InlineMacro(functionCall, parameters); }

            if (statement is AnyCall anyCall)
            { return InlineMacro(anyCall, parameters); }

            if (statement is Pointer pointer)
            { return InlineMacro(pointer, parameters); }

            if (statement is AddressGetter addressGetter)
            { return InlineMacro(addressGetter, parameters); }

            if (statement is LiteralStatement literal)
            {
                return new LiteralStatement(literal.Type, literal.Value, literal.ValueToken)
                {
                    ImaginaryPosition = literal.ImaginaryPosition,
                    SaveValue = literal.SaveValue,
                    Semicolon = literal.Semicolon,
                };
            }

            throw new NotImplementedException();
        }

        #endregion

        #region TryCompute()
        public static DataItem Compute(string @operator, DataItem left, DataItem right)
        {
            return @operator switch
            {
                "!" => !left,

                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => left,
                "%" => left,

                "&&" => new DataItem(left.Boolean && right.Boolean),
                "||" => new DataItem(left.Boolean || right.Boolean),

                "&" => left & right,
                "|" => left | right,
                "^" => left ^ right,

                "<<" => DataItem.BitshiftLeft(left, right),
                ">>" => DataItem.BitshiftRight(left, right),

                "<" => new DataItem(left < right),
                ">" => new DataItem(left > right),
                "==" => new DataItem(left == right),
                "!=" => new DataItem(left != right),
                "<=" => new DataItem(left <= right),
                ">=" => new DataItem(left >= right),
                _ => throw new NotImplementedException($"Unknown operator '{@operator}'"),
            };
        }

        protected bool TryCompute(OperatorCall @operator, RuntimeType? expectedType, out DataItem value)
        {
            if (GetOperator(@operator, out _))
            {
                value = DataItem.Null;
                return false;
            }

            if (!TryCompute(@operator.Left, expectedType, out DataItem leftValue))
            {
                value = DataItem.Null;
                return false;
            }

            string op = @operator.Operator.Content;

            if (op == "!")
            {
                value = !leftValue;
                return true;
            }

            if (@operator.Right != null)
            {
                if (TryCompute(@operator.Right, expectedType, out DataItem rightValue))
                {
                    value = Compute(op, leftValue, rightValue);
                    return true;
                }

                switch (op)
                {
                    case "&&":
                        {
                            if (!leftValue.Boolean)
                            {
                                value = new DataItem(false);
                                return true;
                            }
                            break;
                        }
                    case "||":
                        {
                            if (leftValue.Boolean)
                            {
                                value = new DataItem(true);
                                return true;
                            }
                            break;
                        }
                    default:
                        value = DataItem.Null;
                        return false;
                }
            }

            value = leftValue;
            return true;
        }
        public static bool TryComputeSimple(OperatorCall @operator, RuntimeType? expectedType, out DataItem value)
        {
            if (!TryComputeSimple(@operator.Left, expectedType, out DataItem leftValue))
            {
                value = DataItem.Null;
                return false;
            }

            string op = @operator.Operator.Content;

            if (op == "!")
            {
                value = !leftValue;
                return true;
            }

            if (@operator.Right != null)
            {
                if (TryComputeSimple(@operator.Right, expectedType, out DataItem rightValue))
                {
                    value = Compute(op, leftValue, rightValue);
                    return true;
                }

                switch (op)
                {
                    case "&&":
                        {
                            if (!leftValue.Boolean)
                            {
                                value = new DataItem(false);
                                return true;
                            }
                            break;
                        }
                    case "||":
                        {
                            if (leftValue.Boolean)
                            {
                                value = new DataItem(true);
                                return true;
                            }
                            break;
                        }
                    default:
                        value = DataItem.Null;
                        return false;
                }
            }

            value = leftValue;
            return true;
        }
        public static bool TryCompute(LiteralStatement literal, RuntimeType? expectedType, out DataItem value)
        {
            switch (literal.Type)
            {
                case LiteralType.Integer:
                    value = new DataItem(int.Parse(literal.Value));
                    break;
                case LiteralType.Float:
                    value = new DataItem(float.Parse(literal.Value.EndsWith('f') ? literal.Value[..^1] : literal.Value));
                    break;
                case LiteralType.Boolean:
                    value = new DataItem(bool.Parse(literal.Value));
                    break;
                case LiteralType.Char:
                    if (literal.Value.Length != 1)
                    {
                        value = DataItem.Null;
                        return false;
                    }
                    value = new DataItem(literal.Value[0]);
                    break;
                case LiteralType.String:
                default:
                    value = DataItem.Null;
                    return false;
            }
            Convert(ref value, expectedType);
            return true;
        }
        protected bool TryCompute(KeywordCall keywordCall, RuntimeType? expectedType, out DataItem value)
        {
            if (keywordCall.FunctionName == "sizeof")
            {
                if (keywordCall.Parameters.Length != 1)
                {
                    value = DataItem.Null;
                    return false;
                }

                StatementWithValue param0 = keywordCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                value = new DataItem(param0Type.Size);
                Convert(ref value, expectedType);
                return true;
            }

            value = DataItem.Null;
            return false;
        }
        protected bool TryCompute(AnyCall anyCall, RuntimeType? expectedType, out DataItem value)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
            { return TryCompute(functionCall, expectedType, out value); }

            value = DataItem.Null;
            return false;
        }
        protected bool TryCompute(FunctionCall functionCall, RuntimeType? expectedType, out DataItem value)
        {
            value = DataItem.Null;

            if (!TryGetMacro(functionCall, out MacroDefinition? macro))
            { return false; }

            Statement inlined = InlineMacro(macro, functionCall.Parameters);

            if (inlined is StatementWithValue statementWithValue)
            { return TryCompute(statementWithValue, expectedType, out value); }

            return false;
        }
        protected bool TryCompute(Identifier identifier, RuntimeType? expectedType, out DataItem value)
        {
            if (GetConstant(identifier.Content, out DataItem constantValue))
            {
                value = constantValue;
                Convert(ref value, expectedType);
                return true;
            }

            value = DataItem.Null;
            return false;
        }
        protected bool TryCompute(Field field, RuntimeType? expectedType, out DataItem value)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsStackArray && field.FieldName == "Length")
            {
                value = new DataItem(prevType.StackArraySize);
                Convert(ref value, expectedType);
                return true;
            }

            value = DataItem.Null;
            return false;
        }

        protected bool TryCompute(StatementWithValue? statement, RuntimeType? expectedType, out DataItem value)
        {
            if (statement is null)
            {
                value = DataItem.Null;
                return false;
            }

            if (statement is LiteralStatement literal)
            { return TryCompute(literal, expectedType, out value); }

            if (statement is OperatorCall @operator)
            { return TryCompute(@operator, expectedType, out value); }

            if (statement is KeywordCall keywordCall)
            { return TryCompute(keywordCall, expectedType, out value); }

            if (statement is FunctionCall functionCall)
            { return TryCompute(functionCall, expectedType, out value); }

            if (statement is AnyCall anyCall)
            { return TryCompute(anyCall, expectedType, out value); }

            if (statement is Identifier identifier)
            { return TryCompute(identifier, expectedType, out value); }

            value = DataItem.Null;
            return false;
        }
        public static bool TryComputeSimple(StatementWithValue? statement, RuntimeType? expectedType, out DataItem value)
        {
            if (statement is null)
            {
                value = DataItem.Null;
                return false;
            }

            if (statement is LiteralStatement literal)
            { return TryCompute(literal, expectedType, out value); }

            if (statement is OperatorCall @operator)
            { return TryComputeSimple(@operator, expectedType, out value); }

            value = DataItem.Null;
            return false;
        }
        #endregion

        protected static bool Convert(ref DataItem value, RuntimeType? type)
        {
            if (!type.HasValue) return false;
            return Convert(ref value, type.Value);
        }
        protected static bool Convert(ref DataItem value, RuntimeType type)
        {
            DataItem input = value;
            bool result = Convert(in input, type, out DataItem output);
            value = output;
            return result;
        }

        protected static bool Convert(in DataItem input, RuntimeType? type, out DataItem value)
        {
            value = input;

            if (type == null)
            { return false; }

            return Convert(in input, type.Value, out value);
        }
        protected static bool Convert(in DataItem input, RuntimeType type, out DataItem value)
        {
            switch (type)
            {
                case RuntimeType.UInt8:
                    switch (input.Type)
                    {
                        case RuntimeType.UInt8:
                            value = input;
                            return true;
                        case RuntimeType.SInt32:
                            if (input.ValueSInt32 >= byte.MinValue && input.ValueSInt32 <= byte.MaxValue)
                            {
                                value = new DataItem((byte)input.ValueSInt32);
                                return true;
                            }
                            value = input;
                            return false;
                        case RuntimeType.Single:
                            value = input;
                            return false;
                        case RuntimeType.UInt16:
                            if (input.ValueUInt16 >= byte.MinValue && input.ValueUInt16 <= byte.MaxValue)
                            {
                                value = new DataItem((byte)input.ValueUInt16);
                                return true;
                            }
                            value = input;
                            return false;
                        default:
                            value = input;
                            return false;
                    }
                case RuntimeType.SInt32:
                    switch (input.Type)
                    {
                        case RuntimeType.UInt8:
                            value = new DataItem((int)input.ValueUInt8);
                            return true;
                        case RuntimeType.SInt32:
                            value = input;
                            return true;
                        case RuntimeType.Single:
                            value = input;
                            return false;
                        case RuntimeType.UInt16:
                            value = new DataItem((int)input.ValueUInt16);
                            return true;
                        default:
                            value = input;
                            return false;
                    }
                case RuntimeType.Single:
                    switch (input.Type)
                    {
                        case RuntimeType.UInt8:
                            value = new DataItem((float)input.ValueUInt8);
                            return true;
                        case RuntimeType.SInt32:
                            value = new DataItem((float)input.ValueSInt32);
                            return true;
                        case RuntimeType.Single:
                            value = input;
                            return true;
                        case RuntimeType.UInt16:
                            value = new DataItem((float)input.ValueUInt16);
                            return true;
                        default:
                            value = input;
                            return false;
                    }
                case RuntimeType.UInt16:
                    switch (input.Type)
                    {
                        case RuntimeType.UInt8:
                            value = new DataItem((char)input.ValueUInt8);
                            return true;
                        case RuntimeType.SInt32:
                            if (input.ValueSInt32 >= char.MinValue && input.ValueSInt32 <= char.MaxValue)
                            {
                                value = new DataItem((char)input.ValueSInt32);
                                return true;
                            }
                            value = input;
                            return false;
                        case RuntimeType.Single:
                            value = input;
                            return false;
                        case RuntimeType.UInt16:
                            value = input;
                            return true;
                        default:
                            value = input;
                            return false;
                    }
                default:
                    value = input;
                    return false;
            }
        }

        #region Collapse()

        protected Statement Collapse(Statement statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement is VariableDeclaration newVariable)
            { return Collapse(newVariable, parameters); }
            else if (statement is Block block)
            { return Collapse(block, parameters); }
            else if (statement is AnyAssignment setter)
            { return Collapse(setter.ToAssignment(), parameters); }
            else if (statement is WhileLoop whileLoop)
            { return Collapse(whileLoop, parameters); }
            else if (statement is ForLoop forLoop)
            { return Collapse(forLoop, parameters); }
            else if (statement is IfContainer @if)
            { return Collapse(@if, parameters); }
            else if (statement is StatementWithValue statementWithValue)
            { return Collapse(statementWithValue, parameters); }
            else
            { throw new InternalException($"Statement \"{statement.GetType().Name}\" isn't collapsible"); }
        }

        protected StatementWithValue Collapse(StatementWithValue statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement is FunctionCall functionCall)
            { return Collapse(functionCall, parameters); }
            else if (statement is KeywordCall keywordCall)
            { return Collapse(keywordCall, parameters); }
            else if (statement is OperatorCall @operator)
            { return Collapse(@operator, parameters); }
            else if (statement is LiteralStatement literal)
            { return Collapse(literal, parameters); }
            else if (statement is Identifier variable)
            { return Collapse(variable, parameters); }
            else if (statement is AddressGetter memoryAddressGetter)
            { return Collapse(memoryAddressGetter, parameters); }
            else if (statement is Pointer memoryAddressFinder)
            { return Collapse(memoryAddressFinder, parameters); }
            else if (statement is NewInstance newStruct)
            { return Collapse(newStruct, parameters); }
            else if (statement is ConstructorCall constructorCall)
            { return Collapse(constructorCall, parameters); }
            else if (statement is IndexCall indexStatement)
            { return Collapse(indexStatement, parameters); }
            else if (statement is Field field)
            { return Collapse(field, parameters); }
            else if (statement is TypeCast @as)
            { return Collapse(@as, parameters); }
            else if (statement is ModifiedStatement modifiedStatement)
            { return Collapse(modifiedStatement, parameters); }
            else if (statement is AnyCall anyCall)
            { return Collapse(anyCall, parameters); }
            else
            { throw new InternalException($"Statement \"{statement.GetType().Name}\" isn't collapsible"); }
        }

        protected Statement Collapse(Block block, Dictionary<string, StatementWithValue> parameters)
        {
            if (block.Statements.Length == 1)
            { return Collapse(block.Statements[0], parameters); }

            List<Statement> newStatements = new();

            foreach (Statement statement in block.Statements)
            { newStatements.Add(Collapse(statement, parameters)); }

            return new Block(block.BracketStart, newStatements, block.BracketEnd)
            {
                Semicolon = block.Semicolon,
            };
        }
        protected VariableDeclaration Collapse(VariableDeclaration statement, Dictionary<string, StatementWithValue> parameters)
        { throw new NotImplementedException(); }
        protected StatementWithValue Collapse(FunctionCall statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (TryGetMacro(statement, out MacroDefinition? macro))
            {
                Statement inlined = InlineMacro(macro, parameters);

                if (inlined is StatementWithValue statementWithValue)
                { return statementWithValue; }
                else
                { throw new NotImplementedException(); }
            }
            return statement;
        }
        protected KeywordCall Collapse(KeywordCall statement, Dictionary<string, StatementWithValue> parameters)
        {
            List<StatementWithValue> newParameters = new();
            foreach (StatementWithValue parameter in statement.Parameters)
            { newParameters.Add(Collapse(parameter, parameters)); }

            return new KeywordCall(statement.Identifier, newParameters)
            {
                Semicolon = statement.Semicolon,
                SaveValue = statement.SaveValue,
            };
        }
        protected OperatorCall Collapse(OperatorCall statement, Dictionary<string, StatementWithValue> parameters)
        {
            StatementWithValue left = Collapse(statement.Left, parameters);
            StatementWithValue? right = statement.Right is not null ? Collapse(statement.Right, parameters) : null;
            return new OperatorCall(statement.Operator, left, right)
            {
                Semicolon = statement.Semicolon,
                InsideBracelet = statement.InsideBracelet,
                SaveValue = statement.SaveValue,
            };
        }
        protected Assignment Collapse(Assignment statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new Assignment(
                statement.Operator,
                Collapse(statement.Left, parameters),
                Collapse(statement.Right, parameters))
            {
                Semicolon = statement.Semicolon,
            };
        }
        protected CompoundAssignment Collapse(CompoundAssignment statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new CompoundAssignment(
                statement.Operator,
                Collapse(statement.Left, parameters),
                Collapse(statement.Right, parameters))
            {
                Semicolon = statement.Semicolon,
            };
        }
        protected ShortOperatorCall Collapse(ShortOperatorCall statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new ShortOperatorCall(
                statement.Operator,
                Collapse(statement.Left, parameters))
            {
                Semicolon = statement.Semicolon,
            };
        }
        protected AnyAssignment Collapse(AnyAssignment statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement is Assignment assignment)
            { return Collapse(assignment, parameters); }

            if (statement is CompoundAssignment compoundAssignment)
            { return Collapse(compoundAssignment, parameters); }

            if (statement is ShortOperatorCall shortOperatorCall)
            { return Collapse(shortOperatorCall, parameters); }

            throw new NotImplementedException();
        }
        protected LiteralStatement Collapse(LiteralStatement statement, Dictionary<string, StatementWithValue> parameters)
        { return statement; }
        protected StatementWithValue Collapse(Identifier statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (parameters.TryGetValue(statement.Content, out var parameter))
            { return parameter; }
            return statement;
        }
        protected AddressGetter Collapse(AddressGetter statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new AddressGetter(statement.OperatorToken, Collapse(statement.PrevStatement, parameters))
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        }
        protected Pointer Collapse(Pointer statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new Pointer(statement.OperatorToken, Collapse(statement.PrevStatement, parameters))
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        }
        protected WhileLoop Collapse(WhileLoop statement, Dictionary<string, StatementWithValue> parameters)
        { throw new NotImplementedException(); }
        protected ForLoop Collapse(ForLoop statement, Dictionary<string, StatementWithValue> parameters)
        { throw new NotImplementedException(); }
        protected Statement Collapse(IfContainer statement, Dictionary<string, StatementWithValue> parameters)
        {
            bool prevIsCollapsed = false;
            List<BaseBranch> branches = new();
            foreach (BaseBranch part in statement.Parts)
            {
                if (part is IfBranch ifBranch)
                {
                    StatementWithValue condition = ifBranch.Condition;
                    condition = Collapse(condition, parameters);
                    if (TryCompute(condition, null, out DataItem conditionValue))
                    {
                        if (conditionValue.Boolean)
                        {
                            Statement result = ifBranch.Block;
                            result = Collapse(result, parameters);
                            return result;
                        }

                        prevIsCollapsed = true;
                    }
                    else { prevIsCollapsed = false; }

                    branches.Add(new IfBranch(ifBranch.Keyword, condition, ifBranch.Block)
                    { Semicolon = ifBranch.Semicolon });
                    continue;
                }
                else if (part is ElseIfBranch elseIfBranch)
                {
                    StatementWithValue condition = elseIfBranch.Condition;
                    condition = Collapse(condition, parameters);
                    if (prevIsCollapsed && TryCompute(condition, null, out DataItem conditionValue))
                    {
                        if (conditionValue.Boolean)
                        {
                            Statement result = elseIfBranch.Block;
                            result = Collapse(result, parameters);
                            return result;
                        }

                        prevIsCollapsed = true;
                    }
                    else { prevIsCollapsed = false; }

                    branches.Add(new ElseIfBranch(elseIfBranch.Keyword, condition, elseIfBranch.Block)
                    { Semicolon = elseIfBranch.Semicolon });
                    continue;
                }
                else if (part is ElseBranch elseBranch)
                {
                    if (prevIsCollapsed)
                    {
                        Statement result = elseBranch.Block;
                        result = Collapse(result, parameters);
                        return result;
                    }

                    branches.Add(new ElseBranch(elseBranch.Keyword, elseBranch.Block)
                    { Semicolon = elseBranch.Semicolon });
                    continue;
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            return new IfContainer(branches) { Semicolon = statement.Semicolon };
        }
        protected NewInstance Collapse(NewInstance statement, Dictionary<string, StatementWithValue> parameters)
        { throw new NotImplementedException(); }
        protected ConstructorCall Collapse(ConstructorCall statement, Dictionary<string, StatementWithValue> parameters)
        { throw new NotImplementedException(); }
        protected IndexCall Collapse(IndexCall statement, Dictionary<string, StatementWithValue> parameters)
        { throw new NotImplementedException(); }
        protected Field Collapse(Field statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new Field(Collapse(statement.PrevStatement, parameters), statement.FieldName)
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        }
        protected TypeCast Collapse(TypeCast statement, Dictionary<string, StatementWithValue> parameters)
        { throw new NotImplementedException(); }
        protected ModifiedStatement Collapse(ModifiedStatement statement, Dictionary<string, StatementWithValue> parameters)
        { throw new NotImplementedException(); }
        protected StatementWithValue Collapse(AnyCall statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement.ToFunctionCall(out FunctionCall? functionCall))
            { return Collapse(functionCall, parameters); }

            throw new NotImplementedException();
        }

        #endregion

        protected static bool CanConvertImplicitly(CompiledType? from, CompiledType? to)
        {
            if (from is null || to is null) return false;

            if (from == Type.Integer &&
                to.IsEnum &&
                to.Enum.CompiledAttributes.HasAttribute("Define", "boolean"))
            { return true; }

            return false;
        }

        protected static bool TryConvertType(ref CompiledType? type, CompiledType? targetType)
        {
            if (type is null || targetType is null) return false;

            if (type == Type.Integer &&
                targetType.IsEnum &&
                targetType.Enum.CompiledAttributes.HasAttribute("Define", "boolean"))
            {
                type = targetType;
                return true;
            }

            if (type.IsClass && targetType == Type.Integer)
            {
                type = targetType;
                return true;
            }

            return false;
        }
    }
}
