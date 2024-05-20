namespace LanguageCore.Runtime;

public enum Opcode : byte
{
    _ = 0,

    #region === STACK OPERATIONS ===
    /// <summary>
    /// <code>
    /// Push(<paramref name="SRC"/>)
    /// </code>
    /// </summary>
    Push,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = Pop()
    /// </code>
    /// </summary>
    Pop,
    #endregion

    #region === FLOW CONTROL ===
    Exit,

    /// <summary>
    /// <code>
    /// Push(CodePointer);
    /// CodePointer += <paramref name="SRC"/>;
    /// </code>
    /// </summary>
    Call,

    /// <summary>
    /// <code>
    /// CodePointer = Pop()
    /// </code>
    /// </summary>
    Return,

    /// <summary>
    /// <code>
    /// <see langword="if"/> (<see cref="Flags.Zero"/> == 1)
    /// {
    ///   CodePointer += <paramref name="SRC"/>;
    /// }
    /// </code>
    /// </summary>
    JumpIfEqual,

    /// <summary>
    /// <code>
    /// <see langword="if"/> (<see cref="Flags.Zero"/> == 0)
    /// {
    ///   CodePointer += <paramref name="SRC"/>;
    /// }
    /// </code>
    /// </summary>
    JumpIfNotEqual,

    /// <summary>
    /// <code>
    /// <see langword="if"/> (((<see cref="Flags.Sign"/> ^ <see cref="Flags.Overflow"/>) | <see cref="Flags.Zero"/>) == 0)
    /// {
    ///   CodePointer += <paramref name="SRC"/>;
    /// }
    /// </code>
    /// </summary>
    JumpIfGreater,

    /// <summary>
    /// <code>
    /// <see langword="if"/> ((<see cref="Flags.Sign"/> ^ <see cref="Flags.Overflow"/>) == 0)
    /// {
    ///   CodePointer += <paramref name="SRC"/>;
    /// }
    /// </code>
    /// </summary>
    JumpIfGreaterOrEqual,

    /// <summary>
    /// <code>
    /// <see langword="if"/> ((<see cref="Flags.Sign"/> ^ <see cref="Flags.Overflow"/>) == 1)
    /// {
    ///   CodePointer += <paramref name="SRC"/>;
    /// }
    /// </code>
    /// </summary>
    JumpIfLess,

    /// <summary>
    /// <code>
    /// <see langword="if"/> (((<see cref="Flags.Sign"/> ^ <see cref="Flags.Overflow"/>) | <see cref="Flags.Zero"/>) == 1)
    /// {
    ///   CodePointer += <paramref name="SRC"/>;
    /// }
    /// </code>
    /// </summary>
    JumpIfLessOrEqual,

    /// <summary>
    /// <code>
    /// CodePointer += <paramref name="SRC"/>
    /// </code>
    /// </summary>
    Jump,

    /// <summary>
    /// <code>
    /// Push(<paramref name="EXT"/>(Pop(), Pop(), ...))
    /// </code>
    /// </summary>
    CallExternal,

    /// <summary>
    /// <code>
    /// <see langword="crash"/> (*<see cref="char"/>)<paramref name="SRC"/>
    /// </code>
    /// </summary>
    Throw,
    #endregion

    #region === COMPARISON OPERATIONS ===

    /// <summary>
    /// <code>
    /// <see langword="temp"/> = <paramref name="DST"/> - <paramref name="SRC"/>
    /// </code>
    /// </summary>
    Compare,

    #endregion

    #region === LOGIC OPERATIONS ===
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = <paramref name="DST"/> || <paramref name="SRC"/>
    /// </code>
    /// </summary>
    LogicOR,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = <paramref name="DST"/> &amp;&amp; <paramref name="SRC"/>
    /// </code>
    /// </summary>
    LogicAND,
    #endregion

    #region === BITWISE OPERATIONS ===
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = <paramref name="DST"/> &amp; <paramref name="SRC"/>
    /// </code>
    /// </summary>
    BitsAND,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = <paramref name="DST"/> | <paramref name="SRC"/>
    /// </code>
    /// </summary>
    BitsOR,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = <paramref name="DST"/> ^ <paramref name="SRC"/>
    /// </code>
    /// </summary>
    BitsXOR,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = ~<paramref name="DST"/>
    /// </code>
    /// </summary>
    BitsNOT,

    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = <paramref name="DST"/> &lt;&lt; <paramref name="SRC"/>
    /// </code>
    /// </summary>
    BitsShiftLeft,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = <paramref name="DST"/> &gt;&gt; <paramref name="SRC"/>
    /// </code>
    /// </summary>
    BitsShiftRight,
    #endregion

    #region === MATH OPERATIONS ===
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> += <paramref name="SRC"/>
    /// </code>
    /// </summary>
    MathAdd,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> -= <paramref name="SRC"/>
    /// </code>
    /// </summary>
    MathSub,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> *= <paramref name="SRC"/>
    /// </code>
    /// </summary>
    MathMult,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> /= <paramref name="SRC"/>
    /// </code>
    /// </summary>
    MathDiv,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> %= <paramref name="SRC"/>
    /// </code>
    /// </summary>
    MathMod,
    #endregion

    #region === FLOAT MATH OPERATIONS ===
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> += <paramref name="SRC"/>
    /// </code>
    /// </summary>
    FMathAdd,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> -= <paramref name="SRC"/>
    /// </code>
    /// </summary>
    FMathSub,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> *= <paramref name="SRC"/>
    /// </code>
    /// </summary>
    FMathMult,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> /= <paramref name="SRC"/>
    /// </code>
    /// </summary>
    FMathDiv,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> %= <paramref name="SRC"/>
    /// </code>
    /// </summary>
    FMathMod,
    #endregion

    #region === HEAP OPERATIONS ===

    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = alloc(<paramref name="SRC"/>)
    /// </code>
    /// </summary>
    Allocate,

    /// <summary>
    /// <code>
    /// free(<paramref name="SRC"/>)
    /// </code>
    /// </summary>
    Free,

    #endregion

    #region === REGISTERS ===

    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = <paramref name="SRC"/>
    /// </code>
    /// </summary>
    Move,

    #endregion

    #region === FLOATS ===

    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = (<see cref="float"/>)<paramref name="SRC"/>
    /// </code>
    /// </summary>
    FTo,
    /// <summary>
    /// <code>
    /// <paramref name="DST"/> = (<see cref="int"/>)<paramref name="SRC"/>
    /// </code>
    /// </summary>
    FFrom,

    #endregion
}
