using System.Net.Http;

namespace LanguageCore.Tokenizing;

public static class AnyTokenizer
{
    public static TokenizerResult Tokenize(Uri uri, IEnumerable<string> preprocessorVariables, TokenizerSettings? settings = null)
    {
        if (uri.IsFile)
        { return StreamTokenizer.Tokenize(uri.LocalPath, preprocessorVariables, settings); }
        else
        {
            using HttpClient client = new();
            using HttpResponseMessage res = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).Result;
            res.EnsureSuccessStatusCode();

            return StreamTokenizer.Tokenize(res.Content.ReadAsStream(), preprocessorVariables, uri, settings);
        }
    }
}
