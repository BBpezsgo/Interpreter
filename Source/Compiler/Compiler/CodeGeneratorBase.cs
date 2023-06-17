using System;
using System.Collections.Generic;

namespace IngameCoding.BBCode.Compiler
{
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Bytecode;
    using IngameCoding.Errors;

    public class CodeGeneratorBase
    {
        protected static readonly string[] BuiltinFunctions = new string[]
        {
            "return",
            "break",
            "type",
            "delete",
        };
        protected static readonly string[] Keywords = new string[]
        {
            "struct",
            "class",

            "void",
            "namespace",
            "using",

            "byte",
            "int",
            "bool",
            "float",
            "string"
        };

        protected CompiledStruct[] CompiledStructs;
        protected CompiledClass[] CompiledClasses;
        protected CompiledFunction[] CompiledFunctions;
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

            throw new InternalException($"Unknown type '{name}'");
        }

        /// <returns>
        /// <list type="bullet">
        /// <item><see cref="CompiledStruct"/></item>
        /// <item><see cref="CompiledClass"/></item>
        /// <item><see langword="null"/> if <paramref name="returnNull"/> is set to <see langword="true"/></item>
        /// </list>
        /// </returns>
        /// <exception cref="InternalException"></exception>
        protected ITypeDefinition GetCustomType(TypeToken name, bool returnNull = false)
        {
            if (CompiledStructs.ContainsKey(name.Content)) return CompiledStructs.Get<string, ITypeDefinition>(name.Content);
            if (CompiledClasses.ContainsKey(name.Content)) return CompiledClasses.Get<string, ITypeDefinition>(name.Content);

            if (returnNull) return null;

            throw new InternalException($"Unknown type '{name}'");
        }

