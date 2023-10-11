using System;
using DataUtilities.ReadableFileFormat;
using DataUtilities.Serializer;

namespace LanguageCore.Runtime
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
    public class Instruction : ISerializable<Instruction>, IFullySerializableText
    {
        public AddressingMode AddressingMode;
        public Opcode opcode;
        DataItem parameter;

        /// <exception cref="Errors.InternalException"/>
        public int ParameterInt
        {
            get
            {
                if (parameter.IsNull) throw new InternalException($"Can't cast null to {nameof(Int32)}");
                if (parameter is DataItem dataItem && dataItem.Type == RuntimeType.INT) return dataItem.ValueInt;
                throw new InternalException($"Can't cast {parameter.GetType().Name} to {nameof(Int32)}"); ;
            }
            set => parameter = new DataItem(value, parameter.Tag);
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

        #region Serialize
        public void Serialize(Serializer serializer)
        {
            serializer.Serialize((byte)this.opcode);
            serializer.Serialize((byte)this.AddressingMode);
            serializer.Serialize(this.parameter);
            serializer.Serialize(this.tag);
        }

        public void Deserialize(Deserializer deserializer)
        {
            this.opcode = (Opcode)deserializer.DeserializeByte();
            this.AddressingMode = (AddressingMode)deserializer.DeserializeByte();
            this.parameter = deserializer.DeserializeObject<DataItem>();
            this.tag = deserializer.DeserializeString();
        }

        public Value SerializeText()
        {
            Value result = Value.Object();

            result["OpCode"] = Value.Literal(opcode.ToString());
            result["AddressingMode"] = Value.Literal(AddressingMode.ToString());
            result["Parameter"] = Value.Object(parameter);
            result["Tag"] = Value.Literal(tag);
            return result;
        }

        public void DeserializeText(Value data)
        {
            opcode = data["OpCode"].Enum<Opcode>();
            AddressingMode = data["AddressingMode"].Enum<AddressingMode>();
            parameter = data["Parameter"].Object<DataItem>();
            tag = data["Tag"].String;
        }
        #endregion
    }
}
