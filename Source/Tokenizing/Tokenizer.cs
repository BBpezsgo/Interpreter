namespace LanguageCore.Tokenizing;

public partial class Tokenizer
{
    bool IsBeginningOfLine
    {
        get
        {
            foreach (Token token in Tokens)
            {
                if (token.Position.Range.End.Line != CurrentLine ||
                    token.TokenType is
                    TokenType.LineBreak or
                    TokenType.Whitespace)
                { continue; }
                return false;
            }

            if (CurrentToken.Position.Range.End.Line != CurrentLine ||
                CurrentToken.TokenType is
                PreparationTokenType.LineBreak or
                PreparationTokenType.Whitespace)
            { return true; }

            return false;
        }
    }

    bool ExpectPreprocessArguments
    {
        get
        {
            for (int i = Tokens.Count - 1; i >= 0; i--)
            {
                Token token = Tokens[i];

                if (token.TokenType == TokenType.Whitespace)
                {
                    if (token.Content.Contains('\r') || token.Content.Contains('\n'))
                    { break; }
                    continue;
                }

                if (token.TokenType == TokenType.CommentMultiline)
                { continue; }

                if (token.TokenType == TokenType.PreprocessIdentifier)
                { return token.Position.Range.End.Line == CurrentLine; }

                break;
            }

            return false;
        }
    }

    PreprocessorTag? LastPreprocess
    {
        get
        {
            Token? argument = null;

            for (int i = Tokens.Count - 1; i >= 0; i--)
            {
                Token token = Tokens[i];

                if (token.TokenType == TokenType.Whitespace)
                {
                    if (token.Content.Contains('\r') || token.Content.Contains('\n'))
                    { break; }
                    continue;
                }

                if (token.TokenType == TokenType.CommentMultiline)
                { continue; }

                if (argument is null &&
                    token.TokenType == TokenType.PreprocessArgument)
                {
                    argument = token;
                    continue;
                }

                if (token.TokenType == TokenType.PreprocessIdentifier)
                { return new PreprocessorTag(token, argument); }

                break;
            }

            return null;
        }
    }

    enum PreprocessPhase
    {
        If,
        Elseif,
        Else,
    }

    class PreprocessThing
    {
        public PreprocessPhase Phase;
        public bool Condition;
    }

    readonly List<PreprocessorTag> UnprocessedPreprocessorTags = new();

    void HandlePreprocess(bool finished)
    {
        PreprocessorTag? last = LastPreprocess;
        if (last is null) return;

        switch (last.Identifier.Content)
        {
            case "#if":
            {
                if (!finished)
                { break; }

                if (last.Argument is null)
                { Diagnostics.Add(Diagnostic.Critical($"Argument expected after preprocessor tag \"{last.Identifier}\"", last.Identifier.Position.After(), File)); }

                bool condition = PreprocessorVariables.Contains(last.Argument?.Content ?? string.Empty);
                PreprocessorConditions.Push(new PreprocessThing()
                {
                    Condition = condition,
                    Phase = PreprocessPhase.If,
                });

                break;
            }

            // case "#elseif":
            // {
            //     if (PreprocessConditions.Count == 0)
            //     { throw new TokenizerException($"Unexpected preprocessor tag \"{last.Identifier}\"", last.Identifier.Position, File); }
            // 
            //     if (last.Argument is null)
            //     { throw new TokenizerException($"Argument expected after preprocessor tag \"{last.Identifier}\"", last.Identifier.Position.After(), File); }
            // 
            //     bool condition = PreprocessVariables.Contains(last.Argument.Content);
            //     PreprocessConditions.Last = PreprocessConditions.Last ? !PreprocessConditions.Last : condition;
            // 
            //     break;
            // }

            case "#else":
            {
                if (finished)
                { break; }

                if (PreprocessorConditions.Count == 0)
                { Diagnostics.Add(Diagnostic.Critical($"Unexpected preprocessor tag \"{last.Identifier}\"", last.Identifier.Position, File)); }
                else
                {
                    switch (PreprocessorConditions.Last.Phase)
                    {
                        case PreprocessPhase.If:
                            PreprocessorConditions.Last.Condition = !PreprocessorConditions.Last.Condition;
                            PreprocessorConditions.Last.Phase = PreprocessPhase.Else;
                            break;
                        case PreprocessPhase.Else:
                            Diagnostics.Add(Diagnostic.Critical($"Unexpected preprocessor tag \"{last.Identifier}\"", last.Identifier.Position, File));
                            break;
                    }
                }

                break;
            }

            case "#endif":
            {
                if (finished)
                { break; }

                if (PreprocessorConditions.Count == 0)
                { Diagnostics.Add(Diagnostic.Critical($"Unexpected preprocessor tag \"{last.Identifier}\"", last.Identifier.Position, File)); }
                else
                { PreprocessorConditions.Pop(); }

                break;
            }

            case "#define":
            {
                if (!finished)
                { break; }

                if (IsPreprocessSkipping)
                { break; }

                if (last.Argument is null)
                { Diagnostics.Add(Diagnostic.Critical($"Argument expected after preprocessor tag \"{last.Identifier}\"", last.Identifier.Position.After(), File)); }
                else
                { PreprocessorVariables.Add(last.Argument.Content); }

                break;
            }

            case "#undefine":
            {
                if (!finished)
                { break; }

                if (IsPreprocessSkipping)
                { break; }

                if (last.Argument is null)
                { Diagnostics.Add(Diagnostic.Critical($"Argument expected after preprocessor tag \"{last.Identifier}\"", last.Identifier.Position.After(), File)); }
                else
                { PreprocessorVariables.Remove(last.Argument.Content); }

                break;
            }

            default:
            {
                if (finished)
                { UnprocessedPreprocessorTags.Add(last); }
                break;
            }
        }
    }

