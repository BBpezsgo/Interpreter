using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode
{
    using Core;
    using IngameCoding.Tokenizer;
    using Terminal;

    public enum TokenType
    {
        WHITESPACE,
        LINEBREAK,

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

    public struct TokenizerSettings
    {
        /// <summary> The Tokenizer will produce <see cref="TokenType.WHITESPACE"/> </summary>
        public bool TokenizeWhitespaces;
        /// <summary> The Tokenizer will produce <see cref="TokenType.LINEBREAK"/> </summary>
        public bool DistinguishBetweenSpacesAndNewlines;
        public bool JoinLinebreaks;

        public static TokenizerSettings Default => new()
        {
            TokenizeWhitespaces = false,
            DistinguishBetweenSpacesAndNewlines = false,
            JoinLinebreaks = true,
        };
    }

    public class Token : BaseToken
    {
        public TokenType type = TokenType.WHITESPACE;
        public string text = "";

        public TokenSubtype subtype = TokenSubtype.None;

        public Token Clone() => (Token)MemberwiseClone();
        public override string ToString() => type.ToString() + " \"" + text.ToString() + "\"";
    }

    /// <summary>
    /// The tokenizer for the BBCode language
    /// </summary>
    public class Tokenizer
    {
        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <param name="settings">
        /// Tokenizer settings<br/>
        /// Use <see cref="TokenizerSettings.Default"/> if you don't know
        /// </param>
        /// <param name="printCallback">
        /// Optional: Print callback
        /// </param>
        /// <returns>
        /// Two token arrays: the first without, the second with the comments
        /// (tokens, tokens with comments)
        /// </returns>
        /// <exception cref="Errors.SyntaxException"/>
        public static (Token[], Token[]) Parse(string sourceCode, TokenizerSettings settings, Action<string, TerminalInterpreter.LogType> printCallback = null)
        {
            DateTime tokenizingStarted = DateTime.Now;
            if (printCallback != null)
            { printCallback?.Invoke("Tokenizing...", TerminalInterpreter.LogType.Debug); }

            List<Token> tokens = new();
            List<Token> tokensWithComments = new();
            Token currentToken = new()
            {
                lineNumber = 1,
                startOffset = 1,
                endOffset = 1
            };

            char[] bracelets = new char[] { '{', '}', '(', ')', '[', ']' };
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

                if (currChar == '\n' && currentToken.type == TokenType.COMMENT_MULTILINE)
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.text = "";
                    currentToken.type = TokenType.COMMENT_MULTILINE;
                }

                cursorPosition = currChar == '\n' ? 1 : cursorPosition + 1;

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
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
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
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
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
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                }

                if (int.TryParse(currChar.ToString(), out _))
                {
                    if (currentToken.type == TokenType.WHITESPACE)
                        currentToken.type = TokenType.LITERAL_NUMBER;
                    else if (currentToken.type == TokenType.POTENTIAL_FLOAT)
                        currentToken.type = TokenType.LITERAL_FLOAT;
                    else if (currentToken.type == TokenType.OPERATOR)
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
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
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
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
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.POTENTIAL_COMMENT;
                        currentToken.text += currChar;
                    }
                }
                else if (bracelets.Contains(currChar))
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                }
                else if (currChar == ';')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                }
                else if (currChar == ',')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                }
                else if (currChar == '=')
                {
                    string[] strings = new string[] { "+", "-", "*", "%", "=", "!", "|", "&", "^" };
                    if (strings.Contains(currentToken.text))
                    {
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                    }
                }
                else if (currChar == '-')
                {
                    if (currentToken.text == "-")
                    {
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                    }
                }
                else if (currChar == '+')
                {
                    if (currentToken.text == "+")
                    {
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
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
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                    }
                }
                else if (currChar == '<')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '>')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '%')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '!')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '&')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '|')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == '^')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.OPERATOR;
                    currentToken.text += currChar;
                }
                else if (currChar == ' ' || currChar == '\t')
                {
                    EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    currentToken.type = TokenType.WHITESPACE;
                    currentToken.text = currChar.ToString();
                }
                else if (currChar == '\n')
                {
                    if (currentToken.type == TokenType.COMMENT_MULTILINE)
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.COMMENT_MULTILINE;
                    }
                    else
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = settings.DistinguishBetweenSpacesAndNewlines ? TokenType.LINEBREAK : TokenType.WHITESPACE;
                        currentToken.text = currChar.ToString();
                    }
                    currentToken.lineNumber++;
                }
                else if (currChar == '"')
                {
                    if (currentToken.type != TokenType.LITERAL_STRING)
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.LITERAL_STRING;
                    }
                    else if (currentToken.type == TokenType.LITERAL_STRING)
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
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
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.OPERATOR;
                        currentToken.text += currChar;
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                    }
                }
                else
                {
                    if (currentToken.type == TokenType.WHITESPACE ||
                        currentToken.type == TokenType.LITERAL_NUMBER ||
                        currentToken.type == TokenType.LITERAL_FLOAT ||
                        currentToken.type == TokenType.OPERATOR)
                    {
                        EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);
                        currentToken.type = TokenType.IDENTIFIER;
                        currentToken.text += currChar;
                    }
                    else
                    {
                        currentToken.text += currChar;
                    }
                }
            }

            EndToken(currentToken, tokens, tokensWithComments, cursorPosition, cursorPositionTotal, settings);

            if (printCallback != null)
            { printCallback?.Invoke($"Tokenized in {(DateTime.Now - tokenizingStarted).TotalMilliseconds} ms", TerminalInterpreter.LogType.Debug); }

            return (NormalizeTokens(tokens, settings).ToArray(), NormalizeTokens(tokensWithComments, settings).ToArray());
        }

        static void EndToken(Token token, List<Token> tokens, List<Token> tokensWithComments, int cursorPosition, int cursorPositionTotal, TokenizerSettings settings)
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
            else if (!string.IsNullOrEmpty(token.text) && settings.TokenizeWhitespaces)
            {
                tokensWithComments.Add(token.Clone());
                tokens.Add(token.Clone());
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
    
        static List<Token> NormalizeTokens(List<Token> tokens, TokenizerSettings settings)
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

                if (token.type == TokenType.LINEBREAK && lastToken.type == TokenType.LINEBREAK && settings.JoinLinebreaks)
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
