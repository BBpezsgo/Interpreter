using System;
using System.IO;

namespace TheProgram
{
    using Communicating;

    using IngameCoding.BBCode;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;

    using System.Collections.Generic;

    internal static class CodeEditor
    {
        public static void Run(ArgumentParser.Settings settings_)
        {
            ArgumentParser.Settings settings = settings_;

            var ipc = new IPC();

            ipc.OnRecived += (manager, message) =>
            {
                switch (message.type)
                {
                    case "raw-code":
                        {
                            var code = File.ReadAllText(settings.File.FullName, System.Text.Encoding.UTF8);
                            (Token[] tokens, Token[] tokensWithComments) = (null, null);
                            try
                            {
                                (tokens, tokensWithComments) = Tokenizer.Parse(code, new TokenizerSettings()
                                {
                                    DistinguishBetweenSpacesAndNewlines = true,
                                    JoinLinebreaks = false,
                                    TokenizeWhitespaces = true,
                                });
                            }
                            catch (IngameCoding.Errors.Exception err)
                            {
                                ipc.Send("result", new Data_Result()
                                {
                                    Tokens = tokensWithComments.ToData(v => new Data_Token(v)),
                                    Error = new Data_Error(err),
                                });
                                return;
                            }
                            catch (Exception)
                            {
                                ipc.Send("result", new Data_Result()
                                {
                                    Tokens = tokensWithComments.ToData(v => new Data_Token(v)),
                                    Error = null,
                                });
                                return;
                            }

                            List<Token> tokensWithoutWhitespaces = new();
                            for (int i = 0; i < tokensWithComments.Length; i++)
                            {
                                if (tokensWithComments[i].type == TokenType.WHITESPACE) continue;
                                if (tokensWithComments[i].type == TokenType.COMMENT) continue;
                                if (tokensWithComments[i].type == TokenType.COMMENT_MULTILINE) continue;
                                if (tokensWithComments[i].type == TokenType.LINEBREAK) continue;
                                tokensWithoutWhitespaces.Add(tokensWithComments[i]);
                            }

                            var parser = new IngameCoding.BBCode.Parser.Parser();
                            try
                            {
                                parser.Parse(tokensWithoutWhitespaces.ToArray(), new List<IngameCoding.Errors.Warning>());
                            }
                            catch (IngameCoding.Errors.Exception err)
                            {
                                ipc.Send("result", new Data_Result()
                                {
                                    Tokens = tokensWithComments.ToData(v => new Data_Token(v)),
                                    Error = new Data_Error(err),
                                });
                                return;
                            }
                            catch (Exception)
                            {
                                ipc.Send("result", new Data_Result()
                                {
                                    Tokens = tokensWithComments.ToData(v => new Data_Token(v)),
                                    Error = null,
                                });
                                return;
                            }

                            ipc.Send("result", new Data_Result()
                            {
                                Tokens = tokensWithComments.ToData(v => new Data_Token(v)),
                                Error = null,
                            });
                        }
                        break;
                }
            };

            ipc.Start();
        }
    }

    public class Data_Result
    {
        public Data_Token[] Tokens { get; set; }
        public Data_Error Error { get; set; }
    }

    public class Data_Token : Data_Serializable<Token>
    {
        public string Type { get; set; }
        public int Col { get; set; }
        public int Line { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
        public string Text { get; set; }
        public string Subtype { get; set; }

        public Data_Token(Token v) : base(v)
        {
            this.Type = v.type.ToString();
            this.Col = v.startOffset;
            this.Line = v.lineNumber;
            this.Start = v.startOffsetTotal;
            this.End = v.endOffsetTotal;
            this.Text = v.text;
            this.Subtype = v.subtype.ToString();
        }
    }

    public class Data_Error : Data_Serializable<IngameCoding.Errors.Exception>
    {
        public string Message { get; set; }
        public int Start { get; set; }
        public int End { get; set; }

        public Data_Error(IngameCoding.Errors.Exception v) : base(v)
        {
            this.Message = v.Message;
            if (v.Token != null)
            {
                this.Start = v.Token.startOffsetTotal;
                this.End = v.Token.endOffsetTotal;
            }
            else
            {
                this.Start = v.Position.AbsolutePosition.Start;
                this.End = v.Position.AbsolutePosition.End;
            }
        }
    }
}
