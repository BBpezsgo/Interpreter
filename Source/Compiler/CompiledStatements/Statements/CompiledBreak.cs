namespace LanguageCore.Compiler;

public class CompiledBreak : CompiledStatement
{
    public override string Stringify(int depth = 0) => $"break";
    public override string ToString() => $"break";
}
