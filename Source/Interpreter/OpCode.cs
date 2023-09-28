namespace ProgrammingLanguage.Bytecode
{
    public enum Opcode : byte
    {
        UNKNOWN = 0,
        COMMENT = 1,

        // === STACK OPERATIONS ===
        /// <summary>
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c><see cref="Instruction.parameter"/></c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        ///     <c>any value</c><br/>
        /// </para>
        /// <br/><br/>
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from Instruction or value
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.GetStackAddress"/>)
        /// </para>
        /// </summary>
        LOAD_VALUE,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>value</c><br/>
        ///     <c><see cref="int"/> address</c> (optional)<br/>
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from value
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.GetStackAddress"/>)
        /// </para>
        /// </summary>
        STORE_VALUE,
        // === ===

        // === FLOW CONTROL ===
        EXIT,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>condition</c><br/>
        ///     <c><see cref="int"/> relative address</c> (optional)<br/>
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.GetAddress"/>)
        /// </para>
        /// </summary>
        JUMP_BY_IF_FALSE,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> relative address</c> (optional)<br/>
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.GetAddress"/>)
        /// </para>
        /// </summary>
        JUMP_BY,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>parameters</c> (as much as needed)<br/>
        ///     <c><see cref="int"/> function name address (string pointer)</c><br/>
        /// </para><br/><br/>
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from external or <c>"return v"</c>
        /// </para>
        /// </summary>
        CALL_EXTERNAL,

        /// <summary>
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c><see cref="int"/> basepointer</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     "basepointer"
        /// </para>
        /// </summary>
        GET_BASEPOINTER,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> value</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para><br/><br/>
        ///     <see cref="AddressingMode"/>s:
        ///     <list type="table">
        ///         <item>
        ///             <term>
        ///                 <see cref="AddressingMode.RUNTIME"/>
        ///             </term>
        ///             <description>
        ///                 Pops an element from the stack and uses it as a new value
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>
        ///                 <see cref="AddressingMode.ABSOLUTE"/>
        ///             </term>
        ///             <description>
        ///                 Uses the instruction's parameter as a new value
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>
        ///                 <see cref="AddressingMode.RELATIVE"/>
        ///             </term>
        ///             <description>
        ///                 Uses the stack's size as a new value and adds the instruction's parameter as an offset
        ///             </description>
        ///         </item>
        ///     </list>
        /// </para>
        /// </summary>
        SET_BASEPOINTER,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> value</c> (optional)<br/>
        /// </para><br/><br/>
        /// <para><br/><br/>
        ///     <see cref="AddressingMode"/>s:
        ///     <list type="table">
        ///         <item>
        ///             <term>
        ///                 <see cref="AddressingMode.RUNTIME"/>
        ///             </term>
        ///             <description>
        ///                 Pops an element from the stack and uses it as a new value
        ///             </description>
        ///         </item>
        ///         <item>
        ///             <term>
        ///                 <see cref="AddressingMode.ABSOLUTE"/>
        ///             </term>
        ///             <description>
        ///                 Uses the instruction's parameter as a new value
        ///             </description>
        ///         </item>
        ///     </list>
        /// </para>
        /// </summary>
        SET_CODEPOINTER,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>*<see cref="string"/> message</c><br/>
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c>result</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from value
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.GetAddress"/>)
        /// </para>
        /// </summary>
        HEAP_GET,
        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c>value</c><br/>
        ///     <c><see cref="int"/> address</c> (optional)<br/>
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Tag:</b><br/>
        ///     Inherits from value
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.GetAddress"/>)
        /// </para>
        /// </summary>
        HEAP_SET,

        /// <summary>
        /// <para>
        ///     <b>Expected stack elements:</b><br/>
        ///     <c>...</c><br/>
        ///     <c><see cref="int"/> size</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
        /// <para>
        ///     <b>Stack elements added:</b><br/>
        ///     <c><see cref="int"/> pointer</c><br/>
        /// </para>
        /// <br/><br/>
        /// 
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
        /// </para>
        /// <br/><br/>
        /// 
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
