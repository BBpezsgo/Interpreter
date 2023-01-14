using IngameCoding.Serialization;

using System;

namespace IngameCoding.Bytecode
{
    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Instruction : ISerializable<Instruction>
    {
        public Opcode opcode;
        /// <summary>
        /// Can be:
        /// <list type="bullet">
        /// <item><see cref="null"/></item>
        /// <item><see cref="int"/></item>
        /// <item><see cref="bool"/></item>
        /// <item><see cref="float"/></item>
        /// <item><see cref="string"/></item>
        /// <item><see cref="IStruct"/></item>
        /// <item><see cref="DataItem.Struct"/></item>
        /// <item><see cref="DataItem.List"/></item>
        /// </list>
        /// </summary>
        public object parameter;
        /// <summary>Used for: <b>Only struct field names!</b></summary>
        public string additionParameter = string.Empty;
        /// <summary>Used for: <b>Only lists!</b> This is the value <c>.[i]</c></summary>
        public int additionParameter2 = -1;

        /// <summary>
        /// <b>Only for debugging!</b><br/>
        /// Sets the <see cref="DataItem.Tag"/> to this.<br/>
        /// Can use on:
        /// <list type="bullet">
        /// <item><see cref="Opcode.LOAD_VALUE_BR"/></item>
        /// <item><see cref="Opcode.LOAD_FIELD_BR"/></item>
        /// <item><see cref="Opcode.LOAD_VALUE"/></item>
        /// <item><see cref="Opcode.PUSH_VALUE"/></item>
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
            this.parameter = null;
        }
        public Instruction(Opcode opcode)
        {
            this.opcode = opcode;
            this.parameter = null;
        }
        public Instruction(Opcode opcode, object parameter)
        {
            this.opcode = opcode;
            this.parameter = parameter;
        }

        public Instruction(Opcode opcode, object parameter, string additionParameter)
        {
            this.opcode = opcode;
            this.parameter = parameter;
            this.additionParameter = additionParameter;
        }
        public Instruction(Opcode opcode, object parameter, int additionParameter)
        {
            this.opcode = opcode;
            this.parameter = parameter;
            this.additionParameter2 = additionParameter;
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
                if (additionParameter != string.Empty)
                {
                    str += ", " + additionParameter + "";
                }
                str += " }";
                return IsRunning + str;
            }
        }

        void ISerializable<Instruction>.Serialize(Serializer serializer)
        {
            serializer.Serialize((int)this.opcode);
            serializer.Serialize(this.tag);
            serializer.Serialize(this.additionParameter);
            serializer.Serialize(this.additionParameter2);
            if (this.parameter is int)
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
            else if (this.parameter is null)
            {
                serializer.Serialize((byte)0);
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        void ISerializable<Instruction>.Deserialize(Deserializer deserializer)
        {
            this.opcode = (Opcode)deserializer.DeserializeInt32();
            this.tag = deserializer.DeserializeString();
            this.additionParameter = deserializer.DeserializeString();
            this.additionParameter2 = deserializer.DeserializeInt32();
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
        }
    }
}
