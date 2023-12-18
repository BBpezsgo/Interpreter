using System;
using DataUtilities.ReadableFileFormat;
using DataUtilities.Serializer;

namespace LanguageCore.Runtime
{
    public enum AddressingMode : byte
    {
        /// <summary>
        /// <c>CurrentInstruction.ParameterInt</c>
        /// </summary>
        Absolute,

        /// <summary>
        /// <c>Memory.Stack.Pop().ToInt32(null)</c>
        /// </summary>
        Runtime,

        /// <summary>
        /// <b>Only for stack!</b>
        /// <br/>
        /// <c>BasePointer + CurrentInstruction.ParameterInt</c>
        /// </summary>
        BasePointerRelative,

        /// <summary>
        /// <b>Only for stack!</b>
        /// <br/>
        /// <c>Memory.Stack.Count + CurrentInstruction.ParameterInt</c>
        /// </summary>
        StackRelative,
    }

    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Instruction : ISerializable<Instruction>, IFullySerializableText
    {
        public AddressingMode AddressingMode;
        public Opcode opcode;
        DataItem parameter;

        public DataItem Parameter
        {
            get => parameter;
            set => parameter = value;
        }

        [Obsolete("Only for deserialization", true)]
        public Instruction()
        {
            this.opcode = Opcode.UNKNOWN;
            this.AddressingMode = AddressingMode.Absolute;
            this.parameter = DataItem.Null;
        }

        public Instruction(Opcode opcode)
        {
            this.opcode = opcode;
            this.AddressingMode = AddressingMode.Absolute;
            this.parameter = DataItem.Null;
        }
        public Instruction(Opcode opcode, DataItem parameter)
        {
            this.opcode = opcode;
            this.AddressingMode = AddressingMode.Absolute;
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
            string result = $"{opcode}";

            if (opcode == Opcode.LOAD_VALUE ||
                opcode == Opcode.STORE_VALUE)
            { result += " " + AddressingMode.ToString(); }

            if (!this.parameter.IsNull)
            { result += $" {{ {parameter} }}"; }

            return result;
        }

        #region Serialize
        public void Serialize(Serializer serializer)
        {
            serializer.Serialize((byte)this.opcode);
            serializer.Serialize((byte)this.AddressingMode);
            serializer.Serialize(this.parameter);
        }

        public void Deserialize(Deserializer deserializer)
        {
            this.opcode = (Opcode)deserializer.DeserializeByte();
            this.AddressingMode = (AddressingMode)deserializer.DeserializeByte();
            DataItem dataItem = new();
            dataItem.Deserialize(deserializer);
            this.parameter = dataItem;
        }

        public Value SerializeText()
        {
            Value result = Value.Object();

            result["OpCode"] = Value.Literal(opcode.ToString());
            result["AddressingMode"] = Value.Literal(AddressingMode.ToString());
            result["Parameter"] = Value.Object(parameter);
            return result;
        }

        public void DeserializeText(Value data)
        {
            opcode = data["OpCode"].Enum<Opcode>();
            AddressingMode = data["AddressingMode"].Enum<AddressingMode>();
            DataItem dataItem = new();
            dataItem.DeserializeText(data["Parameter"]);
            parameter = dataItem;
        }
        #endregion
    }
}
