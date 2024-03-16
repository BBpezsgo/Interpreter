﻿namespace LanguageCore;

public class WillBeCompilerException
{
    public string Message { get; }

    public WillBeCompilerException(string message)
    {
        Message = message;
    }

    public CompilerException Instantiate(Position position, Uri? uri) => new(Message, position, uri);
    public CompilerException Instantiate(IPositioned? position, Uri? uri) => new(Message, position, uri);

    public override string ToString() => Message;
}
