﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.BBCode.Compiler
{
    using System.Runtime.CompilerServices;
    using ConsoleGUI;
    using LanguageCore.Runtime;
    using LanguageCore.Tokenizing;
    using Parser;
    using Parser.Statement;

    using LiteralStatement = Parser.Statement.Literal;

    public readonly struct FunctionNames
    {
        public const string Destructor = "destructor";
        public const string Cloner = "clone";
        public const string Constructor = "constructor";
        public const string IndexerGet = "indexer_get";
        public const string IndexerSet = "indexer_set";
    }

    public abstract class CodeGeneratorBase
    {
        protected readonly struct CompliableTemplate<T> where T : IDuplicatable<T>
        {
            internal readonly T OriginalFunction;
            internal readonly T Function;
            internal readonly Dictionary<string, CompiledType> TypeArguments;

            public CompliableTemplate(T function, Dictionary<string, CompiledType> typeArguments)
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
                    if (compiledFunction.ParameterTypes[i].IsGeneric)
                    {
                        if (!TypeArguments.TryGetValue(compiledFunction.ParameterTypes[i].Name, out CompiledType bruh))
                        { throw new InternalException(); }

                        compiledFunction.ParameterTypes[i] = bruh;
                        continue;
                    }

                    if (compiledFunction.ParameterTypes[i].IsClass && compiledFunction.ParameterTypes[i].Class.TemplateInfo != null)
                    {
                        Token[] classTypeParameters = compiledFunction.ParameterTypes[i].Class.TemplateInfo.TypeParameters;

                        CompiledType[] classTypeParameterValues = new CompiledType[classTypeParameters.Length];

                        foreach (KeyValuePair<string, CompiledType> item in this.TypeArguments)
                        {
                            if (compiledFunction.ParameterTypes[i].Class.TryGetTypeArgumentIndex(item.Key, out int j))
                            { classTypeParameterValues[j] = item.Value; }
                        }

                        for (int j = 0; j < classTypeParameterValues.Length; j++)
                        {
                            if (classTypeParameterValues[j] is null ||
                                classTypeParameterValues[j].IsGeneric)
                            { throw new InternalException(); }
                        }

                        compiledFunction.ParameterTypes[i] = new CompiledType(compiledFunction.ParameterTypes[i].Class, classTypeParameterValues);
                        continue;
                    }
                }

                if (compiledFunction.Type.IsGeneric)
                {
                    if (!TypeArguments.TryGetValue(compiledFunction.Type.Name, out CompiledType bruh))
                    { throw new InternalException(); }

                    compiledFunction.Type = bruh;
                }

                if (compiledFunction.Context != null && compiledFunction.Context.TemplateInfo != null)
                {
                    compiledFunction.Context = compiledFunction.Context.Duplicate();
                    compiledFunction.Context.AddTypeArguments(TypeArguments);
                }
            }
            void FinishInitialization(CompiledGeneralFunction compiledGeneralFunction)
            {
                for (int i = 0; i < compiledGeneralFunction.ParameterTypes.Length; i++)
                {
                    if (compiledGeneralFunction.ParameterTypes[i].IsGeneric)
                    {
                        if (!TypeArguments.TryGetValue(compiledGeneralFunction.ParameterTypes[i].Name, out CompiledType bruh))
                        { throw new InternalException(); }

                        compiledGeneralFunction.ParameterTypes[i] = bruh;
                        continue;
                    }

                    if (compiledGeneralFunction.Type.IsGeneric)
                    {
                        if (!TypeArguments.TryGetValue(compiledGeneralFunction.Type.Name, out CompiledType bruh))
                        { throw new InternalException(); }

                        compiledGeneralFunction.Type = bruh;
                    }

                    if (compiledGeneralFunction.ParameterTypes[i].IsClass && compiledGeneralFunction.ParameterTypes[i].Class.TemplateInfo != null)
                    {
                        Token[] classTypeParameters = compiledGeneralFunction.ParameterTypes[i].Class.TemplateInfo.TypeParameters;

                        CompiledType[] classTypeParameterValues = new CompiledType[classTypeParameters.Length];

                        foreach (KeyValuePair<string, CompiledType> item in this.TypeArguments)
                        {
                            if (compiledGeneralFunction.ParameterTypes[i].Class.TryGetTypeArgumentIndex(item.Key, out int j))
                            { classTypeParameterValues[j] = item.Value; }
                        }

                        for (int j = 0; j < classTypeParameterValues.Length; j++)
                        {
                            if (classTypeParameterValues[j] is null ||
                                classTypeParameterValues[j].IsGeneric)
                            { throw new InternalException(); }
                        }

                        compiledGeneralFunction.ParameterTypes[i] = new CompiledType(compiledGeneralFunction.ParameterTypes[i].Class, classTypeParameterValues);
                        continue;
                    }
                }

                if (compiledGeneralFunction.Context != null && compiledGeneralFunction.Context.TemplateInfo != null)
                {
                    compiledGeneralFunction.Context = compiledGeneralFunction.Context.Duplicate();
                    compiledGeneralFunction.Context.AddTypeArguments(TypeArguments);
                }
            }

            public override string ToString() => Function == null ? "null" : Function.ToString();
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

        protected IReadOnlyList<CompliableTemplate<CompiledFunction>> CompilableFunctions => compilableFunctions;
        protected IReadOnlyList<CompliableTemplate<CompiledOperator>> CompilableOperators => compilableOperators;
        protected IReadOnlyList<CompliableTemplate<CompiledGeneralFunction>> CompilableGeneralFunctions => compilableGeneralFunctions;

        readonly List<CompliableTemplate<CompiledFunction>> compilableFunctions = new();
        readonly List<CompliableTemplate<CompiledOperator>> compilableOperators = new();
        readonly List<CompliableTemplate<CompiledGeneralFunction>> compilableGeneralFunctions = new();

        protected readonly Dictionary<string, CompiledType> TypeArguments;

        protected readonly List<Error> Errors;
        protected readonly List<Warning> Warnings;
        protected readonly List<Hint> Hints;

        protected string CurrentFile;

        protected CodeGeneratorBase()
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

            TypeArguments = new Dictionary<string, CompiledType>();

            CompiledConstants = new Stack<CompiledConstant>();
            ConstantsStack = new Stack<int>();

            compilableFunctions = new List<CompliableTemplate<CompiledFunction>>();
            compilableOperators = new List<CompliableTemplate<CompiledOperator>>();
            compilableGeneralFunctions = new List<CompliableTemplate<CompiledGeneralFunction>>();
        }

        protected CodeGeneratorBase(Compiler.Result compilerResult) : this()
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

                CompiledConstants.Push(new CompiledConstant(variableDeclaration, constantValue));
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
                if (constant.Declaration.VariableName == identifier)
                {
                    if (success)
                    { throw new CompilerException($"Constant \"{constant.Declaration.VariableName}\" defined more than once", constant.Declaration, constant.Declaration.FilePath); }

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

        protected void AddTypeArguments(Dictionary<string, CompiledType> typeArguments)
        {
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
                if (@enum.Members[i].Value.Type != runtimeType)
                { return false; }
            }

            return true;
        }

        /// <param name="position">
        /// Used for exceptions
        /// </param>
        /// <exception cref="CompilerException"/>
        protected bool TryFindReplacedType(string builtinName, out CompiledType type)
        {
            type = null;

            string replacedName = TypeDefinitionReplacer(builtinName);

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
            string replacedName = TypeDefinitionReplacer(builtinName);

            if (replacedName == null)
            { throw new CompilerException($"Type replacer \"{builtinName}\" not found. Define a type with an attribute [Define(\"{builtinName}\")] to use it as a {builtinName}", position, CurrentFile); }

            return FindType(replacedName, position);
        }

        protected string TypeDefinitionReplacer(string typeName)
        {
            foreach (CompiledStruct @struct in CompiledStructs)
            {
                if (@struct.CompiledAttributes.TryGetAttribute("Define", out string definedType))
                {
                    if (definedType == typeName)
                    {
                        return @struct.Name.Content;
                    }
                }
            }
            foreach (CompiledClass @class in CompiledClasses)
            {
                if (@class.CompiledAttributes.TryGetAttribute("Define", out string definedType))
                {
                    if (definedType == typeName)
                    {
                        return @class.Name.Content;
                    }
                }
            }
            foreach (CompiledEnum @enum in CompiledEnums)
            {
                if (@enum.CompiledAttributes.TryGetAttribute("Define", out string definedType))
                {
                    if (definedType == typeName)
                    {
                        return @enum.Identifier.Content;
                    }
                }
            }
            return null;
        }

        protected bool GetEnum(string name, out CompiledEnum @enum)
            => CompiledEnums.TryGetValue(name, out @enum);

        protected abstract bool GetLocalSymbolType(string symbolName, out CompiledType type);

        protected bool GetFunctionByPointer(FunctionType functionType, out CompiledFunction compiledFunction)
        {
            bool found = false;
            compiledFunction = null;

            foreach (CompiledFunction function in CompiledFunctions)
            {
                if (function == null) continue;

                if (function.IsTemplate) continue;

                if (!functionType.Equals(function)) continue;

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {function} are the same", function.Identifier, function.FilePath); }

                compiledFunction = function;
                found = true;
            }

            foreach (CompliableTemplate<CompiledFunction> function in compilableFunctions)
            {
                if (function.Function == null) continue;

                if (!functionType.Equals(function.Function)) continue;

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {function} are the same", function.Function.Identifier, function.Function.FilePath); }

                compiledFunction = function.Function;
                found = true;
            }

            return found;
        }

        protected bool TryGetMacro(FunctionCall functionCallStatement, out MacroDefinition macro)
            => TryGetMacro(functionCallStatement.Identifier.Content, functionCallStatement.MethodParameters.Length, out macro);

        protected bool GetFunction(FunctionCall functionCallStatement, out CompiledFunction compiledFunction)
        {
            Token functionIdentifier = functionCallStatement.Identifier;
            StatementWithValue[] passedParameters = functionCallStatement.MethodParameters;

            if (TryGetFunction(functionIdentifier, passedParameters.Length, out CompiledFunction possibleFunction))
            {
                return GetFunction(functionIdentifier.Content, FindStatementTypes(passedParameters, possibleFunction.ParameterTypes), out compiledFunction);
            }
            else
            {
                return GetFunction(functionIdentifier.Content, FindStatementTypes(passedParameters), out compiledFunction);
            }
        }

        protected bool GetFunction(string name, CompiledType[] parameters, out CompiledFunction compiledFunction)
        {
            bool found = false;
            compiledFunction = null;

            foreach (CompiledFunction function in CompiledFunctions)
            {
                if (function == null) continue;

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
                if (function.Function == null) continue;

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
                if (element == null) continue;

                if (!element.IsTemplate) continue;

                if (element.Identifier != functionCallStatement.FunctionName) continue;

                if (!CompiledType.TryGetTypeParameters(element.ParameterTypes, parameters, out Dictionary<string, CompiledType> typeParameters)) continue;

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

                if (function.Identifier.Content != FunctionNames.Constructor) continue;
                if (function.Type.Class != @class) continue;
                if (function.ParameterCount != parameters.Length) continue;

                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, out var typeParameters)) continue;

                MapTypeParameters(constructorCall.TypeName, @class.TemplateInfo.TypeParameters, typeParameters);

                compiledGeneralFunction = new CompliableTemplate<CompiledGeneralFunction>(function, typeParameters);

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {function} are the same", function.Identifier, function.FilePath); }

                found = true;
            }

            return found;
        }

        protected bool GetIndexGetter(CompiledType prevType, out CompiledFunction compiledFunction)
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
                if (function.Identifier.Content != FunctionNames.IndexerGet) continue;

                if (function.ParameterTypes.Length != 2)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.INT)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (!function.ReturnSomething)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should return something", function.TypeToken, function.FilePath); }

                compiledFunction = function;
                return true;
            }

            for (int i = 0; i < compilableFunctions.Count; i++)
            {
                CompiledFunction function = compilableFunctions[i].Function;

                if (function.Context != context) continue;
                if (function.Identifier.Content != FunctionNames.IndexerGet) continue;

                if (function.ParameterTypes.Length != 2)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.INT)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (!function.ReturnSomething)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should return something", function.TypeToken, function.FilePath); }

                compiledFunction = function;
                return true;
            }

            compiledFunction = null;
            return false;
        }

        protected bool GetIndexSetter(CompiledType prevType, CompiledType elementType, out CompiledFunction compiledFunction)
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
                if (function.Identifier.Content != FunctionNames.IndexerSet) continue;

                if (function.ParameterTypes.Length < 3)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[2] != elementType)
                { continue; }

                if (function.ParameterTypes.Length > 3)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.INT)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ReturnSomething)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should not return anything", function.TypeToken, function.FilePath); }

                compiledFunction = function;
                return true;
            }

            for (int i = 0; i < compilableFunctions.Count; i++)
            {
                CompiledFunction function = compilableFunctions[i].Function;

                if (function.Context != context) continue;
                if (function.Identifier.Content != FunctionNames.IndexerSet) continue;

                if (function.ParameterTypes.Length < 3)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[2] != elementType)
                { continue; }

                if (function.ParameterTypes.Length > 3)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.INT)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ReturnSomething)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should not return anything", function.TypeToken, function.FilePath); }

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
                if (function.Identifier.Content != FunctionNames.IndexerGet) continue;

                if (function.ParameterTypes.Length != 2)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.INT)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (!function.ReturnSomething)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should return something", function.TypeToken, function.FilePath); }

                Dictionary<string, CompiledType> typeParameters = new(context.CurrentTypeArguments);

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
                if (function.Identifier.Content != FunctionNames.IndexerSet) continue;

                if (function.ParameterTypes.Length < 3)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (!function.ParameterTypes[2].IsGeneric && function.ParameterTypes[2] != elementType)
                { continue; }

                if (function.ParameterTypes.Length > 3)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.INT)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should have 1 integer parameter and 1 other parameter of any type", function.Identifier, function.FilePath); }

                if (function.ReturnSomething)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerSet}\" should not return anything", function.TypeToken, function.FilePath); }

                Dictionary<string, CompiledType> typeParameters = new(context.CurrentTypeArguments);

                compiledFunction = new CompliableTemplate<CompiledFunction>(function, typeParameters);
                context.ClearTypeArguments();
                return true;
            }

            compiledFunction = default;
            context.ClearTypeArguments();
            return false;
        }

        bool TryGetFunction(string name, out CompiledFunction compiledFunction)
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
        protected bool TryGetFunction(Token name, out CompiledFunction compiledFunction)
            => TryGetFunction(name.Content, out compiledFunction);

        bool TryGetFunction(string name, int parameterCount, out CompiledFunction compiledFunction)
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
        protected bool TryGetFunction(Token name, int parameterCount, out CompiledFunction compiledFunction)
            => TryGetFunction(name.Content, parameterCount, out compiledFunction);

        bool TryGetMacro(string name, int parameterCount, out MacroDefinition macro)
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
        protected bool TryGetMacro(Token name, int parameterCount, out MacroDefinition macro)
            => TryGetMacro(name.Content, parameterCount, out macro);

        protected bool GetFunction(FunctionType type, out CompiledFunction compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (!CompiledType.Equals(function.ParameterTypes, type.Parameters)) continue;
                if (!function.Type.Equals(type.ReturnType)) continue;

                if (compiledFunction != null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ReadableID()} (at {compiledFunction.Identifier.Position.ToMinString()}) ; {function.ReadableID()} (at {function.Identifier.Position.ToMinString()}) ; (and possibly more)", CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction != null;
        }

        bool GetFunction(string name, out CompiledFunction compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.Identifier != name) continue;

                if (compiledFunction != null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ReadableID()} (at {compiledFunction.Identifier.Position.ToMinString()}) ; {function.ReadableID()} (at {function.Identifier.Position.ToMinString()}) ; (and possibly more)", CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction != null;
        }

        protected bool GetFunction(Token name, out CompiledFunction compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.Identifier != name.Content) continue;

                if (compiledFunction is not null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ReadableID()} (at {compiledFunction.Identifier.Position.ToMinString()}) ; {function.ReadableID()} (at {function.Identifier.Position.ToMinString()}) ; (and possibly more)", name, CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }

        protected bool GetFunction(Token name, CompiledType type, out CompiledFunction compiledFunction)
        {
            if (type is null || !type.IsFunction)
            { return GetFunction(name, out compiledFunction); }
            return GetFunction(name, type.Function, out compiledFunction);
        }
        protected bool GetFunction(Token name, FunctionType type, out CompiledFunction compiledFunction)
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

        protected bool GetOperator(OperatorCall @operator, out CompiledOperator compiledOperator)
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
                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, out Dictionary<string, CompiledType> typeParameters)) continue;

                if (found)
                { throw new CompilerException($"Duplicated operator definitions: {compiledOperator} and {function} are the same", function.Identifier, function.FilePath); }

                compiledOperator = new CompliableTemplate<CompiledOperator>(function, typeParameters);

                found = true;
            }

            return found;
        }

        protected bool GetGeneralFunction(CompiledClass @class, string name, out CompiledGeneralFunction generalFunction)
            => GetGeneralFunction(@class, Array.Empty<CompiledType>(), name, out generalFunction);
        protected bool GetGeneralFunction(CompiledClass @class, CompiledType[] parameters, string name, out CompiledGeneralFunction generalFunction)
        {
            for (int i = 0; i < CompiledGeneralFunctions.Length; i++)
            {
                CompiledGeneralFunction function = CompiledGeneralFunctions[i];

                if (function.IsTemplate) continue;
                if (function.Identifier != name) continue;
                if (function.Type.Class != @class) continue;
                if (!CompiledType.Equals(function.ParameterTypes, parameters)) continue;

                generalFunction = function;
                return true;
            }

            for (int i = 0; i < compilableGeneralFunctions.Count; i++)
            {
                CompiledGeneralFunction function = compilableGeneralFunctions[i].Function;

                if (function.Identifier.Content != name) continue;
                if (function.Type.Class != @class) continue;
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
                if (function.Type.Class != @class) continue;
                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, out Dictionary<string, CompiledType> typeParameters)) continue;

                compiledGeneralFunction = new CompliableTemplate<CompiledGeneralFunction>(function, typeParameters);

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {compiledGeneralFunction.OriginalFunction} and {function} are the same", function.Identifier, function.FilePath); }
                found = true;
            }

            return found;
        }

        protected bool GetOutputWriter(CompiledType type, out CompiledFunction function)
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

        protected bool GetField(Field field, out CompiledField compiledField)
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

        protected bool GetStruct(NewInstance newStructStatement, out CompiledStruct compiledStruct)
            => GetStruct(newStructStatement.TypeName, out compiledStruct);

        protected bool GetStruct(TypeInstance type, out CompiledStruct compiledStruct)
            => GetStruct(type.Identifier.Content, out compiledStruct);

        protected bool GetStruct(string structName, out CompiledStruct compiledStruct)
        {
            for (int i = 0; i < CompiledStructs.Length; i++)
            {
                var @struct = CompiledStructs[i];

                if (@struct.Name != structName) continue;

                compiledStruct = @struct;
                return true;
            }

            compiledStruct = null;
            return false;
        }

        #endregion

        #region GetClass()

        protected bool GetClass(NewInstance newClassStatement, out CompiledClass compiledClass)
            => GetClass(newClassStatement.TypeName, out compiledClass);

        protected bool GetClass(ConstructorCall constructorCall, out CompiledClass compiledClass)
            => GetClass(constructorCall.TypeName, out compiledClass);

        protected bool GetClass(TypeInstance type, out CompiledClass compiledClass)
            => GetClass(type.Identifier.Content, type.GenericTypes.Count, out compiledClass);

        protected bool GetClass(string className, out CompiledClass compiledClass)
            => GetClass(className, 0, out compiledClass);
        protected bool GetClass(string className, int typeParameterCount, out CompiledClass compiledClass)
        {
            for (int i = 0; i < CompiledClasses.Length; i++)
            {
                var @class = CompiledClasses[i];

                if (@class.Name != className) continue;
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
        protected CompiledType FindType(string name, IThingWithPosition position) => FindType(name, position.GetPosition());

        /// <exception cref="CompilerException"/>
        protected CompiledType FindType(string name) => FindType(name, Position.UnknownPosition);

        /// <param name="position">
        /// Used for exceptions
        /// </param>
        /// <exception cref="CompilerException"/>
        CompiledType FindType(string name, Position position)
        {
            if (CompiledStructs.TryGetValue(name, out CompiledStruct @struct)) return new CompiledType(@struct);
            if (CompiledClasses.TryGetValue(name, out CompiledClass @class)) return new CompiledType(@class);
            if (CompiledEnums.TryGetValue(name, out CompiledEnum @enum)) return new CompiledType(@enum);

            if (TypeArguments.TryGetValue(name, out CompiledType typeArgument))
            { return typeArgument; }

            if (GetFunction(name, out CompiledFunction function))
            { return new CompiledType(new FunctionType(function)); }

            throw new CompilerException($"Type \"{name}\" not found", position, CurrentFile);
        }

        /// <exception cref="InternalException"/>
        protected CompiledType FindType(TypeInstance name)
            => new(name, FindType);

        #endregion

        /// <summary>
        /// Collects the type parameters from <paramref name="type"/> with names got from <paramref name="typeParameterNames"/> and puts the result to <paramref name="typeParameters"/>
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        void MapTypeParameters(TypeInstance type, Token[] typeParameterNames, Dictionary<string, CompiledType> typeParameters)
            => MapTypeParameters(new CompiledType(type, FindType), typeParameterNames, typeParameters);

        /// <summary>
        /// Collects the type parameters from <paramref name="type"/> with names got from <paramref name="typeParameterNames"/> and puts the result to <paramref name="typeParameters"/>
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        static void MapTypeParameters(CompiledType type, Token[] typeParameterNames, Dictionary<string, CompiledType> typeParameters)
        {
            if (type.TypeParameters.Length != typeParameterNames.Length)
            { throw new NotImplementedException($"There should be the same number of type parameter values as type parameter names"); }

            LanguageCore.Utils.Map(typeParameterNames, type.TypeParameters, typeParameters);
        }

        /// <summary>
        /// <para>
        /// Collects the type parameters from <paramref name="type"/> and puts the result to <paramref name="typeParameters"/>
        /// </para>
        /// <para>
        /// <paramref name="type"/> must be a class template type!
        /// </para>
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        static void MapTypeParameters(CompiledType type, Dictionary<string, CompiledType> typeParameters)
        {
            if (!type.IsClass)
            { return; }

            if (type.Class.TemplateInfo == null)
            { return; }

            MapTypeParameters(type, type.Class.TemplateInfo.TypeParameters, typeParameters);
        }

        protected CompiledVariable CompileVariable(VariableDeclaration newVariable, int memoryOffset, bool isGlobal)
        {
            if (Constants.Keywords.Contains(newVariable.VariableName.Content))
            { throw new CompilerException($"Illegal variable name '{newVariable.VariableName.Content}'", newVariable.VariableName, CurrentFile); }

            CompiledType type;
            if (newVariable.Type.Identifier == "var")
            {
                if (newVariable.InitialValue == null)
                { throw new CompilerException($"Initial value for variable declaration with implicit type is required", newVariable, newVariable.FilePath); }

                type = FindStatementType(newVariable.InitialValue);
            }
            else
            {
                type = new CompiledType(newVariable.Type, FindType, TryCompute);
            }

            return new CompiledVariable(
                memoryOffset,
                type,
                isGlobal,
                newVariable);
        }

        protected CompiledFunction GetCodeEntry()
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
                Type.BYTE => new DataItem((byte)0),
                Type.INT => new DataItem((int)0),
                Type.FLOAT => new DataItem((float)0f),
                Type.CHAR => new DataItem((char)'\0'),

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
            => type.Identifier.Content switch
            {
                "int" => new DataItem((int)0),
                "byte" => new DataItem((byte)0),
                "float" => new DataItem((float)0f),
                "char" => new DataItem((char)'\0'),

                "var" => throw new CompilerException("Undefined type", type.Identifier, null),
                "void" => throw new CompilerException("Invalid type", type.Identifier, null),
                _ => throw new InternalException($"Initial value for type \"{type.Identifier.Content}\" is unimplemented"),
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
            { return new DataItem(LanguageCore.Utils.NULL_POINTER); }

            if (type.IsEnum)
            {
                if (type.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{type.Enum.Identifier.Content}\" initial value: enum has no members", type.Enum.Identifier, type.Enum.FilePath); }

                return type.Enum.Members[0].Value;
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

        protected CompiledType FindStatementType(KeywordCall keywordCall)
        {
            if (keywordCall.FunctionName == "return") return new CompiledType(Type.VOID);

            if (keywordCall.FunctionName == "throw") return new CompiledType(Type.VOID);

            if (keywordCall.FunctionName == "break") return new CompiledType(Type.VOID);

            if (keywordCall.FunctionName == "sizeof") return new CompiledType(Type.INT);

            if (keywordCall.FunctionName == "delete") return new CompiledType(Type.VOID);

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

            if (!GetIndexGetter(prevType, out CompiledFunction indexer))
            {
                if (!GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index getter for type \"{prevType}\" not found", index, CurrentFile); }
                indexer = indexerTemplate.Function;
            }

            return indexer.Type;
        }
        protected CompiledType FindStatementType(FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "Dealloc") return new CompiledType(Type.VOID);

            if (functionCall.FunctionName == "Alloc") return new CompiledType(Type.INT);

            if (functionCall.FunctionName == "AllocFrom") return new CompiledType(Type.INT);

            if (functionCall.FunctionName == "sizeof") return new CompiledType(Type.INT);

            if (TryGetMacro(functionCall, out MacroDefinition macro))
            { return FindMacroType(macro, functionCall.Parameters); }

            if (!GetFunction(functionCall, out CompiledFunction compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out var compiledFunctionTemplate))
                { throw new CompilerException($"Function \"{functionCall.ReadableID(FindStatementType)}\" not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compiledFunctionTemplate.Function;
            }

            return compiledFunction.Type;
        }

        protected CompiledType FindStatementType(OperatorCall @operator, CompiledType expectedType)
        {
            if (Constants.Operators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
            {
                if (Constants.Operators.ParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {Constants.Operators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }
            }
            else
            { opcode = Opcode.UNKNOWN; }

            if (opcode == Opcode.UNKNOWN)
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }

            if (GetOperator(@operator, out CompiledOperator operatorDefinition))
            { return operatorDefinition.Type; }

            CompiledType leftType = FindStatementType(@operator.Left);
            if (@operator.Right == null)
            { return leftType; }

            CompiledType rightType = FindStatementType(@operator.Right);

            if (!leftType.CanBeBuiltin || !rightType.CanBeBuiltin || leftType.BuiltinType == Type.VOID || rightType.BuiltinType == Type.VOID)
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

            if (CanConvertImplicitly(result, expectedType)) return expectedType;

            return result;
        }
        protected CompiledType FindStatementType(LiteralStatement literal, CompiledType expectedType)
        {
            switch (literal.Type)
            {
                case LiteralType.INT:
                    if (expectedType == Type.BYTE &&
                        int.TryParse(literal.Value, out int value) &&
                        value >= byte.MinValue && value <= byte.MaxValue)
                    { return new CompiledType(Type.BYTE); }
                    return new CompiledType(Type.INT);
                case LiteralType.FLOAT:
                    return new CompiledType(Type.FLOAT);
                case LiteralType.BOOLEAN:
                    return FindReplacedType("boolean", literal);
                case LiteralType.STRING:
                    return FindReplacedType("string", literal);
                case LiteralType.CHAR:
                    return new CompiledType(Type.CHAR);
                default:
                    throw new ImpossibleException($"Unknown literal type {literal.Type}");
            }
        }
        protected CompiledType FindStatementType(Identifier identifier, CompiledType expectedType = null)
        {
            if (identifier.Content == "nullptr")
            { return new CompiledType(Type.INT); }

            if (GetConstant(identifier.Content, out DataItem constant))
            { return new CompiledType(constant.Type); }

            if (GetLocalSymbolType(identifier.Content, out CompiledType type))
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
        protected static CompiledType FindStatementType(AddressGetter _) => new(Type.INT);
        protected static CompiledType FindStatementType(Pointer _) => new(Type.UNKNOWN);
        protected CompiledType FindStatementType(NewInstance newInstance) => new(newInstance.TypeName, FindType);
        protected CompiledType FindStatementType(ConstructorCall constructorCall) => new(constructorCall.TypeName, FindType);
        protected CompiledType FindStatementType(Field field)
        {
            CompiledType prevStatementType = FindStatementType(field.PrevStatement);

            if (prevStatementType.IsStackArray && field.FieldName == "Length")
            {
                return new CompiledType(Type.INT);
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
                        if (this.TypeArguments.TryGetValue(definedField.Type.Name, out CompiledType typeParameter))
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
                            if (TypeArguments.TryGetValue(result.TypeParameters[j].Name, out CompiledType genericType))
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
                if (prevStatementType.Enum.Members.TryGetValue(field.FieldName.Content, out CompiledEnumMember enumMember))
                { return new CompiledType(enumMember.Value.Type); }

                throw new CompilerException($"Enum member \"{prevStatementType}\" not found in enum \"{prevStatementType.Enum.Identifier.Content}\"", field.FieldName, CurrentFile);
            }

            throw new CompilerException($"Class/struct/enum definition \"{prevStatementType}\" not found", field, CurrentFile);
        }
        protected CompiledType FindStatementType(TypeCast @as) => new(@as.Type, FindType);
        protected CompiledType FindStatementType(ModifiedStatement modifiedStatement, CompiledType expectedType)
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

        protected CompiledType FindStatementType(StatementWithValue statement)
            => FindStatementType(statement, null);
        protected CompiledType FindStatementType(StatementWithValue statement, CompiledType expectedType)
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

            throw new CompilerException($"Statement {statement.GetType().Name} does not have a type", statement, CurrentFile);
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
                CompiledType expectedType = null;
                if (i < expectedTypes.Length) expectedType = expectedTypes[i];
                result[i] = FindStatementType(statements[i], expectedType);
            }
            return result;
        }

        protected CompiledType FindMacroType(MacroDefinition macro, params StatementWithValue[] parameters)
        {
            Statement inlinedMacro = InlineMacro(macro, parameters);
            List<CompiledType> result = new();
            StatementFinder.GetAllStatement(inlinedMacro, (statement) =>
            {
                if (statement is KeywordCall keywordCall &&
                    keywordCall.Identifier == "return" &&
                    keywordCall.Parameters.Length > 0)
                {
                    if (keywordCall.Parameters.Length > 0)
                    { result.Add(FindStatementType(keywordCall.Parameters[0])); }
                    else
                    { result.Add(new CompiledType(Type.VOID)); }
                }
                return false;
            });
            if (result.Count == 0)
            { return new CompiledType(Type.VOID); }

            CompiledType result2 = result[0];
            for (int i = 1; i < result.Count; i++)
            {
                if (result[i] != result2)
                {
                    throw new CompilerException($"Macro \"{macro.ReadableID()}\" returns more than one type of value", macro.Block, macro.FilePath);
                }
            }
            return result2;
        }

        #endregion

        protected static Statement InlineMacro(MacroDefinition macro, params StatementWithValue[] parameters)
        {
            Dictionary<string, StatementWithValue> _parameters = new();
            LanguageCore.Utils.Map(macro.Parameters, parameters, _parameters);
            if (macro.Block.Statements.Count == 0)
            { throw new CompilerException($"Macro \"{macro.ReadableID()}\" has no statements", macro.Block, macro.FilePath); }
            else if (macro.Block.Statements.Count == 1)
            { return InlineMacro(macro.Block.Statements[0], _parameters); }
            else
            { return InlineMacro(macro.Block, _parameters); }
        }

        protected static Block InlineMacro(Block block, Dictionary<string, StatementWithValue> parameters)
        {
            Statement[] statements = new Statement[block.Statements.Count];
            for (int i = 0; i < block.Statements.Count; i++)
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
            StatementWithValue prevStatement = functionCall.PrevStatement;
            if (prevStatement != null)
            { prevStatement = InlineMacro(prevStatement, parameters); ; }
            return new FunctionCall(prevStatement, functionCall.Identifier, functionCall.BracketLeft, _parameters, functionCall.BracketRight);
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

            throw new NotImplementedException();
        }

        protected static StatementWithValue InlineMacro(StatementWithValue statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement is Identifier identifier)
            {
                if (parameters.TryGetValue(identifier.Content, out StatementWithValue inlinedStatement))
                { return inlinedStatement; }
            }

            if (statement is OperatorCall operatorCall)
            { return InlineMacro(operatorCall, parameters); }

            if (statement is KeywordCall keywordCall)
            { return InlineMacro(keywordCall, parameters); }

            if (statement is FunctionCall functionCall)
            { return InlineMacro(functionCall, parameters); }

            throw new NotImplementedException();
        }

        #region TryCompute()
        protected static DataItem Compute(string @operator, DataItem left, DataItem right)
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
        protected static bool TryCompute(LiteralStatement literal, RuntimeType? expectedType, out DataItem value)
        {
            switch (literal.Type)
            {
                case LiteralType.INT:
                    value = new DataItem(int.Parse(literal.Value));
                    if (expectedType.HasValue &&
                        expectedType.Value == RuntimeType.BYTE &&
                        value.ValueInt >= byte.MinValue && value.ValueInt <= byte.MaxValue)
                    { value = new DataItem((byte)value.ValueInt); }
                    return true;
                case LiteralType.FLOAT:
                    value = new DataItem(float.Parse(literal.Value.EndsWith('f') ? literal.Value[..^1] : literal.Value));
                    return true;
                case LiteralType.BOOLEAN:
                    value = new DataItem(bool.Parse(literal.Value));
                    return true;
                case LiteralType.CHAR:
                    if (literal.Value.Length != 1)
                    {
                        value = DataItem.Null;
                        return false;
                    }
                    value = new DataItem(literal.Value[0]);
                    return true;
                case LiteralType.STRING:
                default:
                    value = DataItem.Null;
                    return false;
            }
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

                if (!param0Type.IsClass)
                { throw new CompilerException($"{param0Type} is not a reference type", param0, CurrentFile); }

                if (expectedType == RuntimeType.BYTE)
                { value = new DataItem((byte)param0Type.SizeOnHeap); }
                else
                { value = new DataItem(param0Type.SizeOnHeap); }
                return true;
            }

            value = DataItem.Null;
            return false;
        }
        protected bool TryCompute(FunctionCall functionCall, RuntimeType? expectedType, out DataItem value)
        {
            value = default;

            if (!TryGetMacro(functionCall, out MacroDefinition macro))
            { return false; }

            Statement inlined = InlineMacro(macro, functionCall.Parameters);
            if (inlined is KeywordCall keywordCall &&
                keywordCall.Identifier == "return")
            {
                if (keywordCall.Parameters.Length > 0)
                {
                    return TryCompute(keywordCall.Parameters[0], expectedType, out value);
                }
            }

            return false;
        }

        protected bool TryCompute(StatementWithValue statement, RuntimeType? expectedType, out DataItem value)
        {
            if (statement is LiteralStatement literal)
            { return TryCompute(literal, expectedType, out value); }

            if (statement is OperatorCall @operator)
            { return TryCompute(@operator, expectedType, out value); }

            if (statement is KeywordCall keywordCall)
            { return TryCompute(keywordCall, expectedType, out value); }

            if (statement is FunctionCall functionCall)
            { return TryCompute(functionCall, expectedType, out value); }

            value = DataItem.Null;
            return false;
        }
        #endregion

        protected bool CanConvertImplicitly(CompiledType from, CompiledType to)
        {
            if (from is null || to is null) return false;

            if (from == Type.INT &&
                to.IsEnum &&
                to.Enum.CompiledAttributes.HasAttribute("Define", "boolean"))
            { return true; }

            return false;
        }
    }
}