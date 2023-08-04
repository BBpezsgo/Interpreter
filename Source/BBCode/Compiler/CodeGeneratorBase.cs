using System;
using System.Collections.Generic;

namespace ProgrammingLanguage.BBCode.Compiler
{
    using Bytecode;

    using Core;

    using Errors;

    using Parser;
    using Parser.Statement;

    public static class Constants
    {
        public static readonly string[] Keywords = new string[]
        {
            "struct",
            "class",
            "enum",
            "macro",
            "adaptive",

            "void",
            "namespace",
            "using",

            "byte",
            "int",
            "float",
            "char",

            "as",
        };

        public static readonly string[] BuiltinTypes = new string[]
        {
            "byte",
            "int",
            "float",
            "char",
        };

        public static readonly Dictionary<string, RuntimeType> BuiltinTypeMap1 = new()
        {
            { "byte", RuntimeType.BYTE },
            { "int", RuntimeType.INT },
            { "float", RuntimeType.FLOAT },
            { "char", RuntimeType.CHAR },
        };

        public static readonly Dictionary<string, Type> BuiltinTypeMap3 = new()
        {
            { "byte", Type.BYTE },
            { "int", Type.INT },
            { "float", Type.FLOAT },
            { "char", Type.CHAR },
            { "void", Type.VOID },
        };

        public static class Operators
        {
            public static readonly Dictionary<string, Opcode> OpCodes = new()
            {
                { "!", Opcode.LOGIC_NOT },
                { "+", Opcode.MATH_ADD },
                { "<", Opcode.LOGIC_LT },
                { ">", Opcode.LOGIC_MT },
                { "-", Opcode.MATH_SUB },
                { "*", Opcode.MATH_MULT },
                { "/", Opcode.MATH_DIV },
                { "%", Opcode.MATH_MOD },
                { "==", Opcode.LOGIC_EQ },
                { "!=", Opcode.LOGIC_NEQ },
                { "&&", Opcode.LOGIC_AND },
                { "||", Opcode.LOGIC_OR },
                { "^", Opcode.LOGIC_XOR },
                { "<=", Opcode.LOGIC_LTEQ },
                { ">=", Opcode.LOGIC_MTEQ },
                { "<<", Opcode.BITSHIFT_LEFT },
                { ">>", Opcode.BITSHIFT_RIGHT },
            };

            public static readonly Dictionary<string, int> ParameterCounts = new()
            {
                { "!", 1 },
                { "+", 2 },
                { "<", 2 },
                { ">", 2 },
                { "-", 2 },
                { "*", 2 },
                { "/", 2 },
                { "%", 2 },
                { "==", 2 },
                { "!=", 2 },
                { "&&", 2 },
                { "||", 2 },
                { "^", 2 },
                { "<=", 2 },
                { ">=", 2 },
                { "<<", 2 },
                { ">>", 2 },
            };
        }
    }

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
        protected readonly struct CompileableTemplate<T> where T : IDuplicateable<T>
        {
            internal readonly T OriginalFunction;
            internal readonly T Function;
            internal readonly Dictionary<string, CompiledType> TypeArguments;

            public CompileableTemplate(T function, Dictionary<string, CompiledType> typeArguments)
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
                    { throw new InternalException(); }
                }

