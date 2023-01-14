namespace IngameCoding.Bytecode
{
    public enum Opcode
    {
        UNKNOWN = 0,
        COMMENT = 1,

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
    }
}