        /// <returns>
        /// <list type="bullet">
        /// <item><see cref="CompiledStruct"/></item>
        /// <item><see cref="CompiledClass"/></item>
        /// <item><see langword="null"/> if <paramref name="returnNull"/> is set to <see langword="true"/></item>
        /// </list>
        /// </returns>
        /// <exception cref="InternalException"></exception>
        protected ITypeDefinition GetCustomType(string name)
        {
            if (CompiledStructs.ContainsKey(name)) return CompiledStructs.Get<string, ITypeDefinition>(name);
            if (CompiledClasses.ContainsKey(name)) return CompiledClasses.Get<string, ITypeDefinition>(name);

            throw new InternalException($"Unknown type '{name}'");
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

        protected bool GetCompiledVariable(string variableName, out CompiledVariable compiledVariable)
            => compiledVariables.TryGetValue(variableName, out compiledVariable);

        protected bool GetParameter(string parameterName, out CompiledParameter parameters)
        {
            for (int i = 0; i < this.parameters.Count; i++)
            {
                if (this.parameters[i].Identifier.Content == parameterName)
                {
                    parameters = this.parameters[i];
                    return true;
                }
            }
            parameters = null;
            return false;
        }

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
        protected bool GetGeneralFunction(CompiledClass @class, string name, out CompiledGeneralFunction generalFunction)
        {
            for (int i = 0; i < CompiledGeneralFunctions.Length; i++)
            {
                var function = CompiledGeneralFunctions[i];
                if (function.Identifier.Content != name) continue;
                if (function.Type.Class != @class) continue;

                generalFunction = function;
                return true;
            }

            generalFunction = null;
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
        protected static DataItem GetInitialValue(TypeToken type)
        {
            if (type.IsList)
            {
                throw new NotImplementedException();
            }

            return type.Type switch
            {
                TypeTokenType.INT => new DataItem((int)0),
                TypeTokenType.BYTE => new DataItem((byte)0),
                TypeTokenType.FLOAT => new DataItem((float)0f),
                TypeTokenType.BOOLEAN => new DataItem((bool)false),
                TypeTokenType.CHAR => new DataItem((char)'\0'),

                TypeTokenType.AUTO => throw new CompilerException("Undefined type", type),
                TypeTokenType.VOID => throw new CompilerException("Invalid type", type),
                _ => throw new InternalException($"Initial value for type {type.Type} is unimplemented"),
            };
        }

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="InternalException"></exception>
        protected static DataItem GetInitialValue(CompiledType type)
        {
            if (type.IsList)
            {
                throw new NotImplementedException();
            }

            if (type.IsStruct)
            {
                throw new NotImplementedException();
            }

            if (type.IsClass)
            {
                return new DataItem((int)Utils.NULL_POINTER);
            }

            return type.BuiltinType switch
            {
                CompiledType.CompiledTypeType.BYTE => new DataItem((byte)0),
                CompiledType.CompiledTypeType.INT => new DataItem((int)0),
                CompiledType.CompiledTypeType.FLOAT => new DataItem((float)0f),
                CompiledType.CompiledTypeType.CHAR => new DataItem((char)'\0'),
                CompiledType.CompiledTypeType.BOOL => new DataItem((bool)false),

                CompiledType.CompiledTypeType.VOID => throw new NotImplementedException(),
                CompiledType.CompiledTypeType.NONE => throw new NotImplementedException(),
                _ => throw new InternalException($"Initial value for type {type.FullName} is unimplemented"),
            };
        }

        #endregion

        #region FindStatementType()
        protected CompiledType FindStatementType(Statement_FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "Dealloc") return new CompiledType(BuiltinType.VOID, GetCustomType);

            if (functionCall.FunctionName == "Alloc") return new CompiledType(BuiltinType.INT, GetCustomType);

            if (functionCall.FunctionName == "sizeof") return new CompiledType(BuiltinType.INT, GetCustomType);

            if (!GetCompiledFunction(functionCall, out var calledFunc))
            { throw new CompilerException("Function '" + GetReadableID(functionCall) + "' not found!", functionCall.Identifier, CurrentFile); }
            return calledFunc.Type;
        }
        protected CompiledType FindStatementType(Statement_Operator @operator)
        {
            Opcode opcode = Opcode.UNKNOWN;

            if (@operator.Operator.Content == "!")
            {
                if (@operator.ParameterCount != 1) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_NOT;
            }
            else if (@operator.Operator.Content == "+")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_ADD;
            }
            else if (@operator.Operator.Content == "<")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_LT;
            }
            else if (@operator.Operator.Content == ">")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_MT;
            }
            else if (@operator.Operator.Content == "-")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_SUB;
            }
            else if (@operator.Operator.Content == "*")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_MULT;
            }
            else if (@operator.Operator.Content == "/")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_DIV;
            }
            else if (@operator.Operator.Content == "%")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_MOD;
            }
            else if (@operator.Operator.Content == "==")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_EQ;
            }
            else if (@operator.Operator.Content == "!=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_NEQ;
            }
            else if (@operator.Operator.Content == "&&")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_AND;
            }
            else if (@operator.Operator.Content == "||")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_OR;
            }
            else if (@operator.Operator.Content == "^")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_XOR;
            }
            else if (@operator.Operator.Content == "<=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_LTEQ;
            }
            else if (@operator.Operator.Content == ">=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_MTEQ;
            }
            else if (@operator.Operator.Content == "<<")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.BITSHIFT_LEFT;
            }
            else if (@operator.Operator.Content == ">>")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.BITSHIFT_RIGHT;
            }

            if (opcode != Opcode.UNKNOWN)
            {
                var leftType = FindStatementType(@operator.Left);
                if (@operator.Right != null)
                {
                    var rightType = FindStatementType(@operator.Right);

                    if (leftType.IsBuiltin && rightType.IsBuiltin && leftType.GetBuiltinType() != BuiltinType.VOID && rightType.GetBuiltinType() != BuiltinType.VOID)
                    {
                        var leftValue = GetInitialValue(TypeToken.CreateAnonymous(leftType.Name, leftType.GetBuiltinType()));
                        var rightValue = GetInitialValue(TypeToken.CreateAnonymous(rightType.Name, rightType.GetBuiltinType()));

                        var predictedValue = PredictStatementValue(@operator.Operator.Content, leftValue, rightValue);
                        if (predictedValue.HasValue)
                        {
                            switch (predictedValue.Value.type)
                            {
                                case RuntimeType.BYTE: return new CompiledType(BuiltinType.BYTE, GetCustomType);
                                case RuntimeType.INT: return new CompiledType(BuiltinType.INT, GetCustomType);
                                case RuntimeType.FLOAT: return new CompiledType(BuiltinType.FLOAT, GetCustomType);
                                case RuntimeType.CHAR: return new CompiledType(BuiltinType.CHAR, GetCustomType);
                                case RuntimeType.BOOLEAN: return new CompiledType(BuiltinType.BOOLEAN, GetCustomType);
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
            { throw new CompilerException($"Unknown operator '{@operator.Operator.Content}'", @operator.Operator, CurrentFile); }
        }
        protected CompiledType FindStatementType(Statement_Literal literal) => literal.Type.Type switch
        {
            TypeTokenType.INT => new CompiledType(BuiltinType.INT, GetCustomType),
            TypeTokenType.BYTE => new CompiledType(BuiltinType.BYTE, GetCustomType),
            TypeTokenType.FLOAT => new CompiledType(BuiltinType.FLOAT, GetCustomType),
            TypeTokenType.STRING => new CompiledType("String", GetCustomType),
            TypeTokenType.BOOLEAN => new CompiledType(BuiltinType.BOOLEAN, GetCustomType),
            TypeTokenType.CHAR => new CompiledType(BuiltinType.CHAR, GetCustomType),
            _ => throw new CompilerException($"Unknown literal type {literal.Type.Type}", literal, CurrentFile),
        };
        protected CompiledType FindStatementType(Statement_Variable variable)
        {
            if (variable.VariableName.Content == "nullptr")
            { return new CompiledType(BuiltinType.INT, GetCustomType); }

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
            else
            {
                throw new CompilerException("Unknown variable '" + variable.VariableName.Content + "'", variable.VariableName, CurrentFile);
            }
        }
        protected CompiledType FindStatementType(Statement_MemoryAddressGetter _) => new(CompiledType.CompiledTypeType.INT);
        protected CompiledType FindStatementType(Statement_MemoryAddressFinder _) => new(CompiledType.CompiledTypeType.UNKNOWN);
        protected CompiledType FindStatementType(Statement_NewInstance newStruct)
        {
            if (GetCompiledStruct(newStruct, out var structDefinition))
            {
                return new CompiledType(structDefinition);
            }
            else if (GetCompiledClass(newStruct, out var classDefinition))
            {
                return new CompiledType(classDefinition);
            }
            else
            {
                throw new CompilerException("Unknown type '" + newStruct.TypeName.Content + "'", newStruct.TypeName, CurrentFile);
            }
        }
        protected CompiledType FindStatementType(Statement_ConstructorCall constructorCall)
        {
            if (GetCompiledClass(constructorCall, out var classDefinition))
            {
                return new CompiledType(classDefinition);
            }
            else
            {
                throw new CompilerException("Unknown type '" + constructorCall.TypeName.Content + "'", constructorCall.TypeName, CurrentFile);
            }
        }
        protected CompiledType FindStatementType(Statement_Field field)
        {
            var prevStatementType = FindStatementType(field.PrevStatement);

            if (prevStatementType.Name == "string" || prevStatementType.Name.EndsWith("[]"))
            {
                if (field.FieldName.Content == "Length") return new CompiledType(BuiltinType.INT, GetCustomType);
            }

            foreach (var strct in CompiledStructs)
            {
                if (strct.Key != prevStatementType.Name) continue;

                foreach (var sField in strct.Fields)
                {
                    if (sField.Identifier.Content != field.FieldName.Content) continue;
                    return sField.Type;
                }

                break;
            }

            foreach (var @class_ in CompiledClasses)
            {
                if (@class_.Key != prevStatementType.Name) continue;

                foreach (var sField in @class_.Fields)
                {
                    if (sField.Identifier.Content != field.FieldName.Content) continue;
                    return sField.Type;
                }

                break;
            }

            throw new CompilerException("Unknown type '" + prevStatementType + "'", field.TotalPosition(), CurrentFile);
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
                {
                    if (list.Values.Length > 0) return new CompiledType(FindStatementType(list.Values[0]));
                    throw new NotImplementedException();
                }
                else if (st is Statement_Index index)
                {
                    var type = FindStatementType(index.PrevStatement);
                    if (type.IsList) return type.ListOf;
                    throw new NotImplementedException();
                }
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
            return literal.Type.Type switch
            {
                TypeTokenType.INT => new DataItem(int.Parse(literal.Value), null),
                TypeTokenType.FLOAT => new DataItem(float.Parse(literal.Value.EndsWith('f') ? literal.Value[..^1] : literal.Value), null),
                TypeTokenType.BYTE => new DataItem(byte.Parse(literal.Value), null),
                TypeTokenType.STRING => new DataItem(literal.Value, null),
                TypeTokenType.BOOLEAN => new DataItem(bool.Parse(literal.Value), null),
                TypeTokenType.USER_DEFINED => throw new NotImplementedException(),
                _ => null,
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
