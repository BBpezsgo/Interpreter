namespace LanguageCore.Tests;

public sealed class InputBuffer(string buffer)
{
    int i;

    public char Read()
    {
        if (i >= buffer.Length) throw new Exception($"No more characters in the buffer");
        return buffer[i++];
    }
}
