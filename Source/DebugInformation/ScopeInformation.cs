namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public struct ScopeInformation
{
    public SourceCodeLocation Location;
    public List<StackElementInformation> Stack;
}
