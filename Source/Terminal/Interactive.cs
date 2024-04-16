using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Win32;
using Win32.Console;
using Color = System.Drawing.Color;

namespace LanguageCore.Interactive;

using BBCode.Generator;
using Compiler;
using Parser;
using Parser.Statement;
using Runtime;
using Tokenizing;

readonly struct InteractiveSession
{
    public readonly string Input;
    public readonly string Output;
    public readonly string InterpreterStdOutput;

    public InteractiveSession(
        string input,
        string output,
        string interpreterStdOutput)
    {
        Input = input;
        Output = output;
        InterpreterStdOutput = interpreterStdOutput;
    }
}

struct ActiveInteractiveSession
{
    public string? ColorizedText;
    public string? CurrentError;
    public bool FailedToColorize;

    public AnsiBuilder InterpreterStandardOutput;

    public static ActiveInteractiveSession Null => new()
    {
        ColorizedText = null,
        FailedToColorize = false,
        CurrentError = null,
        InterpreterStandardOutput = new AnsiBuilder(),
    };

    public void Success(string colorized)
    {
        ColorizedText = colorized;
        FailedToColorize = false;
        CurrentError = null;
    }

    public void Error(string error)
    {
        ColorizedText = null;
        FailedToColorize = true;
        CurrentError = error;
    }

    public void Clear()
    {
        ColorizedText = null;
        FailedToColorize = false;
        InterpreterStandardOutput.Clear();
    }
}

class InteractiveCompiler
{
    string _text;
    ImmutableArray<Token> _tokens;
    Statement? _parsed;
    CompilerResult _compiled;
    BBCodeGeneratorResult _generated;
    Task? _task;
    readonly Action<Task> _onCompiledAsync;
    readonly Queue<Task> _completedTasks;

    public ImmutableArray<Token> Tokens => _tokens;
    public Statement? Statement => _parsed;
    public CompilerResult Compiled => _compiled;
    public BBCodeGeneratorResult Generated => _generated;
    public ParserResult InteractiveAST => new(
        Enumerable.Empty<Error>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<StructDefinition>(),
        Enumerable.Empty<UsingDefinition>(),
        Enumerable.Empty<CompileTag>(),
        _parsed != null ? [_parsed] :
        Enumerable.Empty<Statement>(),
        Enumerable.Empty<EnumDefinition>(),
        Enumerable.Empty<Token>(),
        Enumerable.Empty<Token>());

    public InteractiveCompiler(Action<Task> onCompiledAsync)
    {
        _text = string.Empty;
        _tokens = ImmutableArray<Token>.Empty;
        _parsed = null;
        _compiled = CompilerResult.Empty;
        _generated = default;
        _task = null;
        _onCompiledAsync = onCompiledAsync;
        _completedTasks = new Queue<Task>();
    }

