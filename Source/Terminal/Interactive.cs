using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Win32;
using Win32.LowLevel;
using Color = System.Drawing.Color;

namespace LanguageCore
{
    using BBCode.Generator;
    using Compiler;
    using Parser.Statement;
    using Runtime;
    using Tokenizing;

    public class Interactive
    {
        struct Session
        {
            public string Input;
            public string Output;
        }

        struct Colorized
        {
            public string? ColorizedText;
            public int[] ColorizedCharacterPositions;
            public string? CurrentError;
            public bool FailedToColorize;

            public static Colorized Null => new()
            {
                ColorizedText = null,
                ColorizedCharacterPositions = Array.Empty<int>(),
                FailedToColorize = false,
                CurrentError = null,
            };

            public static Colorized Success(string colorized, int originalLength) => new()
            {
                ColorizedText = colorized,
                ColorizedCharacterPositions = new int[originalLength],
                FailedToColorize = false,
                CurrentError = null,
            };

            public static Colorized Error(string error) => new()
            {
                ColorizedText = null,
                ColorizedCharacterPositions = Array.Empty<int>(),
                FailedToColorize = true,
                CurrentError = error,
            };

            public void Clear()
            {
                ColorizedText = null;
                ColorizedCharacterPositions = Array.Empty<int>();
                FailedToColorize = false;
            }

            public readonly void Render(AnsiBuilder renderer, int cursorPosition)
            {
                int bruh = (cursorPosition >= 0 && cursorPosition < ColorizedCharacterPositions.Length) ? ColorizedCharacterPositions[cursorPosition] : -1;
                for (int i = 0; i < ColorizedText!.Length; i++)
                {
                    if (bruh == i)
                    { renderer.Underline = true; }
                    renderer.Append(ColorizedText[i]);
                    if (bruh == i)
                    { renderer.Underline = false; }
                }
                renderer.ResetStyle();
            }

            public readonly void Render(AnsiBuilder renderer)
            {
                renderer.Append(ColorizedText);
                renderer.ResetStyle();
            }
        }

        readonly AnsiBuilder Renderer;
        readonly StringBuilder Input;
        Colorized ColorizedInput;
        readonly List<Session> Sessions;
        int LastRendererLength;
        bool ForceClear;
        bool ShouldRender;

        double LastInput;

        bool Entered;
        bool Escaped;
        int CursorPosition;
        bool EnableInput;

        static bool Blinker => DateTime.UtcNow.TimeOfDay.TotalSeconds % 1d < 0.5d;
        bool lastBlinked;

        public Interactive()
        {
            Input = new StringBuilder();
            CursorPosition = 0;
            Entered = false;
            Escaped = false;
            EnableInput = true;
            Sessions = new List<Session>();
            ColorizedInput = Colorized.Null;
            LastInput = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            LastRendererLength = 0;
            ForceClear = false;
            ShouldRender = true;

            Console.CursorVisible = false;
            Console.Clear();

            Renderer = new AnsiBuilder();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConsoleListener.Start();

                ConsoleListener.KeyEvent += OnKey;
                ConsoleListener.MouseEvent += OnMouse;
                ConsoleListener.WindowBufferSizeEvent += OnWindowResize;
            }
        }

        void OnWindowResize(WindowBufferSizeEvent e)
        {
            ShouldRender = true;
        }

        void OnMouse(MouseEvent e)
        {

        }

        void OnKey(KeyEvent e)
        {
            if (e.IsDown == 0) return;
            if (!EnableInput) return;

            if ((e.UnicodeChar >= '!' && e.UnicodeChar <= '~') ||
                e.UnicodeChar == ' ')
            {
                if (CursorPosition == Input.Length)
                { Input.Append(e.UnicodeChar); }
                else
                { Input.Insert(CursorPosition, e.UnicodeChar); }
                CursorPosition++;
                ColorizedInput.Clear();
                LastInput = DateTime.UtcNow.TimeOfDay.TotalSeconds;
                ShouldRender = true;
                return;
            }

            if (e.VirtualKeyCode == VirtualKeyCode.BACK)
            {
                if (Input.Length > 0 && CursorPosition > 0)
                {
                    CursorPosition--;
                    Input.Remove(CursorPosition, 1);
                    ColorizedInput.Clear();
                    LastInput = DateTime.UtcNow.TimeOfDay.TotalSeconds;
                    ForceClear = true;
                    ShouldRender = true;
                }
                return;
            }

            if (e.VirtualKeyCode == VirtualKeyCode.LEFT)
            {
                CursorPosition--;
                CursorPosition = Math.Clamp(CursorPosition, 0, Input.Length);
                ShouldRender = true;
                return;
            }

            if (e.VirtualKeyCode == VirtualKeyCode.RIGHT)
            {
                CursorPosition++;
                CursorPosition = Math.Clamp(CursorPosition, 0, Input.Length);
                ShouldRender = true;
                return;
            }

            if (e.VirtualKeyCode == VirtualKeyCode.UP)
            {
                CursorPosition = 0;
                ShouldRender = true;
                return;
            }

            if (e.VirtualKeyCode == VirtualKeyCode.DOWN)
            {
                CursorPosition = Input.Length;
                ShouldRender = true;
                return;
            }

            if (e.VirtualKeyCode == VirtualKeyCode.RETURN)
            {
                Entered = true;
                EnableInput = false;
                CursorPosition = 0;
                ForceClear = true;
                ShouldRender = true;
                return;
            }

            if (e.VirtualKeyCode == VirtualKeyCode.ESCAPE)
            {
                Escaped = true;
                EnableInput = false;
                ShouldRender = true;
                return;
            }
        }

