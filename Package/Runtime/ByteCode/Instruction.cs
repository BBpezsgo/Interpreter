using IngameCoding.Serialization;

using System;

namespace IngameCoding.Bytecode
{
    public enum Opcode
    {
        EXIT,
        PUSH_VALUE,
        POP_VALUE,

        JUMP_BY_IF_FALSE,
        JUMP_BY_IF_TRUE,
        JUMP_BY,

        LOAD_VALUE,
        STORE_VALUE,

        /// <summary>
        /// LOAD_VALUE_BASEPOINTER_RELATIVE
        /// </summary>
        LOAD_VALUE_BR,
        /// <summary>
        /// LOAD_VALUE_RELATIVE
        /// </summary>
        LOAD_VALUE_R,
        /// <summary>
        /// STORE_VALUE_BASEPOINTER_RELATIVE
        /// </summary>
        STORE_VALUE_BR,
        /// <summary>
        /// STORE_VALUE_RELATIVE
        /// </summary>
        STORE_VALUE_R,

        LOAD_VALUE_AS_REF,
        STORE_VALUE_AS_REF,

        /// <summary>
        /// LOAD_VALUE_BASEPOINTER_RELATIVE_AS_REF
        /// </summary>
        LOAD_VALUE_BR_AS_REF,
        /// <summary>
        /// STORE_VALUE_BASEPOINTER_RELATIVE_AS_REF
        /// </summary>
        STORE_VALUE_BR_AS_REF,

        CALL,
        RETURN,

        CALL_BUILTIN,

        // === ALU ===
        /// <summary> LESS_THAN </summary>
        LOGIC_LT,
        /// <summary> MORE_THAN </summary>
        LOGIC_MT,
        /// <summary> LESS_THAN or EQUAL </summary>
        LOGIC_LTEQ,
        /// <summary> MORE_THAN or EQUAL </summary>
        LOGIC_MTEQ,
        LOGIC_AND,
        LOGIC_OR,
        LOGIC_XOR,
        /// <summary> EQUAL </summary>
        LOGIC_EQ,
        /// <summary> NOT_EQUAL </summary>
        LOGIC_NEQ,
        /// <summary> NOT </summary>
        LOGIC_NOT,

        MATH_ADD,
        MATH_SUB,
        MATH_MULT,
        MATH_DIV,
        MATH_MOD,
        // === ===

        // === Structs ===
        LOAD_FIELD,
        STORE_FIELD,

        LOAD_FIELD_R,

        /// <summary>
        /// LOAD_FIELD_BASEPOINTER_RELATIVE
        /// </summary>
        LOAD_FIELD_BR,
        /// <summary>
        /// STORE_FIELD_BASEPOINTER_RELATIVE
        /// </summary>
        STORE_FIELD_BR,
        // === ===

        // === Lists ===
        LIST_INDEX,
        /// <summary>
        /// Adds new item to the end of list
        /// </summary>
        LIST_PUSH_ITEM,
        /// <summary>
        /// Adds new item to the list at the given index
        /// </summary>
        LIST_ADD_ITEM,
        /// <summary>
        /// Removes new item to the end of list
        /// </summary>
        LIST_PULL_ITEM,
        /// <summary>
        /// Removes new item to the list at the given index
        /// </summary>
        LIST_REMOVE_ITEM,
        // === ===

        // === HEAP ===
        HEAP_GET,
        HEAP_SET,
        // === ===

        TYPE_GET,

        /// <summary>
        /// Sets the last stack item's tag
        /// </summary>
        DEBUG_SET_TAG,

        /// <summary>
        /// Call Stack Push
        /// </summary>
        CS_PUSH,
        /// <summary>
        /// Call Stack Pop
        /// </summary>
        CS_POP,

        COMMENT,
        UNKNOWN,
    }

    [Serializable]
    [System.Diagnostics.DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class Instruction : IngameCoding.Serialization.ISerializable<Instruction>
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
        /// <item><see cref="DataStack.IStruct"/></item>
        /// <item><see cref="DataItem.Struct"/></item>
        /// <item><see cref="DataItem.List"/></item>
        /// </list>
        /// </summary>
        public object parameter;
        /// <summary>
        /// Used for:
        /// <list type="bullet">
        /// <item>Builtin function calls</item>
        /// <item>Struct field names</item>
        /// </list>
        /// </summary>
        public string additionParameter = string.Empty;
        /// <summary>Used for: Only lists! This is the value []</summary>
        public int additionParameter2 = -1;

        /// <summary>
        /// Only for debugging:<br/>
        /// sets the Stack item <b>Tag</b> value to this.<br/>
        /// Can use on:
        /// <list type="bullet">
        /// <item><see cref="Opcode.LOAD_VALUE_BR"/></item>
        /// <item><see cref="Opcode.LOAD_FIELD_BR"/></item>
        /// <item><see cref="Opcode.LOAD_VALUE"/></item>
        /// <item><see cref="Opcode.PUSH_VALUE"/></item>
        /// </list>
        /// </summary>
        public string tag = string.Empty;

        internal int? index;
        internal CentralProcessingUnit cpu;

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

        /// <param name="additionParameter">For builtin function calls</param>
        public Instruction(Opcode opcode, object parameter, string additionParameter)
        {
            this.opcode = opcode;
            this.parameter = parameter;
            this.additionParameter = additionParameter;
        }

        /// <param name="additionParameter">Only for lists! This is the value []</param>
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

        private string GetDebuggerDisplay() => ToString();

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
