using LanguageCore.Compiler;

namespace LanguageCore.Parser.Statements;

public abstract class Expression : Statement
{
    /// <summary>
    /// Set by the <see cref="Parser"/>
    /// </summary>
    public bool SaveValue { get; internal set; } = true;
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public GeneralType? CompiledType { get; internal set; }
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public CompiledValue? PredictedValue { get; internal set; }
    /// <summary>
    /// Set by the compiler
    /// </summary>
    public TokenPair? SurroundingBrackets { get; internal set; }

    protected Expression(Uri file) : base(file)
    {
        SaveValue = true;
        CompiledType = null;
        PredictedValue = null;
        SurroundingBrackets = null;
    }
}