    readonly Stack<PreprocessThing> PreprocessorConditions;
    readonly ImmutableHashSet<string> PreprocessorVariables;

    bool IsPreprocessSkipping
    {
        get
        {
            foreach (PreprocessThing item in PreprocessorConditions)
            {
                if (!item.Condition)
                { return true; }
            }
            return false;
        }
    }

    void ProcessCharacter(char currChar, int offsetTotal)
    {
    Again:

        if (CurrentToken.TokenType == PreparationTokenType.STRING_UnicodeCharacter)
        {
            if (SavedUnicode == null) throw new UnreachableException($"{nameof(SavedUnicode)} is null");
            if (SavedUnicode.Length == 4)
            {
                string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(SavedUnicode, 16));
                UnicodeCharacters.Add(new SimpleToken(
                        unicodeChar,
                        new Position(
                            new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), CurrentSinglePosition),
                            new Range<int>(offsetTotal - 6, offsetTotal)
                        )
                    ));
                CurrentToken.Content.Append(unicodeChar);
                CurrentToken.TokenType = PreparationTokenType.LiteralString;
                SavedUnicode = null;
            }
#if NET_STANDARD
            else if (!CompatibilityUtils.IsAsciiHexDigit(currChar))
#else
            else if (!char.IsAsciiHexDigit(currChar))
#endif
            {
                Diagnostics.Add(Diagnostic.Critical($"This isn't a hex digit \"{currChar}\"", GetCurrentPosition(offsetTotal), File));
            }
            else
            {
                SavedUnicode += currChar;
                goto FinishCharacter;
            }
        }
        else if (CurrentToken.TokenType == PreparationTokenType.CHAR_UnicodeCharacter)
        {
            if (SavedUnicode == null) throw new UnreachableException($"{nameof(SavedUnicode)} is null");
            if (SavedUnicode.Length == 4)
            {
                string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(SavedUnicode, 16));
                UnicodeCharacters.Add(new SimpleToken(
                    unicodeChar,
                    new Position(
                        new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn - 6), CurrentSinglePosition),
                        new Range<int>(offsetTotal - 6, offsetTotal)
                    )
                ));
                CurrentToken.Content.Append(unicodeChar);
                CurrentToken.TokenType = PreparationTokenType.LiteralCharacter;
                SavedUnicode = null;
            }
#if NET_STANDARD
            else if (!CompatibilityUtils.IsAsciiHexDigit(currChar))
#else
            else if (!char.IsAsciiHexDigit(currChar))
