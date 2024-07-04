namespace LanguageCore.ASM;

[ExcludeFromCodeCoverage]
public readonly struct IndentBlock : IDisposable
{
    readonly SectionBuilder Builder;

    public IndentBlock(SectionBuilder builder)
    {
        Builder = builder;
        Builder.Indent += SectionBuilder.IndentIncrement;
    }

    public void Dispose()
    {
        Builder.Indent -= SectionBuilder.IndentIncrement;
    }
}
