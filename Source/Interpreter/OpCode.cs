namespace LanguageCore.Runtime;

public enum Opcode : byte
{
    _ = 0,

    #region === STACK OPERATIONS ===
    /// <summary>
    /// <b>Stack elements added:</b><br/>
    /// <c><see cref="PreparationInstruction.parameter"/></c><br/>
    /// </summary>
    Push,
    /// <summary>
    /// <b>Expected stack elements:</b><br/>
    /// <c>...</c><br/>
    /// <c>any value</c><br/>
    /// </summary>
    Pop,

    /// <summary>
    /// <para>
    /// <b>Expected stack elements:</b><br/>
    /// <c>...</c><br/>
    /// <c><see cref="int"/> address</c> (optional)<br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.FetchStackAddress"/>)
    /// </para>
    /// </summary>
    StackLoad,
    /// <summary>
    /// <para>
    /// <b>Expected stack elements:</b><br/>
    /// <c>...</c><br/>
    /// <c>value</c><br/>
    /// <c><see cref="int"/> address</c> (optional)<br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.FetchStackAddress"/>)
    /// </para>
    /// </summary>
    StackStore,
    #endregion

    #region === FLOW CONTROL ===
    Exit,

    /// <summary>
    /// <para>
    ///     <b>Expected stack elements:</b><br/>
    ///     <c>...</c><br/>
    ///     <c><see cref="int"/> relative address</c> (depends on the addressing mode)<br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    ///     <b>Stack elements added:</b><br/>
    ///     <c><see cref="int"/> saved CP</c><br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.FetchData"/>)
    /// </para>
    /// </summary>
    Call,
    /// <summary>
    /// <para>
    /// <b>Expected stack elements:</b><br/>
    /// <c>...</c><br/>
    /// <c><see cref="int"/> CP</c><br/>
    /// </para>
    /// </summary>
    Return,

    /// <summary>
    /// <para>
    /// <b>Expected stack elements:</b><br/>
    /// <c>...</c><br/>
    /// <c>condition</c><br/>
    /// <c><see cref="int"/> relative address</c> (optional)<br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.FetchData"/>)
    /// </para>
    /// </summary>
    JumpIfZero,
    /// <summary>
    /// <para>
    /// <b>Expected stack elements:</b><br/>
    /// <c>...</c><br/>
    /// <c><see cref="int"/> relative address</c> (optional)<br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.FetchData"/>)
    /// </para>
    /// </summary>
    Jump,

    /// <summary>
    /// <para>
    /// <b>Expected stack elements:</b><br/>
    /// <c>...</c><br/>
    /// <c>parameters</c> (as much as needed)<br/>
    /// <c><see cref="int"/> function name address (string pointer)</c><br/>
    /// </para>
    /// </summary>
    CallExternal,

    /// <summary>
    /// <para>
    /// <b>Stack elements added:</b><br/>
    /// <c><see cref="int"/> basepointer</c><br/>
    /// </para>
    /// </summary>
    GetBasePointer,

    /// <summary>
    /// <para>
    ///     <b>Expected stack elements:</b><br/>
    ///     <c>...</c><br/>
    ///     <c><see cref="int"/> value</c> (optional)<br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    ///     <see cref="AddressingMode"/>s:
    ///     <list type="table">
    ///         <item>
    ///             <term>
    ///                 <see cref="AddressingMode.Runtime"/>
    ///             </term>
    ///             <description>
    ///                 Pops an element from the stack and uses it as a new value
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>
    ///                 <see cref="AddressingMode.Absolute"/>
    ///             </term>
    ///             <description>
    ///                 Uses the instruction's parameter as a new value
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>
    ///                 <see cref="AddressingMode.StackRelative"/>
    ///             </term>
    ///             <description>
    ///                 Uses the stack's size as a new value and adds the instruction's parameter as an offset
    ///             </description>
    ///         </item>
    ///     </list>
    /// </para>
    /// </summary>
    SetBasePointer,

    /// <summary>
    /// <para>
    ///     <b>Expected stack elements:</b><br/>
    ///     <c>...</c><br/>
    ///     <c><see cref="int"/> value</c> (optional)<br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    ///     <see cref="AddressingMode"/>s:
    ///     <list type="table">
    ///         <item>
    ///             <term>
    ///                 <see cref="AddressingMode.Runtime"/>
    ///             </term>
    ///             <description>
    ///                 Pops an element from the stack and uses it as a new value
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <term>
    ///                 <see cref="AddressingMode.Absolute"/>
    ///             </term>
    ///             <description>
    ///                 Uses the instruction's parameter as a new value
    ///             </description>
    ///         </item>
    ///     </list>
    /// </para>
    /// </summary>
    SetCodePointer,

    /// <summary>
    /// <para>
    /// <b>Expected stack elements:</b><br/>
    /// <c>...</c><br/>
    /// <c>*<see cref="string"/> message</c><br/>
    /// </para>
    /// </summary>
    Throw,
    #endregion

    #region === LOGIC OPERATIONS ===
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
    /// </summary>
    LogicLT,
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
    /// </summary>
    LogicMT,
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
    /// </summary>
    LogicLTEQ,
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
    /// </summary>
    LogicMTEQ,
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
    /// </summary>
    LogicOR,
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
    /// </summary>
    LogicAND,
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
    /// </summary>
    LogicEQ,
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
    /// </summary>
    LogicNEQ,
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
    /// </summary>
    LogicNOT,
    #endregion

    #region === BITWISE OPERATIONS ===
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
    /// </summary>
    BitsAND,
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
    /// </summary>
    BitsOR,
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
    /// </summary>
    BitsXOR,
    /// <summary>
    /// <para>
    ///     <b>Expected stack elements:</b><br/>
    ///     <c>...</c><br/>
    ///     <c>left</c><br/>
    /// </para>
    /// <br/><br/>
    ///
    /// <para>
    ///     <b>Stack elements added:</b><br/>
    ///     <c>result</c><br/>
    /// </para>
    /// </summary>
    BitsNOT,

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
    /// </summary>
    BitsShiftLeft,
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
    /// </summary>
    BitsShiftRight,
    #endregion

    #region === MATH OPERATIONS ===
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
    /// </summary>
    MathAdd,
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
    /// </summary>
    MathSub,
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
    /// </summary>
    MathMult,
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
    /// </summary>
    MathDiv,
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
    /// </summary>
    MathMod,
    #endregion

    #region === HEAP OPERATIONS ===
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
    /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.FetchData"/>)
    /// </para>
    /// </summary>
    HeapGet,
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
    /// <b>Uses</b> <see cref="AddressingMode"/> (<see cref="BytecodeProcessor.FetchData"/>)
    /// </para>
    /// </summary>
    HeapSet,

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
    /// </summary>
    Allocate,
    /// <summary>
    /// <para>
    ///     <b>Expected stack elements:</b><br/>
    ///     <c>...</c><br/>
    ///     <c><see cref="int"/> pointer</c><br/>
    /// </para>
    /// </summary>
    Free,
    #endregion

    #region === TYPE OPERATINS ===
    TypeGet,
    TypeSet,
    #endregion
}
