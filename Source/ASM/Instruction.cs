namespace LanguageCore.ASM;

public enum Instruction
{
    #region General Purpose Data Transfers

    /// <summary>
    /// <b>MOV:</b>
    /// Copies the second operand (source operand) to the first operand (destination operand).
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST = SRC
    /// </code>
    /// </remarks>
    Move,

    /// <summary>
    /// <b>PUSH:</b>
    /// Decrements the SP by 2 and then stores the source operand on the top of the stack now pointed to by SP.
    /// </summary>
    Push,

    /// <summary>
    /// <b>POP:</b>
    /// Loads the word from the top of the stack (pointed to by SP) to the location specified with the destination operand and then increments SP by 2.
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST = Pop()
    /// </code>
    /// </remarks>
    Pop,

    /// <summary>
    /// <b>LEA:</b>
    /// Computes the effective address of the second operand (the source operand) and stores it in the first operand (destination operand).
    /// </summary>
    LoadEA,

    #endregion

    #region Arithmetic Instructions

    /// <summary>
    /// <b>ADD:</b>
    /// Adds the destination operand (first operand) and the source operand (second operand) and then stores the result in the destination operand.
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST += SRC
    /// </code>
    /// </remarks>
    MathAdd,

    /// <summary>
    /// <b>SUB:</b>
    /// Subtracts the second operand (source operand) from the first operand (destination operand) and stores the result in the destination operand.
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST -= SRC
    /// </code>
    /// </remarks>
    MathSub,

    /// <summary>
    /// <b>CMP:</b>
    /// Compares the first source operand with the second source operand and sets the status flags in the EFLAGS register according to the results.
    /// The comparison is performed by subtracting the second operand from the first operand and then setting the status flags in the same manner as the <see cref="MathSub"/> instruction. When an immediate value is used as an operand, it is sign-extended to the length of the first operand.
    /// </summary>
    Compare,

    /// <summary>
    /// <b>MUL</b>
    /// </summary>
    MathMult,

    /// <summary>
    /// <b>DIV</b>
    /// </summary>
    MathDiv,

    /// <summary>
    /// <b>IMUL</b>
    /// </summary>
    IMathMult,

    /// <summary
    /// <b>IDIV</b>
    /// </summary>
    IMathDiv,

    /// <summary>
    /// <b>AND:</b>
    /// Performs a bitwise AND operation on the destination (first) and source (second) operands and stores the result in the destination operand location.
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST &amp;= SRC
    /// </code>
    /// </remarks>
    BitsAND,

    /// <summary>
    /// <b>OR:</b>
    /// Performs a bitwise inclusive OR operation between the destination (first) and source (second) operands and stores the result in the destination operand location.
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST |= SRC
    /// </code>
    /// </remarks>
    BitsOR,

    /// <summary>
    /// <b>XOR:</b>
    /// Performs a bitwise exclusive OR (XOR) operation on the destination (first) and source (second) operands and stores the result in the destination operand location.
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST ^= SRC
    /// </code>
    /// </remarks>
    BitsXOR,

    /// <summary>
    /// Computes the bit-wise logical AND of first operand (source 1 operand) and the second operand (source 2 operand) and updates the flags according to the result.
    /// The result is then discarded.
    /// </summary>
    Test,

    /// <summary>
    /// <b>SHR</b>
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST &gt;&gt;= SRC
    /// </code>
    /// </remarks>
    BitsShiftRight,

    /// <summary>
    /// <b>SHL</b>
    /// </summary>
    ///
    /// <remarks>
    /// <code>
    /// DST &lt;&lt;= SRC
    /// </code>
    /// </remarks>
    BitsShiftLeft,

    #endregion

    #region Program Transfer Instructions

    /// <summary>
    /// <b>CALL:</b>
    /// Saves procedure linking information on the stack and branches to the called procedure specified using the target operand.
    /// </summary>
    Call,

    /// <summary>
    /// <b>RET:</b>
    /// Transfers program control to a return address located on the top of the stack.
    /// The address is usually placed on the stack by a <see cref="Call"/> instruction, and the return is made to the instruction that follows the <see cref="Call"/> instruction.
    /// </summary>
    Return,

    /// <summary>
    /// <b>JMP:</b>
    /// Transfers program control to a different point in the instruction stream without recording return information.
    /// The destination (target) operand specifies the address of the instruction being jumped to.
    /// </summary>
    Jump,

    #region Conditional Jumps

    /// <summary>
    /// <b>JNE:</b>
    /// Jump if not equal
    /// </summary>
    ///
    /// <remarks>
    /// Condition: <c>ZF == <see langword="false"/></c>
    /// </remarks>
    JumpIfNotEQ,

    /// <summary>
    /// <b>JGE:</b>
    /// Jump if greater than or equal
    /// </summary>
    ///
    /// <remarks>
    /// Condition: <c>(SF xor OF) == <see langword="false"/></c>
    /// </remarks>
    JumpIfGEQ,

    /// <summary>
    /// <b>JG:</b>
    /// Jump if greater than
    /// </summary>
    ///
    /// <remarks>
    /// Condition: <c>((SF xor OF) or ZF) == <see langword="false"/></c>
    /// </remarks>
    JumpIfG,

    /// <summary>
    /// <b>JLE:</b>
    /// Jump if less than or equal
    /// </summary>
    ///
    /// <remarks>
    /// Condition: <c>((SF xor OF) or ZF) == <see langword="true"/></c>
    /// </remarks>
    JumpIfLEQ,

    /// <summary>
    /// <b>JL:</b>
    /// Jump if less than
    /// </summary>
    ///
    /// <remarks>
    /// Condition: <c>(SF xor OF) == <see langword="true"/></c>
    /// </remarks>
    JumpIfL,

    /// <summary>
    /// <b>JE:</b>
    /// Jump if equal
    /// </summary>
    ///
    /// <remarks>
    /// Condition: <c>ZF == <see langword="true"/></c>
    /// </remarks>
    JumpIfEQ,

    /// <summary>
    /// <b>JZ:</b>
    /// Jump if zero
    /// </summary>
    ///
    /// <remarks>
    /// Condition: <c>ZF == <see langword="true"/></c>
    /// </remarks>
    JumpIfZero,

    #endregion

    #endregion

    /// <summary>
    /// <b>HLT</b>
    /// Stops instruction execution and places the processor in a halt state.
    /// </summary>
    Halt,

    /// <summary>
    /// <b>CBW:</b>
    /// Extends the sign of the byte in register AL throughout register AH.
    /// </summary>
    ConvertByteToWord,

    /// <summary>
    /// <b>CWD:</b>
    /// Extends the sign of the word in register AX throughout register DX.
    /// </summary>
    ConvertWordToDoubleword,
}