#endif
            {
                Diagnostics.Add(Diagnostic.Critical($"This isn't a hex digit: \"{currChar}\"", GetCurrentPosition(offsetTotal), File));
            }
            else
            {
                SavedUnicode += currChar;
                goto FinishCharacter;
            }
        }

        if (IsBeginningOfLine &&
            currChar == '#' &&
            CurrentToken.TokenType is
            PreparationTokenType.Whitespace or
            PreparationTokenType.PREPROCESS_Skipped)
        {
            _ = IsBeginningOfLine;
            EndToken(offsetTotal);
            CurrentToken.TokenType = PreparationTokenType.PREPROCESS_Operator;
            CurrentToken.Content.Append(currChar);
            goto FinishCharacter;
        }
        else if (CurrentToken.TokenType == PreparationTokenType.PREPROCESS_Operator)
        {
#if NET_STANDARD
            if (CompatibilityUtils.IsAsciiLetter(currChar))
#else
            if (char.IsAsciiLetter(currChar))
#endif
            {
                CurrentToken.TokenType = PreparationTokenType.PREPROCESS_Identifier;
                CurrentToken.Content.Append(currChar);
            }
            else
            {
                CurrentToken.TokenType = PreparationTokenType.Operator;
                EndToken(offsetTotal);
            }
            goto FinishCharacter;
        }
        else if (CurrentToken.TokenType == PreparationTokenType.PREPROCESS_Identifier)
        {
#if NET_STANDARD
            if (CompatibilityUtils.IsAsciiLetterOrDigit(currChar))
#else
            if (char.IsAsciiLetterOrDigit(currChar))
#endif
            {
                CurrentToken.Content.Append(currChar);
                goto FinishCharacter;
            }
            else
            {
                EndToken(offsetTotal);
                HandlePreprocess(false);
            }
        }
        else if (ExpectPreprocessArguments || CurrentToken.TokenType == PreparationTokenType.PREPROCESS_Argument)
        {
            if (CurrentToken.TokenType != PreparationTokenType.PREPROCESS_Argument)
            { EndToken(offsetTotal); }

#if NET_STANDARD
            if (CompatibilityUtils.IsAsciiLetter(currChar))
#else
            if (char.IsAsciiLetter(currChar))
#endif
            {
                CurrentToken.TokenType = PreparationTokenType.PREPROCESS_Argument;
                CurrentToken.Content.Append(currChar);
                goto FinishCharacter;
            }

            if (char.IsWhiteSpace(currChar))
            {
                EndToken(offsetTotal);
                HandlePreprocess(true);
            }
            else
            { Diagnostics.Add(Diagnostic.Critical($"Unexpected character \'{currChar.Escape()}\'", GetCurrentPosition(offsetTotal), File)); }
        }

        if (IsPreprocessSkipping)
        {
            if (currChar is '\r' or '\n' ||
                CurrentLine != CurrentToken.Position.Range.End.Line)
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = PreparationTokenType.PREPROCESS_Skipped;
            }
            else
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = PreparationTokenType.PREPROCESS_Skipped;
            }
            goto FinishCharacter;
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
                char c;
                switch (currChar)
                {
                    case 'n': c = '\n'; break;
                    case 'r': c = '\r'; break;
                    case 't': c = '\t'; break;
                    case '\\': c = '\\'; break;
                    case '"': c = '\"'; break;
                    case '0': c = '\0'; break;
                    case 'e': c = '\u001b'; break;
                    default:
                    {
                        c = currChar;
                        Diagnostics.Add(Diagnostic.Critical($"I don't know this escape sequence: \\{currChar}", GetCurrentPosition(offsetTotal), File));
                        break;
                    }
                }
                CurrentToken.Content.Append(c);
                CurrentToken.TokenType = PreparationTokenType.LiteralString;
            }
            goto FinishCharacter;
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
                char c;
                switch (currChar)
                {
                    case 'n': c = '\n'; break;
                    case 'r': c = '\r'; break;
                    case 't': c = '\t'; break;
                    case '\\': c = '\\'; break;
                    case '"': c = '\"'; break;
                    case '0': c = '\0'; break;
                    case 'e': c = '\u001b'; break;
                    default:
                    {
                        c = currChar;
                        Diagnostics.Add(Diagnostic.Critical($"I don't know this escape sequence: \\{currChar}", GetCurrentPosition(offsetTotal), File));
                        break;
                    }
                }
                CurrentToken.Content.Append(c);
                CurrentToken.TokenType = PreparationTokenType.LiteralCharacter;
            }
            goto FinishCharacter;
        }
        else if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
        {
            CurrentToken.TokenType = PreparationTokenType.Operator;
            if (currChar == '=')
            {
                CurrentToken.Content.Append(currChar);
                EndToken(offsetTotal, true);
                goto FinishCharacter;
            }
            else
            {
                EndToken(offsetTotal);
                goto Again;
            }
        }
        else if (CurrentToken.TokenType == PreparationTokenType.Comment && (currChar is not '\n' and not '\r'))
        {
            CurrentToken.Content.Append(currChar);
            goto FinishCharacter;
        }
        else if (CurrentToken.TokenType == PreparationTokenType.LiteralString && currChar != '"')
        {
            if (currChar == '\\')
            { CurrentToken.TokenType = PreparationTokenType.STRING_EscapeSequence; }
            else
            { CurrentToken.Content.Append(currChar); }
            goto FinishCharacter;
        }
        else if (CurrentToken.TokenType == PreparationTokenType.LiteralCharacter && currChar != '\'')
        {
            if (currChar == '\\')
            { CurrentToken.TokenType = PreparationTokenType.CHAR_EscapeSequence; }
            else
            { CurrentToken.Content.Append(currChar); }
            goto FinishCharacter;
        }

        if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_END_MULTILINE_COMMENT)
        {
            if (currChar == '/')
            {
                // CurrentToken.Content.Append(currChar);
                // Remove last character ('*')
                CurrentToken.Content.Remove(CurrentToken.Content.Length - 1, 1);
                // Remove first two characters ('/' and '*')
                CurrentToken.Content.Remove(0, 2);
                CurrentToken.TokenType = PreparationTokenType.CommentMultiline;
                EndToken(offsetTotal, true);
                goto FinishCharacter;
            }
            else
            {
                CurrentToken.Content.Append(currChar);
                CurrentToken.TokenType = PreparationTokenType.CommentMultiline;
                goto FinishCharacter;
            }
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
            goto FinishCharacter;
        }