        public void Run()
        {
            while (!Escaped)
            {
                {
                    bool blinker = Blinker;
                    if (lastBlinked != blinker)
                    {
                        lastBlinked = blinker;
                        ShouldRender = true;
                    }
                }

                if (EnableInput &&
                    ColorizedInput.ColorizedText == null &&
                    DateTime.UtcNow.TimeOfDay.TotalSeconds - LastInput > 1d &&
                    !ColorizedInput.FailedToColorize)
                {
                    CompileAndColorizeInput();
                    ShouldRender = true;
                }

                if (EnableInput &&
                    DateTime.UtcNow.TimeOfDay.TotalSeconds - LastInput > 1d &&
                    Input.Length == 0 &&
                    !string.IsNullOrEmpty(ColorizedInput.CurrentError))
                {
                    ColorizedInput = Colorized.Null;
                    ShouldRender = true;
                    ForceClear = true;
                }

                if (Entered)
                {
                    Evaluate(Input.ToString());
                    Input.Clear();
                    ColorizedInput = Colorized.Null;
                    Entered = false;
                    EnableInput = true;
                    ForceClear = true;
                }

                if (ShouldRender)
                {
                    ShouldRender = false;
                    Render();
                }

                System.Threading.Thread.Sleep(100);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConsoleListener.Stop();
            }
            Console.CursorVisible = true;
        }

        void Render()
        {
            Renderer.Clear();

            int line = 0;
            for (int i = 0; i < Sessions.Count; i++)
            {
                Renderer.Append(' ');
                Renderer.Append('>');
                Renderer.Append(' ');
                Renderer.Append(Sessions[i].Input);
                Renderer.AppendLine(); line++;
                Renderer.Append(Sessions[i].Output);
                Renderer.AppendLine(); line++;
            }

            if (EnableInput)
            {
                Renderer.Append(' ');
                Renderer.Append('>');
                Renderer.Append(' ');

                if (ColorizedInput.ColorizedText != null)
                {
                    if (Blinker)
                    {
                        ColorizedInput.Render(Renderer, CursorPosition);
                        if (CursorPosition == Input.Length)
                        { Renderer.Append('_'); }
                        else
                        { Renderer.Append(' '); }
                    }
                    else
                    { ColorizedInput.Render(Renderer); }
                }
                else
                {
                    if (Blinker)
                    {
                        RenderInput(Input, Renderer, CursorPosition);
                        if (CursorPosition == Input.Length)
                        { Renderer.Append('_'); }
                        else
                        { Renderer.Append(' '); }
                    }
                    else
                    { RenderInput(Input, Renderer); }
                }

                if (ColorizedInput.CurrentError != null)
                {
                    Renderer.AppendLine();
                    Renderer.Append(ColorizedInput.CurrentError);
                }
            }

            Console.CursorVisible = false;
            Console.SetCursorPosition(0, 0);

            if (ForceClear)
            {
                ForceClear = false;
                Console.Clear();
                LastRendererLength = Renderer.Length;
            }
            else
            {
                int l = Renderer.Length;
                if (l < LastRendererLength)
                { Renderer.Append(' ', LastRendererLength - l + 1); }
                LastRendererLength = l;
            }

            Console.Out.Write((StringBuilder)Renderer);
        }

        static void RenderInput(StringBuilder input, AnsiBuilder renderer, int cursorPosition)
        {
            for (int i = 0; i < input.Length; i++)
            {
                if (cursorPosition == i)
                { renderer.Underline = true; }
                renderer.Append(input[i]);
                if (cursorPosition == i)
                { renderer.Underline = false; }
            }
            renderer.ResetStyle();
        }

        static void RenderInput(StringBuilder input, AnsiBuilder renderer)
        {
            renderer.Append(input);
            renderer.ResetStyle();
        }

