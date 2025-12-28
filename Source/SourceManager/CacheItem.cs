using LanguageCore.Parser;
using LanguageCore.Tokenizing;

namespace LanguageCore;

public class CacheItem
{
    public readonly ulong Version;
    public readonly string Content;
    public readonly TokenizerResult TokenizerResult;
    public readonly ParserResult ParserResult;

    public CacheItem(ulong version, string content, TokenizerResult tokenizerResult, ParserResult parserResult)
    {
        Version = version;
        Content = content;
        TokenizerResult = tokenizerResult;
        ParserResult = parserResult;
    }
}