    public void Compile(string text)
    {
        if (_parsed != null && string.Equals(text, _text))
        { return; }

        _text = text;
        _tokens = StringTokenizer.Tokenize(_text, PreprocessorVariables.Interactive).Tokens;
        _parsed = default;
        _compiled = default;
        _generated = default;

        if (_tokens.Length != 0)
        {
            _parsed = Parser.ParseStatement(_tokens, null);

            Dictionary<int, ExternalFunctionBase> externalFunctions = Interpreter.GetExternalFunctions();

            Statement parsed2 = _parsed;
            if (parsed2 is StatementWithValue statementWithValue)
            { parsed2 = new KeywordCall((Token)StatementKeywords.Return, new StatementWithValue[] { statementWithValue }); }

            _compiled = Compiler.CompileInteractive(
                parsed2,
                externalFunctions,
                new CompilerSettings() { BasePath = @"D:\Program Files\BBCodeProject\BBCode\StandardLibrary\" },
                [UsingDefinition.CreateAnonymous("System")],
                PreprocessorVariables.Interactive,
                null,
                null,
                null);

            _generated = CodeGeneratorForMain.Generate(_compiled, GeneratorSettings.Default);
        }
    }

    public void CompileAsync(string text)
    {
        if (_parsed != null && string.Equals(text, _text))
        { return; }

        if (_task != null && !_task.IsCompleted)
        { return; }

        _text = text;
        _task = Task.Run(CompileTask);
        _task.ContinueWith(OnTaskCompleted);
    }

    void OnTaskCompleted(Task task)
    {
        _completedTasks.Enqueue(task);
    }

    void CompileTask()
    {
        _tokens = StringTokenizer.Tokenize(_text, PreprocessorVariables.Interactive).Tokens;
        _parsed = default;
        _compiled = default;
        _generated = default;

        if (_tokens.Length != 0)
        {
            _parsed = Parser.ParseStatement(_tokens, null);

            Dictionary<int, ExternalFunctionBase> externalFunctions = Interpreter.GetExternalFunctions();

            Statement parsed2 = _parsed;
            if (parsed2 is StatementWithValue statementWithValue)
            { parsed2 = new KeywordCall((Token)StatementKeywords.Return, new StatementWithValue[] { statementWithValue }); }

            _compiled = Compiler.CompileInteractive(
                parsed2,
                externalFunctions,
                new CompilerSettings() { BasePath = @"D:\Program Files\BBCodeProject\BBCode\StandardLibrary\" },
                [UsingDefinition.CreateAnonymous("System")],
                PreprocessorVariables.Interactive,
                null,
                null,
                null);

            _generated = CodeGeneratorForMain.Generate(_compiled, GeneratorSettings.Default);
        }
    }

    public void Tick()
    {
        while (_completedTasks.TryDequeue(out Task? task))
        { _onCompiledAsync.Invoke(task); }
    }
}

static class InteractiveColors
{
    public static readonly Color Error = Color.FromArgb(int.Parse("fc3e36", NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    public static readonly Color Comment = Color.FromArgb(int.Parse("57a64a", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color LiteralNumber = Color.FromArgb(int.Parse("b5cea8", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color LiteralString = Color.FromArgb(int.Parse("d69d85", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color Operator = Color.FromArgb(int.Parse("b4b4b4", NumberStyles.HexNumber, CultureInfo.InvariantCulture));

    public static readonly Color Type = Color.FromArgb(int.Parse("4ec9b0", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color Struct = Color.FromArgb(int.Parse("86c691", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color Keyword = Color.FromArgb(int.Parse("569cd6", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color FunctionName = Color.FromArgb(int.Parse("dcdcaa", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color FieldName = Color.FromArgb(int.Parse("dcdcdc", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color LocalSymbol = Color.FromArgb(int.Parse("9cdcfe", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color Statement = Color.FromArgb(int.Parse("d8a0df", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color Enum = Color.FromArgb(int.Parse("b8d7a3", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color EnumMember = Color.FromArgb(int.Parse("dcdcdc", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    public static readonly Color TypeParameter = Color.FromArgb(int.Parse("b8d7a3", NumberStyles.HexNumber, CultureInfo.InvariantCulture));
}

public class Interactive
{
    readonly InteractiveCompiler CompilerCache;

    readonly AnsiBuilder Renderer;
    readonly StringBuilder Input;
    ActiveInteractiveSession CurrentSession;
    readonly List<InteractiveSession> Sessions;
    // int LastRendererLength;
    bool ForceClear;
    bool ShouldRender;

    double LastInput;

    bool Entered;
    bool Escaped;
    int CursorPosition;
    bool EnableInput;

    static bool Blinker => DateTime.UtcNow.TimeOfDay.TotalSeconds % 1d < 0.5d;
    bool LastBlinked;

    public Interactive()
    {
        CompilerCache = new InteractiveCompiler(OnCompiledAsync);
        Input = new StringBuilder();
        CursorPosition = 0;
        Entered = false;
        Escaped = false;
        EnableInput = true;
        Sessions = new List<InteractiveSession>();
        CurrentSession = ActiveInteractiveSession.Null;
        LastInput = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        // LastRendererLength = 0;
        ForceClear = false;
        ShouldRender = true;

        Console.CursorVisible = false;
        Console.Clear();

        Renderer = new AnsiBuilder();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ConsoleListener.Start();

            ConsoleListener.KeyEvent += OnKey;
            ConsoleListener.WindowBufferSizeEvent += OnWindowResize;
        }
    }

    void OnWindowResize(WindowBufferSizeEvent e)
    {
        ShouldRender = true;
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
            CurrentSession.Clear();
            LastInput = DateTime.UtcNow.TimeOfDay.TotalSeconds;
            ShouldRender = true;
            return;
        }

        if (e.VirtualKeyCode == VirtualKeyCode.Back)
        {
            if (Input.Length > 0 && CursorPosition > 0)
            {
                CursorPosition--;
                Input.Remove(CursorPosition, 1);
                CurrentSession.Clear();
                LastInput = DateTime.UtcNow.TimeOfDay.TotalSeconds;
                ForceClear = true;
                ShouldRender = true;
            }
            return;
        }

        if (e.VirtualKeyCode == VirtualKeyCode.Left)
        {
            CursorPosition--;
            CursorPosition = Math.Clamp(CursorPosition, 0, Input.Length);
            ShouldRender = true;
            return;
        }

        if (e.VirtualKeyCode == VirtualKeyCode.Right)
        {
            CursorPosition++;
            CursorPosition = Math.Clamp(CursorPosition, 0, Input.Length);
            ShouldRender = true;
            return;
        }

        if (e.VirtualKeyCode == VirtualKeyCode.Up)
        {
            CursorPosition = 0;
            ShouldRender = true;
            return;
        }

        if (e.VirtualKeyCode == VirtualKeyCode.Down)
        {
            CursorPosition = Input.Length;
            ShouldRender = true;
            return;
        }

        if (e.VirtualKeyCode == VirtualKeyCode.Return)
        {
            Entered = true;
            EnableInput = false;
            CursorPosition = 0;
            ForceClear = true;
            ShouldRender = true;
            return;
        }

        if (e.VirtualKeyCode == VirtualKeyCode.Escape)
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
                if (LastBlinked != blinker)
                {
                    LastBlinked = blinker;
                    ShouldRender = true;
                }
            }

            if (EnableInput &&
                CurrentSession.ColorizedText == null &&
                DateTime.UtcNow.TimeOfDay.TotalSeconds - LastInput > 1d &&
                !CurrentSession.FailedToColorize)
            {
                CompilerCache.Tick();
                CompileAndColorizeInput(CursorPosition);
                ShouldRender = true;
            }

            if (EnableInput &&
                DateTime.UtcNow.TimeOfDay.TotalSeconds - LastInput > 1d &&
                Input.Length == 0 &&
                !string.IsNullOrEmpty(CurrentSession.CurrentError))
            {
                CurrentSession = ActiveInteractiveSession.Null;
                ShouldRender = true;
                ForceClear = true;
            }

            if (Entered)
            {
                Evaluate(Input.ToString());
                Input.Clear();
                CurrentSession = ActiveInteractiveSession.Null;
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
            if (Sessions[i].InterpreterStdOutput.Length > 0)
            {
                Renderer.Append(Sessions[i].InterpreterStdOutput);
                Renderer.AppendLine(); line++;
            }
            Renderer.Append(Sessions[i].Output);
            Renderer.AppendLine(); line++;
        }

        if (EnableInput)
        {
            Renderer.Append(' ');
            Renderer.Append('>');
            Renderer.Append(' ');

            /*
            Token? token = compilerCache.Tokens?.GetTokenAt(CursorPosition);
            if (token != null)
            {
                bool searching = true;
                if (searching)
                {
                    foreach (CompiledFunction function in compilerCache.Compiled.Functions)
                    {
                        if (function.Identifier.Content.StartsWith(token.Content, StringComparison.OrdinalIgnoreCase))
                        {
                            Renderer.ForegroundColor = Colors.FunctionName;
                            Renderer.Append(function.Identifier.Content.AsSpan(token.Content.Length));
                            Renderer.ResetStyle();
                            searching = false;
                            break;
                        }
                    }
                }

                if (searching)
                {
                    foreach (MacroDefinition macro in compilerCache.Compiled.Macros)
                    {
                        if (macro.Identifier.Content.StartsWith(token.Content, StringComparison.OrdinalIgnoreCase))
                        {
                            Renderer.ForegroundColor = Colors.FunctionName;
                            Renderer.Append(macro.Identifier.Content.AsSpan(token.Content.Length));
                            Renderer.ResetStyle();
                            searching = false;
                            break;
                        }
                    }
                }
            }
            */

            CompileAndColorizeInput(CursorPosition);

            if (CurrentSession.ColorizedText != null)
            {
                Renderer.Append(CurrentSession.ColorizedText);
                Renderer.ResetStyle();

                if (Blinker)
                {
                    if (CursorPosition == Input.Length)
                    { Renderer.Append('_'); }
                    else
                    { Renderer.Append(' '); }
                }
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

            if (CurrentSession.CurrentError != null)
            {
                Renderer.AppendLine();
                Renderer.Append(CurrentSession.CurrentError);
            }
            else
            {
                Renderer.AppendLine();
                Renderer.Append(CurrentSession.InterpreterStandardOutput);
            }
        }

        Console.CursorVisible = false;
        Console.SetCursorPosition(0, 0);

        if (ForceClear)
        {
            ForceClear = false;
            Console.Clear();
            // LastRendererLength = Renderer.Length;
        }
        else
        {
            // int l = Renderer.Length;
            // if (l < LastRendererLength)
            // { Renderer.Append(' ', LastRendererLength - l + 1); }
            // LastRendererLength = l;
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

    void OnCompiledAsync(Task task)
    {
        if (task.IsFaulted)
        {
            Exception ex = task.Exception.GetBaseException();

            AnsiBuilder output = new()
            { ForegroundColor = InteractiveColors.Error };

            if (ex is LanguageException languageException)
            {
                string? arrows = LanguageException.GetArrows(languageException.Position, Input.ToString());

                if (arrows != null)
                {
                    output.Append(arrows);
                    output.AppendLine();
                }

                output.Append(languageException.ToString());
            }
            else
            {
                output.Append(ex.Message);
            }

            output.AppendLine();
            output.ResetStyle();

            CurrentSession.Error(output.ToString());
        }
        else
        {
            CurrentSession.Success(ColorizeSource(Input.ToString(), CompilerCache.Tokens));
        }
    }

    void CompileAndColorizeInput(int _ = -1)
    {
        CompilerCache.CompileAsync(Input.ToString());
        /*
        try
        {
            CompilerCache.Compile(Input.ToString());

            CurrentSession.Success(ColorizeSource(Input.ToString(), CompilerCache.Tokens, cursorPosition));
        }
        catch (LanguageException ex)
        {
            AnsiBuilder output = new()
            { ForegroundColor = InteractiveColors.Error };

            string? arrows = LanguageException.GetArrows(ex.Position, Input.ToString());

            if (arrows != null)
            {
                output.Append(arrows);
                output.AppendLine();
            }

            output.Append(ex.ToString());
            output.AppendLine();

            output.ResetStyle();

            CurrentSession.Error(output.ToString());
        }
        catch (Exception ex)
        {
            AnsiBuilder output = new()
            { ForegroundColor = InteractiveColors.Error };

            output.Append(ex.Message);
            output.AppendLine();

            output.ResetStyle();

            CurrentSession.Error(output.ToString());
        }
        */
    }

    static string ColorizeSource(string source, IReadOnlyList<Token>? tokens, int cursorPosition = -1)
    {
        if (tokens == null) return source;

        AnsiBuilder result = new(source.Length);

        Color GetColorAt(int i)
        {
            for (int j = 0; j < tokens.Count; j++)
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
                            TokenType.LiteralFloat => InteractiveColors.LiteralNumber,
                            TokenType.LiteralString or
                            TokenType.LiteralCharacter => InteractiveColors.LiteralString,
                            TokenType.Operator => InteractiveColors.Operator,
                            TokenType.Comment or
                            TokenType.CommentMultiline => InteractiveColors.Comment,
                            _ => Color.White,
                        },
                        TokenAnalyzedType.Attribute or
                        TokenAnalyzedType.Type or
                        TokenAnalyzedType.Struct => InteractiveColors.Struct,
                        TokenAnalyzedType.Keyword or
                        TokenAnalyzedType.BuiltinType => InteractiveColors.Keyword,
                        TokenAnalyzedType.FunctionName => InteractiveColors.FunctionName,
                        TokenAnalyzedType.FieldName => InteractiveColors.FieldName,
                        TokenAnalyzedType.VariableName or
                        TokenAnalyzedType.ParameterName => InteractiveColors.LocalSymbol,
                        TokenAnalyzedType.CompileTag => Color.White,
                        TokenAnalyzedType.CompileTagParameter => Color.White,
                        TokenAnalyzedType.Statement => InteractiveColors.Statement,
                        TokenAnalyzedType.Enum => InteractiveColors.Enum,
                        TokenAnalyzedType.EnumMember => InteractiveColors.EnumMember,
                        TokenAnalyzedType.TypeParameter => InteractiveColors.TypeParameter,
                        _ => Color.White,
                    };
                }
            }

            return Color.White;
        }

        for (int i = 0; i < source.Length; i++)
        {
            result.ForegroundColor = GetColorAt(i);
            if (cursorPosition == i)
            { result.Underline = true; }
            result.Append(source[i]);
            result.Underline = false;
        }

        result.ResetStyle();

        return result.ToString();
    }

    public void Evaluate(string? source)
    {
        source ??= string.Empty;

        Interpreter interpreter;

        try
        {
            CompilerCache.Compile(Input.ToString());

            if (CompilerCache.Tokens.IsEmpty) return;

            Dictionary<int, ExternalFunctionBase> externalFunctions = Interpreter.GetExternalFunctions();

            BBCodeGeneratorResult generated = CodeGeneratorForMain.Generate(CompilerCache.Compiled, GeneratorSettings.Default);

            interpreter = new(true, BytecodeInterpreterSettings.Default, generated.Code, generated.DebugInfo);

            interpreter.OnStdOut += OnInterpreterStandardOut;
            interpreter.OnStdError += OnInterpreterStandardError;
            interpreter.OnOutput += OnInterpreterOutput;
            interpreter.OnNeedInput += OnInterpreterNeedInput;

            while (!interpreter.BytecodeInterpreter.IsDone)
            { interpreter.Update(); }
        }
        catch (LanguageException ex)
        {
            AnsiBuilder output = new()
            { ForegroundColor = InteractiveColors.Error };

            string? arrows = LanguageException.GetArrows(ex.Position, source);

            if (arrows != null)
            {
                output.Append(arrows);
                output.AppendLine();
            }

            output.Append(ex.ToString());
            output.AppendLine();

            output.ResetStyle();

            CurrentSession.InterpreterStandardOutput.ResetStyle();
            Sessions.Add(new InteractiveSession(
                ColorizeSource(source, CompilerCache.Tokens),
                output.ToString(),
                CurrentSession.InterpreterStandardOutput.ToString()));

            ShouldRender = true;
            return;
        }
        catch (Exception ex)
        {
            AnsiBuilder output = new()
            { ForegroundColor = InteractiveColors.Error };

            output.Append(ex.ToString());
            output.AppendLine();

            output.ResetStyle();

            CurrentSession.InterpreterStandardOutput.ResetStyle();
            Sessions.Add(new InteractiveSession(
                ColorizeSource(source, CompilerCache.Tokens),
                output.ToString(),
                CurrentSession.InterpreterStandardOutput.ToString()));

            ShouldRender = true;
            return;
        }

        if (interpreter.BytecodeInterpreter.Memory.Length > 0)
        {
            DataItem exitCode = interpreter.BytecodeInterpreter.Memory[interpreter.BytecodeInterpreter.Registers.StackPointer - 1];

            AnsiBuilder output = new();

            switch (exitCode.Type)
            {
                case RuntimeType.Byte:
                    output.ForegroundColor = InteractiveColors.LiteralNumber;
                    output.Append(exitCode.VByte);
                    break;
                case RuntimeType.Integer:
                    output.ForegroundColor = InteractiveColors.LiteralNumber;
                    output.Append(exitCode.VInt);
                    break;
                case RuntimeType.Single:
                    output.ForegroundColor = InteractiveColors.LiteralNumber;
                    output.Append($"{exitCode.VSingle}f");
                    break;
                case RuntimeType.Char:
                    output.ForegroundColor = InteractiveColors.LiteralString;
                    output.Append($"'{exitCode.VChar}'");
                    break;
                case RuntimeType.Null:
                default:
                    output.ForegroundColor = InteractiveColors.Keyword;
                    output.Append("null");
                    break;
            }

            output.ResetStyle();

            CurrentSession.InterpreterStandardOutput.ResetStyle();
            Sessions.Add(new InteractiveSession(
                ColorizeSource(source, CompilerCache.Tokens),
                output.ToString(),
                CurrentSession.InterpreterStandardOutput.ToString()));
        }
        else
        {
            CurrentSession.InterpreterStandardOutput.ResetStyle();
            Sessions.Add(new InteractiveSession(
                ColorizeSource(source, CompilerCache.Tokens),
                string.Empty,
                CurrentSession.InterpreterStandardOutput.ToString()));
        }
        ShouldRender = true;
    }

    void OnInterpreterNeedInput(Interpreter sender) { throw new NotImplementedException(); }
    void OnInterpreterOutput(Interpreter sender, string message, LogType logType) { }
    void OnInterpreterStandardError(Interpreter sender, char data)
    {
        CurrentSession.InterpreterStandardOutput.ForegroundColor = InteractiveColors.Error;
        CurrentSession.InterpreterStandardOutput.Append(data);
    }
    void OnInterpreterStandardOut(Interpreter sender, char data)
    {
        CurrentSession.InterpreterStandardOutput.ForegroundColor = Color.Silver;
        CurrentSession.InterpreterStandardOutput.Append(data);
    }
}
