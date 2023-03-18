using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode.Compiler
{
    using IngameCoding.BBCode.Parser;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;
    using IngameCoding.Errors;

    public struct Reference<T>
    {
        public string FilePath;
        public T Ref;
        public bool IsAnything;

        public Reference(T @ref)
        {
            this.FilePath = string.Empty;
            this.Ref = @ref;
            this.IsAnything = true;
        }

        public override string ToString()
        {
            return IsAnything ? $"Ref<{(Ref == null ? "?" : Ref.GetType().Name)}> {{ \"{FilePath}\" {(Ref == null ? "Ref is null" : "Have ref")} }}" : $"Ref<?> null";
        }
    }

    public enum TokenSubSubtype
    {
        None,
        Attribute,
        Type,
        Struct,
        Keyword,
        FunctionName,
        VariableName,
        FieldName,
        ParameterName,
        Namespace,
    }

    class VariableStack : Stack<int>
    {
        internal int GetAllInStatements()
        {
            int result = 0;
            for (int i = 1; i < Count; i++)
            {
                result += this[i];
            }
            return result;
        }

        internal int GetAll()
        {
            int result = 0;
            for (int i = 0; i < Count; i++)
            { result += this[i]; }
            return result;
        }
    }

    public class DebugInfo
    {
        internal Position Position;
        internal int InstructionStart;
        internal int InstructionEnd;
    }

    public class CodeGenerator
    {
        static readonly string[] BuiltinFunctions = new string[]
        {
            "return",
            "break",
            "type",
        };

        static readonly string[] Keywords = new string[]
        {
            "int",
            "struct",
            "bool",
            "string",
            "float",
            "byte",
            "void"
        };

        static readonly Dictionary<string, DataType> TypeMap = new()
        {
            { "byte", DataType.BYTE },
            { "int", DataType.INT },
            { "float", DataType.FLOAT },
            { "string", DataType.STRING },
            { "bool", DataType.BOOLEAN },
        };

        #region Fields

        bool BlockCodeGeneration;

        internal Dictionary<string, CompiledStruct> compiledStructs;
        internal Dictionary<string, CompiledClass> compiledClasses;
        internal Dictionary<string, CompiledFunction> compiledFunctions;
        internal Dictionary<string, CompiledVariable> compiledVariables;
        /// <summary>
        /// Used for variable cleanups
        /// </summary>
        internal VariableStack variableCountStack;
        Dictionary<string, Parameter> parameters;

        Dictionary<string, BuiltinFunction> builtinFunctions;

        List<int> returnInstructions;
        List<List<int>> breakInstructions;
        string[] CurrentNamespace;

        List<Instruction> compiledCode;

        List<UndefinedFunctionOffset> undefinedFunctionOffsets;

        bool OptimizeCode;
        bool AddCommentsToCode = true;
        readonly bool TrimUnreachableCode = true;
        bool GenerateDebugInstructions = true;
        Compiler.CompileLevel CompileLevel;
        internal string CurrentFunction_ForRecursionProtection;

        public List<Error> errors;
        public List<Warning> warnings;
        internal List<Information> informations;
        public List<Hint> hints;
        string CurrentFile;
        internal readonly List<DebugInfo> GeneratedDebugInfo = new();

        #endregion

        #region Helper Functions

        ITypeDefinition GetCustomType(string name)
        {
            if (compiledStructs.ContainsKey(name)) return compiledStructs[name];
            if (compiledClasses.ContainsKey(name)) return compiledClasses[name];

            throw new InternalException($"Unknown type '{name}'");
        }

        string GetReadableID(Statement_FunctionCall functionCall)
        {
            string readableID = functionCall.TargetNamespacePathPrefix + functionCall.FunctionName;
            readableID += "(";
            bool addComma = false;
            if (functionCall.IsMethodCall && functionCall.PrevStatement != null)
            {
                readableID += "this " + FindStatementType(functionCall.PrevStatement);
                addComma = true;
            }
            for (int i = 0; i < functionCall.Parameters.Count; i++)
            {
                if (addComma) { readableID += ", "; }
                readableID += FindStatementType(functionCall.Parameters[i]);
                addComma = true;
            }
            readableID += ")";
            return readableID;
        }

        bool GetCompiledVariable(string variableName, out CompiledVariable compiledVariable) => compiledVariables.TryGetValue(variableName, out compiledVariable);

        bool GetParameter(string parameterName, out Parameter parameters) => this.parameters.TryGetValue(parameterName, out parameters);

        bool GetCompiledFunction(Statement_FunctionCall functionCallStatement, out CompiledFunction compiledFunction)
        {
            compiledFunction = null;
            string callID = "";

            string namespacePath = TryGetFunctionNamespacePath(functionCallStatement);
            if (namespacePath != "")
            {
                if (namespacePath.EndsWith(".")) namespacePath = namespacePath[..^1];
                functionCallStatement.TargetNamespacePathPrefix = namespacePath;
                functionCallStatement.PrevStatement = null;

                for (int i = 0; i < functionCallStatement.Parameters.Count; i++)
                { callID += "," + FindStatementType(functionCallStatement.Parameters[i]); }
            }
            else
            {
                if (functionCallStatement.PrevStatement != null)
                { callID += "," + FindStatementType(functionCallStatement.PrevStatement); }
                for (int i = 0; i < functionCallStatement.Parameters.Count; i++)
                { callID += "," + FindStatementType(functionCallStatement.Parameters[i]); }
            }

            List<string> searchForThese = new()
            {
                functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID,
                functionCallStatement.NamespacePathPrefix + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID
            };

            foreach (var searchName in searchForThese)
            {
                if (compiledFunctions.TryGetValue(searchName, out compiledFunction))
                { return true; }
            }

            return false;
        }

        string TryGetFunctionNamespacePath(Statement_FunctionCall functionCallStatement)
        {
            string[] Get(Statement statement)
            {
                if (statement is Statement_Variable s1)
                {
                    if (GetParameter(s1.VariableName.text, out _))
                    {
                        return null;
                    }
                    else if (GetCompiledVariable(s1.VariableName.text, out _))
                    {
                        return null;
                    }
                    s1.VariableName.Analysis.CompilerReached = true;
                    s1.VariableName.Analysis.SubSubtype = TokenSubSubtype.Namespace;
                    return new string[] { s1.VariableName.text };
                }
                if (statement is Statement_Field s2)
                {
                    var prev_ = Get(s2.PrevStatement);
                    if (prev_ == null) { return null; }

                    s2.FieldName.Analysis.CompilerReached = true;
                    s2.FieldName.Analysis.SubSubtype = TokenSubSubtype.Namespace;

                    var prev = prev_.ToList();
                    prev.Insert(0, s2.FieldName.text);
                    return prev.ToArray();
                }
                return null;
            }

            if (functionCallStatement.PrevStatement != null)
            {
                string[] path = Get(functionCallStatement.PrevStatement);
                if (path == null) return "";
                if (!functionCallStatement.TargetNamespacePathPrefixIsReversed)
                {
                    functionCallStatement.TargetNamespacePathPrefixIsReversed = true;
                    List<string> pathReversed = path.ToList();
                    pathReversed.Reverse();
                    path = pathReversed.ToArray();
                }
                return string.Join(".", path) + ".";
            }
            else
            {
                return "";
            }
        }

        bool GetCompiledStruct(Statement_NewStruct newStructStatement, out CompiledStruct compiledStruct)
        {
            if (compiledStructs.TryGetValue(newStructStatement.StructName.text, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.NamespacePathPrefix + newStructStatement.StructName.text, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.NamespacePathPrefix + newStructStatement.TargetNamespacePathPrefix + newStructStatement.StructName.text, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.TargetNamespacePathPrefix + newStructStatement.StructName.text, out compiledStruct))
            {
                return true;
            }
            return false;
        }

        bool GetCompiledClass(Statement_NewStruct newClassStatement, out CompiledClass compiledClass)
        {
            if (compiledClasses.TryGetValue(newClassStatement.StructName.text, out compiledClass))
            {
                return true;
            }
            else if (compiledClasses.TryGetValue(newClassStatement.NamespacePathPrefix + newClassStatement.StructName.text, out compiledClass))
            {
                return true;
            }
            else if (compiledClasses.TryGetValue(newClassStatement.NamespacePathPrefix + newClassStatement.TargetNamespacePathPrefix + newClassStatement.StructName.text, out compiledClass))
            {
                return true;
            }
            else if (compiledClasses.TryGetValue(newClassStatement.TargetNamespacePathPrefix + newClassStatement.StructName.text, out compiledClass))
            {
                return true;
            }
            return false;
        }

        bool GetCompiledClass(string className, out CompiledClass compiledClass) => compiledClasses.TryGetValue(className, out compiledClass);

        object GenerateInitialValue(TypeToken type)
        {
            if (type.IsList)
            {
                return new DataItem.List(type.ListOf.typeName.Convert());
            }
            else
            {
                return type.typeName switch
                {
                    BuiltinType.INT => 0,
                    BuiltinType.BYTE => (byte)0,
                    BuiltinType.AUTO => throw new CompilerException("Undefined type", type, CurrentFile),
                    BuiltinType.FLOAT => 0f,
                    BuiltinType.VOID => throw new CompilerException("Invalid type", type, CurrentFile),
                    BuiltinType.STRING => "",
                    BuiltinType.BOOLEAN => false,
                    BuiltinType.STRUCT => new UnassignedStruct(),
                    _ => throw new InternalException($"initial value for type {type.typeName} is unimplemented", CurrentFile),
                };
            }
        }

        #endregion

        #region AddInstruction()

        void AddInstruction(Instruction instruction)
        {
            if (BlockCodeGeneration) return;
            if (instruction.opcode == Opcode.COMMENT && !AddCommentsToCode) return;
            if (instruction.opcode == Opcode.DEBUG_SET_TAG && !GenerateDebugInstructions) return;

            compiledCode.Add(instruction);
        }
        void AddInstruction(Opcode opcode) => AddInstruction(new Instruction(opcode));
        void AddInstruction(Opcode opcode, object param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, DataItem.List param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, DataItem param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, IStruct param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, string param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, int param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, bool param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, float param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, byte param0, string tag = null) => AddInstruction(new Instruction(opcode, param0) { tag = tag ?? string.Empty });

        void AddInstruction(Opcode opcode, AddressingMode addressingMode) => AddInstruction(new Instruction(opcode, addressingMode));
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, object param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, DataItem.List param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, DataItem param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, IStruct param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, string param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, int param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, (object)param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, bool param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, float param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });
        void AddInstruction(Opcode opcode, AddressingMode addressingMode, byte param0, string tag = null) => AddInstruction(new Instruction(opcode, addressingMode, param0) { tag = tag ?? string.Empty });

        #endregion

        #region FindStatementType()
        CompiledType FindStatementType(Statement_FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "type") return new CompiledType(BuiltinType.STRING);

            if (!GetCompiledFunction(functionCall, out var calledFunc))
            { throw new CompilerException("Function '" + GetReadableID(functionCall) + "' not found!", functionCall.Identifier, CurrentFile); }
            return FindStatementType(calledFunc.Type);
        }
        CompiledType FindStatementType(Statement_Operator @operator)
        {
            Opcode opcode = Opcode.UNKNOWN;

            if (@operator.Operator.text == "!")
            {
                if (@operator.ParameterCount != 1) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_NOT;
            }
            else if (@operator.Operator.text == "+")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_ADD;
            }
            else if (@operator.Operator.text == "<")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_LT;
            }
            else if (@operator.Operator.text == ">")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_MT;
            }
            else if (@operator.Operator.text == "-")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_SUB;
            }
            else if (@operator.Operator.text == "*")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_MULT;
            }
            else if (@operator.Operator.text == "/")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_DIV;
            }
            else if (@operator.Operator.text == "%")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.MATH_MOD;
            }
            else if (@operator.Operator.text == "==")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_EQ;
            }
            else if (@operator.Operator.text == "!=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_NEQ;
            }
            else if (@operator.Operator.text == "&")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_AND;
            }
            else if (@operator.Operator.text == "|")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_OR;
            }
            else if (@operator.Operator.text == "^")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_XOR;
            }
            else if (@operator.Operator.text == "<=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_LTEQ;
            }
            else if (@operator.Operator.text == ">=")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.LOGIC_MTEQ;
            }
            else if (@operator.Operator.text == "<<")
            {
                if (@operator.ParameterCount != 2) throw new CompilerException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", @operator.Operator, CurrentFile);
                opcode = Opcode.BITSHIFT_LEFT;
            }
            else if (@operator.Operator.text == ">>")
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

                    if (TypeMap.TryGetValue(leftType.Name, out var leftTypeName) && TypeMap.TryGetValue(rightType.Name, out var rightTypeName))
                    {
                        var leftValue = GenerateInitialValue(TypeToken.CreateAnonymous(leftType.Name, leftTypeName.Convert()));
                        var rightValue = GenerateInitialValue(TypeToken.CreateAnonymous(rightType.Name, rightTypeName.Convert()));

                        var predictedValue = PredictStatementValue(@operator.Operator.text, new DataItem(leftValue, null), new DataItem(rightValue, null)); ;
                        if (predictedValue.HasValue)
                        {
                            switch (predictedValue.Value.type)
                            {
                                case DataType.BYTE: return new CompiledType(BuiltinType.BYTE);
                                case DataType.INT: return new CompiledType(BuiltinType.INT);
                                case DataType.FLOAT: return new CompiledType(BuiltinType.FLOAT);
                                case DataType.STRING: return new CompiledType(BuiltinType.STRING);
                                case DataType.BOOLEAN: return new CompiledType(BuiltinType.BOOLEAN);
                            }
                        }
                    }

                    warnings.Add(new Warning("Thats not good :(", @operator.TotalPosition(), CurrentFile));
                    return null;
                }
                else
                { return leftType; }
            }
            else if (@operator.Operator.text == "=")
            {
                throw new NotImplementedException();
            }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.text}'", @operator.Operator, CurrentFile); }
        }
        static CompiledType FindStatementType(Statement_Literal literal) => literal.Type.typeName switch
        {
            BuiltinType.INT => new CompiledType(BuiltinType.INT),
            BuiltinType.BYTE => new CompiledType(BuiltinType.BYTE),
            BuiltinType.FLOAT => new CompiledType(BuiltinType.FLOAT),
            BuiltinType.STRING => new CompiledType(BuiltinType.STRING),
            BuiltinType.BOOLEAN => new CompiledType(BuiltinType.BOOLEAN),
            _ => throw new NotImplementedException(),
        };
        CompiledType FindStatementType(Statement_Variable variable)
        {
            if (GetParameter(variable.VariableName.text, out Parameter param))
            {
                if (variable.ListIndex != null)
                { throw new NotImplementedException(); }
                return new CompiledType(param.type, GetCustomType);
            }
            else if (GetCompiledVariable(variable.VariableName.text, out CompiledVariable val))
            {
                return new CompiledType(val, GetCustomType);
            }
            else if (variable.VariableName.text == "this")
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.VariableName.text + "'", variable.VariableName, CurrentFile);
            }
        }
        CompiledType FindStatementType(Statement_NewStruct newStruct)
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
                throw new CompilerException("Unknown type '" + newStruct.StructName.text + "'", newStruct.StructName, CurrentFile);
            }
        }
        CompiledType FindStatementType(TypeToken type)
        {
            if (type.typeName == BuiltinType.STRUCT)
            {
                if (compiledStructs.TryGetValue(type.text, out var @struct)) return new CompiledType(@struct);
                if (compiledClasses.TryGetValue(type.text, out var @class)) return new CompiledType(@class);
            }

            return new CompiledType(type, GetCustomType);
        }
        CompiledType FindStatementType(Statement_Field field)
        {
            var prevStatementType = FindStatementType(field.PrevStatement);

            if (prevStatementType.Name == "string" || prevStatementType.Name.EndsWith("[]"))
            {
                if (field.FieldName.text == "Length") return new CompiledType(BuiltinType.INT);
            }

            foreach (var strct in compiledStructs)
            {
                if (strct.Key != prevStatementType.Name) continue;

                foreach (var sField in strct.Value.Fields)
                {
                    if (sField.name.text != field.FieldName.text) continue;
                    return FindStatementType(sField.type);
                }

                break;
            }

            foreach (var @class_ in compiledClasses)
            {
                if (@class_.Key != prevStatementType.Name) continue;

                foreach (var sField in @class_.Value.Fields)
                {
                    if (sField.name.text != field.FieldName.text) continue;
                    return FindStatementType(sField.type);
                }

                break;
            }

            throw new CompilerException("Unknown type '" + prevStatementType + "'", field.TotalPosition(), CurrentFile);
        }

        CompiledType FindStatementType(Statement st)
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
                else if (st is Statement_NewStruct newStruct)
                { return FindStatementType(newStruct); }
                else if (st is Statement_Field field)
                { return FindStatementType(field); }
                else if (st is Statement_ListValue list)
                {
                    if (list.Values.Count > 0) return new CompiledType(FindStatementType(list.Values[0]));
                    throw new NotImplementedException();
                }
                else if (st is Statement_Index index)
                {
                    var type = FindStatementType(index.PrevStatement);
                    if (type.IsList) return type.ListOf;
                    else if (type.Name == "string") return new CompiledType(BuiltinType.STRING);
                    throw new NotImplementedException();
                }
                throw new NotImplementedException();
            }
            catch (InternalException error)
            {
                errors.Add(new Error(error.Message, st.TotalPosition()));
                throw;
            }
        }
        #endregion

        #region PredictStatementValue()
        static DataItem? PredictStatementValue(string @operator, DataItem left, DataItem right) => @operator switch
        {
            "!" => !left,

            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => left / right,
            "%" => left % right,

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
        static DataItem? PredictStatementValue(Statement_Operator @operator)
        {
            var leftValue = PredictStatementValue(@operator.Left);
            if (!leftValue.HasValue) return null;

            if (@operator.Operator.text == "!")
            {
                return !leftValue;
            }

            if (@operator.Right != null)
            {
                var rightValue = PredictStatementValue(@operator.Right);
                if (!rightValue.HasValue) return null;

                return PredictStatementValue(@operator.Operator.text, leftValue.Value, rightValue.Value);
            }
            else
            { return leftValue; }
        }
        static DataItem? PredictStatementValue(Statement_Literal literal)
        {
            return literal.Type.typeName switch
            {
                BuiltinType.INT => new DataItem(int.Parse(literal.Value), null),
                BuiltinType.FLOAT => new DataItem(float.Parse(literal.Value), null),
                BuiltinType.BYTE => new DataItem(byte.Parse(literal.Value), null),
                BuiltinType.STRING => new DataItem(literal.Value, null),
                BuiltinType.BOOLEAN => new DataItem(bool.Parse(literal.Value), null),
                BuiltinType.STRUCT => new DataItem(new UnassignedStruct(), null),
                _ => null,
            };
        }
        static DataItem? PredictStatementValue(Statement st)
        {
            if (st is Statement_Literal literal)
            { return PredictStatementValue(literal); }
            else if (st is Statement_Operator @operator)
            { return PredictStatementValue(@operator); }

            return null;
        }
        #endregion

        #region GenerateCodeForStatement

        void GenerateCodeForStatement(Statement_NewVariable newVariable)
        {
            newVariable.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
            newVariable.VariableName.Analysis.CompilerReached = true;

            if (!GetCompiledVariable(newVariable.VariableName.text, out CompiledVariable val_))
            { throw new CompilerException("Unknown variable '" + newVariable.VariableName.text + "'", newVariable.VariableName, CurrentFile); }

            if (newVariable.InitialValue != null)
            {
                if (newVariable.InitialValue is Statement_ListValue initialListValue)
                {
                    if (initialListValue.Values.Count == 0)
                    { AddInstruction(Opcode.PUSH_VALUE, GenerateInitialValue(newVariable.Type)); }
                    else
                    { GenerateCodeForStatement(newVariable.InitialValue); }
                }
                else
                { GenerateCodeForStatement(newVariable.InitialValue); }
                AddInstruction(Opcode.COPY_VALUE_RECURSIVE);
            }
            else
            { AddInstruction(Opcode.PUSH_VALUE, GenerateInitialValue(newVariable.Type)); }

            if (val_.IsStoredInHEAP)
            { AddInstruction(Opcode.HEAP_SET, val_.offset); }
            else
            { AddInstruction(Opcode.STORE_VALUE, val_.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, val_.offset); }
        }
        void GenerateCodeForStatement(Statement_FunctionCall functionCall)
        {
            AddInstruction(Opcode.COMMENT, $"{functionCall.FunctionName}():");

            functionCall.Identifier.Analysis.CompilerReached = true;

            if (functionCall.FunctionName == "return")
            {
                if (functionCall.Parameters.Count > 1)
                { throw new CompilerException("Wrong number of parameters passed to 'return'", functionCall.TotalPosition(), CurrentFile); }
                else if (functionCall.Parameters.Count == 1)
                {
                    GenerateCodeForStatement(functionCall.Parameters[0]);
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, -2 - parameters.Count);
                }

                // Clear variables
                int variableCount = variableCountStack.GetAllInStatements();
                if (AddCommentsToCode && variableCount > 0)
                {
                    AddInstruction(Opcode.COMMENT, "Clear variables");
                }
                for (int i = 0; i < variableCount; i++)
                { AddInstruction(Opcode.POP_VALUE); }

                returnInstructions.Add(compiledCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                return;
            }

            if (functionCall.FunctionName == "break")
            {
                if (breakInstructions.Count <= 0)
                { throw new CompilerException("The keyword 'break' does not avaiable in the current context", functionCall.Identifier, CurrentFile); }

                breakInstructions.Last().Add(compiledCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                return;
            }

            if (functionCall.FunctionName == "type")
            {
                if (functionCall.Parameters.Count != 1)
                { throw new CompilerException("Wrong number of parameters passed to 'type'", functionCall.TotalPosition(), CurrentFile); }

                GenerateCodeForStatement(functionCall.Parameters[0]);
                AddInstruction(Opcode.TYPE_GET);

                return;
            }

            if (functionCall.IsMethodCall)
            {
                if (functionCall.PrevStatement is Statement_Variable prevVar)
                {
                    if (GetCompiledVariable(prevVar.VariableName.text, out var prevVarInfo))
                    {
                        prevVar.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                        prevVar.VariableName.Analysis.CompilerReached = true;
                        prevVar.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(prevVarInfo.Declaration, prevVarInfo.IsGlobal);

                        if (prevVarInfo.IsList)
                        {
                            if (functionCall.FunctionName == "Push")
                            {
                                functionCall.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;
                                functionCall.Identifier.Analysis.CompilerReached = true;
                                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinMethod("Push", "void", prevVarInfo.Type, new string[] { prevVarInfo.Type[..^2] }, new string[] { "newElement" });

                                AddInstruction(Opcode.LOAD_VALUE, prevVarInfo.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, prevVarInfo.offset);

                                if (functionCall.Parameters.Count != 1)
                                { throw new CompilerException($"Wrong number of parameters passed to '{prevVarInfo.Type}.Push'", functionCall.Identifier, CurrentFile); }

                                var paramType = FindStatementType(functionCall.Parameters[0]);
                                if (paramType + "[]" != prevVarInfo.Type)
                                { throw new CompilerException($"Wrong type passed to '{prevVarInfo.Type}.Push': {paramType}, expected {prevVarInfo.Type[..^2]}", functionCall.Parameters[0].TotalPosition()); }

                                GenerateCodeForStatement(functionCall.Parameters[0]);

                                AddInstruction(Opcode.LIST_PUSH_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Pull")
                            {
                                functionCall.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;
                                functionCall.Identifier.Analysis.CompilerReached = true;
                                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinMethod("Pull", prevVarInfo.Type[..^2], prevVarInfo.Type, Array.Empty<string>(), Array.Empty<string>());

                                AddInstruction(Opcode.LOAD_VALUE, prevVarInfo.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, prevVarInfo.offset);

                                if (functionCall.Parameters.Count != 0)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Pull'", functionCall.Identifier, CurrentFile); }

                                AddInstruction(Opcode.LIST_PULL_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Add")
                            {
                                functionCall.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;
                                functionCall.Identifier.Analysis.CompilerReached = true;
                                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinMethod("Add", "void", prevVarInfo.Type, new string[] { prevVarInfo.Type[..^2], "int" }, new string[] { "newElement", "index" });

                                AddInstruction(Opcode.LOAD_VALUE, prevVarInfo.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, prevVarInfo.offset);

                                if (functionCall.Parameters.Count != 2)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Add'", functionCall.Identifier, CurrentFile); }

                                var paramType = FindStatementType(functionCall.Parameters[0]);
                                if (paramType + "[]" != prevVarInfo.Type)
                                { throw new CompilerException($"Wrong type passed to '<list>.Add': {paramType}, expected {prevVarInfo.Type[..^2]}", functionCall.Parameters[0].TotalPosition()); }

                                GenerateCodeForStatement(functionCall.Parameters[0]);
                                GenerateCodeForStatement(functionCall.Parameters[1]);

                                AddInstruction(Opcode.LIST_ADD_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Remove")
                            {
                                functionCall.Identifier.Analysis.SubSubtype = TokenSubSubtype.FunctionName;
                                functionCall.Identifier.Analysis.CompilerReached = true;
                                functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefBuiltinMethod("Remove", "void", prevVarInfo.Type, new string[] { "int" }, new string[] { "index" });

                                AddInstruction(Opcode.LOAD_VALUE, prevVarInfo.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, prevVarInfo.offset);

                                if (functionCall.Parameters.Count != 1)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Remove'", functionCall.Identifier, CurrentFile); }
                                GenerateCodeForStatement(functionCall.Parameters[0]);

                                AddInstruction(Opcode.LIST_REMOVE_ITEM);

                                return;
                            }
                        }
                    }
                }
            }

            if (functionCall.FunctionName == "None")
            {
                foreach (var param in functionCall.Parameters)
                {
                    Console.WriteLine(PredictStatementValue(param));
                }
            }

            string searchedID = functionCall.TargetNamespacePathPrefix + functionCall.FunctionName;
            searchedID += "(";
            for (int i = 0; i < functionCall.Parameters.Count; i++)
            {
                if (i > 0) { searchedID += ", "; }

                searchedID += FindStatementType(functionCall.Parameters[i]);
            }
            searchedID += ")";

            if (!GetCompiledFunction(functionCall, out CompiledFunction compiledFunction))
            {
                throw new CompilerException("Unknown function " + searchedID + "", functionCall.Identifier, CurrentFile);
            }

            if (!compiledFunction.CanUse(CurrentFile))
            {
                errors.Add(new Error($"The {searchedID} function cannot be called due to its protection level", functionCall.Identifier, CurrentFile));
                return;
            }

            if (CurrentFunction_ForRecursionProtection != null && compiledFunction.ID() == CurrentFunction_ForRecursionProtection)
            {
                warnings.Add(new Warning($"Potential infinite recursive call", functionCall.TotalPosition(), CurrentFile));
            }

            functionCall.Identifier.Analysis.Reference = new TokenAnalysis.RefFunction(compiledFunction);

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new CompilerException("Wrong number of parameters passed to '" + searchedID + $"': requied {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall.TotalPosition(), CurrentFile); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new CompilerException($"You called the {((compiledFunction.IsMethod) ? "method" : "function")} '{functionCall.FunctionName}' as {((functionCall.IsMethodCall) ? "method" : "function")}", functionCall.TotalPosition(), CurrentFile); }

            if (compiledFunction.IsBuiltin)
            {
                if (!builtinFunctions.TryGetValue(compiledFunction.BuiltinName, out var builtinFunction))
                {
                    errors.Add(new Error($"Builtin function '{compiledFunction.BuiltinName}' not found", functionCall.Identifier, CurrentFile));

                    var savedBlockCodeGeneration = BlockCodeGeneration;
                    BlockCodeGeneration = false;

                    if (functionCall.PrevStatement != null)
                    { GenerateCodeForStatement(functionCall.PrevStatement); }
                    foreach (var param in functionCall.Parameters)
                    { GenerateCodeForStatement(param); }

                    BlockCodeGeneration = savedBlockCodeGeneration;
                    return;
                }

                if (functionCall.PrevStatement != null)
                { GenerateCodeForStatement(functionCall.PrevStatement); }
                foreach (var param in functionCall.Parameters)
                { GenerateCodeForStatement(param); }

                AddInstruction(Opcode.PUSH_VALUE, compiledFunction.BuiltinName);
                AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
                { AddInstruction(Opcode.POP_VALUE); }

                return;
            }

            if (compiledFunction.ReturnSomething)
            { AddInstruction(Opcode.PUSH_VALUE, GenerateInitialValue(compiledFunction.Type), "return value"); }

            if (functionCall.PrevStatement != null)
            {
                GenerateCodeForStatement(functionCall.PrevStatement);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");
            }

            for (int i = 0; i < functionCall.Parameters.Count; i++)
            {
                Statement param = functionCall.Parameters[i];
                ParameterDefinition definedParam = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];

                AddInstruction(Opcode.COMMENT, $"param:");
                GenerateCodeForStatement(param);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.name);
            }

            if (compiledFunction.InstructionOffset == -1)
            { undefinedFunctionOffsets.Add(new UndefinedFunctionOffset(compiledCode.Count, functionCall, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

            AddInstruction(Opcode.CALL, compiledFunction.InstructionOffset - compiledCode.Count);

            for (int i = 0; i < functionCall.Parameters.Count; i++)
            {
                if (functionCall.Parameters[i] is Statement_Variable variable) if (GetCompiledVariable(variable.VariableName.text, out var v))
                        if (v.IsStoredInHEAP)
                        {
                            AddInstruction(Opcode.DEBUG_SET_TAG, "");
                            AddInstruction(Opcode.HEAP_SET, v.offset);
                            continue;
                        }


                AddInstruction(Opcode.POP_VALUE);
            }

            if (functionCall.PrevStatement != null)
            {
                bool x = true;
                if (functionCall.PrevStatement is Statement_Variable variable) if (GetCompiledVariable(variable.VariableName.text, out var v))
                        if (v.IsStoredInHEAP)
                        {
                            AddInstruction(Opcode.DEBUG_SET_TAG, "");
                            AddInstruction(Opcode.HEAP_SET, v.offset);
                            x = false;
                        }
                if (x) AddInstruction(Opcode.POP_VALUE);
            }

            if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
            { AddInstruction(Opcode.POP_VALUE); }
        }
        void GenerateCodeForStatement(Statement_Operator @operator)
        {
            @operator.Operator.Analysis.CompilerReached = true;

            if (OptimizeCode)
            {
                DataItem? predictedValueN = PredictStatementValue(@operator);
                if (predictedValueN.HasValue)
                {
                    var predictedValue = predictedValueN.Value;

                    switch (predictedValue.type)
                    {
                        case DataType.BYTE:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueByte);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueByte}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case DataType.INT:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueInt);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueInt}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case DataType.BOOLEAN:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueBoolean);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueBoolean}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case DataType.FLOAT:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueFloat);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueFloat}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                        case DataType.STRING:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueString);
                                informations.Add(new Information($"Predicted value: {predictedValue.ValueString}", @operator.TotalPosition(), CurrentFile));
                                return;
                            }
                    }
                }
            }

            Dictionary<string, Opcode> operatorOpCodes = new()
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
                { "&", Opcode.LOGIC_AND },
                { "|", Opcode.LOGIC_OR },
                { "^", Opcode.LOGIC_XOR },
                { "<=", Opcode.LOGIC_LTEQ },
                { ">=", Opcode.LOGIC_MTEQ },
                { "<<", Opcode.BITSHIFT_LEFT },
                { ">>", Opcode.BITSHIFT_RIGHT },
            };
            Dictionary<string, int> operatorParameterCounts = new()
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
                { "&", 2 },
                { "|", 2 },
                { "^", 2 },
                { "<=", 2 },
                { ">=", 2 },
                { "<<", 2 },
                { ">>", 2 },
            };


            if (operatorOpCodes.TryGetValue(@operator.Operator.text, out Opcode opcode))
            {
                if (operatorParameterCounts[@operator.Operator.text] != @operator.ParameterCount)
                { throw new CompilerException($"Wrong number of passed ({@operator.ParameterCount}) to operator '{@operator.Operator.text}', requied: {operatorParameterCounts[@operator.Operator.text]}", @operator.Operator, CurrentFile); }
            }
            else
            {
                opcode = Opcode.UNKNOWN;
            }

            if (opcode != Opcode.UNKNOWN)
            {
                GenerateCodeForStatement(@operator.Left);
                if (@operator.Right != null) GenerateCodeForStatement(@operator.Right);
                AddInstruction(opcode);
            }
            else if (@operator.Operator.text == "=")
            {
                if (@operator.ParameterCount != 2)
                { throw new CompilerException("Wrong number of parameters passed to assigment operator '" + @operator.Operator.text + "'", @operator.Operator, CurrentFile); }

                GenerateCodeForValueSetter(@operator.Left, @operator.Right);
            }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.text}'", @operator.Operator, CurrentFile); }
        }
        void GenerateCodeForStatement(Statement_Literal literal)
        {
            if (literal.ValueToken != null)
            { try { literal.ValueToken.Analysis.CompilerReached = true; } catch (NullReferenceException) { } }

            switch (literal.Type.typeName)
            {
                case BuiltinType.INT:
                    AddInstruction(Opcode.PUSH_VALUE, int.Parse(literal.Value));
                    break;
                case BuiltinType.FLOAT:
                    AddInstruction(Opcode.PUSH_VALUE, float.Parse(literal.Value.TrimEnd('f'), System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case BuiltinType.STRING:
                    AddInstruction(Opcode.PUSH_VALUE, literal.Value);
                    break;
                case BuiltinType.BOOLEAN:
                    AddInstruction(Opcode.PUSH_VALUE, bool.Parse(literal.Value));
                    break;
                case BuiltinType.BYTE:
                    AddInstruction(Opcode.PUSH_VALUE, byte.Parse(literal.Value));
                    break;
            }
        }
        void GenerateCodeForStatement(Statement_Variable variable)
        {
            variable.VariableName.Analysis.CompilerReached = true;

            if (GetParameter(variable.VariableName.text, out Parameter param))
            {
                variable.VariableName.Analysis.SubSubtype = TokenSubSubtype.ParameterName;
                variable.VariableName.Analysis.Reference = new TokenAnalysis.RefParameter(param.type);

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, param.RealIndex);
            }
            else if (GetCompiledVariable(variable.VariableName.text, out CompiledVariable val))
            {
                variable.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                variable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(val.Declaration, val.IsGlobal);

                if (val.IsStoredInHEAP)
                {
                    AddInstruction(Opcode.HEAP_GET, val.offset);
                    AddInstruction(Opcode.COPY_VALUE_RECURSIVE);
                }
                else
                {
                    AddInstruction(Opcode.LOAD_VALUE, val.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, val.offset);
                }
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.VariableName.text + "'", variable.VariableName, CurrentFile);
            }

            if (variable.ListIndex != null)
            {
                GenerateCodeForStatement(variable.ListIndex);
                AddInstruction(Opcode.LIST_INDEX);
            }
        }
        void GenerateCodeForStatement(Statement_WhileLoop whileLoop)
        {
            var conditionValue_ = PredictStatementValue(whileLoop.Condition);
            if (conditionValue_.HasValue)
            {
                if (conditionValue_.Value.type != DataType.BOOLEAN)
                {
                    warnings.Add(new Warning($"Condition must be boolean", whileLoop.Condition.TotalPosition(), CurrentFile));
                }
                else if (TrimUnreachableCode)
                {
                    if (!conditionValue_.Value.ValueBoolean)
                    {
                        AddInstruction(Opcode.COMMENT, "Unreachable code not compiled");
                        informations.Add(new Information($"Unreachable code not compiled", new Position(whileLoop.BracketStart, whileLoop.BracketEnd), CurrentFile));
                        return;
                    }
                }
            }

            AddInstruction(Opcode.COMMENT, "while (...) {");
            AddInstruction(Opcode.COMMENT, "Condition");
            int conditionOffset = compiledCode.Count;
            GenerateCodeForStatement(whileLoop.Condition);

            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffset = compiledCode.Count - 1;

            breakInstructions.Add(new List<int>());

            AddInstruction(Opcode.COMMENT, "Statements {");
            for (int i = 0; i < whileLoop.Statements.Count; i++)
            {
                GenerateCodeForStatement(whileLoop.Statements[i]);
            }

            AddInstruction(Opcode.COMMENT, "}");

            AddInstruction(Opcode.COMMENT, "Jump Back");
            AddInstruction(Opcode.JUMP_BY, conditionOffset - compiledCode.Count);

            AddInstruction(Opcode.COMMENT, "}");

            compiledCode[conditionJumpOffset].parameter = compiledCode.Count - conditionJumpOffset;
            List<int> currentBreakInstructions = breakInstructions.Last();

            if (currentBreakInstructions.Count == 0)
            {
                if (conditionValue_.HasValue)
                {
                    var conditionValue = conditionValue_.Value;
                    if (conditionValue.type == DataType.BOOLEAN)
                    {
                        if (conditionValue.ValueBoolean)
                        { warnings.Add(new Warning($"Infinity loop", whileLoop.Keyword, CurrentFile)); }
                        else
                        { warnings.Add(new Warning($"Why? this will never run", whileLoop.Keyword, CurrentFile)); }
                    }
                }
            }

            foreach (var breakInstruction in currentBreakInstructions)
            {
                compiledCode[breakInstruction].parameter = compiledCode.Count - breakInstruction;
            }
            breakInstructions.RemoveAt(breakInstructions.Count - 1);
        }
        void GenerateCodeForStatement(Statement_ForLoop forLoop)
        {
            AddInstruction(Opcode.COMMENT, "for (...) {");

            AddInstruction(Opcode.COMMENT, "FOR Declaration");
            // Index variable
            GenerateCodeForVariable(forLoop.VariableDeclaration, false);
            variableCountStack.Push(1);
            GenerateCodeForStatement(forLoop.VariableDeclaration);

            AddInstruction(Opcode.COMMENT, "FOR Condition");
            // Index condition
            int conditionOffsetFor = compiledCode.Count;
            GenerateCodeForStatement(forLoop.Condition);
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffsetFor = compiledCode.Count - 1;

            breakInstructions.Add(new List<int>());

            AddInstruction(Opcode.COMMENT, "Statements {");
            for (int i = 0; i < forLoop.Statements.Count; i++)
            {
                Statement currStatement = forLoop.Statements[i];
                GenerateCodeForStatement(currStatement);
            }

            AddInstruction(Opcode.COMMENT, "}");

            AddInstruction(Opcode.COMMENT, "FOR Expression");
            // Index expression
            GenerateCodeForStatement(forLoop.Expression);

            AddInstruction(Opcode.COMMENT, "Jump back");
            AddInstruction(Opcode.JUMP_BY, conditionOffsetFor - compiledCode.Count);
            compiledCode[conditionJumpOffsetFor].parameter = compiledCode.Count - conditionJumpOffsetFor;

            foreach (var breakInstruction in breakInstructions.Last())
            {
                compiledCode[breakInstruction].parameter = compiledCode.Count - breakInstruction;
            }
            breakInstructions.RemoveAt(breakInstructions.Count - 1);

            ClearVariables(variableCountStack.Pop());

            AddInstruction(Opcode.COMMENT, "}");
        }
        void GenerateCodeForStatement(Statement_If @if)
        {
            List<int> jumpOutInstructions = new();

            foreach (var ifSegment in @if.Parts)
            {
                if (ifSegment is Statement_If_If partIf)
                {
                    var conditionValue_ = PredictStatementValue(partIf.Condition);
                    if (conditionValue_.HasValue)
                    {
                        var conditionValue = conditionValue_.Value;

                        if (conditionValue.type != DataType.BOOLEAN)
                        {
                            warnings.Add(new Warning($"Condition must be boolean", partIf.Condition.TotalPosition(), CurrentFile));
                        }
                    }

                    AddInstruction(Opcode.COMMENT, "if (...) {");

                    AddInstruction(Opcode.COMMENT, "IF Condition");
                    GenerateCodeForStatement(partIf.Condition);
                    AddInstruction(Opcode.COMMENT, "IF Jump to Next");
                    int jumpNextInstruction = compiledCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    variableCountStack.Push(CompileVariables(partIf, false));

                    AddInstruction(Opcode.COMMENT, "IF Statements");
                    for (int i = 0; i < partIf.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partIf.Statements[i]);
                    }

                    ClearVariables(variableCountStack.Pop());

                    AddInstruction(Opcode.COMMENT, "IF Jump to End");
                    jumpOutInstructions.Add(compiledCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddInstruction(Opcode.COMMENT, "}");

                    compiledCode[jumpNextInstruction].parameter = compiledCode.Count - jumpNextInstruction;
                }
                else if (ifSegment is Statement_If_ElseIf partElseif)
                {
                    AddInstruction(Opcode.COMMENT, "elseif (...) {");

                    AddInstruction(Opcode.COMMENT, "ELSEIF Condition");
                    GenerateCodeForStatement(partElseif.Condition);
                    AddInstruction(Opcode.COMMENT, "ELSEIF Jump to Next");
                    int jumpNextInstruction = compiledCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    variableCountStack.Push(CompileVariables(partElseif, false));

                    AddInstruction(Opcode.COMMENT, "ELSEIF Statements");
                    for (int i = 0; i < partElseif.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElseif.Statements[i]);
                    }

                    ClearVariables(variableCountStack.Pop());

                    AddInstruction(Opcode.COMMENT, "IF Jump to End");
                    jumpOutInstructions.Add(compiledCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddInstruction(Opcode.COMMENT, "}");

                    compiledCode[jumpNextInstruction].parameter = compiledCode.Count - jumpNextInstruction;
                }
                else if (ifSegment is Statement_If_Else partElse)
                {
                    AddInstruction(Opcode.COMMENT, "else {");

                    AddInstruction(Opcode.COMMENT, "ELSE Statements");

                    variableCountStack.Push(CompileVariables(partElse, false));

                    for (int i = 0; i < partElse.Statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElse.Statements[i]);
                    }

                    ClearVariables(variableCountStack.Pop());

                    AddInstruction(Opcode.COMMENT, "}");
                }
            }

            foreach (var item in jumpOutInstructions)
            {
                compiledCode[item].parameter = compiledCode.Count - item;
            }
        }
        void GenerateCodeForStatement(Statement_NewStruct newObject)
        {
            newObject.StructName.Analysis.CompilerReached = true;

            if (GetCompiledStruct(newObject, out var structDefinition))
            {
                newObject.StructName.Analysis.Reference = new TokenAnalysis.RefStruct(structDefinition);

                if (structDefinition.IsBuiltin)
                {
                    AddInstruction(Opcode.PUSH_VALUE, structDefinition.CreateBuiltinStructCallback());
                }
                else
                {
                    Dictionary<string, DataItem> fields = new();
                    foreach (ParameterDefinition structDefFieldDefinition in structDefinition.Fields)
                    {
                        fields.Add(structDefFieldDefinition.name.text, new DataItem(GenerateInitialValue(structDefFieldDefinition.type), null));
                    }
                    AddInstruction(Opcode.PUSH_VALUE, new Struct(fields, structDefinition.FullName));
                }
            }
            else if (GetCompiledClass(newObject, out var classDefinition))
            {
                newObject.StructName.Analysis.Reference = new TokenAnalysis.RefClass(classDefinition);

                Dictionary<string, DataItem> fields = new();
                foreach (ParameterDefinition classDefFieldDefinition in classDefinition.Fields)
                {
                    fields.Add(classDefFieldDefinition.name.text, new DataItem(classDefFieldDefinition.type, null));
                }
                AddInstruction(Opcode.PUSH_VALUE, new Struct(fields, classDefinition.FullName));
            }
            else
            {
                throw new CompilerException("Unknown struct/class '" + newObject.StructName.text + "'", newObject.StructName, CurrentFile);
            }
        }
        void GenerateCodeForStatement(Statement_Field field)
        {
            field.FieldName.Analysis.CompilerReached = true;
            field.FieldName.Analysis.SubSubtype = TokenSubSubtype.FieldName;

            {
                var type = FindStatementType(field);
                field.FieldName.Analysis.Reference = new TokenAnalysis.RefField(type.Name, field.FieldName.text, type.Struct?.FullName, type.Struct?.FilePath);
            }

            GenerateCodeForStatement(field.PrevStatement);
            AddInstruction(Opcode.PUSH_VALUE, field.FieldName.text);
            AddInstruction(Opcode.LOAD_FIELD, AddressingMode.POP);
        }
        void GenerateCodeForStatement(Statement_Index indexStatement)
        {
            GenerateCodeForStatement(indexStatement.PrevStatement);
            if (indexStatement.Expression == null)
            { throw new CompilerException($"Index expression for indexer is missing", indexStatement.TotalPosition(), CurrentFile); }
            GenerateCodeForStatement(indexStatement.Expression);
            AddInstruction(Opcode.LIST_INDEX);
        }
        void GenerateCodeForStatement(Statement_ListValue listValue)
        {
            BuiltinType? listType = null;
            for (int i = 0; i < listValue.Size; i++)
            {
                var itemType = FindStatementType(listValue.Values[i]);
                BuiltinType itemTypeName;
                if (itemType.IsList)
                { itemTypeName = BuiltinType.LISTOF; }
                else if (TypeMap.TryGetValue(itemType.Name, out var itemTypeName2))
                { itemTypeName = itemTypeName2.Convert(); }
                else
                { throw new CompilerException($"Unknown type {listType}", listValue.Values[i].TotalPosition(), CurrentFile); }

                if (i == 0)
                { listType = itemTypeName; }

                if (itemTypeName != listType)
                { throw new CompilerException($"Wrong type {itemType}. Expected {listType}", listValue.Values[i].TotalPosition(), CurrentFile); }
            }
            if (listType == null)
            { throw new CompilerException($"Failed to get the type of the list", listValue, CurrentFile); }
            DataItem newList = new(new DataItem.List(listType.Value.Convert()), null);
            AddInstruction(Opcode.COMMENT, "Generate List {");
            AddInstruction(Opcode.PUSH_VALUE, newList);
            for (int i = 0; i < listValue.Size; i++)
            {
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -1);
                GenerateCodeForStatement(listValue.Values[i]);
                AddInstruction(Opcode.LIST_PUSH_ITEM);
            }
            AddInstruction(Opcode.COMMENT, "}");
        }

        void GenerateCodeForStatement(Statement st)
        {
            DebugInfo debugInfo = new()
            {
                InstructionStart = compiledCode.Count,
                InstructionEnd = compiledCode.Count,
            };
            if (st is StatementParent statementParent)
            { variableCountStack.Push(CompileVariables(statementParent, false)); }

            if (st is Statement_ListValue listValue)
            { GenerateCodeForStatement(listValue); }
            else if (st is Statement_NewVariable newVariable)
            { GenerateCodeForStatement(newVariable); }
            else if (st is Statement_FunctionCall functionCall)
            { GenerateCodeForStatement(functionCall); }
            else if (st is Statement_Operator @operator)
            { GenerateCodeForStatement(@operator); }
            else if (st is Statement_Literal literal)
            { GenerateCodeForStatement(literal); }
            else if (st is Statement_Variable variable)
            { GenerateCodeForStatement(variable); }
            else if (st is Statement_WhileLoop whileLoop)
            { GenerateCodeForStatement(whileLoop); }
            else if (st is Statement_ForLoop forLoop)
            { GenerateCodeForStatement(forLoop); }
            else if (st is Statement_If @if)
            { GenerateCodeForStatement(@if); }
            else if (st is Statement_NewStruct newStruct)
            { GenerateCodeForStatement(newStruct); }
            else if (st is Statement_Index indexStatement)
            { GenerateCodeForStatement(indexStatement); }
            else if (st is Statement_Field field)
            { GenerateCodeForStatement(field); }
            else
            {
                Output.Debug.Debug.Log("[Compiler]: Unimplemented statement " + st.GetType().Name);
            }

            if (st is StatementParent)
            { ClearVariables(variableCountStack.Pop()); }

            debugInfo.InstructionEnd = compiledCode.Count - 1;
            debugInfo.Position = st.TotalPosition();
            GeneratedDebugInfo.Add(debugInfo);
        }

        int CompileVariables(StatementParent statement, bool isGlobal, bool addComments = true)
        {
            if (addComments) AddInstruction(Opcode.COMMENT, "Variables");
            int variableCount = 0;
            foreach (var s in statement.Statements)
            {
                GenerateCodeForVariable(s, isGlobal, out int newVariableCount);
                variableCount += newVariableCount;
            }
            return variableCount;
        }
        void ClearVariables(int variableCount, bool addComments = true)
        {
            if (variableCount > 0)
            {
                if (addComments) AddInstruction(Opcode.COMMENT, "Clear variables");
                compiledVariables.Remove(compiledVariables.Last().Key);
                for (int x = 0; x < variableCount; x++)
                {
                    AddInstruction(Opcode.POP_VALUE);
                }
            }
        }

        void GenerateCodeForValueSetter(Statement statementToSet, Statement value)
        {
            if (statementToSet is Statement_Variable variable)
            { GenerateCodeForValueSetter(variable, value); }
            else if (statementToSet is Statement_Field field)
            { GenerateCodeForValueSetter(field, value); }
            else if (statementToSet is Statement_Index index)
            { GenerateCodeForValueSetter(index, value); }
            else
            { throw new CompilerException("Unexpected statement", statementToSet.TotalPosition(), CurrentFile); }
        }

        void GenerateCodeForValueSetter(Statement_Variable statementToSet, Statement value)
        {
            statementToSet.VariableName.Analysis.CompilerReached = true;

            if (GetParameter(statementToSet.VariableName.text, out Parameter parameter))
            {
                statementToSet.VariableName.Analysis.SubSubtype = TokenSubSubtype.ParameterName;
                statementToSet.VariableName.Analysis.Reference = new TokenAnalysis.RefParameter(parameter.type);

                GenerateCodeForStatement(value);
                AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, parameter.RealIndex);
            }
            else if (GetCompiledVariable(statementToSet.VariableName.text, out CompiledVariable valueMemoryIndex))
            {
                statementToSet.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                statementToSet.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(valueMemoryIndex.Declaration, valueMemoryIndex.IsGlobal);

                GenerateCodeForStatement(value);
                if (valueMemoryIndex.IsStoredInHEAP)
                { AddInstruction(Opcode.HEAP_SET, valueMemoryIndex.offset); }
                else
                { AddInstruction(Opcode.STORE_VALUE, valueMemoryIndex.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, valueMemoryIndex.offset); }
            }
            else
            {
                throw new CompilerException("Unknown variable '" + statementToSet.VariableName.text + "'", statementToSet.VariableName, CurrentFile);
            }
        }
        void GenerateCodeForValueSetter(Statement_Field statementToSet, Statement value)
        {
            statementToSet.FieldName.Analysis.CompilerReached = true;
            statementToSet.FieldName.Analysis.SubSubtype = TokenSubSubtype.FieldName;

            {
                var type = FindStatementType(statementToSet);
                statementToSet.FieldName.Analysis.Reference = new TokenAnalysis.RefField(type.Name, statementToSet.FieldName.text, type.Struct?.FullName, type.Struct?.FilePath);
            }

            if (statementToSet.PrevStatement is Statement_Variable variable1)
            {
                variable1.VariableName.Analysis.CompilerReached = true;

                if (GetParameter(variable1.VariableName.text, out Parameter parameter))
                {
                    variable1.VariableName.Analysis.SubSubtype = TokenSubSubtype.ParameterName;
                    variable1.VariableName.Analysis.Reference = new TokenAnalysis.RefParameter(parameter.type);

                    GenerateCodeForStatement(value);
                    AddInstruction(Opcode.PUSH_VALUE, statementToSet.FieldName.text);
                    AddInstruction(Opcode.STORE_FIELD, AddressingMode.BASEPOINTER_RELATIVE, parameter.RealIndex);
                }
                else if (GetCompiledVariable(variable1.VariableName.text, out CompiledVariable valueMemoryIndex))
                {
                    variable1.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                    variable1.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(valueMemoryIndex.Declaration, valueMemoryIndex.IsGlobal);

                    if (valueMemoryIndex.IsStoredInHEAP)
                    {
                        AddInstruction(Opcode.HEAP_GET, valueMemoryIndex.offset);
                        AddInstruction(Opcode.COPY_VALUE_RECURSIVE);
                        GenerateCodeForStatement(value);
                        AddInstruction(Opcode.PUSH_VALUE, statementToSet.FieldName.text);
                        AddInstruction(Opcode.STORE_FIELD, AddressingMode.POP);
                        AddInstruction(Opcode.HEAP_SET, valueMemoryIndex.offset);
                    }
                    else
                    {
                        GenerateCodeForStatement(value);
                        AddInstruction(Opcode.PUSH_VALUE, statementToSet.FieldName.text);
                        AddInstruction(Opcode.STORE_FIELD, valueMemoryIndex.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, valueMemoryIndex.offset);
                    }
                }
                else
                {
                    throw new CompilerException("Unknown variable '" + variable1.VariableName.text + "'", variable1.VariableName, CurrentFile);
                }
            }
            else
            { errors.Add(new Error($"Not implemented", statementToSet.TotalPosition(), CurrentFile)); }

        }
        void GenerateCodeForValueSetter(Statement_Index statementToSet, Statement value)
        {
            if (statementToSet.PrevStatement is Statement_Variable variable1)
            {
                variable1.VariableName.Analysis.CompilerReached = true;

                if (GetCompiledVariable(variable1.VariableName.text, out CompiledVariable valueMemoryIndex))
                {
                    variable1.VariableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                    variable1.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(valueMemoryIndex.Declaration, valueMemoryIndex.IsGlobal);

                    GenerateCodeForStatement(value);
                    AddInstruction(Opcode.LOAD_VALUE, valueMemoryIndex.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, valueMemoryIndex.offset);
                    GenerateCodeForStatement(statementToSet.Expression);
                    AddInstruction(Opcode.LIST_SET_ITEM);

                    AddInstruction(Opcode.STORE_VALUE, valueMemoryIndex.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE, valueMemoryIndex.offset);
                }
                else
                {
                    throw new CompilerException("Unknown variable '" + variable1.VariableName.text + "'", variable1.VariableName, CurrentFile);
                }
            }
            { errors.Add(new Error($"Not implemented", statementToSet.TotalPosition(), CurrentFile)); }
        }

        #endregion

        #region GenerateCodeFor...

        void GenerateCodeForGlobalVariable(Statement st, out int globalVariablesAdded)
        {
            int variableCount = 0;

            if (st is Statement_NewVariable newVariable)
            {
                newVariable.VariableName.Analysis.CompilerReached = true;
                newVariable.Type.Analysis.CompilerReached = true;

                if (Keywords.Contains(newVariable.VariableName.text))
                { throw new CompilerException($"Illegal variable name '{newVariable.VariableName.text}'", newVariable.VariableName, newVariable.FilePath); }

                switch (newVariable.Type.typeName)
                {
                    case BuiltinType.INT:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue1 = 0;
                        if (newVariable.Type.IsList)
                        {
                            initialValue1 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.typeName == newVariable.Type.typeName)
                                { initialValue1 = int.Parse(literal.Value); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledVariables.Add(newVariable.VariableName.text, new CompiledVariable(variableCount, newVariable.Type.typeName, newVariable.Type.ListOf, true, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue1, "var." + newVariable.VariableName.text);
                        variableCount++;
                        break;
                    case BuiltinType.BYTE:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue5 = 0;
                        if (newVariable.Type.IsList)
                        {
                            initialValue5 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.typeName == newVariable.Type.typeName)
                                { initialValue5 = byte.Parse(literal.Value); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledVariables.Add(newVariable.VariableName.text, new CompiledVariable(variableCount, newVariable.Type.typeName, newVariable.Type.ListOf, true, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue5, "var." + newVariable.VariableName.text);
                        variableCount++;
                        break;
                    case BuiltinType.FLOAT:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue2 = 0;
                        if (newVariable.Type.IsList)
                        {
                            initialValue2 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.typeName == BuiltinType.FLOAT || literal.Type.typeName == IngameCoding.BBCode.BuiltinType.INT)
                                { initialValue2 = float.Parse(literal.Value); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledVariables.Add(newVariable.VariableName.text, new CompiledVariable(variableCount, newVariable.Type.typeName, newVariable.Type.ListOf, true, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue2, "var." + newVariable.VariableName.text);
                        variableCount++;
                        break;
                    case BuiltinType.STRING:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue3 = "";
                        if (newVariable.Type.IsList)
                        {
                            initialValue3 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.typeName == BuiltinType.STRING)
                                { initialValue3 = literal.Value; }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledVariables.Add(newVariable.VariableName.text, new CompiledVariable(variableCount, newVariable.Type.typeName, newVariable.Type.ListOf, true, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue3, "var." + newVariable.VariableName.text);
                        variableCount++;
                        break;
                    case BuiltinType.BOOLEAN:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue4 = false;
                        if (newVariable.Type.IsList)
                        {
                            initialValue4 = GenerateInitialValue(newVariable.Type);
                        }
                        else if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                if (literal.Type.typeName == newVariable.Type.typeName)
                                { initialValue4 = bool.Parse(literal.Value); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledVariables.Add(newVariable.VariableName.text, new CompiledVariable(variableCount, newVariable.Type.typeName, newVariable.Type.ListOf, true, newVariable));
                        AddInstruction(Opcode.PUSH_VALUE, initialValue4, "var." + newVariable.VariableName.text);
                        variableCount++;
                        break;
                    case BuiltinType.STRUCT:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Struct;

                        if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_NewStruct newStruct)
                            {
                                if (newStruct.StructName.text == newVariable.Type.text)
                                { GenerateCodeForStatement(newStruct); }
                                else
                                { throw new CompilerException("Can't cast " + newStruct.StructName.text + " to " + newVariable.Type.text, newStruct.TotalPosition(), newVariable.FilePath); }
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledVariables.Add(newVariable.VariableName.text, new CompiledVariable(variableCount, newVariable.Type.text, newVariable.Type.ListOf, true, newVariable));
                        variableCount++;
                        break;
                    case BuiltinType.AUTO:
                        newVariable.Type.Analysis.SubSubtype = TokenSubSubtype.Keyword;

                        if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                newVariable.Type.typeName = literal.Type.typeName;
                            }
                            else if (newVariable.InitialValue is Statement_NewStruct newStruct)
                            {
                                newVariable.Type.typeName = BuiltinType.STRUCT;
                                newVariable.Type.text = newStruct.StructName.text;
                            }
                        }
                        newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        if (newVariable.Type.typeName == BuiltinType.AUTO)
                        { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }
                        GenerateCodeForGlobalVariable(newVariable, out _);
                        variableCount++;
                        break;
                }
            }

            globalVariablesAdded = variableCount;
        }

        void GenerateCodeForVariable(Statement_NewVariable newVariable, bool isGlobal)
        {
            newVariable.VariableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false);

            if (newVariable.Type.typeName == BuiltinType.AUTO)
            {
                if (newVariable.InitialValue != null)
                {
                    if (newVariable.InitialValue is Statement_Literal literal)
                    {
                        newVariable.Type.typeName = literal.Type.typeName;
                    }
                    else if (newVariable.InitialValue is Statement_NewStruct newStruct)
                    {
                        newVariable.Type.typeName = BuiltinType.STRUCT;
                        newVariable.Type.text = newStruct.StructName.text;
                    }
                    else
                    {
                        try
                        {
                            var initialTypeRaw = FindStatementType(newVariable.InitialValue);
                            var initialType = Parser.ParseType(initialTypeRaw.Name);

                            newVariable.Type.typeName = initialType.typeName;
                            newVariable.Type.ListOf = initialType.ListOf;
                            newVariable.Type.text = initialType.text;

                            GenerateCodeForVariable(newVariable, isGlobal);
                            return;
                        }
                        catch (FormatException) { throw; }
                        catch (System.Exception) { throw new NotImplementedException(); }
                    }
                }
                else
                { throw new CompilerException($"Initial value for 'var' variable declaration is requied", newVariable.Type); }

                if (newVariable.Type.typeName == BuiltinType.AUTO)
                { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }

                GenerateCodeForVariable(newVariable, isGlobal);
                return;
            }

            var initialValue = GenerateInitialValue(newVariable.Type);

            compiledVariables.Add(newVariable.VariableName.text, GetVariableInfo(newVariable, compiledVariables.Count, isGlobal));
            AddInstruction(Opcode.PUSH_VALUE, initialValue, "var." + newVariable.VariableName.text);
        }
        void GenerateCodeForVariable(Statement st, bool isGlobal, out int variablesAdded)
        {
            variablesAdded = 0;
            if (st is Statement_NewVariable newVariable)
            {
                variablesAdded = 1;
                GenerateCodeForVariable(newVariable, isGlobal);
            }
        }
        void GenerateCodeForVariable(Statement[] sts, bool isGlobal, out int variablesAdded)
        {
            variablesAdded = 0;
            for (int i = 0; i < sts.Length; i++)
            {
                GenerateCodeForVariable(sts[i], isGlobal, out var x);
                variablesAdded += x;
            }
        }
        void GenerateCodeForVariable(List<Statement> sts, bool isGlobal, out int variablesAdded) => GenerateCodeForVariable(sts.ToArray(), isGlobal, out variablesAdded);

        void ClearLocalVariables()
        {
            for (int i = compiledVariables.Count - 1; i >= 0; i--)
            {
                var key = compiledVariables.ElementAt(i).Key;
                if (compiledVariables[key].IsGlobal) continue;
                compiledVariables.Remove(key);
            }
        }

        void GenerateCodeForFunction(KeyValuePair<string, CompiledFunction> function)
        {
            function.Value.Name.Analysis.CompilerReached = true;
            function.Value.Type.Analysis.CompilerReached = true;
            CurrentFunction_ForRecursionProtection = function.Value.ID();

            if (Keywords.Contains(function.Value.Name.text))
            { throw new CompilerException($"Illegal function name '{function.Value.Name.text}'", function.Value.Name, function.Value.FilePath); }

            function.Value.Name.Analysis.SubSubtype = TokenSubSubtype.FunctionName;

            if (function.Value.IsBuiltin) return;

            parameters.Clear();
            ClearLocalVariables();
            returnInstructions.Clear();

            // Compile parameters
            int paramIndex = 0;
            foreach (ParameterDefinition parameter in function.Value.Parameters)
            {
                paramIndex++;
                parameter.name.Analysis.CompilerReached = true;
                parameter.type.Analysis.CompilerReached = true;
                parameters.Add(parameter.name.text, new Parameter(paramIndex, parameter.name.text, function.Value.Parameters.Count, parameter.type.ToString()));
            }

            CurrentNamespace = function.Value.NamespacePath;
            CurrentFile = function.Value.FilePath;

            AddInstruction(Opcode.CS_PUSH, $"{function.Value.ReadableID()};{CurrentFile};{compiledCode.Count};{function.Value.Name.Position.Start.Line}");

            // Search for variables
            AddInstruction(Opcode.COMMENT, "Variables");
            GenerateCodeForVariable(function.Value.Statements, false, out int variableCount);
            variableCountStack.Push(variableCount);
            if (variableCount == 0 && AddCommentsToCode)
            { compiledCode.RemoveAt(compiledCode.Count - 1); }

            // Compile statements
            if (function.Value.Statements.Count > 0)
            {
                AddInstruction(Opcode.COMMENT, "Statements");
                foreach (Statement statement in function.Value.Statements)
                {
                    GenerateCodeForStatement(statement);
                }
            }

            CurrentFile = null;
            CurrentNamespace = Array.Empty<string>();

            int cleanupCodeOffset = compiledCode.Count;

            foreach (var returnCommandJumpInstructionIndex in returnInstructions)
            {
                compiledCode[returnCommandJumpInstructionIndex].parameter = cleanupCodeOffset - returnCommandJumpInstructionIndex;
            }

            // Clear variables
            variableCountStack.Pop();
            if (variableCount > 0)
            {
                AddInstruction(Opcode.COMMENT, "Clear variables");
                for (int x = 0; x < variableCount; x++)
                {
                    AddInstruction(Opcode.POP_VALUE);
                }
            }

            AddInstruction(Opcode.CS_POP);

            AddInstruction(Opcode.COMMENT, "Return");
            AddInstruction(Opcode.RETURN);

            parameters.Clear();
            ClearLocalVariables();
            returnInstructions.Clear();

            CurrentFunction_ForRecursionProtection = null;
        }

        void GenerateCodeForStruct(KeyValuePair<string, StructDefinition> @struct, Dictionary<string, Func<IStruct>> builtinStructs)
        {
            CurrentFile = @struct.Value.FilePath;
            CurrentNamespace = @struct.Value.NamespacePath;

            @struct.Value.Name.Analysis.CompilerReached = true;

            if (Keywords.Contains(@struct.Key))
            { throw new CompilerException($"Illegal struct name '{@struct.Value.FullName}'", @struct.Value.Name, CurrentFile); }

            @struct.Value.Name.Analysis.SubSubtype = TokenSubSubtype.Struct;

            if (compiledStructs.ContainsKey(@struct.Value.FullName))
            { throw new CompilerException($"Struct with name '{@struct.Value.FullName}' already exist", @struct.Value.Name, CurrentFile); }

            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in @struct.Value.Attributes)
            {
                attribute.Name.Analysis.CompilerReached = true;
                attribute.Name.Analysis.SubSubtype = TokenSubSubtype.Attribute;

                AttributeValues newAttribute = new()
                { parameters = new() };

                if (attribute.Parameters != null)
                {
                    foreach (object parameter in attribute.Parameters)
                    {
                        newAttribute.parameters.Add(new Literal(parameter));
                    }
                }

                attributes.Add(attribute.Name.text, newAttribute);
            }

#if false
            if (attributes.TryGetValue("Builtin", out var attributeBuiltin))
            {
                if (attributeBuiltin.parameters.Count != 1)
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.NameToken, CurrentFile); }
                if (attributeBuiltin.TryGetValue(0, out string paramBuiltinName))
                {
                    foreach (var builtinStruct in builtinStructs)
                    {
                        if (builtinStruct.Key.ToLower() == paramBuiltinName.ToLower())
                        {
                            this.compiledStructs.Add(@struct.Key,
                                new CompiledStruct(attributes, @struct.Value)
                                { CreateBuiltinStructCallback = builtinStruct.Value });

                            foreach (var method in @struct.Value.Methods)
                            {
                                if (compiledFunctions.ContainsKey(method.Key))
                                { throw new CompilerException($"Function with name '{method.Key}' already defined", method.Value.Name, CurrentFile); }

                                var methodInfo = GetFunctionInfo(method, true);

                                this.compiledFunctions.Add(method.Value.FullName, methodInfo);

                                AddInstruction(Opcode.COMMENT, @struct.Value.FullName + "." + method.Value.FullName + ((method.Value.Parameters.Count > 0) ? "(...)" : "()") + " {");
                                GenerateCodeForFunction(method, true);
                                AddInstruction(Opcode.COMMENT, "}");
                                this.compiledStructs.Last().Value.CompiledMethods.Add(method.Key, methodInfo);
                            }

                            CurrentFile = null;
                            return;
                        }
                    }
                    throw new CompilerException("Builtin struct '" + paramBuiltinName.ToLower() + "' not found", attributeBuiltin.NameToken, CurrentFile);
                }
                else
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.NameToken, CurrentFile); }
            }
#endif

            foreach (var field in @struct.Value.Fields)
            {
                field.name.Analysis.CompilerReached = true;
                field.type.Analysis.CompilerReached = true;
            }

            this.compiledStructs.Add(@struct.Value.FullName, new CompiledStruct(attributes, @struct.Value));

            CurrentFile = null;
            CurrentNamespace = Array.Empty<string>();
        }

        void GenerateCodeForClass(KeyValuePair<string, ClassDefinition> @class)
        {
            CurrentFile = @class.Value.FilePath;
            CurrentNamespace = @class.Value.NamespacePath;

            @class.Value.Name.Analysis.CompilerReached = true;

            if (Keywords.Contains(@class.Key))
            { throw new CompilerException($"Illegal class name '{@class.Value.FullName}'", @class.Value.Name, CurrentFile); }

            @class.Value.Name.Analysis.SubSubtype = TokenSubSubtype.Struct;

            if (compiledClasses.ContainsKey(@class.Value.FullName))
            { throw new CompilerException($"Class with name '{@class.Value.FullName}' already exist", @class.Value.Name, CurrentFile); }

            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in @class.Value.Attributes)
            {
                attribute.Name.Analysis.CompilerReached = true;
                attribute.Name.Analysis.SubSubtype = TokenSubSubtype.Attribute;

                AttributeValues newAttribute = new()
                { parameters = new() };

                if (attribute.Parameters != null)
                {
                    foreach (object parameter in attribute.Parameters)
                    {
                        newAttribute.parameters.Add(new Literal(parameter));
                    }
                }

                attributes.Add(attribute.Name.text, newAttribute);
            }

            foreach (var field in @class.Value.Fields)
            {
                field.name.Analysis.CompilerReached = true;
                field.type.Analysis.CompilerReached = true;
            }

            this.compiledClasses.Add(@class.Key, new CompiledClass(attributes, @class.Value));

            CurrentFile = null;
            CurrentNamespace = Array.Empty<string>();
        }

        #endregion

        CompiledVariable GetVariableInfo(Statement_NewVariable newVariable, int offset, bool isGlobal)
        {
            newVariable.VariableName.Analysis.CompilerReached = true;
            newVariable.Type.Analysis.CompilerReached = true;

            if (Keywords.Contains(newVariable.VariableName.text))
            { throw new CompilerException($"Illegal variable name '{newVariable.VariableName.text}'", newVariable.VariableName, CurrentFile); }

            bool storedInHeap = false;
            if (GetCompiledClass(newVariable.Type.text, out _))
            { storedInHeap = true; }

            if (newVariable.Type.typeName == BuiltinType.STRUCT)
            {
                return new CompiledVariable(offset, newVariable.Type.text, newVariable.Type.ListOf, isGlobal, newVariable) { IsStoredInHEAP = storedInHeap };
            }
            return new CompiledVariable(offset, newVariable.Type.typeName, newVariable.Type.ListOf, isGlobal, newVariable) { IsStoredInHEAP = storedInHeap };
        }

        #region Result Structs

        public struct CodeGeneratorResult
        {
            public Instruction[] compiledCode;
            internal DebugInfo[] DebugInfo;

            public Dictionary<string, CompiledFunction> compiledFunctions;
            public Dictionary<string, CompiledStruct> compiledStructs;

            public int clearGlobalVariablesInstruction;
            public int setGlobalVariablesInstruction;
        }

        #endregion

        CompiledFunction GetFunctionInfo(KeyValuePair<string, FunctionDefinition> function)
        {
            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in function.Value.Attributes)
            {
                attribute.Name.Analysis.CompilerReached = true;
                attribute.Name.Analysis.SubSubtype = TokenSubSubtype.Attribute;

                AttributeValues newAttribute = new()
                {
                    parameters = new(),
                    NameToken = attribute.Name,
                };

                if (attribute.Parameters != null)
                {
                    foreach (var parameter in attribute.Parameters)
                    {
                        newAttribute.parameters.Add(new Literal(parameter));
                    }
                }

                attributes.Add(attribute.Name.text, newAttribute);
            }

            if (attributes.TryGetValue("Builtin", out var attributeBuiltin))
            {
                if (attributeBuiltin.parameters.Count != 1)
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.NameToken, function.Value.FilePath); }
                if (attributeBuiltin.TryGetValue(0, out string paramBuiltinName))
                {
                    foreach (var builtinFunction in builtinFunctions)
                    {
                        if (builtinFunction.Key.ToLower() == paramBuiltinName.ToLower())
                        {
                            if (builtinFunction.Value.ParameterCount != function.Value.Parameters.Count)
                            { throw new CompilerException("Wrong number of parameters passed to builtin function '" + builtinFunction.Key + "'", function.Value.Name, function.Value.FilePath); }
                            if (builtinFunction.Value.ReturnSomething != (function.Value.Type.typeName != BuiltinType.VOID))
                            { throw new CompilerException("Wrong type definied for builtin function '" + builtinFunction.Key + "'", function.Value.Type, function.Value.FilePath); }

                            for (int i = 0; i < builtinFunction.Value.ParameterTypes.Length; i++)
                            {
                                if (builtinFunction.Value.ParameterTypes[i].typeName == BuiltinType.ANY) continue;

                                if (builtinFunction.Value.ParameterTypes[i].typeName != function.Value.Parameters[i].type.typeName)
                                { throw new CompilerException("Wrong type of parameter passed to builtin function '" + builtinFunction.Key + $"'. Parameter index: {i} Requied type: {builtinFunction.Value.ParameterTypes[i].typeName.ToString().ToLower()} Passed: {function.Value.Parameters[i].type.typeName.ToString().ToLower()}", function.Value.Parameters[i].type, function.Value.FilePath); }
                            }

                            return new CompiledFunction(function.Value)
                            {
                                ParameterTypes = builtinFunction.Value.ParameterTypes,
                                CompiledAttributes = attributes,
                            };
                        }
                    }

                    errors.Add(new Error("Builtin function '" + paramBuiltinName.ToLower() + "' not found", attributeBuiltin.NameToken, function.Value.FilePath));
                    return new CompiledFunction(
                        function.Value.Parameters.ToArray(),
                        (function.Value.Parameters.Count > 0) && function.Value.Parameters.First().withThisKeyword,
                        function.Value
                        )
                    { CompiledAttributes = attributes };
                }
                else
                { throw new CompilerException("Attribute 'Builtin' requies 1 string parameter", attributeBuiltin.NameToken, function.Value.FilePath); }
            }

            return new CompiledFunction(
                function.Value.Parameters.ToArray(),
                (function.Value.Parameters.Count > 0) && function.Value.Parameters.First().withThisKeyword,
                function.Value
                )
            { CompiledAttributes = attributes };
        }

        int AnalyzeFunctions(Dictionary<string, CompiledFunction> functions, Action<string, Output.LogType> printCallback = null)
        {
            printCallback?.Invoke($"  Remove unused functions ...", Output.LogType.Debug);

            // Remove unused functions
            {
                FunctionDefinition currentFunction = null;

                void AnalyzeNewVariable(Statement_NewVariable newVariable)
                {
                    if (newVariable.Type.typeName == BuiltinType.AUTO)
                    {
                        if (newVariable.InitialValue != null)
                        {
                            if (newVariable.InitialValue is Statement_Literal literal)
                            {
                                newVariable.Type.typeName = literal.Type.typeName;
                            }
                            else if (newVariable.InitialValue is Statement_NewStruct newStruct)
                            {
                                newVariable.Type.typeName = BuiltinType.STRUCT;
                                newVariable.Type.text = newStruct.StructName.text;
                            }
                            else
                            {
                                try
                                {
                                    var initialTypeRaw = FindStatementType(newVariable.InitialValue);
                                    var initialType = Parser.ParseType(initialTypeRaw.Name);

                                    newVariable.Type.typeName = initialType.typeName;
                                    newVariable.Type.ListOf = initialType.ListOf;
                                    newVariable.Type.text = initialType.text;
                                }
                                catch (FormatException) { throw; }
                                catch (System.Exception) { throw new NotImplementedException(); }
                            }
                        }
                        else
                        { throw new CompilerException($"Initial value for 'var' variable declaration is requied", newVariable.Type); }

                        if (newVariable.Type.typeName == BuiltinType.AUTO)
                        { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }
                    }
                    this.compiledVariables.Add(newVariable.VariableName.text, GetVariableInfo(newVariable, -1, false));
                }
                void AnalyzeStatements(List<Statement> statements)
                {
                    int variablesAdded = 0;
                    foreach (var st in statements)
                    {
                        if (st is Statement_NewVariable newVar)
                        {
                            AnalyzeNewVariable(newVar);
                            variablesAdded++;
                        }
                        else if (st is Statement_ForLoop forLoop)
                        {
                            AnalyzeNewVariable(forLoop.VariableDeclaration);
                            variablesAdded++;
                        }
                    }

                    foreach (var st in statements)
                    {
                        AnalyzeStatement(st);
                        if (st is StatementParent pr)
                        {
                            AnalyzeStatements(pr.Statements);
                        }
                    }

                    for (int i = 0; i < variablesAdded; i++)
                    {
                        this.compiledVariables.Remove(this.compiledVariables.ElementAt(this.compiledVariables.Count - 1).Key);
                    }
                }

                void AnalyzeStatement(Statement st)
                {
                    if (st is Statement_ForLoop st0)
                    {
                        AnalyzeStatement(st0.VariableDeclaration);
                        AnalyzeStatement(st0.Condition);
                        AnalyzeStatement(st0.Expression);
                    }
                    else if (st is Statement_If st1)
                    {
                        foreach (var st2 in st1.Parts)
                        { AnalyzeStatement(st2); }
                    }
                    else if (st is Statement_If_If st2)
                    {
                        AnalyzeStatement(st2.Condition);
                        AnalyzeStatements(st2.Statements);
                    }
                    else if (st is Statement_If_ElseIf st3)
                    {
                        AnalyzeStatement(st3.Condition);
                        AnalyzeStatements(st3.Statements);
                    }
                    else if (st is Statement_If_Else st3a)
                    {
                        AnalyzeStatements(st3a.Statements);
                    }
                    else if (st is Statement_Index st4)
                    {
                        AnalyzeStatement(st4.Expression);
                    }
                    else if (st is Statement_NewVariable st5)
                    {
                        if (st5.InitialValue != null) AnalyzeStatement(st5.InitialValue);
                    }
                    else if (st is Statement_Operator st6)
                    {
                        if (st6.Left != null) AnalyzeStatement(st6.Left);
                        if (st6.Right != null) AnalyzeStatement(st6.Right);
                    }
                    else if (st is Statement_WhileLoop st7)
                    {
                        AnalyzeStatement(st7.Condition);
                    }
                    else if (st is Statement_FunctionCall st8)
                    {
                        foreach (var st9 in st8.Parameters)
                        { AnalyzeStatement(st9); }

                        if (st8.PrevStatement != null)
                        { AnalyzeStatement(st8.PrevStatement); }

                        if (!BuiltinFunctions.Contains(st8.FunctionName) && GetCompiledFunction(st8, out var cf))
                        {
                            var thisID = cf.ID();
                            var currentID = currentFunction.ID();
                            if (thisID != currentID)
                            {
                                cf.TimesUsed++;
                            }
                            cf.TimesUsedTotal++;
                        }
                    }
                    else if (st is Statement_Field st9)
                    { AnalyzeStatement(st9.PrevStatement); }
                    else if (st is Statement_Variable)
                    { }
                    else if (st is Statement_NewStruct)
                    { }
                    else if (st is Statement_Literal)
                    { }
                    else if (st is Statement_ListValue st10)
                    { AnalyzeStatements(st10.Values); }
                    else
                    { throw new NotImplementedException(); }
                }

                foreach (var f in functions)
                {
                    if (compiledFunctions.TryGetValue(f.Value.ID(), out var compiledFunction))
                    { compiledFunction.TimesUsed = 0; }
                }

                foreach (var f in functions)
                {
                    parameters.Clear();
                    foreach (ParameterDefinition parameter in f.Value.Parameters)
                    { parameters.Add(parameter.name.text, new Parameter(-1, parameter.name.text, -1, parameter.type.ToString())); }
                    CurrentFile = f.Value.FilePath;

                    currentFunction = f.Value;
                    AnalyzeStatements(f.Value.Statements);

                    CurrentFile = null;
                    parameters.Clear();
                }
            }

            printCallback?.Invoke($"   Processing ...", Output.LogType.Debug);

            int functionsRemoved = 0;

            for (int i = functions.Count - 1; i >= 0; i--)
            {
                var element = functions.ElementAt(i);

                if (!this.compiledFunctions.TryGetValue(element.Value.ID(), out var f)) continue;
                if (f.TimesUsed > 0) continue;
                foreach (var attr in f.CompiledAttributes)
                {
                    if (attr.Key == "CodeEntry") goto JumpOut;
                    if (attr.Key == "Catch") goto JumpOut;
                }

                if (CompileLevel == Compiler.CompileLevel.All) continue;
                if (CompileLevel == Compiler.CompileLevel.Exported && f.IsExport) continue;

                string readableID = element.Value.ReadableID();

                printCallback?.Invoke($"      Remove function '{readableID}' ...", Output.LogType.Debug);
                informations.Add(new Information($"Unused function '{readableID}' is not compiled", element.Value.Name, element.Value.FilePath));

                functions.Remove(element.Key);
                functionsRemoved++;

            JumpOut:;
            }

            return functionsRemoved;
        }

        internal CodeGeneratorResult GenerateCode(
            Dictionary<string, FunctionDefinition> functions,
            Dictionary<string, StructDefinition> structs,
            Dictionary<string, ClassDefinition> classes,
            Statement_HashInfo[] hashes,
            List<Statement_NewVariable> globalVariables,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            Dictionary<string, Func<IStruct>> builtinStructs,
            Compiler.CompilerSettings settings,
            Action<string, Output.LogType> printCallback = null,
            Compiler.CompileLevel level = Compiler.CompileLevel.Minimal)
        {
            BlockCodeGeneration = true;

            this.GenerateDebugInstructions = settings.GenerateDebugInstructions;
            this.AddCommentsToCode = settings.GenerateComments;
            this.compiledStructs = new();
            this.compiledClasses = new();
            this.compiledVariables = new();
            this.compiledCode = new();
            this.builtinFunctions = builtinFunctions;
            this.OptimizeCode = !settings.DontOptimize;
            this.compiledFunctions = new();
            this.GeneratedDebugInfo.Clear();
            this.CompileLevel = level;
            this.variableCountStack = new();
            this.parameters = new();
            this.returnInstructions = new();
            this.breakInstructions = new();
            this.undefinedFunctionOffsets = new();
            this.CurrentNamespace = Array.Empty<string>();

            #region Compile test built-in functions

            foreach (var hash in hashes)
            {
                switch (hash.HashName.text)
                {
                    case "bf":
                        {
                            if (hash.Parameters.Length <= 1)
                            { errors.Add(new Error($"Hash '{hash.HashName}' requies minimum 2 parameter", hash.HashName, hash.FilePath)); break; }
                            string bfName = hash.Parameters[0].Value;

                            if (builtinFunctions.ContainsKey(bfName)) break;

                            string[] bfParams = new string[hash.Parameters.Length - 1];
                            for (int i = 1; i < hash.Parameters.Length; i++)
                            { bfParams[i - 1] = hash.Parameters[i].Value; }

                            TypeToken[] parameterTypes = new TypeToken[bfParams.Length];
                            for (int i = 0; i < bfParams.Length; i++)
                            {
                                switch (bfParams[i])
                                {
                                    case "void":
                                        if (i > 0)
                                        { errors.Add(new Error($"Invalid type \"{bfParams[i]}\"", hash.Parameters[i + 1].ValueToken, hash.FilePath)); goto ExitBreak; }
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.VOID);
                                        break;
                                    case "int":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.INT);
                                        break;
                                    case "string":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.STRING);
                                        break;
                                    case "float":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.FLOAT);
                                        break;
                                    case "bool":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.BOOLEAN);
                                        break;
                                    case "byte":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.BYTE);
                                        break;
                                    case "int[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("int", BuiltinType.INT));
                                        break;
                                    case "string[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("string", BuiltinType.STRING));
                                        break;
                                    case "float[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("float", BuiltinType.FLOAT));
                                        break;
                                    case "bool[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("bool", BuiltinType.BOOLEAN));
                                        break;
                                    case "byte[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], TypeToken.CreateAnonymous("byte", BuiltinType.BYTE));
                                        break;
                                    default:
                                        errors.Add(new Error($"Unknown type \"{bfParams[i]}\"", hash.Parameters[i + 1].ValueToken, hash.FilePath));
                                        goto ExitBreak;
                                }
                            }

                            var returnType = parameterTypes[0];
                            var x = parameterTypes.ToList();
                            x.RemoveAt(0);
                            var pTypes = x.ToArray();

                            if (parameterTypes[0].typeName == BuiltinType.VOID)
                            {
                                builtinFunctions.AddBuiltinFunction(bfName, pTypes, new Action<DataItem[]>((p) =>
                                {
                                    Output.Output.Debug($"Built-in function \"{bfName}\" called with params:\n  {string.Join(", ", p)}");
                                }), false);
                            }
                            else
                            {
                                builtinFunctions.Add(bfName, new BuiltinFunction(new Action<DataItem[], BuiltinFunction>((p, self) =>
                                {
                                    Output.Output.Debug($"Built-in function \"{bfName}\" called with params:\n  {string.Join(", ", p)}");

                                    switch (returnType.typeName)
                                    {
                                        case BuiltinType.INT:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.IsList ?
                                                new DataItem.List(DataType.INT) : 0
                                            , "return value"));
                                            break;
                                        case BuiltinType.BYTE:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.IsList ?
                                                new DataItem.List(DataType.BYTE) : (byte)0
                                            , "return value"));
                                            break;
                                        case BuiltinType.FLOAT:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.IsList ?
                                                new DataItem.List(DataType.FLOAT) : 0f
                                            , "return value"));
                                            break;
                                        case BuiltinType.STRING:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.IsList ?
                                                new DataItem.List(DataType.STRING) : ""
                                            , "return value"));
                                            break;
                                        case BuiltinType.BOOLEAN:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.IsList ?
                                                new DataItem.List(DataType.BOOLEAN) : false
                                            , "return value"));
                                            break;
                                        case BuiltinType.STRUCT:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.IsList ?
                                                new DataItem.List(DataType.STRUCT) : new UnassignedStruct()
                                            , "return value"));
                                            break;
                                        case BuiltinType.ANY:
                                        case BuiltinType.VOID:
                                        case BuiltinType.AUTO:
                                        default:
                                            throw new RuntimeException($"Invalid return type \"{returnType.text}\"/{returnType.ToString().ToLower()} from built-in function \"{bfName}\"");
                                    }
                                }), bfName, pTypes, true));
                            }
                        }
                        break;
                    default:
                        warnings.Add(new Warning($"Hash '{hash.HashName}' does not exists, so this is ignored", hash.HashName, hash.FilePath));
                        break;
                }

            ExitBreak:
                continue;
            }

            #endregion

            #region Compile Classes

            BlockCodeGeneration = false;

            foreach (var @class in classes)
            { GenerateCodeForClass(@class); }

            BlockCodeGeneration = true;

            #endregion

            #region Compile Structs

            BlockCodeGeneration = false;

            foreach (var @struct in structs)
            { GenerateCodeForStruct(@struct, builtinStructs); }

            BlockCodeGeneration = true;

            #endregion

            #region Analyse Struct Fields

            BlockCodeGeneration = false;

            foreach (var @struct in structs)
            {
                CurrentFile = @struct.Value.FilePath;
                CurrentNamespace = @struct.Value.NamespacePath;

                foreach (var field in @struct.Value.Fields)
                {
                    if (compiledStructs.TryGetValue(field.type.text, out var fieldStructType))
                    {
                        field.type.Analysis.SubSubtype = TokenSubSubtype.Struct;
                        field.type.Analysis.Reference = new TokenAnalysis.RefStruct(fieldStructType);
                    }
                }

                CurrentFile = null;
                CurrentNamespace = Array.Empty<string>();
            }

            BlockCodeGeneration = true;

            #endregion

            #region Compile Functions

            foreach (var function in functions)
            {
                CurrentFile = function.Value.FilePath;
                CurrentNamespace = function.Value.NamespacePath;

                var id = function.Value.ID();

                if (this.compiledFunctions.ContainsKey(id))
                { throw new CompilerException($"Function with name '{id}' already defined", function.Value.Name, function.Value.FilePath); }

                this.compiledFunctions.Add(id, GetFunctionInfo(function));

                CurrentFile = null;
                CurrentNamespace = Array.Empty<string>();
            }

            #endregion

            #region Remove Unused Functions

            int iterations = settings.RemoveUnusedFunctionsMaxIterations;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int functionsRemoved = AnalyzeFunctions(this.compiledFunctions, printCallback);
                if (functionsRemoved == 0)
                {
                    printCallback?.Invoke($"  Deletion of unused functions is complete", Output.LogType.Debug);
                    break;
                }

                printCallback?.Invoke($"  Removed {functionsRemoved} unused functions (iteration {iteration})", Output.LogType.Debug);
            }

            #endregion

            #region Code Generation

            BlockCodeGeneration = false;

            var setGlobalVariablesInstruction = compiledCode.Count;
            AddInstruction(Opcode.COMMENT, "Global variables");
            AddInstruction(Opcode.CS_PUSH, "state: SetGlobalVariables");
            int globalVariableCount = 0;
            foreach (var globalVariable in globalVariables)
            {
                GenerateCodeForVariable(globalVariable, true);
                globalVariableCount++;
            }
            AddInstruction(Opcode.CS_POP);
            AddInstruction(Opcode.EXIT);

            foreach (var function in this.compiledFunctions)
            {
                function.Value.InstructionOffset = compiledCode.Count;

                AddInstruction(Opcode.COMMENT, function.Value.FullName + ((function.Value.Parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Value.Statements.Count > 0) ? "" : " }"));
                GenerateCodeForFunction(function);
                if (function.Value.Statements.Count > 0) AddInstruction(Opcode.COMMENT, "}");
            }

            var clearGlobalVariablesInstruction = compiledCode.Count;
            AddInstruction(Opcode.COMMENT, "Clear global variables");
            AddInstruction(Opcode.CS_PUSH, "state: DisposeGlobalVariables");
            for (int i = 0; i < globalVariableCount; i++)
            { AddInstruction(Opcode.POP_VALUE); }
            compiledVariables.Clear();
            AddInstruction(Opcode.CS_POP);
            AddInstruction(Opcode.EXIT);

            BlockCodeGeneration = true;

            #endregion

            foreach (UndefinedFunctionOffset item in undefinedFunctionOffsets)
            {
                foreach (var pair in item.currentParameters)
                { parameters.Add(pair.Key, pair.Value); }
                foreach (var pair in item.currentVariables)
                { compiledVariables.Add(pair.Key, pair.Value); }

                if (!GetCompiledFunction(item.functionCallStatement, out var function))
                {
                    string searchedID = item.functionCallStatement.TargetNamespacePathPrefix + item.functionCallStatement.FunctionName;
                    searchedID += "(";
                    for (int i = 0; i < item.functionCallStatement.Parameters.Count; i++)
                    {
                        if (i > 0) { searchedID += ", "; }

                        searchedID += FindStatementType(item.functionCallStatement.Parameters[i]);
                    }
                    searchedID += ")";
                    throw new CompilerException("Unknown function " + searchedID + "", item.functionCallStatement.Identifier, CurrentFile);
                }

                if (function.InstructionOffset == -1)
                { throw new InternalException($"Function '{function.ReadableID()}' does not have instruction offset", item.CurrentFile); }

                compiledCode[item.callInstructionIndex].parameter = function.InstructionOffset - item.callInstructionIndex;

                parameters.Clear();
                compiledVariables.Clear();
            }

            if (OptimizeCode)
            {
                int removedInstructions = 0;
                int changedInstructions = 0;
                for (int i = compiledCode.Count - 1; i >= 0; i--)
                {
                    var instruction = compiledCode[i];
                    if (instruction.opcode == Opcode.JUMP_BY || instruction.opcode == Opcode.JUMP_BY_IF_FALSE || instruction.opcode == Opcode.JUMP_BY_IF_TRUE)
                    {
                        if (instruction.parameter is int jumpBy)
                        {
                            if (jumpBy == 1)
                            {
                                List<int> indexes = new()
                                {
                                    setGlobalVariablesInstruction,
                                    clearGlobalVariablesInstruction,
                                };

                                foreach (var item in this.compiledFunctions)
                                { indexes.Add(item.Value.InstructionOffset); }

                                changedInstructions += compiledCode.RemoveInstruction(i, indexes);
                                removedInstructions++;

                                setGlobalVariablesInstruction = indexes[0];
                                clearGlobalVariablesInstruction = indexes[1];

                                for (int j = 0; j < this.compiledFunctions.Count; j++)
                                { this.compiledFunctions[this.compiledFunctions.ElementAt(j).Key].InstructionOffset = indexes[2 + j]; }
                            }
                        }
                    }
                }
                printCallback?.Invoke($"Optimalization: Removed {removedInstructions} & changed {changedInstructions} instructions", Output.LogType.Debug);
            }

            return new CodeGeneratorResult()
            {
                compiledCode = compiledCode.ToArray(),
                DebugInfo = GeneratedDebugInfo.ToArray(),

                compiledFunctions = this.compiledFunctions,
                compiledStructs = this.compiledStructs,

                clearGlobalVariablesInstruction = clearGlobalVariablesInstruction,
                setGlobalVariablesInstruction = setGlobalVariablesInstruction
            };
        }
    }
}