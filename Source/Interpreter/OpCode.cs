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

        BITSHIFT_LEFT,
        BITSHIFT_RIGHT,

        MATH_ADD,
        MATH_SUB,
        MATH_MULT,
        MATH_DIV,
        MATH_MOD,
        // === ===

        // === Structs ===
        LOAD_FIELD,
        STORE_FIELD,
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
        /// <summary>
        /// Sets a value at the given index
        /// </summary>
        LIST_SET_ITEM,
        // === ===

        // === HEAP ===
        HEAP_GET,
        HEAP_SET,
        // === ===

        TYPE_GET,
        TYPE_CAST,

        // === Debug ===
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
        // === ===

        /// <summary>
        /// Replaces the last element with its own copy
        /// </summary>
        COPY_VALUE,
        /// <summary>
        /// Replaces the last element with its own recursive copy
        /// </summary>
        COPY_VALUE_RECURSIVE,

        GET_BASEPOINTER,

        FIND_HEAP_FREE_SPACE,
    }
}
