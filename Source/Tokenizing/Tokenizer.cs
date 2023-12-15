﻿using System;
using System.Linq;

// TODO: new lines aren't working

namespace LanguageCore.Tokenizing
{
    public abstract partial class Tokenizer
    {
        protected void ProcessCharacter(char currChar, int offsetTotal, out bool breakLine)
        {
            breakLine = false;

            if (currChar == '\n')
            {
                breakLine = true;
            }

            if (currChar == '\n' && CurrentToken.TokenType == PreparationTokenType.CommentMultiline)
            {
                EndToken(offsetTotal);
                CurrentToken.Content.Clear();
                CurrentToken.TokenType = PreparationTokenType.CommentMultiline;
            }

            if (CurrentToken.TokenType == PreparationTokenType.STRING_UnicodeCharacter)
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
                    CurrentToken.TokenType = PreparationTokenType.LiteralString;
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
            else if (CurrentToken.TokenType == PreparationTokenType.CHAR_UnicodeCharacter)
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
                    CurrentToken.TokenType = PreparationTokenType.LiteralCharacter;
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

            if (CurrentToken.TokenType == PreparationTokenType.STRING_EscapeSequence)
            {
                if (currChar == 'u')
                {
                    CurrentToken.TokenType = PreparationTokenType.STRING_UnicodeCharacter;
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
                    CurrentToken.TokenType = PreparationTokenType.LiteralString;
                }
                return;
            }
            else if (CurrentToken.TokenType == PreparationTokenType.CHAR_EscapeSequence)
            {
                if (currChar == 'u')
                {
                    CurrentToken.TokenType = PreparationTokenType.CHAR_UnicodeCharacter;
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
                    CurrentToken.TokenType = PreparationTokenType.LiteralCharacter;
                }
                return;
            }
            else if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
            {
                CurrentToken.TokenType = PreparationTokenType.Operator;
                if (currChar == '=')
                { CurrentToken.Content.Append(currChar); }
                EndToken(offsetTotal);
                return;
            }
            else if (CurrentToken.TokenType == PreparationTokenType.Comment && currChar != '\n')
            {
                CurrentToken.Content.Append(currChar);
                return;
            }
            else if (CurrentToken.TokenType == PreparationTokenType.LiteralString && currChar != '"')
            {
                if (currChar == '\\')
                { CurrentToken.TokenType = PreparationTokenType.STRING_EscapeSequence; }
                else
                { CurrentToken.Content.Append(currChar); }
                return;
            }
            else if (CurrentToken.TokenType == PreparationTokenType.LiteralCharacter && currChar != '\'')
            {
                if (currChar == '\\')
                { CurrentToken.TokenType = PreparationTokenType.CHAR_EscapeSequence; }
                else
                { CurrentToken.Content.Append(currChar); }
                return;
            }

            if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = PreparationTokenType.CommentMultiline;
                EndToken(offsetTotal);
                return;
            }

            if (CurrentToken.TokenType is PreparationTokenType.CommentMultiline or PreparationTokenType.POTENTIAL_END_MULTILINE_COMMENT)
            {
                CurrentToken.Content.Append(currChar);
                if (CurrentToken.TokenType == PreparationTokenType.CommentMultiline && currChar == '*')
                {
                    CurrentToken.TokenType = PreparationTokenType.POTENTIAL_END_MULTILINE_COMMENT;
                }
                else
                {
                    CurrentToken.TokenType = PreparationTokenType.CommentMultiline;
                }
                return;
            }

            if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_FLOAT && !char.IsAsciiDigit(currChar))
            {
                CurrentToken.TokenType = PreparationTokenType.Operator;
                EndToken(offsetTotal);
            }

