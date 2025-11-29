namespace LanguageCore.Parser.Statements;

[Flags]
public enum StatementWalkFlags
{
    IncludeThis = 1,
    FrameOnly = 2,
}
