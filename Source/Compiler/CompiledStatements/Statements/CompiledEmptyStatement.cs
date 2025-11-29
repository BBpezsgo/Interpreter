namespace LanguageCore.Compiler;

public class CompiledEmptyStatement : CompiledStatement
{
    public override string Stringify(int depth = 0) => "";
    public override string ToString() => $";";
}
