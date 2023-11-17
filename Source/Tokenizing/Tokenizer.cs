using System;
using System.Linq;

// TODO: new lines aren't working

namespace LanguageCore.Tokenizing
{
    enum TokenizerState
    {
        Normal,
    }

    public partial class Tokenizer
    {
        TokenizerState State = TokenizerState.Normal;

        void ProcessCharacter(int offsetTotal, out bool breakLine)
        {
            char currChar = Text[offsetTotal];

            breakLine = false;

            if (currChar == '\n')
            {
                breakLine = true;
            }

            if (currChar == '\n' && CurrentToken.TokenType == TokenType.CommentMultiline)
            {
                EndToken(offsetTotal);
                CurrentToken.Content.Clear();
                CurrentToken.TokenType = TokenType.CommentMultiline;
            }

            /*
            if (currChar > byte.MaxValue)
            {
                RefreshTokenPosition(offsetTotal);
                if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
                { Warnings.Add(new Warning($"Don't use special characters (please). Use \\u{((int)currChar).ToString("X").PadLeft(4, '0')}", CurrentToken.Position.After(), File)); }
                else
                { Warnings.Add(new Warning($"Don't use special characters.", CurrentToken.Position.After(), File)); }
            }
            */

            if (CurrentToken.TokenType == TokenType.STRING_UnicodeCharacter)
            {
                if (SavedUnicode == null) throw new InternalException($"{nameof(SavedUnicode)} is null");
                if (SavedUnicode.Length == 4)
                {
                    string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(SavedUnicode, 16));
                    UnicodeCharacters.Add(new SimpleToken(
                            unicodeChar,
                            new Position(
                                new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), new SinglePosition(CurrentLine, CurrentColumn)),
                                new Range<int>(offsetTotal - 6, offsetTotal)
                            )
                        ));
                    CurrentToken.Content.Append(unicodeChar);
                    CurrentToken.TokenType = TokenType.LiteralString;
                    SavedUnicode = null;
                }
                else if (!DigitsHex.Contains(char.ToLowerInvariant(currChar)))
                {
                    throw new TokenizerException($"This isn't a hex digit \"{currChar}\"", GetCurrentPosition(offsetTotal));
                }
                else
                {
                    SavedUnicode += currChar;
                    return;
                }
            }
            else if (CurrentToken.TokenType == TokenType.CHAR_UnicodeCharacter)
            {
                if (SavedUnicode == null) throw new InternalException($"{nameof(SavedUnicode)} is null");
                if (SavedUnicode.Length == 4)
                {
                    string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(SavedUnicode, 16));
                    UnicodeCharacters.Add(new SimpleToken(
                        unicodeChar,
                        new Position(
                            new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), new SinglePosition(CurrentLine, CurrentColumn)),
                            new Range<int>(offsetTotal - 6, offsetTotal)
                        )
                    ));
                    CurrentToken.Content.Append(unicodeChar);
                    CurrentToken.TokenType = TokenType.LiteralCharacter;
                    SavedUnicode = null;
                }
                else if (!DigitsHex.Contains(char.ToLowerInvariant(currChar)))
                {
                    throw new TokenizerException($"This isn't a hex digit: \"{currChar}\"", GetCurrentPosition(offsetTotal));
                }
                else
                {
                    SavedUnicode += currChar;
                    return;
                }
            }

            if (CurrentToken.TokenType == TokenType.STRING_EscapeSequence)
            {
                if (currChar == 'u')
                {
                    CurrentToken.TokenType = TokenType.STRING_UnicodeCharacter;
                    SavedUnicode = string.Empty;
                }
                else
                {
                    CurrentToken.Content.Append(currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '"' => "\"",
                        '0' => "\0",
                        _ => throw new TokenizerException($"I don't know this escape sequence: \\{currChar}", GetCurrentPosition(offsetTotal)),
                    });
                    CurrentToken.TokenType = TokenType.LiteralString;
                }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.CHAR_EscapeSequence)
            {
                if (currChar == 'u')
                {
                    CurrentToken.TokenType = TokenType.CHAR_UnicodeCharacter;
                    SavedUnicode = string.Empty;
                }
                else
                {
                    CurrentToken.Content.Append(currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '\'' => "\'",
                        '0' => "\0",
                        _ => throw new TokenizerException($"I don't know this escape sequence: \\{currChar}", GetCurrentPosition(offsetTotal)),
                    });
                    CurrentToken.TokenType = TokenType.LiteralCharacter;
                }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
            {
                CurrentToken.TokenType = TokenType.Operator;
                if (currChar == '=')
                { CurrentToken.Content.Append(currChar); }
                EndToken(offsetTotal);
                return;
            }
            else if (CurrentToken.TokenType == TokenType.Comment && currChar != '\n')
            {
                CurrentToken.Content.Append(currChar);
                return;
            }
            else if (CurrentToken.TokenType == TokenType.LiteralString && currChar != '"')
            {
                if (currChar == '\\')
                { CurrentToken.TokenType = TokenType.STRING_EscapeSequence; }
                else
                { CurrentToken.Content.Append(currChar); }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.LiteralCharacter && currChar != '\'')
            {
                if (currChar == '\\')
                { CurrentToken.TokenType = TokenType.CHAR_EscapeSequence; }
                else
                { CurrentToken.Content.Append(currChar); }
                return;
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.CommentMultiline;
                EndToken(offsetTotal);
                return;
            }

            if (CurrentToken.TokenType == TokenType.CommentMultiline || CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT)
            {
                CurrentToken.Content.Append(currChar);
                if (CurrentToken.TokenType == TokenType.CommentMultiline && currChar == '*')
                {
                    CurrentToken.TokenType = TokenType.POTENTIAL_END_MULTILINE_COMMENT;
                }
                else
                {
                    CurrentToken.TokenType = TokenType.CommentMultiline;
                }
                return;
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT && !char.IsAsciiDigit(currChar))
            {
                CurrentToken.TokenType = TokenType.Operator;
                EndToken(offsetTotal);
            }

            if (currChar == 'f' && (CurrentToken.TokenType == TokenType.LiteralNumber || CurrentToken.TokenType == TokenType.LiteralFloat))
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LiteralFloat;
                EndToken(offsetTotal);
            }
            else if (currChar == 'e' && (CurrentToken.TokenType == TokenType.LiteralNumber || CurrentToken.TokenType == TokenType.LiteralFloat))
            {
                if (CurrentToken.ToString().Contains(currChar))
                { throw new TokenizerException($"Am I stupid or this is not a float number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LiteralFloat;
            }
            else if (currChar == 'x' && CurrentToken.TokenType == TokenType.LiteralNumber)
            {
                if (!CurrentToken.ToString().EndsWith('0'))
                { throw new TokenizerException($"Am I stupid or this is not a hex number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LiteralHex;
            }
            else if (currChar == 'b' && CurrentToken.TokenType == TokenType.LiteralNumber)
            {
                if (!CurrentToken.Content.ToString().EndsWith('0'))
                { throw new TokenizerException($"Am I stupid or this is not a binary number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LiteralBinary;
            }
            else if (char.IsAsciiDigit(currChar))
            {
                if (CurrentToken.TokenType == TokenType.Whitespace)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LiteralNumber;
                }
                else if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT)
                { CurrentToken.TokenType = TokenType.LiteralFloat; }
                else if (CurrentToken.TokenType == TokenType.Operator)
                {
                    if (CurrentToken.ToString() != "-")
                    { EndToken(offsetTotal); }
                    CurrentToken.TokenType = TokenType.LiteralNumber;
                }

                if (CurrentToken.TokenType == TokenType.LiteralBinary)
                {
                    if (currChar != '0' && currChar != '1')
                    {
                        RefreshTokenPosition(offsetTotal);
                        throw new TokenizerException($"This isn't a binary digit am i right? \'{currChar}\'", CurrentToken.Position.After());
                    }
                }

                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == TokenType.LiteralBinary && currChar == '_')
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == TokenType.LiteralNumber && currChar == '_')
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == TokenType.LiteralFloat && currChar == '_')
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (currChar == '.')
            {
                if (CurrentToken.TokenType == TokenType.Whitespace)
                {
                    CurrentToken.TokenType = TokenType.POTENTIAL_FLOAT;
                    CurrentToken.Content.Append(currChar);
                }
                else if (CurrentToken.TokenType == TokenType.LiteralNumber)
                {
                    CurrentToken.TokenType = TokenType.LiteralFloat;
                    CurrentToken.Content.Append(currChar);
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.Operator;
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '/')
            {
                if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                {
                    CurrentToken.TokenType = TokenType.Comment;
                    CurrentToken.Content.Clear();
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.POTENTIAL_COMMENT;
                    CurrentToken.Content.Append(currChar);
                }
            }
            else if (Bracelets.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.Operator;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (SimpleOperators.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.Operator;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (currChar == '=')
            {
                if (CurrentToken.Content.Length == 1 && Operators.Contains(CurrentToken.Content[0]))
                {
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal);
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.Operator;
                    CurrentToken.Content.Append(currChar);
                }
            }
            else if (DoubleOperators.Contains(CurrentToken.ToString() + currChar))
            {
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (currChar == '*' && CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
            {
                CurrentToken.TokenType = TokenType.CommentMultiline;
                CurrentToken.Content.Append(currChar);
            }
            else if (Operators.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.Operator;
                CurrentToken.Content.Append(currChar);
            }
            else if (Whitespaces.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.Whitespace;
                CurrentToken.Content.Append(currChar);
            }
            else if (currChar == '\n')
            {
                if (CurrentToken.TokenType == TokenType.CommentMultiline)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.CommentMultiline;
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = Settings.DistinguishBetweenSpacesAndNewlines ? TokenType.LineBreak : TokenType.Whitespace;
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '"')
            {
                if (CurrentToken.TokenType != TokenType.LiteralString)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LiteralString;
                }
                else if (CurrentToken.TokenType == TokenType.LiteralString)
                {
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '\'')
            {
                if (CurrentToken.TokenType != TokenType.LiteralCharacter)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LiteralCharacter;
                }
                else if (CurrentToken.TokenType == TokenType.LiteralCharacter)
                {
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '\\')
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.Operator;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (CurrentToken.TokenType == TokenType.LiteralHex)
            {
                if (currChar is not ((>= 'a' and <= 'f') or (>= 'A' and <= 'F') or '_'))
                {
                    RefreshTokenPosition(offsetTotal);
                    throw new TokenizerException($"This isn't a hex digit am i right? \'{currChar}\'", CurrentToken.Position.After());
                }
                CurrentToken.Content.Append(currChar);
            }
            else
            {
                if (CurrentToken.TokenType == TokenType.Whitespace ||
                    CurrentToken.TokenType == TokenType.LiteralNumber ||
                    CurrentToken.TokenType == TokenType.LiteralHex ||
                    CurrentToken.TokenType == TokenType.LiteralFloat ||
                    CurrentToken.TokenType == TokenType.Operator)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.Identifier;
                    CurrentToken.Content.Append(currChar);
                }
                else
                {
                    CurrentToken.Content.Append(currChar);
                }
            }
        }

        void EndToken(int offsetTotal)
        {
            CurrentToken.position.Range.End.Character = CurrentColumn;
            CurrentToken.position.Range.End.Line = CurrentLine;
            CurrentToken.position.AbsoluteRange.End = offsetTotal;

            // Skip comments if they should be ignored
            if (!Settings.TokenizeComments &&
                (CurrentToken.TokenType == TokenType.Comment ||
                CurrentToken.TokenType == TokenType.CommentMultiline))
            { goto Finish; }

            // Skip whitespaces if they should be ignored
            if (!Settings.TokenizeWhitespaces &&
                CurrentToken.TokenType == TokenType.Whitespace)
            { goto Finish; }

            // Skip empty whitespaces
            if (Settings.TokenizeWhitespaces &&
                CurrentToken.TokenType == TokenType.Whitespace &&
                string.IsNullOrEmpty(CurrentToken.ToString()))
            { goto Finish; }

            if (CurrentToken.TokenType == TokenType.LiteralFloat)
            {
                if (CurrentToken.ToString().EndsWith('.'))
                {
                    CurrentToken.position.Range.End.Character--;
                    CurrentToken.position.Range.End.Line--;
                    CurrentToken.position.AbsoluteRange.End--;
                    CurrentToken.Content.Remove(CurrentToken.Content.Length - 1, 1);
                    CurrentToken.TokenType = TokenType.LiteralNumber;
                    Tokens.Add(CurrentToken.Instantiate());

                    CurrentToken.position.Range.Start.Character = CurrentToken.position.Range.End.Character + 1;
                    CurrentToken.position.Range.Start.Line = CurrentToken.position.Range.End.Line + 1;
                    CurrentToken.position.AbsoluteRange.Start = CurrentToken.position.AbsoluteRange.End + 1;

                    CurrentToken.position.Range.End.Character++;
                    CurrentToken.position.Range.End.Line++;
                    CurrentToken.position.AbsoluteRange.End++;
                    CurrentToken.TokenType = TokenType.Operator;
                    CurrentToken.Content.Clear();
                    CurrentToken.Content.Append('.');
                }
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT)
            {
                if (CurrentToken.ToString().CompareTo(".") == 0)
                {
                    CurrentToken.TokenType = TokenType.Operator;
                }
                else
                {
                    CurrentToken.TokenType = TokenType.LiteralFloat;
                }
            }

            Tokens.Add(CurrentToken.Instantiate());

        Finish:
            CurrentToken.TokenType = TokenType.Whitespace;
            CurrentToken.Content.Clear();
            CurrentToken.position.Range.Start.Line = CurrentLine;
            CurrentToken.position.Range.Start.Character = CurrentColumn;
            CurrentToken.position.AbsoluteRange.Start = offsetTotal;
        }
    }
}
