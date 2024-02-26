using System;

namespace LanguageCore.Compiler;

using Runtime;

public abstract class CompiledConstant : IPositioned
{
    public readonly DataItem Value;
    public abstract string Identifier { get; }
    public abstract Uri? FilePath { get; }
    public abstract Position Position { get; }

    protected CompiledConstant(DataItem value)
    {
        Value = value;
    }
}
