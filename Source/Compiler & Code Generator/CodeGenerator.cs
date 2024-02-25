using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConsoleGUI;
using LanguageCore.BBCode.Generator;
using LanguageCore.Parser;
using LanguageCore.Parser.Statement;
using LanguageCore.Runtime;
using LanguageCore.Tokenizing;
using Microsoft.VisualBasic;
using LiteralStatement = LanguageCore.Parser.Statement.Literal;

namespace LanguageCore.Compiler
{
    public struct GeneratorSettings
    {
        public bool GenerateComments;
        public bool PrintInstructions;
        public bool DontOptimize;
        public bool GenerateDebugInstructions;
        public bool ExternalFunctionsCache;
        public bool CheckNullPointers;
        public CompileLevel CompileLevel;

        public readonly bool OptimizeCode => !DontOptimize;

        public GeneratorSettings(GeneratorSettings other)
        {
            GenerateComments = other.GenerateComments;
            PrintInstructions = other.PrintInstructions;
            DontOptimize = other.DontOptimize;
            GenerateDebugInstructions = other.GenerateDebugInstructions;
            ExternalFunctionsCache = other.ExternalFunctionsCache;
            CheckNullPointers = other.CheckNullPointers;
            CompileLevel = other.CompileLevel;
        }

        public static GeneratorSettings Default => new()
        {
            GenerateComments = true,
            PrintInstructions = false,
            DontOptimize = false,
            GenerateDebugInstructions = true,
            ExternalFunctionsCache = true,
            CheckNullPointers = true,
            CompileLevel = CompileLevel.Minimal,
        };
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

                switch (Function)
                {
                    case CompiledFunction compiledFunction:
                    {
                        CompiledType.InsertTypeParameters(compiledFunction.ParameterTypes, TypeArguments);
                        compiledFunction.Type =
                            CompiledType.InsertTypeParameters(compiledFunction.Type, TypeArguments) ??
                            compiledFunction.Type;
                        break;
                    }

                    case CompiledGeneralFunction compiledGeneralFunction:
                    {
                        CompiledType.InsertTypeParameters(compiledGeneralFunction.ParameterTypes, TypeArguments);
                        compiledGeneralFunction.Type =
                            CompiledType.InsertTypeParameters(compiledGeneralFunction.Type, TypeArguments) ??
                            compiledGeneralFunction.Type;
                        break;
                    }

                    case CompiledConstructor compiledConstructor:
                    {
                        CompiledType.InsertTypeParameters(compiledConstructor.ParameterTypes, TypeArguments);
                        compiledConstructor.Type =
                            CompiledType.InsertTypeParameters(compiledConstructor.Type, TypeArguments) ??
                            compiledConstructor.Type;
                        break;
                    }
                }
            }

