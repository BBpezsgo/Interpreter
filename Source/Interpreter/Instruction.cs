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
        object parameter;

        public int ParameterInt
        {
            get
            {
                if (parameter == null) throw new Errors.InternalException($"Can't cast null to {nameof(Int32)}");
                if (parameter is int @int) return @int;
                if (parameter is DataItem dataItem && dataItem.type == RuntimeType.INT) return dataItem.ValueInt;
                throw new Errors.InternalException($"Can't cast {parameter.GetType().Name} to {nameof(Int32)}"); ;
            }
        }
        public DataItem ParameterData
        {
            get
            {
                if (parameter == null) return DataItem.Null;
                if (parameter is int @int) return new DataItem(@int, null);
                if (parameter is float @float) return new DataItem(@float, null);
                if (parameter is byte @byte) return new DataItem(@byte, null);
                if (parameter is char @char) return new DataItem(@char, null);
                if (parameter is bool @bool) return new DataItem(@bool, null);
                if (parameter is DataItem dataItem) return dataItem;
                return DataItem.Null;
            }
        }
        /// <summary>
        /// Can be:
        /// <list type="bullet">
        /// <item><see langword="null"/></item>
        /// <item><see cref="int"/></item>
        /// <item><see cref="bool"/></item>
        /// <item><see cref="float"/></item>
        /// <item><see cref="string"/></item>
        /// <item><see cref="char"/></item>
        /// </list>
        /// </summary>
        public object Parameter
        {
            get => parameter;
            set => parameter = value;
        }

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
                string str = "";
                str += opcode.ToString();
                if (opcode == Opcode.LOAD_VALUE ||
                    opcode == Opcode.STORE_VALUE)
                {
                    str += " " + AddressingMode.ToString();
                }
                if (this.parameter == null)
                {
                    str += " { " + "<null>";
                }
                else
                {
                    str += " { " + parameter.ToString();
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
            else if (this.parameter is char @char)
            {
                serializer.Serialize((byte)5);
                serializer.Serialize(@char);
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
                case RuntimeType.INT:
                    serializer.Serialize(dataItem.ValueInt);
                    break;
                case RuntimeType.FLOAT:
                    serializer.Serialize(dataItem.ValueFloat);
                    break;
                case RuntimeType.BOOLEAN:
                    serializer.Serialize(dataItem.ValueBoolean);
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
            RuntimeType type = (RuntimeType)deserializer.DeserializeInt32();
            string tag = deserializer.DeserializeString();

            switch (type)
            {
                case RuntimeType.INT:
                    return new DataItem(deserializer.DeserializeInt32(), tag);
                case RuntimeType.FLOAT:
                    return new DataItem(deserializer.DeserializeFloat(), tag);
                case RuntimeType.BOOLEAN:
                    return new DataItem(deserializer.DeserializeBoolean(), tag);
                case RuntimeType.CHAR:
                    return new DataItem(deserializer.DeserializeChar(), tag);
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
                case RuntimeType.INT:
                    result["Value"] = Value.Literal(dataItem.ValueInt);
                    return result;
                case RuntimeType.FLOAT:
                    result["Value"] = Value.Literal(dataItem.ValueFloat);
                    return result;
                case RuntimeType.BOOLEAN:
                    result["Value"] = Value.Literal(dataItem.ValueBoolean);
                    return result;
                case RuntimeType.CHAR:
                    result["Value"] = Value.Literal(dataItem.ValueChar);
                    return result;
                default:
                    throw new Errors.InternalException($"Unknown type {dataItem.type}");
            }
        }
        DataItem DeserializeTextDataItem(Value data)
        {
            RuntimeType type = (RuntimeType)data["Type"].Int;
            string tag = data["Tag"].String;

            switch (type)
            {
                case RuntimeType.INT:
                    return new DataItem((int)data["Value"].Int, tag);
                case RuntimeType.FLOAT:
                    return new DataItem((float)data["Value"].Float, tag);
                case RuntimeType.BOOLEAN:
                    return new DataItem((bool)data["Value"].Bool, tag);
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
            var parameterType = deserializer.DeserializeByte();
            if (parameterType == 0)
            {
                this.parameter = null;
            }
            else if (parameterType == 1)
            {
                this.parameter = deserializer.DeserializeInt32();
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
                this.parameter = deserializer.DeserializeChar();
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
            else if (this.parameter is char @char)
            {
                result["ParameterType"] = Value.Literal(5);
                result["ParameterValue"] = Value.Literal(@char);
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
                this.parameter = (char)data["ParameterValue"].Int;
            }
            else
            { throw new NotImplementedException(); }
        }
    }
}
