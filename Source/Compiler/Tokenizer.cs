using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode
{
    using IngameCoding.BBCode.Compiler;
    using IngameCoding.BBCode.Parser.Statements;
    using IngameCoding.Core;
    using IngameCoding.Tokenizer;

    public class TokenAnalysis
    {
        public TokenSubtype Subtype = TokenSubtype.None;
        public TokenSubSubtype SubSubtype = TokenSubSubtype.None;

        public bool ParserReached;
        public bool CompilerReached;

        public RefBase Reference;

        string ReachedUnit => CompilerReached ? "compiled" : (ParserReached ? "parsed" : "tokenized");

        public override string ToString() => $"TokenAnalysis {{ {ReachedUnit} {Subtype} {SubSubtype} {Reference} }}";

        public abstract class RefBase
        {
            public override abstract string ToString();
        }

        public abstract class RefLocal : RefBase
        {

        }
        public abstract class Ref : RefBase
        {
            public string FilePath;

            public Ref(string filePath)
            {
                FilePath = filePath;
            }
        }

        public class RefVariable : RefLocal
        {
            public Statement_NewVariable Declaration;
            public bool IsGlobal;

            public RefVariable(Statement_NewVariable declaration, bool isGlobal)
            {
                Declaration = declaration;
                IsGlobal = isGlobal;
            }

            public override string ToString() => $"Ref Variable";
        }

        public class RefParameter : RefLocal
        {
            public string Type;

            public RefParameter(string type)
            {
                Type = type;
            }

            public override string ToString() => $"Ref Parameter";
        }

        public class RefField : Ref
        {
            public string Type;
            public Token Name;
            public string StructName;

            public RefField(string type, Token name, string structName, string filePath) : base(filePath)
            {
                Type = type;
                Name = name;
                StructName = structName;
            }

            public override string ToString() => $"Ref Field";
        }

        public class RefFunction : Ref
        {
            public CompiledFunction Definition;

            public RefFunction(CompiledFunction definition) : base(definition.FilePath)
            {
                Definition = definition;
            }

            public override string ToString() => $"Ref Function";
        }

        public class RefStruct : Ref
        {
            public CompiledStruct Definition;

            public RefStruct(CompiledStruct definition) : base(definition.FilePath)
            {
                Definition = definition;
            }

            public override string ToString() => $"Ref Struct";
        }

        public class RefClass : Ref
        {
            public CompiledClass Definition;

            public RefClass(CompiledClass definition) : base(definition.FilePath)
            {
                Definition = definition;
            }

            public override string ToString() => $"Ref Class";
        }
    }

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

        public Token()
        {
            Analysis = new()
            {
                Subtype = TokenSubtype.None,
                SubSubtype = TokenSubSubtype.None,
            };
        }

        public TokenAnalysis Analysis;

        public Token Clone() => new()
        {
            Position = Position,
            AbsolutePosition = AbsolutePosition,

            text = text,
            type = type,
            Analysis = new TokenAnalysis()
            {
                Subtype = Analysis.Subtype,
            },
        };
        public override string ToString() => text;

        internal string ToFullString()
        {
            return $"Token:{type} {{ \"{text}\" {Position} {(Analysis == null ? "No analysis" : Analysis.ToString())} }}";
        }
    }

    /// <summary>
    /// The tokenizer for the BBCode language
    /// </summary>
    public class Tokenizer
    {
        readonly Token CurrentToken;
        int CurrentColumn;
        int CurrentLine;

        readonly List<Token> tokens;
        readonly List<Token> tokensWithComments;

        readonly TokenizerSettings settings;
        readonly Action<string, Output.LogType> printCallback;

        void Print(string message, Output.LogType type) => printCallback?.Invoke(message, type);

        static readonly char[] bracelets = new char[] { '{', '}', '(', ')', '[', ']' };
        static readonly char[] banned = new char[] { '\r', '\u200B' };

        /// <param name="settings">
        /// Tokenizer settings<br/>
        /// Use <see cref="TokenizerSettings.Default"/> if you don't know
        /// </param>
        /// <param name="printCallback">
        /// Optional: Print callback
        /// </param>
        public Tokenizer(TokenizerSettings settings, Action<string, Output.LogType> printCallback = null)
        {
            CurrentToken = new()
            {
                Position = new Range<SinglePosition>()
                {
                    Start = new SinglePosition(0, 0),
                    End = new SinglePosition(0, 0),
                },
                AbsolutePosition = new Range<int>(0, 0),
            };
            CurrentColumn = 0;
            CurrentLine = 1;

            tokens = new();
            tokensWithComments = new();

            this.settings = settings;
            this.printCallback = printCallback;
        }

        Position GetCurrentPosition(int OffsetTotal) => new(new Range<SinglePosition>(new SinglePosition(CurrentLine, CurrentColumn), new SinglePosition(CurrentLine, CurrentColumn + 1)), new Range<int>(OffsetTotal, OffsetTotal + 1));

        /// <summary>
        /// Convert source code into tokens
        /// </summary>
        /// <param name="sourceCode">
        /// The source code
        /// </param>
        /// <returns>
        /// Two token arrays: the first without, the second with the comments
        /// (tokens, tokens with comments)
        /// </returns>
        /// <exception cref="Errors.TokenizerException"/>
        public (Token[], Token[]) Parse(string sourceCode)
        {
            DateTime tokenizingStarted = DateTime.Now;
            Print("Tokenizing ...", Output.LogType.Debug);

            for (int OffsetTotal = 0; OffsetTotal < sourceCode.Length; OffsetTotal++)
            {
                char currChar = sourceCode[OffsetTotal];

                CurrentColumn++;
                if (currChar == '\n')
                {
                    CurrentColumn = 1;
                    CurrentLine++;
                }

                if (banned.Contains(currChar))
                {
                    continue;
                }

                if (currChar == '\n' && CurrentToken.type == TokenType.COMMENT_MULTILINE)
                {
                    EndToken(OffsetTotal);
                    CurrentToken.text = "";
                    CurrentToken.type = TokenType.COMMENT_MULTILINE;
                }

                if (CurrentToken.type == TokenType.STRING_ESCAPE_SEQUENCE)
                {
                    CurrentToken.text += currChar switch
                    {
                        'n' => "\n",
                        'r' => "\r",
                        't' => "\t",
                        '\\' => "\\",
                        '"' => "\"",
                        _ => throw new Errors.TokenizerException("Unknown escape sequence: \\" + currChar.ToString() + " in string.", GetCurrentPosition(OffsetTotal)),
                    };
                    CurrentToken.type = TokenType.LITERAL_STRING;
                    continue;
                }
                else if (CurrentToken.type == TokenType.POTENTIAL_COMMENT && currChar != '/' && currChar != '*')
                {
                    CurrentToken.type = TokenType.OPERATOR;
                    if (currChar == '=')
                    {
                        CurrentToken.text += currChar;
                    }
                    EndToken(OffsetTotal);
                    continue;
                }
                else if (CurrentToken.type == TokenType.COMMENT && currChar != '\n')
                {
                    CurrentToken.text += currChar;
                    continue;
                }
                else if (CurrentToken.type == TokenType.LITERAL_STRING && currChar != '"')
                {
                    if (currChar == '\\')
                    {
                        CurrentToken.type = TokenType.STRING_ESCAPE_SEQUENCE;
                        continue;
                    }
                    CurrentToken.text += currChar;
                    continue;
                }

                if (CurrentToken.type == TokenType.POTENTIAL_END_MULTILINE_COMMENT && currChar == '/')
                {
                    CurrentToken.text += currChar;
                    CurrentToken.type = TokenType.COMMENT_MULTILINE;
                    EndToken(OffsetTotal);
                    continue;
                }

                if (CurrentToken.type == TokenType.COMMENT_MULTILINE || CurrentToken.type == TokenType.POTENTIAL_END_MULTILINE_COMMENT)
                {
                    CurrentToken.text += currChar;
                    if (CurrentToken.type == TokenType.COMMENT_MULTILINE && currChar == '*')
                    {
                        CurrentToken.type = TokenType.POTENTIAL_END_MULTILINE_COMMENT;
                    }
                    else
                    {
                        CurrentToken.type = TokenType.COMMENT_MULTILINE;
                    }
                    continue;
                }

                if (CurrentToken.type == TokenType.POTENTIAL_FLOAT && !int.TryParse(currChar.ToString(), out _))
                {
                    CurrentToken.type = TokenType.OPERATOR;
                    EndToken(OffsetTotal);
                }

                if (int.TryParse(currChar.ToString(), out _))
                {
                    if (CurrentToken.type == TokenType.WHITESPACE)
                    { CurrentToken.type = TokenType.LITERAL_NUMBER; }
                    else if (CurrentToken.type == TokenType.POTENTIAL_FLOAT)
                    { CurrentToken.type = TokenType.LITERAL_FLOAT; }
                    else if (CurrentToken.type == TokenType.OPERATOR)
                    {
                        if (CurrentToken.text != "-")
                        {
                            EndToken(OffsetTotal);
                        }
                        else
                        {
                            CurrentToken.type = TokenType.LITERAL_NUMBER;
                        }
                    }
                    CurrentToken.text += currChar;
                }
                else if (currChar == '.')
                {
                    if (CurrentToken.type == TokenType.WHITESPACE)
                    {
                        CurrentToken.type = TokenType.POTENTIAL_FLOAT;
                        CurrentToken.text += currChar;
                    }
                    else if (CurrentToken.type == TokenType.LITERAL_NUMBER)
                    {
                        CurrentToken.type = TokenType.LITERAL_FLOAT;
                        CurrentToken.text += currChar;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '/')
                {
                    if (CurrentToken.type == TokenType.POTENTIAL_COMMENT)
                    {
                        CurrentToken.type = TokenType.COMMENT;
                        CurrentToken.text = "";
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.POTENTIAL_COMMENT;
                        CurrentToken.text += currChar;
                    }
                }
                else if (bracelets.Contains(currChar))
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                    EndToken(OffsetTotal);
                }
                else if (currChar == ';')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                    EndToken(OffsetTotal);
                }
                else if (currChar == '#')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                    EndToken(OffsetTotal);
                }
                else if (currChar == ',')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                    EndToken(OffsetTotal);
                }
                else if (currChar == '=')
                {
                    string[] strings = new string[] { "+", "-", "*", "%", "=", "!", "|", "&", "^", "<", ">" };
                    if (strings.Contains(CurrentToken.text))
                    {
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                    }
                }
                else if (currChar == '<')
                {
                    if (CurrentToken.text == "<")
                    {
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                    }
                }
                else if (currChar == '>')
                {
                    if (CurrentToken.text == ">")
                    {
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                    }
                }
                else if (currChar == '-')
                {
                    if (CurrentToken.text == "-")
                    {
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                    }
                }
                else if (currChar == '+')
                {
                    if (CurrentToken.text == "+")
                    {
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                    }
                }
                else if (currChar == '*')
                {
                    if (CurrentToken.type == TokenType.POTENTIAL_COMMENT)
                    {
                        CurrentToken.type = TokenType.COMMENT_MULTILINE;
                        CurrentToken.text += currChar;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                    }
                }
                else if (currChar == '<')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                }
                else if (currChar == '>')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                }
                else if (currChar == '%')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                }
                else if (currChar == '!')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                }
                else if (currChar == '&')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                }
                else if (currChar == '|')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                }
                else if (currChar == '^')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.OPERATOR;
                    CurrentToken.text += currChar;
                }
                else if (currChar == ' ' || currChar == '\t')
                {
                    EndToken(OffsetTotal);
                    CurrentToken.type = TokenType.WHITESPACE;
                    CurrentToken.text = currChar.ToString();
                }
                else if (currChar == '\n')
                {
                    if (CurrentToken.type == TokenType.COMMENT_MULTILINE)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.COMMENT_MULTILINE;
                    }
                    else
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = settings.DistinguishBetweenSpacesAndNewlines ? TokenType.LINEBREAK : TokenType.WHITESPACE;
                        CurrentToken.text = currChar.ToString();
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '"')
                {
                    if (CurrentToken.type != TokenType.LITERAL_STRING)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.LITERAL_STRING;
                    }
                    else if (CurrentToken.type == TokenType.LITERAL_STRING)
                    {
                        EndToken(OffsetTotal);
                    }
                }
                else if (currChar == '\\')
                {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.OPERATOR;
                        CurrentToken.text += currChar;
                        EndToken(OffsetTotal);
                }
                else
                {
                    if (CurrentToken.type == TokenType.WHITESPACE ||
                        CurrentToken.type == TokenType.LITERAL_NUMBER ||
                        CurrentToken.type == TokenType.LITERAL_FLOAT ||
                        CurrentToken.type == TokenType.OPERATOR)
                    {
                        EndToken(OffsetTotal);
                        CurrentToken.type = TokenType.IDENTIFIER;
                        CurrentToken.text += currChar;
                    }
                    else
                    {
                        CurrentToken.text += currChar;
                    }
                }
            }

            EndToken(sourceCode.Length);

            Print($"Tokenized in {(DateTime.Now - tokenizingStarted).TotalMilliseconds} ms", Output.LogType.Debug);

            return (NormalizeTokens(tokens, settings).ToArray(), NormalizeTokens(tokensWithComments, settings).ToArray());
        }

        void EndToken(int OffsetTotal)
        {
            CurrentToken.Position.End.Character = CurrentColumn;
            CurrentToken.Position.End.Line = CurrentLine;
            CurrentToken.AbsolutePosition.End = OffsetTotal;

            if (CurrentToken.type != TokenType.WHITESPACE)
            {
                tokensWithComments.Add(CurrentToken.Clone());
                if (CurrentToken.type != TokenType.COMMENT && CurrentToken.type != TokenType.COMMENT_MULTILINE)
                {
                    tokens.Add(CurrentToken.Clone());
                }
            }
            else if (!string.IsNullOrEmpty(CurrentToken.text) && settings.TokenizeWhitespaces)
            {
                tokensWithComments.Add(CurrentToken.Clone());
                tokens.Add(CurrentToken.Clone());
            }

            if (CurrentToken.type == TokenType.POTENTIAL_FLOAT)
            {
                if (CurrentToken.text.CompareTo(".") == 0)
                {
                    CurrentToken.type = TokenType.OPERATOR;
                }
                else
                {
                    CurrentToken.type = TokenType.LITERAL_FLOAT;
                }
            }

            CurrentToken.type = TokenType.WHITESPACE;
            CurrentToken.text = "";
            CurrentToken.Position.Start.Line = CurrentLine;
            CurrentToken.Position.Start.Character = CurrentColumn;
            CurrentToken.AbsolutePosition.Start = OffsetTotal;

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
