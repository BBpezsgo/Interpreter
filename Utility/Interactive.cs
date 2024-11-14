using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Win32;
using Win32.Console;
using Color = Win32.Gdi32.GdiColor;

namespace LanguageCore.Interactive;

using BBLang.Generator;
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
    BBLangGeneratorResult _generated;
    Task? _task;
    readonly Action<Task> _onCompiledAsync;
    readonly Queue<Task> _completedTasks;
    readonly string? _basePath;

    public ImmutableArray<Token> Tokens { get; private set; }
    public Statement? Statement { get; private set; }
    public CompilerResult Compiled { get; private set; }
    public BBLangGeneratorResult Generated => _generated;
    public ParserResult InteractiveAST => new(
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<FunctionDefinition>(),
        Enumerable.Empty<StructDefinition>(),
        Enumerable.Empty<UsingDefinition>(),
        Enumerable.Empty<AliasDefinition>(),
        Statement != null ? [Statement] :
        Enumerable.Empty<Statement>(),
        Enumerable.Empty<Token>(),
        Enumerable.Empty<Token>());

    public InteractiveCompiler(Action<Task> onCompiledAsync)
    {
        _text = string.Empty;
        Tokens = ImmutableArray<Token>.Empty;
        Statement = null;
        Compiled = CompilerResult.MakeEmpty(Utils.AssemblyFile);
        _generated = default;
        _task = null;
        _onCompiledAsync = onCompiledAsync;
        _completedTasks = new Queue<Task>();
        _basePath = null;
    }

    public void Compile(string text)
    {
        if (Statement != null && string.Equals(text, _text))
        { return; }

        _text = text;
        Tokens = StringTokenizer.Tokenize(_text, new(), PreprocessorVariables.Interactive, Utils.AssemblyFile).Tokens;
        Statement = default;
        Compiled = default;
        _generated = default;
        DiagnosticsCollection diagnostics = new();

        if (Tokens.Length != 0)
        {
            Statement = Parser.ParseStatement(Tokens, Utils.AssemblyFile, diagnostics);

            List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

            Statement parsed2 = Statement;
            if (parsed2 is StatementWithValue statementWithValue)
            { parsed2 = new KeywordCall((Token)StatementKeywords.Return, ImmutableArray.Create<StatementWithValue>(statementWithValue), statementWithValue.File); }

            Compiled = Compiler.CompileInteractive(
                parsed2,
                externalFunctions,
                new CompilerSettings() { BasePath = _basePath },
                PreprocessorVariables.Interactive,
                null,
                diagnostics,
                null,
                Utils.AssemblyFile);

            _generated = CodeGeneratorForMain.Generate(
                Compiled,
                MainGeneratorSettings.Default,
                null,
                diagnostics);
        }
    }

    public void StartCompilation(string text)
    {
        if (Statement != null && string.Equals(text, _text))
        { return; }

        if (_task != null && !_task.IsCompleted)
        { return; }

        _text = text;
        _task = Task.Run(CompileTask);
        _task.ContinueWith(_completedTasks.Enqueue);
    }

    void CompileTask()
    {
        Tokens = StringTokenizer.Tokenize(_text, new(), PreprocessorVariables.Interactive, Utils.AssemblyFile).Tokens;
        Statement = default;
        Compiled = default;
        _generated = default;
        DiagnosticsCollection diagnostics = new();

        if (Tokens.Length != 0)
        {
            Statement = Parser.ParseStatement(Tokens, Utils.AssemblyFile, diagnostics);

            List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

            Statement parsed2 = Statement;
            if (parsed2 is StatementWithValue statementWithValue)
            { parsed2 = new KeywordCall((Token)StatementKeywords.Return, ImmutableArray.Create<StatementWithValue>(statementWithValue), statementWithValue.File); }

            Compiled = Compiler.CompileInteractive(
                parsed2,
                externalFunctions,
                new CompilerSettings() { BasePath = _basePath },
                PreprocessorVariables.Interactive,
                null,
                diagnostics,
                null,
                Utils.AssemblyFile);

            _generated = CodeGeneratorForMain.Generate(
                Compiled,
                MainGeneratorSettings.Default,
                null,
                diagnostics);
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
    public static readonly Color Error = Color.ParseHex("fc3e36");

    public static readonly Color Comment = Color.ParseHex("57a64a");
    public static readonly Color LiteralNumber = Color.ParseHex("b5cea8");
    public static readonly Color LiteralString = Color.ParseHex("d69d85");
    public static readonly Color Operator = Color.ParseHex("b4b4b4");

    public static readonly Color Type = Color.ParseHex("4ec9b0");
    public static readonly Color Struct = Color.ParseHex("86c691");
    public static readonly Color Keyword = Color.ParseHex("569cd6");
    public static readonly Color FunctionName = Color.ParseHex("dcdcaa");
    public static readonly Color FieldName = Color.ParseHex("dcdcdc");
    public static readonly Color LocalSymbol = Color.ParseHex("9cdcfe");
    public static readonly Color Statement = Color.ParseHex("d8a0df");
    public static readonly Color TypeParameter = Color.ParseHex("b8d7a3");
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
        CompilerCache = new InteractiveCompiler(OnCompiled);
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

        if (e.UnicodeChar is (>= '!' and <= '~') or ' ')
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

    void OnCompiled(Task task)
    {
        if (task.IsFaulted)
        {
            Exception ex = task.Exception.GetBaseException();

            AnsiBuilder output = new()
            { ForegroundColor = InteractiveColors.Error };

            if (ex is LanguageException languageException)
            {
                (string SourceCode, string Arrows)? arrows = LanguageException.GetArrows(languageException.Position, Input.ToString());

                if (arrows.HasValue)
                {
                    output.Append(arrows.Value.SourceCode);
                    output.AppendLine();
                    output.Append(arrows.Value.Arrows);
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
        CompilerCache.StartCompilation(Input.ToString());
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

    static string ColorizeSource(string source, ImmutableArray<Token> tokens, int cursorPosition = -1)
    {
        if (tokens.IsDefaultOrEmpty) return source;

        AnsiBuilder result = new(source.Length);

        Color GetColorAt(int i)
        {
            for (int j = 0; j < tokens.Length; j++)
            {
                Range<int> range = tokens[j].Position.AbsoluteRange;
                if (range.Start <= i && range.End > i)
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

        BytecodeProcessorEx interpreter;
        DiagnosticsCollection diagnostics = new();

        try
        {
            CompilerCache.Compile(Input.ToString());

            if (CompilerCache.Tokens.IsEmpty) return;

            List<IExternalFunction> externalFunctions = BytecodeProcessorEx.GetExternalFunctions();

            BBLangGeneratorResult generated = CodeGeneratorForMain.Generate(
                CompilerCache.Compiled,
                MainGeneratorSettings.Default,
                null,
                diagnostics);

            interpreter = new(BytecodeInterpreterSettings.Default, generated.Code, null, generated.DebugInfo);

            interpreter.IO.OnStdOut += OnInterpreterStandardOut;
            interpreter.IO.OnNeedInput += OnInterpreterNeedInput;

            while (!interpreter.Processor.IsDone)
            { interpreter.Tick(); }
        }
        catch (LanguageException ex)
        {
            AnsiBuilder output = new()
            { ForegroundColor = InteractiveColors.Error };

            (string SourceCode, string Arrows)? arrows = LanguageException.GetArrows(ex.Position, source);

            if (arrows.HasValue)
            {
                output.Append(arrows.Value.SourceCode);
                output.AppendLine();
                output.Append(arrows.Value.Arrows);
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

        {
            int exitCode = interpreter.Processor.Memory.AsSpan().Get<int>(interpreter.Processor.Registers.StackPointer - (1 * BytecodeProcessor.StackDirection));

            AnsiBuilder output = new();

            output.ForegroundColor = InteractiveColors.LiteralNumber;
            output.Append(exitCode);

            output.ResetStyle();

            CurrentSession.InterpreterStandardOutput.ResetStyle();
            Sessions.Add(new InteractiveSession(
                ColorizeSource(source, CompilerCache.Tokens),
                output.ToString(),
                CurrentSession.InterpreterStandardOutput.ToString()
            ));

            ShouldRender = true;
        }
    }

    void OnInterpreterNeedInput() { throw new NotImplementedException(); }
    void OnInterpreterStandardOut(char data)
    {
        CurrentSession.InterpreterStandardOutput.ForegroundColor = Color.Silver;
        CurrentSession.InterpreterStandardOutput.Append(data);
    }
}
