using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BCCode
{
    using BBCode;
    using Core;
    using IngameCoding.Tokenizer;

    public enum TokenType
    {
        WHITESPACE,
        IDENTIFIER,

        LITERAL_INTEGER,
        LITERAL_STRING,
        LITERAL_DOUBLE,

        OPERATOR,
        STRING_ESCAPE_SEQUENCE,

        POTENTIAL_DOUBLE,

        COMMENT,

        LINEBREAK,
    }

    [System.Diagnostics.DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public class Token : BaseToken
    {
        public TokenType type = TokenType.WHITESPACE;
        public string text = "";

        public TokenSubtype subtype = TokenSubtype.None;

        public Token Clone() => (Token)MemberwiseClone();
        public override string ToString() => type.ToString() + " \"" + text.ToString() + "\"";
    }

    public class Tokenizer
    {
        public static List<Token> Parse(string sourceCode)
        {
            List<Token> tokens = new();
            Token currentToken = new()
            {
                lineNumber = 1,
                startOffset = 1,
                endOffset = 1
            };

            string[] operators = new string[] { "-", ";", ",", ":" };
            char[] banned = new char[] { '\r', '\u200B' };

            int cursorPosition = 0;
            int cursorPositionTotal = 0;

            foreach (var currChar in sourceCode)
            {
                if (banned.Contains(currChar))
                {
                    cursorPositionTotal++;
                    continue;
                }

                cursorPosition = (currChar == '\n') ? 1 : cursorPosition + 1;
                cursorPositionTotal++;

                if (currentToken.type == TokenType.STRING_ESCAPE_SEQUENCE)
                {
                    currentToken.text += currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        _ => throw new Errors.SyntaxException("Unknown escape sequence: \\" + currChar.ToString() + " in string.", currentToken),
                    };
                    currentToken.type = TokenType.LITERAL_STRING;
                    continue;
                }
                else if (currentToken.type == TokenType.COMMENT && (currChar != '\n' && currChar != '\r'))
                {
                    currentToken.text += currChar;
                    continue;
                }

                if (int.TryParse(currChar.ToString(), out _))
                {
                    if (currentToken.type == TokenType.WHITESPACE)
                        currentToken.type = TokenType.LITERAL_INTEGER;
                    else if (currentToken.type == TokenType.POTENTIAL_DOUBLE)
                        currentToken.type = TokenType.LITERAL_DOUBLE;
                    currentToken.text += currChar;
                }
                else if (currChar == '.')
                {
                    if (currentToken.type == TokenType.WHITESPACE)
                    {
                        currentToken.type = TokenType.POTENTIAL_DOUBLE;
                        currentToken.text += currChar;
                    }
                    else if (currentToken.type == TokenType.LITERAL_INTEGER)
                    {
                        currentToken.type = TokenType.LITERAL_DOUBLE;
                        currentToken.text += currChar;
                    }
                    else if (currentToken.type == TokenType.LITERAL_STRING)
                    {
                        currentToken.text += currChar;
                    }
                    else
                    {
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                    }
                }
                else if (currChar == ';')
                {
                    if (currentToken.type == TokenType.LITERAL_STRING)
                    {
                        currentToken.text += currChar;
                    }
                    else
                    {
                        currentToken.type = TokenType.COMMENT;
                        currentToken.text += currChar;
                    }
                }
                else if (operators.Contains(currChar.ToString()))
                {
                    if (currentToken.type != TokenType.LITERAL_STRING)
                    {
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                    }
                    else
                    {
                        currentToken.text += currChar;
                    }
                }
                else if (currChar == ' ' || currChar == '\t')
                {
                    if (currentToken.type == TokenType.LITERAL_STRING || currentToken.type == TokenType.COMMENT)
                    {
                        currentToken.text += currChar;
                    }
                    else
                    {
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                    }
                }
                else if (currChar == '\n')
                {
                    EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                    currentToken.type = TokenType.LINEBREAK;
                    currentToken.text = currChar.ToString();
                    EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                    if (currChar == '\n') currentToken.lineNumber++;
                }
                else if (currChar == '"')
                {
                    if (currentToken.type != TokenType.LITERAL_STRING)
                    {
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.LITERAL_STRING;
                    }
                    else if (currentToken.type == TokenType.LITERAL_STRING)
                    {
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
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
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                    }
                }
                else
                {
                    if (currentToken.type == TokenType.WHITESPACE ||
                        currentToken.type == TokenType.LITERAL_INTEGER ||
                        currentToken.type == TokenType.LITERAL_DOUBLE)
                    {
                        EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);
                        currentToken.type = TokenType.IDENTIFIER;
                        currentToken.text += currChar;
                    }
                    else
                    {
                        currentToken.text += currChar;
                    }
                }
            }

            EndToken(currentToken, tokens, cursorPosition, cursorPositionTotal);

            return NormalizeTokens(tokens);
        }

        static void EndToken(Token token, List<Token> tokens, int cursorPosition, int cursorPositionTotal)
        {
            token.endOffset = cursorPosition - 1;
            token.startOffset = cursorPosition - token.text.Length;
            token.endOffsetTotal = cursorPositionTotal - 1;
            token.startOffsetTotal = cursorPositionTotal - token.text.Length;


            if (token.type != TokenType.WHITESPACE)
            {
                tokens.Add(token.Clone());
            }

            if (token.type == TokenType.POTENTIAL_DOUBLE)
            {
                if (token.text.CompareTo(".") == 0)
                {
                    token.type = TokenType.OPERATOR;
                }
                else
                {
                    token.type = TokenType.LITERAL_DOUBLE;
                }
            }

            token.type = TokenType.WHITESPACE;
            token.text = "";
        }

        static List<Token> NormalizeTokens(List<Token> tokens)
        {
            List<Token> result = new();

            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (result.Count == 0)
                {
                    result.Add(token);
                    continue;
                }

                var lastToken = result.Last();

                if (token.type == TokenType.WHITESPACE && lastToken.type == TokenType.WHITESPACE)
                {
                    lastToken.text += token.text;
                    continue;
                }

                if (token.type == TokenType.LINEBREAK && lastToken.type == TokenType.LINEBREAK)
                {
                    lastToken.text += token.text;
                    continue;
                }


                result.Add(token);
            }

            return result;
        }
    }
}