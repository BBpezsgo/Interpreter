namespace ProgrammingLanguage.Bytecode
{
    public enum Opcode : byte
    {
        UNKNOWN = 0,
        COMMENT = 1,

        // === STACK OPERATIONS ===
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from Instruction
        /// </para>
        /// </summary>
        PUSH_VALUE,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        POP_VALUE,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> address</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from Instruction or value
        /// </para>
        /// <para><br/><br/>
        /// <b>Uses</b> <see cref="AddressingMode"/>
        /// </para>
        /// </summary>
        LOAD_VALUE,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>value</c><br/>
        ///     <c><see cref="int"/> address</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from value
        /// </para>
        /// <para><br/><br/>
        /// <b>Uses</b> <see cref="AddressingMode"/>
        /// </para>
        /// </summary>
        STORE_VALUE,
        // === ===

        // === FLOW CONTROL ===
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        EXIT,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>condition</c><br/>
        ///     <c><see cref="int"/> relative address</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// <para><br/><br/>
        /// <b>Uses</b> <see cref="AddressingMode"/>
        /// </para>
        /// </summary>
        JUMP_BY_IF_FALSE,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> relative address</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// <para><br/><br/>
        /// <b>Uses</b> <see cref="AddressingMode"/>
        /// </para>
        /// </summary>
        JUMP_BY,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> relative address</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     <c>"saved base pointer"</c>
        /// </para>
        /// <para><br/><br/>
        /// <b>Uses</b> <see cref="AddressingMode"/>
        /// </para>
        /// </summary>
        CALL,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> saved base address</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        RETURN,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>parameters</c> (as much as needed)<br/>
        ///     <c><see cref="int"/> function name length</c><br/>
        ///     <c><see cref="int"/> function name address</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from external or <c>"return v"</c>
        /// </para>
        /// </summary>
        CALL_BUILTIN,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        GET_BASEPOINTER,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>(<see cref="string"/>) value</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        THROW,
        // === ===

        // === LOGIC OPERATIONS ===
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        LOGIC_LT,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        LOGIC_MT,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        LOGIC_LTEQ,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        LOGIC_MTEQ,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        LOGIC_AND,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        LOGIC_OR,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        LOGIC_XOR,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        LOGIC_EQ,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        LOGIC_NEQ,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>value</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from value
        /// </para>
        /// </summary>
        LOGIC_NOT,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        BITSHIFT_LEFT,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        BITSHIFT_RIGHT,
        // === ===

        // === MATH OPERATIONS ===
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        MATH_ADD,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        MATH_SUB,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        MATH_MULT,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        MATH_DIV,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>left</c><br/>
        ///     <c>right</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from left side
        /// </para>
        /// </summary>
        MATH_MOD,
        // === ===

        // === HEAP OPERATIONS ===
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> address</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from value
        /// </para>
        /// <para><br/><br/>
        /// <b>Uses</b> <see cref="AddressingMode"/>
        /// </para>
        /// </summary>
        HEAP_GET,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>value</c><br/>
        ///     <c><see cref="int"/> address</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from value
        /// </para>
        /// <para><br/><br/>
        /// <b>Uses</b> <see cref="AddressingMode"/>
        /// </para>
        /// </summary>
        HEAP_SET,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> size</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from Instruction
        /// </para>
        /// </summary>
        HEAP_ALLOC,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> pointer</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     none
        /// </para>
        /// </summary>
        HEAP_DEALLOC,
        // === ===

        // === TYPE OPERATINS ===
        TYPE_GET,
        TYPE_SET,
        // === ===

        // === Debug ===
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>value</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from Instruction
        /// </para>
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
    }
}
