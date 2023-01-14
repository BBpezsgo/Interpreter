using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode.Compiler
{
    using Bytecode;

    using Core;

    using Errors;

    using Parser;
    using Parser.Statements;

    using Terminal;

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
            "void"
        };

        #region Fields

        bool BlockCodeGeneration;

        internal Dictionary<string, CompiledStruct> compiledStructs;
        internal Dictionary<string, CompiledFunction> compiledFunctions;
        internal readonly Dictionary<string, CompiledVariable> compiledVariables = new();
        internal Dictionary<string, CompiledVariable> compiledGlobalVariables;
        internal VariableStack variableCountStack = new();

        internal Dictionary<string, int> functionOffsets;

        Dictionary<string, BuiltinFunction> builtinFunctions;
        readonly Dictionary<string, Parameter> parameters = new();

        public List<Error> errors;
        public List<Warning> warnings;
        internal List<Information> informations;
        public List<Hint> hints;

        readonly List<int> returnInstructions = new();
        readonly List<List<int>> breakInstructions = new();

        bool isStructMethod;

        List<Instruction> compiledCode;

        readonly List<UndefinedFunctionOffset> undefinedFunctionOffsets = new();

        bool OptimizeCode;
        bool AddCommentsToCode = true;
        bool TrimUnreachableCode = true;
        bool GenerateDebugInstructions = true;

        int ContextPosition;
        private bool FindContext;
        Context ContextResult;

        string CurrentFile;

        #endregion

        #region Helper Functions

        bool GetCompiledVariable(string variableName, out CompiledVariable compiledVariable, out bool isGlobal)
        {
            isGlobal = false;
            if (compiledVariables.TryGetValue(variableName, out compiledVariable))
            {
                return true;
            }
            else if (compiledGlobalVariables.TryGetValue(variableName, out compiledVariable))
            {
                isGlobal = true;
                return true;
            }
            return false;
        }

        bool GetParameter(string parameterName, out Parameter parameters)
        {
            if (this.parameters.TryGetValue(parameterName, out parameters))
            {
                return true;
            }
            return false;
        }

        bool GetCompiledFunction(Statement_FunctionCall functionCallStatement, out CompiledFunction compiledFunction)
        {
            string callID = "";
            for (int i = 0; i < functionCallStatement.parameters.Count; i++)
            { callID += "," + FindStatementType(functionCallStatement.parameters[i]); }

            if (compiledFunctions.TryGetValue(functionCallStatement.FunctionName + callID, out compiledFunction))
            { return true; }

            if (compiledFunctions.TryGetValue(functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID, out compiledFunction))
            { return true; }

            string namespacePath = TryGetFunctionNamespacePath(functionCallStatement);

            if (namespacePath == "")
            {
                callID = "";
                for (int i = 0; i < functionCallStatement.MethodParameters.Length; i++)
                { callID += "," + FindStatementType(functionCallStatement.MethodParameters[i]); }

                if (compiledFunctions.TryGetValue(functionCallStatement.FunctionName + callID, out compiledFunction))
                { return true; }

                if (compiledFunctions.TryGetValue(functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID, out compiledFunction))
                { return true; }
            }

            if (compiledFunctions.TryGetValue(namespacePath + functionCallStatement.FunctionName + callID, out compiledFunction))
            {
                if (namespacePath.EndsWith("."))
                { namespacePath = namespacePath[..^1]; }
                functionCallStatement.IsMethodCall = false;
                functionCallStatement.NamespacePathPrefix = namespacePath;
                functionCallStatement.PrevStatement = null;
                return true;
            }

            if (compiledFunctions.TryGetValue(namespacePath + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID, out compiledFunction))
            {
                if (namespacePath.EndsWith("."))
                { namespacePath = namespacePath[..^1]; }
                functionCallStatement.IsMethodCall = false;
                functionCallStatement.NamespacePathPrefix = namespacePath;
                functionCallStatement.PrevStatement = null;
                return true;
            }

            if (compiledFunctions.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.FunctionName + callID, out compiledFunction))
            { return true; }

            if (compiledFunctions.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID, out compiledFunction))
            { return true; }

            return false;
        }

        string TryGetFunctionNamespacePath(Statement_FunctionCall functionCallStatement)
        {
            string[] Get(Statement statement)
            {
                if (statement is Statement_Variable s1)
                {
                    if (GetCompiledVariable(s1.variableName.text, out _, out _))
                    {
                        return null;
                    }
                    else if (GetParameter(s1.variableName.text, out _))
                    {
                        return null;
                    }
                    s1.variableName.Analysis.CompilerReached = true;
                    s1.variableName.Analysis.SubSubtype = TokenSubSubtype.Namespace;
                    return new string[] { s1.variableName.text };
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
                var path = Get(functionCallStatement.PrevStatement);
                if (path == null) return "";
                return string.Join(".", path) + ".";
            }
            else
            {
                return "";
            }
        }

        bool GetFunctionOffset(Statement_FunctionCall functionCallStatement, out int functionOffset)
        {
            string callID = "";

            if (TryGetFunctionNamespacePath(functionCallStatement) == "")
            {
                for (int i = 0; i < functionCallStatement.MethodParameters.Length; i++)
                { callID += "," + FindStatementType(functionCallStatement.MethodParameters[i]); }
            }
            else
            {
                for (int i = 0; i < functionCallStatement.parameters.Count; i++)
                { callID += "," + FindStatementType(functionCallStatement.parameters[i]); }
            }

            if (functionOffsets.TryGetValue(functionCallStatement.FunctionName + callID, out functionOffset))
            {
                return true;
            }
            else if (functionOffsets.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.FunctionName + callID, out functionOffset))
            {
                return true;
            }
            else if (functionOffsets.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID, out functionOffset))
            {
                return true;
            }
            else if (functionOffsets.TryGetValue(functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID, out functionOffset))
            {
                return true;
            }
            functionOffset = -1;
            return false;
        }
        bool GetFunctionOffset(Statement_MethodCall methodCallStatement, out int functionOffset)
        {
            if (GetCompiledVariable(methodCallStatement.VariableName, out CompiledVariable compiledVariable, out _))
            {
                bool IsStructMethodCall = true;
                if (!GetCompiledStruct(compiledVariable.structName, out _))
                { IsStructMethodCall = false; }
                else
                {
                    if (!compiledStructs[compiledVariable.structName].CompiledMethods.ContainsKey(methodCallStatement.FunctionName))
                    { IsStructMethodCall = false; }
                }

                if (!IsStructMethodCall)
                {
                    string callID = "";
                    for (int i = 0; i < methodCallStatement.parameters.Count; i++)
                    { callID += "," + FindStatementType(methodCallStatement.parameters[i]); }

                    if (functionOffsets.TryGetValue(methodCallStatement.FunctionName + callID, out functionOffset))
                    {
                        return true;
                    }
                    else if (functionOffsets.TryGetValue(methodCallStatement.NamespacePathPrefix + methodCallStatement.FunctionName + callID, out functionOffset))
                    {
                        return true;
                    }
                    else if (functionOffsets.TryGetValue(methodCallStatement.NamespacePathPrefix + methodCallStatement.TargetNamespacePathPrefix + methodCallStatement.FunctionName + callID, out functionOffset))
                    {
                        return true;
                    }
                    else if (functionOffsets.TryGetValue(methodCallStatement.TargetNamespacePathPrefix + methodCallStatement.FunctionName + callID, out functionOffset))
                    {
                        return true;
                    }
                }
            }

            functionOffset = -1;
            return false;
        }

        bool GetCompiledStruct(Statement_NewStruct newStructStatement, out CompiledStruct compiledStruct)
        {
            if (compiledStructs.TryGetValue(newStructStatement.structName.text, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.NamespacePathPrefix + newStructStatement.structName.text, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.NamespacePathPrefix + newStructStatement.TargetNamespacePathPrefix + newStructStatement.structName.text, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.TargetNamespacePathPrefix + newStructStatement.structName.text, out compiledStruct))
            {
                return true;
            }
            return false;
        }

        bool GetCompiledStruct(string structName, out CompiledStruct compiledStruct)
        { return compiledStructs.TryGetValue(structName, out compiledStruct); }

        object GenerateInitialValue(TypeToken type)
        {
            if (type.isList)
            {
                return new DataItem.List(DataItem.Type.INT);
            }
            else
            {
                return type.typeName switch
                {
                    BuiltinType.INT => 0,
                    BuiltinType.AUTO => throw new CompilerException("Undefined type", type, CurrentFile),
                    BuiltinType.FLOAT => 0f,
                    BuiltinType.VOID => throw new CompilerException("Invalid type", type, CurrentFile),
                    BuiltinType.STRING => "",
                    BuiltinType.BOOLEAN => false,
                    BuiltinType.STRUCT => new DataItem.UnassignedStruct(),
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
        void AddInstruction(Opcode opcode, object param0) => AddInstruction(new Instruction(opcode, param0));
        void AddInstruction(Opcode opcode, string param0) => AddInstruction(new Instruction(opcode, param0));
        void AddInstruction(Opcode opcode, int param0) => AddInstruction(new Instruction(opcode, param0));
        void AddInstruction(Opcode opcode, bool param0) => AddInstruction(new Instruction(opcode, param0));
        void AddInstruction(Opcode opcode, float param0) => AddInstruction(new Instruction(opcode, param0));
        void AddInstruction(Opcode opcode, object param0, string param1) => AddInstruction(new Instruction(opcode, param0, param1));

        #endregion

        #region FindStatementType()
        string FindStatementType(Statement_FunctionCall functionCall)
        {
            if (functionCall.FunctionName == "type") return "string";

            if (!GetCompiledFunction(functionCall, out var calledFunc))
            { throw new CompilerException("Function '" + functionCall.FunctionName + "' not found!", functionCall.functionNameT, CurrentFile); }
            return FindStatementType(calledFunc.Type);
        }
        string FindStatementType(Statement_Operator @operator)
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

            if (opcode != Opcode.UNKNOWN)
            {
                var leftType = FindStatementType(@operator.Left);
                if (@operator.Right != null)
                {
                    var rightType = FindStatementType(@operator.Right);
                    if (leftType == rightType)
                    {
                        return leftType;
                    }
                    else
                    {
                        if (@operator.TryGetTotalPosition(out var totalPosition))
                        {
                            warnings.Add(new Warning("Thats not good :(", totalPosition, CurrentFile));
                        }
                        else
                        {
                            warnings.Add(new Warning("Thats not good :(", @operator.Operator, CurrentFile));
                        }
                        return "any";
                    }
                }
                else
                { return leftType; }
            }
            else if (@operator.Operator.text == "=")
            {
                throw new NotImplementedException();
            }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.text}'", @operator.position, CurrentFile); }
        }
        static string FindStatementType(Statement_Literal literal)
        {
            return literal.type.typeName switch
            {
                BuiltinType.INT => BuiltinType.INT.ToString().ToLower(),
                BuiltinType.FLOAT => BuiltinType.FLOAT.ToString().ToLower(),
                BuiltinType.STRING => BuiltinType.STRING.ToString().ToLower(),
                BuiltinType.BOOLEAN => BuiltinType.BOOLEAN.ToString().ToLower(),
                _ => throw new NotImplementedException(),
            };
        }
        string FindStatementType(Statement_Variable variable)
        {
            if (GetCompiledVariable(variable.variableName.text, out CompiledVariable val, out _))
            {
                if (variable.listIndex != null)
                {
                    if (val.type == BuiltinType.STRUCT)
                    {
                        return val.structName;
                    }
                    else
                    {
                        return val.type.ToString().ToLower();
                    }
                }
                return val.Type;
            }
            else if (parameters.TryGetValue(variable.variableName.text, out Parameter param))
            {
                if (variable.listIndex != null)
                { throw new NotImplementedException(); }
                return param.type;
            }
            else if (variable.variableName.text == "this")
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.variableName.text + "'", variable.variableName, CurrentFile);
            }
        }
        string FindStatementType(Statement_NewStruct newStruct)
        {
            if (GetCompiledStruct(newStruct, out var structDefinition))
            {
                return structDefinition.Name.text;
            }
            else
            {
                throw new CompilerException("Unknown struct '" + newStruct.structName.text + "'", newStruct.structName, CurrentFile);
            }
        }
        static string FindStatementType(TypeToken type)
        {
            if (type.typeName == BuiltinType.STRUCT)
            { return type.text; }

            return type.typeName.ToString().ToLower();
        }
        string FindStatementType(Statement_Field field)
        {
            var prevStatementType = FindStatementType(field.PrevStatement);

            if (prevStatementType == "string")
            {
                if (field.FieldName.text == "Length")
                {
                    return "int";
                }
            }

            foreach (var strct in compiledStructs)
            {
                if (strct.Key != prevStatementType) continue;

                foreach (var sField in strct.Value.Fields)
                {
                    if (sField.name.text != field.FieldName.text) continue;

                    return FindStatementType(sField.type);
                }

                break;
            }

            throw new NotImplementedException();
        }

        string FindStatementType(Statement st)
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
                string type = "any";
                if (list.Values.Count > 0)
                { type = FindStatementType(list.Values[0]); }
                return type + "[]";
            }
            else if (st is Statement_Index index)
            {
                var type = FindStatementType(index.PrevStatement);
                if (type.EndsWith("[]"))
                { return type[..^2]; }
                else if (type == "string")
                { return "string"; }

                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }
        #endregion

        #region PredictStatementValue()
        static DataItem? PredictStatementValue(Statement_Operator @operator)
        {
            var leftValue = PredictStatementValue(@operator.Left);
            if (!leftValue.HasValue) return null;

            if (@operator.Operator.text == "!")
            {
                return new DataItem(!leftValue, null);
            }

            if (@operator.Right != null)
            {
                var rightValue = PredictStatementValue(@operator.Right);
                if (!rightValue.HasValue) return null;

                return @operator.Operator.text switch
                {
                    "+" => leftValue + rightValue,
                    "-" => leftValue - rightValue,
                    "*" => leftValue * rightValue,
                    "/" => leftValue / rightValue,
                    "%" => leftValue % rightValue,

                    "<" => new DataItem(leftValue < rightValue, null),
                    ">" => new DataItem(leftValue > rightValue, null),
                    "==" => new DataItem(leftValue == rightValue, null),
                    "!=" => new DataItem(leftValue != rightValue, null),
                    "&" => new DataItem(leftValue & rightValue, null),
                    "|" => new DataItem(leftValue | rightValue, null),
                    "^" => new DataItem(leftValue ^ rightValue, null),
                    "<=" => new DataItem(leftValue <= rightValue, null),
                    ">=" => new DataItem(leftValue >= rightValue, null),
                    _ => null,
                };
            }
            else
            { return leftValue; }
        }
        static DataItem? PredictStatementValue(Statement_Literal literal)
        {
            return literal.type.typeName switch
            {
                BuiltinType.INT => new DataItem(int.Parse(literal.value), null),
                BuiltinType.FLOAT => new DataItem(float.Parse(literal.value), null),
                BuiltinType.STRING => new DataItem(literal.value, null),
                BuiltinType.BOOLEAN => new DataItem(bool.Parse(literal.value), null),
                BuiltinType.STRUCT => new DataItem(new DataItem.UnassignedStruct(), null),
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
            newVariable.variableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
            newVariable.variableName.Analysis.CompilerReached = true;

            switch (newVariable.type.typeName)
            {
                case BuiltinType.AUTO:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName.text, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(Opcode.COPY_VALUE_RECURSIVE);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new CompilerException("Unknown variable '" + newVariable.variableName.text + "'", newVariable.variableName, CurrentFile);
                            }
                        }
                    }
                    break;
                case BuiltinType.INT:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName.text, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new CompilerException("Unknown variable '" + newVariable.variableName.text + "'", newVariable.variableName, CurrentFile);
                            }
                        }
                    }
                    break;
                case BuiltinType.FLOAT:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName.text, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new CompilerException("Unknown variable '" + newVariable.variableName.text + "'", newVariable.variableName, CurrentFile);
                            }
                        }
                    }
                    break;
                case BuiltinType.STRING:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName.text, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new CompilerException("Unknown variable '" + newVariable.variableName.text + "'", newVariable.variableName, CurrentFile);
                            }
                        }
                    }
                    break;
                case BuiltinType.BOOLEAN:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName.text, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new CompilerException("Unknown variable '" + newVariable.variableName.text + "'", newVariable.variableName, CurrentFile);
                            }
                        }
                    }
                    break;
                case BuiltinType.STRUCT:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName.text, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(Opcode.COPY_VALUE_RECURSIVE);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new CompilerException("Unknown variable '" + newVariable.variableName.text + "'", newVariable.variableName, CurrentFile);
                            }
                        }
                    }
                    break;
                case BuiltinType.RUNTIME:
                    if (newVariable.initialValue != null)
                    {
                        throw new NotImplementedException();
                    }
                    break;
                case BuiltinType.VOID:
                case BuiltinType.ANY:
                default:
                    throw new InternalException($"Unknown variable type '{newVariable.type.typeName}'", CurrentFile);
            }
        }
        void GenerateCodeForStatement(Statement_FunctionCall functionCall)
        {
            AddInstruction(Opcode.COMMENT, $"{functionCall.FunctionName}():");

            functionCall.functionNameT.Analysis.CompilerReached = true;

            if (functionCall.FunctionName == "return")
            {
                if (functionCall.parameters.Count > 1)
                { throw new CompilerException("Wrong number of parameters passed to 'return'", functionCall.position, CurrentFile); }
                else if (functionCall.parameters.Count == 1)
                {
                    GenerateCodeForStatement(functionCall.parameters[0]);
                    AddInstruction(Opcode.STORE_VALUE_BR, -2 - parameters.Count - ((isStructMethod) ? 1 : 0));
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
                { throw new CompilerException("The keyword 'break' does not avaiable in the current context", functionCall.position, CurrentFile); }

                breakInstructions.Last().Add(compiledCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                return;
            }

            if (functionCall.FunctionName == "type")
            {
                if (functionCall.parameters.Count != 1)
                { throw new CompilerException("Wrong number of parameters passed to 'type'", functionCall.position, CurrentFile); }

                GenerateCodeForStatement(functionCall.parameters[0]);
                AddInstruction(Opcode.TYPE_GET);

                return;
            }

            if (functionCall.IsMethodCall)
            {
                if (functionCall.PrevStatement is Statement_Variable prevVar)
                {
                    if (GetCompiledVariable(prevVar.variableName.text, out var prevVarInfo, out bool isGlobal))
                    {
                        if (prevVarInfo.isList)
                        {
                            if (functionCall.FunctionName == "Push")
                            {
                                AddInstruction(isGlobal ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, prevVarInfo.offset);

                                if (functionCall.parameters.Count != 1)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Push'", functionCall.functionNameT, CurrentFile); }
                                GenerateCodeForStatement(functionCall.parameters[0]);

                                AddInstruction(Opcode.LIST_PUSH_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Pull")
                            {
                                AddInstruction(isGlobal ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, prevVarInfo.offset);

                                if (functionCall.parameters.Count != 0)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Pull'", functionCall.functionNameT, CurrentFile); }

                                AddInstruction(Opcode.LIST_PULL_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Add")
                            {
                                AddInstruction(isGlobal ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, prevVarInfo.offset);

                                if (functionCall.parameters.Count != 2)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Add'", functionCall.functionNameT, CurrentFile); }
                                GenerateCodeForStatement(functionCall.parameters[0]);
                                GenerateCodeForStatement(functionCall.parameters[1]);

                                AddInstruction(Opcode.LIST_ADD_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Remove")
                            {
                                AddInstruction(isGlobal ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, prevVarInfo.offset);

                                if (functionCall.parameters.Count != 1)
                                { throw new CompilerException("Wrong number of parameters passed to '<list>.Remove'", functionCall.functionNameT, CurrentFile); }
                                GenerateCodeForStatement(functionCall.parameters[0]);

                                AddInstruction(Opcode.LIST_REMOVE_ITEM);

                                return;
                            }
                        }
                    }
                }
            }

            if (functionCall.FunctionName == "None")
            {
                foreach (var param in functionCall.parameters)
                {
                    Console.WriteLine(PredictStatementValue(param));
                }
            }

            string searchedID = functionCall.FunctionName;
            searchedID += "(";
            for (int i = 0; i < functionCall.parameters.Count; i++)
            {
                if (i > 0) { searchedID += ", "; }

                searchedID += FindStatementType(functionCall.parameters[i]);
            }
            searchedID += ")";

            if (!GetCompiledFunction(functionCall, out CompiledFunction compiledFunction))
            {
                throw new CompilerException("Unknown function " + searchedID + "", functionCall.functionNameT, CurrentFile);
            }

            functionCall.functionNameT.Analysis.Reference = new TokenAnalysis.RefFunction(compiledFunction);

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new CompilerException("Wrong number of parameters passed to '" + searchedID + $"': requied {compiledFunction.ParameterCount} passed {functionCall.MethodParameters.Length}", functionCall.position, CurrentFile); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new CompilerException($"You called the {((compiledFunction.IsMethod) ? "method" : "function")} '{functionCall.FunctionName}' as {((functionCall.IsMethodCall) ? "method" : "function")}", functionCall.position, CurrentFile); }

            if (compiledFunction.IsBuiltin)
            {
                if (!builtinFunctions.TryGetValue(compiledFunction.BuiltinName, out var builtinFunction))
                {
                    errors.Add(new Error($"Builtin function '{compiledFunction.BuiltinName}' not found", functionCall.functionNameT, CurrentFile));

                    var savedBlockCodeGeneration = BlockCodeGeneration;
                    BlockCodeGeneration = false;

                    if (functionCall.PrevStatement != null)
                    { GenerateCodeForStatement(functionCall.PrevStatement); }
                    foreach (var param in functionCall.parameters)
                    { GenerateCodeForStatement(param); }

                    BlockCodeGeneration = savedBlockCodeGeneration;
                    return;
                }

                if (functionCall.PrevStatement != null)
                { GenerateCodeForStatement(functionCall.PrevStatement); }
                foreach (var param in functionCall.parameters)
                { GenerateCodeForStatement(param); }

                AddInstruction(Opcode.PUSH_VALUE, compiledFunction.BuiltinName);
                AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount);

                if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
                { AddInstruction(Opcode.POP_VALUE); }

                return;
            }

            if (compiledFunction.ReturnSomething)
            { AddInstruction(new Instruction(Opcode.PUSH_VALUE, GenerateInitialValue(compiledFunction.Type)) { tag = "return value" }); }

            if (functionCall.PrevStatement != null)
            {
                GenerateCodeForStatement(functionCall.PrevStatement);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");
            }

            for (int i = 0; i < functionCall.parameters.Count; i++)
            {
                Statement param = functionCall.parameters[i];
                ParameterDefinition definedParam = compiledFunction.Parameters[compiledFunction.IsMethod ? (i + 1) : i];

                AddInstruction(Opcode.COMMENT, $"param:");
                GenerateCodeForStatement(param);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.name);
            }

            if (!GetFunctionOffset(functionCall, out var functionCallOffset))
            { undefinedFunctionOffsets.Add(new UndefinedFunctionOffset(compiledCode.Count, functionCall, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile)); }

            AddInstruction(Opcode.CALL, functionCallOffset - compiledCode.Count);

            for (int i = 0; i < functionCall.parameters.Count; i++)
            { AddInstruction(Opcode.POP_VALUE); }

            if (functionCall.PrevStatement != null)
            { AddInstruction(Opcode.POP_VALUE); }

            if (compiledFunction.ReturnSomething && !functionCall.SaveValue)
            { AddInstruction(Opcode.POP_VALUE); }
        }
        void GenerateCodeForStatement(Statement_MethodCall structMethodCall)
        {
            if (GetCompiledVariable(structMethodCall.VariableName, out CompiledVariable compiledVariable, out var isGlob3))
            {
                if (compiledVariable.type == BuiltinType.RUNTIME)
                {
                    warnings.Add(new Warning($"The type of the variable '{structMethodCall.VariableName}' will be set at runtime. Potential errors may occur.", structMethodCall.variableNameToken, CurrentFile));
                }

                if (compiledVariable.isList)
                {
                    AddInstruction(isGlob3 ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, compiledVariable.offset);

                    if (structMethodCall.FunctionName == "Push")
                    {
                        if (structMethodCall.parameters.Count != 1)
                        { throw new CompilerException("Wrong number of parameters passed to '<list>.Push'", structMethodCall.functionNameT, CurrentFile); }
                        GenerateCodeForStatement(structMethodCall.parameters[0]);

                        AddInstruction(Opcode.LIST_PUSH_ITEM);
                    }
                    else if (structMethodCall.FunctionName == "Pull")
                    {
                        if (structMethodCall.parameters.Count != 0)
                        { throw new CompilerException("Wrong number of parameters passed to '<list>.Pull'", structMethodCall.functionNameT, CurrentFile); }

                        AddInstruction(Opcode.LIST_PULL_ITEM);
                    }
                    else if (structMethodCall.FunctionName == "Add")
                    {
                        if (structMethodCall.parameters.Count != 2)
                        { throw new CompilerException("Wrong number of parameters passed to '<list>.Add'", structMethodCall.functionNameT, CurrentFile); }
                        GenerateCodeForStatement(structMethodCall.parameters[0]);
                        GenerateCodeForStatement(structMethodCall.parameters[1]);

                        AddInstruction(Opcode.LIST_ADD_ITEM);
                    }
                    else if (structMethodCall.FunctionName == "Remove")
                    {
                        if (structMethodCall.parameters.Count != 1)
                        { throw new CompilerException("Wrong number of parameters passed to '<list>.Remove'", structMethodCall.functionNameT, CurrentFile); }
                        GenerateCodeForStatement(structMethodCall.parameters[0]);

                        AddInstruction(Opcode.LIST_REMOVE_ITEM);
                    }
                    else
                    {
                        throw new CompilerException("Unknown list method " + structMethodCall.FunctionName, structMethodCall.functionNameT, CurrentFile);
                    }
                }
                else
                {
                    bool IsStructMethodCall = true;
                    if (!GetCompiledStruct(compiledVariable.structName, out var compiledStruct))
                    { IsStructMethodCall = false; }
                    else
                    {
                        if (!compiledStruct.CompiledMethods.ContainsKey(structMethodCall.FunctionName))
                        { IsStructMethodCall = false; }
                        else
                        {
                            if (structMethodCall.parameters.Count != compiledStruct.CompiledMethods[structMethodCall.FunctionName].ParameterCount)
                            { throw new CompilerException("Wrong number of parameters passed to '" + structMethodCall.VariableName + "'", structMethodCall.position, CurrentFile); }

                            if (compiledStruct.CompiledMethods[structMethodCall.FunctionName].ReturnSomething)
                            {
                                AddInstruction(new Instruction(Opcode.PUSH_VALUE, GenerateInitialValue(compiledStruct.CompiledMethods[structMethodCall.FunctionName].Type)) { tag = "return value" });
                            }

                            if (structMethodCall.parameters.Count != compiledStruct.CompiledMethods[structMethodCall.FunctionName].ParameterCount)
                            { throw new CompilerException("Method '" + structMethodCall.VariableName + "' requies " + compiledStruct.CompiledMethods[structMethodCall.FunctionName].ParameterCount + " parameters", structMethodCall.position, CurrentFile); }

                            AddInstruction(new Instruction(isGlob3 ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, compiledVariable.offset) { tag = "struct.this" });
                            compiledVariables.Add("this", new CompiledVariable(compiledVariable.offset, compiledVariable.structName, compiledVariable.isList, compiledVariable.Declaration));

                            foreach (Statement param in structMethodCall.parameters)
                            { GenerateCodeForStatement(param); }

                            if (compiledStruct.MethodOffsets.TryGetValue(structMethodCall.FunctionName, out var methodCallOffset))
                            { AddInstruction(Opcode.CALL, methodCallOffset - compiledCode.Count); }
                            else
                            { throw new InternalException($"Method '{compiledVariable.structName}.{structMethodCall.FunctionName}' offset not found", CurrentFile); }

                            for (int i = 0; i < structMethodCall.parameters.Count; i++)
                            { AddInstruction(Opcode.POP_VALUE); }

                            if (compiledVariables.Last().Key == "this")
                            {
                                compiledVariables.Remove("this");
                            }
                            else
                            {
                                throw new InternalException("Can't clear the variable 'this': not found", CurrentFile);
                            }

                            if (compiledStruct.CompiledMethods[structMethodCall.FunctionName].ReturnSomething)
                            {
                                AddInstruction(isGlob3 ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, compiledVariable.offset);
                            }
                            else
                            {
                                AddInstruction(isGlob3 ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, compiledVariable.offset);
                            }
                        }
                    }

                    if (!IsStructMethodCall)
                    {
                        if (GetCompiledFunction(structMethodCall, out var compiledFunction))
                        {
                            if (!compiledFunction.IsMethod)
                            { throw new CompilerException($"You called the function '{structMethodCall.FunctionName}' as method", structMethodCall.position, CurrentFile); }

                            if (compiledFunction.ReturnSomething)
                            {
                                AddInstruction(new Instruction(Opcode.PUSH_VALUE, GenerateInitialValue(compiledFunction.Type)) { tag = "return value" });
                            }

                            if (structMethodCall.parameters.Count + 1 != compiledFunction.ParameterCount)
                            { throw new CompilerException("Method '" + structMethodCall.FunctionName + "' requies " + compiledFunction.ParameterCount + " parameters", structMethodCall.position, CurrentFile); }

                            AddInstruction(new Instruction(isGlob3 ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, compiledVariable.offset) { tag = "param.this" });
                            foreach (Statement param in structMethodCall.parameters)
                            {
                                GenerateCodeForStatement(param);
                                compiledCode.Last().tag = "param";
                            }
                            if (!GetFunctionOffset(structMethodCall, out var functionCallOffset))
                            {
                                undefinedFunctionOffsets.Add(new UndefinedFunctionOffset(compiledCode.Count, structMethodCall, parameters.ToArray(), compiledVariables.ToArray(), CurrentFile));
                            }
                            AddInstruction(Opcode.CALL, functionCallOffset - compiledCode.Count);
                            AddInstruction(Opcode.POP_VALUE);
                            for (int i = 0; i < structMethodCall.parameters.Count; i++)
                            {
                                AddInstruction(Opcode.POP_VALUE);
                            }
                        }
                        else
                        { throw new CompilerException($"Method '{structMethodCall.FunctionName}' is doesn't exists", structMethodCall.position, CurrentFile); }
                    }
                }
            }
            else
            { throw new CompilerException("Unknown variable '" + structMethodCall.VariableName + "'", structMethodCall.position, CurrentFile); }

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
                        case DataItem.Type.INT:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueInt);
                                if (@operator.TryGetTotalPosition(out var totalPosition))
                                { informations.Add(new Information($"Predicted value: {predictedValue.ValueInt}", totalPosition, CurrentFile)); }
                                else
                                { informations.Add(new Information($"Predicted value: {predictedValue.ValueInt}", @operator.Operator, CurrentFile)); }
                                return;
                            }
                        case DataItem.Type.BOOLEAN:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueBoolean);
                                if (@operator.TryGetTotalPosition(out var totalPosition))
                                { informations.Add(new Information($"Predicted value: {predictedValue.ValueBoolean}", totalPosition, CurrentFile)); }
                                else
                                { informations.Add(new Information($"Predicted value: {predictedValue.ValueBoolean}", @operator.Operator, CurrentFile)); }
                                return;
                            }
                        case DataItem.Type.FLOAT:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueFloat);
                                if (@operator.TryGetTotalPosition(out var totalPosition))
                                { informations.Add(new Information($"Predicted value: {predictedValue.ValueFloat}", totalPosition, CurrentFile)); }
                                else
                                { informations.Add(new Information($"Predicted value: {predictedValue.ValueFloat}", @operator.Operator, CurrentFile)); }
                                return;
                            }
                        case DataItem.Type.STRING:
                            {
                                AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueString);
                                if (@operator.TryGetTotalPosition(out var totalPosition))
                                { informations.Add(new Information($"Predicted value: {predictedValue.ValueString}", totalPosition, CurrentFile)); }
                                else
                                { informations.Add(new Information($"Predicted value: {predictedValue.ValueString}", @operator.Operator, CurrentFile)); }
                                return;
                            }
                    }
                }
            }

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

                if (@operator.Left is Statement_Variable variable)
                {
                    variable.variableName.Analysis.CompilerReached = true;
                    variable.variableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;

                    if (GetCompiledVariable(variable.variableName.text, out CompiledVariable valueMemoryIndex, out var isGlob))
                    {
                        GenerateCodeForStatement(@operator.Right);
                        AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, valueMemoryIndex.offset);
                    }
                    else if (GetParameter(variable.variableName.text, out Parameter parameter))
                    {
                        GenerateCodeForStatement(@operator.Right);
                        AddInstruction(parameter.isReference ? Opcode.STORE_VALUE_BR_AS_REF : Opcode.STORE_VALUE_BR, parameter.RealIndex);
                    }
                    else
                    {
                        throw new CompilerException("Unknown variable '" + variable.variableName.text + "'", variable.variableName, CurrentFile);
                    }
                }
                else if (@operator.Left is Statement_Field field)
                {
                    field.FieldName.Analysis.CompilerReached = true;
                    field.FieldName.Analysis.SubSubtype = TokenSubSubtype.FieldName;

                    if (field.PrevStatement is Statement_Variable variable1)
                    {
                        variable1.variableName.Analysis.CompilerReached = true;
                        variable1.variableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;

                        if (GetCompiledVariable(variable1.variableName.text, out CompiledVariable valueMemoryIndex, out var isGlob))
                        {
                            GenerateCodeForStatement(@operator.Right);
                            AddInstruction(isGlob ? Opcode.STORE_FIELD : Opcode.STORE_FIELD_BR, valueMemoryIndex.offset, field.FieldName.text);
                        }
                        else if (GetParameter(variable1.variableName.text, out Parameter parameter))
                        {
                            GenerateCodeForStatement(@operator.Right);
                            AddInstruction(isGlob ? Opcode.STORE_FIELD : Opcode.STORE_FIELD_BR, parameter.RealIndex, field.FieldName.text);
                        }
                        else
                        {
                            throw new CompilerException("Unknown variable '" + variable1.variableName.text + "'", variable1.variableName, CurrentFile);
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    throw new CompilerException("Unexpected statement", @operator.Left.position, CurrentFile);
                }
            }
            else
            { throw new CompilerException($"Unknown operator '{@operator.Operator.text}'", @operator.Operator, CurrentFile); }
        }
        void GenerateCodeForStatement(Statement_Literal literal)
        {
            if (literal.ValueToken != null)
            { try { literal.ValueToken.Analysis.CompilerReached = true; } catch (NullReferenceException) { } }

            switch (literal.type.typeName)
            {
                case BuiltinType.INT:
                    AddInstruction(Opcode.PUSH_VALUE, int.Parse(literal.value));
                    break;
                case BuiltinType.FLOAT:
                    AddInstruction(Opcode.PUSH_VALUE, float.Parse(literal.value, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case BuiltinType.STRING:
                    AddInstruction(Opcode.PUSH_VALUE, literal.value);
                    break;
                case BuiltinType.BOOLEAN:
                    AddInstruction(Opcode.PUSH_VALUE, bool.Parse(literal.value));
                    break;
            }
        }
        void GenerateCodeForStatement(Statement_Variable variable)
        {
            variable.variableName.Analysis.CompilerReached = true;

            if (GetCompiledVariable(variable.variableName.text, out CompiledVariable val, out var isGlob_))
            {
                variable.variableName.Analysis.SubSubtype = TokenSubSubtype.VariableName;
                variable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(val.Declaration, isGlob_);

                if (variable.reference)
                {
                    AddInstruction(isGlob_ ? Opcode.LOAD_VALUE_AS_REF : Opcode.LOAD_VALUE_BR_AS_REF, val.offset);
                }
                else
                {
                    AddInstruction(isGlob_ ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, val.offset);
                }
            }
            else if (parameters.TryGetValue(variable.variableName.text, out Parameter param))
            {
                variable.variableName.Analysis.SubSubtype = TokenSubSubtype.ParameterName;
                variable.variableName.Analysis.Reference = new TokenAnalysis.RefParameter(param.type);

                if (variable.reference)
                { throw new CompilerException("Reference can only be applied to a variable", variable.variableName, CurrentFile); }

                AddInstruction((param.isReference) ? Opcode.LOAD_VALUE_BR_AS_REF : Opcode.LOAD_VALUE_BR, param.RealIndex);
            }
            else
            {
                throw new CompilerException("Unknown variable '" + variable.variableName.text + "'", variable.variableName, CurrentFile);
            }

            if (variable.listIndex != null)
            {
                if (variable.reference)
                { throw new CompilerException("Reference cannot be applied to the list", variable.variableName, CurrentFile); }
                GenerateCodeForStatement(variable.listIndex);
                AddInstruction(new Instruction(Opcode.LIST_INDEX));
            }
        }
        void GenerateCodeForStatement(Statement_WhileLoop whileLoop)
        {
            var conditionValue_ = PredictStatementValue(whileLoop.condition);
            if (conditionValue_.HasValue)
            {
                if (conditionValue_.Value.type != DataItem.Type.BOOLEAN)
                {
                    if (whileLoop.condition.TryGetTotalPosition(out var p))
                    {
                        warnings.Add(new Warning($"Condition must be boolean", p, CurrentFile));
                    }
                    else
                    {
                        warnings.Add(new Warning($"Condition must be boolean", whileLoop.condition.position, CurrentFile));
                    }
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
            GenerateCodeForStatement(whileLoop.condition);

            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffset = compiledCode.Count - 1;

            breakInstructions.Add(new List<int>());

            AddInstruction(Opcode.COMMENT, "Statements {");
            for (int i = 0; i < whileLoop.statements.Count; i++)
            {
                GenerateCodeForStatement(whileLoop.statements[i]);
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
                    if (conditionValue.type == DataItem.Type.BOOLEAN)
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
            GenerateCodeForVariable(forLoop.variableDeclaration, out int variablesAdded);
            variableCountStack.Push(variablesAdded);
            GenerateCodeForStatement(forLoop.variableDeclaration);

            AddInstruction(Opcode.COMMENT, "FOR Condition");
            // Index condition
            int conditionOffsetFor = compiledCode.Count;
            GenerateCodeForStatement(forLoop.condition);
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffsetFor = compiledCode.Count - 1;

            breakInstructions.Add(new List<int>());

            AddInstruction(Opcode.COMMENT, "Statements {");
            for (int i = 0; i < forLoop.statements.Count; i++)
            {
                Statement currStatement = forLoop.statements[i];
                GenerateCodeForStatement(currStatement);
            }

            AddInstruction(Opcode.COMMENT, "}");

            AddInstruction(Opcode.COMMENT, "FOR Expression");
            // Index expression
            GenerateCodeForStatement(forLoop.expression);

            AddInstruction(Opcode.COMMENT, "Jump back");
            AddInstruction(Opcode.JUMP_BY, conditionOffsetFor - compiledCode.Count);
            compiledCode[conditionJumpOffsetFor].parameter = compiledCode.Count - conditionJumpOffsetFor;

            foreach (var breakInstruction in breakInstructions.Last())
            {
                compiledCode[breakInstruction].parameter = compiledCode.Count - breakInstruction;
            }
            breakInstructions.RemoveAt(breakInstructions.Count - 1);

            AddInstruction(Opcode.COMMENT, "Clear variables");
            variableCountStack.Pop();
            for (int x = 0; x < variablesAdded; x++)
            {
                AddInstruction(Opcode.POP_VALUE);
            }

            AddInstruction(Opcode.COMMENT, "}");
        }
        void GenerateCodeForStatement(Statement_If @if)
        {
            List<int> jumpOutInstructions = new();
            int jumpNextInstruction = 0;

            foreach (var ifSegment in @if.parts)
            {
                if (ifSegment is Statement_If_If partIf)
                {
                    var conditionValue_ = PredictStatementValue(partIf.condition);
                    if (conditionValue_.HasValue)
                    {
                        var conditionValue = conditionValue_.Value;

                        if (conditionValue.type != DataItem.Type.BOOLEAN)
                        {
                            if (partIf.condition.TryGetTotalPosition(out var p))
                            {
                                warnings.Add(new Warning($"Condition must be boolean", p, CurrentFile));
                            }
                        }
                    }

                    AddInstruction(Opcode.COMMENT, "if (...) {");

                    AddInstruction(Opcode.COMMENT, "IF Condition");
                    GenerateCodeForStatement(partIf.condition);
                    AddInstruction(Opcode.COMMENT, "IF Jump to Next");
                    jumpNextInstruction = compiledCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    int variableCount = CompileVariables(partIf);
                    variableCountStack.Push(variableCount);

                    AddInstruction(Opcode.COMMENT, "IF Statements");
                    for (int i = 0; i < partIf.statements.Count; i++)
                    {
                        GenerateCodeForStatement(partIf.statements[i]);
                    }

                    ClearVariables(variableCount);
                    variableCountStack.Pop();

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
                    GenerateCodeForStatement(partElseif.condition);
                    AddInstruction(Opcode.COMMENT, "ELSEIF Jump to Next");
                    jumpNextInstruction = compiledCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    int variableCount = CompileVariables(partElseif);
                    variableCountStack.Push(variableCount);

                    AddInstruction(Opcode.COMMENT, "ELSEIF Statements");
                    for (int i = 0; i < partElseif.statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElseif.statements[i]);
                    }

                    ClearVariables(variableCount);
                    variableCountStack.Pop();

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

                    int variableCount = CompileVariables(partElse);
                    variableCountStack.Push(variableCount);

                    for (int i = 0; i < partElse.statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElse.statements[i]);
                    }

                    ClearVariables(variableCount);
                    variableCountStack.Pop();

                    AddInstruction(Opcode.COMMENT, "}");
                }
            }

            foreach (var item in jumpOutInstructions)
            {
                compiledCode[item].parameter = compiledCode.Count - item;
            }
        }
        void GenerateCodeForStatement(Statement_NewStruct newStruct)
        {
            newStruct.structName.Analysis.CompilerReached = true;

            if (GetCompiledStruct(newStruct, out var structDefinition))
            {
                newStruct.structName.Analysis.Reference = new TokenAnalysis.RefStruct(structDefinition);

                if (structDefinition.IsBuiltin)
                {
                    AddInstruction(Opcode.PUSH_VALUE, structDefinition.CreateBuiltinStructCallback());
                }
                else
                {
                    Dictionary<string, DataItem> fields = new();
                    foreach (ParameterDefinition structDefFieldDefinition in structDefinition.Fields)
                    {
                        fields.Add(structDefFieldDefinition.name.text, new DataItem(structDefFieldDefinition.type, null));
                    }
                    AddInstruction(Opcode.PUSH_VALUE, new DataItem.Struct(fields));
                }
            }
            else
            {
                throw new CompilerException("Unknown struct '" + newStruct.structName.text + "'", newStruct.structName, CurrentFile);
            }
        }
        void GenerateCodeForStatement(Statement_Field field)
        {
            field.FieldName.Analysis.CompilerReached = true;
            field.FieldName.Analysis.SubSubtype = TokenSubSubtype.FieldName;

            if (field.PrevStatement is Statement_Variable prevVariable)
            {
                prevVariable.variableName.Analysis.CompilerReached = true;

                if (GetCompiledVariable(prevVariable.variableName.text, out CompiledVariable variable, out var isGlob))
                {
                    if (variable.isList)
                    {
                        if (field.FieldName.text == "Length")
                        { }
                        else
                        { throw new CompilerException($"Type list does not have field '{field.FieldName.text}'", field.FieldName, CurrentFile); }
                    }
                    else if (variable.type == BuiltinType.STRING)
                    {
                        if (field.FieldName.text == "Length")
                        { }
                        else
                        { throw new CompilerException($"Type list does not have field '{field.FieldName.text}'", field.FieldName, CurrentFile); }
                    }
                    else if (variable.type == BuiltinType.STRUCT)
                    {
                        if (GetCompiledStruct(variable.structName, out var @struct))
                        {
                            foreach (var field_ in @struct.Fields)
                            {
                                if (field_.name.text == field.FieldName.text)
                                {
                                    field.FieldName.Analysis.Reference = new TokenAnalysis.RefField(field_.type.ToString(), field_.name, @struct.Name.text, @struct.FilePath);
                                    break;
                                }
                            }
                        }
                    }
                    prevVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(variable.Declaration, isGlob);
                    AddInstruction(isGlob ? Opcode.LOAD_FIELD : Opcode.LOAD_FIELD_BR, variable.offset, field.FieldName.text);
                }
                else if (parameters.TryGetValue(prevVariable.variableName.text, out Parameter param))
                {
                    if (GetCompiledStruct(param.type, out var @struct))
                    {
                        foreach (var field_ in @struct.Fields)
                        {
                            if (field_.name.text == field.FieldName.text)
                            {
                                field.FieldName.Analysis.Reference = new TokenAnalysis.RefField(field_.type.ToString(), field_.name, @struct.Name.text, @struct.FilePath);
                                break;
                            }
                        }
                    }
                    prevVariable.variableName.Analysis.Reference = new TokenAnalysis.RefParameter(param.type);
                    AddInstruction(Opcode.LOAD_FIELD_BR, param.RealIndex, field.FieldName.text);
                }
                else
                {
                    throw new CompilerException($"Unknown variable '{prevVariable.variableName.text}'", prevVariable.position, CurrentFile);
                }
                return;
            }

            GenerateCodeForStatement(field.PrevStatement);
            AddInstruction(Opcode.LOAD_FIELD_R, -1, field.FieldName.text);
        }
        void GenerateCodeForStatement(Statement_Index indexStatement)
        {
            GenerateCodeForStatement(indexStatement.PrevStatement);
            if (indexStatement.indexStatement == null)
            { throw new CompilerException($"Index statement for indexer is requied", indexStatement.position, CurrentFile); }
            GenerateCodeForStatement(indexStatement.indexStatement);
            AddInstruction(new Instruction(Opcode.LIST_INDEX));
        }
        void GenerateCodeForStatement(Statement_ListValue listValue)
        {
            DataItem.Type listType = DataItem.Type.RUNTIME;
            for (int i = 0; i < listValue.Size; i++)
            {
                if (listValue.Values[i] is not Statement_Literal literal)
                { throw new CompilerException("Only literals are supported in list value", listValue.Values[i].position, CurrentFile); }
                if (i == 0)
                {
                    listType = literal.type.typeName.Convert();
                    if (listType == DataItem.Type.RUNTIME)
                    { throw new CompilerException($"Unknown literal type {listType}", literal.type, CurrentFile); }
                }
                if (literal.type.typeName.Convert() != listType)
                { throw new CompilerException($"Wrong literal type {literal.type.typeName}. Expected {listType}", literal.type, CurrentFile); }
            }
            DataItem newList = new(new DataItem.List(listType), null);
            AddInstruction(Opcode.COMMENT, "Generate List {");
            AddInstruction(Opcode.PUSH_VALUE, newList);
            for (int i = 0; i < listValue.Size; i++)
            {
                AddInstruction(Opcode.LOAD_VALUE_R, -1);
                GenerateCodeForStatement(listValue.Values[i]);
                AddInstruction(Opcode.LIST_PUSH_ITEM);
            }
            AddInstruction(Opcode.COMMENT, "}");
        }

        void GenerateCodeForStatement(Statement st)
        {
            int variableCount = 0;
            if (st is StatementParent statementParent)
            { variableCount = CompileVariables(statementParent); variableCountStack.Push(variableCount); }

            if (st is Statement_ListValue listValue)
            { GenerateCodeForStatement(listValue); }
            else if (st is Statement_NewVariable newVariable)
            { GenerateCodeForStatement(newVariable); }
            else if (st is Statement_FunctionCall functionCall)
            {
                if (st is Statement_MethodCall structMethodCall)
                { GenerateCodeForStatement(structMethodCall); }
                else
                { GenerateCodeForStatement(functionCall); }
            }
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
            { ClearVariables(variableCount); variableCountStack.Pop(); }
        }

        int CompileVariables(StatementParent statement, bool addComments = true)
        {
            if (addComments) AddInstruction(Opcode.COMMENT, "Variables");
            int variableCount = 0;
            foreach (var s in statement.statements)
            {
                GenerateCodeForVariable(s, out int newVariableCount);
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

        #endregion

        #region GenerateCodeFor...

        void GenerateCodeForGlobalVariable(Statement st, out int globalVariablesAdded)
        {
            int variableCount = 0;

            if (st is Statement_NewVariable newVariable)
            {
                newVariable.variableName.Analysis.CompilerReached = true;
                newVariable.type.Analysis.CompilerReached = true;

                if (Keywords.Contains(newVariable.variableName.text))
                { throw new CompilerException($"Illegal variable name '{newVariable.variableName.text}'", st.position, newVariable.FilePath); }

                switch (newVariable.type.typeName)
                {
                    case BuiltinType.INT:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue1 = 0;
                        if (newVariable.type.isList)
                        {
                            initialValue1 = new DataItem.List(DataItem.Type.INT);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == newVariable.type.typeName)
                                { initialValue1 = int.Parse(literal.value); }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledGlobalVariables.Add(newVariable.variableName.text, new CompiledVariable(variableCount, newVariable.type.typeName, newVariable.type.isList, newVariable));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue1) { tag = "var." + newVariable.variableName.text });
                        variableCount++;
                        break;
                    case BuiltinType.FLOAT:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue2 = 0;
                        if (newVariable.type.isList)
                        {
                            initialValue2 = new DataItem.List(DataItem.Type.FLOAT);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == BuiltinType.FLOAT || literal.type.typeName == IngameCoding.BBCode.BuiltinType.INT)
                                { initialValue2 = float.Parse(literal.value); }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledGlobalVariables.Add(newVariable.variableName.text, new CompiledVariable(variableCount, newVariable.type.typeName, newVariable.type.isList, newVariable));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue2) { tag = "var." + newVariable.variableName.text });
                        variableCount++;
                        break;
                    case BuiltinType.STRING:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue3 = "";
                        if (newVariable.type.isList)
                        {
                            initialValue3 = new DataItem.List(DataItem.Type.STRING);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == BuiltinType.STRING)
                                { initialValue3 = literal.value; }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledGlobalVariables.Add(newVariable.variableName.text, new CompiledVariable(variableCount, newVariable.type.typeName, newVariable.type.isList, newVariable));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue3) { tag = "var." + newVariable.variableName.text });
                        variableCount++;
                        break;
                    case BuiltinType.BOOLEAN:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue4 = false;
                        if (newVariable.type.isList)
                        {
                            initialValue4 = new DataItem.List(DataItem.Type.BOOLEAN);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == newVariable.type.typeName)
                                { initialValue4 = bool.Parse(literal.value); }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledGlobalVariables.Add(newVariable.variableName.text, new CompiledVariable(variableCount, newVariable.type.typeName, newVariable.type.isList, newVariable));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue4) { tag = "var." + newVariable.variableName.text });
                        variableCount++;
                        break;
                    case BuiltinType.STRUCT:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Struct;

                        if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_NewStruct newStruct)
                            {
                                if (newStruct.structName.text == newVariable.type.text)
                                { GenerateCodeForStatement(newStruct); }
                                else
                                { throw new CompilerException("Can't cast " + newStruct.structName.text + " to " + newVariable.type.text, newStruct.position, newVariable.FilePath); }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        compiledGlobalVariables.Add(newVariable.variableName.text, new CompiledVariable(variableCount, newVariable.type.text, newVariable.type.isList, newVariable));
                        variableCount++;
                        break;
                    case BuiltinType.AUTO:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Keyword;

                        if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                newVariable.type.typeName = literal.type.typeName;
                            }
                            else if (newVariable.initialValue is Statement_NewStruct newStruct)
                            {
                                newVariable.type.typeName = BuiltinType.STRUCT;
                                newVariable.type.text = newStruct.structName.text;
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, true);
                        if (newVariable.type.typeName == BuiltinType.AUTO)
                        { throw new InternalException("Invalid or unimplemented initial value", newVariable.FilePath); }
                        GenerateCodeForGlobalVariable(newVariable, out _);
                        variableCount++;
                        break;
                }
            }

            globalVariablesAdded = variableCount;
        }

        void GenerateCodeForVariable(Statement st, out int variablesAdded)
        {
            int variableCount = 0;

            if (st is Statement_NewVariable newVariable)
            {
                newVariable.variableName.Analysis.CompilerReached = true;
                newVariable.type.Analysis.CompilerReached = true;

                if (Keywords.Contains(newVariable.variableName.text))
                { throw new CompilerException($"Illegal variable name '{newVariable.variableName.text}'", st.position, CurrentFile); }

                switch (newVariable.type.typeName)
                {
                    case BuiltinType.INT:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue1 = 0;
                        if (newVariable.type.isList)
                        {
                            initialValue1 = new DataItem.List(IngameCoding.Bytecode.DataItem.Type.INT);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == newVariable.type.typeName)
                                {
                                    initialValue1 = int.Parse(literal.value);
                                }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false);
                        compiledVariables.Add(newVariable.variableName.text, new CompiledVariable(compiledVariables.Count, newVariable.type.typeName, newVariable.type.isList, newVariable));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue1) { tag = "var." + newVariable.variableName.text });
                        variableCount++;
                        break;
                    case BuiltinType.FLOAT:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue2 = 0;
                        if (newVariable.type.isList)
                        {
                            initialValue2 = new DataItem.List(DataItem.Type.FLOAT);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == BuiltinType.FLOAT || literal.type.typeName == BuiltinType.INT)
                                {
                                    initialValue2 = float.Parse(literal.value);
                                }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false);
                        compiledVariables.Add(newVariable.variableName.text, new CompiledVariable(compiledVariables.Count, newVariable.type.typeName, newVariable.type.isList, newVariable));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue2) { tag = "var." + newVariable.variableName.text });
                        variableCount++;
                        break;
                    case BuiltinType.STRING:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue3 = "";
                        if (newVariable.type.isList)
                        {
                            initialValue3 = new DataItem.List(DataItem.Type.STRING);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == BuiltinType.STRING)
                                {
                                    initialValue3 = literal.value;
                                }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false);
                        compiledVariables.Add(newVariable.variableName.text, new CompiledVariable(compiledVariables.Count, newVariable.type.typeName, newVariable.type.isList, newVariable));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue3) { tag = "var." + newVariable.variableName.text });
                        variableCount++;
                        break;
                    case BuiltinType.BOOLEAN:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Type;

                        object initialValue4 = false;
                        if (newVariable.type.isList)
                        {
                            initialValue4 = new DataItem.List(DataItem.Type.BOOLEAN);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == newVariable.type.typeName)
                                {
                                    initialValue4 = bool.Parse(literal.value);
                                }
                            }
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false);
                        compiledVariables.Add(newVariable.variableName.text, new CompiledVariable(compiledVariables.Count, newVariable.type.typeName, newVariable.type.isList, newVariable));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue4) { tag = "var." + newVariable.variableName.text });
                        variableCount++;
                        break;
                    case BuiltinType.STRUCT:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Struct;

                        if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_NewStruct literal)
                            {
                                if (literal.structName.text == newVariable.type.text)
                                {
                                    GenerateCodeForStatement(literal);
                                    compiledCode.Last().tag = "var." + newVariable.variableName.text;
                                    compiledVariables.Add(newVariable.variableName.text, new CompiledVariable(compiledVariables.Count, literal.structName.text, newVariable.type.isList, newVariable));
                                }
                                else
                                {
                                    throw new CompilerException("Can't cast " + literal.structName.text + " to " + newVariable.type.text, literal.position, CurrentFile);
                                }
                            }
                            else
                            {
                                compiledVariables.Add(newVariable.variableName.text, new CompiledVariable(compiledVariables.Count, newVariable.type.text, newVariable.type.isList, newVariable));
                                AddInstruction(new Instruction(Opcode.PUSH_VALUE, new DataItem.UnassignedStruct()) { tag = "var." + newVariable.variableName.text });
                            }
                        }
                        else
                        {
                            compiledVariables.Add(newVariable.variableName.text, new CompiledVariable(compiledVariables.Count, newVariable.type.text, newVariable.type.isList, newVariable));
                            AddInstruction(new Instruction(Opcode.PUSH_VALUE, new DataItem.UnassignedStruct()) { tag = "var." + newVariable.variableName.text });
                        }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false);
                        variableCount++;
                        break;
                    case BuiltinType.AUTO:
                        newVariable.type.Analysis.SubSubtype = TokenSubSubtype.Keyword;

                        if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                newVariable.type.typeName = literal.type.typeName;
                            }
                            else if (newVariable.initialValue is Statement_NewStruct newStruct)
                            {
                                newVariable.type.typeName = BuiltinType.STRUCT;
                                newVariable.type.text = newStruct.structName.text;
                            }
                            else
                            { throw new CompilerException("Expected literal or new struct as initial value for variable", newVariable.variableName, CurrentFile); }
                        }
                        else
                        { throw new CompilerException("Expected literal or new struct as initial value for variable", newVariable.variableName, CurrentFile); }
                        newVariable.variableName.Analysis.Reference = new TokenAnalysis.RefVariable(newVariable, false);
                        GenerateCodeForVariable(newVariable, out _);
                        variableCount++;
                        break;
                    case BuiltinType.RUNTIME:
                        {
                            if (newVariable.initialValue != null)
                            {
                                throw new NotImplementedException();
                            }
                            else
                            { throw new CompilerException("Expected literal or new struct as initial value for variable", newVariable.variableName, CurrentFile); }
                        }
                    case BuiltinType.VOID:
                    case BuiltinType.ANY:
                    default:
                        throw new CompilerException($"Unknown variable type '{newVariable.type.typeName}'", newVariable.type, CurrentFile);
                }
            }

            variablesAdded = variableCount;
        }
        void GenerateCodeForVariable(Statement[] sts, out int variablesAdded)
        {
            variablesAdded = 0;
            for (int i = 0; i < sts.Length; i++)
            {
                GenerateCodeForVariable(sts[i], out var x);
                variablesAdded += x;
            }
        }
        void GenerateCodeForVariable(List<Statement> sts, out int variablesAdded) => GenerateCodeForVariable(sts.ToArray(), out variablesAdded);

        void GenerateCodeForFunction(KeyValuePair<string, FunctionDefinition> function, bool isMethod)
        {
            function.Value.Name.Analysis.CompilerReached = true;
            function.Value.Type.Analysis.CompilerReached = true;

            if (Keywords.Contains(function.Value.Name.text))
            { throw new CompilerException($"Illegal function name '{function.Value.Name.text}'", function.Value.Name, function.Value.FilePath); }

            function.Value.Name.Analysis.SubSubtype = TokenSubSubtype.FunctionName;

            this.isStructMethod = isMethod;

            if (GetFunctionInfo(function, isMethod).IsBuiltin) return;

            parameters.Clear();
            compiledVariables.Clear();
            returnInstructions.Clear();

            functionOffsets.Add(function.Value.ID(), compiledCode.Count);
            if (isMethod)
            { compiledStructs[compiledStructs.Last().Key].MethodOffsets.Add(function.Value.FullName, compiledCode.Count); }

            // Compile parameters
            int paramIndex = 0;
            foreach (ParameterDefinition parameter in function.Value.Parameters)
            {
                paramIndex++;
                parameter.name.Analysis.CompilerReached = true;
                parameter.type.Analysis.CompilerReached = true;
                parameters.Add(parameter.name.text, new Parameter(paramIndex, parameter.name.text, parameter.withRefKeyword, function.Value.Parameters.Count, parameter.type.ToString()));
            }

            CurrentFile = function.Value.FilePath;

            AddInstruction(Opcode.CS_PUSH, $"{function.Value.ReadableID()}");

            // Search for variables
            AddInstruction(Opcode.COMMENT, "Variables");
            GenerateCodeForVariable(function.Value.Statements, out int variableCount);
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
            compiledVariables.Clear();
            returnInstructions.Clear();

            this.isStructMethod = false;
        }

        void GenerateCodeForStruct(KeyValuePair<string, StructDefinition> @struct, Dictionary<string, Func<IStruct>> builtinStructs)
        {
            CurrentFile = @struct.Value.FilePath;
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

            foreach (var field in @struct.Value.Fields)
            {
                field.name.Analysis.CompilerReached = true;
                field.type.Analysis.CompilerReached = true;
            }

            this.compiledStructs.Add(@struct.Key, new CompiledStruct(attributes, @struct.Value));

            /*
            foreach (var method in @struct.Value.Methods)
            {
                if (compiledFunctions.ContainsKey(method.Key))
                { throw new CompilerException($"Function with name '{method.Key}' already defined", method.Value.Name, CurrentFile); }

                var methodInfo = GetFunctionInfo(method, true);
                methodInfo.IsMethod = true;
                this.compiledFunctions.Add(method.Value.FullName, methodInfo);

                AddInstruction(Opcode.COMMENT, @struct.Value.FullName + "." + method.Value.FullName + ((method.Value.Parameters.Count > 0) ? "(...)" : "()") + " {");
                GenerateCodeForFunction(method, true);
                AddInstruction(Opcode.COMMENT, "}");
                this.compiledStructs.Last().Value.CompiledMethods.Add(method.Key, methodInfo);
            }
            */

            CurrentFile = null;
        }

        #endregion

        #region Result Structs

        public struct CodeGeneratorResult
        {
            public Instruction[] compiledCode;

            public Dictionary<string, CompiledFunction> compiledFunctions;
            public Dictionary<string, CompiledStruct> compiledStructs;

            public int clearGlobalVariablesInstruction;
            public int setGlobalVariablesInstruction;
        }

        #endregion

        CompiledFunction GetFunctionInfo(KeyValuePair<string, FunctionDefinition> function, bool isStructMethod = false)
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
                            if (builtinFunction.Value.ParameterCount != function.Value.Parameters.Count + (isStructMethod ? 1 : 0))
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

        int AnalyzeFunctions(Dictionary<string, FunctionDefinition> functions, List<Statement_NewVariable> globalVariables, Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            printCallback?.Invoke($"  Remove unused functions ...", TerminalInterpreter.LogType.Debug);

            // Remove unused functions
            {
                void AnalyzeStatements(List<Statement> statements)
                {
                    int variablesAdded = 0;
                    foreach (var st in statements)
                    {
                        if (st is Statement_NewVariable newVar)
                        {
                            this.compiledVariables.Add(newVar.variableName.text, new CompiledVariable()
                            {
                                isList = newVar.type.isList,
                                offset = -1,
                                type = newVar.type.typeName,
                                structName = (newVar.type.typeName == BuiltinType.STRUCT) ? newVar.type.text : null,
                            });
                            variablesAdded++;
                        }
                        else if (st is Statement_ForLoop forLoop)
                        {
                            this.compiledVariables.Add(forLoop.variableDeclaration.variableName.text, new CompiledVariable()
                            {
                                isList = forLoop.variableDeclaration.type.isList,
                                offset = -1,
                                type = forLoop.variableDeclaration.type.typeName,
                                structName = (forLoop.variableDeclaration.type.typeName == BuiltinType.STRUCT) ? forLoop.variableDeclaration.type.text : null,
                            });
                            variablesAdded++;
                        }
                    }

                    foreach (var st in statements)
                    {
                        AnalyzeStatement(st);
                        if (st is StatementParent pr)
                        {
                            AnalyzeStatements(pr.statements);
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
                        AnalyzeStatement(st0.variableDeclaration);
                        AnalyzeStatement(st0.condition);
                        AnalyzeStatement(st0.expression);
                    }
                    else if (st is Statement_If st1)
                    {
                        foreach (var st2 in st1.parts)
                        { AnalyzeStatement(st2); }
                    }
                    else if (st is Statement_If_If st2)
                    {
                        AnalyzeStatement(st2.condition);
                        AnalyzeStatements(st2.statements);
                    }
                    else if (st is Statement_If_ElseIf st3)
                    {
                        AnalyzeStatement(st3.condition);
                        AnalyzeStatements(st3.statements);
                    }
                    else if (st is Statement_If_Else st3a)
                    {
                        AnalyzeStatements(st3a.statements);
                    }
                    else if (st is Statement_Index st4)
                    {
                        AnalyzeStatement(st4.indexStatement);
                    }
                    else if (st is Statement_NewVariable st5)
                    {
                        if (st5.initialValue != null) AnalyzeStatement(st5.initialValue);
                    }
                    else if (st is Statement_Operator st6)
                    {
                        if (st6.Left != null) AnalyzeStatement(st6.Left);
                        if (st6.Right != null) AnalyzeStatement(st6.Right);
                    }
                    else if (st is Statement_WhileLoop st7)
                    {
                        AnalyzeStatement(st7.condition);
                    }
                    else if (st is Statement_FunctionCall st8)
                    {
                        foreach (var st9 in st8.parameters)
                        { AnalyzeStatement(st9); }

                        if (st8.FunctionName == "return")
                        { return; }

                        if (st8.PrevStatement != null)
                        { AnalyzeStatement(st8.PrevStatement); }

                        if (GetCompiledFunction(st8, out var cf))
                        { cf.TimesUsed++; }
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
                    parameters.Clear();

                    foreach (ParameterDefinition parameter in f.Value.Parameters)
                    {
                        parameters.Add(parameter.name.text, new Parameter(-1, parameter.name.text, parameter.withRefKeyword, -1, parameter.type.ToString()));
                    }

                    AnalyzeStatements(f.Value.Statements);

                    parameters.Clear();
                }
            }

            printCallback?.Invoke($"   Processing ...", TerminalInterpreter.LogType.Debug);

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

                string readableID = element.Value.ReadableID();

                printCallback?.Invoke($"      Remove function '{readableID}' ...", TerminalInterpreter.LogType.Debug);
                informations.Add(new Information($"Unused function '{readableID}' is not compiled", element.Value.Name, element.Value.FilePath));

                functions.Remove(element.Key);
                functionsRemoved++;

            JumpOut:;
            }

            return functionsRemoved;
        }

        public struct Context
        {

        }

        public Context GenerateCode(
            Dictionary<string, FunctionDefinition> functions,
            Dictionary<string, StructDefinition> structs,
            Statement_HashInfo[] hashes,
            List<Statement_NewVariable> globalVariables,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            Dictionary<string, Func<IStruct>> builtinStructs,
            Compiler.CompilerSettings settings,
            int contextPosition,
            Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            this.ContextPosition = contextPosition;
            this.FindContext = true;
            this.ContextResult = new Context();
            GenerateCode(functions, structs, hashes, globalVariables, builtinFunctions, builtinStructs, settings, printCallback);
            return this.ContextResult;
        }

        bool AtContext(Statement st)
        {
            return st.position.AbsolutePosition.Contains(ContextPosition);
        }

        internal CodeGeneratorResult GenerateCode(
            Dictionary<string, FunctionDefinition> functions,
            Dictionary<string, StructDefinition> structs,
            Statement_HashInfo[] hashes,
            List<Statement_NewVariable> globalVariables,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            Dictionary<string, Func<IStruct>> builtinStructs,
            Compiler.CompilerSettings settings,
            Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            BlockCodeGeneration = true;

            this.GenerateDebugInstructions = settings.GenerateDebugInstructions;
            this.AddCommentsToCode = settings.GenerateComments;
            this.compiledStructs = new();
            this.compiledGlobalVariables = new();
            this.functionOffsets = new();
            this.compiledCode = new();
            this.builtinFunctions = builtinFunctions;
            this.OptimizeCode = !settings.DontOptimize;
            this.compiledFunctions = new();

            #region Compile test built-in functions

            foreach (var hash in hashes)
            {
                switch (hash.HashName.text)
                {
                    case "bf":
                        {
                            if (hash.Parameters.Length <= 1)
                            { errors.Add(new Error($"Hash '{hash.HashName}' requies minimum 2 parameter", hash.HashName, hash.FilePath)); break; }
                            string bfName = hash.Parameters[0].value;

                            if (builtinFunctions.ContainsKey(bfName)) break;

                            string[] bfParams = new string[hash.Parameters.Length - 1];
                            for (int i = 1; i < hash.Parameters.Length; i++)
                            { bfParams[i - 1] = hash.Parameters[i].value; }

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
                                    case "int[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.INT, true);
                                        break;
                                    case "string[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.STRING, true);
                                        break;
                                    case "float[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.FLOAT, true);
                                        break;
                                    case "bool[]":
                                        parameterTypes[i] = TypeToken.CreateAnonymous(bfParams[i], BuiltinType.BOOLEAN, true);
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
                                                returnType.isList ?
                                                new DataItem.List(DataItem.Type.INT) : 0
                                            , "return value"));
                                            break;
                                        case BuiltinType.FLOAT:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.isList ?
                                                new DataItem.List(DataItem.Type.FLOAT) : 0f
                                            , "return value"));
                                            break;
                                        case BuiltinType.STRING:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.isList ?
                                                new DataItem.List(DataItem.Type.STRING) : ""
                                            , "return value"));
                                            break;
                                        case BuiltinType.BOOLEAN:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.isList ?
                                                new DataItem.List(DataItem.Type.BOOLEAN) : false
                                            , "return value"));
                                            break;
                                        case BuiltinType.STRUCT:
                                            self.RaiseReturnEvent(new DataItem(
                                                returnType.isList ?
                                                new DataItem.List(DataItem.Type.STRUCT) : new DataItem.UnassignedStruct()
                                            , "return value"));
                                            break;
                                        case BuiltinType.RUNTIME:
                                        case BuiltinType.ANY:
                                        case BuiltinType.VOID:
                                        case BuiltinType.AUTO:
                                        default:
                                            throw new RuntimeException($"Invalid return type \"{returnType.text}\"/{returnType.ToString().ToLower()} from built-in function \"{bfName}\"");
                                    }
                                }), pTypes, true));
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

            #region Compile Structs

            BlockCodeGeneration = false;

            foreach (var @struct in structs)
            { GenerateCodeForStruct(@struct, builtinStructs); }

            BlockCodeGeneration = true;

            #endregion


            foreach (var function in functions)
            {
                var id = function.Value.ID();

                if (this.compiledFunctions.ContainsKey(id))
                { throw new CompilerException($"Function with name '{id}' already defined", function.Value.Name, function.Value.FilePath); }

                this.compiledFunctions.Add(id, GetFunctionInfo(function));
            }

            int iterations = settings.RemoveUnusedFunctionsMaxIterations;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int functionsRemoved = AnalyzeFunctions(functions, globalVariables, printCallback);
                if (functionsRemoved == 0)
                {
                    printCallback?.Invoke($"  Deletion of unused functions is complete", TerminalInterpreter.LogType.Debug);
                    break;
                }

                printCallback?.Invoke($"  Removed {functionsRemoved} unused functions (iteration {iteration})", TerminalInterpreter.LogType.Debug);
            }

            #region Code Generation

            BlockCodeGeneration = false;

            var setGlobalVariablesInstruction = compiledCode.Count;
            AddInstruction(Opcode.COMMENT, "Global variables");
            int globalVariableCount = 0;
            foreach (var globalVariable in globalVariables)
            {
                GenerateCodeForGlobalVariable(globalVariable, out int x);
                globalVariableCount += x;
            }
            AddInstruction(Opcode.EXIT);

            foreach (KeyValuePair<string, FunctionDefinition> function in functions)
            {
                AddInstruction(Opcode.COMMENT, function.Value.FullName + ((function.Value.Parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Value.Statements.Count > 0) ? "" : " }"));
                GenerateCodeForFunction(function, false);
                if (function.Value.Statements.Count > 0) AddInstruction(Opcode.COMMENT, "}");
            }

            var clearGlobalVariablesInstruction = compiledCode.Count;
            AddInstruction(Opcode.COMMENT, "Clear global variables");
            for (int i = 0; i < globalVariableCount; i++)
            { AddInstruction(Opcode.POP_VALUE); }
            AddInstruction(Opcode.EXIT);

            BlockCodeGeneration = true;

            #endregion

            foreach (var item in undefinedFunctionOffsets)
            {
                foreach (var pair in item.currentParameters)
                { parameters.Add(pair.Key, pair.Value); }
                foreach (var pair in item.currentVariables)
                { compiledVariables.Add(pair.Key, pair.Value); }

                if (GetFunctionOffset(item.functionCallStatement, out var functionCallOffset))
                { compiledCode[item.callInstructionIndex].parameter = functionCallOffset - item.callInstructionIndex; }
                else
                { throw new InternalException($"Function '{item.functionCallStatement.TargetNamespacePathPrefix + item.functionCallStatement.FunctionName}' offset not found", item.CurrentFile); }

                parameters.Clear();
                compiledVariables.Clear();
            }

            return new CodeGeneratorResult()
            {
                compiledCode = compiledCode.ToArray(),

                compiledFunctions = this.compiledFunctions,
                compiledStructs = this.compiledStructs,

                clearGlobalVariablesInstruction = clearGlobalVariablesInstruction,
                setGlobalVariablesInstruction = setGlobalVariablesInstruction
            };
        }
    }
}