            public override string ToString() => Function?.ToString() ?? "null";
        }

        protected delegate void BuiltinFunctionCompiler(params StatementWithValue[] parameters);

        protected CompiledStruct[] CompiledStructs;
        protected CompiledFunction[] CompiledFunctions;
        protected MacroDefinition[] CompiledMacros;
        protected CompiledOperator[] CompiledOperators;
        protected CompiledConstructor[] CompiledConstructors;
        protected CompiledGeneralFunction[] CompiledGeneralFunctions;
        protected CompiledEnum[] CompiledEnums;

        protected readonly Stack<CompiledConstant> CompiledConstants;
        protected readonly Stack<int> ConstantsStack;

        protected readonly List<CompiledParameter> CompiledParameters;
        protected readonly List<CompiledVariable> CompiledVariables;
        protected readonly List<CompiledVariable> CompiledGlobalVariables;

        protected IReadOnlyList<CompliableTemplate<CompiledFunction>> CompilableFunctions => compilableFunctions;
        protected IReadOnlyList<CompliableTemplate<CompiledOperator>> CompilableOperators => compilableOperators;
        protected IReadOnlyList<CompliableTemplate<CompiledGeneralFunction>> CompilableGeneralFunctions => compilableGeneralFunctions;
        protected IReadOnlyList<CompliableTemplate<CompiledConstructor>> CompilableConstructors => compilableConstructors;

        readonly List<CompliableTemplate<CompiledFunction>> compilableFunctions = new();
        readonly List<CompliableTemplate<CompiledOperator>> compilableOperators = new();
        readonly List<CompliableTemplate<CompiledGeneralFunction>> compilableGeneralFunctions = new();
        readonly List<CompliableTemplate<CompiledConstructor>> compilableConstructors = new();

        protected readonly TypeArguments TypeArguments;

        protected readonly AnalysisCollection? AnalysisCollection;

        protected Uri? CurrentFile;
        protected bool InFunction;

        protected readonly GeneratorSettings Settings;

        protected CodeGenerator()
        {
            CompiledStructs = Array.Empty<CompiledStruct>();
            CompiledFunctions = Array.Empty<CompiledFunction>();
            CompiledMacros = Array.Empty<MacroDefinition>();
            CompiledOperators = Array.Empty<CompiledOperator>();
            CompiledGeneralFunctions = Array.Empty<CompiledGeneralFunction>();
            CompiledConstructors = Array.Empty<CompiledConstructor>();
            CompiledEnums = Array.Empty<CompiledEnum>();

            CompiledConstants = new Stack<CompiledConstant>();
            ConstantsStack = new Stack<int>();

            CompiledParameters = new List<CompiledParameter>();
            CompiledVariables = new List<CompiledVariable>();
            CompiledGlobalVariables = new List<CompiledVariable>();

            compilableFunctions = new List<CompliableTemplate<CompiledFunction>>();
            compilableOperators = new List<CompliableTemplate<CompiledOperator>>();
            compilableGeneralFunctions = new List<CompliableTemplate<CompiledGeneralFunction>>();

            TypeArguments = new TypeArguments();

            AnalysisCollection = null;

            CurrentFile = null;
            InFunction = false;

            Settings = GeneratorSettings.Default;
        }

        protected CodeGenerator(CompilerResult compilerResult, GeneratorSettings settings, AnalysisCollection? analysisCollection) : this()
        {
            CompiledStructs = compilerResult.Structs;
            CompiledFunctions = compilerResult.Functions;
            CompiledMacros = compilerResult.Macros;
            CompiledOperators = compilerResult.Operators;
            CompiledConstructors = compilerResult.Constructors;
            CompiledGeneralFunctions = compilerResult.GeneralFunctions;
            CompiledEnums = compilerResult.Enums;

            AnalysisCollection = analysisCollection;

            Settings = settings;
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

                if (!TryCompute(variableDeclaration.InitialValue, out DataItem constantValue))
                { throw new CompilerException($"Constant value must be evaluated at compile-time", variableDeclaration.InitialValue, variableDeclaration.FilePath); }

                if (variableDeclaration.Type != "var")
                {
                    CompiledType constantType = new(variableDeclaration.Type, FindType, TryCompute);
                    variableDeclaration.Type.SetAnalyzedType(constantType);

                    if (!constantType.IsBuiltin)
                    { throw new NotSupportedException($"Only builtin types supported as a constant value"); }

                    DataItem.TryCast(ref constantValue, constantType.RuntimeType);
                }

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
                modifiedStatement.Modifier.Equals("temp"))
            {
                if (modifiedStatement.Statement is LiteralStatement ||
                    modifiedStatement.Statement is OperatorCall)
                {
                    AnalysisCollection?.Hints.Add(new Hint($"Unnecessary explicit temp modifier (this kind of statements (\"{modifiedStatement.Statement.GetType().Name}\") are implicitly deallocated)", modifiedStatement.Modifier, CurrentFile));
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

        #region AddCompilable()

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

        protected CompliableTemplate<CompiledConstructor> AddCompilable(CompliableTemplate<CompiledConstructor> compilable)
        {
            for (int i = 0; i < compilableConstructors.Count; i++)
            {
                if (compilableConstructors[i].Function.IsSame(compilable.Function))
                { return compilableConstructors[i]; }
            }
            compilableConstructors.Add(compilable);
            return compilable;
        }

        #endregion

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

        #region FindTypeReplacer()

        /// <param name="position"> Used for exceptions </param>
        /// <exception cref="CompilerException"/>
        protected CompiledType FindTypeReplacer(string typeName, IPositioned position)
        {
            CompiledType? replacedName = TryFindTypeReplacer(typeName);

            if (replacedName is null)
            { throw new CompilerException($"Type replacer \"{typeName}\" not found. Define a type with an attribute [Define(\"{typeName}\")] to use it as a {typeName}", position, CurrentFile); }

            return replacedName;
        }

        protected bool TryFindTypeReplacer(string typeName, [NotNullWhen(true)] out CompiledType? replacedType)
        {
            replacedType = TryFindTypeReplacer(typeName);
            return replacedType is not null;
        }

        protected CompiledType? TryFindTypeReplacer(string typeName)
        {
            foreach (CompiledStruct @struct in CompiledStructs)
            {
                if (!@struct.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                { continue; }
                if (!string.Equals(definedType, typeName, StringComparison.Ordinal))
                { continue; }

                return new CompiledType(@struct);
            }
            foreach (CompiledEnum @enum in CompiledEnums)
            {
                if (!@enum.CompiledAttributes.TryGetAttribute("Define", out string? definedType))
                { continue; }
                if (!string.Equals(definedType, typeName, StringComparison.Ordinal))
                { continue; }

                return new CompiledType(@enum);
            }

            return null;
        }

        #endregion

        #region GetEnum()

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

        #endregion

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

        protected bool GetConstructor(CompiledType type, CompiledType[] parameters, [NotNullWhen(true)] out CompiledConstructor? compiledFunction)
        {
            compiledFunction = null;

            {
                List<CompiledType> _parameters = new();
                _parameters.Add(type);
                _parameters.AddRange(parameters);
                parameters = _parameters.ToArray();
            }

            foreach (CompiledConstructor function in CompiledConstructors)
            {
                if (function is null) continue;
                if (function.IsTemplate) continue;
                if (function.Type != type) continue;
                if (!CompiledType.Equals(function.ParameterTypes, parameters)) continue;

                if (compiledFunction != null)
                { throw new CompilerException($"Duplicated constructor definitions: {compiledFunction} and {function} are the same", function.Identifier, function.FilePath); }

                compiledFunction = function;
            }

            foreach (CompliableTemplate<CompiledConstructor> function in compilableConstructors)
            {
                if (function.Function is null) continue;
                if (function.Function.Type != type) continue;
                if (!CompiledType.Equals(function.Function.ParameterTypes, parameters)) continue;

                if (compiledFunction != null)
                { throw new CompilerException($"Duplicated constructor definitions: {compiledFunction} and {function} are the same", function.Function.Identifier, function.Function.FilePath); }

                compiledFunction = function.Function;
            }

            return compiledFunction != null;
        }

        protected bool GetConstructorTemplate(CompiledType type, CompiledType[] parameters, out CompliableTemplate<CompiledConstructor> compiledConstructor)
        {
            bool found = false;
            compiledConstructor = default;

            {
                List<CompiledType> _parameters = new();
                _parameters.Add(type);
                _parameters.AddRange(parameters);
                parameters = _parameters.ToArray();
            }

            foreach (CompiledConstructor constructor in CompiledConstructors)
            {
                if (!constructor.IsTemplate) continue;
                if (constructor.ParameterCount != parameters.Length) continue;

                TypeArguments typeArguments = new(TypeArguments);

                if (!CompiledType.TryGetTypeParameters(constructor.ParameterTypes, parameters, typeArguments)) continue;

                compiledConstructor = new CompliableTemplate<CompiledConstructor>(constructor, typeArguments);

                if (found)
                { throw new CompilerException($"Duplicated constructor definitions: {compiledConstructor} and {constructor} are the same", constructor.Identifier, constructor.FilePath); }

                found = true;
            }

            return found;
        }

        protected bool GetIndexGetter(CompiledType prevType, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
            => GetFunction(
                BuiltinFunctionNames.IndexerGet,
                new CompiledType[] { prevType, new(Type.Integer) },
                out compiledFunction);

        protected bool GetIndexSetter(CompiledType prevType, CompiledType elementType, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
            => GetFunction(
                BuiltinFunctionNames.IndexerSet,
                new CompiledType[] { prevType, new(Type.Integer), elementType },
                out compiledFunction);

        protected bool GetIndexGetterTemplate(CompiledType prevType, out CompliableTemplate<CompiledFunction> compiledFunction)
            => GetFunctionTemplate(
                BuiltinFunctionNames.IndexerGet,
                new CompiledType[] { prevType, new(Type.Integer) },
                out compiledFunction);

        protected bool GetIndexSetterTemplate(CompiledType prevType, CompiledType elementType, out CompliableTemplate<CompiledFunction> compiledFunction)
        {
            if (prevType.IsStruct)
            {
                CompiledStruct context = prevType.Struct;

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

            compiledFunction = default;
            return false;
        }

        protected bool TryGetBuiltinFunction(string builtinName, CompiledType[] parameters, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.IsTemplate) continue;
                if (function.BuiltinFunctionName != builtinName) continue;

                if (compiledFunction is not null)
                { return false; }

                compiledFunction = function;
            }

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (!function.IsTemplate) continue;
                if (function.BuiltinFunctionName != builtinName) continue;

                if (compiledFunction is not null)
                { return false; }

                TypeArguments typeArguments = new(TypeArguments);

                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, typeArguments)) continue;

                compiledFunction = new CompliableTemplate<CompiledFunction>(function, typeArguments).Function;
            }

            return compiledFunction is not null;
        }

        #region TryGetMacro()

        protected bool TryGetMacro(FunctionCall functionCallStatement, [NotNullWhen(true)] out MacroDefinition? macro)
            => TryGetMacro(functionCallStatement.Identifier.Content, functionCallStatement.MethodParameters.Length, out macro);

        protected bool TryGetMacro(string name, int parameterCount, [NotNullWhen(true)] out MacroDefinition? macro)
        {
            macro = null;

            for (int i = 0; i < this.CompiledMacros.Length; i++)
            {
                MacroDefinition _macro = this.CompiledMacros[i];

                if (_macro.Identifier.Content != name) continue;
                if (_macro.ParameterCount != parameterCount) continue;

                if (macro is not null)
                { return false; }

                macro = _macro;
            }

            return macro is not null;
        }

        protected bool TryGetMacro(Token name, int parameterCount, [NotNullWhen(true)] out MacroDefinition? macro)
            => TryGetMacro(name.Content, parameterCount, out macro);

        #endregion

        #region GetFunction()

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

        protected bool GetFunctionTemplate(FunctionCall functionCallStatement, out CompliableTemplate<CompiledFunction> compiledFunction)
            => GetFunctionTemplate(functionCallStatement.FunctionName, FindStatementTypes(functionCallStatement.MethodParameters), out compiledFunction);

        protected bool GetFunctionTemplate(string identifier, CompiledType[] parameters, out CompliableTemplate<CompiledFunction> compiledFunction)
        {
            bool found = false;
            compiledFunction = default;

            foreach (CompiledFunction element in CompiledFunctions)
            {
                if (element is null) continue;

                if (!element.IsTemplate) continue;

                if (!element.Identifier.Equals(identifier)) continue;

                TypeArguments typeArguments = new(TypeArguments);

                if (!CompiledType.TryGetTypeParameters(element.ParameterTypes, parameters, typeArguments)) continue;

                // if (element.Context != null && element.Context.TemplateInfo != null)
                // { CollectTypeParameters(FindStatementType(functionCallStatement.PrevStatement), element.Context.TemplateInfo.TypeParameters, typeParameters); }

                CompliableTemplate<CompiledFunction> compiledFunction_ = new(element, typeArguments);

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {compiledFunction} and {compiledFunction_} are the same", element.Identifier, element.FilePath); }

                compiledFunction = compiledFunction_;

                found = true;
            }

            return found;
        }

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

            if (!found)
            {
                parameters = Utils.Duplicate(parameters);
                try
                { CompiledType.InsertTypeParameters(parameters, TypeArguments); }
                catch (Exception)
                { return false; }

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
            }

            return found;
        }

        bool TryGetFunction(string name, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (!function.Identifier.Equals(name)) continue;

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

                if (!function.Identifier.Equals(name)) continue;

                if (function.ParameterCount != parameterCount) continue;

                if (compiledFunction is not null)
                { return false; }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }
        protected bool TryGetFunction(Token name, int parameterCount, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
            => TryGetFunction(name.Content, parameterCount, out compiledFunction);

        protected bool GetFunction(FunctionType type, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (!CompiledType.Equals(function.ParameterTypes, type.Parameters)) continue;
                if (!function.Type.Equals(type.ReturnType)) continue;

                if (compiledFunction is not null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ToReadable()} (at {compiledFunction.Identifier.Position.ToStringRange()}) ; {function.ToReadable()} (at {function.Identifier.Position.ToStringRange()}) ; (and possibly more)", CurrentFile); }

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

                if (!function.Identifier.Equals(name)) continue;

                if (compiledFunction is not null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ToReadable()} (at {compiledFunction.Identifier.Position.ToStringRange()}) ; {function.ToReadable()} (at {function.Identifier.Position.ToStringRange()}) ; (and possibly more)", CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }

        protected bool GetFunction(Token name, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (!function.Identifier.Equals(name.Content)) continue;

                if (compiledFunction is not null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ToReadable()} (at {compiledFunction.Identifier.Position.ToStringRange()}) ; {function.ToReadable()} (at {function.Identifier.Position.ToStringRange()}) ; (and possibly more)", name, CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction is not null;
        }

        public static bool GetFunction(CompiledFunction[] compiledFunctions, Token name, [NotNullWhen(true)] out CompiledFunction? compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < compiledFunctions.Length; i++)
            {
                CompiledFunction function = compiledFunctions[i];

                if (!function.Identifier.Equals(name.Content)) continue;

                if (compiledFunction is not null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ToReadable()} (at {compiledFunction.Identifier.Position.ToStringRange()}) ; {function.ToReadable()} (at {function.Identifier.Position.ToStringRange()}) ; (and possibly more)", name); }

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

                if (!function.Identifier.Equals(name.Content)) continue;

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

        #endregion

        /// <exception cref="CompilerException"/>
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

                TypeArguments typeArguments = new(TypeArguments);

                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, typeArguments)) continue;

                if (found)
                { throw new CompilerException($"Duplicated operator definitions: {compiledOperator} and {function} are the same", function.Identifier, function.FilePath); }

                compiledOperator = new CompliableTemplate<CompiledOperator>(function, typeArguments);

                found = true;
            }

            return found;
        }

        static bool ContextIs(CompiledGeneralFunction function, CompiledType type)
            => type.IsStruct && function.Context is not null && function.Context == type.Struct;

        protected bool GetGeneralFunction(CompiledType context, CompiledType[] parameters, string name, [NotNullWhen(true)] out CompiledGeneralFunction? compiledFunction)
        {
            compiledFunction = null;

            foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
            {
                if (function.IsTemplate) continue;
                if (function.Identifier.Content != name) continue;
                if (!ContextIs(function, context)) continue;
                if (!CompiledType.Equals(function.ParameterTypes, parameters)) continue;

                if (compiledFunction != null)
                { throw new CompilerException($"Duplicated general function definitions: {compiledFunction} and {function} are the same", function.Identifier, function.FilePath); }

                compiledFunction = function;
            }

            foreach (CompliableTemplate<CompiledGeneralFunction> function in CompilableGeneralFunctions)
            {
                if (function.Function.Identifier.Content != name) continue;
                if (!ContextIs(function.Function, context)) continue;
                if (!CompiledType.Equals(function.Function.ParameterTypes, parameters)) continue;

                if (compiledFunction != null)
                { throw new CompilerException($"Duplicated general function definitions: {compiledFunction} and {function} are the same", function.Function.Identifier, function.Function.FilePath); }

                compiledFunction = function.Function;
            }

            return compiledFunction != null;
        }

        protected bool GetGeneralFunctionTemplate(CompiledType type, CompiledType[] parameters, string name, out CompliableTemplate<CompiledGeneralFunction> compiledGeneralFunction)
        {
            bool found = false;
            compiledGeneralFunction = default;

            foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
            {
                if (!function.IsTemplate) continue;
                if (function.Identifier.Content != name) continue;
                if (function.ParameterCount != parameters.Length) continue;
                if (!ContextIs(function, type.IsPointer ? type.PointerTo : type)) continue;

                TypeArguments typeArguments = new(TypeArguments);

                if (!CompiledType.TryGetTypeParameters(function.ParameterTypes, parameters, typeArguments)) continue;

                compiledGeneralFunction = new CompliableTemplate<CompiledGeneralFunction>(function, typeArguments);

                if (found)
                { throw new CompilerException($"Duplicated general function definitions: {compiledGeneralFunction} and {function} are the same", function.Identifier, function.FilePath); }

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

                if (_function.Parameters.Count != 1)
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
            { return GetStruct(type.Identifier.Content, type.GenericTypes.Length, out compiledStruct); }
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
            => CodeGenerator.GetStruct(CompiledStructs, structName, out compiledStruct);

        public static bool GetStruct(CompiledStruct?[] structs, string structName, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
        {
            for (int i = 0; i < structs.Length; i++)
            {
                CompiledStruct? @struct = structs[i];
                if (@struct == null) continue;

                if (@struct.Identifier.Content == structName)
                {
                    compiledStruct = @struct;
                    return true;
                }
            }
            compiledStruct = null;
            return false;
        }

        protected bool GetStruct(string structName, int typeParameterCount, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
            => CodeGenerator.GetStruct(CompiledStructs, structName, typeParameterCount, out compiledStruct);
        public static bool GetStruct(CompiledStruct?[] structs, string structName, int typeParameterCount, [NotNullWhen(true)] out CompiledStruct? compiledStruct)
        {
            for (int i = 0; i < structs.Length; i++)
            {
                CompiledStruct? @struct = structs[i];
                if (@struct == null) continue;

                if (@struct.Identifier.Content != structName) continue;
                if (typeParameterCount > 0 && @struct.TemplateInfo != null)
                { if (@struct.TemplateInfo.TypeParameters.Length != typeParameterCount) continue; }

                compiledStruct = @struct;
                return true;
            }

            compiledStruct = null;
            return false;
        }

        #endregion

        #region FindType()

        /// <exception cref="CompilerException"/>
        protected CompiledType FindType(Token name)
        {
            if (!FindType(name, out CompiledType? result))
            { throw new CompilerException($"Type \"{name}\" not found", name, CurrentFile); }

            return result;
        }

        protected bool FindType(Token name, [NotNullWhen(true)] out CompiledType? result)
        {
            if (LanguageConstants.BuiltinTypeMap3.TryGetValue(name.Content, out Type builtinType))
            {
                result = new CompiledType(builtinType);
                return true;
            }

            if (GetStruct(name.Content, out CompiledStruct? @struct))
            {
                name.AnalyzedType = TokenAnalyzedType.Struct;
                @struct.AddReference(new TypeInstanceSimple(name), CurrentFile);

                result = new CompiledType(@struct);
                return true;
            }

            if (GetEnum(name.Content, out CompiledEnum? @enum))
            {
                name.AnalyzedType = TokenAnalyzedType.Enum;
                result = new CompiledType(@enum);
                return true;
            }

            if (TypeArguments.TryGetValue(name.Content, out CompiledType? typeArgument))
            {
                result = typeArgument;
                return true;
            }

            if (GetFunction(name, out CompiledFunction? function))
            {
                name.AnalyzedType = TokenAnalyzedType.FunctionName;
                function.AddReference(new Identifier(name), CurrentFile);
                result = new CompiledType(new FunctionType(function));
                return true;
            }

            if (GetGlobalVariable(name.Content, out CompiledVariable? globalVariable))
            {
                result = globalVariable.Type;
                return true;
            }

            result = null;
            return false;
        }

        /// <exception cref="InternalException"/>
        /// <exception cref="CompilerException"/>
        protected CompiledType FindType(TypeInstance name)
            => new(name, FindType, TryCompute);

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

        protected bool GetGlobalVariable(string variableName, [NotNullWhen(true)] out CompiledVariable? compiledVariable)
        {
            foreach (CompiledVariable compiledVariable_ in CompiledGlobalVariables)
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

        protected void AssignTypeCheck(CompiledType destination, CompiledType valueType, StatementWithValue value)
        {
            if (destination == valueType)
            { return; }

            if (destination.Size != valueType.Size)
            { throw new CompilerException($"Can not set \"{valueType}\" (size of {valueType.Size}) value to {destination} (size of {destination.Size})", value, CurrentFile); }

            if (destination.IsEnum)
            { if (CodeGenerator.SameType(destination.Enum, valueType)) return; }

            if (valueType.IsEnum)
            { if (CodeGenerator.SameType(valueType.Enum, destination)) return; }

            if (destination.IsPointer &&
                valueType.IsBuiltin &&
                valueType.BuiltinType == Type.Integer)
            { return; }

            if (destination.IsBuiltin &&
                destination.BuiltinType == Type.Byte &&
                TryCompute(value, out DataItem yeah) &&
                yeah.Type == RuntimeType.SInt32)
            { return; }

            if (value is LiteralStatement literal &&
                literal.Type == LiteralType.String)
            {
                if (destination.IsStackArray &&
                    destination.StackArrayOf == Type.Char)
                {
                    string literalValue = literal.Value;
                    if (literalValue.Length != destination.StackArraySize)
                    { throw new CompilerException($"Can not set \"{literalValue}\" (length of {literalValue.Length}) value to stack array {destination} (length of {destination.StackArraySize})", value, CurrentFile); }
                    return;
                }

                if (destination.IsPointer &&
                    destination.PointerTo == Type.Char)
                { return; }
            }

            throw new CompilerException($"Can not set a {valueType} type value to the {destination} type", value, CurrentFile);
        }

        protected void AssignTypeCheck(CompiledType destination, DataItem value, IPositioned valuePosition)
        {
            CompiledType valueType = new(value.Type);

            if (destination == valueType)
            { return; }

            if (destination.Size != valueType.Size)
            { throw new CompilerException($"Can not set \"{valueType}\" (size of {valueType.Size}) value to {destination} (size of {destination.Size})", valuePosition, CurrentFile); }

            if (destination.IsEnum)
            { if (CodeGenerator.SameType(destination.Enum, valueType)) return; }

            if (destination.IsPointer)
            { return; }

            if (destination.IsBuiltin &&
                destination.BuiltinType == Type.Byte &&
                value.Type == RuntimeType.SInt32)
            { return; }

            throw new CompilerException($"Can not set a {valueType} type value to the {destination} type", valuePosition, CurrentFile);
        }

        #region Addressing Helpers

        /// <exception cref="NotImplementedException"/>
        protected ValueAddress GetDataAddress(StatementWithValue value) => value switch
        {
            IndexCall v => GetDataAddress(v),
            Identifier v => GetDataAddress(v),
            Field v => GetDataAddress(v),
            _ => throw new NotImplementedException()
        };
        protected ValueAddress GetDataAddress(Identifier variable)
        {
            if (GetConstant(variable.Content, out _))
            { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

            if (GetParameter(variable.Content, out CompiledParameter? parameter))
            { return GetBaseAddress(parameter); }

            if (GetVariable(variable.Content, out CompiledVariable? localVariable))
            { return new ValueAddress(localVariable); }

            if (GetGlobalVariable(variable.Content, out CompiledVariable? globalVariable))
            { return GetGlobalVariableAddress(globalVariable); }

            throw new CompilerException($"Local symbol \"{variable.Content}\" not found", variable, CurrentFile);
        }
        protected ValueAddress GetDataAddress(Field field)
        {
            ValueAddress address = GetBaseAddress(field);
            if (address.IsReference)
            { throw new NotImplementedException(); }
            int offset = GetDataOffset(field);
            return new ValueAddress(address.Address + offset, address.AddressingMode, address.IsReference, address.InHeap);
        }
        protected ValueAddress GetDataAddress(IndexCall indexCall)
        {
            ValueAddress address = GetBaseAddress(indexCall.PrevStatement!);
            if (address.IsReference)
            { throw new NotImplementedException(); }
            int currentOffset = GetDataOffset(indexCall);
            return new ValueAddress(address.Address + currentOffset, address.AddressingMode, address.IsReference, address.InHeap);
        }

        /// <exception cref="NotImplementedException"/>
        protected int GetDataOffset(StatementWithValue value) => value switch
        {
            IndexCall v => GetDataOffset(v),
            Field v => GetDataOffset(v),
            Identifier => 0,
            _ => throw new NotImplementedException()
        };
        protected int GetDataOffset(Field field)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            IReadOnlyDictionary<string, int> fieldOffsets;

            if (prevType.IsStruct)
            {
                prevType.Struct.AddTypeArguments(TypeArguments);
                prevType.Struct.AddTypeArguments(prevType.TypeParameters);

                fieldOffsets = prevType.Struct.FieldOffsets;

                prevType.Struct.ClearTypeArguments();
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

            if (!TryCompute(indexCall.Expression, out DataItem index))
            { throw new CompilerException($"Can't compute the index value", indexCall.Expression, CurrentFile); }

            int prevOffset = GetDataOffset(indexCall.PrevStatement);
            int offset = (int)index * prevType.StackArrayOf.Size;
            return prevOffset + offset;
        }

        /// <exception cref="NotImplementedException"/>
        protected ValueAddress GetBaseAddress(StatementWithValue statement) => statement switch
        {
            Identifier v => GetBaseAddress(v),
            Field v => GetBaseAddress(v),
            IndexCall v => GetBaseAddress(v),
            _ => throw new NotImplementedException()
        };
        protected abstract ValueAddress GetBaseAddress(CompiledParameter parameter);
        protected abstract ValueAddress GetBaseAddress(CompiledParameter parameter, int offset);
        protected abstract ValueAddress GetGlobalVariableAddress(CompiledVariable variable);
        /// <exception cref="CompilerException"/>
        protected ValueAddress GetBaseAddress(Identifier variable)
        {
            if (GetConstant(variable.Content, out _))
            { throw new CompilerException($"Constant does not have a memory address", variable, CurrentFile); }

            if (GetParameter(variable.Content, out CompiledParameter? parameter))
            { return GetBaseAddress(parameter); }

            if (GetVariable(variable.Content, out CompiledVariable? localVariable))
            { return new ValueAddress(localVariable); }

            if (GetGlobalVariable(variable.Content, out CompiledVariable? globalVariable))
            { return GetGlobalVariableAddress(globalVariable); }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
        }
        /// <exception cref="NotImplementedException"/>
        protected ValueAddress GetBaseAddress(Field statement)
        {
            ValueAddress address = GetBaseAddress(statement.PrevStatement);
            bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement).IsPointer;
            return new ValueAddress(address.Address, address.AddressingMode, address.IsReference, inHeap);
        }
        /// <exception cref="NotImplementedException"/>
        protected ValueAddress GetBaseAddress(IndexCall statement)
        {
            ValueAddress address = GetBaseAddress(statement.PrevStatement!);
            bool inHeap = address.InHeap || FindStatementType(statement.PrevStatement).IsPointer;
            return new ValueAddress(address.Address, address.AddressingMode, address.IsReference, inHeap);
        }

        /// <exception cref="NotImplementedException"/>
        protected bool IsItInHeap(StatementWithValue value) => value switch
        {
            Identifier => false,
            Field field => IsItInHeap(field),
            IndexCall indexCall => IsItInHeap(indexCall),
            _ => throw new NotImplementedException()
        };

        /// <exception cref="NotImplementedException"/>
        protected bool IsItInHeap(IndexCall indexCall)
            => IsItInHeap(indexCall.PrevStatement!) || FindStatementType(indexCall.PrevStatement).IsPointer;

        /// <exception cref="NotImplementedException"/>
        protected bool IsItInHeap(Field field)
            => IsItInHeap(field.PrevStatement) || FindStatementType(field.PrevStatement).IsPointer;

        #endregion

        protected CompiledVariable CompileVariable(VariableDeclaration newVariable, int memoryOffset)
        {
            if (LanguageConstants.Keywords.Contains(newVariable.VariableName.Content))
            { throw new CompilerException($"Illegal variable name \"{newVariable.VariableName.Content}\"", newVariable.VariableName, CurrentFile); }

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

                newVariable.Type.SetAnalyzedType(type);
                newVariable.CompiledType = type;
            }

            if (!type.AllGenericsDefined())
            { throw new InternalException($"Failed to qualify all generics in variable \"{newVariable.VariableName}\" type \"{type}\"", newVariable.FilePath); }

            return new CompiledVariable(memoryOffset, type, newVariable);
        }

        protected CompiledFunction? FindCodeEntry()
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
        protected static DataItem GetInitialValue(Type type) => type switch
        {
            Type.Byte => new DataItem((byte)0),
            Type.Integer => new DataItem((int)0),
            Type.Float => new DataItem((float)0f),
            Type.Char => new DataItem((char)'\0'),

            _ => throw new NotImplementedException($"Initial value for type \"{type}\" isn't implemented"),
        };

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem[] GetInitialValue(CompiledStruct @struct)
        {
            List<DataItem> result = new();

            foreach (CompiledField field in @struct.Fields)
            { result.Add(GetInitialValue(field.Type)); }

            if (result.Count != @struct.SizeOnStack)
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

                "var" => throw new CompilerException("Undefined type", type),
                "void" => throw new CompilerException("Invalid type", type),
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

            if (type.IsEnum)
            {
                if (type.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{type.Enum.Identifier.Content}\" initial value: enum has no members", type.Enum.Identifier, type.Enum.FilePath); }

                return type.Enum.Members[0].ComputedValue;
            }

            if (type.IsFunction)
            { return new DataItem(int.MaxValue); }

            if (type.IsBuiltin)
            { return GetInitialValue(type.BuiltinType); }

            if (type.IsPointer)
            { return new DataItem(0); }

            throw new NotImplementedException();
        }

        #endregion

        #region FindStatementType()

        protected virtual CompiledType OnGotStatementType(StatementWithValue statement, CompiledType type)
        {
            statement.CompiledType = type;
            return type;
        }

        protected CompiledType FindStatementType(AnyCall anyCall)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
            {
                return OnGotStatementType(anyCall, FindStatementType(functionCall));
            }

            CompiledType prevType = FindStatementType(anyCall.PrevStatement);

            if (!prevType.IsFunction)
            { throw new CompilerException($"This isn't a function", anyCall.PrevStatement, CurrentFile); }

            return OnGotStatementType(anyCall, prevType.Function.ReturnType);
        }
        protected CompiledType FindStatementType(KeywordCall keywordCall)
        {
            return keywordCall.FunctionName switch
            {
                "return" => OnGotStatementType(keywordCall, new CompiledType(Type.Void)),
                "throw" => OnGotStatementType(keywordCall, new CompiledType(Type.Void)),
                "break" => OnGotStatementType(keywordCall, new CompiledType(Type.Void)),
                "sizeof" => OnGotStatementType(keywordCall, new CompiledType(Type.Integer)),
                "delete" => OnGotStatementType(keywordCall, new CompiledType(Type.Void)),
                _ => throw new CompilerException($"Unknown keyword-function \"{keywordCall.FunctionName}\"", keywordCall.Identifier, CurrentFile)
            };
        }
        protected CompiledType FindStatementType(IndexCall index)
        {
            CompiledType prevType = FindStatementType(index.PrevStatement);

            if (prevType.IsStackArray)
            { return OnGotStatementType(index, prevType.StackArrayOf); }

            if (!GetIndexGetter(prevType, out CompiledFunction? indexer))
            {
                if (GetIndexGetterTemplate(prevType, out CompliableTemplate<CompiledFunction> indexerTemplate))
                {
                    indexer = indexerTemplate.Function;
                }
            }

            if (indexer == null && prevType.IsPointer)
            {
                return prevType.PointerTo;
            }

            if (indexer == null)
            { throw new CompilerException($"Index getter for type \"{prevType}\" not found", index, CurrentFile); }

            return OnGotStatementType(index, indexer.Type);
        }
        protected CompiledType FindStatementType(FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "sizeof") return new CompiledType(Type.Integer);

            if (TryGetMacro(functionCall, out MacroDefinition? macro))
            {
                functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
                return OnGotStatementType(functionCall, FindMacroType(macro, functionCall.Parameters));
            }

            if (!GetFunction(functionCall, out CompiledFunction? compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out CompliableTemplate<CompiledFunction> compiledFunctionTemplate))
                { throw new CompilerException($"Function \"{functionCall.ToReadable(FindStatementType)}\" not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compiledFunctionTemplate.Function;
            }

            functionCall.Identifier.AnalyzedType = TokenAnalyzedType.FunctionName;
            return OnGotStatementType(functionCall, compiledFunction.Type);
        }

        protected CompiledType FindStatementType(OperatorCall @operator, CompiledType? expectedType)
        {
            if (LanguageOperators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
            {
                if (LanguageOperators.ParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': required {LanguageOperators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }
            }
            else
            { opcode = Opcode.UNKNOWN; }

            if (opcode == Opcode.UNKNOWN)
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }

            if (GetOperator(@operator, out CompiledOperator? operatorDefinition))
            {
                @operator.Operator.AnalyzedType = TokenAnalyzedType.FunctionName;
                return OnGotStatementType(@operator, operatorDefinition.Type);
            }

            CompiledType leftType = FindStatementType(@operator.Left);
            if (@operator.Right == null)
            { return OnGotStatementType(@operator, leftType); }

            CompiledType rightType = FindStatementType(@operator.Right);

            if (!leftType.CanBeBuiltin || !rightType.CanBeBuiltin || leftType.BuiltinType == Type.Void || rightType.BuiltinType == Type.Void)
            { throw new CompilerException($"Unknown operator {leftType} {@operator.Operator.Content} {rightType}", @operator.Operator, CurrentFile); }

            DataItem leftValue = GetInitialValue(leftType);
            DataItem rightValue = GetInitialValue(rightType);

            DataItem predictedValue = @operator.Operator.Content switch
            {
                "!" => !leftValue,

                "+" => leftValue + rightValue,
                "-" => leftValue - rightValue,
                "*" => leftValue * rightValue,
                "/" => leftValue,
                "%" => leftValue,

                "&&" => new DataItem((bool)leftValue && (bool)rightValue),
                "||" => new DataItem((bool)leftValue || (bool)rightValue),

                "&" => leftValue & rightValue,
                "|" => leftValue | rightValue,
                "^" => leftValue ^ rightValue,

                "<<" => leftValue << rightValue,
                ">>" => leftValue >> rightValue,

                "<" => new DataItem(leftValue < rightValue),
                ">" => new DataItem(leftValue > rightValue),
                "==" => new DataItem(leftValue == rightValue),
                "!=" => new DataItem(leftValue != rightValue),
                "<=" => new DataItem(leftValue <= rightValue),
                ">=" => new DataItem(leftValue >= rightValue),

                _ => throw new NotImplementedException($"Unknown operator \"{@operator}\""),
            };

            CompiledType result = new(predictedValue.Type);

            if (expectedType is not null)
            {
                if (CanConvertImplicitly(result, expectedType))
                { return OnGotStatementType(@operator, expectedType); }

                if (result == Type.Integer &&
                    expectedType.IsPointer)
                { return OnGotStatementType(@operator, expectedType); }
            }

            return OnGotStatementType(@operator, result);
        }
        protected CompiledType FindStatementType(LiteralStatement literal, CompiledType? expectedType)
        {
            switch (literal.Type)
            {
                case LiteralType.Integer:
                    if (expectedType == Type.Byte &&
                        int.TryParse(literal.Value, out int value) &&
                        value >= byte.MinValue && value <= byte.MaxValue)
                    { return OnGotStatementType(literal, new CompiledType(Type.Byte)); }
                    return OnGotStatementType(literal, new CompiledType(Type.Integer));
                case LiteralType.Float:
                    return OnGotStatementType(literal, new CompiledType(Type.Float));
                case LiteralType.Boolean:
                    return OnGotStatementType(literal, FindTypeReplacer("boolean", literal));
                case LiteralType.String:
                    return OnGotStatementType(literal, CompiledType.Pointer(new CompiledType(Type.Char)));
                case LiteralType.Char:
                    return OnGotStatementType(literal, new CompiledType(Type.Char));
                default:
                    throw new UnreachableException($"Unknown literal type {literal.Type}");
            }
        }
        protected CompiledType FindStatementType(Identifier identifier, CompiledType? expectedType = null)
        {
            if (identifier.Content == "nullptr")
            { return new CompiledType(Type.Integer); }

            if (GetConstant(identifier.Content, out DataItem constant))
            {
                identifier.Token.AnalyzedType = TokenAnalyzedType.ConstantName;
                return OnGotStatementType(identifier, new CompiledType(constant.Type));
            }

            if (GetLocalSymbolType(identifier.Content, out CompiledType? type))
            {
                if (GetParameter(identifier.Content, out CompiledParameter? parameter))
                {
                    if (identifier.Content != "this")
                    { identifier.Token.AnalyzedType = TokenAnalyzedType.ParameterName; }
                    identifier.Reference = parameter;
                }
                else if (GetVariable(identifier.Content, out CompiledVariable? variable))
                {
                    identifier.Token.AnalyzedType = TokenAnalyzedType.VariableName;
                    identifier.Reference = variable;
                }
                else if (GetGlobalVariable(identifier.Content, out CompiledVariable? globalVariable))
                {
                    identifier.Token.AnalyzedType = TokenAnalyzedType.VariableName;
                    identifier.Reference = globalVariable;
                }

                return OnGotStatementType(identifier, type);
            }

            if (GetEnum(identifier.Content, out CompiledEnum? @enum))
            {
                identifier.Token.AnalyzedType = TokenAnalyzedType.Enum;
                return OnGotStatementType(identifier, new CompiledType(@enum));
            }

            if (GetFunction(identifier.Token, expectedType, out CompiledFunction? function))
            {
                identifier.Token.AnalyzedType = TokenAnalyzedType.FunctionName;
                return OnGotStatementType(identifier, new CompiledType(function));
            }

            for (int i = CurrentEvaluationContext.Count - 1; i >= 0; i--)
            {
                if (CurrentEvaluationContext[i].TryGetType(identifier, out CompiledType? _type))
                { return _type; }
            }

            if (FindType(identifier.Token, out CompiledType? result))
            { return OnGotStatementType(identifier, result); }

            throw new CompilerException($"Symbol \"{identifier.Content}\" not found", identifier, CurrentFile);
        }
        protected CompiledType FindStatementType(AddressGetter addressGetter)
        {
            CompiledType to = FindStatementType(addressGetter.PrevStatement);
            return OnGotStatementType(addressGetter, CompiledType.Pointer(to));
        }
        protected CompiledType FindStatementType(Pointer pointer)
        {
            CompiledType to = FindStatementType(pointer.PrevStatement);
            if (!to.IsPointer)
            { return new CompiledType(Type.Unknown); }
            return OnGotStatementType(pointer, to.PointerTo);
        }
        protected CompiledType FindStatementType(NewInstance newInstance)
        {
            CompiledType type = new(newInstance.TypeName, FindType);
            newInstance.TypeName.SetAnalyzedType(type);
            return OnGotStatementType(newInstance, type);
        }
        protected CompiledType FindStatementType(ConstructorCall constructorCall)
        {
            CompiledType type = new(constructorCall.TypeName, FindType);
            CompiledType[] parameters = FindStatementTypes(constructorCall.Parameters);

            if (GetConstructor(type, parameters, out CompiledConstructor? constructor))
            {
                constructorCall.TypeName.SetAnalyzedType(constructor.Type);
                return OnGotStatementType(constructorCall, constructor.Type);
            }

            if (GetConstructorTemplate(type, parameters, out CompliableTemplate<CompiledConstructor> compilableGeneralFunction))
            {
                constructorCall.TypeName.SetAnalyzedType(compilableGeneralFunction.Function.Type);
                return OnGotStatementType(constructorCall, compilableGeneralFunction.Function.Type);
            }

            throw new CompilerException($"Constructor {constructorCall.ToReadable(FindStatementType)} not found", constructorCall.Keyword, CurrentFile);
        }
        protected CompiledType FindStatementType(Field field)
        {
            CompiledType prevStatementType = FindStatementType(field.PrevStatement);

            if (prevStatementType.IsStackArray && field.FieldName.Equals("Length"))
            {
                field.FieldName.AnalyzedType = TokenAnalyzedType.FieldName;
                return OnGotStatementType(field, new CompiledType(Type.Integer));
            }

            if (prevStatementType.IsPointer)
            { prevStatementType = prevStatementType.PointerTo; }

            if (prevStatementType.IsStruct)
            {
                for (int i = 0; i < prevStatementType.Struct.Fields.Length; i++)
                {
                    CompiledField definedField = prevStatementType.Struct.Fields[i];

                    if (definedField.Identifier.Content != field.FieldName.Content) continue;
                    field.FieldName.AnalyzedType = TokenAnalyzedType.FieldName;

                    if (definedField.Type.IsGeneric)
                    {
                        if (this.TypeArguments.TryGetValue(definedField.Type.Name, out CompiledType? typeParameter))
                        { return OnGotStatementType(field, typeParameter); }

                        if (!prevStatementType.Struct.TryGetTypeArgumentIndex(definedField.Type.Name, out int j))
                        { throw new CompilerException($"Type argument \"{definedField.Type.Name}\" not found", definedField, prevStatementType.Struct.FilePath); }

                        if (prevStatementType.TypeParameters.Length <= j)
                        { throw new NotImplementedException(); }

                        return OnGotStatementType(field, prevStatementType.TypeParameters[j]);
                    }

                    CompiledType result = new(definedField.Type);

                    result = CompiledType.InsertTypeParameters(result, TypeArguments) ?? result;

                    for (int j = 0; j < result.TypeParameters.Length; j++)
                    {
                        if (result.TypeParameters[j].IsGeneric)
                        {
                            if (TypeArguments.TryGetValue(result.TypeParameters[j].Name, out CompiledType? genericType))
                            {
                                result.TypeParameters[j] = genericType;
                            }
                            else if (prevStatementType.Struct.TryGetTypeArgumentIndex(result.TypeParameters[j].Name, out int k))
                            {
                                if (result.TypeParameters.Length <= k)
                                { throw new NotImplementedException(); }

                                result.TypeParameters[j] = prevStatementType.TypeParameters[k];
                            }
                            else
                            { throw new CompilerException($"Type argument \"{result.TypeParameters[j].Name}\" not found", definedField, CurrentFile); }
                        }
                    }

                    return OnGotStatementType(field, result);
                }

                throw new CompilerException($"Field definition \"{field.FieldName}\" not found in type \"{prevStatementType}\"", field.FieldName, CurrentFile);
            }

            if (prevStatementType.IsEnum)
            {
                foreach (CompiledEnumMember enumMember in prevStatementType.Enum.Members)
                {
                    if (enumMember.Identifier.Content != field.FieldName.Content) continue;
                    field.FieldName.AnalyzedType = TokenAnalyzedType.EnumMember;
                    return OnGotStatementType(field, new CompiledType(enumMember.ComputedValue.Type));
                }

                throw new CompilerException($"Enum member \"{prevStatementType}\" not found in enum \"{prevStatementType.Enum.Identifier.Content}\"", field.FieldName, CurrentFile);
            }

            throw new CompilerException($"Type \"{prevStatementType}\" does not have a field \"{field.FieldName}\"", field, CurrentFile);
        }
        protected CompiledType FindStatementType(TypeCast @as)
        {
            CompiledType type = new(@as.Type, FindType);
            @as.Type.SetAnalyzedType(type);
            return OnGotStatementType(@as, type);
        }
        protected CompiledType FindStatementType(ModifiedStatement modifiedStatement, CompiledType? expectedType)
        {
            if (modifiedStatement.Modifier.Equals("ref"))
            {
                return OnGotStatementType(modifiedStatement, FindStatementType(modifiedStatement.Statement, expectedType));
            }

            if (modifiedStatement.Modifier.Equals("temp"))
            {
                return OnGotStatementType(modifiedStatement, FindStatementType(modifiedStatement.Statement, expectedType));
            }

            throw new CompilerException($"Unimplemented modifier \"{modifiedStatement.Modifier}\"", modifiedStatement.Modifier, CurrentFile);
        }

        [return: NotNullIfNotNull(nameof(statement))]
        protected CompiledType? FindStatementType(StatementWithValue? statement)
            => FindStatementType(statement, null);

        [return: NotNullIfNotNull(nameof(statement))]
        protected CompiledType? FindStatementType(StatementWithValue? statement, CompiledType? expectedType)
        {
            return statement switch
            {
                null => null,
                FunctionCall v => FindStatementType(v),
                OperatorCall v => FindStatementType(v, expectedType),
                LiteralStatement v => FindStatementType(v, expectedType),
                Identifier v => FindStatementType(v, expectedType),
                AddressGetter v => FindStatementType(v),
                Pointer v => FindStatementType(v),
                NewInstance v => FindStatementType(v),
                ConstructorCall v => FindStatementType(v),
                Field v => FindStatementType(v),
                TypeCast v => FindStatementType(v),
                KeywordCall v => FindStatementType(v),
                IndexCall v => FindStatementType(v),
                ModifiedStatement v => FindStatementType(v, expectedType),
                AnyCall v => FindStatementType(v),
                _ => throw new CompilerException($"Statement {statement.GetType().Name} does not have a type", statement, CurrentFile)
            };
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
            if (!InlineMacro(macro, out Statement? inlinedMacro, parameters))
            { throw new CompilerException($"Failed to inline the macro", new Position(parameters), CurrentFile); }

            if (inlinedMacro is StatementWithValue statementWithValue)
            { return FindStatementType(statementWithValue); }

            List<CompiledType> result = new();

            if (inlinedMacro.TryGetStatement(out KeywordCall? keywordCall, s => s.Identifier.Equals("return")))
            {
                if (keywordCall.Parameters.Length == 0)
                { result.Add(new CompiledType(Type.Void)); }
                else
                { result.Add(FindStatementType(keywordCall.Parameters[0])); }
            }

            if (result.Count == 0)
            { return new CompiledType(Type.Void); }

            for (int i = 1; i < result.Count; i++)
            {
                if (!result[i].Equals(result[0]))
                { throw new CompilerException($"Macro \"{macro.ToReadable()}\" returns more than one type of value", macro.Block, macro.FilePath); }
            }

            return result[0];
        }

        #endregion

        #region InlineMacro()

        protected class InlineException : Exception
        {

        }

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
                return InlineMacro(macro, out inlined, functionCall.MethodParameters);
            }

            inlined = null;
            return false;
        }

        protected bool InlineMacro(MacroDefinition macro, [NotNullWhen(true)] out Statement? inlined, params StatementWithValue[] parameters)
        {
            Dictionary<string, StatementWithValue> _parameters = Utils.Map(
                macro.Parameters,
                parameters,
                (key, value) => (key.Content, value));

            try
            {
                inlined = InlineMacro(macro, _parameters);
                return true;
            }
            catch (InlineException)
            {
                inlined = null;
                return false;
            }
        }

        /// <exception cref="InlineException"/>
        Statement InlineMacro(MacroDefinition macro, Dictionary<string, StatementWithValue> parameters)
        {
            Statement result;

            if (macro.Block.Statements.Length == 0)
            { throw new CompilerException($"Macro \"{macro.ToReadable()}\" has no statements", macro.Block, macro.FilePath); }
            else if (macro.Block.Statements.Length == 1)
            { result = InlineMacro(macro.Block.Statements[0], parameters); }
            else
            { result = InlineMacro(macro.Block, parameters); }

            result = Collapse(result, parameters);

            if (result is KeywordCall keywordCall &&
                keywordCall.Identifier.Equals("return") &&
                keywordCall.Parameters.Length == 1)
            { result = keywordCall.Parameters[0]; }

            return result;
        }

        protected bool InlineMacro(FunctionThingDefinition function, [NotNullWhen(true)] out Statement? inlined, params StatementWithValue[] parameters)
        {
            Dictionary<string, StatementWithValue> _parameters = Utils.Map(
                function.Parameters.ToArray(),
                parameters,
                (key, value) => (key.Identifier.Content, value));

            try
            {
                inlined = InlineMacro(function, _parameters);
                return true;
            }
            catch (InlineException)
            {
                inlined = null;
                return false;
            }
        }

        /// <exception cref="InlineException"/>
        Statement InlineMacro(FunctionThingDefinition function, Dictionary<string, StatementWithValue> parameters)
        {
            Statement result;

            if (function.Block is null || function.Block.Statements.Length == 0)
            { throw new CompilerException($"Function \"{function.ToReadable()}\" has no statements", function.Block, function.FilePath); }
            else if (function.Block.Statements.Length == 1)
            { result = InlineMacro(function.Block.Statements[0], parameters); }
            else
            { result = InlineMacro(function.Block, parameters); }

            result = Collapse(result, parameters);

            if (result is KeywordCall keywordCall &&
                keywordCall.Identifier.Equals("return") &&
                keywordCall.Parameters.Length == 1)
            { result = keywordCall.Parameters[0]; }

            return result;
        }

        /// <exception cref="InlineException"/>
        static Block InlineMacro(Block block, Dictionary<string, StatementWithValue> parameters)
        {
            Statement[] statements = new Statement[block.Statements.Length];
            for (int i = 0; i < block.Statements.Length; i++)
            {
                Statement statement = block.Statements[i];
                statements[i] = InlineMacro(statement, parameters);

                if (statement is KeywordCall keywordCall &&
                    keywordCall.Identifier.Equals("return"))
                { break; }
            }
            return new Block(block.BracketStart, statements, block.BracketEnd)
            {
                Semicolon = block.Semicolon,
            };
        }

        static OperatorCall InlineMacro(OperatorCall operatorCall, Dictionary<string, StatementWithValue> parameters)
            => new(
                op: operatorCall.Operator,
                left: InlineMacro(operatorCall.Left, parameters),
                right: InlineMacro(operatorCall.Right, parameters))
            {
                InsideBracelet = operatorCall.InsideBracelet,
                SaveValue = operatorCall.SaveValue,
                Semicolon = operatorCall.Semicolon,
            };

        static KeywordCall InlineMacro(KeywordCall keywordCall, Dictionary<string, StatementWithValue> parameters)
            => new(
                identifier: keywordCall.Identifier,
                parameters: InlineMacro(keywordCall.Parameters, parameters))
            {
                SaveValue = keywordCall.SaveValue,
                Semicolon = keywordCall.Semicolon,
            };

        static FunctionCall InlineMacro(FunctionCall functionCall, Dictionary<string, StatementWithValue> parameters)
        {
            IEnumerable<StatementWithValue> _parameters = InlineMacro(functionCall.Parameters, parameters);
            StatementWithValue? prevStatement = functionCall.PrevStatement;
            if (prevStatement != null)
            { prevStatement = InlineMacro(prevStatement, parameters); }
            return new FunctionCall(prevStatement, functionCall.Identifier, functionCall.BracketLeft, _parameters, functionCall.BracketRight);
        }

        static AnyCall InlineMacro(AnyCall anyCall, Dictionary<string, StatementWithValue> parameters)
            => new(
                prevStatement: InlineMacro(anyCall.PrevStatement, parameters),
                bracketLeft: anyCall.BracketLeft,
                parameters: InlineMacro(anyCall.Parameters, parameters),
                bracketRight: anyCall.BracketRight)
            {
                SaveValue = anyCall.SaveValue,
                Semicolon = anyCall.Semicolon,
            };

        static IEnumerable<StatementWithValue> InlineMacro(IEnumerable<StatementWithValue> statements, Dictionary<string, StatementWithValue> parameters)
        { return statements.Select(statement => InlineMacro(statement, parameters)); }

        /// <exception cref="InlineException"/>
        static IEnumerable<Statement> InlineMacro(IEnumerable<Statement> statements, Dictionary<string, StatementWithValue> parameters)
        { return statements.Select(statement => InlineMacro(statement, parameters)); }

        /// <exception cref="InlineException"/>
        static Statement InlineMacro(Statement statement, Dictionary<string, StatementWithValue> parameters) => statement switch
        {
            Block v => InlineMacro(v, parameters),
            StatementWithValue v => InlineMacro(v, parameters),
            ForLoop v => InlineMacro(v, parameters),
            IfContainer v => InlineMacro(v, parameters),
            AnyAssignment v => InlineMacro(v, parameters),
            VariableDeclaration v => InlineMacro(v, parameters),
            WhileLoop v => InlineMacro(v, parameters),
            _ => throw new NotImplementedException(statement.GetType().ToString()),
        };

        /// <exception cref="InlineException"/>
        static IfContainer InlineMacro(IfContainer statement, Dictionary<string, StatementWithValue> parameters)
        {
            BaseBranch[] branches = new BaseBranch[statement.Parts.Length];
            for (int i = 0; i < branches.Length; i++)
            { branches[i] = InlineMacro(statement.Parts[i], parameters); }
            return new IfContainer(branches)
            { Semicolon = statement.Semicolon, };
        }

        /// <exception cref="InlineException"/>
        static BaseBranch InlineMacro(BaseBranch statement, Dictionary<string, StatementWithValue> parameters) => statement switch
        {
            IfBranch v => InlineMacro(v, parameters),
            ElseIfBranch v => InlineMacro(v, parameters),
            ElseBranch v => InlineMacro(v, parameters),
            _ => throw new UnreachableException()
        };

        /// <exception cref="InlineException"/>
        static IfBranch InlineMacro(IfBranch statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                keyword: statement.Keyword,
                condition: InlineMacro(statement.Condition, parameters),
                block: InlineMacro(statement.Block, parameters))
            {
                Semicolon = statement.Semicolon,
            };

        /// <exception cref="InlineException"/>
        static ElseIfBranch InlineMacro(ElseIfBranch statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                keyword: statement.Keyword,
                condition: InlineMacro(statement.Condition, parameters),
                block: InlineMacro(statement.Block, parameters))
            {
                Semicolon = statement.Semicolon,
            };

        /// <exception cref="InlineException"/>
        static ElseBranch InlineMacro(ElseBranch statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                keyword: statement.Keyword,
                block: InlineMacro(statement.Block, parameters))
            {
                Semicolon = statement.Semicolon,
            };

        /// <exception cref="InlineException"/>
        static WhileLoop InlineMacro(WhileLoop statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                keyword: statement.Keyword,
                condition: InlineMacro(statement.Condition, parameters),
                block: InlineMacro(statement.Block, parameters)
                )
            {
                Semicolon = statement.Semicolon,
            };

        /// <exception cref="InlineException"/>
        static ForLoop InlineMacro(ForLoop statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                keyword: statement.Keyword,
                variableDeclaration: InlineMacro(statement.VariableDeclaration, parameters),
                condition: InlineMacro(statement.Condition, parameters),
                expression: InlineMacro(statement.Expression, parameters),
                block: InlineMacro(statement.Block, parameters)
                )
            {
                Semicolon = statement.Semicolon,
            };

        /// <exception cref="InlineException"/>
        static AnyAssignment InlineMacro(AnyAssignment statement, Dictionary<string, StatementWithValue> parameters) => statement switch
        {
            Assignment v => InlineMacro(v, parameters),
            ShortOperatorCall v => InlineMacro(v, parameters),
            CompoundAssignment v => InlineMacro(v, parameters),
            _ => throw new UnreachableException()
        };

        /// <exception cref="InlineException"/>
        static Assignment InlineMacro(Assignment statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement.Left is Identifier identifier &&
                parameters.ContainsKey(identifier.Content))
            { throw new InlineException(); }

            return new Assignment(
                @operator: statement.Operator,
                left: statement.Left,
                right: InlineMacro(statement.Right, parameters))
            {
                Semicolon = statement.Semicolon,
            };
        }

        /// <exception cref="InlineException"/>
        static ShortOperatorCall InlineMacro(ShortOperatorCall statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement.Left is Identifier identifier &&
                parameters.ContainsKey(identifier.Content))
            { throw new InlineException(); }

            return new ShortOperatorCall(
                 op: statement.Operator,
                 left: statement.Left)
            {
                Semicolon = statement.Semicolon,
            };
        }

        /// <exception cref="InlineException"/>
        static CompoundAssignment InlineMacro(CompoundAssignment statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement.Left is Identifier identifier &&
                parameters.ContainsKey(identifier.Content))
            { throw new InlineException(); }

            return new CompoundAssignment(
                @operator: statement.Operator,
                left: statement.Left,
                right: InlineMacro(statement.Right, parameters))
            {
                Semicolon = statement.Semicolon,
            };
        }

        /// <exception cref="InlineException"/>
        static VariableDeclaration InlineMacro(VariableDeclaration statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (parameters.ContainsKey(statement.VariableName.Content))
            { throw new InlineException(); }

            return new VariableDeclaration(
                modifiers: statement.Modifiers,
                type: statement.Type,
                variableName: statement.VariableName,
                initialValue: InlineMacro(statement.InitialValue, parameters))
            {
                FilePath = statement.FilePath,
                Semicolon = statement.Semicolon,
            };
        }

        static Pointer InlineMacro(Pointer statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                operatorToken: statement.OperatorToken,
                prevStatement: InlineMacro(statement.PrevStatement, parameters))
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };

        static AddressGetter InlineMacro(AddressGetter statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                operatorToken: statement.OperatorToken,
                prevStatement: InlineMacro(statement.PrevStatement, parameters))
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };

        static StatementWithValue InlineMacro(Identifier statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (parameters.TryGetValue(statement.Content, out StatementWithValue? inlinedStatement))
            { return inlinedStatement; }
            return statement;
        }

        static LiteralStatement InlineMacro(LiteralStatement statement, Dictionary<string, StatementWithValue> _)
            => new(
                type: statement.Type,
                value: statement.Value,
                valueToken: statement.ValueToken)
            {
                ImaginaryPosition = statement.ImaginaryPosition,
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };

        static Field InlineMacro(Field statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                prevStatement: InlineMacro(statement.PrevStatement, parameters),
                fieldName: statement.FieldName)
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };

        static IndexCall InlineMacro(IndexCall statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                prevStatement: InlineMacro(statement.PrevStatement, parameters),
                bracketLeft: statement.BracketLeft,
                indexStatement: InlineMacro(statement.Expression, parameters),
                bracketRight: statement.BracketRight)
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };

        static TypeCast InlineMacro(TypeCast statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                prevStatement: InlineMacro(statement.PrevStatement, parameters),
                keyword: statement.Keyword,
                type: statement.Type)
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,

                CompiledType = statement.CompiledType,
            };

        static ModifiedStatement InlineMacro(ModifiedStatement modifiedStatement, Dictionary<string, StatementWithValue> parameters)
            => new(
                modifier: modifiedStatement.Modifier,
                statement: InlineMacro(modifiedStatement.Statement, parameters))
            {
                SaveValue = modifiedStatement.SaveValue,
                Semicolon = modifiedStatement.Semicolon,

                CompiledType = modifiedStatement.CompiledType,
            };

        [return: NotNullIfNotNull(nameof(statement))]
        static StatementWithValue? InlineMacro(StatementWithValue? statement, Dictionary<string, StatementWithValue> parameters) => statement switch
        {
            null => null,
            Identifier v => InlineMacro(v, parameters),
            OperatorCall v => InlineMacro(v, parameters),
            KeywordCall v => InlineMacro(v, parameters),
            FunctionCall v => InlineMacro(v, parameters),
            AnyCall v => InlineMacro(v, parameters),
            Pointer v => InlineMacro(v, parameters),
            AddressGetter v => InlineMacro(v, parameters),
            LiteralStatement v => InlineMacro(v, parameters),
            Field v => InlineMacro(v, parameters),
            IndexCall v => InlineMacro(v, parameters),
            TypeCast v => InlineMacro(v, parameters),
            ModifiedStatement v => InlineMacro(v, parameters),
            _ => throw new NotImplementedException(statement.GetType().ToString()),
        };

        #endregion

        protected class EvaluationContext
        {
            readonly Dictionary<StatementWithValue, DataItem> _values;
            readonly Stack<Stack<Dictionary<string, DataItem>>>? _frames;

            public readonly List<Statement> RuntimeStatements;
            public Dictionary<string, DataItem>? LastScope => _frames?.Last.Last;
            public bool IsReturning;

            public static EvaluationContext Empty => new(null, null);

            public EvaluationContext(IDictionary<StatementWithValue, DataItem>? values, IDictionary<string, DataItem>? variables)
            {
                if (values != null)
                { _values = new Dictionary<StatementWithValue, DataItem>(values); }
                else
                { _values = new Dictionary<StatementWithValue, DataItem>(); }

                if (variables != null)
                { _frames = new Stack<Stack<Dictionary<string, DataItem>>>() { new() { new Dictionary<string, DataItem>(variables) } }; }
                else
                { _frames = null; }

                RuntimeStatements = new List<Statement>();
            }

            public bool TryGetValue(StatementWithValue statement, out DataItem value)
            {
                if (_values.TryGetValue(statement, out value))
                { return true; }

                if (statement is Identifier identifier &&
                    TryGetVariable(identifier.Content, out value))
                { return true; }

                value = default;
                return false;
            }

            public bool TryGetVariable(string name, out DataItem value)
            {
                value = default;

                if (_frames == null)
                { return false; }

                Stack<Dictionary<string, DataItem>> frame = _frames.Last;
                foreach (Dictionary<string, DataItem> scope in frame)
                {
                    if (scope.TryGetValue(name, out value))
                    { return true; }
                }

                return false;
            }

            public bool TrySetVariable(string name, DataItem value)
            {
                if (_frames == null)
                { return false; }

                Stack<Dictionary<string, DataItem>> frame = _frames.Last;
                foreach (Dictionary<string, DataItem> scope in frame)
                {
                    if (scope.ContainsKey(name))
                    {
                        scope[name] = value;
                        return true;
                    }
                }

                return false;
            }

            public bool TryGetType(StatementWithValue statement, [NotNullWhen(true)] out CompiledType? type)
            {
                if (!TryGetValue(statement, out DataItem value))
                {
                    type = null;
                    return false;
                }

                type = new CompiledType(value.Type);
                return true;
            }

            public void PushScope(IDictionary<string, DataItem>? variables = null)
            {
                if (_frames is null) return;
                if (variables is null)
                { _frames?.Last.Push(new Dictionary<string, DataItem>()); }
                else
                { _frames?.Last.Push(new Dictionary<string, DataItem>(variables)); }
            }

            public void PopScope()
            {
                if (_frames is null) return;
                _frames.Last.Pop();
            }
        }

        #region TryCompute()
        /// <exception cref="NotImplementedException"/>
        public static DataItem Compute(string @operator, DataItem left, DataItem right) => @operator switch
        {
            "!" => !left,

            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "%" => left % right,

            "&&" => new DataItem((bool)left && (bool)right),
            "||" => new DataItem((bool)left || (bool)right),

            "&" => left & right,
            "|" => left | right,
            "^" => left ^ right,

            "<<" => left << right,
            ">>" => left >> right,

            "<" => new DataItem(left < right),
            ">" => new DataItem(left > right),
            "==" => new DataItem(left == right),
            "!=" => new DataItem(left != right),
            "<=" => new DataItem(left <= right),
            ">=" => new DataItem(left >= right),

            _ => throw new NotImplementedException($"Unknown operator \"{@operator}\""),
        };

        bool TryCompute(Pointer pointer, EvaluationContext context, out DataItem value)
        {
            {
                if (pointer.PrevStatement is OperatorCall _operatorCall &&
                    _operatorCall.Left is AddressGetter _addressGetter &&
                    _addressGetter.PrevStatement is LiteralStatement _literal &&
                    _literal.Type == LiteralType.String &&
                    TryCompute(_operatorCall.Right, context, out DataItem _index))
                {
                    CompiledType stringType = FindTypeReplacer("string", _literal);
                    _index -= stringType.Size;
                    value = new DataItem(_literal.Value[(int)_index]);
                    return true;
                }
            }

            {
                if (pointer.PrevStatement is OperatorCall _operatorCall &&
                    _operatorCall.Left is TypeCast _typeCast &&
                    _typeCast.PrevStatement is LiteralStatement _literal &&
                    _literal.Type == LiteralType.String &&
                    TryCompute(_operatorCall.Right, context, out DataItem _index))
                {
                    CompiledType stringType = FindTypeReplacer("string", _literal);
                    _index -= stringType.Size;
                    value = new DataItem(_literal.Value[(int)_index]);
                    return true;
                }
            }

            value = DataItem.Null;
            return false;
        }
        bool TryCompute(OperatorCall @operator, EvaluationContext context, out DataItem value)
        {
            if (GetOperator(@operator, out _))
            {
                if (context is not null &&
                    context.TryGetValue(@operator, out value))
                { return true; }

                value = DataItem.Null;
                return false;
            }

            if (!TryCompute(@operator.Left, context, out DataItem leftValue))
            {
                if (context is not null &&
                    context.TryGetValue(@operator, out value))
                { return true; }

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
                if (TryCompute(@operator.Right, context, out DataItem rightValue))
                {
                    value = Compute(op, leftValue, rightValue);
                    return true;
                }

                switch (op)
                {
                    case "&&":
                    {
                        if (!(bool)leftValue)
                        {
                            value = new DataItem(false);
                            return true;
                        }
                        break;
                    }
                    case "||":
                    {
                        if ((bool)leftValue)
                        {
                            value = new DataItem(true);
                            return true;
                        }
                        break;
                    }
                    default:
                        if (context is not null &&
                            context.TryGetValue(@operator, out value))
                        { return true; }

                        value = DataItem.Null;
                        return false;
                }
            }

            value = leftValue;
            return true;
        }
        static bool TryCompute(LiteralStatement literal, out DataItem value)
        {
            switch (literal.Type)
            {
                case LiteralType.Integer:
                    value = new DataItem(literal.GetInt());
                    break;
                case LiteralType.Float:
                    value = new DataItem(literal.GetFloat());
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
            return true;
        }
        bool TryCompute(KeywordCall keywordCall, out DataItem value)
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
                return true;
            }

            value = DataItem.Null;
            return false;
        }
        bool TryCompute(AnyCall anyCall, EvaluationContext context, out DataItem value)
        {
            if (anyCall.ToFunctionCall(out FunctionCall? functionCall))
            { return TryCompute(functionCall, context, out value); }

            if (context is not null &&
                context.TryGetValue(anyCall, out value))
            { return true; }

            value = DataItem.Null;
            return false;
        }
        bool TryCompute(FunctionCall functionCall, EvaluationContext context, out DataItem value)
        {
            if (functionCall.FunctionName == "sizeof")
            {
                if (functionCall.Parameters.Length != 1)
                {
                    value = DataItem.Null;
                    return false;
                }

                StatementWithValue param0 = functionCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                value = new DataItem(param0Type.Size);
                return true;
            }

            if (TryGetMacro(functionCall, out MacroDefinition? macro))
            {
                if (!InlineMacro(macro, out Statement? inlined, functionCall.Parameters))
                { throw new CompilerException($"Failed to inline the macro", functionCall, CurrentFile); }

                if (inlined is StatementWithValue statementWithValue)
                { return TryCompute(statementWithValue, context, out value); }
            }

            if (GetFunction(functionCall, out CompiledFunction? function))
            {
                if (function.IsExternal &&
                    !functionCall.SaveValue &&
                    TryCompute(functionCall.MethodParameters, context, out DataItem[]? parameters))
                {
                    FunctionCall newFunctionCall = new(
                        null,
                        functionCall.Identifier,
                        functionCall.BracketLeft,
                        parameters.Select(v => Literal.CreateAnonymous(v, Position.UnknownPosition)),
                        functionCall.BracketRight)
                    {
                        SaveValue = functionCall.SaveValue,
                        Semicolon = functionCall.Semicolon,
                    };
                    context.RuntimeStatements.Add(newFunctionCall);
                    value = DataItem.Null;
                    return true;
                }

                if (function.IsInlineable &&
                    TryCompute(functionCall.MethodParameters, context, out parameters) &&
                    TryEvaluate(function, parameters, out DataItem? returnValue, out Statement[]? runtimeStatements) &&
                    returnValue.HasValue &&
                    runtimeStatements.Length == 0)
                {
                    value = returnValue.Value;
                    return true;
                }
            }

            if (context.TryGetValue(functionCall, out value))
            { return true; }

            value = DataItem.Null;
            return false;
        }
        bool TryCompute(Identifier identifier, EvaluationContext context, out DataItem value)
        {
            if (GetConstant(identifier.Content, out DataItem constantValue))
            {
                value = constantValue;
                return true;
            }

            if (context.TryGetValue(identifier, out value))
            { return true; }

            value = DataItem.Null;
            return false;
        }
        bool TryCompute(Field field, EvaluationContext context, out DataItem value)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsStackArray && field.FieldName.Equals("Length"))
            {
                value = new DataItem(prevType.StackArraySize);
                return true;
            }

            if (context.TryGetValue(field, out value))
            { return true; }

            value = DataItem.Null;
            return false;
        }
        bool TryCompute(TypeCast typeCast, EvaluationContext context, out DataItem value)
        {
            if (TryCompute(typeCast.PrevStatement, context, out value))
            {
                CompiledType type = new(typeCast.Type, FindType, TryCompute);
                if (!type.IsBuiltin) return false;
                DataItem.Cast(ref value, type.RuntimeType);
                return true;
            }

            value = DataItem.Null;
            return false;
        }
        bool TryCompute(IndexCall indexCall, EvaluationContext context, out DataItem value)
        {
            if (indexCall.PrevStatement is LiteralStatement literal &&
                literal.Type == LiteralType.String &&
                TryCompute(indexCall.Expression, context, out DataItem index))
            {
                if (index == literal.Value.Length)
                { value = new DataItem('\0'); }
                else
                { value = new DataItem(literal.Value[(int)index]); }
                return true;
            }

            value = DataItem.Null;
            return false;
        }
        protected bool TryEvaluate(CompiledFunction function, StatementWithValue[] parameters, out DataItem? value, [NotNullWhen(true)] out Statement[]? runtimeStatements)
        {
            value = null;
            runtimeStatements = null;

            if (function.ReturnSomething &&
                !function.Type.IsBuiltin)
            { return false; }

            if (function.Block is null)
            { return false; }

            if (!TryCompute(parameters, new EvaluationContext(null, null), out DataItem[]? parameterValues))
            { return false; }

            return TryEvaluate(function, parameterValues, out value, out runtimeStatements);
        }
        protected bool TryEvaluate(CompiledFunction function, DataItem[] parameterValues, out DataItem? value, [NotNullWhen(true)] out Statement[]? runtimeStatements)
        {
            value = null;
            runtimeStatements = null;

            if (function.ReturnSomething &&
                !function.Type.IsBuiltin)
            { return false; }

            if (function.Block is null)
            { return false; }

            Dictionary<string, DataItem> variables = new();

            if (function.ReturnSomething)
            { variables.Add("@return", GetInitialValue(function.Type.BuiltinType)); }

            for (int i = 0; i < parameterValues.Length; i++)
            { variables.Add(function.Parameters[i].Identifier.Content, parameterValues[i]); }

            EvaluationContext context = new(null, variables);

            CurrentEvaluationContext.Push(context);

            if (!TryEvaluate(function.Block, context))
            {
                CurrentEvaluationContext.Pop();
                return false;
            }

            CurrentEvaluationContext.Pop();

            if (function.ReturnSomething)
            { value = context.LastScope!["@return"]; }

            runtimeStatements = context.RuntimeStatements.ToArray();

            return true;
        }
        bool TryCompute(StatementWithValue[]? statements, EvaluationContext context, [NotNullWhen(true)] out DataItem[]? values)
        {
            if (statements is null)
            {
                values = null;
                return false;
            }

            values = new DataItem[statements.Length];

            for (int i = 0; i < statements.Length; i++)
            {
                StatementWithValue statement = statements[i];

                if (!TryCompute(statement, context, out DataItem value))
                {
                    values = null;
                    return false;
                }

                values[i] = value;
            }

            return true;
        }

        bool TryEvaluate(WhileLoop whileLoop, EvaluationContext context)
        {
            int iterations = 64;

            while (true)
            {
                if (!TryCompute(whileLoop.Condition, context, out DataItem condition))
                { return false; }

                if (!condition)
                { return true; }

                if (iterations-- < 0)
                { return false; }

                if (!TryEvaluate(whileLoop.Block, context))
                { return false; }
            }
        }
        bool TryEvaluate(Block block, EvaluationContext context)
        {
            context.PushScope();
            bool result = TryEvaluate(block.Statements, context);
            context.PopScope();
            return result;
        }
        bool TryEvaluate(VariableDeclaration variableDeclaration, EvaluationContext context)
        {
            DataItem value;

            if (context.LastScope is null)
            { return false; }

            if (variableDeclaration.InitialValue is null &&
                variableDeclaration.Type.ToString() != "var")
            {
                value = GetInitialValue(variableDeclaration.Type);
            }
            else
            {
                if (!TryCompute(variableDeclaration.InitialValue, context, out value))
                { return false; }
            }

            if (!context.LastScope.TryAdd(variableDeclaration.VariableName.Content, value))
            { return false; }

            return true;
        }
        bool TryEvaluate(AnyAssignment anyAssignment, EvaluationContext context)
        {
            Assignment assignment = anyAssignment.ToAssignment();

            if (assignment.Left is not Identifier identifier)
            { return false; }

            if (!TryCompute(assignment.Right, context, out DataItem value))
            { return false; }

            if (!context.TrySetVariable(identifier.Content, value))
            { return false; }

            return true;
        }
        bool TryEvaluate(KeywordCall keywordCall, EvaluationContext context)
        {
            if (keywordCall.Identifier.Content == "return")
            {
                context.IsReturning = true;

                if (keywordCall.Parameters.Length == 0)
                { return true; }

                if (keywordCall.Parameters.Length == 1)
                {
                    if (!TryCompute(keywordCall.Parameters[0], context, out DataItem returnValue))
                    { return false; }

                    if (!context.TrySetVariable("@return", returnValue))
                    { return false; }

                    return true;
                }

                return false;
            }

            return false;
        }
        bool TryEvaluate(Statement statement, EvaluationContext context) => statement switch
        {
            Block v => TryEvaluate(v, context),
            VariableDeclaration v => TryEvaluate(v, context),
            WhileLoop v => TryEvaluate(v, context),
            AnyAssignment v => TryEvaluate(v, context),
            KeywordCall v => TryEvaluate(v, context),
            StatementWithValue v => TryCompute(v, context, out _),
            IfContainer => false,
            _ => throw new NotImplementedException(statement.GetType().ToString()),
        };
        bool TryEvaluate(IEnumerable<Statement> statements, EvaluationContext context)
        {
            foreach (Statement statement in statements)
            {
                if (!TryEvaluate(statement, context))
                { return false; }
                if (context.IsReturning)
                { break; }
            }
            return true;
        }

        protected bool TryCompute(StatementWithValue? statement, out DataItem value)
            => TryCompute(statement, EvaluationContext.Empty, out value);

        readonly Stack<EvaluationContext> CurrentEvaluationContext = new();

        protected bool TryCompute(StatementWithValue? statement, EvaluationContext context, out DataItem value)
        {
            value = DataItem.Null;

            if (statement is null)
            { return false; }

            if (context.TryGetValue(statement, out value))
            { return true; }

            return statement switch
            {
                LiteralStatement v => TryCompute(v, out value),
                OperatorCall v => TryCompute(v, context, out value),
                Pointer v => TryCompute(v, context, out value),
                KeywordCall v => TryCompute(v, out value),
                FunctionCall v => TryCompute(v, context, out value),
                AnyCall v => TryCompute(v, context, out value),
                Identifier v => TryCompute(v, context, out value),
                TypeCast v => TryCompute(v, context, out value),
                Field v => TryCompute(v, context, out value),
                IndexCall v => TryCompute(v, context, out value),
                ModifiedStatement => false,
                NewInstance => false,
                _ => throw new NotImplementedException(statement.GetType().ToString()),
            };
        }

        public static bool TryComputeSimple(OperatorCall @operator, out DataItem value)
        {
            if (!TryComputeSimple(@operator.Left, out DataItem leftValue))
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
                if (TryComputeSimple(@operator.Right, out DataItem rightValue))
                {
                    value = Compute(op, leftValue, rightValue);
                    return true;
                }

                switch (op)
                {
                    case "&&":
                    {
                        if (!leftValue)
                        {
                            value = new DataItem(false);
                            return true;
                        }
                        break;
                    }
                    case "||":
                    {
                        if (leftValue)
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
        public static bool TryComputeSimple(IndexCall indexCall, out DataItem value)
        {
            if (indexCall.PrevStatement is LiteralStatement literal &&
                literal.Type == LiteralType.String &&
                TryComputeSimple(indexCall.Expression, out DataItem index))
            {
                if (index == literal.Value.Length)
                { value = new DataItem('\0'); }
                else
                { value = new DataItem(literal.Value[(int)index]); }
                return true;
            }

            value = DataItem.Null;
            return false;
        }
        public static bool TryComputeSimple(StatementWithValue? statement, out DataItem value)
        {
            value = DataItem.Null;
            return statement switch
            {
                LiteralStatement v => TryCompute(v, out value),
                OperatorCall v => TryComputeSimple(v, out value),
                IndexCall v => TryComputeSimple(v, out value),
                _ => false,
            };
        }
        #endregion

        #region Collapse()

        [return: NotNullIfNotNull(nameof(statement))]
        protected Statement? Collapse(Statement? statement, Dictionary<string, StatementWithValue> parameters) => statement switch
        {
            null => null,
            VariableDeclaration v => Collapse(v, parameters),
            Block v => Collapse(v, parameters),
            AnyAssignment v => Collapse(v.ToAssignment(), parameters),
            WhileLoop v => Collapse(v, parameters),
            ForLoop v => Collapse(v, parameters),
            IfContainer v => Collapse(v, parameters),
            StatementWithValue v => Collapse(v, parameters),
            _ => throw new InternalException($"Statement \"{statement.GetType().Name}\" isn't collapsible")
        };

        protected IEnumerable<Statement> Collapse(IEnumerable<Statement>? statements, Dictionary<string, StatementWithValue> parameters)
        {
            if (statements is null) yield break;
            foreach (Statement statement in statements)
            { yield return Collapse(statement, parameters); }
        }

        [return: NotNullIfNotNull(nameof(statement))]
        protected StatementWithValue? Collapse(StatementWithValue? statement, Dictionary<string, StatementWithValue> parameters) => statement switch
        {
            null => null,
            FunctionCall v => Collapse(v, parameters),
            KeywordCall v => Collapse(v, parameters),
            OperatorCall v => Collapse(v, parameters),
            LiteralStatement v => v,
            Identifier v => Collapse(v, parameters),
            AddressGetter v => Collapse(v, parameters),
            Pointer v => Collapse(v, parameters),
            NewInstance v => Collapse(v, parameters),
            ConstructorCall v => Collapse(v, parameters),
            IndexCall v => Collapse(v, parameters),
            Field v => Collapse(v, parameters),
            TypeCast v => Collapse(v, parameters),
            ModifiedStatement v => Collapse(v, parameters),
            AnyCall v => Collapse(v, parameters),
            _ => throw new InternalException($"Statement \"{statement.GetType().Name}\" isn't collapsible")
        };

        protected IEnumerable<StatementWithValue> Collapse(IEnumerable<StatementWithValue>? statements, Dictionary<string, StatementWithValue> parameters)
        {
            if (statements is null) yield break;
            foreach (StatementWithValue statement in statements)
            { yield return Collapse(statement, parameters); }
        }

        protected Statement Collapse(Block block, Dictionary<string, StatementWithValue> parameters)
        {
            if (block.Statements.Length == 1 &&
                block.Statements[0] is not VariableDeclaration)
            { return Collapse(block.Statements[0], parameters); }

            return new Block(block.BracketStart, Collapse(block.Statements, parameters), block.BracketEnd)
            {
                Semicolon = block.Semicolon,
            };
        }
        protected VariableDeclaration Collapse(VariableDeclaration statement, Dictionary<string, StatementWithValue> parameters)
        {
            return new VariableDeclaration(
                statement.Modifiers,
                statement.Type,
                statement.VariableName,
                Collapse(statement.InitialValue, parameters)
                )
            {
                FilePath = statement.FilePath,
                Semicolon = statement.Semicolon,
            };
        }
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
            => new(
                identifier: statement.Identifier,
                parameters: Collapse(statement.Parameters, parameters))
            {
                Semicolon = statement.Semicolon,
                SaveValue = statement.SaveValue,
            };
        protected OperatorCall Collapse(OperatorCall statement, Dictionary<string, StatementWithValue> parameters)
        {
            StatementWithValue left = Collapse(statement.Left, parameters);
            StatementWithValue? right = Collapse(statement.Right, parameters);
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
            return statement switch
            {
                Assignment v => Collapse(v, parameters),
                CompoundAssignment v => Collapse(v, parameters),
                ShortOperatorCall v => Collapse(v, parameters),
                _ => throw new NotImplementedException()
            };
        }
        protected static StatementWithValue Collapse(Identifier statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (parameters.TryGetValue(statement.Content, out StatementWithValue? parameter))
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
            => new(
                operatorToken: statement.OperatorToken,
                prevStatement: Collapse(statement.PrevStatement, parameters))
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        protected WhileLoop Collapse(WhileLoop statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                 keyword: statement.Keyword,
                 condition: Collapse(statement.Condition, parameters),
                 block: Block.CreateIfNotBlock(Collapse(statement.Block, parameters)))
            {
                Semicolon = statement.Semicolon
            };
        protected Statement Collapse(ForLoop statement, Dictionary<string, StatementWithValue> parameters)
        {
            ForLoop result = new(
                statement.Keyword,
                Collapse(statement.VariableDeclaration, parameters),
                Collapse(statement.Condition, parameters),
                Collapse(statement.Expression, parameters),
                statement.Block)
            { Semicolon = statement.Semicolon };

            if (TryComputeSimple(statement.Condition, out DataItem condition) &&
                !condition)
            { return result.VariableDeclaration; }

            return result;
        }
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
                    if (TryCompute(condition, EvaluationContext.Empty, out DataItem conditionValue))
                    {
                        if (conditionValue)
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
                    if (prevIsCollapsed && TryCompute(condition, EvaluationContext.Empty, out DataItem conditionValue))
                    {
                        if (conditionValue)
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
        protected static NewInstance Collapse(NewInstance statement, Dictionary<string, StatementWithValue> parameters)
        {
            return statement;
        }
        protected ConstructorCall Collapse(ConstructorCall statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                keyword: statement.Keyword,
                typeName: statement.TypeName,
                bracketLeft: statement.BracketLeft,
                parameters: Collapse(statement.Parameters, parameters),
                bracketRight: statement.BracketRight)
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        protected IndexCall Collapse(IndexCall statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                prevStatement: Collapse(statement.PrevStatement, parameters),
                bracketLeft: statement.BracketLeft,
                indexStatement: Collapse(statement.Expression, parameters),
                bracketRight: statement.BracketRight)
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        protected Field Collapse(Field statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                prevStatement: Collapse(statement.PrevStatement, parameters),
                fieldName: statement.FieldName)
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        protected StatementWithValue Collapse(TypeCast statement, Dictionary<string, StatementWithValue> parameters)
        {
            StatementWithValue prevStatement = Collapse(statement.PrevStatement, parameters);
            CompiledType prevType = FindStatementType(prevStatement);
            CompiledType targetType = new(statement.Type, FindType, TryCompute);
            if (targetType.Equals(prevType))
            { return prevStatement; }

            return new TypeCast(
                prevStatement: prevStatement,
                keyword: statement.Keyword,
                type: statement.Type)
            {
                CompiledType = statement.CompiledType,
                Semicolon = statement.Semicolon,
                SaveValue = statement.SaveValue,
            };
        }
        protected ModifiedStatement Collapse(ModifiedStatement statement, Dictionary<string, StatementWithValue> parameters)
            => new(
                modifier: statement.Modifier,
                statement: Collapse(statement.Statement, parameters))
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
                CompiledType = statement.CompiledType,
            };
        protected StatementWithValue Collapse(AnyCall statement, Dictionary<string, StatementWithValue> parameters)
        {
            if (statement.ToFunctionCall(out FunctionCall? functionCall))
            { return Collapse(functionCall, parameters); }

            return new AnyCall(
                prevStatement: Collapse(statement.PrevStatement, parameters),
                bracketLeft: statement.BracketLeft,
                parameters: Collapse(statement.Parameters, parameters),
                bracketRight: statement.BracketRight)
            {
                SaveValue = statement.SaveValue,
                Semicolon = statement.Semicolon,
            };
        }

        #endregion

        protected bool IsUnrollable(ForLoop loop)
        {
            string iteratorVariable = loop.VariableDeclaration.VariableName.Content;
            Dictionary<string, StatementWithValue> _params = new()
            {
                { iteratorVariable, Literal.CreateAnonymous(new DataItem(0), loop.VariableDeclaration) }
            };

            StatementWithValue condition = loop.Condition;
            Assignment iteratorExpression = loop.Expression.ToAssignment();

            if (iteratorExpression.Left is not Identifier iteratorExpressionLeft ||
                iteratorExpressionLeft.Content != iteratorVariable)
            { return false; }

            condition = InlineMacro(condition, _params);
            StatementWithValue iteratorExpressionRight = InlineMacro(iteratorExpression.Right, _params);

            if (!TryCompute(condition, EvaluationContext.Empty, out _) ||
                !TryCompute(iteratorExpressionRight, EvaluationContext.Empty, out _))
            { return false; }

            // TODO: return and break in unrolled loop
            if (loop.Block.TryGetStatement<KeywordCall>(out _, (statement) => statement.Identifier.Content is "break" or "return"))
            { return false; }

            return true;
        }

        protected Block[] Unroll(ForLoop loop, Dictionary<StatementWithValue, DataItem> values)
        {
            VariableDeclaration iteratorVariable = loop.VariableDeclaration;
            StatementWithValue condition = loop.Condition;
            AnyAssignment iteratorExpression = loop.Expression;

            DataItem iterator;
            if (iteratorVariable.InitialValue is not null)
            {
                if (!TryCompute(iteratorVariable.InitialValue, EvaluationContext.Empty, out iterator))
                { throw new CompilerException($"Failed to compute the iterator initial value (\"{iteratorVariable.InitialValue}\") for loop unrolling", iteratorVariable.InitialValue, CurrentFile); }
            }
            else
            {
                CompiledType iteratorType = new(iteratorVariable.Type, FindType, TryCompute);
                iteratorVariable.Type.SetAnalyzedType(iteratorType);
                iterator = GetInitialValue(iteratorType);
            }

            KeyValuePair<string, StatementWithValue> GetIteratorStatement()
                => new(iteratorVariable.VariableName.Content, Literal.CreateAnonymous(iterator, Position.UnknownPosition));

            DataItem ComputeIterator()
            {
                KeyValuePair<string, StatementWithValue> _yeah = GetIteratorStatement();
                StatementWithValue _condition = InlineMacro(condition, new Dictionary<string, StatementWithValue>()
                {
                    {_yeah.Key, _yeah.Value }
                });

                if (!TryCompute(_condition, new EvaluationContext(values, null), out DataItem result))
                { throw new CompilerException($"Failed to compute the condition value (\"{_condition}\") for loop unrolling", condition, CurrentFile); }

                return result;
            }

            DataItem ComputeExpression()
            {
                KeyValuePair<string, StatementWithValue> _yeah = GetIteratorStatement();
                Assignment assignment = iteratorExpression.ToAssignment();

                if (assignment.Left is not Identifier leftIdentifier)
                { throw new CompilerException($"Failed to unroll for loop", assignment.Left, CurrentFile); }

                StatementWithValue _value = InlineMacro(assignment.Right, new Dictionary<string, StatementWithValue>()
                {
                    { _yeah.Key, _yeah.Value }
                });

                if (!TryCompute(_value, new EvaluationContext(values, null), out DataItem result))
                { throw new CompilerException($"Failed to compute the condition value (\"{_value}\") for loop unrolling", condition, CurrentFile); }

                return result;
            }

            List<Block> statements = new();

            while (ComputeIterator())
            {
                KeyValuePair<string, StatementWithValue> _yeah = GetIteratorStatement();
                Block subBlock = InlineMacro(loop.Block, new Dictionary<string, StatementWithValue>()
                {
                    { _yeah.Key, _yeah.Value }
                });
                statements.Add(subBlock);

                iterator = ComputeExpression();
            }

            return statements.ToArray();
        }

        protected static bool CanConvertImplicitly(CompiledType? from, CompiledType? to)
        {
            if (from is null || to is null) return false;

            if (to.IsEnum && to.Enum.Type == from)
            { return true; }

            return false;
        }

        protected static bool TryConvertType(ref CompiledType? type, CompiledType? targetType)
        {
            if (type is null || targetType is null) return false;

            if (targetType.IsEnum && targetType.Enum.Type == type)
            {
                type = targetType;
                return true;
            }

            return false;
        }
    }
}