            if (currChar == 'f' && (CurrentToken.TokenType is PreparationTokenType.LiteralNumber or PreparationTokenType.LiteralFloat))
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = PreparationTokenType.LiteralFloat;
                EndToken(offsetTotal);
            }
            else if (currChar == 'e' && (CurrentToken.TokenType is PreparationTokenType.LiteralNumber or PreparationTokenType.LiteralFloat))
            {
                if (CurrentToken.ToString().Contains(currChar))
                { throw new TokenizerException($"Am I stupid or this is not a float number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = PreparationTokenType.LiteralFloat;
            }
            else if (currChar == 'x' && CurrentToken.TokenType == PreparationTokenType.LiteralNumber)
            {
                if (!CurrentToken.ToString().EndsWith('0'))
                { throw new TokenizerException($"Am I stupid or this is not a hex number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = PreparationTokenType.LiteralHex;
            }
            else if (currChar == 'b' && CurrentToken.TokenType == PreparationTokenType.LiteralNumber)
            {
                if (!CurrentToken.Content.ToString().EndsWith('0'))
                { throw new TokenizerException($"Am I stupid or this is not a binary number?", CurrentToken.Position); }
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = PreparationTokenType.LiteralBinary;
            }
            else if (char.IsAsciiDigit(currChar))
            {
                if (CurrentToken.TokenType == PreparationTokenType.Whitespace)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = PreparationTokenType.LiteralNumber;
                }
                else if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_FLOAT)
                {
                    CurrentToken.TokenType = PreparationTokenType.LiteralFloat;
                }
                else if (CurrentToken.TokenType == PreparationTokenType.Operator)
                {
                    if (CurrentToken.ToString() != "-")
                    { EndToken(offsetTotal); }
                    CurrentToken.TokenType = PreparationTokenType.LiteralNumber;
                }

                if (CurrentToken.TokenType == PreparationTokenType.LiteralBinary)
                {
                    if (currChar != '0' && currChar != '1')
                    {
                        RefreshTokenPosition(offsetTotal);
                        throw new TokenizerException($"This isn't a binary digit am i right? \'{currChar}\'", CurrentToken.Position.After());
                    }
                }

                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == PreparationTokenType.LiteralBinary && currChar == '_')
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == PreparationTokenType.LiteralNumber && currChar == '_')
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (CurrentToken.TokenType == PreparationTokenType.LiteralFloat && currChar == '_')
            {
                CurrentToken.Content.Append(currChar);
            }
            else if (currChar == '.')
            {
                if (CurrentToken.TokenType == PreparationTokenType.Whitespace)
                {
                    CurrentToken.TokenType = PreparationTokenType.POTENTIAL_FLOAT;
                    CurrentToken.Content.Append(currChar);
                }
                else if (CurrentToken.TokenType == PreparationTokenType.LiteralNumber)
                {
                    CurrentToken.TokenType = PreparationTokenType.LiteralFloat;
                    CurrentToken.Content.Append(currChar);
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = PreparationTokenType.Operator;
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal, true);
                }
            }
            else if (currChar == '/')
            {
                if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_COMMENT)
                {
                    CurrentToken.TokenType = PreparationTokenType.Comment;
                    CurrentToken.Content.Clear();
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = PreparationTokenType.POTENTIAL_COMMENT;
                    CurrentToken.Content.Append(currChar);
                }
            }
            else if (Bracelets.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = PreparationTokenType.Operator;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal, true);
            }
            else if (SimpleOperators.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = PreparationTokenType.Operator;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal, true);
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
                    CurrentToken.TokenType = PreparationTokenType.Operator;
                    CurrentToken.Content.Append(currChar);
                }
            }
            else if (DoubleOperators.Contains(CurrentToken.ToString() + currChar))
            {
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal);
            }
            else if (currChar == '*' && CurrentToken.TokenType == PreparationTokenType.POTENTIAL_COMMENT)
            {
                CurrentToken.TokenType = PreparationTokenType.CommentMultiline;
                CurrentToken.Content.Append(currChar);
            }
            else if (Operators.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = PreparationTokenType.Operator;
                CurrentToken.Content.Append(currChar);
            }
            else if (Whitespaces.Contains(currChar))
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = PreparationTokenType.Whitespace;
                CurrentToken.Content.Append(currChar);
            }
            else if (currChar == '\n')
            {
                if (CurrentToken.TokenType == PreparationTokenType.CommentMultiline)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = PreparationTokenType.CommentMultiline;
                }
                else
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = Settings.DistinguishBetweenSpacesAndNewlines ? PreparationTokenType.LineBreak : PreparationTokenType.Whitespace;
                    CurrentToken.Content.Append(currChar);
                    EndToken(offsetTotal, true);
                }
            }
            else if (currChar == '"')
            {
                if (CurrentToken.TokenType != PreparationTokenType.LiteralString)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = PreparationTokenType.LiteralString;
                }
                else if (CurrentToken.TokenType == PreparationTokenType.LiteralString)
                {
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '\'')
            {
                if (CurrentToken.TokenType != PreparationTokenType.LiteralCharacter)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = PreparationTokenType.LiteralCharacter;
                }
                else if (CurrentToken.TokenType == PreparationTokenType.LiteralCharacter)
                {
                    EndToken(offsetTotal);
                }
            }
            else if (currChar == '\\')
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = PreparationTokenType.Operator;
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal, true);
            }
            else if (CurrentToken.TokenType == PreparationTokenType.LiteralHex)
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
                if (CurrentToken.TokenType is
                    PreparationTokenType.Whitespace or
                    PreparationTokenType.LiteralNumber or
                    PreparationTokenType.LiteralHex or
                    PreparationTokenType.LiteralFloat or
                    PreparationTokenType.Operator)
                {
                    EndToken(offsetTotal);
                    CurrentToken.TokenType = PreparationTokenType.Identifier;
                    CurrentToken.Content.Append(currChar);
                }
                else
                {
                    CurrentToken.Content.Append(currChar);
                }
            }
        }

        protected void EndToken(int offsetTotal, bool inFuture = false)
        {
            CurrentToken.Position.Range.End = new SinglePosition(CurrentLine, CurrentColumn);
            CurrentToken.Position.AbsoluteRange.End = offsetTotal;

            if (inFuture)
            {
                CurrentToken.Position.Range.End.Character++;
                CurrentToken.Position.AbsoluteRange.End++;
            }

            // Skip comments if they should be ignored
            if (!Settings.TokenizeComments &&
                (CurrentToken.TokenType == PreparationTokenType.Comment ||
                CurrentToken.TokenType == PreparationTokenType.CommentMultiline))
            { goto Finish; }

            // Skip whitespaces if they should be ignored
            if (!Settings.TokenizeWhitespaces &&
                CurrentToken.TokenType == PreparationTokenType.Whitespace)
            { goto Finish; }

            // Skip empty whitespaces
            if (Settings.TokenizeWhitespaces &&
                CurrentToken.TokenType == PreparationTokenType.Whitespace &&
                CurrentToken.Content.Length == 0)
            { goto Finish; }

            if (CurrentToken.TokenType == PreparationTokenType.LiteralFloat)
            {
                if (CurrentToken.ToString().EndsWith('.'))
                {
                    CurrentToken.Position.Range.End.Character--;
                    CurrentToken.Position.Range.End.Line--;
                    CurrentToken.Position.AbsoluteRange.End--;
                    CurrentToken.Content.Remove(CurrentToken.Content.Length - 1, 1);
                    CurrentToken.TokenType = PreparationTokenType.LiteralNumber;
                    Tokens.Add(CurrentToken.Instantiate());

                    CurrentToken.Position.Range.Start.Character = CurrentToken.Position.Range.End.Character + 1;
                    CurrentToken.Position.Range.Start.Line = CurrentToken.Position.Range.End.Line + 1;
                    CurrentToken.Position.AbsoluteRange.Start = CurrentToken.Position.AbsoluteRange.End + 1;

                    CurrentToken.Position.Range.End.Character++;
                    CurrentToken.Position.Range.End.Line++;
                    CurrentToken.Position.AbsoluteRange.End++;
                    CurrentToken.TokenType = PreparationTokenType.Operator;
                    CurrentToken.Content.Clear();
                    CurrentToken.Content.Append('.');
                }
            }

            if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_FLOAT)
            {
                if (CurrentToken.ToString().Equals(".", StringComparison.Ordinal))
                { CurrentToken.TokenType = PreparationTokenType.Operator; }
                else
                { CurrentToken.TokenType = PreparationTokenType.LiteralFloat; }
            }

            Tokens.Add(CurrentToken.Instantiate());

        Finish:
            CurrentToken.TokenType = PreparationTokenType.Whitespace;
            CurrentToken.Content.Clear();
            CurrentToken.Position.Range.Start = new SinglePosition(CurrentLine, CurrentColumn);
            CurrentToken.Position.AbsoluteRange.Start = offsetTotal;
        }
    }
}
