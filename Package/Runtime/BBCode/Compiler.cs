using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IngameCoding.BBCode
{
    using BBCode.Parser;
    using BBCode.Parser.Statements;

    using Bytecode;

    using Core;

    using Errors;

    using Terminal;

    static class Extensions
    {
        public static Stack.Item.Type Convert(this BuiltinType v)
        {
            switch (v)
            {
                case BuiltinType.INT:
                    return Stack.Item.Type.INT;
                case BuiltinType.FLOAT:
                    return Stack.Item.Type.FLOAT;
                case BuiltinType.STRING:
                    return Stack.Item.Type.STRING;
                case BuiltinType.BOOLEAN:
                    return Stack.Item.Type.BOOLEAN;
                case BuiltinType.STRUCT:
                    return Stack.Item.Type.STRUCT;
                default:
                    return Stack.Item.Type.RUNTIME;
            }
        }
        public static BuiltinType Convert(this Stack.Item.Type v)
        {
            switch (v)
            {
                case Stack.Item.Type.INT:
                    return BuiltinType.INT;
                case Stack.Item.Type.FLOAT:
                    return BuiltinType.FLOAT;
                case Stack.Item.Type.STRING:
                    return BuiltinType.STRUCT;
                case Stack.Item.Type.BOOLEAN:
                    return BuiltinType.BOOLEAN;
                case Stack.Item.Type.STRUCT:
                    return BuiltinType.STRUCT;
                case Stack.Item.Type.LIST:
                    return BuiltinType.RUNTIME;
                case Stack.Item.Type.RUNTIME:
                    return BuiltinType.RUNTIME;
                default:
                    return BuiltinType.ANY;
            }
        }
    }

    public class Compiler
    {
        #region Fields

        Dictionary<string, CompiledStruct> compiledStructs;
        Dictionary<string, CompiledFunction> compiledFunctions;

        Dictionary<string, int> functionOffsets;

        Dictionary<string, BuiltinFunction> builtinFunctions;
        Dictionary<string, CompiledVariable> compiledGlobalVariables;
        readonly Dictionary<string, CompiledVariable> compiledVariables = new();
        readonly Dictionary<string, Parameter> parameters = new();
        public List<Warning> warnings;

        readonly List<int> returnInstructions = new();
        readonly List<List<int>> breakInstructions = new();

        bool isStructMethod;

        List<Instruction> compiledCode;

        readonly List<UndefinedFunctionOffset> undefinedFunctionOffsets = new();

        bool AddCommentsToCode = true;

        #endregion

        #region Data Structures

        struct UndefinedFunctionOffset
        {
            public int callInstructionIndex;
            public Statement_FunctionCall functionCallStatement;

            public UndefinedFunctionOffset(int callInstructionIndex, Statement_FunctionCall functionCallStatement)
            {
                this.callInstructionIndex = callInstructionIndex;
                this.functionCallStatement = functionCallStatement;
            }
        }

        public struct AttributeValues
        {
            public List<Literal> parameters;

            public bool TryGetValue(int index, out string value)
            {
                value = string.Empty;
                if (parameters == null) return false;
                if (parameters.Count <= index) return false;
                if (parameters[index].type == Literal.Type.String)
                {
                    value = parameters[index].ValueString;
                }
                return true;
            }
            public bool TryGetValue(int index, out int value)
            {
                value = 0;
                if (parameters == null) return false;
                if (parameters.Count <= index) return false;
                if (parameters[index].type == Literal.Type.Integer)
                {
                    value = parameters[index].ValueInt;
                }
                return true;
            }
            public bool TryGetValue(int index, out float value)
            {
                value = 0;
                if (parameters == null) return false;
                if (parameters.Count <= index) return false;
                if (parameters[index].type == Literal.Type.Float)
                {
                    value = parameters[index].ValueFloat;
                }
                return true;
            }
            public bool TryGetValue(int index, out bool value)
            {
                value = false;
                if (parameters == null) return false;
                if (parameters.Count <= index) return false;
                if (parameters[index].type == Literal.Type.Boolean)
                {
                    value = parameters[index].ValueBool;
                }
                return true;
            }
        }

        public struct Literal
        {
            public enum Type
            {
                Integer,
                Float,
                String,
                Boolean,
            }

            public readonly int ValueInt;
            public readonly float ValueFloat;
            public readonly string ValueString;
            public readonly bool ValueBool;
            public readonly Type type;

            public Literal(int value)
            {
                this.type = Type.Integer;

                this.ValueInt = value;
                this.ValueFloat = 0;
                this.ValueString = string.Empty;
                this.ValueBool = false;
            }
            public Literal(float value)
            {
                this.type = Type.Float;

                this.ValueInt = 0;
                this.ValueFloat = value;
                this.ValueString = string.Empty;
                this.ValueBool = false;
            }
            public Literal(string value)
            {
                this.type = Type.String;

                this.ValueInt = 0;
                this.ValueFloat = 0;
                this.ValueString = value;
                this.ValueBool = false;
            }
            public Literal(bool value)
            {
                this.type = Type.Boolean;

                this.ValueInt = 0;
                this.ValueFloat = 0;
                this.ValueString = string.Empty;
                this.ValueBool = value;
            }
            public Literal(object value)
            {
                this.ValueInt = 0;
                this.ValueFloat = 0;
                this.ValueString = string.Empty;
                this.ValueBool = false;

                if (value is int @int)
                {
                    this.type = Type.Integer;
                    this.ValueInt = @int;
                }
                else if (value is float @float)
                {
                    this.type = Type.Float;
                    this.ValueFloat = @float;
                }
                else if (value is string @string)
                {
                    this.type = Type.String;
                    this.ValueString = @string;
                }
                else if (value is bool @bool)
                {
                    this.type = Type.Boolean;
                    this.ValueBool = @bool;
                }
                else
                {
                    throw new System.Exception($"Invalid type '{value.GetType().FullName}'");
                }
            }
        }

        [Serializable]
        public class CompiledFunction
        {
            public TypeToken[] parameters;

            public FunctionDefinition functionDefinition;

            public int TimesUsed = 0;

            public int ParameterCount => parameters.Length;
            public bool returnSomething;

            /// <summary>
            /// the first parameter is labeled as 'this'
            /// </summary>
            public bool IsMethod;

            public Dictionary<string, AttributeValues> attributes;

            public bool IsBuiltin
            {
                get
                {
                    return attributes.ContainsKey("Builtin");
                }
            }
            public string BuiltinName
            {
                get
                {
                    if (attributes.TryGetValue("Builtin", out var attributeValues))
                    {
                        if (attributeValues.TryGetValue(0, out string builtinName))
                        {
                            return builtinName;
                        }
                    }
                    return string.Empty;
                }
            }

            public TypeToken type;

            public CompiledFunction()
            {

            }
            public CompiledFunction(TypeToken[] parameters, bool returnSomething, bool isMethod, TypeToken type)
            {
                this.parameters = parameters;
                this.returnSomething = returnSomething;
                this.IsMethod = isMethod;
                this.type = type;
                this.attributes = new();
                this.functionDefinition = null;
            }
            public CompiledFunction(ParameterDefinition[] parameters, bool returnSomething, bool isMethod, TypeToken type)
            {
                List<TypeToken> @params = new();

                foreach (var param in parameters)
                {
                    @params.Add(param.type);
                }

                this.parameters = @params.ToArray();
                this.returnSomething = returnSomething;
                this.IsMethod = isMethod;
                this.type = type;
                this.attributes = new();
                this.functionDefinition = null;
            }
        }

        public class BuiltinFunction
        {
            public TypeToken[] parameters;

            readonly Action<Stack.Item[]> callback;

            public delegate void ReturnEventHandler(Stack.Item returnValue);
            public event ReturnEventHandler ReturnEvent;

            public int ParameterCount { get { return parameters.Length; } }
            public bool returnSomething;

            // Wrap the event in a protected virtual method
            // to enable derived classes to raise the event.
            public void RaiseReturnEvent(Stack.Item returnValue)
            {
                // Raise the event in a thread-safe manner using the ?. operator.
                ReturnEvent?.Invoke(returnValue);
            }

            /// <summary>
            /// Function without return value
            /// </summary>
            /// <param name="callback">Callback when the machine process this function</param>
            public BuiltinFunction(Action<IngameCoding.Bytecode.Stack.Item[]> callback, TypeToken[] parameters, bool returnSomething = false)
            {
                this.parameters = parameters;
                this.callback = callback;
                this.returnSomething = returnSomething;
            }

            public void Callback(Stack.Item[] parameters)
            {
                if (returnSomething)
                {
                    if (callback != null)
                    {
                        callback(parameters);
                    }
                    else
                    {
                        throw new InternalException("No OnDone");
                    }
                }
                else
                {
                    callback(parameters);
                }
            }
        }
        internal struct CompiledVariable
        {
            public int offset;
            public BuiltinType type;
            public string structName;
            public bool isList;

            public CompiledVariable(int offset, BuiltinType type, bool isList)
            {
                this.offset = offset;
                this.type = type;
                this.structName = string.Empty;
                this.isList = isList;
            }

            public CompiledVariable(int offset, string structName, bool isList)
            {
                this.offset = offset;
                this.type = BuiltinType.STRUCT;
                this.structName = structName;
                this.isList = isList;
            }

            public string Type
            {
                get
                {
                    if (type == BuiltinType.STRUCT)
                    {
                        return structName + (isList ? "[]" : "");
                    }
                    return type.ToString().ToLower() + (isList ? "[]" : "");
                }
            }
        }
        [Serializable]
        public class CompiledStruct
        {
            public Func<Stack.IStruct> CreateBuiltinStruct;
            public bool IsBuiltin => CreateBuiltinStruct != null;
            public string name;
            public ParameterDefinition[] fields;
            public Dictionary<string, CompiledFunction> methods;
            internal Dictionary<string, AttributeValues> attributes;
            public readonly Dictionary<string, int> methodOffsets;

            public CompiledStruct(string name, List<ParameterDefinition> fields, Dictionary<string, CompiledFunction> methods)
            {
                this.name = name;
                this.fields = fields.ToArray();
                this.methods = methods;
                this.methodOffsets = new();
                this.attributes = new();
                this.CreateBuiltinStruct = null;
            }
        }

        class Parameter
        {
            public int index;
            public string name;
            public bool isReference;
            readonly int allParamCount;
            public readonly string type;

            public Parameter(int index, string name, bool isReference, int allParamCount, string type)
            {
                this.index = index;
                this.name = name;
                this.isReference = isReference;
                this.allParamCount = allParamCount;
                this.type = type;
            }

            public override string ToString()
            {
                return $"{((isReference) ? "ref " : "")} {index} {name}";
            }

            public int RealIndex
            {
                get
                {
                    var v = -1 - ((allParamCount + 1) - (index));
                    return v;
                }
            }
        }

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
            if (compiledFunctions.TryGetValue(functionCallStatement.FunctionName, out compiledFunction))
            { return true; }

            if (compiledFunctions.TryGetValue(functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out compiledFunction))
            { return true; }

            var xd = TryGetFunctionNamespacePath(functionCallStatement);

            if (compiledFunctions.TryGetValue(xd + functionCallStatement.FunctionName, out compiledFunction))
            {
                if (xd.EndsWith("."))
                { xd = xd[..^1]; }
                functionCallStatement.IsMethodCall = false;
                functionCallStatement.NamespacePathPrefix = xd;
                functionCallStatement.PrevStatement = null;
                return true;
            }

            if (compiledFunctions.TryGetValue(xd + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out compiledFunction))
            {
                if (xd.EndsWith("."))
                { xd = xd[..^1]; }
                functionCallStatement.IsMethodCall = false;
                functionCallStatement.NamespacePathPrefix = xd;
                functionCallStatement.PrevStatement = null;
                return true;
            }

            if (compiledFunctions.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.FunctionName, out compiledFunction))
            { return true; }

            if (compiledFunctions.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out compiledFunction))
            { return true; }

            return false;
        }

        static string TryGetFunctionNamespacePath(Statement_FunctionCall functionCallStatement)
        {
            static string[] Get(Statement statement)
            {
                if (statement is Statement_Variable s1)
                { return new string[] { s1.variableName }; }
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

        public bool GetFunctionOffset(Statement_FunctionCall functionCallStatement, out int functionOffset)
        {
            if (functionOffsets.TryGetValue(functionCallStatement.FunctionName, out functionOffset))
            {
                return true;
            }
            else if (functionOffsets.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
            {
                return true;
            }
            else if (functionOffsets.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
            {
                return true;
            }
            else if (functionOffsets.TryGetValue(functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
            {
                return true;
            }
            functionOffset = -1;
            return false;
        }
        public bool GetFunctionOffset(Statement_MethodCall methodCallStatement, out int functionOffset)
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
                    if (functionOffsets.TryGetValue(methodCallStatement.FunctionName, out functionOffset))
                    {
                        return true;
                    }
                    else if (functionOffsets.TryGetValue(methodCallStatement.NamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                    {
                        return true;
                    }
                    else if (functionOffsets.TryGetValue(methodCallStatement.NamespacePathPrefix + methodCallStatement.TargetNamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                    {
                        return true;
                    }
                    else if (functionOffsets.TryGetValue(methodCallStatement.TargetNamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                    {
                        return true;
                    }
                }
            }

            functionOffset = -1;
            return false;
        }

        public bool GetFunctionOffset(FunctionDefinition functionCallStatement, out int functionOffset)
        {
            if (functionOffsets.TryGetValue(functionCallStatement.Name, out functionOffset))
            {
                return true;
            }
            else if (functionOffsets.TryGetValue(functionCallStatement.FullName, out functionOffset))
            {
                return true;
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
            if (AddCommentsToCode || instruction.opcode != Opcode.COMMENT)
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


#if false

        string FindStatementType(Statement_FunctionCall functionCall)
        {
            throw new NotImplementedException();
        }
        string FindStatementType(Statement_MethodCall structMethodCall)
        {
            throw new NotImplementedException();
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
                        throw new NotImplementedException();
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
        string FindStatementType(Statement_StructField structField)
        {
            if (GetCompiledVariable(structField.variableName, out CompiledVariable structMemoryAddress1, out var isGlob2))
            {
                throw new NotImplementedException();
            }
            else if (parameters.TryGetValue(structField.variableName, out Parameter param))
            {
                return param.type;
            }
            else if (structField.variableName == "this")
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new ParserException("Unknown variable '" + structField.variableName + "'", new Position(structField.position.Line));
            }
        }

        string? FindStatementType(Statement st)
        {
            if (st is Statement_FunctionCall functionCall)
            {
                if (st is Statement_MethodCall structMethodCall)
                { return FindStatementType(structMethodCall); }
                else
                { return FindStatementType(functionCall); }
            }
            else if (st is Statement_Operator @operator)
            { return FindStatementType(@operator); }
            else if (st is Statement_Literal literal)
            { return FindStatementType(literal); }
            else if (st is Statement_Variable variable)
            { return FindStatementType(variable); }
            else if (st is Statement_NewStruct newStruct)
            { return FindStatementType(newStruct); }
            else if (st is Statement_StructField structField)
            { return FindStatementType(structField); }
            return null;
        }

#endif

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

            if (!GetCompiledFunction(functionCall, out CompiledFunction compiledFunction))
            { throw new ParserException("Unknown function '" + functionCall.FunctionName + "'", new Position(functionCall.position.Line)); }

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
                    compiledCode.Last().tag = "param";
                }
                AddInstruction(Opcode.CALL_BUILTIN, builtinFunction.ParameterCount, compiledFunction.BuiltinName);

                return;
            }

            if (compiledFunction.returnSomething)
            { AddInstruction(new Instruction(Opcode.PUSH_VALUE, GenerateInitialValue(compiledFunction.type)) { tag = "return value" }); }

            if (functionCall.PrevStatement != null)
            { GenerateCodeForStatement(functionCall.PrevStatement); }

            foreach (Statement param in functionCall.parameters)
            {
                GenerateCodeForStatement(param);
                compiledCode.Last().tag = "param";
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
                    AddInstruction(Opcode.PUSH_VALUE, float.Parse(literal.value));
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

            AddInstruction(Opcode.COMMENT, "FOR Expression");
            // Index expression
            GenerateCodeForStatement(forLoop.expression);
            List<int> breakInstructionsFor = new();

            AddInstruction(Opcode.COMMENT, "Statements {");
            for (int i = 0; i < forLoop.statements.Count; i++)
            {
                Statement currStatement = forLoop.statements[i];
                GenerateCodeForStatement(currStatement);
            }

            AddInstruction(Opcode.COMMENT, "}");

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
            throw new NotImplementedException();
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

        void GenerateCodeForFunction(KeyValuePair<string, FunctionDefinition> function, bool isMethod)
        {
            this.isStructMethod = isMethod;

            if (GetFunctionInfo(function, isMethod).IsBuiltin) return;

            parameters.Clear();
            compiledVariables.Clear();
            returnInstructions.Clear();

            functionOffsets.Add(function.Value.FullName, compiledCode.Count);
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
            foreach (var statement in function.Value.statements)
            {
                GenerateCodeForVariable(statement, out int newVariableCount);
                variableCount += newVariableCount;
            }
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

        public struct CompilerResult
        {
            public Instruction[] compiledCode;

            public Dictionary<string, CompiledFunction> compiledFunctions;
            public Dictionary<string, CompiledStruct> compiledStructs;
            internal Dictionary<string, CompiledVariable> compiledGlobalVariables;
            internal Dictionary<string, CompiledVariable> compiledVariables;

            public Dictionary<string, int> functionOffsets;
            public int clearGlobalVariablesInstruction;
            public int setGlobalVariablesInstruction;

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

            bool GetCompiledStruct(string structName, out CompiledStruct compiledStruct)
            { return compiledStructs.TryGetValue(structName, out compiledStruct); }

            public bool GetFunctionOffset(Statement_FunctionCall functionCallStatement, out int functionOffset)
            {
                if (functionOffsets.TryGetValue(functionCallStatement.FunctionName, out functionOffset))
                {
                    return true;
                }
                else if (functionOffsets.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
                {
                    return true;
                }
                else if (functionOffsets.TryGetValue(functionCallStatement.NamespacePathPrefix + functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
                {
                    return true;
                }
                else if (functionOffsets.TryGetValue(functionCallStatement.TargetNamespacePathPrefix + functionCallStatement.FunctionName, out functionOffset))
                {
                    return true;
                }
                functionOffset = -1;
                return false;
            }
            public bool GetFunctionOffset(Statement_MethodCall methodCallStatement, out int functionOffset)
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
                        if (functionOffsets.TryGetValue(methodCallStatement.FunctionName, out functionOffset))
                        {
                            return true;
                        }
                        else if (functionOffsets.TryGetValue(methodCallStatement.NamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                        {
                            return true;
                        }
                        else if (functionOffsets.TryGetValue(methodCallStatement.NamespacePathPrefix + methodCallStatement.TargetNamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                        {
                            return true;
                        }
                        else if (functionOffsets.TryGetValue(methodCallStatement.TargetNamespacePathPrefix + methodCallStatement.FunctionName, out functionOffset))
                        {
                            return true;
                        }
                    }
                }

                functionOffset = -1;
                return false;
            }

            public bool GetFunctionOffset(FunctionDefinition functionCallStatement, out int functionOffset)
            {
                if (functionOffsets.TryGetValue(functionCallStatement.Name, out functionOffset))
                {
                    return true;
                }
                else if (functionOffsets.TryGetValue(functionCallStatement.FullName, out functionOffset))
                {
                    return true;
                }
                functionOffset = -1;
                return false;
            }

            internal void WriteToConsole()
            {
                Console.WriteLine("\n\r === INSTRUCTIONS ===\n\r");
                int indent = 0;

                for (int i = 0; i < this.compiledCode.Length; i++)
                {
                    if (this.clearGlobalVariablesInstruction == i)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine("ClearGlobalVariables:");
                        Console.ResetColor();
                    }
                    if (this.setGlobalVariablesInstruction == i)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkMagenta;
                        Console.WriteLine("SetGlobalVariables:");
                        Console.ResetColor();
                    }

                    Instruction instruction = this.compiledCode[i];
                    if (instruction.opcode == Opcode.COMMENT)
                    {
                        if (!instruction.parameter.ToString().EndsWith("{ }") && instruction.parameter.ToString().EndsWith("}"))
                        {
                            indent--;
                        }

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"{"  ".Repeat(indent)}{instruction.parameter}");
                        Console.ResetColor();

                        if (!instruction.parameter.ToString().EndsWith("{ }") && instruction.parameter.ToString().EndsWith("{"))
                        {
                            indent++;
                        }

                        continue;
                    }

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write($"{"  ".Repeat(indent)} {instruction.opcode}");
                    Console.Write($" ");

                    if (instruction.parameter is int || instruction.parameter is float)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.parameter}");
                        Console.Write($" ");
                    }
                    else if (instruction.parameter is bool)
                    {
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.Write($"{instruction.parameter}");
                        Console.Write($" ");
                    }
                    else if (instruction.parameter is string)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"\"{instruction.parameter}\"");
                        Console.Write($" ");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"{instruction.parameter}");
                        Console.Write($" ");
                    }

                    if (!string.IsNullOrEmpty(instruction.additionParameter))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"\"{instruction.additionParameter}\"");
                        Console.Write($" ");
                    }

                    if (instruction.additionParameter2 != -1)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"{instruction.additionParameter2}");
                        Console.Write($" ");
                    }

                    if (!string.IsNullOrEmpty(instruction.tag))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"{instruction.tag}");
                    }

                    Console.Write("\n\r");

                    Console.ResetColor();
                }

                Console.WriteLine("\n\r === ===\n\r");
            }
        }

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


        int AnalyzeFunctions(Dictionary<string, FunctionDefinition> functions, Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            this.compiledFunctions = new();

            foreach (var function in functions)
            {
                if (this.compiledFunctions.ContainsKey(function.Key))
                { throw new ParserException($"Function with name '{function.Key}' already defined"); }

                this.compiledFunctions.Add(function.Value.FullName, GetFunctionInfo(function));
            }

            // Remove unused functions
            {
                void AnalyzeStatements(List<Statement> statements)
                {
                    foreach (var st in statements)
                    {
                        AnalyzeStatement(st);
                        if (st is StatementParent pr)
                        {
                            AnalyzeStatements(pr.statements);
                        }
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
                        if (GetCompiledFunction(st8, out var cf))
                        { cf.TimesUsed++; }

                        foreach (var st9 in st8.parameters)
                        { AnalyzeStatement(st9); }
                    }
                }

                foreach (var f in functions)
                {
                    AnalyzeStatements(f.Value.statements);
                }
            }

            int functionsRemoved = 0;

            for (int i = functions.Count - 1; i >= 0; i--)
            {
                if (!this.compiledFunctions.TryGetValue(functions.ElementAt(i).Value.FullName, out var f)) continue;
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

        public struct CompilerSettings
        {
            public bool GenerateComments;
            public byte RemoveUnusedFunctionsIterations;
            public bool PrintInstructions;

            public static CompilerSettings Default => new()
            {
                GenerateComments = true,
                RemoveUnusedFunctionsIterations = 4,
                PrintInstructions = false,
            };
        }

        public CodeGeneratorResult GenerateCode(
            Dictionary<string, FunctionDefinition> functions,
            Dictionary<string, StructDefinition> structs,
            List<Statement_NewVariable> globalVariables,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            Dictionary<string, Func<Stack.IStruct>> builtinStructs,
            CompilerSettings settings,
            Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            this.AddCommentsToCode = settings.GenerateComments;
            this.compiledStructs = new();
            this.compiledGlobalVariables = new();
            this.functionOffsets = new();
            this.compiledCode = new();
            this.builtinFunctions = builtinFunctions;

            int iterations = settings.RemoveUnusedFunctionsIterations;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int functionsRemoved = AnalyzeFunctions(functions, printCallback);
                if (printCallback != null)
                { printCallback?.Invoke($"  Removed {functionsRemoved} unused functions (iteration {iteration})", TerminalInterpreter.LogType.Debug); }
            }

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

        /// <summary>
        /// Compiles the source code into a list of instructions
        /// </summary>
        /// <param name="code">
        /// The source code
        /// </param>
        /// <param name="namespacesFolder">
        /// The directory where the source code file is located
        /// </param>
        /// <param name="result">
        /// The compiler result
        /// </param>
        /// <param name="warnings">
        /// A list that this can fill with warnings
        /// </param>
        /// <param name="printCallback">
        /// Optional: Print callback
        /// </param>
        /// <exception cref="EndlessLoopException"/>
        /// <exception cref="SyntaxException"/>
        /// <exception cref="ParserException"/>
        /// <exception cref="Exception"/>
        /// <exception cref="InternalException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="System.Exception"/>
        public static CompilerResult CompileCode(
            ParserResult parserResult,
            Dictionary<string, BuiltinFunction> builtinFunctions,
            Dictionary<string, Func<Stack.IStruct>> builtinStructs,
            DirectoryInfo namespacesFolder,
            List<Warning> warnings,
            CompilerSettings settings,
            ParserSettings parserSettings,
            Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            Dictionary<string, FunctionDefinition> Functions = new();
            Dictionary<string, StructDefinition> Structs = new();

            if (parserResult.Usings.Count > 0)
            { printCallback?.Invoke("Parse usings...", TerminalInterpreter.LogType.Debug); }

            for (int i = 0; i < parserResult.Usings.Count; i++)
            {
                string usingItem = parserResult.Usings[i];
                if (File.Exists(namespacesFolder.FullName + "\\" + usingItem + "." + Core.FileExtensions.Code))
                {
                    printCallback?.Invoke($"Parse code: {usingItem} ...", TerminalInterpreter.LogType.Debug);
                    var parserResult2 = Parser.Parser.Parse(File.ReadAllText(namespacesFolder.FullName + "\\" + usingItem + "." + Core.FileExtensions.Code), warnings,
                        (msg, lv) => { printCallback?.Invoke($"  {msg}", lv); });

                    if (parserSettings.PrintInfo)
                    { parserResult2.WriteToConsole($"PARSER INFO FOR '{usingItem}'"); }

                    foreach (var func in parserResult2.Functions)
                    {
                        if (Functions.ContainsKey(func.Key))
                        { throw new ParserException($"Function '{func.Value.type.text} {func.Value.FullName}" + ((func.Value.parameters.Count > 0) ? "(...)" : "()") + "' already exists"); }
                        else
                        {
                            Functions.Add(func.Key, func.Value);
                        }
                    }

                    foreach (var @struct in parserResult2.Structs)
                    {
                        if (Structs.ContainsKey(@struct.Key))
                        {
                            throw new ParserException($"Struct '{@struct.Value.FullName}' already exists");
                        }
                        else
                        {
                            Structs.Add(@struct.Key, @struct.Value);
                        }
                    }
                }
                else
                { throw new ParserException($"Namespace file '{usingItem}' not found (\"{namespacesFolder.FullName + "\\" + usingItem + "." + Core.FileExtensions.Code}\")"); }
            }

            if (parserSettings.PrintInfo)
            { parserResult.WriteToConsole(); }

            DateTime compileStarted = DateTime.Now;
            if (printCallback != null)
            { printCallback?.Invoke("Compiling...", TerminalInterpreter.LogType.Debug); }

            foreach (var func in parserResult.Functions)
            {
                Functions.Add(func.Key, func.Value);
            }
            foreach (var @struct in parserResult.Structs)
            {
                Structs.Add(@struct.Key, @struct.Value);
            }

            Compiler compiler = new()
            { warnings = warnings };
            var codeGeneratorResult = compiler.GenerateCode(Functions, Structs, parserResult.GlobalVariables, builtinFunctions, builtinStructs, settings, printCallback);

            if (printCallback != null)
            { printCallback?.Invoke($"Compiled in {(DateTime.Now - compileStarted).TotalMilliseconds} ms", TerminalInterpreter.LogType.Debug); }

            return new CompilerResult()
            {
                compiledCode = codeGeneratorResult.compiledCode,

                compiledStructs = codeGeneratorResult.compiledStructs,
                compiledFunctions = codeGeneratorResult.compiledFunctions,
                compiledGlobalVariables = compiler.compiledGlobalVariables,
                compiledVariables = compiler.compiledVariables,

                clearGlobalVariablesInstruction = codeGeneratorResult.clearGlobalVariablesInstruction,
                setGlobalVariablesInstruction = codeGeneratorResult.setGlobalVariablesInstruction,
                functionOffsets = compiler.functionOffsets,
            };
        }
    }
}