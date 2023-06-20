using System;
using System.Collections.Generic;

namespace IngameCoding.BBCode.Compiler
{
    using IngameCoding.BBCode.Parser;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    public class CodeGeneratorBase
    {
        public static readonly string[] BuiltinFunctions = new string[]
        {
            "return",
            "break",
            "type",
            "delete",
        };
        public static readonly string[] Keywords = new string[]
        {
            "struct",
            "class",

            "void",
            "namespace",
            "using",

            "byte",
            "int",
            "float",
            "char",
            "bool",

            "as",
        };

        public static readonly string[] BuiltinTypes = new string[]
        {
            "byte",
            "int",
            "float",
            "char",
            "bool",
        };

        public static readonly Dictionary<string, BuiltinType> BuiltinTypeMap1 = new()
        {
            { "byte", BuiltinType.BYTE },
            { "int", BuiltinType.INT },
            { "float", BuiltinType.FLOAT },
            { "char", BuiltinType.CHAR },
            { "bool", BuiltinType.BOOLEAN }
        };

        internal static readonly Dictionary<string, CompiledType.CompiledTypeType> BuiltinTypeMap3 = new()
        {
            { "byte", CompiledType.CompiledTypeType.BYTE },
            { "int", CompiledType.CompiledTypeType.INT },
            { "float", CompiledType.CompiledTypeType.FLOAT },
            { "char", CompiledType.CompiledTypeType.CHAR },
            { "bool", CompiledType.CompiledTypeType.BOOL }
        };

        protected CompiledStruct[] CompiledStructs;
        protected CompiledClass[] CompiledClasses;
        protected CompiledFunction[] CompiledFunctions;
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
            CompiledGeneralFunctions = Array.Empty<CompiledGeneralFunction>();
            CompiledEnums = Array.Empty<CompiledEnum>();

            compiledVariables = new List<KeyValuePair<string, CompiledVariable>>();
            parameters = new List<CompiledParameter>();

            Errors = new List<Error>();
            Warnings = new List<Warning>();

            CurrentFile = null;
        }

        #region Helper Functions

        /// <returns>
        /// <list type="bullet">
        /// <item><see cref="CompiledStruct"/></item>
        /// <item><see cref="CompiledClass"/></item>
        /// <item><see langword="null"/> if <paramref name="returnNull"/> is set to <see langword="true"/></item>
        /// </list>
        /// </returns>
        /// <exception cref="InternalException"></exception>
        protected ITypeDefinition GetCustomType(string name, bool returnNull = false)
        {
            if (CompiledStructs.ContainsKey(name)) return CompiledStructs.Get<string, ITypeDefinition>(name);
            if (CompiledClasses.ContainsKey(name)) return CompiledClasses.Get<string, ITypeDefinition>(name);

            if (returnNull) return null;

            throw new CompilerException($"Type \"{name}\" not found", Position.UnknownPosition, CurrentFile);
        }

        protected ITypeDefinition GetReplacedType(string builtinName)
        {
            string replacedName = TypeDefinitionReplacer(builtinName);

            if (replacedName == null)
            { throw new CompilerException($"Type replacer \"{builtinName}\" not found. Define a type with the attribute [Define(\"{builtinName}\")] to use it as a {builtinName}", Position.UnknownPosition, CurrentFile); }

            ITypeDefinition typeDefinition = GetCustomType(replacedName, false);

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
        protected ITypeDefinition GetCustomType(TypeInstance name, bool returnNull = false)
            => GetCustomType(name.Identifier.Content, returnNull);

        /// <returns>
        /// <list type="bullet">
        /// <item><see cref="CompiledStruct"/></item>
        /// <item><see cref="CompiledClass"/></item>
        /// <item><see langword="null"/> if <paramref name="returnNull"/> is set to <see langword="true"/></item>
        /// </list>
        /// </returns>
        /// <exception cref="InternalException"></exception>
        protected ITypeDefinition GetCustomType(string name)
            => GetCustomType(name, false);

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
            return null;
        }