        void CompileAndColorizeInput()
        {
            TokenizerResult tokens = TokenizerResult.Empty;

            try
            {
                tokens = StringTokenizer.Tokenize(Input.ToString());

                if (tokens.Tokens.Length != 0)
                {
                    Statement statement = Parser.Parser.ParseInteractive(tokens);

                    Interpreter interpreter = new();
                    interpreter.Initialize();
                    Dictionary<string, ExternalFunctionBase> externalFunctions = interpreter.GenerateExternalFunctions();

                    CompilerResult compiled = Compiler.Compiler.CompileInteractive(
                        statement,
                        externalFunctions,
                        @"D:\Program Files\BBCodeProject\BBCode\CodeFiles\",
                        [Parser.UsingDefinition.CreateAnonymous("System")],
                        null);

                    BBCodeGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, CompilerSettings.Default, null, CompileLevel.Minimal);
                }

                ColorizedInput.ColorizedCharacterPositions = new int[Input.Length];
                ColorizedInput = Colorized.Success(ColorizeSource(Input.ToString(), tokens, ColorizedInput.ColorizedCharacterPositions), Input.Length);
            }
            catch (LanguageException ex)
            {
                AnsiBuilder output = new()
                { ForegroundColor = Color.FromArgb(int.Parse("fc3e36", NumberStyles.HexNumber)) };

                string? arrows = LanguageException.GetArrows(ex.Position, Input.ToString());

                if (arrows != null)
                {
                    output.Append(arrows);
                    output.AppendLine();
                }

                output.Append(ex.ToString());
                output.AppendLine();

                output.ResetStyle();

                ColorizedInput = Colorized.Error(output.ToString());
            }
            catch (Exception ex)
            {
                AnsiBuilder output = new()
                { ForegroundColor = Color.FromArgb(int.Parse("fc3e36", NumberStyles.HexNumber)) };

                output.Append(ex.Message);
                output.AppendLine();

                output.ResetStyle();

                ColorizedInput = Colorized.Error(output.ToString());
            }
        }

        static string ColorizeSource(string source, Token[] tokens, int[]? characterPositions = null)
        {
            AnsiBuilder result = new(source.Length);

            Color GetColorAt(int i)
            {
                for (int j = 0; j < tokens.Length; j++)
                {
                    if (tokens[j].Position.AbsoluteRange.Contains(i))
                    {
                        return tokens[j].AnalyzedType switch
                        {
                            TokenAnalyzedType.None => tokens[j].TokenType switch
                            {
                                TokenType.Whitespace => Color.White,
                                TokenType.LineBreak => Color.White,
                                TokenType.Identifier => Color.White,
                                TokenType.LiteralNumber or
                                TokenType.LiteralHex or TokenType.LiteralBinary or
                                TokenType.LiteralFloat => Color.FromArgb(int.Parse("b5cea8", NumberStyles.HexNumber)),
                                TokenType.LiteralString or
                                TokenType.LiteralCharacter => Color.FromArgb(int.Parse("d69d85", NumberStyles.HexNumber)),
                                TokenType.Operator => Color.FromArgb(int.Parse("b4b4b4", NumberStyles.HexNumber)),
                                TokenType.Comment or
                                TokenType.CommentMultiline => Color.FromArgb(int.Parse("57a64a", NumberStyles.HexNumber)),
                                _ => Color.White,
                            },
                            TokenAnalyzedType.Attribute or
                            TokenAnalyzedType.Type or
                            TokenAnalyzedType.Class => Color.FromArgb(int.Parse("4ec9b0", NumberStyles.HexNumber)),
                            TokenAnalyzedType.Struct => Color.FromArgb(int.Parse("86c691", NumberStyles.HexNumber)),
                            TokenAnalyzedType.Keyword or
                            TokenAnalyzedType.BuiltinType => Color.FromArgb(int.Parse("569cd6", NumberStyles.HexNumber)),
                            TokenAnalyzedType.FunctionName => Color.FromArgb(int.Parse("dcdcaa", NumberStyles.HexNumber)),
                            TokenAnalyzedType.FieldName => Color.FromArgb(int.Parse("dcdcdc", NumberStyles.HexNumber)),
                            TokenAnalyzedType.VariableName or
                            TokenAnalyzedType.ParameterName => Color.FromArgb(int.Parse("9cdcfe", NumberStyles.HexNumber)),
                            TokenAnalyzedType.Namespace => Color.White,
                            TokenAnalyzedType.Hash => Color.White,
                            TokenAnalyzedType.HashParameter => Color.White,
                            TokenAnalyzedType.Library => Color.White,
                            TokenAnalyzedType.Statement => Color.FromArgb(int.Parse("d8a0df", NumberStyles.HexNumber)),
                            TokenAnalyzedType.Enum => Color.FromArgb(int.Parse("b8d7a3", NumberStyles.HexNumber)),
                            TokenAnalyzedType.EnumMember => Color.FromArgb(int.Parse("dcdcdc", NumberStyles.HexNumber)),
                            TokenAnalyzedType.TypeParameter => Color.FromArgb(int.Parse("b8d7a3", NumberStyles.HexNumber)),
                            _ => Color.White,
                        };
                    }
                }

                return Color.White;
            }

            for (int i = 0; i < source.Length; i++)
            {
                result.ForegroundColor = GetColorAt(i);

                if (characterPositions != null)
                { characterPositions[i] = result.Length; }
                result.Append(source[i]);
            }

            result.ResetStyle();

            return result.ToString();
        }

        public void Evaluate(string? source)
        {
            source ??= string.Empty;

            Interpreter interpreter;
            TokenizerResult tokens = TokenizerResult.Empty;

            try
            {
                tokens = StringTokenizer.Tokenize(source);

                if (tokens.Tokens.Length == 0) return;

                Statement statement = Parser.Parser.ParseInteractive(tokens);

                interpreter = new();
                interpreter.Initialize();
                Dictionary<string, ExternalFunctionBase> externalFunctions = interpreter.GenerateExternalFunctions();

                CompilerResult compiled = Compiler.Compiler.CompileInteractive(
                    statement,
                    externalFunctions,
                    @"D:\Program Files\BBCodeProject\BBCode\CodeFiles\",
                    [Parser.UsingDefinition.CreateAnonymous("System")],
                    null);

                BBCodeGeneratorResult generated = CodeGeneratorForMain.Generate(compiled, CompilerSettings.Default, null, CompileLevel.Minimal);

                interpreter.CompilerResult = generated;

                interpreter.ExecuteProgram(generated.Code, BytecodeInterpreterSettings.Default);

                while (interpreter.IsExecutingCode)
                { interpreter.Update(); }
            }
            catch (LanguageException ex)
            {
                AnsiBuilder output = new()
                { ForegroundColor = Color.FromArgb(int.Parse("fc3e36", NumberStyles.HexNumber)) };

                string? arrows = LanguageException.GetArrows(ex.Position, source);

                if (arrows != null)
                {
                    output.Append(arrows);
                    output.AppendLine();
                }

                output.Append(ex.ToString());
                output.AppendLine();

                output.ResetStyle();

                Sessions.Add(new Session()
                {
                    Input = ColorizeSource(source, tokens),
                    Output = output.ToString(),
                });

                ShouldRender = true;
                return;
            }
            catch (Exception ex)
            {
                AnsiBuilder output = new()
                { ForegroundColor = Color.FromArgb(int.Parse("fc3e36", NumberStyles.HexNumber)) };

                output.Append(ex.ToString());
                output.AppendLine();

                output.ResetStyle();

                Sessions.Add(new Session()
                {
                    Input = ColorizeSource(source, tokens),
                    Output = output.ToString(),
                });

                ShouldRender = true;
                return;
            }

            if (interpreter.BytecodeInterpreter!.Memory.Stack.Count > 0)
            {
                DataItem exitCode = interpreter.BytecodeInterpreter!.Memory.Stack.Last;

                AnsiBuilder output = new();

                switch (exitCode.Type)
                {
                    case RuntimeType.UInt8:
                        output.ForegroundColor = Color.FromArgb(int.Parse("b5cea8", NumberStyles.HexNumber));
                        output.Append(exitCode.ValueUInt8);
                        break;
                    case RuntimeType.SInt32:
                        output.ForegroundColor = Color.FromArgb(int.Parse("b5cea8", NumberStyles.HexNumber));
                        output.Append(exitCode.ValueSInt32);
                        break;
                    case RuntimeType.Single:
                        output.ForegroundColor = Color.FromArgb(int.Parse("b5cea8", NumberStyles.HexNumber));
                        output.Append($"{exitCode.ValueSingle}f");
                        break;
                    case RuntimeType.UInt16:
                        output.ForegroundColor = Color.FromArgb(int.Parse("d69d85", NumberStyles.HexNumber));
                        output.Append($"'{exitCode.ValueUInt16}'");
                        break;
                    case RuntimeType.Null:
                    default:
                        output.ForegroundColor = Color.FromArgb(int.Parse("86c691", NumberStyles.HexNumber));
                        output.Append("null");
                        break;
                }

                output.ResetStyle();

                Sessions.Add(new Session()
                {
                    Input = ColorizeSource(source, tokens),
                    Output = output.ToString(),
                });
            }
            else
            {
                Sessions.Add(new Session()
                {
                    Input = ColorizeSource(source, tokens),
                    Output = string.Empty,
                });
            }
            ShouldRender = true;
        }
    }
}
