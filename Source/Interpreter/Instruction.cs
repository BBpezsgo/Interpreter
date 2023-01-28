using IngameCoding.Serialization;

using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.Bytecode
{
    public enum AddressingMode : byte
    {
        ABSOLUTE,
        BASEPOINTER_RELATIVE,
        RELATIVE,
        POP,
    }

    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Instruction : ISerializable<Instruction>
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
        internal CentralProcessingUnit cpu;
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

        public Instruction(Opcode opcode, AddressingMode addressingMode)
        {
            this.opcode = opcode;
            this.AddressingMode = addressingMode;
            this.parameter = null;
        }
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
            else if (this.parameter is int)
            {
                serializer.Serialize((byte)1);
                serializer.Serialize((int)this.parameter);
            }
            else if (this.parameter is string)
            {
                serializer.Serialize((byte)2);
                serializer.Serialize((string)this.parameter);
            }
            else if (this.parameter is bool)
            {
                serializer.Serialize((byte)3);
                serializer.Serialize((bool)this.parameter);
            }
            else if (this.parameter is float)
            {
                serializer.Serialize((byte)4);
                serializer.Serialize((float)this.parameter);
            }
            else if (this.parameter is DataItem.List)
            {
                serializer.Serialize((byte)5);
                serializer.Serialize((int)((DataItem.List)this.parameter).itemTypes);
                serializer.SerializeObjectArray(((DataItem.List)this.parameter).items.ToArray(), SerializeDataItem);
            }
            else if (this.parameter is DataItem.Struct strct)
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
                serializer.SerializeObjectArray(fieldValues, SerializeDataItem);
            }
            else if (this.parameter is DataItem.UnassignedStruct)
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
                case DataItem.Type.INT:
                    serializer.Serialize(dataItem.ValueInt);
                    break;
                case DataItem.Type.FLOAT:
                    serializer.Serialize(dataItem.ValueFloat);
                    break;
                case DataItem.Type.STRING:
                    serializer.Serialize(dataItem.ValueString);
                    break;
                case DataItem.Type.BOOLEAN:
                    serializer.Serialize(dataItem.ValueBoolean);
                    break;
                case DataItem.Type.STRUCT:
                    if (dataItem.ValueStruct is DataItem.Struct strct)
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
                        serializer.SerializeObjectArray(fieldValues, SerializeDataItem);
                        break;
                    }
                    throw new NotImplementedException();
                case DataItem.Type.LIST:
                    serializer.Serialize((int)dataItem.ValueList.itemTypes);
                    serializer.SerializeObjectArray(dataItem.ValueList.items.ToArray(), SerializeDataItem);
                    break;
                default:
                    throw new Errors.InternalException($"Unknown type {dataItem.type}");
            }
        }
        DataItem DeserializeDataItem(Deserializer deserializer)
        {
            DataItem.Type type = (DataItem.Type)deserializer.DeserializeInt32();
            string tag = deserializer.DeserializeString();

            switch (type)
            {
                case DataItem.Type.INT:
                    return new DataItem(deserializer.DeserializeInt32(), tag);
                case DataItem.Type.FLOAT:
                    return new DataItem(deserializer.DeserializeFloat(), tag);
                case DataItem.Type.STRING:
                    return new DataItem(deserializer.DeserializeString(), tag);
                case DataItem.Type.BOOLEAN:
                    return new DataItem(deserializer.DeserializeBoolean(), tag);
                case DataItem.Type.STRUCT:
                    string[] fieldNames = deserializer.DeserializeArray<string>();
                    DataItem[] fieldValues = deserializer.DeserializeObjectArray(DeserializeDataItem);
                    Dictionary<string, DataItem> fields = new();
                    for (int i = 0; i < fieldNames.Length; i++)
                    { fields.Add(fieldNames[i], fieldValues[i]); }
                    return new DataItem(new DataItem.Struct(fields, null), tag);
                case DataItem.Type.LIST:
                    var itemTypes = (DataItem.Type)deserializer.DeserializeInt32();
                    var items = deserializer.DeserializeObjectArray(DeserializeDataItem);
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
                var types = (DataItem.Type)deserializer.DeserializeInt32();
                var newList = new DataItem.List(types);
                var items = deserializer.DeserializeObjectArray(DeserializeDataItem);
                foreach (var item in items)
                { newList.Add(item); }
            }
            else if (parameterType == 6)
            {
                string[] fieldNames = deserializer.DeserializeArray<string>();
                DataItem[] fieldValues = deserializer.DeserializeObjectArray(DeserializeDataItem);
                Dictionary<string, DataItem> fields = new();
                for (int i = 0; i < fieldNames.Length; i++)
                { fields.Add(fieldNames[i], fieldValues[i]); }
                this.parameter = new DataItem.Struct(fields, null);
            }
            else if (parameterType == 7)
            {
                this.parameter = new DataItem.UnassignedStruct();
            }
            else
            { throw new NotImplementedException(); }
        }
    }
}