        protected string GetReadableID(Statement_FunctionCall functionCall)
        {
            string readableID = functionCall.FunctionName;
            readableID += "(";
            bool addComma = false;
            if (functionCall.IsMethodCall && functionCall.PrevStatement != null)
            {
                readableID += "this " + FindStatementType(functionCall.PrevStatement);
                addComma = true;
            }
            for (int i = 0; i < functionCall.Parameters.Length; i++)
            {
                if (addComma) { readableID += ", "; }
                readableID += FindStatementType(functionCall.Parameters[i]);
                addComma = true;
            }
            readableID += ")";
            return readableID;
        }

        protected bool GetEnum(string name, out CompiledEnum @enum)
            => CompiledEnums.TryGetValue(name, out @enum);

        protected bool GetCompiledVariable(string variableName, out CompiledVariable compiledVariable)
            => compiledVariables.TryGetValue(variableName, out compiledVariable);

        protected bool GetParameter(string parameterName, out CompiledParameter parameter)
            => parameters.TryGetValue(parameterName, out parameter);

        protected bool GetCompiledFunction(Statement_FunctionCall functionCallStatement, out CompiledFunction compiledFunction)
        {
            CompiledType[] parameters = new CompiledType[functionCallStatement.MethodParameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            { parameters[i] = FindStatementType(functionCallStatement.MethodParameters[i]); }

            return CompiledFunctions.GetDefinition((functionCallStatement.FunctionName, parameters), out compiledFunction);
        }

        protected bool GetConstructor(Statement_ConstructorCall functionCallStatement, out CompiledGeneralFunction constructor)
        {
            if (!GetCompiledClass(functionCallStatement, out var @class))
            { throw new NotImplementedException(); }

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
        protected bool GetCompiledStruct(Statement_NewInstance newStructStatement, out CompiledStruct compiledStruct)
            => CompiledStructs.TryGetValue(newStructStatement.TypeName.Content, out compiledStruct);

        /// <exception cref="ArgumentNullException"></exception>
        protected bool GetCompiledClass(Statement_NewInstance newClassStatement, out CompiledClass compiledClass)
            => CompiledClasses.TryGetValue(newClassStatement.TypeName.Content, out compiledClass);

        /// <exception cref="ArgumentNullException"></exception>
        protected bool GetCompiledClass(Statement_ConstructorCall constructorCall, out CompiledClass compiledClass)
            => CompiledClasses.TryGetValue(constructorCall.TypeName.Content, out compiledClass);

        /// <exception cref="ArgumentNullException"></exception>
        protected bool GetCompiledClass(string className, out CompiledClass compiledClass)
            => CompiledClasses.TryGetValue(className, out compiledClass);

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
                "bool" => new DataItem((bool)false),
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
            { throw new NotImplementedException(); }

            if (type.IsClass)
            { return new DataItem((int)Utils.NULL_POINTER); }

            return type.BuiltinType switch
            {
                CompiledType.CompiledTypeType.BYTE => new DataItem((byte)0),
                CompiledType.CompiledTypeType.INT => new DataItem((int)0),
                CompiledType.CompiledTypeType.FLOAT => new DataItem((float)0f),
                CompiledType.CompiledTypeType.CHAR => new DataItem((char)'\0'),
                CompiledType.CompiledTypeType.BOOL => new DataItem((bool)false),

                _ => throw new InternalException($"Initial value for type \"{type.Name}\" is unimplemented"),
            };
        }

        #endregion

        #region FindStatementType()
        protected CompiledType FindStatementType(Statement_FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "Dealloc") return new CompiledType(CompiledType.CompiledTypeType.VOID);

            if (functionCall.FunctionName == "Alloc") return new CompiledType(CompiledType.CompiledTypeType.INT);

            if (functionCall.FunctionName == "sizeof") return new CompiledType(CompiledType.CompiledTypeType.INT);

            if (!GetCompiledFunction(functionCall, out var calledFunc))
            { throw new CompilerException($"Function \"{GetReadableID(functionCall)}\" not found!", functionCall.Identifier, CurrentFile); }
            return calledFunc.Type;
        }
        protected CompiledType FindStatementType(Statement_Operator @operator)
        {
            Opcode opcode = Opcode.UNKNOWN;

            if (@operator.Operator.Content == "!")
            {
                if (@operator.ParameterCount != 1) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_NOT;
            }
            else if (@operator.Operator.Content == "+")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_ADD;
            }
            else if (@operator.Operator.Content == "<")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_LT;
            }
            else if (@operator.Operator.Content == ">")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_MT;
            }
            else if (@operator.Operator.Content == "-")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_SUB;
            }
            else if (@operator.Operator.Content == "*")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_MULT;
            }
            else if (@operator.Operator.Content == "/")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_DIV;
            }
            else if (@operator.Operator.Content == "%")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_MOD;
            }
            else if (@operator.Operator.Content == "==")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_EQ;
            }
            else if (@operator.Operator.Content == "!=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_NEQ;
            }
            else if (@operator.Operator.Content == "&&")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_AND;
            }
            else if (@operator.Operator.Content == "||")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_OR;
            }
            else if (@operator.Operator.Content == "^")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_XOR;
            }
            else if (@operator.Operator.Content == "<=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_LTEQ;
            }
            else if (@operator.Operator.Content == ">=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_MTEQ;
            }
            else if (@operator.Operator.Content == "<<")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.BITSHIFT_LEFT;
            }
            else if (@operator.Operator.Content == ">>")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator \"" + @operator.Operator + "\"", @operator.Operator, CurrentFile);
                opcode = Opcode.BITSHIFT_RIGHT;
            }

            if (opcode != Opcode.UNKNOWN)
            {
                var leftType = FindStatementType(@operator.Left);
                if (@operator.Right != null)
                {
                    var rightType = FindStatementType(@operator.Right);

                    if (leftType.IsBuiltin && rightType.IsBuiltin && leftType.BuiltinType != CompiledType.CompiledTypeType.VOID && rightType.BuiltinType != CompiledType.CompiledTypeType.VOID)
                    {
                        var leftValue = GetInitialValue(leftType);
                        var rightValue = GetInitialValue(rightType);

                        var predictedValue = PredictStatementValue(@operator.Operator.Content, leftValue, rightValue);
                        if (predictedValue.HasValue)
                        {
                            switch (predictedValue.Value.type)
                            {
                                case RuntimeType.BYTE: return new CompiledType(CompiledType.CompiledTypeType.BYTE);
                                case RuntimeType.INT: return new CompiledType(CompiledType.CompiledTypeType.INT);
                                case RuntimeType.FLOAT: return new CompiledType(CompiledType.CompiledTypeType.FLOAT);
                                case RuntimeType.CHAR: return new CompiledType(CompiledType.CompiledTypeType.CHAR);
                                case RuntimeType.BOOLEAN: return new CompiledType(CompiledType.CompiledTypeType.BOOL);
                            }
                        }
                    }

                    Warnings.Add(new Warning("Thats not good :(", @operator.TotalPosition(), CurrentFile));
                    return null;
                }
                else
                { return leftType; }
            }
            else if (@operator.Operator.Content == "=")
            {
                throw new NotImplementedException();
            }
            else
            { throw new CompilerException($"Unknown operator \"{@operator.Operator.Content}\"", @operator.Operator, CurrentFile); }
        }
        protected CompiledType FindStatementType(Statement_Literal literal) => literal.Type switch
        {
            LiteralType.INT => new CompiledType(CompiledType.CompiledTypeType.INT),
            LiteralType.FLOAT => new CompiledType(CompiledType.CompiledTypeType.FLOAT),
            LiteralType.STRING => new CompiledType(GetReplacedType("string")),
            LiteralType.BOOLEAN => new CompiledType(CompiledType.CompiledTypeType.BOOL),
            LiteralType.CHAR => new CompiledType(CompiledType.CompiledTypeType.CHAR),
            _ => throw new CompilerException($"Unknown literal type {literal.Type}", literal, CurrentFile),
        };
        protected CompiledType FindStatementType(Statement_Variable variable)
        {
            if (variable.VariableName.Content == "nullptr")
            { return new CompiledType(CompiledType.CompiledTypeType.INT); }

            if (GetParameter(variable.VariableName.Content, out CompiledParameter param))
            {
                if (variable.ListIndex != null)
                { throw new NotImplementedException(); }
                return param.Type;
            }
            else if (GetCompiledVariable(variable.VariableName.Content, out CompiledVariable val))
            {
                return val.Type;
            }
            else if (GetEnum(variable.VariableName.Content, out var @enum))
            {
                return new CompiledType(@enum);
            }
            else
            {
                throw new CompilerException($"Variable \"{variable.VariableName.Content}\" not found", variable.VariableName, CurrentFile);
            }
        }
        protected CompiledType FindStatementType(Statement_MemoryAddressGetter _) => new(CompiledType.CompiledTypeType.INT);
        protected CompiledType FindStatementType(Statement_MemoryAddressFinder _) => new(CompiledType.CompiledTypeType.UNKNOWN);
        protected CompiledType FindStatementType(Statement_NewInstance newStruct)
        {
            if (GetCompiledStruct(newStruct, out var structDefinition))
            { return new CompiledType(structDefinition); }
            else if (GetCompiledClass(newStruct, out var classDefinition))
            { return new CompiledType(classDefinition); }

            throw new CompilerException($"Class or struct definition \"{newStruct.TypeName.Content}\" not found", newStruct.TypeName, CurrentFile);
        }
        protected CompiledType FindStatementType(Statement_ConstructorCall constructorCall)
        {
            if (GetCompiledClass(constructorCall, out var classDefinition))
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

            throw new CompilerException($"Class or struct definition \"{prevStatementType}\" not found", field.TotalPosition(), CurrentFile);
        }
        protected CompiledType FindStatementType(Statement_As @as)
        { return new CompiledType(@as.Type, GetCustomType); }

        protected CompiledType FindStatementType(StatementWithReturnValue st)
        {
            try
            {
                if (st is Statement_FunctionCall functionCall)
                { return FindStatementType(functionCall); }
                else if (st is Statement_Operator @operator)
                { return FindStatementType(@operator); }
                else if (st is Statement_Literal literal)
                { return FindStatementType(literal); }
                else if (st is Statement_Variable variable)
                { return FindStatementType(variable); }
                else if (st is Statement_MemoryAddressGetter memoryAddressGetter)
                { return FindStatementType(memoryAddressGetter); }
                else if (st is Statement_MemoryAddressFinder memoryAddressFinder)
                { return FindStatementType(memoryAddressFinder); }
                else if (st is Statement_NewInstance newStruct)
                { return FindStatementType(newStruct); }
                else if (st is Statement_ConstructorCall constructorCall)
                { return FindStatementType(constructorCall); }
                else if (st is Statement_Field field)
                { return FindStatementType(field); }
                else if (st is Statement_As @as)
                { return FindStatementType(@as); }
                else if (st is Statement_ListValue list)
                { throw new NotImplementedException(); }
                else if (st is Statement_Index index)
                { throw new NotImplementedException(); }
                throw new CompilerException($"Statement without value type: {st.GetType().Name} {st}", st, CurrentFile);
            }
            catch (InternalException error)
            {
                Errors.Add(new Error(error.Message, st.TotalPosition()));
                throw;
            }
        }
        #endregion

        #region PredictStatementValue()
        protected static DataItem? PredictStatementValue(string @operator, DataItem left, DataItem right)
        {
            return @operator switch
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
                _ => null,
            };
        }
        protected static DataItem? PredictStatementValue(Statement_Operator @operator)
        {
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
            else
            { return leftValue; }
        }
        protected static DataItem? PredictStatementValue(Statement_Literal literal)
        {
            return literal.Type switch
            {
                LiteralType.INT => new DataItem(int.Parse(literal.Value), null),
                LiteralType.FLOAT => new DataItem(float.Parse(literal.Value.EndsWith('f') ? literal.Value[..^1] : literal.Value), null),
                LiteralType.STRING => new DataItem(literal.Value, null),
                LiteralType.BOOLEAN => new DataItem(bool.Parse(literal.Value), null),
                _ => throw new NotImplementedException(),
            };
        }
        protected static DataItem? PredictStatementValue(StatementWithReturnValue st)
        {
            if (st is Statement_Literal literal)
            { return PredictStatementValue(literal); }
            else if (st is Statement_Operator @operator)
            { return PredictStatementValue(@operator); }

            return null;
        }
        #endregion

    }
}
