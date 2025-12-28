namespace LanguageCore;

public interface IVersionProvider
{
    bool TryGetVersion(Uri uri, out ulong version);
}