#if NET_STANDARD
        if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_FLOAT && !CompatibilityUtils.IsAsciiDigit(currChar))
#else
        if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_FLOAT && !char.IsAsciiDigit(currChar))
#endif
        {
            CurrentToken.TokenType = PreparationTokenType.Operator;
            EndToken(offsetTotal);
        }

        if (currChar == 'f' && (CurrentToken.TokenType is PreparationTokenType.LiteralNumber or PreparationTokenType.LiteralFloat))
        {
            CurrentToken.Content.Append(currChar);
            CurrentToken.TokenType = PreparationTokenType.LiteralFloat;
            EndToken(offsetTotal, true /* Include the 'f' in position */ );
        }
        else if (currChar == 'e' && (CurrentToken.TokenType is PreparationTokenType.LiteralNumber or PreparationTokenType.LiteralFloat))
        {
            if (CurrentToken.Contains(currChar))
            { Diagnostics.Add(Diagnostic.Critical($"Am I stupid or this is not a float number?", CurrentToken.Position, File)); }
            CurrentToken.Content.Append(currChar);
            CurrentToken.TokenType = PreparationTokenType.LiteralFloat;
        }
        else if (currChar == 'x' &&
            CurrentToken.TokenType == PreparationTokenType.LiteralNumber &&
            CurrentToken.Content.Equals("0"))
        {
            if (!CurrentToken.EndsWith('0'))
            { Diagnostics.Add(Diagnostic.Critical($"Am I stupid or this is not a hex number?", CurrentToken.Position, File)); }
            CurrentToken.Content.Append(currChar);
            CurrentToken.TokenType = PreparationTokenType.LiteralHex;
        }
        else if (currChar == 'b' &&
            CurrentToken.TokenType == PreparationTokenType.LiteralNumber &&
            CurrentToken.Content.Equals("0"))
        {
            if (!CurrentToken.EndsWith('0'))
            { Diagnostics.Add(Diagnostic.Critical($"Am I stupid or this is not a binary number?", CurrentToken.Position, File)); }
            CurrentToken.Content.Append(currChar);
            CurrentToken.TokenType = PreparationTokenType.LiteralBinary;
        }
#if NET_STANDARD
        else if (CompatibilityUtils.IsAsciiDigit(currChar))
#else
        else if (char.IsAsciiDigit(currChar))
