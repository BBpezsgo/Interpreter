using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode.Compiler
{
    using Bytecode;

    using Errors;

    using Parser;
    using Parser.Statements;

    static class Extensions
    {
        public static DataItem.Type Convert(this BuiltinType v) => v switch
        {
            BuiltinType.INT => DataItem.Type.INT,
            BuiltinType.FLOAT => DataItem.Type.FLOAT,
            BuiltinType.STRING => DataItem.Type.STRING,
            BuiltinType.BOOLEAN => DataItem.Type.BOOLEAN,
            BuiltinType.STRUCT => DataItem.Type.STRUCT,
            _ => DataItem.Type.RUNTIME,
        };
        public static BuiltinType Convert(this DataItem.Type v) => v switch
        {
            DataItem.Type.INT => BuiltinType.INT,
            DataItem.Type.FLOAT => BuiltinType.FLOAT,
            DataItem.Type.STRING => BuiltinType.STRUCT,
            DataItem.Type.BOOLEAN => BuiltinType.BOOLEAN,
            DataItem.Type.STRUCT => BuiltinType.STRUCT,
            DataItem.Type.LIST => BuiltinType.RUNTIME,
            DataItem.Type.RUNTIME => BuiltinType.RUNTIME,
            _ => BuiltinType.ANY,
        };
    }

    internal static class FunctionID
    {
        public static string ID(this FunctionDefinition function)
        {
            string result = function.FullName;
            for (int i = 0; i < function.parameters.Count; i++)
            {
                var param = function.parameters[i];
                var paramType = (param.type.typeName == BuiltinType.STRUCT) ? param.type.text : param.type.typeName.ToString().ToLower();
                result += "," + paramType + (param.type.isList ? "[]" : "");
            }
            return result;
        }
    }

    struct UndefinedFunctionOffset
    {
        public int callInstructionIndex;
        public Statement_FunctionCall functionCallStatement;
        public Dictionary<string, Parameter> currentParameters;
        public Dictionary<string, CompiledVariable> currentVariables;

        public UndefinedFunctionOffset(int callInstructionIndex, Statement_FunctionCall functionCallStatement, KeyValuePair<string, Parameter>[] currentParameters, KeyValuePair<string, CompiledVariable>[] currentVariables)
        {
            this.callInstructionIndex = callInstructionIndex;
            this.functionCallStatement = functionCallStatement;

            this.currentParameters = new();
            this.currentVariables = new();

            foreach (var item in currentParameters)
            { this.currentParameters.Add(item.Key, item.Value); }
            foreach (var item in currentVariables)
            { this.currentVariables.Add(item.Key, item.Value); }
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

        public int TimesUsed;

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

        readonly Action<DataItem[]> callback;

        public delegate void ReturnEventHandler(DataItem returnValue);
        public event ReturnEventHandler ReturnEvent;

        public int ParameterCount { get { return parameters.Length; } }
        public bool returnSomething;

        // Wrap the event in a protected virtual method
        // to enable derived classes to raise the event.
        public void RaiseReturnEvent(DataItem returnValue)
        {
            // Raise the event in a thread-safe manner using the ?. operator.
            ReturnEvent?.Invoke(returnValue);
        }

        /// <summary>
        /// Function without return value
        /// </summary>
        /// <param name="callback">Callback when the machine process this function</param>
        public BuiltinFunction(Action<IngameCoding.Bytecode.DataItem[]> callback, TypeToken[] parameters, bool returnSomething = false)
        {
            this.parameters = parameters;
            this.callback = callback;
            this.returnSomething = returnSomething;
        }

        public void Callback(DataItem[] parameters)
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
        public Func<IStruct> CreateBuiltinStruct;
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
}
