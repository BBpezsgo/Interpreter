using DataUtilities.ReadableFileFormat;
using DataUtilities.Serializer;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.Bytecode
{
    /// <summary>
    /// Only used by these instructions:<br/>
    /// <list type="bullet">
    /// <item><see cref="Opcode.STORE_VALUE"/></item>
    /// <item><see cref="Opcode.LOAD_VALUE"/></item>
    /// <item><see cref="Opcode.STORE_FIELD"/></item>
    /// <item><see cref="Opcode.LOAD_FIELD"/></item>
    /// <item><see cref="Opcode.HEAP_SET"/></item>
    /// <item><see cref="Opcode.HEAP_GET"/></item>
    /// </list>
    /// </summary>
    public enum AddressingMode : byte
    {
        ABSOLUTE,
        /// <summary><b>Only for stack!</b></summary>
        BASEPOINTER_RELATIVE,
        /// <summary><b>Only for stack!</b></summary>
        RELATIVE,
        /// <summary><b>Only for stack!</b></summary>
        POP,
        RUNTIME,
    }

    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Instruction : ISerializable<Instruction>, DataUtilities.ReadableFileFormat.ISerializableText, DataUtilities.ReadableFileFormat.IDeserializableText
    {
        public AddressingMode AddressingMode;
        public Opcode opcode;
        /// <summary>
        /// Can be:
        /// <list type="bullet">
        /// <dataItem><see cref="null"/></dataItem>
        /// <dataItem><see cref="int"/></dataItem>
        /// <dataItem><see cref="bool"/></dataItem>
        /// <dataItem><see cref="float"/></dataItem>
        /// <dataItem><see cref="string"/></dataItem>
        /// <dataItem><see cref="IStruct"/></dataItem>
        /// <dataItem><see cref="DataItem.Struct"/></dataItem>
        /// <dataItem><see cref="DataItem.List"/></dataItem>
        /// </list>
        /// </summary>
        public object parameter;

        /// <summary>
        /// <b>Only for debugging!</b><br/>
        /// Sets the <see cref="DataItem.Tag"/> to this.<br/>
        /// Can use on:
        /// <list type="bullet">
        /// <dataItem><see cref="Opcode.LOAD_VALUE_BR"/></dataItem>
        /// <dataItem><see cref="Opcode.LOAD_FIELD_BR"/></dataItem>
        /// <dataItem><see cref="Opcode.LOAD_VALUE"/></dataItem>
        /// <dataItem><see cref="Opcode.PUSH_VALUE"/></dataItem>
        /// </list>
        /// </summary>
        public string tag = string.Empty;

        /// <summary><b>Only works at runtime!</b></summary>
        internal int? index;
        /// <summary><b>Only works at runtime!</b></summary>
        internal BytecodeProcessor cpu;
        /// <summary><b>Only works at runtime!</b></summary>
        string IsRunning
        {
            get
            {
                if (cpu != null && index.HasValue)
                {
                    if (index == cpu.CodePointer)
                    {
                        return ">";
                    }
                }
                return " ";
            }
        }

        [Obsolete("Only for deserialization", true)]
        public Instruction()
        {
            this.opcode = Opcode.UNKNOWN;
            this.AddressingMode = AddressingMode.ABSOLUTE;
            this.parameter = null;
        }

        public Instruction(Opcode opcode)
        {
            this.opcode = opcode;
            this.AddressingMode = AddressingMode.ABSOLUTE;
            this.parameter = null;
        }
        public Instruction(Opcode opcode, object parameter)
        {
            this.opcode = opcode;
            this.AddressingMode = AddressingMode.ABSOLUTE;
            this.parameter = parameter;
        }

        /// <param name="addressingMode">
        /// Only used by these instructions:<br/>
        /// <list type="bullet">
        /// <item><see cref="Opcode.STORE_VALUE"/></item>
        /// <item><see cref="Opcode.LOAD_VALUE"/></item>
        /// <item><see cref="Opcode.STORE_FIELD"/></item>
        /// <item><see cref="Opcode.LOAD_FIELD"/></item>
        /// </list>
        /// </param>
        public Instruction(Opcode opcode, AddressingMode addressingMode)
        {
            this.opcode = opcode;
            this.AddressingMode = addressingMode;
            this.parameter = null;
        }
        /// <param name="addressingMode">
        /// Only used by these instructions:<br/>
        /// <list type="bullet">
        /// <item><see cref="Opcode.STORE_VALUE"/></item>
        /// <item><see cref="Opcode.LOAD_VALUE"/></item>
        /// <item><see cref="Opcode.STORE_FIELD"/></item>
        /// <item><see cref="Opcode.LOAD_FIELD"/></item>
        /// </list>
        /// </param>
        public Instruction(Opcode opcode, AddressingMode addressingMode, object parameter)
        {
            this.opcode = opcode;
            this.AddressingMode = addressingMode;
            this.parameter = parameter;
        }

        public override string ToString()
        {
            if (this.opcode == Opcode.COMMENT)
            {
                if (this.parameter == null)
                {
                    return "# <null>";
                }
                else
                {
                    return "# " + this.parameter.ToString();
                }
            }
            else
            {
                string str;
                if (this.parameter == null)
                {
                    str = opcode.ToString() + " { " + "<null>";
                }
                else
                {
                    str = opcode.ToString() + " { " + parameter.ToString();
                }
                str += " }";
                return IsRunning + str;
            }
        }

        void ISerializable<Instruction>.Serialize(Serializer serializer)
        {
            serializer.Serialize((int)this.opcode);
            serializer.Serialize((byte)this.AddressingMode);
            serializer.Serialize(this.tag);
            if (this.parameter is null)
            {
                serializer.Serialize((byte)0);
            }
            else if (this.parameter is int @int)
            {
                serializer.Serialize((byte)1);
                serializer.Serialize(@int);
            }
            else if (this.parameter is string @string)
            {
                serializer.Serialize((byte)2);
                serializer.Serialize(@string);
            }
            else if (this.parameter is bool @bool)
            {
                serializer.Serialize((byte)3);
                serializer.Serialize(@bool);
            }
            else if (this.parameter is float @float)
            {
                serializer.Serialize((byte)4);
                serializer.Serialize(@float);
            }
            else if (this.parameter is DataItem.List list)
            {
                serializer.Serialize((byte)5);
                serializer.Serialize((int)list.itemTypes);
                serializer.Serialize(list.items.ToArray(), SerializeDataItem);
            }
            else if (this.parameter is Struct strct)
            {
                serializer.Serialize((byte)6);
                string[] fieldNames = new string[strct.fields.Count];
                DataItem[] fieldValues = new DataItem[strct.fields.Count];
                for (int i = 0; i < strct.fields.Count; i++)
                {
                    var pair = strct.fields.ElementAt(i);
                    fieldNames[i] = pair.Key;
                    fieldValues[i] = pair.Value;
                }
                serializer.Serialize(fieldNames);
                serializer.Serialize(fieldValues, SerializeDataItem);
            }
            else if (this.parameter is UnassignedStruct)
            {
                serializer.Serialize((byte)7);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        void SerializeDataItem(Serializer serializer, DataItem dataItem)
        {
            serializer.Serialize((int)dataItem.type);
            serializer.Serialize(dataItem.Tag);
            switch (dataItem.type)
            {
                case DataType.INT:
                    serializer.Serialize(dataItem.ValueInt);
                    break;
                case DataType.FLOAT:
                    serializer.Serialize(dataItem.ValueFloat);
                    break;
                case DataType.STRING:
                    serializer.Serialize(dataItem.ValueString);
                    break;
                case DataType.BOOLEAN:
                    serializer.Serialize(dataItem.ValueBoolean);
                    break;
                case DataType.STRUCT:
                    if (dataItem.ValueStruct is Struct strct)
                    {
                        string[] fieldNames = new string[strct.fields.Count];
                        DataItem[] fieldValues = new DataItem[strct.fields.Count];
                        for (int i = 0; i < strct.fields.Count; i++)
                        {
                            var pair = strct.fields.ElementAt(i);
                            fieldNames[i] = pair.Key;
                            fieldValues[i] = pair.Value;
                        }
                        serializer.Serialize(fieldNames);
                        serializer.Serialize(fieldValues, SerializeDataItem);
                        break;
                    }
                    throw new NotImplementedException();
                case DataType.LIST:
                    serializer.Serialize((int)dataItem.ValueList.itemTypes);
                    serializer.Serialize(dataItem.ValueList.items.ToArray(), SerializeDataItem);
                    break;
                default:
                    throw new Errors.InternalException($"Unknown type {dataItem.type}");
            }
        }
        DataItem DeserializeDataItem(Deserializer deserializer)
        {
            DataType type = (DataType)deserializer.DeserializeInt32();
            string tag = deserializer.DeserializeString();

            switch (type)
            {
                case DataType.INT:
                    return new DataItem(deserializer.DeserializeInt32(), tag);
                case DataType.FLOAT:
                    return new DataItem(deserializer.DeserializeFloat(), tag);
                case DataType.STRING:
                    return new DataItem(deserializer.DeserializeString(), tag);
                case DataType.BOOLEAN:
                    return new DataItem(deserializer.DeserializeBoolean(), tag);
                case DataType.STRUCT:
                    string[] fieldNames = deserializer.DeserializeArray<string>();
                    DataItem[] fieldValues = deserializer.DeserializeArray(DeserializeDataItem);
                    Dictionary<string, DataItem> fields = new();
                    for (int i = 0; i < fieldNames.Length; i++)
                    { fields.Add(fieldNames[i], fieldValues[i]); }
                    return new DataItem(new Struct(fields, null), tag);
                case DataType.LIST:
                    var itemTypes = (DataType)deserializer.DeserializeInt32();
                    var items = deserializer.DeserializeArray(DeserializeDataItem);
                    var newList = new DataItem.List(itemTypes);
                    for (int i = 0; i < items.Length; i++)
                    { newList.Add(items[i]); }
                    return new DataItem(newList, tag);
                default:
                    throw new Errors.InternalException($"Unknown type {type}");
            }
        }

        Value SerializeTextDataItem(DataItem dataItem)
        {
            Value result = Value.Object();
            result["Type"] = Value.Literal((int)dataItem.type);
            result["Tag"] = Value.Literal(dataItem.Tag);
            switch (dataItem.type)
            {
                case DataType.INT:
                    result["Value"] = Value.Literal(dataItem.ValueInt);
                    return result;
                case DataType.FLOAT:
                    result["Value"] = Value.Literal(dataItem.ValueFloat);
                    return result;
                case DataType.STRING:
                    result["Value"] = Value.Literal(dataItem.ValueString);
                    return result;
                case DataType.BOOLEAN:
                    result["Value"] = Value.Literal(dataItem.ValueBoolean);
                    return result;
                case DataType.STRUCT:
                    if (dataItem.ValueStruct is Struct strct)
                    {
                        string[] fieldNames = new string[strct.fields.Count];
                        DataItem[] fieldValues = new DataItem[strct.fields.Count];
                        for (int i = 0; i < strct.fields.Count; i++)
                        {
                            var pair = strct.fields.ElementAt(i);
                            fieldNames[i] = pair.Key;
                            fieldValues[i] = pair.Value;
                        }
                        result["ValueA"] = Value.Object(fieldNames);
                        result["ValueB"] = Value.Object(fieldValues, SerializeTextDataItem);
                        return result;
                    }
                    throw new NotImplementedException();
                case DataType.LIST:
                    result["ValueA"] = Value.Literal((int)dataItem.ValueList.itemTypes);
                    result["ValueB"] = Value.Object(dataItem.ValueList.items.ToArray(), SerializeTextDataItem);
                    return result;
                default:
                    throw new Errors.InternalException($"Unknown type {dataItem.type}");
            }
        }
        DataItem DeserializeTextDataItem(Value data)
        {
            DataType type = (DataType)data["Type"].Int;
            string tag = data["Tag"].String;

            switch (type)
            {
                case DataType.INT:
                    return new DataItem((int)data["Value"].Int, tag);
                case DataType.FLOAT:
                    return new DataItem((float)data["Value"].Float, tag);
                case DataType.STRING:
                    return new DataItem(data["Value"].String, tag);
                case DataType.BOOLEAN:
                    return new DataItem((bool)data["Value"].Bool, tag);
                case DataType.STRUCT:
                    string[] fieldNames = data["ValueA"].Array.ConvertPrimitive<string>();
                    DataItem[] fieldValues = data["ValueB"].Array.Convert(DeserializeTextDataItem);
                    Dictionary<string, DataItem> fields = new();
                    for (int i = 0; i < fieldNames.Length; i++)
                    { fields.Add(fieldNames[i], fieldValues[i]); }
                    return new DataItem(new Struct(fields, null), tag);
                case DataType.LIST:
                    var itemTypes = (DataType)data["ValueA"].Int;
                    var items = data["ValueB"].Array.Convert(DeserializeTextDataItem);
                    var newList = new DataItem.List(itemTypes);
                    for (int i = 0; i < items.Length; i++)
                    { newList.Add(items[i]); }
                    return new DataItem(newList, tag);
                default:
                    throw new Errors.InternalException($"Unknown type {type}");
            }
        }

        void ISerializable<Instruction>.Deserialize(Deserializer deserializer)
        {
            this.opcode = (Opcode)deserializer.DeserializeInt32();
            this.AddressingMode = (AddressingMode)deserializer.DeserializeByte();
            this.tag = deserializer.DeserializeString();
            var parameterType = deserializer.DeserializeByte();
            if (parameterType == 0)
            {
                this.parameter = null;
            }
            else if (parameterType == 1)
            {
                this.parameter = deserializer.DeserializeInt32();
            }
            else if (parameterType == 2)
            {
                this.parameter = deserializer.DeserializeString();
            }
            else if (parameterType == 3)
            {
                this.parameter = deserializer.DeserializeBoolean();
            }
            else if (parameterType == 4)
            {
                this.parameter = deserializer.DeserializeFloat();
            }
            else if (parameterType == 5)
            {
                var types = (DataType)deserializer.DeserializeInt32();
                var newList = new DataItem.List(types);
                var items = deserializer.DeserializeArray(DeserializeDataItem);
                foreach (var item in items)
                { newList.Add(item); }
            }
            else if (parameterType == 6)
            {
                string[] fieldNames = deserializer.DeserializeArray<string>();
                DataItem[] fieldValues = deserializer.DeserializeArray(DeserializeDataItem);
                Dictionary<string, DataItem> fields = new();
                for (int i = 0; i < fieldNames.Length; i++)
                { fields.Add(fieldNames[i], fieldValues[i]); }
                this.parameter = new Struct(fields, null);
            }
            else if (parameterType == 7)
            {
                this.parameter = new UnassignedStruct();
            }
            else
            { throw new NotImplementedException(); }
        }

        Value ISerializableText.SerializeText()
        {
            Value result = Value.Object();

            result["OpCode"] = Value.Literal(opcode.ToString());
            result["AddressingMode"] = Value.Literal(AddressingMode.ToString());
            result["Tag"] = Value.Literal(tag);
            if (this.parameter is null)
            {
                result["ParameterType"] = Value.Literal(0);
            }
            else if (this.parameter is int @int)
            {
                result["ParameterType"] = Value.Literal(1);
                result["ParameterValue"] = Value.Literal(@int);
            }
            else if (this.parameter is string @string)
            {
                result["ParameterType"] = Value.Literal(2);
                result["ParameterValue"] = Value.Literal(@string);
            }
            else if (this.parameter is bool @bool)
            {
                result["ParameterType"] = Value.Literal(3);
                result["ParameterValue"] = Value.Literal(@bool);
            }
            else if (this.parameter is float @float)
            {
                result["ParameterType"] = Value.Literal(4);
                result["ParameterValue"] = Value.Literal(@float);
            }
            else if (this.parameter is DataItem.List list)
            {
                result["ParameterType"] = Value.Literal(5);
                result["ParameterValueA"] = Value.Literal((int)list.itemTypes);
                result["ParameterValueB"] = Value.Object(list.items.ToArray(), SerializeTextDataItem);
            }
            else if (this.parameter is Struct strct)
            {
                result["ParameterType"] = Value.Literal(6);
                string[] fieldNames = new string[strct.fields.Count];
                DataItem[] fieldValues = new DataItem[strct.fields.Count];
                for (int i = 0; i < strct.fields.Count; i++)
                {
                    var pair = strct.fields.ElementAt(i);
                    fieldNames[i] = pair.Key;
                    fieldValues[i] = pair.Value;
                }
                result["ParameterValueA"] = Value.Object(fieldNames);
                result["ParameterValueB"] = Value.Object(fieldValues, SerializeTextDataItem);
            }
            else if (this.parameter is UnassignedStruct)
            {
                result["ParameterType"] = Value.Literal(7);
            }
            else
            {
                throw new NotImplementedException();
            }

            return result;
        }

        public void DeserializeText(Value data)
        {
            opcode = Enum.Parse<Opcode>(data["OpCode"].String);
            AddressingMode = Enum.Parse<AddressingMode>(data["AddressingMode"].String);
            tag = data["Tag"].String;
            int parameterType = (int)data["ParameterType"].Int;
            if (parameterType == 0)
            {
                this.parameter = null;
            }
            else if (parameterType == 1)
            {
                this.parameter = data["ParameterValue"].Int;
            }
            else if (parameterType == 2)
            {
                this.parameter = data["ParameterValue"].String;
            }
            else if (parameterType == 3)
            {
                this.parameter = data["ParameterValue"].Bool;
            }
            else if (parameterType == 4)
            {
                this.parameter = data["ParameterValue"].Float;
            }
            else if (parameterType == 5)
            {
                var types = (DataType)data["ParameterValueA"].Int;
                var newList = new DataItem.List(types);
                var items = data["ParameterValueB"].Array.Convert(DeserializeTextDataItem);
                foreach (var item in items)
                { newList.Add(item); }
            }
            else if (parameterType == 6)
            {
                string[] fieldNames = data["ParameterValueA"].Array.ConvertPrimitive<string>();
                DataItem[] fieldValues = data["ParameterValueB"].Array.Convert(DeserializeTextDataItem);
                Dictionary<string, DataItem> fields = new();
                for (int i = 0; i < fieldNames.Length; i++)
                { fields.Add(fieldNames[i], fieldValues[i]); }
                this.parameter = new Struct(fields, null);
            }
            else if (parameterType == 7)
            {
                this.parameter = new UnassignedStruct();
            }
            else
            { throw new NotImplementedException(); }
        }
    }
}
