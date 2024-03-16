using System.Net.Http;

namespace LanguageCore.Tokenizing;

public static class AnyTokenizer
{
    public static TokenizerResult Tokenize(Uri uri)
        => AnyTokenizer.Tokenize(uri, TokenizerSettings.Default);

    public static TokenizerResult Tokenize(Uri uri, TokenizerSettings settings)
    {
        if (uri.IsFile)
        { return StreamTokenizer.Tokenize(uri.LocalPath, settings); }
        else
        {
            using HttpClient client = new();
            using HttpResponseMessage res = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result;
            res.EnsureSuccessStatusCode();

            return StreamTokenizer.Tokenize(res.Content.ReadAsStream(), uri, settings);
        }
    }
}
