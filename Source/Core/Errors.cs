using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace LanguageCore
{
    using Runtime;

    #region Exception

    public class LanguageException : Exception
    {
        public readonly Position Position;
        public readonly Uri? Uri;

        public override string? StackTrace
        {
            get
            {
                if (_capturedStackTrace != null) return _capturedStackTrace.ToString();
                return base.StackTrace;
            }
        }

        readonly System.Diagnostics.StackTrace? _capturedStackTrace;

        protected LanguageException(string message, Position position) : base(message)
        {
            this.Position = position;
            this.Uri = null;
        }

        protected LanguageException(string message, Position position, Uri? uri) : base(message)
        {
            this.Position = position;
            this.Uri = uri;
        }

        public LanguageException(Error error) : this(error.Message, error.Position, error.File)
        {
            _capturedStackTrace = error.StackTrace;
        }

        public LanguageException(string message, Exception inner) : base(message, inner) { }

        public override string ToString()
        {
            StringBuilder result = new(Message);

            result.Append(Position.ToStringCool(" (at ", ")"));

            if (Uri != null)
            { result.Append(CultureInfo.InvariantCulture, $" (in {Uri})"); }

            if (InnerException != null)
            { result.Append(CultureInfo.InvariantCulture, $" {InnerException}"); }

            return result.ToString();
        }

        public string? GetArrows()
        {
            if (Uri == null) return null;
            if (!Uri.IsFile) return null;
            return GetArrows(Position, System.IO.File.ReadAllText(Uri.LocalPath));
        }

        public static string? GetArrows(Position position, string text)
        {
            if (position.AbsoluteRange == 0) return null;
            if (position == Position.UnknownPosition) return null;
            if (position.Range.Start.Line != position.Range.End.Line)
            { return null; }

            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

            if (position.Range.Start.Line >= lines.Length)
            { return null; }

            string line = lines[position.Range.Start.Line];

            StringBuilder result = new();

            result.Append(line.Replace('\t', ' '));
            result.AppendLine();
            result.Append(' ', Math.Max(0, position.Range.Start.Character));
            result.Append('^', Math.Max(1, position.Range.End.Character - position.Range.Start.Character));
            return result.ToString();
        }
    }

    public class CompilerException : LanguageException
    {
        public CompilerException(string message) : base(message, Position.UnknownPosition) { }
        public CompilerException(string message, Uri? uri) : base(message, Position.UnknownPosition, uri) { }
        public CompilerException(string message, Position position) : base(message, position) { }
        public CompilerException(string message, Position position, Uri? uri) : base(message, position, uri) { }
        public CompilerException(string message, IPositioned? position) : base(message, position?.Position ?? Position.UnknownPosition) { }
        public CompilerException(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
    }

    public class NotSupportedException : CompilerException
    {
        public NotSupportedException(string message) : base(message) { }
        public NotSupportedException(string message, Uri? uri) : base(message, uri) { }
        public NotSupportedException(string message, Position position, Uri? file) : base(message, position, file) { }
        public NotSupportedException(string message, IPositioned? position, Uri? uri) : base(message, position, uri) { }
    }

    public class TokenizerException : LanguageException
    {
        public TokenizerException(string message, Position position) : base(message, position) { }
    }

    public class SyntaxException : LanguageException
    {
        public SyntaxException(string message, Position position) : base(message, position) { }
        public SyntaxException(string message, Position? position) : base(message, position ?? Position.UnknownPosition) { }
        public SyntaxException(string message, IPositioned? position) : base(message, position?.Position ?? Position.UnknownPosition) { }
        public SyntaxException(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
    }

    public class ProcessRuntimeException : Exception
    {
        readonly uint exitCode;
        public uint ExitCode => exitCode;

        ProcessRuntimeException(uint exitCode, string message) : base(message)
        {
            this.exitCode = exitCode;
        }

        public static bool TryGetFromExitCode(int exitCode, [NotNullWhen(true)] out ProcessRuntimeException? processRuntimeException)
            => ProcessRuntimeException.TryGetFromExitCode(unchecked((uint)exitCode), out processRuntimeException);

        public static bool TryGetFromExitCode(uint exitCode, [NotNullWhen(true)] out ProcessRuntimeException? processRuntimeException)
        {
            processRuntimeException = exitCode switch
            {
                0xC0000094 => new ProcessRuntimeException(exitCode, "Integer division by zero"),
                _ => null,
            };
            return processRuntimeException != null;
        }
    }

    public class RuntimeException : LanguageException
    {
        public RuntimeContext? Context;
        public Position SourcePosition;
        public Uri? SourceFile;
        public FunctionInformations[]? CallStack;
        public FunctionInformations? CurrentFrame;

        public void FeedDebugInfo(DebugInformation debugInfo)
        {
            if (!Context.HasValue) return;
            RuntimeContext context = Context.Value;

            if (!debugInfo.TryGetSourceLocation(context.CodePointer, out SourceCodeLocation sourcePosition))
            { SourcePosition = Position.UnknownPosition; }
            else
            { SourcePosition = sourcePosition.SourcePosition; }

            CurrentFrame = debugInfo.GetFunctionInformations(context.CodePointer);

            CallStack = debugInfo.GetFunctionInformations(context.CallTrace);

            SourceFile = CallStack.Length > 0 ? CallStack[^1].File : null;
        }

        public RuntimeException(string message) : base(message, Position.UnknownPosition) { }
        public RuntimeException(string message, Exception inner) : base(message, inner) { }
        public RuntimeException(string message, RuntimeContext context) : base(message, Position.UnknownPosition)
        {
            this.Context = context;
        }
        public RuntimeException(string message, Exception inner, RuntimeContext context) : this(message, inner)
        {
            this.Context = context;
        }

        public override string ToString()
        {
            if (!Context.HasValue) return Message + " (no context)";
            RuntimeContext context = Context.Value;

            StringBuilder result = new(Message);

            result.Append(SourcePosition.ToStringCool(" (at ", ")") ?? string.Empty);

            if (SourceFile != null)
            { result.Append(CultureInfo.InvariantCulture, $" (in {SourceFile})"); }

            result.Append(Environment.NewLine);
            result.Append($"Code Pointer: ");
            result.Append(context.CodePointer);

            result.Append(Environment.NewLine);
            result.Append("Call Stack:");
            if (context.CallTrace.Length == 0)
            { result.Append(" (CallTrace is empty)"); }
            else
            {
                result.AppendLine();
                result.Append('\t');
                result.Append(' ');
                if (CallStack == null)
                { result.Append(string.Join("\n   ", context.CallTrace)); }
                else
                { result.Append(string.Join("\n   ", CallStack)); }
            }

            if (CurrentFrame.HasValue)
            {
                result.AppendLine();
                result.Append('\t');
                result.Append(' ');
                result.Append(CurrentFrame.Value.ToString());
                result.Append(" (current)");
            }

            /*
            result.Append(Environment.NewLine);
            result.Append("System Stack Trace:");
            if (StackTrace == null) { result.Append(" (StackTrace is null)"); }
            else if (StackTrace.Length == 0) { result.Append(" (StackTrace is empty)"); }
            else { result.Append("\n  " + string.Join("\n  ", StackTrace)); }

            result.Append(Environment.NewLine);
            result.Append("Stack:");
            for (int i = 0; i < context.Stack.Count; i++)
            {
                if (context.Stack[i].IsNull)
                { result.Append($"{Environment.NewLine}{i}\t null"); }
                else
                { result.Append($"{Environment.NewLine}{i}\t {context.Stack[i].Type} {context.Stack[i].GetValue()}"); }
            }

            result.Append(Environment.NewLine);
            result.Append("Code:");
            for (int offset = 0; offset < context.Code.Length; offset++)
            { result.Append($"{Environment.NewLine}{offset + context.CodeSampleStart}\t {context.Code[offset]}"); }
            */

            return result.ToString();
        }
    }

    public class UserException : RuntimeException
    {
        public UserException(string message) : base(message)
        { }

        public override string ToString()
        {
            if (!Context.HasValue) return Message + " (no context)";
            RuntimeContext context = Context.Value;

            StringBuilder result = new(Message);

            result.Append(SourcePosition.ToStringCool(" (at ", ")") ?? string.Empty);

            if (SourceFile != null)
            {
                result.Append(CultureInfo.InvariantCulture, $" (in {SourceFile})");
            }

            if (context.CallTrace.Length != 0)
            {
                result.AppendLine();
                result.Append('\t');
                result.Append(' ');
                if (CallStack == null)
                { result.Append(string.Join("\n   ", context.CallTrace)); }
                else
                { result.Append(string.Join("\n   ", CallStack)); }
            }

            if (CurrentFrame.HasValue)
            {
                result.AppendLine();
                result.Append('\t');
                result.Append(' ');
                result.Append(CurrentFrame.Value.ToString());
                result.Append(" (current)");
            }

            return result.ToString();
        }
    }

    #endregion

    #region InternalException

    /// <summary> If this exception raised, it's a <b>big</b> problem. </summary>
    public class InternalException : LanguageException
    {
        public InternalException() : base(string.Empty, Position.UnknownPosition) { }
        public InternalException(string message) : base(message, Position.UnknownPosition) { }
        public InternalException(string message, Uri? uri) : base(message, Position.UnknownPosition, uri) { }
        public InternalException(string message, IPositioned position, Uri? uri) : base(message, position.Position, uri) { }
    }

    /// <inheritdoc/>
    public class EndlessLoopException : InternalException
    {
        public EndlessLoopException() : base("Endless loop") { }
    }

    #endregion

    #region NotExceptionBut

    public class NotExceptionBut
    {
        public readonly string Message;
        public readonly Position Position;
        public readonly Uri? File;

        public readonly System.Diagnostics.StackTrace StackTrace;

        protected NotExceptionBut(string message, Position position)
        {
            this.Message = message;
            this.Position = position;
            this.File = null;
            this.StackTrace = new System.Diagnostics.StackTrace(0, true);
        }
        protected NotExceptionBut(string message, Position position, Uri? file) : this(message, position)
        {
            this.File = file;
        }

        public string? GetArrows()
        {
            if (File == null) return null;
            if (!File.IsFile) return null;
            return LanguageException.GetArrows(Position, System.IO.File.ReadAllText(File.LocalPath));
        }

        public override string ToString()
        {
            StringBuilder result = new(Message);

            if (Position.Range.Start.Line == -1)
            { }
            else if (Position.Range.Start.Character == -1)
            { result.Append(CultureInfo.InvariantCulture, $" (at line {Position.Range.Start.Character})"); }
            else
            { result.Append(CultureInfo.InvariantCulture, $" (at line {Position.Range.Start.Line} and column {Position.Range.Start.Character})"); }

            if (File != null)
            { result.Append(CultureInfo.InvariantCulture, $" (in {File})"); }

            return result.ToString();
        }
    }

    public class Warning : NotExceptionBut
    {
        public Warning(string message, Position position, Uri? uri) : base(message, position, uri) { }
        public Warning(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
    }

    /// <summary> It's an exception, but not. </summary>
    public class Error : NotExceptionBut
    {
        public Error(string message, Uri uri) : base(message, Position.UnknownPosition, uri) { }

        public Error(string message, Position position) : base(message, position) { }
        public Error(string message, Position position, Uri? uri) : base(message, position, uri) { }

        public Error(string message, IPositioned? position) : base(message, position?.Position ?? Position.UnknownPosition) { }
        public Error(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }

        public LanguageException ToException() => new(this);
    }

    public class Hint : NotExceptionBut
    {
        public Hint(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
    }

    public class Information : NotExceptionBut
    {
        public Information(string message, IPositioned? position, Uri? uri) : base(message, position?.Position ?? Position.UnknownPosition, uri) { }
    }

    #endregion
}
