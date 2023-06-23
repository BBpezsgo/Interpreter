using System;
using System.Collections.Generic;

namespace IngameCoding.BBCode.Compiler
{
    using IngameCoding.BBCode.Parser;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    public static class Constants
    {
        public static readonly string[] Keywords = new string[]
        {
            "struct",
            "class",
            "enum",

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

    public class CodeGeneratorBase
    {
        protected CompiledStruct[] CompiledStructs;
        protected CompiledClass[] CompiledClasses;
        protected CompiledFunction[] CompiledFunctions;
        protected CompiledOperator[] CompiledOperators;
        protected CompiledEnum[] CompiledEnums;
        protected CompiledGeneralFunction[] CompiledGeneralFunctions;
        protected List<KeyValuePair<string, CompiledVariable>> compiledVariables;
        protected List<CompiledParameter> parameters;

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

            compiledVariables = new List<KeyValuePair<string, CompiledVariable>>();
            parameters = new List<CompiledParameter>();

            Errors = new List<Error>();
            Warnings = new List<Warning>();

            CurrentFile = null;
        }

        #region Helper Functions

        #region Type Helpers

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

        /// <returns>
        /// <list type="bullet">
        /// <item><see cref="CompiledStruct"/></item>
        /// <item><see cref="CompiledClass"/></item>
        /// <item><see langword="null"/> if <paramref name="returnNull"/> is set to <see langword="true"/></item>
        /// </list>
        /// </returns>
        /// <param name="position">
        /// Used for exceptions
        /// </param>
        /// <exception cref="InternalException"></exception>
        protected ITypeDefinition GetCustomType(string name, Position position)
        {
            if (CompiledStructs.ContainsKey(name)) return CompiledStructs.Get<string, ITypeDefinition>(name);
            if (CompiledClasses.ContainsKey(name)) return CompiledClasses.Get<string, ITypeDefinition>(name);
            if (CompiledEnums.ContainsKey(name)) return CompiledEnums.Get<string, ITypeDefinition>(name);

            if (GetFunction(name, out CompiledFunction function))
            { return new FunctionType(function); }

            throw new CompilerException($"Type definition \"{name}\" not found", position, CurrentFile);
        }

        protected ITypeDefinition GetReplacedType(string builtinName)
        {
            string replacedName = TypeDefinitionReplacer(builtinName);

            if (replacedName == null)
            { throw new CompilerException($"Type replacer \"{builtinName}\" not found. Define a type with an attribute [Define(\"{builtinName}\")] to use it as a {builtinName}", Position.UnknownPosition, CurrentFile); }

            ITypeDefinition typeDefinition = GetCustomType(replacedName, Position.UnknownPosition);

            return typeDefinition;
        }

        /// <returns>
        /// <list type="bullet">
        /// <item><see cref="CompiledStruct"/></item>
        /// <item><see cref="CompiledClass"/></item>
        /// <item><see langword="null"/> if <paramref name="returnNull"/> is set to <see langword="true"/></item>
        /// </list>
        /// </returns>
        /// <exception cref="InternalException"></exception>
        protected ITypeDefinition GetCustomType(TypeInstance name)
            => GetCustomType(name.Identifier.Content, name.Position);

        /// <returns>
        /// <list type="bullet">
        /// <item><see cref="CompiledStruct"/></item>
        /// <item><see cref="CompiledClass"/></item>
        /// <item><see langword="null"/> if <paramref name="returnNull"/> is set to <see langword="true"/></item>
        /// </list>
        /// </returns>
        /// <exception cref="InternalException"></exception>
        protected ITypeDefinition GetCustomType(string name)
            => GetCustomType(name, Position.UnknownPosition);

        protected string TypeDefinitionReplacer(string typeName)
        {
            foreach (var @struct in CompiledStructs)
            {
                if (@struct.CompiledAttributes.TryGetAttribute("Define", out string definedType))
                {
                    if (definedType == typeName)
                    {
                        return @struct.Name.Content;
                    }
                }
            }
            foreach (var @class in CompiledClasses)
            {
                if (@class.CompiledAttributes.TryGetAttribute("Define", out string definedType))
                {
                    if (definedType == typeName)
                    {
                        return @class.Name.Content;
                    }
                }
            }
            foreach (var @enum in CompiledEnums)
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

        #endregion

        protected bool GetEnum(string name, out CompiledEnum @enum)
            => CompiledEnums.TryGetValue(name, out @enum);

        protected bool GetVariable(string variableName, out CompiledVariable compiledVariable)
            => compiledVariables.TryGetValue(variableName, out compiledVariable);

        protected bool GetParameter(string parameterName, out CompiledParameter parameter)
            => parameters.TryGetValue(parameterName, out parameter);

        protected bool GetFunction(Statement_FunctionCall functionCallStatement, out CompiledFunction compiledFunction)
        {
            CompiledType[] parameters = new CompiledType[functionCallStatement.MethodParameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            { parameters[i] = FindStatementType(functionCallStatement.MethodParameters[i]); }

            return CompiledFunctions.GetDefinition((functionCallStatement.FunctionName, parameters), out compiledFunction);
        }

        bool GetFunction(string name, out CompiledFunction compiledFunction)
        {
            compiledFunction = null;

            CompiledFunction found = null;
            for (int i = 0; i < this.CompiledFunctions.Length; i++)
            {
                var f = this.CompiledFunctions[i];
                if (f.Identifier.Content != name) continue;

                if (found != null)
                { throw new CompilerException($"Function type could not be inferred. Definition conflicts: {found.ReadableID()} (at {found.Identifier.Position.ToMinString()}) ; {f.ReadableID()} (at {f.Identifier.Position.ToMinString()}) ; (and possibly more)", Position.UnknownPosition, CurrentFile); }

                found = f;
            }

            if (found != null)
            {
                compiledFunction = found;
                return true;
            }

            return false;
        }

        protected bool GetFunction(Statement_Variable variable, out CompiledFunction compiledFunction)
            => GetFunction(variable.VariableName.Content, out compiledFunction);

        protected bool GetOperator(Statement_Operator @operator, out CompiledOperator operatorDefinition)
        {
            StatementWithReturnValue[] parameters = @operator.Parameters;
            CompiledType[] parameterTypes = new CompiledType[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = FindStatementType(parameters[i]);
            }

            return CompiledOperators.GetDefinition((@operator.Operator.Content, parameterTypes), out operatorDefinition);
        }

        protected bool GetConstructor(Statement_ConstructorCall functionCallStatement, out CompiledGeneralFunction constructor)
        {
            if (!GetClass(functionCallStatement, out var @class))
            { throw new CompilerException($"Class definition \"{functionCallStatement.TypeName}\" not found", functionCallStatement, CurrentFile); }

            for (int i = 0; i < CompiledGeneralFunctions.Length; i++)
            {
                var function = CompiledGeneralFunctions[i];
                if (function.Identifier.Content != "constructor") continue;
                if (function.Type.Class != @class) continue;
                if (function.ParameterCount != functionCallStatement.Parameters.Length) continue;

                bool not = false;
                for (int j = 0; j < function.ParameterTypes.Length; j++)
                {
                    if (FindStatementType(functionCallStatement.Parameters[j]) != function.ParameterTypes[j])
                    {
                        not = true;
                        break;
                    }
                }
                if (not) continue;

                constructor = function;
                return true;
            }

            constructor = null;
            return false;
        }
        protected bool GetCloner(CompiledClass @class, out CompiledGeneralFunction cloner)
        {
            for (int i = 0; i < CompiledGeneralFunctions.Length; i++)
            {
                var function = CompiledGeneralFunctions[i];
                if (function.Identifier.Content != "clone") continue;
                if (function.Type.Class != @class) continue;

                cloner = function;
                return true;
            }

            cloner = null;
            return false;
        }
        protected bool GetDestructor(CompiledClass @class, out CompiledGeneralFunction destructor)
        {
            for (int i = 0; i < CompiledGeneralFunctions.Length; i++)
            {
                var function = CompiledGeneralFunctions[i];
                if (function.Identifier.Content != "destructor") continue;
                if (function.Type.Class != @class) continue;

                destructor = function;
                return true;
            }

            destructor = null;
            return false;
        }

        /// <exception cref="ArgumentNullException"></exception>
        protected bool GetStruct(Statement_NewInstance newStructStatement, out CompiledStruct compiledStruct)
            => CompiledStructs.TryGetValue(newStructStatement.TypeName.Content, out compiledStruct);

        /// <exception cref="ArgumentNullException"></exception>
        protected bool GetClass(Statement_NewInstance newClassStatement, out CompiledClass compiledClass)
            => CompiledClasses.TryGetValue(newClassStatement.TypeName.Content, out compiledClass);

        /// <exception cref="ArgumentNullException"></exception>
        protected bool GetClass(Statement_ConstructorCall constructorCall, out CompiledClass compiledClass)
            => CompiledClasses.TryGetValue(constructorCall.TypeName.Content, out compiledClass);

        /// <exception cref="ArgumentNullException"></exception>
        protected bool GetClass(string className, out CompiledClass compiledClass)
            => CompiledClasses.TryGetValue(className, out compiledClass);

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem GetInitialValue(Type type)
        {
            return type switch
            {
                Type.BYTE => new DataItem((byte)0),
                Type.INT => new DataItem((int)0),
                Type.FLOAT => new DataItem((float)0f),
                Type.CHAR => new DataItem((char)'\0'),

                _ => throw new InternalException($"Initial value for type \"{type}\" is unimplemented"),
            };
        }

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem GetInitialValue(TypeInstance type)
        {
            return type.Identifier.Content switch
            {
                "int" => new DataItem((int)0),
                "byte" => new DataItem((byte)0),
                "float" => new DataItem((float)0f),
                "char" => new DataItem((char)'\0'),

                "var" => throw new CompilerException("Undefined type", type.Identifier),
                "void" => throw new CompilerException("Invalid type", type.Identifier),
                _ => throw new InternalException($"Initial value for type \"{type.Identifier.Content}\" is unimplemented"),
            };
        }

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem GetInitialValue(CompiledType type)
        {
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
        protected CompiledType FindStatementType(Statement_KeywordCall keywordCall)
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
        protected CompiledType FindStatementType(Statement_FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "Dealloc") return new CompiledType(Type.VOID);

            if (functionCall.FunctionName == "Alloc") return new CompiledType(Type.INT);

            if (functionCall.FunctionName == "sizeof") return new CompiledType(Type.INT);

            if (!GetFunction(functionCall, out var calledFunc))
            { throw new CompilerException($"Function \"{functionCall.ReadableID(FindStatementType)}\" not found", functionCall.Identifier, CurrentFile); }
            return calledFunc.Type;
        }
        protected CompiledType FindStatementType(Statement_Operator @operator)
        {
            if (Constants.Operators.OpCodes.TryGetValue(@operator.Operator.Content, out Opcode opcode))
            {
                if (Constants.Operators.ParameterCounts[@operator.Operator.Content] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of parameters passed to operator '{@operator.Operator.Content}': requied {Constants.Operators.ParameterCounts[@operator.Operator.Content]} passed {@operator.ParameterCount}", @operator.Operator, CurrentFile); }
            }
            else
            {
                opcode = Opcode.UNKNOWN;
            }

            if (opcode != Opcode.UNKNOWN)
            {
                if (GetOperator(@operator, out CompiledOperator operatorDefinition))
                { return operatorDefinition.Type; }

                var leftType = FindStatementType(@operator.Left);
                if (@operator.Right != null)
                {
                    var rightType = FindStatementType(@operator.Right);

                    if (!leftType.IsBuiltin || !rightType.IsBuiltin || leftType.BuiltinType == Type.VOID || rightType.BuiltinType == Type.VOID)
                    { throw new CompilerException($"Unknown operator {leftType} {@operator.Operator.Content} {rightType}", @operator.Operator, CurrentFile); }

                    var leftValue = GetInitialValue(leftType);
                    var rightValue = GetInitialValue(rightType);

                    var predictedValue = PredictStatementValue(@operator.Operator.Content, leftValue, rightValue);
                    if (!predictedValue.HasValue)
                    { throw new InternalException($"Failed to evaluate the operator {leftType} {@operator.Operator.Content} {rightType}"); }

                    return predictedValue.Value.type switch
                    {
                        RuntimeType.BYTE => new CompiledType(Type.BYTE),
                        RuntimeType.INT => new CompiledType(Type.INT),
                        RuntimeType.FLOAT => new CompiledType(Type.FLOAT),
                        RuntimeType.CHAR => new CompiledType(Type.CHAR),
                        _ => throw new NotImplementedException($"Unknown type {predictedValue.Value.type}"),
                    };
                }
                else
                {
                    return leftType;
                }
            }
            else if (@operator.Operator.Content == "=")
            { throw new NotImplementedException(); }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }
        }
        protected CompiledType FindStatementType(Statement_Literal literal) => literal.Type switch
        {
            LiteralType.INT => new CompiledType(Type.INT),
            LiteralType.FLOAT => new CompiledType(Type.FLOAT),
            LiteralType.STRING => new CompiledType(GetReplacedType("string")),
            LiteralType.BOOLEAN => new CompiledType(GetReplacedType("boolean")),
            LiteralType.CHAR => new CompiledType(Type.CHAR),
            _ => throw new NotImplementedException($"Unknown literal type {literal.Type}"),
        };
        protected CompiledType FindStatementType(Statement_Variable variable)
        {
            if (variable.VariableName.Content == "nullptr")
            { return new CompiledType(Type.INT); }

            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            { return param.Type; }

            if (GetVariable(variable.VariableName.Content, out CompiledVariable val))
            { return val.Type; }

            if (GetEnum(variable.VariableName.Content, out var @enum))
            { return new CompiledType(@enum); }

            if (GetFunction(variable, out var function))
            { return new CompiledType(function); }

            throw new CompilerException($"Variable/parameter/enum/function \"{variable.VariableName.Content}\" not found", variable.VariableName, CurrentFile);
        }
        protected CompiledType FindStatementType(Statement_MemoryAddressGetter _) => new(Type.INT);
        protected CompiledType FindStatementType(Statement_MemoryAddressFinder _) => new(Type.UNKNOWN);
        protected CompiledType FindStatementType(Statement_NewInstance newStruct)
        {
            if (GetStruct(newStruct, out var structDefinition))
            { return new CompiledType(structDefinition); }

            if (GetClass(newStruct, out var classDefinition))
            { return new CompiledType(classDefinition); }

            throw new CompilerException($"Class/struct definition \"{newStruct.TypeName.Content}\" not found", newStruct.TypeName, CurrentFile);
        }
        protected CompiledType FindStatementType(Statement_ConstructorCall constructorCall)
        {
            if (GetClass(constructorCall, out var classDefinition))
            { return new CompiledType(classDefinition); }

            throw new CompilerException($"Class definition \"{constructorCall.TypeName.Content}\" not found", constructorCall.TypeName, CurrentFile);
        }
        protected CompiledType FindStatementType(Statement_Field field)
        {
            var prevStatementType = FindStatementType(field.PrevStatement);

            foreach (var @struct in CompiledStructs)
            {
                if (@struct.Key != prevStatementType.Name) continue;

                foreach (var sField in @struct.Fields)
                {
                    if (sField.Identifier.Content != field.FieldName.Content) continue;
                    return sField.Type;
                }

                throw new CompilerException($"Field definition \"{prevStatementType}\" not found in struct \"{@struct.Name.Content}\"", field.FieldName, CurrentFile);
            }

            foreach (var @class in CompiledClasses)
            {
                if (@class.Key != prevStatementType.Name) continue;

                foreach (var sField in @class.Fields)
                {
                    if (sField.Identifier.Content != field.FieldName.Content) continue;
                    return sField.Type;
                }

                throw new CompilerException($"Field definition \"{prevStatementType}\" not found in class \"{@class.Name.Content}\"", field.FieldName, CurrentFile);
            }

            foreach (var @enum in CompiledEnums)
            {
                if (@enum.Key != prevStatementType.Name) continue;

                if (@enum.Members.TryGetValue(field.FieldName.Content, out CompiledEnumMember enumMember))
                {
                    return new CompiledType(enumMember.Value.type);
                }

                throw new CompilerException($"Enum member \"{prevStatementType}\" not found in enum \"{@enum.Identifier.Content}\"", field.FieldName, CurrentFile);
            }

            throw new CompilerException($"Class/struct/enum definition \"{prevStatementType}\" not found", field.TotalPosition(), CurrentFile);
        }
        protected CompiledType FindStatementType(Statement_As @as)
        { return new CompiledType(@as.Type, GetCustomType); }

        protected CompiledType FindStatementType(StatementWithReturnValue statement)
        {
            if (statement is Statement_FunctionCall functionCall)
            { return FindStatementType(functionCall); }

            if (statement is Statement_Operator @operator)
            { return FindStatementType(@operator); }

            if (statement is Statement_Literal literal)
            { return FindStatementType(literal); }

            if (statement is Statement_Variable variable)
            { return FindStatementType(variable); }

            if (statement is Statement_MemoryAddressGetter memoryAddressGetter)
            { return FindStatementType(memoryAddressGetter); }

            if (statement is Statement_MemoryAddressFinder memoryAddressFinder)
            { return FindStatementType(memoryAddressFinder); }

            if (statement is Statement_NewInstance newStruct)
            { return FindStatementType(newStruct); }

            if (statement is Statement_ConstructorCall constructorCall)
            { return FindStatementType(constructorCall); }

            if (statement is Statement_Field field)
            { return FindStatementType(field); }

            if (statement is Statement_As @as)
            { return FindStatementType(@as); }

            if (statement is Statement_KeywordCall keywordCall)
            { return FindStatementType(keywordCall); }

            throw new CompilerException($"Statement {statement.GetType().Name} does not have a type", statement, CurrentFile);
        }
        #endregion

        #region PredictStatementValue()
        protected DataItem? PredictStatementValue(string @operator, DataItem left, DataItem right)
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
        protected DataItem? PredictStatementValue(Statement_Operator @operator)
        {
            if (GetOperator(@operator, out _))
            { return null; }

            var leftValue = PredictStatementValue(@operator.Left);
            if (!leftValue.HasValue) return null;

            if (@operator.Operator.Content == "!")
            {
                return !leftValue;
            }

            if (@operator.Right != null)
            {
                var rightValue = PredictStatementValue(@operator.Right);
                if (!rightValue.HasValue) return null;

                return PredictStatementValue(@operator.Operator.Content, leftValue.Value, rightValue.Value);
            }

            return leftValue;
        }
        protected DataItem? PredictStatementValue(Statement_Literal literal)
            => literal.Type switch
            {
                LiteralType.INT => new DataItem(int.Parse(literal.Value), null),
                LiteralType.FLOAT => new DataItem(float.Parse(literal.Value.EndsWith('f') ? literal.Value[..^1] : literal.Value), null),
                LiteralType.STRING => null,
                LiteralType.BOOLEAN => new DataItem(bool.Parse(literal.Value), null),
                _ => throw new NotImplementedException($"This should never occur"),
            };
        protected DataItem? PredictStatementValue(StatementWithReturnValue st)
        {
            if (st is Statement_Literal literal)
            { return PredictStatementValue(literal); }

            if (st is Statement_Operator @operator)
            { return PredictStatementValue(@operator); }

            return null;
        }
        #endregion

    }
}
