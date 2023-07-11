using DataUtilities.ReadableFileFormat;
using DataUtilities.Serializer;

using System;

namespace ProgrammingLanguage.Bytecode
{
    public enum AddressingMode : byte
    {
        ABSOLUTE,
        RUNTIME,

        /// <summary><b>Only for stack!</b></summary>
        BASEPOINTER_RELATIVE,
        /// <summary><b>Only for stack!</b></summary>
        RELATIVE,
        /// <summary><b>Only for stack!</b></summary>
        POP,
    }

    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Instruction : ISerializable<Instruction>, ISerializableText, IDeserializableText
    {
        public AddressingMode AddressingMode;
        public Opcode opcode;
        DataItem parameter;

        /// <exception cref="Errors.InternalException"/>
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
        /// Sets the <see cref="DataItem.Tag"/> to this<br/>
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

        public Instruction(Opcode opcode, AddressingMode addressingMode)
        {
            this.opcode = opcode;
            this.AddressingMode = addressingMode;
            this.parameter = DataItem.Null;
        }
        public Instruction(Opcode opcode, AddressingMode addressingMode, DataItem parameter)
        {
            this.opcode = opcode;
            this.AddressingMode = addressingMode;
            this.parameter = parameter;
        }

        public override string ToString()
        {
            if (this.opcode == Opcode.COMMENT)
            { return $"# {this.tag}"; }
            else
            {
                string result = $"{opcode}";

                if (opcode == Opcode.LOAD_VALUE ||
                    opcode == Opcode.STORE_VALUE)
                { result += " " + AddressingMode.ToString(); }

                if (!this.parameter.IsNull)
                { result += $" {{ {parameter} }}"; }

                return result;
            }
        }

        void ISerializable<Instruction>.Serialize(Serializer serializer)
        {
            serializer.Serialize((int)this.opcode);
            serializer.Serialize((byte)this.AddressingMode);
            serializer.Serialize(this.tag);
            serializer.Serialize(this.parameter, SerializeDataItem);
        }
        /// <exception cref="Errors.InternalException"/>
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
        /// <exception cref="Errors.InternalException"/>
        DataItem DeserializeDataItem(Deserializer deserializer)
        {
            RuntimeType type = (RuntimeType)deserializer.DeserializeByte();
            string tag = deserializer.DeserializeString();

            return type switch
            {
                RuntimeType.INT => new DataItem(deserializer.DeserializeInt32(), tag),
                RuntimeType.FLOAT => new DataItem(deserializer.DeserializeFloat(), tag),
                RuntimeType.CHAR => new DataItem(deserializer.DeserializeChar(), tag),
                _ => throw new Errors.InternalException($"Unknown type {type}"),
            };
        }

        /// <exception cref="Errors.InternalException"/>
        static Value SerializeTextDataItem(DataItem dataItem)
        {
            Value result = Value.Object();
            result["Type"] = Value.Literal((int)dataItem.type);
            result["Tag"] = Value.Literal(dataItem.Tag);
            result["Value"] = dataItem.type switch
            {
                RuntimeType.INT => Value.Literal(dataItem.ValueInt),
                RuntimeType.FLOAT => Value.Literal(dataItem.ValueFloat),
                RuntimeType.CHAR => Value.Literal(dataItem.ValueChar),
                _ => throw new Errors.InternalException($"Unknown type {dataItem.type}"),
            };
            return result;
        }
        /// <exception cref="Errors.InternalException"/>
        static DataItem DeserializeTextDataItem(Value data)
        {
            RuntimeType type = (RuntimeType)data["Type"].Int;
            string tag = data["Tag"].String;

            return type switch
            {
                RuntimeType.INT => new DataItem((int)data["Value"].Int, tag),
                RuntimeType.FLOAT => new DataItem((float)data["Value"].Float, tag),
                RuntimeType.CHAR => new DataItem((char)data["Value"].Int, tag),
                _ => throw new Errors.InternalException($"Unknown type {type}"),
            };
        }

        void ISerializable<Instruction>.Deserialize(Deserializer deserializer)
        {
            this.opcode = (Opcode)deserializer.DeserializeInt32();
            this.AddressingMode = (AddressingMode)deserializer.DeserializeByte();
            this.tag = deserializer.DeserializeString();
            this.parameter = deserializer.DeserializeObject(DeserializeDataItem);
        }

        /// <exception cref="Errors.InternalException"/>
        Value ISerializableText.SerializeText()
        {
            Value result = Value.Object();

            result["OpCode"] = Value.Literal(opcode.ToString());
            result["AddressingMode"] = Value.Literal(AddressingMode.ToString());
            result["Tag"] = Value.Literal(tag);
            result["ParameterValue"] = SerializeTextDataItem(parameter);
            return result;
        }

        /// <exception cref="Errors.InternalException"/>
        public void DeserializeText(Value data)
        {
            opcode = Enum.Parse<Opcode>(data["OpCode"].String);
            AddressingMode = Enum.Parse<AddressingMode>(data["AddressingMode"].String);
            tag = data["Tag"].String;
            parameter = DeserializeTextDataItem(data["ParameterValue"]);
        }
    }
}