                if (Function is CompiledFunction compiledFunction)
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
                else if (Function is CompiledGeneralFunction compiledGeneralFunction)
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
            }

            public override string ToString() => Function == null ? "null" : Function.ToString();
        }

        protected CompiledStruct[] CompiledStructs;
        protected CompiledClass[] CompiledClasses;
        protected CompiledFunction[] CompiledFunctions;
        protected CompiledOperator[] CompiledOperators;
        protected CompiledEnum[] CompiledEnums;
        protected CompiledGeneralFunction[] CompiledGeneralFunctions;

        protected IReadOnlyList<CompileableTemplate<CompiledFunction>> CompilableFunctions => compilableFunctions;
        protected IReadOnlyList<CompileableTemplate<CompiledOperator>> CompilableOperators => compilableOperators;
        protected IReadOnlyList<CompileableTemplate<CompiledGeneralFunction>> CompilableGeneralFunctions => compilableGeneralFunctions;

        readonly List<CompileableTemplate<CompiledFunction>> compilableFunctions = new();
        readonly List<CompileableTemplate<CompiledOperator>> compilableOperators = new();
        readonly List<CompileableTemplate<CompiledGeneralFunction>> compilableGeneralFunctions = new();

        protected readonly Dictionary<string, CompiledType> TypeArguments;

        protected List<Error> Errors;
        protected List<Warning> Warnings;

        protected string CurrentFile;

        protected CodeGeneratorBase()
        {
            CompiledStructs = Array.Empty<CompiledStruct>();
            CompiledClasses = Array.Empty<CompiledClass>();
            CompiledFunctions = Array.Empty<CompiledFunction>();
            CompiledOperators = Array.Empty<CompiledOperator>();
            CompiledGeneralFunctions = Array.Empty<CompiledGeneralFunction>();
            CompiledEnums = Array.Empty<CompiledEnum>();

            Errors = new List<Error>();
            Warnings = new List<Warning>();

            CurrentFile = null;

            TypeArguments = new Dictionary<string, CompiledType>();

            compilableFunctions = new List<CompileableTemplate<CompiledFunction>>();
            compilableOperators = new List<CompileableTemplate<CompiledOperator>>();
            compilableGeneralFunctions = new List<CompileableTemplate<CompiledGeneralFunction>>();
        }

        #region Helper Functions

        protected CompileableTemplate<CompiledFunction> AddCompilable(CompileableTemplate<CompiledFunction> compilable)
        {
            for (int i = 0; i < compilableFunctions.Count; i++)
            {
                if (compilableFunctions[i].Function.IsSame(compilable.Function))
                { return compilableFunctions[i]; }
            }
            compilableFunctions.Add(compilable);
            return compilable;
        }

        protected CompileableTemplate<CompiledOperator> AddCompilable(CompileableTemplate<CompiledOperator> compilable)
        {
            for (int i = 0; i < compilableOperators.Count; i++)
            {
                if (compilableOperators[i].Function.IsSame(compilable.Function))
                { return compilableOperators[i]; }
            }
            compilableOperators.Add(compilable);
            return compilable;
        }

        protected CompileableTemplate<CompiledGeneralFunction> AddCompilable(CompileableTemplate<CompiledGeneralFunction> compilable)
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
                if (@enum.Members[i].Value.type != runtimeType)
                { return false; }
            }

            return true;
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

        protected bool GetFunction(FunctionCall functionCallStatement, out CompiledFunction compiledFunction)
            => GetFunction(functionCallStatement.FunctionName, FindStatementTypes(functionCallStatement.MethodParameters), out compiledFunction);

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

            foreach (CompileableTemplate<CompiledFunction> function in compilableFunctions)
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

        protected bool GetFunctionTemplate(FunctionCall functionCallStatement, out CompileableTemplate<CompiledFunction> compiledFunction)
        {
            CompiledType[] parameters = FindStatementTypes(functionCallStatement.MethodParameters);

            bool found = false;
            compiledFunction = default;

            foreach (CompiledFunction element in CompiledFunctions)
            {
                if (element == null) continue;

                if (!element.IsTemplate) continue;

                if (element.Identifier != functionCallStatement.FunctionName) continue;

                if (!CompiledType.Equals(element.ParameterTypes, parameters, out Dictionary<string, CompiledType> typeParameters)) continue;

                if (element.Context != null && element.Context.TemplateInfo != null)
                { CollectTypeParameters(FindStatementType(functionCallStatement.PrevStatement), element.Context.TemplateInfo.TypeParameters, typeParameters); }

                compiledFunction = new CompileableTemplate<CompiledFunction>(element, typeParameters);

                if (found)
                { throw new CompilerException($"Duplicated function definitions: {found} and {element} are the same", element.Identifier, element.FilePath); }

                found = true;
            }

            return found;
        }

        protected bool GetConstructorTemplate(CompiledClass @class, ConstructorCall constructorCall, out CompileableTemplate<CompiledGeneralFunction> compiledGeneralFunction)
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

                if (!CompiledType.Equals(function.ParameterTypes, parameters, out var typeParameters)) continue;

                CollectTypeParameters(constructorCall.TypeName, @class.TemplateInfo.TypeParameters, typeParameters);

                compiledGeneralFunction = new CompileableTemplate<CompiledGeneralFunction>(function, typeParameters);

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
            CompiledClass @class = prevType.Class;

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (function.IsTemplate) continue;
                if (function.Context != @class) continue;
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

                if (function.Context != @class) continue;
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
            CompiledClass @class = prevType.Class;

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (function.IsTemplate) continue;
                if (function.Context != @class) continue;
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

                if (function.Context != @class) continue;
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

        protected bool GetIndexGetterTemplate(CompiledType prevType, out CompileableTemplate<CompiledFunction> compiledFunction)
        {
            if (!prevType.IsClass)
            {
                compiledFunction = default;
                return false;
            }
            CompiledClass @class = prevType.Class;

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (!function.IsTemplate) continue;
                if (function.Context != @class) continue;
                if (function.Identifier.Content != FunctionNames.IndexerGet) continue;

                if (function.ParameterTypes.Length != 2)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (function.ParameterTypes[1] != Type.INT)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should have 1 integer parameter", function.Identifier, function.FilePath); }

                if (!function.ReturnSomething)
                { throw new CompilerException($"Method \"{FunctionNames.IndexerGet}\" should return something", function.TypeToken, function.FilePath); }

                Dictionary<string, CompiledType> typeParameters = new();

                CollectTypeParameters(prevType, @class.TemplateInfo.TypeParameters, typeParameters);

                compiledFunction = new CompileableTemplate<CompiledFunction>(function, typeParameters);
                return true;
            }

            compiledFunction = default;
            return false;
        }

        protected bool GetIndexSetterTemplate(CompiledType prevType, CompiledType elementType, out CompileableTemplate<CompiledFunction> compiledFunction)
        {
            if (!prevType.IsClass)
            {
                compiledFunction = default;
                return false;
            }
            CompiledClass @class = prevType.Class;

            for (int i = 0; i < CompiledFunctions.Length; i++)
            {
                CompiledFunction function = CompiledFunctions[i];

                if (!function.IsTemplate) continue;
                if (function.Context != @class) continue;
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

                Dictionary<string, CompiledType> typeParameters = new();

                CollectTypeParameters(prevType, @class.TemplateInfo.TypeParameters, typeParameters);

                compiledFunction = new CompileableTemplate<CompiledFunction>(function, typeParameters);
                return true;
            }

            compiledFunction = default;
            return false;
        }

        bool GetFunction(string name, out CompiledFunction compiledFunction)
        {
            compiledFunction = null;

            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                CompiledFunction function = this.CompiledFunctions[i];

                if (function.Identifier != name) continue;

                if (compiledFunction != null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {compiledFunction.ReadableID()} (at {compiledFunction.Identifier.Position.ToMinString()}) ; {function.ReadableID()} (at {function.Identifier.Position.ToMinString()}) ; (and possibly more)", Position.UnknownPosition, CurrentFile); }

                compiledFunction = function;
            }

            return compiledFunction != null;
        }

        protected bool GetFunction(Identifier variable, out CompiledFunction compiledFunction)
            => GetFunction(variable.VariableName.Content, out compiledFunction);

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

        protected bool GetOperatorTemplate(OperatorCall @operator, out CompileableTemplate<CompiledOperator> compiledOperator)
        {
            CompiledType[] parameters = FindStatementTypes(@operator.Parameters);

            bool found = false;
            compiledOperator = default;

            foreach (CompiledOperator function in CompiledOperators)
            {
                if (!function.IsTemplate) continue;
                if (function.Identifier.Content != @operator.Operator.Content) continue;
                if (!CompiledType.Equals(function.ParameterTypes, parameters, out Dictionary<string, CompiledType> typeParameters)) continue;

                if (found)
                { throw new CompilerException($"Duplicated operator definitions: {compiledOperator} and {function} are the same", function.Identifier, function.FilePath); }

                compiledOperator = new CompileableTemplate<CompiledOperator>(function, typeParameters);

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

        protected bool GetGeneralFunctionTemplate(CompiledClass @class, string name, out CompileableTemplate<CompiledGeneralFunction> compiledGeneralFunction)
            => GetGeneralFunctionTemplate(@class, Array.Empty<CompiledType>(), name, out compiledGeneralFunction);
        protected bool GetGeneralFunctionTemplate(CompiledClass @class, CompiledType[] parameters, string name, out CompileableTemplate<CompiledGeneralFunction> compiledGeneralFunction)
        {
            bool found = false;
            compiledGeneralFunction = default;

            foreach (CompiledGeneralFunction function in CompiledGeneralFunctions)
            {
                if (!function.IsTemplate) continue;
                if (function.Identifier != name) continue;
                if (function.Type.Class != @class) continue;
                if (!CompiledType.Equals(function.ParameterTypes, parameters, out Dictionary<string, CompiledType> typeParameters)) continue;

                compiledGeneralFunction = new CompileableTemplate<CompiledGeneralFunction>(function, typeParameters);

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

        /// <exception cref="InternalException"></exception>
        protected CompiledType FindType(Token name) => FindType(name.Content, name);

        /// <exception cref="InternalException"></exception>
        protected CompiledType FindType(string name, IThingWithPosition position) => FindType(name, position.GetPosition());

        /// <exception cref="InternalException"></exception>
        protected CompiledType FindType(string name) => FindType(name, Position.UnknownPosition);

        /// <param name="position">
        /// Used for exceptions
        /// </param>
        /// <exception cref="InternalException"></exception>
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
        void CollectTypeParameters(TypeInstance type, Token[] typeParameterNames, Dictionary<string, CompiledType> typeParameters)
            => CollectTypeParameters(new CompiledType(type, FindType), typeParameterNames, typeParameters);

        /// <summary>
        /// Collects the type parameters from <paramref name="type"/> with names got from <paramref name="typeParameterNames"/> and puts the result to <paramref name="typeParameters"/>
        /// </summary>
        /// <exception cref="NotImplementedException"/>
        static void CollectTypeParameters(CompiledType type, Token[] typeParameterNames, Dictionary<string, CompiledType> typeParameters)
        {
            if (type.TypeParameters.Length != typeParameterNames.Length)
            { throw new NotImplementedException(); }

            for (int i = 0; i < typeParameterNames.Length; i++)
            { typeParameters[typeParameterNames[i].Content] = type.TypeParameters[i]; }
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
            { return new DataItem((int)Utils.NULL_POINTER); }

            if (type.IsEnum)
            {
                if (type.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{type.Enum.Identifier.Content}\" initial value: enum has no members", type.Enum.Identifier, type.Enum.FilePath); }

                return type.Enum.Members[0].Value;
            }

            return GetInitialValue(type.BuiltinType);
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
                { throw new CompilerException($"Wrong number of parameters passed to keyword-function \"clone\": requied {1}, passed {keywordCall.Parameters.Length}", keywordCall.TotalPosition(), CurrentFile); }

                return FindStatementType(keywordCall.Parameters[0]);
            }

            throw new CompilerException($"Unknown keyword-function \"{keywordCall.FunctionName}\"", keywordCall.Identifier, CurrentFile);
        }
        protected CompiledType FindStatementType(IndexCall index)
        {
            CompiledType prevType = FindStatementType(index.PrevStatement);

            if (!prevType.IsClass)
            { throw new CompilerException($"Index getter for type \"{prevType.Name}\" not found", index, CurrentFile); }

            if (!GetIndexGetter(prevType, out CompiledFunction indexer))
            {
                if (!GetIndexGetterTemplate(prevType, out CompileableTemplate<CompiledFunction> indexerTemplate))
                { throw new CompilerException($"Index getter for class \"{prevType.Class.Name}\" not found", index, CurrentFile); }

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

            if (!GetFunction(functionCall, out CompiledFunction compiledFunction))
            {
                if (!GetFunctionTemplate(functionCall, out var compiledFunctionTemplate))
                { throw new CompilerException($"Function \"{functionCall.ReadableID(FindStatementType)}\" not found", functionCall.Identifier, CurrentFile); }

                compiledFunction = compiledFunctionTemplate.Function;
            }

            return compiledFunction.Type;
        }
        protected CompiledType FindStatementType(OperatorCall @operator)
        {
            if (Constants.Operators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
            {
                if (Constants.Operators.ParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': requied {Constants.Operators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }
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

            if (!leftType.IsBuiltin || !rightType.IsBuiltin || leftType.BuiltinType == Type.VOID || rightType.BuiltinType == Type.VOID)
            { throw new CompilerException($"Unknown operator {leftType} {@operator.Operator.Content} {rightType}", @operator.Operator, CurrentFile); }

            DataItem leftValue = GetInitialValue(leftType);
            DataItem rightValue = GetInitialValue(rightType);

            DataItem? predictedValue = ComputeValue(@operator.Operator.Content, leftValue, rightValue);

            if (!predictedValue.HasValue)
            { throw new InternalException($"Failed to compute {leftType} {@operator.Operator.Content} {rightType}"); }

            return new CompiledType(predictedValue.Value.type);
        }
        protected CompiledType FindStatementType(BBCode.Parser.Statement.Literal literal) => literal.Type switch
        {
            LiteralType.INT => new CompiledType(Type.INT),
            LiteralType.FLOAT => new CompiledType(Type.FLOAT),
            LiteralType.STRING => FindReplacedType("string", literal),
            LiteralType.BOOLEAN => FindReplacedType("boolean", literal),
            LiteralType.CHAR => new CompiledType(Type.CHAR),
            _ => throw new NotImplementedException($"Unknown literal type {literal.Type}"),
        };
        protected CompiledType FindStatementType(Identifier variable)
        {
            if (variable.VariableName.Content == "nullptr")
            { return new CompiledType(Type.INT); }

            if (GetLocalSymbolType(variable.VariableName.Content, out CompiledType type))
            { return type; }

            if (GetEnum(variable.VariableName.Content, out var @enum))
            { return new CompiledType(@enum); }

            if (GetFunction(variable, out var function))
            { return new CompiledType(function); }

            try
            { return FindType(variable.VariableName); }
            catch (Exception)
            { }

            throw new CompilerException($"Local symbol/enum/function \"{variable.VariableName.Content}\" not found", variable.VariableName, CurrentFile);
        }
        protected static CompiledType FindStatementType(AddressGetter _) => new(Type.INT);
        protected static CompiledType FindStatementType(Pointer _) => new(Type.UNKNOWN);
        protected CompiledType FindStatementType(NewInstance newInstance) => new(newInstance.TypeName, FindType);
        protected CompiledType FindStatementType(ConstructorCall constructorCall) => new(constructorCall.TypeName, FindType);
        protected CompiledType FindStatementType(Field field)
        {
            CompiledType prevStatementType = FindStatementType(field.PrevStatement);

            if (prevStatementType.IsStruct)
            {
                for (int i = 0; i < prevStatementType.Struct.Fields.Length; i++)
                {
                    CompiledField definedField = prevStatementType.Struct.Fields[i];

                    if (definedField.Identifier.Content != field.FieldName.Content) continue;

                    if (definedField.Type.IsGeneric)
                    { throw new CompilerException($"Struct templates not supported :(", definedField, prevStatementType.Struct.FilePath); }

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

                    return definedField.Type;
                }

                throw new CompilerException($"Field definition \"{prevStatementType}\" not found in class \"{prevStatementType.Class.Name.Content}\"", field.FieldName, CurrentFile);
            }

            if (prevStatementType.IsEnum)
            {
                if (prevStatementType.Enum.Members.TryGetValue(field.FieldName.Content, out CompiledEnumMember enumMember))
                { return new CompiledType(enumMember.Value.type); }

                throw new CompilerException($"Enum member \"{prevStatementType}\" not found in enum \"{prevStatementType.Enum.Identifier.Content}\"", field.FieldName, CurrentFile);
            }

            throw new CompilerException($"Class/struct/enum definition \"{prevStatementType}\" not found", field.TotalPosition(), CurrentFile);
        }
        protected CompiledType FindStatementType(TypeCast @as) => new(@as.Type, FindType);

        protected CompiledType FindStatementType(StatementWithValue statement)
        {
            if (statement is FunctionCall functionCall)
            { return FindStatementType(functionCall); }

            if (statement is OperatorCall @operator)
            { return FindStatementType(@operator); }

            if (statement is BBCode.Parser.Statement.Literal literal)
            { return FindStatementType(literal); }

            if (statement is Identifier variable)
            { return FindStatementType(variable); }

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

            throw new CompilerException($"Statement {statement.GetType().Name} does not have a type", statement, CurrentFile);
        }

        protected CompiledType[] FindStatementTypes(StatementWithValue[] statements)
        {
            CompiledType[] result = new CompiledType[statements.Length];
            for (int i = 0; i < statements.Length; i++)
            { result[i] = FindStatementType(statements[i]); }
            return result;
        }

        #endregion

        #region ComputeValue()
        protected static DataItem? ComputeValue(string @operator, DataItem left, DataItem right)
            => @operator switch
            {
                "!" => !left,

                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => left,
                "%" => left,

                "&&" => new DataItem((!left.IsFalsy()) && (!right.IsFalsy()), null),
                "||" => new DataItem((!left.IsFalsy()) || (!right.IsFalsy()), null),

                "&" => left & right,
                "|" => left | right,
                "^" => left ^ right,

                "<<" => DataItem.BitshiftLeft(left, right),
                ">>" => DataItem.BitshiftRight(left, right),

                "<" => new DataItem(left < right, null),
                ">" => new DataItem(left > right, null),
                "==" => new DataItem(left == right, null),
                "!=" => new DataItem(left != right, null),
                "<=" => new DataItem(left <= right, null),
                ">=" => new DataItem(left >= right, null),
                _ => throw new NotImplementedException($"Unknown operator '{@operator}'"),
            };

        protected DataItem? ComputeValue(OperatorCall @operator)
        {
            if (GetOperator(@operator, out _))
            { return null; }

            var leftValue = ComputeValue(@operator.Left);
            if (!leftValue.HasValue) return null;

            if (@operator.Operator.Content == "!")
            {
                return !leftValue;
            }

            if (@operator.Right != null)
            {
                var rightValue = ComputeValue(@operator.Right);
                if (!rightValue.HasValue) return null;

                return ComputeValue(@operator.Operator.Content, leftValue.Value, rightValue.Value);
            }

            return leftValue;
        }
        protected static DataItem? ComputeValue(BBCode.Parser.Statement.Literal literal)
            => literal.Type switch
            {
                LiteralType.INT => new DataItem(int.Parse(literal.Value), null),
                LiteralType.FLOAT => new DataItem(float.Parse(literal.Value.EndsWith('f') ? literal.Value[..^1] : literal.Value), null),
                LiteralType.STRING => null,
                LiteralType.BOOLEAN => new DataItem(bool.Parse(literal.Value), null),
                _ => throw new NotImplementedException($"This should never occur"),
            };
        protected DataItem? ComputeValue(KeywordCall keywordCall)
        {
            if (keywordCall.FunctionName == "sizeof")
            {
                if (keywordCall.Parameters.Length != 1)
                { return null; }

                StatementWithValue param0 = keywordCall.Parameters[0];
                CompiledType param0Type = FindStatementType(param0);

                if (!param0Type.IsClass)
                { throw new CompilerException($"{param0Type} is not a reference type", param0, CurrentFile); }

                return new DataItem(param0Type.SizeOnHeap, $"sizeof({param0Type.Name})");
            }

            return null;
        }

        protected DataItem? ComputeValue(StatementWithValue st)
        {
            if (st is BBCode.Parser.Statement.Literal literal)
            { return ComputeValue(literal); }

            if (st is OperatorCall @operator)
            { return ComputeValue(@operator); }

            if (st is KeywordCall keywordCall)
            { return ComputeValue(keywordCall); }

            return null;
        }
        #endregion

    }
}
