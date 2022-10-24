#if DEBUG
using System.Diagnostics;
#endif

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
        /// STORE_VALUE_BASEPOINTER_RELATIVE
        /// </summary>
        STORE_VALUE_BR,

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

        /// <summary>
        /// LOAD_FIELD_BASEPOINTER_RELATIVE
        /// </summary>
        LOAD_FIELD_BR,
        /// <summary>
        /// STORE_FIELD_BASEPOINTER_RELATIVE
        /// </summary>
        STORE_FIELD_BR,

        LOAD_THIS_FIELD,
        STORE_THIS_FIELD,
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

        TYPE_GET,

        COMMENT,
        UNKNOWN,
    }

    [Serializable]
#if DEBUG
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
#endif
    public class Instruction
    {
        public Opcode opcode;
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

        private string GetDebuggerDisplay()
        {
            return ToString();
        }
    }
}
