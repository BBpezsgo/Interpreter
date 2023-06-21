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
        DataItem parameter;

        public int ParameterInt
        {
            get
            {
                if (parameter.IsNull) throw new Errors.InternalException($"Can't cast null to {nameof(Int32)}");
                if (parameter is DataItem dataItem && dataItem.type == RuntimeType.INT) return dataItem.ValueInt;
                throw new Errors.InternalException($"Can't cast {parameter.GetType().Name} to {nameof(Int32)}"); ;
            }
        }
        public DataItem ParameterData
        {
            get
            {
                if (parameter.IsNull) return DataItem.Null;
                if (parameter is DataItem dataItem) return dataItem;
                return DataItem.Null;
            }
        }
        public DataItem Parameter
        {
            get => parameter;
            set => parameter = value;
        }

        /// <summary>
        /// <b>Only for debugging!</b><br/>
        /// Sets the <see cref="DataItem.Tag"/> to this.<br/>
        /// Can use on:
        /// <list type="bullet">
        /// <item><see cref="Opcode.HEAP_GET"/></item>
        /// <item><see cref="Opcode.HEAP_SET"/></item>
        /// <item><see cref="Opcode.LOAD_VALUE"/></item>
        /// <item><see cref="Opcode.PUSH_VALUE"/></item>
        /// </list>
        /// </summary>
        public string tag = string.Empty;

        [Obsolete("Only for deserialization", true)]
        public Instruction()
        {
            this.opcode = Opcode.UNKNOWN;
            this.AddressingMode = AddressingMode.ABSOLUTE;
            this.parameter = DataItem.Null;
        }

        public Instruction(Opcode opcode)
        {
            this.opcode = opcode;
            this.AddressingMode = AddressingMode.ABSOLUTE;
            this.parameter = DataItem.Null;
        }
        public Instruction(Opcode opcode, DataItem parameter)
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
            this.parameter = DataItem.Null;
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
        public Instruction(Opcode opcode, AddressingMode addressingMode, DataItem parameter)
        {
            this.opcode = opcode;
            this.AddressingMode = addressingMode;
            this.parameter = parameter;
        }

        public override string ToString()
        {
            if (this.opcode == Opcode.COMMENT)
            {
                if (this.tag == null)
                {
                    return "# <null>";
                }
                else
                {
                    return "# " + this.tag.ToString();
                }
            }
            else
            {
                string str = "";
                str += opcode.ToString();
                if (opcode == Opcode.LOAD_VALUE ||
                    opcode == Opcode.STORE_VALUE)
                {
                    str += " " + AddressingMode.ToString();
                }
                if (this.parameter.IsNull)
                {
                    str += " { " + "<null>";
                }
                else
                {
                    str += " { " + parameter.ToString();
                }
                str += " }";
                return str;
            }
        }

        void ISerializable<Instruction>.Serialize(Serializer serializer)
        {
            serializer.Serialize((int)this.opcode);
            serializer.Serialize((byte)this.AddressingMode);
            serializer.Serialize(this.tag);
            serializer.Serialize(this.parameter, SerializeDataItem);
        }
        void SerializeDataItem(Serializer serializer, DataItem dataItem)
        {
            serializer.Serialize((byte)dataItem.type);
            serializer.Serialize(dataItem.Tag);
            switch (dataItem.type)
            {
                case RuntimeType.INT:
                    serializer.Serialize(dataItem.ValueInt);
                    break;
                case RuntimeType.FLOAT:
                    serializer.Serialize(dataItem.ValueFloat);
                    break;
                case RuntimeType.CHAR:
                    serializer.Serialize(dataItem.ValueChar);
                    break;
                default:
                    throw new Errors.InternalException($"Unknown type {dataItem.type}");
            }
        }
        DataItem DeserializeDataItem(Deserializer deserializer)
        {
            RuntimeType type = (RuntimeType)deserializer.DeserializeByte();
            string tag = deserializer.DeserializeString();

            switch (type)
            {
                case RuntimeType.INT:
                    return new DataItem(deserializer.DeserializeInt32(), tag);
                case RuntimeType.FLOAT:
                    return new DataItem(deserializer.DeserializeFloat(), tag);
                case RuntimeType.CHAR:
                    return new DataItem(deserializer.DeserializeChar(), tag);
                default:
                    throw new Errors.InternalException($"Unknown type {type}");
            }
        }

        static Value SerializeTextDataItem(DataItem dataItem)
        {
            Value result = Value.Object();
            result["Type"] = Value.Literal((int)dataItem.type);
            result["Tag"] = Value.Literal(dataItem.Tag);
            switch (dataItem.type)
            {
                case RuntimeType.INT:
                    result["Value"] = Value.Literal(dataItem.ValueInt);
                    return result;
                case RuntimeType.FLOAT:
                    result["Value"] = Value.Literal(dataItem.ValueFloat);
                    return result;
                case RuntimeType.CHAR:
                    result["Value"] = Value.Literal(dataItem.ValueChar);
                    return result;
                default:
                    throw new Errors.InternalException($"Unknown type {dataItem.type}");
            }
        }
        static DataItem DeserializeTextDataItem(Value data)
        {
            RuntimeType type = (RuntimeType)data["Type"].Int;
            string tag = data["Tag"].String;

            switch (type)
            {
                case RuntimeType.INT:
                    return new DataItem((int)data["Value"].Int, tag);
                case RuntimeType.FLOAT:
                    return new DataItem((float)data["Value"].Float, tag);
                case RuntimeType.CHAR:
                    return new DataItem((char)data["Value"].Int, tag);
                default:
                    throw new Errors.InternalException($"Unknown type {type}");
            }
        }

        void ISerializable<Instruction>.Deserialize(Deserializer deserializer)
        {
            this.opcode = (Opcode)deserializer.DeserializeInt32();
            this.AddressingMode = (AddressingMode)deserializer.DeserializeByte();
            this.tag = deserializer.DeserializeString();
            this.parameter = deserializer.DeserializeObject(DeserializeDataItem);
        }

        Value ISerializableText.SerializeText()
        {
            Value result = Value.Object();

            result["OpCode"] = Value.Literal(opcode.ToString());
            result["AddressingMode"] = Value.Literal(AddressingMode.ToString());
            result["Tag"] = Value.Literal(tag);
            result["ParameterValue"] = SerializeTextDataItem(parameter);
            return result;
        }

        public void DeserializeText(Value data)
        {
            opcode = Enum.Parse<Opcode>(data["OpCode"].String);
            AddressingMode = Enum.Parse<AddressingMode>(data["AddressingMode"].String);
            tag = data["Tag"].String;
            parameter = DeserializeTextDataItem(data["ParameterValue"]);
        }
    }
}
