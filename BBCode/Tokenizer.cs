using IngameCoding.Core;
using IngameCoding.Terminal;

namespace IngameCoding.BBCode
{
    enum TokenType
    {
        WHITESPACE,
        IDENTIFIER,

        LITERAL_NUMBER,
        LITERAL_STRING,
        LITERAL_FLOAT,

        OPERATOR,
        STRING_ESCAPE_SEQUENCE,

        POTENTIAL_FLOAT,
        POTENTIAL_COMMENT,
        POTENTIAL_END_MULTILINE_COMMENT,

        COMMENT,
        COMMENT_MULTILINE,
    }

    class BaseToken
    {
        public int startOffset;
        public int endOffset;
        public int lineNumber;

        public int startOffsetTotal;
        public int endOffsetTotal;
        public Position Position => new(lineNumber, startOffset, new Interval(startOffsetTotal, endOffsetTotal));
    }

    class Token : BaseToken
    {
        public TokenType type = TokenType.WHITESPACE;
        public string text = "";

        public TokenSubtype subtype = TokenSubtype.None;

        public Token Clone()
        {
            return (Token)MemberwiseClone();
        }

        public override string ToString()
        {
            return type.ToString() + " { text: \"" + text.ToString() + "\" }";
        }
    }

    class Tokenizer
    {
        /// <returns>(tokens, tokens with comments)</returns>
        public static (Token[], Token[]) Parse(string program, System.Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            System.Diagnostics.Stopwatch sw = null;
            if (printCallback != null)
            {
                printCallback("Tokenizing Code...", TerminalInterpreter.LogType.Debug);
                sw = new();
                sw.Start();
            }

            List<Token> tokens = new();
            List<Token> tokensWithComments = new();
            Token currentToken = new()
            {
                lineNumber = 1,
                startOffset = 1,
                endOffset = 1
            };

            char[] bracelets = new char[] { '{', '}', '(', ')', '[', ']' };

            int cursorPosition = 0;
            int cursorPositionTotal = 0;

            foreach (var currChar in program)
            {
                if (currChar == '\r')
                {
                    cursorPositionTotal++;
                    continue;
                }

                if (currChar == '\n' && currentToken.type == TokenType.COMMENT_MULTILINE)
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.text = "";
                    currentToken.type = TokenType.COMMENT_MULTILINE;
                }

                cursorPosition = (currChar == '\n') ? 1 : cursorPosition + 1;
                cursorPositionTotal++;

                if (currChar == '\u200B')
                {
                    continue;
                }

                if (currentToken.type == TokenType.STRING_ESCAPE_SEQUENCE)
                {
                    currentToken.text += currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '"' => "\"",
                        _ => throw new Errors.SyntaxException("Unknown escape sequence: \\" + currChar.ToString() + " in string.", currentToken),
                    };
                    currentToken.type = TokenType.LITERAL_STRING;
                    continue;
                }
                else if (currentToken.type == TokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
                {
                    currentToken.type = TokenType.OPERATOR;
                    if (currChar == '=')
                    {
                        currentToken.text += currChar;
                    }
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    continue;
                }
                else if (currentToken.type == TokenType.COMMENT && currChar != '\n')
                {
                    currentToken.text += currChar;
                    continue;
                }
                else if (currentToken.type == TokenType.LITERAL_STRING && currChar != '"')
                {
                    currentToken.text += currChar;
                    continue;
                }

                if (currentToken.type == TokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
                {
                    currentToken.text += currChar;
                    currentToken.type = TokenType.COMMENT_MULTILINE;
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    continue;
                }

                if (currentToken.type == TokenType.COMMENT_MULTILINE || currentToken.type == TokenType.POTENTIAL_END_MULTILINE_COMMENT)
                {
                    currentToken.text += currChar;
                    if (currentToken.type == TokenType.COMMENT_MULTILINE && currChar == '*')
                    {
                        currentToken.type = TokenType.POTENTIAL_END_MULTILINE_COMMENT;
                    }
                    else
                    {
                        currentToken.type = TokenType.COMMENT_MULTILINE;
                    }
                    continue;
                }

                if (currentToken.type == TokenType.POTENTIAL_FLOAT && !int.TryParse(currChar.ToString(), out _))
                {
                    currentToken.type = TokenType.OPERATOR;
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                }

                if (int.TryParse(currChar.ToString(), out _))
                {
                    if (currentToken.type == TokenType.WHITESPACE)
                        currentToken.type = TokenType.LITERAL_NUMBER;
                    else if (currentToken.type == TokenType.POTENTIAL_FLOAT)
                        currentToken.type = TokenType.LITERAL_FLOAT;
                    else if (currentToken.type == TokenType.OPERATOR)
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.text += currChar;
                }
                else if (currChar == '.')
                {
                    if (currentToken.type == TokenType.WHITESPACE)
                    {
                        currentToken.type = TokenType.POTENTIAL_FLOAT;
                        currentToken.text += currChar;
                    }
                    else if (currentToken.type == TokenType.LITERAL_NUMBER)
                    {
                        currentToken.type = TokenType.LITERAL_FLOAT;
                        currentToken.text += currChar;
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    }
                }
                else if (currChar == '/')
                {
                    if (currentToken.type == TokenType.POTENTIAL_COMMENT)
                    {
                        currentToken.type = TokenType.COMMENT;
                        currentToken.text = "";
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.POTENTIAL_COMMENT;
                        currentToken.text += currChar;
                    }
                }
                else if (bracelets.Contains(currChar))
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                }
                else if (currChar == ';')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                }
                else if (currChar == ',')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                }
                else if (currChar == '=')
                {
                    string[] strings = new string[] { "+", "-", "*", "%", "=", "!", "|", "&", "^" };
                    if (strings.Contains(currentToken.text))
                    {
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                    }
                }
                else if (currChar == '-')
                {
                    if (currentToken.text == "-")
                    {
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                    }
                }
                else if (currChar == '+')
                {
                    if (currentToken.text == "+")
                    {
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                    }
                }
                else if (currChar == '*')
                {
                    if (currentToken.type == TokenType.POTENTIAL_COMMENT)
                    {
                        currentToken.type = TokenType.COMMENT_MULTILINE;
                        currentToken.text += currChar;
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                    }
                }
                else if (currChar == '<')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '>')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '%')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '!')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '&')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '|')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '^')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == ' ' || currChar == '\t')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                }
                else if (currChar == '\r' || currChar == '\n')
                {
                    if (currentToken.type == TokenType.COMMENT_MULTILINE)
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.COMMENT_MULTILINE;
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    }
                    if (currChar == '\n') currentToken.lineNumber++;
                }
                else if (currChar == '"')
                {
                    if (currentToken.type != TokenType.LITERAL_STRING)
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.LITERAL_STRING;
                    }
                    else if (currentToken.type == TokenType.LITERAL_STRING)
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    }
                }
                else if (currChar == '\\')
                {
                    if (currentToken.type == TokenType.LITERAL_STRING)
                    {
                        currentToken.type = TokenType.STRING_ESCAPE_SEQUENCE;
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                    }
                }
                else
                {
                    if (currentToken.type == TokenType.WHITESPACE ||
                        currentToken.type == TokenType.LITERAL_NUMBER ||
                        currentToken.type == TokenType.LITERAL_FLOAT ||
                        currentToken.type == TokenType.OPERATOR)
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.IDENTIFIER;
                        currentToken.text += currChar;
                    }
                    else
                    {
                        currentToken.text += currChar;
                    }
                }
            }

            EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal);

            if (sw != null)
            {
                sw.Stop();
            }
            printCallback?.Invoke($"Code tokenized in {sw.ElapsedMilliseconds} ms", TerminalInterpreter.LogType.Debug);

            return (tokens.ToArray(), tokensWithComments.ToArray());
        }

        static void EndToken(Token token, List<Token> tokens, List<Token> tokensWithComments, int cursorPosition, int cursorPositionTotal)
        {
            token.endOffset = cursorPosition - 1;
            token.startOffset = cursorPosition - token.text.Length;
            token.endOffsetTotal = cursorPositionTotal - 1;
            token.startOffsetTotal = cursorPositionTotal - token.text.Length;

            if (token.type != TokenType.WHITESPACE)
            {
                tokensWithComments.Add(token.Clone());
                if (token.type != TokenType.COMMENT && token.type != TokenType.COMMENT_MULTILINE)
                {
                    tokens.Add(token.Clone());
                }
            }

            if (token.type == TokenType.POTENTIAL_FLOAT)
            {
                if (token.text.CompareTo(".") == 0)
                {
                    token.type = TokenType.OPERATOR;
                }
                else
                {
                    token.type = TokenType.LITERAL_FLOAT;
                }
            }

            token.type = TokenType.WHITESPACE;
            token.text = "";
        }
    }
}
