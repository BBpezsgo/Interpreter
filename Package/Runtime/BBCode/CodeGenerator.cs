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

    internal class CodeGenerator
    {
        static readonly string[] BuiltinFunctions = new string[]
        {
            "return",
            "break",
            "type",
        };

        #region Fields

        bool BlockCodeGeneration = false;

        internal Dictionary<string, CompiledStruct> compiledStructs;
        internal Dictionary<string, CompiledFunction> compiledFunctions;
        internal readonly Dictionary<string, CompiledVariable> compiledVariables = new();
        internal Dictionary<string, CompiledVariable> compiledGlobalVariables;

        internal Dictionary<string, int> functionOffsets;

        Dictionary<string, BuiltinFunction> builtinFunctions;
        readonly Dictionary<string, Parameter> parameters = new();

        public List<Warning> warnings;

        readonly List<int> returnInstructions = new();
        readonly List<List<int>> breakInstructions = new();

        bool isStructMethod;

        List<Instruction> compiledCode;

        readonly List<UndefinedFunctionOffset> undefinedFunctionOffsets = new();

        bool OptimizeCode;
        bool AddCommentsToCode = true;

        #endregion

        #region Helpre Functions

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

            var xd = TryGetFunctionNamespacePath(functionCallStatement);

            if (xd == "")
            {
                callID = "";
                for (int i = 0; i < functionCallStatement.MethodParameters.Length; i++)
                { callID += "," + FindStatementType(functionCallStatement.MethodParameters[i]); }

                if (compiledFunctions.TryGetValue(functionCallStatement.FunctionName + callID, out compiledFunction))
                { return true; }

                if (compiledFunctions.TryGetValue(functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID, out compiledFunction))
                { return true; }
            }

            if (compiledFunctions.TryGetValue(xd + functionCallStatement.FunctionName + callID, out compiledFunction))
            {
                if (xd.EndsWith("."))
                { xd = xd[..^1]; }
                functionCallStatement.IsMethodCall = false;
                functionCallStatement.NamespacePathPrefix = xd;
                functionCallStatement.PrevStatement = null;
                return true;
            }

            if (compiledFunctions.TryGetValue(xd + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName + callID, out compiledFunction))
            {
                if (xd.EndsWith("."))
                { xd = xd[..^1]; }
                functionCallStatement.IsMethodCall = false;
                functionCallStatement.NamespacePathPrefix = xd;
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
                    if (GetCompiledVariable(s1.variableName, out _, out _))
                    {
                        return null;
                    }
                    else if (GetParameter(s1.variableName, out _))
                    {
                        return null;
                    }
                    return new string[] { s1.variableName };
                }
                if (statement is Statement_Field s2)
                {
                    var prev_ = Get(s2.PrevStatement);
                    if (prev_ == null) { return null; }

                    var prev = prev_.ToList();
                    prev.Insert(0, s2.FieldName);
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
                    if (!compiledStructs[compiledVariable.structName].methods.ContainsKey(methodCallStatement.FunctionName))
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
            if (compiledStructs.TryGetValue(newStructStatement.structName, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.NamespacePathPrefix + newStructStatement.structName, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.NamespacePathPrefix + newStructStatement.TargetNamespacePathPrefix + newStructStatement.structName, out compiledStruct))
            {
                return true;
            }
            else if (compiledStructs.TryGetValue(newStructStatement.TargetNamespacePathPrefix + newStructStatement.structName, out compiledStruct))
            {
                return true;
            }
            return false;
        }

        bool GetCompiledStruct(string structName, out CompiledStruct compiledStruct)
        { return compiledStructs.TryGetValue(structName, out compiledStruct); }

        static object GenerateInitialValue(TypeToken type)
        {
            if (type.isList)
            {
                return new Stack.Item.List(Stack.Item.Type.INT);
            }
            else
            {
                return type.typeName switch
                {
                    BuiltinType.INT => 0,
                    BuiltinType.AUTO => throw new ParserException("Undefined type"),
                    BuiltinType.FLOAT => 0f,
                    BuiltinType.VOID => throw new ParserException("Invalid type"),
                    BuiltinType.STRING => "",
                    BuiltinType.BOOLEAN => false,
                    BuiltinType.STRUCT => new Stack.Item.UnassignedStruct(),
                    _ => throw new InternalException($"initial value for type {type.typeName} is unimplemented"),
                };
            }
        }

        #endregion

        #region AddInstruction()

        void AddInstruction(Instruction instruction)
        {
            if ((AddCommentsToCode || instruction.opcode != Opcode.COMMENT) && !BlockCodeGeneration)
            { compiledCode.Add(instruction); }
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
            if (!GetCompiledFunction(functionCall, out var calledFunc))
            { throw new ParserException("Function '" + functionCall.FunctionName + "' not found!"); }
            return FindStatementType(calledFunc.type);
        }
        string FindStatementType(Statement_Operator @operator)
        {
            Opcode opcode = Opcode.UNKNOWN;

            if (@operator.Operator == "!")
            {
                if (@operator.ParameterCount != 1) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_NOT;
            }
            else if (@operator.Operator == "+")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_ADD;
            }
            else if (@operator.Operator == "<")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_LT;
            }
            else if (@operator.Operator == ">")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_MT;
            }
            else if (@operator.Operator == "-")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_SUB;
            }
            else if (@operator.Operator == "*")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_MULT;
            }
            else if (@operator.Operator == "/")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_DIV;
            }
            else if (@operator.Operator == "%")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_MOD;
            }
            else if (@operator.Operator == "==")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_EQ;
            }
            else if (@operator.Operator == "!=")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_NEQ;
            }
            else if (@operator.Operator == "&")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_AND;
            }
            else if (@operator.Operator == "|")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_OR;
            }
            else if (@operator.Operator == "^")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_XOR;
            }
            else if (@operator.Operator == "<=")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_LTEQ;
            }
            else if (@operator.Operator == ">=")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
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
                        warnings.Add(new Warning()
                        {
                            Message = "Thats not good :(",
                            Position = @operator.position,
                        });
                        return "any";
                    }
                }
                else
                { return leftType; }
            }
            else if (@operator.Operator == "=")
            {
                throw new NotImplementedException();
            }
            else
            { throw new ParserException($"Unknown operator '{@operator.Operator}'"); }
        }
        string FindStatementType(Statement_Literal literal)
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
            if (GetCompiledVariable(variable.variableName, out CompiledVariable val, out var isGlob_))
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
            else if (parameters.TryGetValue(variable.variableName, out Parameter param))
            {
                if (variable.listIndex != null)
                { throw new NotImplementedException(); }
                return param.type;
            }
            else if (variable.variableName == "this")
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new ParserException("Unknown variable '" + variable.variableName + "'", new Position(variable.position.Line));
            }
        }
        string FindStatementType(Statement_NewStruct newStruct)
        {
            if (GetCompiledStruct(newStruct, out var structDefinition))
            {
                return structDefinition.name;
            }
            else
            {
                throw new ParserException("Unknown struct '" + newStruct.structName + "'", new Position(newStruct.position.Line));
            }
        }
        string FindStatementType(TypeToken type)
        {
            if (type.typeName == BuiltinType.STRUCT)
            { return type.text; }

            return type.typeName.ToString().ToLower();
        }
        string FindStatementType(Statement_Field field)
        {
            var prevStatementType = FindStatementType(field.PrevStatement);

            foreach (var strct in compiledStructs)
            {
                if (strct.Key != prevStatementType) continue;

                foreach (var sField in strct.Value.fields)
                {
                    if (sField.name != field.FieldName) continue;

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
            { return FindStatementType(index.PrevStatement)[..^2]; }

            throw new NotImplementedException();
        }
        #endregion

        #region PredictStatementValue()
        static Stack.Item? PredictStatementValue(Statement_Operator @operator)
        {
            var leftValue = PredictStatementValue(@operator.Left);
            if (!leftValue.HasValue) return null;

            if (@operator.Operator == "!")
            {
                return new Stack.Item(!leftValue, null);
            }

            if (@operator.Right != null)
            {
                var rightValue = PredictStatementValue(@operator.Right);
                if (!rightValue.HasValue) return null;

                return @operator.Operator switch
                {
                    "+" => leftValue + rightValue,
                    "-" => leftValue - rightValue,
                    "*" => leftValue * rightValue,
                    "/" => leftValue / rightValue,
                    "%" => leftValue % rightValue,

                    "<" => new Stack.Item(leftValue < rightValue, null),
                    ">" => new Stack.Item(leftValue > rightValue, null),
                    "==" => new Stack.Item(leftValue == rightValue, null),
                    "!=" => new Stack.Item(leftValue != rightValue, null),
                    "&" => new Stack.Item(leftValue & rightValue, null),
                    "|" => new Stack.Item(leftValue | rightValue, null),
                    "^" => new Stack.Item(leftValue ^ rightValue, null),
                    "<=" => new Stack.Item(leftValue <= rightValue, null),
                    ">=" => new Stack.Item(leftValue >= rightValue, null),
                    _ => null,
                };
            }
            else
            { return leftValue; }
        }
        static Stack.Item? PredictStatementValue(Statement_Literal literal)
        {
            return literal.type.typeName switch
            {
                BuiltinType.INT => new Stack.Item(int.Parse(literal.value), null),
                BuiltinType.FLOAT => new Stack.Item(float.Parse(literal.value), null),
                BuiltinType.STRING => new Stack.Item(literal.value, null),
                BuiltinType.BOOLEAN => new Stack.Item(bool.Parse(literal.value), null),
                BuiltinType.STRUCT => new Stack.Item(new Stack.Item.UnassignedStruct(), null),
                _ => null,
            };
        }
        static Stack.Item? PredictStatementValue(Statement st)
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
            switch (newVariable.type.typeName)
            {
                case BuiltinType.AUTO:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new ParserException("Unknown variable '" + newVariable.variableName + "'", new Position(newVariable.position.Line));
                            }
                        }
                    }
                    break;
                case BuiltinType.INT:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new ParserException("Unknown variable '" + newVariable.variableName + "'", new Position(newVariable.position.Line));
                            }
                        }
                    }
                    break;
                case BuiltinType.FLOAT:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new ParserException("Unknown variable '" + newVariable.variableName + "'", new Position(newVariable.position.Line));
                            }
                        }
                    }
                    break;
                case BuiltinType.STRING:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new ParserException("Unknown variable '" + newVariable.variableName + "'", new Position(newVariable.position.Line));
                            }
                        }
                    }
                    break;
                case BuiltinType.BOOLEAN:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new ParserException("Unknown variable '" + newVariable.variableName + "'", new Position(newVariable.position.Line));
                            }
                        }
                    }
                    break;
                case BuiltinType.STRUCT:
                    if (newVariable.initialValue != null)
                    {
                        if (newVariable.initialValue is not Statement_Literal)
                        {
                            if (GetCompiledVariable(newVariable.variableName, out CompiledVariable val_, out var isGlob))
                            {
                                GenerateCodeForStatement(newVariable.initialValue);
                                AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, val_.offset);
                            }
                            else
                            {
                                throw new ParserException("Unknown variable '" + newVariable.variableName + "'", new Position(newVariable.position.Line));
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
                    throw new InternalException($"Unknown variable type '{newVariable.type.typeName}'");
            }
        }
        void GenerateCodeForStatement(Statement_FunctionCall functionCall)
        {
            AddInstruction(Opcode.COMMENT, $"{functionCall.FunctionName}():");

            if (functionCall.FunctionName == "return")
            {
                if (functionCall.parameters.Count > 1)
                { throw new ParserException("Wrong number of parameters passed to 'return'", functionCall.position); }
                else if (functionCall.parameters.Count == 1)
                {
                    GenerateCodeForStatement(functionCall.parameters[0]);
                    AddInstruction(Opcode.STORE_VALUE_BR, -2 - parameters.Count - ((isStructMethod) ? 1 : 0));
                }


                returnInstructions.Add(compiledCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);



                return;
            }

            if (functionCall.FunctionName == "break")
            {
                if (breakInstructions.Count <= 0)
                { throw new ParserException("The keyword 'break' does not avaiable in the current context", functionCall.position); }

                breakInstructions.Last().Add(compiledCode.Count);
                AddInstruction(Opcode.JUMP_BY, 0);

                return;
            }

            if (functionCall.FunctionName == "type")
            {
                if (functionCall.parameters.Count != 1)
                { throw new ParserException("Wrong number of parameters passed to 'type'", functionCall.position); }

                GenerateCodeForStatement(functionCall.parameters[0]);
                AddInstruction(Opcode.TYPE_GET);

                return;
            }

            if (functionCall.IsMethodCall)
            {
                if (functionCall.PrevStatement is Statement_Variable prevVar)
                {
                    if (GetCompiledVariable(prevVar.variableName, out var prevVarInfo, out bool isGlobal))
                    {
                        if (prevVarInfo.isList)
                        {
                            AddInstruction(isGlobal ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, prevVarInfo.offset);

                            if (functionCall.FunctionName == "Push")
                            {
                                if (functionCall.parameters.Count != 1)
                                { throw new ParserException("Wrong number of parameters passed to '<list>.Push'", new Position(functionCall.position.Line)); }
                                GenerateCodeForStatement(functionCall.parameters[0]);

                                AddInstruction(Opcode.LIST_PUSH_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Pull")
                            {
                                if (functionCall.parameters.Count != 0)
                                { throw new ParserException("Wrong number of parameters passed to '<list>.Pull'", new Position(functionCall.position.Line)); }

                                AddInstruction(Opcode.LIST_PULL_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Add")
                            {
                                if (functionCall.parameters.Count != 2)
                                { throw new ParserException("Wrong number of parameters passed to '<list>.Add'", new Position(functionCall.position.Line)); }
                                GenerateCodeForStatement(functionCall.parameters[0]);
                                GenerateCodeForStatement(functionCall.parameters[1]);

                                AddInstruction(Opcode.LIST_ADD_ITEM);

                                return;
                            }
                            else if (functionCall.FunctionName == "Remove")
                            {
                                if (functionCall.parameters.Count != 1)
                                { throw new ParserException("Wrong number of parameters passed to '<list>.Remove'", new Position(functionCall.position.Line)); }
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

            if (!GetCompiledFunction(functionCall, out CompiledFunction compiledFunction))
            {
                string searchedID = functionCall.FunctionName;
                searchedID += "(";
                for (int i = 0; i < functionCall.parameters.Count; i++)
                {
                    if (i > 0) { searchedID += ", "; }

                    searchedID += FindStatementType(functionCall.parameters[i]);
                }
                searchedID += ")";

                throw new ParserException("Unknown function " + searchedID + "", new Position(functionCall.position.Line));
            }

            if (functionCall.MethodParameters.Length != compiledFunction.ParameterCount)
            { throw new ParserException("Wrong number of parameters passed to '" + functionCall.FunctionName + "'", functionCall.position); }

            if (functionCall.IsMethodCall != compiledFunction.IsMethod)
            { throw new ParserException($"You called the {((compiledFunction.IsMethod) ? "method" : "function")} '{functionCall.FunctionName}' as {((functionCall.IsMethodCall) ? "method" : "function")}", functionCall.position); }

            if (compiledFunction.IsBuiltin)
            {
                if (!builtinFunctions.TryGetValue(compiledFunction.BuiltinName, out var builtinFunction))
                { throw new ParserException($"Builtin function '{compiledFunction.BuiltinName}' doesn't exists", functionCall.position); }


                if (functionCall.PrevStatement != null)
                { GenerateCodeForStatement(functionCall.PrevStatement); }

                foreach (var param in functionCall.parameters)
                {
                    GenerateCodeForStatement(param);
                }
                AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount, compiledFunction.BuiltinName);

                return;
            }

            if (compiledFunction.returnSomething)
            { AddInstruction(new Instruction(Opcode.PUSH_VALUE, GenerateInitialValue(compiledFunction.type)) { tag = "return value" }); }

            if (functionCall.PrevStatement != null)
            {
                GenerateCodeForStatement(functionCall.PrevStatement);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param.this");
            }

            for (int i = 0; i < functionCall.parameters.Count; i++)
            {
                Statement param = functionCall.parameters[i];
                ParameterDefinition definedParam = compiledFunction.functionDefinition.parameters[i];

                AddInstruction(Opcode.COMMENT, $"param:");
                GenerateCodeForStatement(param);
                AddInstruction(Opcode.DEBUG_SET_TAG, "param." + definedParam.name);
            }

            if (!GetFunctionOffset(functionCall, out var functionCallOffset))
            { undefinedFunctionOffsets.Add(new UndefinedFunctionOffset(compiledCode.Count, functionCall)); }

            AddInstruction(Opcode.CALL, functionCallOffset - compiledCode.Count);

            for (int i = 0; i < functionCall.parameters.Count; i++)
            { AddInstruction(Opcode.POP_VALUE); }

            if (functionCall.PrevStatement != null)
            { AddInstruction(Opcode.POP_VALUE); }
        }
        void GenerateCodeForStatement(Statement_MethodCall structMethodCall)
        {
            if (GetCompiledVariable(structMethodCall.VariableName, out CompiledVariable compiledVariable, out var isGlob3))
            {
                if (compiledVariable.type == BuiltinType.RUNTIME)
                {
                    warnings.Add(new Warning()
                    {
                        Message = $"The type of the variable '{structMethodCall.VariableName}' will be set at runtime. Potential errors may occur.",
                        Position = structMethodCall.variableNameToken.Position,
                    });
                }

                if (compiledVariable.isList)
                {
                    AddInstruction(isGlob3 ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, compiledVariable.offset);

                    if (structMethodCall.FunctionName == "Push")
                    {
                        if (structMethodCall.parameters.Count != 1)
                        { throw new ParserException("Wrong number of parameters passed to '<list>.Push'", new Position(structMethodCall.position.Line)); }
                        GenerateCodeForStatement(structMethodCall.parameters[0]);

                        AddInstruction(Opcode.LIST_PUSH_ITEM);
                    }
                    else if (structMethodCall.FunctionName == "Pull")
                    {
                        if (structMethodCall.parameters.Count != 0)
                        { throw new ParserException("Wrong number of parameters passed to '<list>.Pull'", new Position(structMethodCall.position.Line)); }

                        AddInstruction(Opcode.LIST_PULL_ITEM);
                    }
                    else if (structMethodCall.FunctionName == "Add")
                    {
                        if (structMethodCall.parameters.Count != 2)
                        { throw new ParserException("Wrong number of parameters passed to '<list>.Add'", new Position(structMethodCall.position.Line)); }
                        GenerateCodeForStatement(structMethodCall.parameters[0]);
                        GenerateCodeForStatement(structMethodCall.parameters[1]);

                        AddInstruction(Opcode.LIST_ADD_ITEM);
                    }
                    else if (structMethodCall.FunctionName == "Remove")
                    {
                        if (structMethodCall.parameters.Count != 1)
                        { throw new ParserException("Wrong number of parameters passed to '<list>.Remove'", new Position(structMethodCall.position.Line)); }
                        GenerateCodeForStatement(structMethodCall.parameters[0]);

                        AddInstruction(Opcode.LIST_REMOVE_ITEM);
                    }
                    else
                    {
                        throw new ParserException("Unknown list method " + structMethodCall.FunctionName, new Position(structMethodCall.position.Line));
                    }
                }
                else
                {
                    bool IsStructMethodCall = true;
                    if (!GetCompiledStruct(compiledVariable.structName, out var compiledStruct))
                    { IsStructMethodCall = false; }
                    else
                    {
                        if (!compiledStruct.methods.ContainsKey(structMethodCall.FunctionName))
                        { IsStructMethodCall = false; }
                        else
                        {
                            if (structMethodCall.parameters.Count != compiledStruct.methods[structMethodCall.FunctionName].ParameterCount)
                            { throw new ParserException("Wrong number of parameters passed to '" + structMethodCall.VariableName + "'", structMethodCall.position); }

                            if (compiledStruct.methods[structMethodCall.FunctionName].returnSomething)
                            {
                                AddInstruction(new Instruction(Opcode.PUSH_VALUE, GenerateInitialValue(compiledStruct.methods[structMethodCall.FunctionName].type)) { tag = "return value" });
                            }

                            if (structMethodCall.parameters.Count != compiledStruct.methods[structMethodCall.FunctionName].ParameterCount)
                            { throw new ParserException("Method '" + structMethodCall.VariableName + "' requies " + compiledStruct.methods[structMethodCall.FunctionName].ParameterCount + " parameters", structMethodCall.position); }

                            AddInstruction(new Instruction(isGlob3 ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, compiledVariable.offset) { tag = "struct.this" });
                            compiledVariables.Add("this", new CompiledVariable(compiledVariable.offset, compiledVariable.structName, compiledVariable.isList));

                            foreach (Statement param in structMethodCall.parameters)
                            { GenerateCodeForStatement(param); }

                            if (compiledStruct.methodOffsets.TryGetValue(structMethodCall.FunctionName, out var methodCallOffset))
                            { AddInstruction(Opcode.CALL, methodCallOffset - compiledCode.Count); }
                            else
                            { throw new InternalException($"Method '{compiledVariable.structName}.{structMethodCall.FunctionName}' offset not found"); }

                            for (int i = 0; i < structMethodCall.parameters.Count; i++)
                            { AddInstruction(Opcode.POP_VALUE); }

                            if (compiledVariables.Last().Key == "this")
                            {
                                compiledVariables.Remove("this");
                            }
                            else
                            {
                                throw new InternalException("Can't clear the variable 'this': not found");
                            }

                            if (compiledStruct.methods[structMethodCall.FunctionName].returnSomething)
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
                            { throw new ParserException($"You called the function '{structMethodCall.FunctionName}' as method", structMethodCall.position); }

                            if (compiledFunction.returnSomething)
                            {
                                AddInstruction(new Instruction(Opcode.PUSH_VALUE, GenerateInitialValue(compiledFunction.type)) { tag = "return value" });
                            }

                            if (structMethodCall.parameters.Count + 1 != compiledFunction.ParameterCount)
                            { throw new ParserException("Method '" + structMethodCall.FunctionName + "' requies " + compiledFunction.ParameterCount + " parameters", structMethodCall.position); }

                            AddInstruction(new Instruction(isGlob3 ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, compiledVariable.offset) { tag = "param.this" });
                            foreach (Statement param in structMethodCall.parameters)
                            {
                                GenerateCodeForStatement(param);
                                compiledCode.Last().tag = "param";
                            }
                            if (!GetFunctionOffset(structMethodCall, out var functionCallOffset))
                            {
                                undefinedFunctionOffsets.Add(new UndefinedFunctionOffset(compiledCode.Count, structMethodCall));
                            }
                            AddInstruction(Opcode.CALL, functionCallOffset - compiledCode.Count);
                            AddInstruction(Opcode.POP_VALUE);
                            for (int i = 0; i < structMethodCall.parameters.Count; i++)
                            {
                                AddInstruction(Opcode.POP_VALUE);
                            }
                        }
                        else
                        { throw new ParserException($"Method '{structMethodCall.FunctionName}' is doesn't exists", structMethodCall.position); }
                    }
                }
            }
            else
            { throw new ParserException("Unknown variable '" + structMethodCall.VariableName + "'", structMethodCall.position); }

        }
        void GenerateCodeForStatement(Statement_Operator @operator)
        {
            if (OptimizeCode)
            {
                Stack.Item? predictedValueN = PredictStatementValue(@operator);
                if (predictedValueN.HasValue)
                {
                    var predictedValue = predictedValueN.Value;

                    switch (predictedValue.type)
                    {
                        case Stack.Item.Type.INT:
                            AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueInt);
                            return;
                        case Stack.Item.Type.BOOLEAN:
                            AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueBoolean);
                            return;
                        case Stack.Item.Type.FLOAT:
                            AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueFloat);
                            return;
                        case Stack.Item.Type.STRING:
                            AddInstruction(Opcode.PUSH_VALUE, predictedValue.ValueString);
                            return;
                    }
                }
            }

            Opcode opcode = Opcode.UNKNOWN;

            if (@operator.Operator == "!")
            {
                if (@operator.ParameterCount != 1) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_NOT;
            }
            else if (@operator.Operator == "+")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_ADD;
            }
            else if (@operator.Operator == "<")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_LT;
            }
            else if (@operator.Operator == ">")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_MT;
            }
            else if (@operator.Operator == "-")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_SUB;
            }
            else if (@operator.Operator == "*")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_MULT;
            }
            else if (@operator.Operator == "/")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_DIV;
            }
            else if (@operator.Operator == "%")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.MATH_MOD;
            }
            else if (@operator.Operator == "==")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_EQ;
            }
            else if (@operator.Operator == "!=")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_NEQ;
            }
            else if (@operator.Operator == "&")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_AND;
            }
            else if (@operator.Operator == "|")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_OR;
            }
            else if (@operator.Operator == "^")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_XOR;
            }
            else if (@operator.Operator == "<=")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_LTEQ;
            }
            else if (@operator.Operator == ">=")
            {
                if (@operator.ParameterCount != 2) throw new ParserException("Wrong number of parameters passed to operator '" + @operator.Operator + "'", new Position(@operator.position.Line));
                opcode = Opcode.LOGIC_MTEQ;
            }

            if (opcode != Opcode.UNKNOWN)
            {
                GenerateCodeForStatement(@operator.Left);
                if (@operator.Right != null) GenerateCodeForStatement(@operator.Right);
                AddInstruction(opcode);
            }
            else if (@operator.Operator == "=")
            {
                if (@operator.ParameterCount != 2)
                { throw new ParserException("Wrong number of parameters passed to assigment operator '" + @operator.Operator + "'", new Position(@operator.position.Line)); }

                if (@operator.Left is Statement_Variable variable)
                {
                    if (GetCompiledVariable(variable.variableName, out CompiledVariable valueMemoryIndex, out var isGlob))
                    {
                        GenerateCodeForStatement(@operator.Right);
                        AddInstruction(isGlob ? Opcode.STORE_VALUE : Opcode.STORE_VALUE_BR, valueMemoryIndex.offset);
                    }
                    else if (GetParameter(variable.variableName, out Parameter parameter))
                    {
                        GenerateCodeForStatement(@operator.Right);
                        AddInstruction(parameter.isReference ? Opcode.STORE_VALUE_BR_AS_REF : Opcode.STORE_VALUE_BR, parameter.RealIndex);
                    }
                    else
                    {
                        throw new ParserException("Unknown variable '" + variable.variableName + "'", new Position(variable.position.Line));
                    }
                }
                else if (@operator.Left is Statement_Field field)
                {
                    if (field.PrevStatement is Statement_Variable variable1)
                    {
                        if (GetCompiledVariable(variable1.variableName, out CompiledVariable valueMemoryIndex, out var isGlob))
                        {
                            GenerateCodeForStatement(@operator.Right);
                            AddInstruction(isGlob ? Opcode.STORE_FIELD : Opcode.STORE_FIELD_BR, valueMemoryIndex.offset, field.FieldName);
                        }
                        else if (GetParameter(variable1.variableName, out Parameter parameter))
                        {
                            GenerateCodeForStatement(@operator.Right);
                            AddInstruction(isGlob ? Opcode.STORE_FIELD : Opcode.STORE_FIELD_BR, parameter.RealIndex, field.FieldName);
                        }
                        else
                        {
                            throw new ParserException("Unknown variable '" + variable1.variableName + "'", new Position(field.position.Line));
                        }
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
                else
                {
                    throw new ParserException("Unexpected statement", new Position(@operator.Left.position.Line));
                }
            }
            else
            { throw new ParserException($"Unknown operator '{@operator.Operator}'"); }
        }
        void GenerateCodeForStatement(Statement_Literal literal)
        {
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
            if (GetCompiledVariable(variable.variableName, out CompiledVariable val, out var isGlob_))
            {
                if (variable.reference)
                {
                    AddInstruction(isGlob_ ? Opcode.LOAD_VALUE_AS_REF : Opcode.LOAD_VALUE_BR_AS_REF, val.offset);
                }
                else
                {
                    AddInstruction(isGlob_ ? Opcode.LOAD_VALUE : Opcode.LOAD_VALUE_BR, val.offset);
                }
            }
            else if (parameters.TryGetValue(variable.variableName, out Parameter param))
            {
                if (variable.reference)
                { throw new ParserException("Reference can only be applied to a variable", new Position(variable.position.Line)); }

                AddInstruction((param.isReference) ? Opcode.LOAD_VALUE_BR_AS_REF : Opcode.LOAD_VALUE_BR, param.RealIndex);
            }
            else
            {
                throw new ParserException("Unknown variable '" + variable.variableName + "'", new Position(variable.position.Line));
            }

            if (variable.listIndex != null)
            {
                if (variable.reference)
                { throw new ParserException("Reference cannot be applied to the list", new Position(variable.position.Line)); }
                GenerateCodeForStatement(variable.listIndex);
                AddInstruction(new Instruction(Opcode.LIST_INDEX));
            }
        }
        void GenerateCodeForStatement(Statement_WhileLoop whileLoop)
        {
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
            foreach (var breakInstruction in breakInstructions.Last())
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

            AddInstruction(Opcode.COMMENT, "FOR Condition");
            // Index condition
            int conditionOffsetFor = compiledCode.Count;
            GenerateCodeForStatement(forLoop.condition);
            AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);
            int conditionJumpOffsetFor = compiledCode.Count - 1;

            List<int> breakInstructionsFor = new();

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

            foreach (var breakInstruction in breakInstructionsFor)
            {
                compiledCode[breakInstruction].parameter = compiledCode.Count - breakInstruction;
            }

            AddInstruction(Opcode.COMMENT, "Clear variables");
            for (int x = 0; x < variablesAdded; x++)
            {
                AddInstruction(Opcode.POP_VALUE);
            }

            AddInstruction(Opcode.COMMENT, "}");
        }
        void GenerateCodeForStatement(Statement_If @if)
        {
            List<int> conditionJumpOffsets = new();
            int prevIfJumpOffset = 0;

            foreach (var ifSegment in @if.parts)
            {
                if (ifSegment is Statement_If_If partIf)
                {
                    AddInstruction(Opcode.COMMENT, "if (...) {");

                    AddInstruction(Opcode.COMMENT, "IF Condition");
                    GenerateCodeForStatement(partIf.condition);
                    prevIfJumpOffset = compiledCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    int variableCount = CompileVariables(partIf);

                    AddInstruction(Opcode.COMMENT, "IF Statements");
                    for (int i = 0; i < partIf.statements.Count; i++)
                    {
                        GenerateCodeForStatement(partIf.statements[i]);
                    }

                    ClearVariables(variableCount);

                    AddInstruction(Opcode.COMMENT, "IF Jump to End");
                    conditionJumpOffsets.Add(compiledCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddInstruction(Opcode.COMMENT, "}");

                    compiledCode[prevIfJumpOffset].parameter = compiledCode.Count - prevIfJumpOffset;

                }
                else if (ifSegment is Statement_If_ElseIf partElseif)
                {
                    AddInstruction(Opcode.COMMENT, "elseif (...) {");

                    AddInstruction(Opcode.COMMENT, "ELSEIF Condition");
                    GenerateCodeForStatement(partElseif.condition);
                    prevIfJumpOffset = compiledCode.Count;
                    AddInstruction(Opcode.JUMP_BY_IF_FALSE, 0);

                    int variableCount = CompileVariables(partElseif);

                    AddInstruction(Opcode.COMMENT, "ELSEIF Statements");
                    for (int i = 0; i < partElseif.statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElseif.statements[i]);
                    }

                    ClearVariables(variableCount);

                    AddInstruction(Opcode.COMMENT, "IF Jump to End");
                    conditionJumpOffsets.Add(compiledCode.Count);
                    AddInstruction(Opcode.JUMP_BY, 0);

                    AddInstruction(Opcode.COMMENT, "}");

                    compiledCode[prevIfJumpOffset].parameter = compiledCode.Count - prevIfJumpOffset;
                }
                else if (ifSegment is Statement_If_Else partElse)
                {
                    AddInstruction(Opcode.COMMENT, "else {");

                    AddInstruction(Opcode.COMMENT, "ELSE Statements");

                    int variableCount = CompileVariables(partElse);

                    for (int i = 0; i < partElse.statements.Count; i++)
                    {
                        GenerateCodeForStatement(partElse.statements[i]);
                    }

                    ClearVariables(variableCount);

                    AddInstruction(Opcode.COMMENT, "}");

                    compiledCode[prevIfJumpOffset].parameter = compiledCode.Count - prevIfJumpOffset;
                }
            }

            foreach (var item in conditionJumpOffsets)
            {
                compiledCode[item].parameter = compiledCode.Count - item;
            }
        }
        void GenerateCodeForStatement(Statement_NewStruct newStruct)
        {
            if (GetCompiledStruct(newStruct, out var structDefinition))
            {
                if (structDefinition.IsBuiltin)
                {
                    AddInstruction(Opcode.PUSH_VALUE, structDefinition.CreateBuiltinStruct());
                }
                else
                {
                    Dictionary<string, Stack.Item> fields = new();
                    foreach (ParameterDefinition structDefFieldDefinition in structDefinition.fields)
                    {
                        fields.Add(structDefFieldDefinition.name, new Stack.Item(structDefFieldDefinition.type, null));
                    }
                    AddInstruction(Opcode.PUSH_VALUE, new Stack.Item.Struct(fields));
                }
            }
            else
            {
                throw new ParserException("Unknown struct '" + newStruct.structName + "'", new Position(newStruct.position.Line));
            }
        }
        void GenerateCodeForStatement(Statement_Field field)
        {
            if (field.PrevStatement is Statement_Variable prevVariable)
            {
                if (GetCompiledVariable(prevVariable.variableName, out CompiledVariable variable, out var isGlob))
                {
                    AddInstruction(isGlob ? Opcode.LOAD_FIELD : Opcode.LOAD_FIELD_BR, variable.offset, field.FieldName);
                }
                else if (parameters.TryGetValue(prevVariable.variableName, out Parameter param))
                {
                    AddInstruction(Opcode.LOAD_FIELD_BR, param.RealIndex, field.FieldName);
                }
                else
                {
                    throw new ParserException($"Unknown variable '{prevVariable.variableName}'", prevVariable.position);
                }
                return;
            }

            GenerateCodeForStatement(field.PrevStatement);
            AddInstruction(Opcode.LOAD_FIELD_R, -1, field.FieldName);
        }
        void GenerateCodeForStatement(Statement_Index indexStatement)
        {
            GenerateCodeForStatement(indexStatement.PrevStatement);
            if (indexStatement.indexStatement == null)
            { throw new ParserException($"Index statement for indexer is requied", indexStatement.position); }
            GenerateCodeForStatement(indexStatement.indexStatement);
            AddInstruction(new Instruction(Opcode.LIST_INDEX));
        }
        void GenerateCodeForStatement(Statement_ListValue listValue)
        {
            Stack.Item.Type listType = Stack.Item.Type.RUNTIME;
            for (int i = 0; i < listValue.Size; i++)
            {
                if (listValue.Values[i] is not Statement_Literal literal)
                { throw new ParserException("Only literals are supported in list value"); }
                if (i == 0)
                {
                    listType = literal.type.typeName.Convert();
                    if (listType == Stack.Item.Type.RUNTIME)
                    { throw new ParserException($"Unknown literal type {listType}"); }
                }
                if (literal.type.typeName.Convert() != listType)
                { throw new ParserException($"Wrong literal type {literal.type.typeName}. Expected {listType}"); }
            }
            Stack.Item newList = new(new Stack.Item.List(listType), null);
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
            { variableCount = CompileVariables(statementParent); }

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
            { ClearVariables(variableCount); }
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

        void GenerateCodeForGlobalVariable(Statement st, out int globalVariableSadded)
        {
            int variableCount = 0;

            if (st is Statement_NewVariable newVariable)
            {
                switch (newVariable.type.typeName)
                {
                    case BuiltinType.INT:
                        object initialValue1 = 0;
                        if (newVariable.type.isList)
                        {
                            initialValue1 = new Stack.Item.List(Stack.Item.Type.INT);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == newVariable.type.typeName)
                                { initialValue1 = int.Parse(literal.value); }
                            }
                        }
                        compiledGlobalVariables.Add(newVariable.variableName, new CompiledVariable(variableCount, newVariable.type.typeName, newVariable.type.isList));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue1) { tag = "var." + newVariable.variableName });
                        variableCount++;
                        break;
                    case BuiltinType.FLOAT:
                        object initialValue2 = 0;
                        if (newVariable.type.isList)
                        {
                            initialValue2 = new Stack.Item.List(Stack.Item.Type.FLOAT);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == BuiltinType.FLOAT || literal.type.typeName == IngameCoding.BBCode.BuiltinType.INT)
                                { initialValue2 = float.Parse(literal.value); }
                            }
                        }
                        compiledGlobalVariables.Add(newVariable.variableName, new CompiledVariable(variableCount, newVariable.type.typeName, newVariable.type.isList));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue2) { tag = "var." + newVariable.variableName });
                        variableCount++;
                        break;
                    case BuiltinType.STRING:
                        object initialValue3 = "";
                        if (newVariable.type.isList)
                        {
                            initialValue3 = new Stack.Item.List(Stack.Item.Type.STRING);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == BuiltinType.STRING)
                                { initialValue3 = literal.value; }
                            }
                        }
                        compiledGlobalVariables.Add(newVariable.variableName, new CompiledVariable(variableCount, newVariable.type.typeName, newVariable.type.isList));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue3) { tag = "var." + newVariable.variableName });
                        variableCount++;
                        break;
                    case BuiltinType.BOOLEAN:
                        object initialValue4 = false;
                        if (newVariable.type.isList)
                        {
                            initialValue4 = new Stack.Item.List(Stack.Item.Type.BOOLEAN);
                        }
                        else if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                if (literal.type.typeName == newVariable.type.typeName)
                                { initialValue4 = bool.Parse(literal.value); }
                            }
                        }
                        compiledGlobalVariables.Add(newVariable.variableName, new CompiledVariable(variableCount, newVariable.type.typeName, newVariable.type.isList));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue4) { tag = "var." + newVariable.variableName });
                        variableCount++;
                        break;
                    case BuiltinType.STRUCT:
                        if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_NewStruct newStruct)
                            {
                                if (newStruct.structName == newVariable.type.text)
                                { GenerateCodeForStatement(newStruct); }
                                else
                                { throw new ParserException("Can't cast " + newStruct.structName + " to " + newVariable.type.text, new Position(newStruct.position.Line)); }
                            }
                        }
                        compiledGlobalVariables.Add(newVariable.variableName, new CompiledVariable(variableCount, newVariable.type.text, newVariable.type.isList));
                        variableCount++;
                        break;
                    case BuiltinType.AUTO:
                        if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                newVariable.type.typeName = literal.type.typeName;
                            }
                            else if (newVariable.initialValue is Statement_NewStruct newStruct)
                            {
                                newVariable.type.typeName = BuiltinType.STRUCT;
                                newVariable.type.text = newStruct.structName;
                            }
                        }
                        if (newVariable.type.typeName == BuiltinType.AUTO)
                        { throw new InternalException("Invalid or unimplemented initial value"); }
                        GenerateCodeForGlobalVariable(newVariable, out _);
                        variableCount++;
                        break;
                }
            }

            globalVariableSadded = variableCount;
        }

        void GenerateCodeForVariable(Statement st, out int variablesAdded)
        {
            int variableCount = 0;

            if (st is Statement_NewVariable newVariable)
            {
                switch (newVariable.type.typeName)
                {
                    case BuiltinType.INT:
                        object initialValue1 = 0;
                        if (newVariable.type.isList)
                        {
                            initialValue1 = new Stack.Item.List(IngameCoding.Bytecode.Stack.Item.Type.INT);
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
                        compiledVariables.Add(newVariable.variableName, new CompiledVariable(compiledVariables.Count, newVariable.type.typeName, newVariable.type.isList));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue1) { tag = "var." + newVariable.variableName });
                        variableCount++;
                        break;
                    case BuiltinType.FLOAT:
                        object initialValue2 = 0;
                        if (newVariable.type.isList)
                        {
                            initialValue2 = new Stack.Item.List(Stack.Item.Type.FLOAT);
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
                        compiledVariables.Add(newVariable.variableName, new CompiledVariable(compiledVariables.Count, newVariable.type.typeName, newVariable.type.isList));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue2) { tag = "var." + newVariable.variableName });
                        variableCount++;
                        break;
                    case BuiltinType.STRING:
                        object initialValue3 = "";
                        if (newVariable.type.isList)
                        {
                            initialValue3 = new Stack.Item.List(Stack.Item.Type.STRING);
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
                        compiledVariables.Add(newVariable.variableName, new CompiledVariable(compiledVariables.Count, newVariable.type.typeName, newVariable.type.isList));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue3) { tag = "var." + newVariable.variableName });
                        variableCount++;
                        break;
                    case BuiltinType.BOOLEAN:
                        object initialValue4 = false;
                        if (newVariable.type.isList)
                        {
                            initialValue4 = new Stack.Item.List(Stack.Item.Type.BOOLEAN);
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
                        compiledVariables.Add(newVariable.variableName, new CompiledVariable(compiledVariables.Count, newVariable.type.typeName, newVariable.type.isList));
                        AddInstruction(new Instruction(Opcode.PUSH_VALUE, initialValue4) { tag = "var." + newVariable.variableName });
                        variableCount++;
                        break;
                    case BuiltinType.STRUCT:
                        if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_NewStruct literal)
                            {
                                if (literal.structName == newVariable.type.text)
                                {
                                    GenerateCodeForStatement(literal);
                                    compiledCode.Last().tag = "var." + newVariable.variableName;
                                    compiledVariables.Add(newVariable.variableName, new CompiledVariable(compiledVariables.Count, literal.structName, newVariable.type.isList));
                                }
                                else
                                {
                                    throw new ParserException("Can't cast " + literal.structName + " to " + newVariable.type.text, new Position(literal.position.Line));
                                }
                            }
                        }
                        else
                        {
                            compiledVariables.Add(newVariable.variableName, new CompiledVariable(compiledVariables.Count, newVariable.type.text, newVariable.type.isList));
                            AddInstruction(new Instruction(Opcode.PUSH_VALUE, new Stack.Item.UnassignedStruct()) { tag = "var." + newVariable.variableName });
                        }
                        variableCount++;
                        break;
                    case BuiltinType.AUTO:
                        if (newVariable.initialValue != null)
                        {
                            if (newVariable.initialValue is Statement_Literal literal)
                            {
                                newVariable.type.typeName = literal.type.typeName;
                            }
                            else if (newVariable.initialValue is Statement_NewStruct newStruct)
                            {
                                newVariable.type.typeName = BuiltinType.STRUCT;
                                newVariable.type.text = newStruct.structName;
                            }
                            else
                            { throw new ParserException("Expected literal or new struct as initial value for variable", new Position(newVariable.position.Line)); }
                        }
                        else
                        { throw new ParserException("Expected literal or new struct as initial value for variable", new Position(newVariable.position.Line)); }
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
                            { throw new ParserException("Expected literal or new struct as initial value for variable", new Position(newVariable.position.Line)); }
                        }
                        break;
                    case BuiltinType.VOID:
                    case BuiltinType.ANY:
                    default:
                        throw new ParserException($"Unknown variable type '{newVariable.type.typeName}'", new Position(newVariable.position.Line));
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
            this.isStructMethod = isMethod;

            if (GetFunctionInfo(function, isMethod).IsBuiltin) return;

            parameters.Clear();
            compiledVariables.Clear();
            returnInstructions.Clear();

            functionOffsets.Add(function.Value.ID(), compiledCode.Count);
            if (isMethod)
            { compiledStructs[compiledStructs.Last().Key].methodOffsets.Add(function.Value.FullName, compiledCode.Count); }

            // Compile parameters
            int paramIndex = 0;
            foreach (ParameterDefinition parameter in function.Value.parameters)
            {
                paramIndex++;
                parameters.Add(parameter.name, new Parameter(paramIndex, parameter.name, parameter.withRefKeyword, function.Value.parameters.Count, parameter.type.ToString()));
            }

            // Search for variables
            int variableCount = 0;
            AddInstruction(Opcode.COMMENT, "Variables");
            GenerateCodeForVariable(function.Value.statements, out variableCount);
            if (variableCount == 0 && AddCommentsToCode)
            { compiledCode.RemoveAt(compiledCode.Count - 1); }

            // Compile statements
            if (function.Value.statements.Count > 0)
            {
                AddInstruction(Opcode.COMMENT, "Statements");
                foreach (Statement statement in function.Value.statements)
                {
                    GenerateCodeForStatement(statement);
                }
            }

            int cleanupCodeOffset = compiledCode.Count;

            foreach (var returnCommandJumpInstructionIndex in returnInstructions)
            {
                compiledCode[returnCommandJumpInstructionIndex].parameter = cleanupCodeOffset - returnCommandJumpInstructionIndex;
            }

            // Clear variables
            if (variableCount > 0)
            {
                AddInstruction(Opcode.COMMENT, "Clear variables");
                for (int x = 0; x < variableCount; x++)
                {
                    AddInstruction(Opcode.POP_VALUE);
                }
            }

            AddInstruction(Opcode.COMMENT, "Return");
            AddInstruction(Opcode.RETURN);

            parameters.Clear();
            compiledVariables.Clear();
            returnInstructions.Clear();

            this.isStructMethod = false;
        }

        void GenerateCodeForStruct(KeyValuePair<string, StructDefinition> @struct, Dictionary<string, Func<Stack.IStruct>> builtinStructs)
        {
            if (compiledFunctions.ContainsKey(@struct.Value.FullName))
            { throw new ParserException($"Struct with name '{@struct.Value.FullName}' already exist"); }

            Dictionary<string, AttributeValues> attributes = new();

            foreach (var attribute in @struct.Value.attributes)
            {
                AttributeValues newAttribute = new()
                {
                    parameters = new()
                };
                if (attribute.Parameters != null)
                {
                    foreach (var parameter in attribute.Parameters)
                    {
                        newAttribute.parameters.Add(new Literal(parameter));
                    }
                }
                attributes.Add(attribute.Name, newAttribute);
            }

            if (attributes.TryGetValue("Builtin", out var attributeBuiltin))
            {
                if (attributeBuiltin.parameters.Count != 1)
                { throw new ParserException("Attribute 'Builtin' requies 1 string parameter"); }
                if (attributeBuiltin.TryGetValue(0, out string paramBuiltinName))
                {
                    foreach (var builtinStruct in builtinStructs)
                    {
                        if (builtinStruct.Key.ToLower() == paramBuiltinName.ToLower())
                        {
                            this.compiledStructs.Add(@struct.Key, new CompiledStruct(
                                @struct.Key,
                                @struct.Value.fields,
                                new Dictionary<string, CompiledFunction>()
                                )
                            {
                                attributes = attributes,
                                CreateBuiltinStruct = builtinStruct.Value
                            });

                            foreach (var method in @struct.Value.methods)
                            {
                                if (compiledFunctions.ContainsKey(method.Key))
                                { throw new ParserException($"Function with name '{method.Key}' already defined"); }

                                var methodInfo = GetFunctionInfo(method, true);

                                this.compiledFunctions.Add(method.Value.FullName, methodInfo);

                                AddInstruction(Opcode.COMMENT, @struct.Value.FullName + "." + method.Value.FullName + ((method.Value.parameters.Count > 0) ? "(...)" : "()") + " {");
                                GenerateCodeForFunction(method, true);
                                AddInstruction(Opcode.COMMENT, "}");
                                this.compiledStructs.Last().Value.methods.Add(method.Key, methodInfo);
                            }

                            return;
                        }
                    }
                    throw new ParserException("'Builtin' struct '" + paramBuiltinName.ToLower() + "' not found");
                }
                else
                { throw new ParserException("Attribute 'Builtin' requies 1 string parameter"); }
            }

            this.compiledStructs.Add(@struct.Key, new CompiledStruct(
                @struct.Key,
                @struct.Value.fields,
                new Dictionary<string, CompiledFunction>()
                )
            { attributes = attributes });

            foreach (var method in @struct.Value.methods)
            {
                if (compiledFunctions.ContainsKey(method.Key))
                { throw new ParserException($"Function with name '{method.Key}' already defined"); }

                var methodInfo = GetFunctionInfo(method, true);
                methodInfo.IsMethod = true;
                this.compiledFunctions.Add(method.Value.FullName, methodInfo);

                AddInstruction(Opcode.COMMENT, @struct.Value.FullName + "." + method.Value.FullName + ((method.Value.parameters.Count > 0) ? "(...)" : "()") + " {");
                GenerateCodeForFunction(method, true);
                AddInstruction(Opcode.COMMENT, "}");
                this.compiledStructs.Last().Value.methods.Add(method.Key, methodInfo);
            }
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

            foreach (var attribute in function.Value.attributes)
            {
                AttributeValues newAttribute = new()
                {
                    parameters = new()
                };
                if (attribute.Parameters != null)
                {
                    foreach (var parameter in attribute.Parameters)
                    {
                        newAttribute.parameters.Add(new Literal(parameter));
                    }
                }
                attributes.Add(attribute.Name, newAttribute);
            }

            if (attributes.TryGetValue("Builtin", out var attributeBuiltin))
            {
                if (attributeBuiltin.parameters.Count != 1)
                { throw new ParserException("Attribute 'Builtin' requies 1 string parameter"); }
                if (attributeBuiltin.TryGetValue(0, out string paramBuiltinName))
                {
                    foreach (var builtinFunction in builtinFunctions)
                    {
                        if (builtinFunction.Key.ToLower() == paramBuiltinName.ToLower())
                        {
                            if (builtinFunction.Value.ParameterCount != function.Value.parameters.Count + (isStructMethod ? 1 : 0))
                            { throw new ParserException("Wrong number of parameters passed to builtin function '" + builtinFunction.Key + "'"); }
                            if (builtinFunction.Value.returnSomething != (function.Value.type.typeName != BuiltinType.VOID))
                            { throw new ParserException("Wrong type definied for builtin function '" + builtinFunction.Key + "'"); }

                            for (int i = 0; i < builtinFunction.Value.parameters.Length; i++)
                            {
                                if (builtinFunction.Value.parameters[i].typeName == BuiltinType.ANY) continue;

                                if (builtinFunction.Value.parameters[i].typeName != function.Value.parameters[i].type.typeName)
                                { throw new ParserException("Wrong type of parameter passed to builtin function '" + builtinFunction.Key + $"'. Parameter index: {i} Requied type: {builtinFunction.Value.parameters[i].typeName.ToString().ToLower()} Passed: {function.Value.parameters[i].type.typeName.ToString().ToLower()}"); }
                            }

                            return new CompiledFunction()
                            {
                                parameters = builtinFunction.Value.parameters,
                                returnSomething = builtinFunction.Value.returnSomething,
                                attributes = attributes,
                            };
                        }
                    }
                    throw new ParserException("'Builtin' function '" + paramBuiltinName.ToLower() + "' not found");
                }
                else
                { throw new ParserException("Attribute 'Builtin' requies 1 string parameter"); }
            }

            return new CompiledFunction(
                function.Value.parameters.ToArray(),
                function.Value.type.typeName != BuiltinType.VOID,
                (function.Value.parameters.Count > 0) && function.Value.parameters.First().withThisKeyword,
                function.Value.type.Clone()
                )
            { attributes = attributes, functionDefinition = function.Value };
        }

        int AnalyzeFunctions(Dictionary<string, FunctionDefinition> functions, List<Statement_NewVariable> globalVariables, Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            this.compiledFunctions = new();

            foreach (var function in functions)
            {
                var id = function.Value.ID();

                if (this.compiledFunctions.ContainsKey(id))
                { throw new ParserException($"Function with name '{id}' already defined"); }

                this.compiledFunctions.Add(id, GetFunctionInfo(function));
            }

            // Remove unused functions
            {
                void AnalyzeStatements(List<Statement> statements)
                {
                    int variablesAdded = 0;
                    foreach (var st in statements)
                    {
                        if (st is Statement_NewVariable newVar)
                        {
                            this.compiledVariables.Add(newVar.variableName, new CompiledVariable()
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
                            this.compiledVariables.Add(forLoop.variableDeclaration.variableName, new CompiledVariable()
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
                    }
                    else if (st is Statement_If_ElseIf st3)
                    {
                        AnalyzeStatement(st3.condition);
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
                        if (st8.FunctionName == "return")
                        { return; }

                        if (GetCompiledFunction(st8, out var cf))
                        { cf.TimesUsed++; }

                        foreach (var st9 in st8.parameters)
                        { AnalyzeStatement(st9); }
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

                    foreach (ParameterDefinition parameter in f.Value.parameters)
                    {
                        parameters.Add(parameter.name, new Parameter(-1, parameter.name, parameter.withRefKeyword, -1, parameter.type.ToString()));
                    }



                    AnalyzeStatements(f.Value.statements);

                    parameters.Clear();
                }
            }

            int functionsRemoved = 0;

            for (int i = functions.Count - 1; i >= 0; i--)
            {
                if (!this.compiledFunctions.TryGetValue(functions.ElementAt(i).Value.ID(), out var f)) continue;
                if (f.TimesUsed > 0) continue;
                foreach (var attr in f.attributes)
                {
                    if (attr.Key == "CodeEntry") goto JumpOut;
                    if (attr.Key == "Catch") goto JumpOut;
                }

                functions.Remove(functions.ElementAt(i).Key);
                functionsRemoved++;

            JumpOut:;
            }

            return functionsRemoved;
        }

        internal CodeGeneratorResult GenerateCode(
            Dictionary<string, FunctionDefinition> functions,
            Dictionary<string, StructDefinition> structs,
            List<Statement_NewVariable> globalVariables,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            Dictionary<string, Func<Stack.IStruct>> builtinStructs,
            Compiler.CompilerSettings settings,
            Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            this.AddCommentsToCode = settings.GenerateComments;
            this.compiledStructs = new();
            this.compiledGlobalVariables = new();
            this.functionOffsets = new();
            this.compiledCode = new();
            this.builtinFunctions = builtinFunctions;
            this.OptimizeCode = !settings.DontOptimize;

            int iterations = settings.RemoveUnusedFunctionsMaxIterations;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int functionsRemoved = AnalyzeFunctions(functions, globalVariables, printCallback);
                if (functionsRemoved == 0) break;

                printCallback?.Invoke($"  Removed {functionsRemoved} unused functions (iteration {iteration})", TerminalInterpreter.LogType.Debug);
            }

            #region Code Generation

            BlockCodeGeneration = false;

            foreach (var @struct in structs)
            { GenerateCodeForStruct(@struct, builtinStructs); }

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
                AddInstruction(Opcode.COMMENT, function.Value.FullName + ((function.Value.parameters.Count > 0) ? "(...)" : "()") + " {" + ((function.Value.statements.Count > 0) ? "" : " }"));
                GenerateCodeForFunction(function, false);
                if (function.Value.statements.Count > 0) AddInstruction(Opcode.COMMENT, "}");
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
                if (GetFunctionOffset(item.functionCallStatement, out var functionCallOffset))
                { compiledCode[item.callInstructionIndex].parameter = functionCallOffset - item.callInstructionIndex; }
                else
                { throw new InternalException($"Function '{item.functionCallStatement.TargetNamespacePathPrefix + item.functionCallStatement.FunctionName}' offset not found"); }
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