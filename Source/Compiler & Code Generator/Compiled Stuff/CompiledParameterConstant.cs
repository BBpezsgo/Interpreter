using System;

namespace LanguageCore.Compiler;

using Parser;
using Runtime;

public class CompiledParameterConstant : CompiledConstant
{
    public readonly ParameterDefinition Declaration;
    public override string Identifier => Declaration.Identifier.Content;
    public override Uri? FilePath => null;
    public override Position Position => Declaration.Position;

    public CompiledParameterConstant(ParameterDefinition declaration, DataItem value) : base(value)
    {
        Declaration = declaration;
    }
}
