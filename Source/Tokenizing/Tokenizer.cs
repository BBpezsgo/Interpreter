using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

// TODO: new lines aren't working

namespace LanguageCore.Tokenizing
{
    public partial class Tokenizer
    {
        void ProcessCharacter((char? Prev, char Curr, char? Next) ctx, int offsetTotal, out bool breakLine)
        {
            breakLine = false;

            char currChar = ctx.Curr;

            if (currChar == '\n')
            {
                breakLine = true;
            }

            if (currChar == '\n' && CurrentToken.TokenType == TokenType.COMMENT_MULTILINE)
            {
                EndToken(offsetTotal);
                CurrentToken.Content.Clear();
                CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
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

            if (CurrentToken.TokenType == TokenType.STRING_UNICODE_CHARACTER)
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
                    CurrentToken.TokenType = TokenType.LITERAL_STRING;
                    SavedUnicode = null;
                }
                else if (!(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                {
                    throw new TokenizerException($"This isn't a hex digit \"{currChar}\"", GetCurrentPosition(offsetTotal));
                }
                else
                {
                    SavedUnicode += currChar;
                    return;
                }
            }
            else if (CurrentToken.TokenType == TokenType.CHAR_UNICODE_CHARACTER)
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
                    CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                    SavedUnicode = null;
                }
                else if (!(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                {
                    throw new TokenizerException($"This isn't a hex digit: \"{currChar}\"", GetCurrentPosition(offsetTotal));
                }
                else
                {
                    SavedUnicode += currChar;
                    return;
                }
            }
            
            if (CurrentToken.TokenType == TokenType.STRING_ESCAPE_SEQUENCE)
            {
                if (currChar == 'u')
                {
                    CurrentToken.TokenType = TokenType.STRING_UNICODE_CHARACTER;
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
                    CurrentToken.TokenType = TokenType.LITERAL_STRING;
                }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.CHAR_ESCAPE_SEQUENCE)
            {
                if (currChar == 'u')
                {
                    CurrentToken.TokenType = TokenType.CHAR_UNICODE_CHARACTER;
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
                    CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
            {
                CurrentToken.TokenType = TokenType.OPERATOR;
                if (currChar == '=')
                { CurrentToken.Content.Append(currChar); }
                EndToken(offsetTotal);
                return;
            }
            else if (CurrentToken.TokenType == TokenType.COMMENT && currChar != '\n')
            {
                CurrentToken.Content.Append(currChar);
                return;
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_STRING && currChar != '"')
            {
                if (currChar == '\\')
                { CurrentToken.TokenType = TokenType.STRING_ESCAPE_SEQUENCE; }
                else
                { CurrentToken.Content.Append(currChar); }
                return;
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_CHAR && currChar != '\'')
            {
                if (currChar == '\\')
                { CurrentToken.TokenType = TokenType.CHAR_ESCAPE_SEQUENCE; }
                else
                { CurrentToken.Content.Append(currChar); }
                return;
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                EndToken(offsetTotal);
                return;
            }

            if (CurrentToken.TokenType == TokenType.COMMENT_MULTILINE || CurrentToken.TokenType == TokenType.POTENTIAL_END_MULTILINE_COMMENT)
            {
                CurrentToken.Content.Append(currChar);
                if (CurrentToken.TokenType == TokenType.COMMENT_MULTILINE && currChar == '*')
                {
                    CurrentToken.TokenType = TokenType.POTENTIAL_END_MULTILINE_COMMENT;
                }
                else
                {
                    CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                }
                return;
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT && !int.TryParse(currChar.ToString(), out _))
            {
                CurrentToken.TokenType = TokenType.OPERATOR;
                EndToken(offsetTotal);
            }

            if (currChar == 'f' && (CurrentToken.TokenType == TokenType.LITERAL_NUMBER || CurrentToken.TokenType == TokenType.LITERAL_FLOAT))
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                EndToken(offsetTotal);
            }
            else if (currChar == 'e' && (CurrentToken.TokenType == TokenType.LITERAL_NUMBER || CurrentToken.TokenType == TokenType.LITERAL_FLOAT))
            {
                if (CurrentToken.ToString().Contains(currChar))
                { throw new TokenizerException($"Am I stupid or is this not a float number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
            }
            else if (currChar == 'x' && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                if (!CurrentToken.ToString().EndsWith('0'))
                { throw new TokenizerException($"Am I stupid or is this not a hex number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LITERAL_HEX;
            }
            else if (currChar == 'b' && CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
            {
                if (!CurrentToken.ToString().EndsWith('0'))
                { throw new TokenizerException($"Am I stupid or is this not a binary number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = TokenType.LITERAL_BIN;
            }
            else if ((new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', }).Contains(currChar))
            {
                if (CurrentToken.TokenType == TokenType.WHITESPACE)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                }
                else if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT)
                { CurrentToken.TokenType = TokenType.LITERAL_FLOAT; }
                else if (CurrentToken.TokenType == TokenType.OPERATOR)
                {
                    if (CurrentToken.ToString() != "-")
                    { EndToken(offsetTotal); }
                    CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                }

                if (CurrentToken.TokenType == TokenType.LITERAL_BIN)
                {
                    if (currChar != '0' && currChar != '1')
                    {
                        RefreshTokenPosition(offsetTotal);
                        throw new TokenizerException($"This isn't a binary digit am i right? \'{currChar}\'", CurrentToken.Position.After());
                    }
                }

                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_BIN && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_NUMBER && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_FLOAT && new char[] { '_' }.Contains(currChar.ToString().ToLower()[0]))
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (currChar == '.')
            {
                if (CurrentToken.TokenType == TokenType.WHITESPACE)
                {
                    CurrentToken.TokenType = TokenType.POTENTIAL_FLOAT;
                    CurrentToken.Content.Append(currChar);
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_NUMBER)
                {
                    CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                    CurrentToken.Content.Append(currChar);
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '/')
            {
                if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                {
                    CurrentToken.TokenType = TokenType.COMMENT;
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
                CurrentToken.TokenType = TokenType.OPERATOR;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (SimpleOperators.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.OPERATOR;
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
                    CurrentToken.TokenType = TokenType.OPERATOR;
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
                if (CurrentToken.TokenType == TokenType.POTENTIAL_COMMENT)
                {
                    CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                    CurrentToken.Content.Append(currChar);
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Append(currChar);
                }
            }
            else if (Operators.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.OPERATOR;
                CurrentToken.Content.Append(currChar);
            }
            else if (Whitespaces.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.WHITESPACE;
                CurrentToken.Content.Append(currChar);
            }
            else if (currChar == '\n')
            {
                if (CurrentToken.TokenType == TokenType.COMMENT_MULTILINE)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.COMMENT_MULTILINE;
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = Settings.DistinguishBetweenSpacesAndNewlines ? TokenType.LINEBREAK : TokenType.WHITESPACE;
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '"')
            {
                if (CurrentToken.TokenType != TokenType.LITERAL_STRING)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LITERAL_STRING;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_STRING)
                {
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '\'')
            {
                if (CurrentToken.TokenType != TokenType.LITERAL_CHAR)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.LITERAL_CHAR;
                }
                else if (CurrentToken.TokenType == TokenType.LITERAL_CHAR)
                {
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '\\')
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = TokenType.OPERATOR;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (CurrentToken.TokenType == TokenType.LITERAL_HEX)
            {
                if (!(new char[] { '_', 'a', 'b', 'c', 'd', 'e', 'f' }).Contains(currChar.ToString().ToLower()[0]))
                {
                    RefreshTokenPosition(offsetTotal);
                    throw new TokenizerException($"This isn't a hex digit am i right? \'{currChar}\'", CurrentToken.Position.After());
                }
                CurrentToken.Content.Append(currChar);
            }
            else
            {
                if (CurrentToken.TokenType == TokenType.WHITESPACE ||
                    CurrentToken.TokenType == TokenType.LITERAL_NUMBER ||
                    CurrentToken.TokenType == TokenType.LITERAL_HEX ||
                    CurrentToken.TokenType == TokenType.LITERAL_FLOAT ||
                    CurrentToken.TokenType == TokenType.OPERATOR)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = TokenType.IDENTIFIER;
                    CurrentToken.Content.Append(currChar);
                }
                else
                {
                    CurrentToken.Content.Append(currChar);
                }
            }
        }

        void EndToken(int OffsetTotal)
        {
            CurrentToken.position.Range.End.Character = CurrentColumn;
            CurrentToken.position.Range.End.Line = CurrentLine;
            CurrentToken.position.AbsoluteRange.End = OffsetTotal;

            // Skip comments if they should be ignored
            if (!Settings.TokenizeComments &&
                (CurrentToken.TokenType == TokenType.COMMENT ||
                CurrentToken.TokenType == TokenType.COMMENT_MULTILINE))
            { goto Finish; }

            // Skip whitespaces if they should be ignored
            if (!Settings.TokenizeWhitespaces &&
                CurrentToken.TokenType == TokenType.WHITESPACE)
            { goto Finish; }

            // Skip empty whitespaces
            if (Settings.TokenizeWhitespaces &&
                CurrentToken.TokenType == TokenType.WHITESPACE &&
                string.IsNullOrEmpty(CurrentToken.ToString()))
            { goto Finish; }

            if (CurrentToken.TokenType == TokenType.LITERAL_FLOAT)
            {
                if (CurrentToken.ToString().EndsWith('.'))
                {
                    CurrentToken.position.Range.End.Character--;
                    CurrentToken.position.Range.End.Line--;
                    CurrentToken.position.AbsoluteRange.End--;
                    CurrentToken.Content.Remove(CurrentToken.Content.Length - 1, 1);
                    CurrentToken.TokenType = TokenType.LITERAL_NUMBER;
                    Tokens.Add(CurrentToken.Instantiate());

                    CurrentToken.position.Range.Start.Character = CurrentToken.position.Range.End.Character + 1;
                    CurrentToken.position.Range.Start.Line = CurrentToken.position.Range.End.Line + 1;
                    CurrentToken.position.AbsoluteRange.Start = CurrentToken.position.AbsoluteRange.End + 1;

                    CurrentToken.position.Range.End.Character++;
                    CurrentToken.position.Range.End.Line++;
                    CurrentToken.position.AbsoluteRange.End++;
                    CurrentToken.TokenType = TokenType.OPERATOR;
                    CurrentToken.Content.Clear();
                    CurrentToken.Content.Append('.');
                }
            }

            if (CurrentToken.TokenType == TokenType.POTENTIAL_FLOAT)
            {
                if (CurrentToken.ToString().CompareTo(".") == 0)
                {
                    CurrentToken.TokenType = TokenType.OPERATOR;
                }
                else
                {
                    CurrentToken.TokenType = TokenType.LITERAL_FLOAT;
                }
            }

            Tokens.Add(CurrentToken.Instantiate());

        Finish:
            CurrentToken.TokenType = TokenType.WHITESPACE;
            CurrentToken.Content.Clear();
            CurrentToken.position.Range.Start.Line = CurrentLine;
            CurrentToken.position.Range.Start.Character = CurrentColumn;
            CurrentToken.position.AbsoluteRange.Start = OffsetTotal;
        }

        static List<Token> NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
        {
            List<Token> result = new();

            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];

                if (result.Count == 0)
                {
                    result.Add(token);
                    continue;
                }

                Token lastToken = result[^1];

                if (token.TokenType == TokenType.WHITESPACE && lastToken.TokenType == TokenType.WHITESPACE)
                {
                    result[^1] = new Token(
                        lastToken.TokenType,
                        lastToken.Content + token.Content,
                        lastToken.IsAnonymous,
                        lastToken.Position);
                    continue;
                }

                if (token.TokenType == TokenType.LINEBREAK && lastToken.TokenType == TokenType.LINEBREAK && settings.JoinLinebreaks)
                {
                    result[^1] = new Token(
                        lastToken.TokenType,
                        lastToken.Content + token.Content,
                        lastToken.IsAnonymous,
                        lastToken.Position);
                    continue;
                }


                result.Add(token);
            }

            return result;
        }
    }
}
