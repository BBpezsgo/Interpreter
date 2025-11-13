namespace LanguageCore.Runtime;

public enum Opcode : byte
{
    NOP = 0,

    #region === STACK OPERATIONS ===
    Push,
    PopTo8,
    PopTo16,
    PopTo32,
    PopTo64,
    #endregion

    #region === FLOW CONTROL ===
    Exit,
    Call,
    Return,

    JumpIfEqual,
    JumpIfNotEqual,
    JumpIfGreaterS,
    JumpIfGreaterU,
    JumpIfGreaterOrEqualS,
    JumpIfGreaterOrEqualU,
    JumpIfLessS,
    JumpIfLessU,
    JumpIfLessOrEqualS,
    JumpIfLessOrEqualU,
    Jump,

    CallExternal,
    CallMSIL,
    HotFuncEnd,

    Crash,
    #endregion

    #region === COMPARISON OPERATIONS ===

    Compare,
    CompareF,

    #endregion

    #region === LOGIC OPERATIONS ===
    LogicOR,
    LogicAND,
    #endregion

    #region === BITWISE OPERATIONS ===
    BitsAND,
    BitsOR,
    BitsXOR,
    BitsNOT,

    BitsShiftLeft,
    BitsShiftRight,
    #endregion

    #region === MATH OPERATIONS ===
    MathAdd,
    MathSub,
    MathMultS,
    MathMultU,
    MathDivS,
    MathDivU,
    MathModS,
    MathModU,
    #endregion

    #region === FLOAT MATH OPERATIONS ===
    FMathAdd,
    FMathSub,
    FMathMult,
    FMathDiv,
    FMathMod,
    #endregion

    #region === REGISTERS ===

    Move,

    #endregion

    #region === FLOATS ===

    FTo,
    FFrom,

    #endregion
}
