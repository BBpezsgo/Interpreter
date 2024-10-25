namespace LanguageCore.Compiler;

public interface IRuntimeInfoProvider
{
    public int PointerSize { get; }
}

public struct RuntimeInfoProvider : IRuntimeInfoProvider
{
    public int PointerSize { get; set; }
}