#endif
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
                if (!CurrentToken.Equals("-"))
                { EndToken(offsetTotal); }
                CurrentToken.TokenType = PreparationTokenType.LiteralNumber;
            }

            if (CurrentToken.TokenType == PreparationTokenType.LiteralBinary)
            {
                if (currChar is not '0' and not '1')
                {
                    RefreshTokenPosition(offsetTotal);
                    Diagnostics.Add(Diagnostic.Critical($"This isn't a binary digit am i right? \'{currChar}\'", CurrentToken.Position.After(), File));
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
                EndToken(offsetTotal, true);
            }
            else
            {
                EndToken(offsetTotal);
                CurrentToken.TokenType = PreparationTokenType.Operator;
                CurrentToken.Content.Append(currChar);
            }
        }
        else if (CurrentToken.Content.Length == 1 && DoubleOperators.Any(v => v[0] == CurrentToken.Content[0] && v[1] == currChar))
        {
            CurrentToken.Content.Append(currChar);
            EndToken(offsetTotal, true);
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
        else if (currChar is '\r' or '\n')
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
        else if (char.IsWhiteSpace(currChar))
        {
            EndToken(offsetTotal);
            CurrentToken.TokenType = PreparationTokenType.Whitespace;
            CurrentToken.Content.Append(currChar);
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
                EndToken(offsetTotal, true /* Include the '"' in position */);
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
                EndToken(offsetTotal, true /* Include the '\'' in position */);
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
                Diagnostics.Add(Diagnostic.Critical($"This isn't a hex digit am i right? \'{currChar}\'", CurrentToken.Position.After(), File));
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

    FinishCharacter:
        CurrentColumn++;
        if (currChar is '\n')
        {
            CurrentLine++;
            CurrentColumn = 0;
        }
    }

    void EndToken(int offsetTotal, bool inFuture = false)
    {
        if (inFuture)
        {
            CurrentToken.Position = new Position(
                new Range<SinglePosition>(CurrentToken.Position.Range.Start, new SinglePosition(CurrentLine, CurrentColumn + 1)),
                new Range<int>(CurrentToken.Position.AbsoluteRange.Start, offsetTotal + 1)
                );
        }
        else
        {
            CurrentToken.Position = new Position(
                new Range<SinglePosition>(CurrentToken.Position.Range.Start, CurrentSinglePosition),
                new Range<int>(CurrentToken.Position.AbsoluteRange.Start, offsetTotal)
                );
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

        // Skip empty tokens
        if (CurrentToken.Content.Length == 0 &&
            CurrentToken.TokenType is
            PreparationTokenType.Whitespace or
            PreparationTokenType.PREPROCESS_Skipped)
        { goto Finish; }

        if (CurrentToken.TokenType == PreparationTokenType.LiteralFloat)
        {
            if (CurrentToken.EndsWith('.'))
            {
                (PreparationToken? number, PreparationToken? op) = CurrentToken.Slice(CurrentToken.Content.Length - 1);

                if (number is null || op is null)
                { throw new UnreachableException($"I failed at token splitting :("); }

                number.TokenType = PreparationTokenType.LiteralNumber;
                op.TokenType = PreparationTokenType.Operator;

                Tokens.Add(number.Instantiate());
                Tokens.Add(op.Instantiate());
                goto Finish;
            }
        }

        if (CurrentToken.TokenType == PreparationTokenType.LiteralCharacter)
        {
            if (CurrentToken.Content.Length > 1)
            { Diagnostics.Add(Diagnostic.Critical($"I think there are more characters than there should be ({CurrentToken.Content.Length})", CurrentToken.Position, File)); }
            else if (CurrentToken.Content.Length < 1)
            { Diagnostics.Add(Diagnostic.Critical($"I think there are less characters than there should be ({CurrentToken.Content.Length})", CurrentToken.Position, File)); }
        }

        if (CurrentToken.TokenType == PreparationTokenType.POTENTIAL_FLOAT)
        {
            if (CurrentToken.Equals("."))
            { CurrentToken.TokenType = PreparationTokenType.Operator; }
            else
            { CurrentToken.TokenType = PreparationTokenType.LiteralFloat; }
        }

        Tokens.Add(CurrentToken.Instantiate());

    Finish:
        CurrentToken.TokenType = PreparationTokenType.Whitespace;
        CurrentToken.Content.Clear();

        CurrentToken.Position = new Position(
            new Range<SinglePosition>(CurrentSinglePosition, CurrentToken.Position.Range.End),
            new Range<int>(offsetTotal, CurrentToken.Position.AbsoluteRange.End)
            );
    }
}
