using System;
using System.Runtime.Serialization;

#nullable enable

namespace ProgrammingLanguage.Errors
{
    using System.Collections.Generic;
    using Core;

    #region Exception

    [Serializable]
    public class Exception : System.Exception
    {
        public Position Position => position;
        public readonly string? File;
        readonly Position position;

        protected Exception(string message, Position position, string? file) : base(message)
        {
            this.position = position;
            this.File = file;
        }
        public Exception(Error error)
            : this(error.Message, error.Position, error.File)
        { }

        public Exception(string message, System.Exception inner)
            : base(message, inner) { }
        protected Exception(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        public override string ToString()
        {
            if (Position.Start.Line == -1)
            {
                return Message;
            }

            if (Position.Start.Character == -1)
            {
                return $"{Message} (at line {Position.Start.Character}) {InnerException}";
            }

            return $"{Message} (at line {Position.Start.Line} and column {Position.Start.Character}) {InnerException}";
        }
    }

    [Serializable]
    public class CompilerException : Exception
    {
        public CompilerException(string message, Position position, string file) : base(message, position, file) { }
        public CompilerException(string message, string file) : base(message, Position.UnknownPosition, file) { }
        public CompilerException(string message) : base(message, Position.UnknownPosition, null) { }
        public CompilerException(string message, IThingWithPosition position, string file) : base(message, position.GetPosition(), file) { }

        protected CompilerException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    /// <summary> Thrown by the <see cref="BBCode.Tokenizer"/> </summary>
    [Serializable]
    public class TokenizerException : Exception
    {
        public TokenizerException(string message, Position position) : base(message, position, null) { }

        protected TokenizerException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    /// <summary> Thrown by the <see cref="BBCode.Parser.Parser"/> </summary>
    [Serializable]
    public class SyntaxException : Exception
    {
        public SyntaxException(string message, Position position) : base(message, position, null) { }
        public SyntaxException(string message, IThingWithPosition position) : base(message, position.GetPosition(), null) { }
        public SyntaxException(string message, IThingWithPosition position, string file) : base(message, position.GetPosition(), file) { }

        protected SyntaxException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    /// <summary> Thrown by the <see cref="Bytecode.BytecodeInterpreter"/> </summary>
    [Serializable]
    public class RuntimeException : Exception
    {
        internal Bytecode.Context? Context;
        BBCode.Compiler.DebugInfo[]? ContextDebugInfo;

        internal void FeedDebugInfo(BBCode.Compiler.DebugInfo[] DebugInfo)
        {
            if (!Context.HasValue) return;
            List<BBCode.Compiler.DebugInfo> contextDebugInfo = new();
            var context = Context.Value;
            for (int i = 0; i < DebugInfo.Length; i++)
            {
                BBCode.Compiler.DebugInfo item = DebugInfo[i];
                if (item.InstructionStart > context.CodePointer || item.InstructionEnd < context.CodePointer) continue;
                contextDebugInfo.Add(item);
            }
            ContextDebugInfo = contextDebugInfo.ToArray();
        }

        internal RuntimeException(string message)
            : base(message, Position.UnknownPosition, null) { }
        internal RuntimeException(string message, Bytecode.Context context)
            : base(message, Position.UnknownPosition, null)
        { this.Context = context; }
        protected RuntimeException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        internal RuntimeException(string message, System.Exception inner, Bytecode.Context context)
            : this(message, inner)
        { this.Context = context; }

        internal RuntimeException(string message, System.Exception inner)
            : base(message, inner) { }

        public override string ToString()
        {
            if (!Context.HasValue) return Message + " (no context)";
            Bytecode.Context context = Context.Value;

            string result = Message;
            result += $"\n Executed Instructions: {context.ExecutedInstructionCount}";
            result += $"\n Code Pointer: {context.CodePointer}";
            result += $"\n Call Stack:";
            if (context.RawCallStack.Length == 0) { result += " (callstack is empty)"; }
            else { result += "\n   " + string.Join("\n   ", context.CallStack); }
            if (ContextDebugInfo != null && ContextDebugInfo.Length > 0)
            {
                result += $"\n Position: {ContextDebugInfo[0].Position.ToMinString()}";
            }
            result += $"\n System Stack Trace:";
            if (StackTrace == null) { result += " (stacktrace is null)"; }
            else if (StackTrace.Length == 0) { result += " (stacktrace is empty)"; }
            else { result += "\n  " + string.Join("\n  ", StackTrace); }

            if (Context.HasValue)
            {
                result += $"\n Stack:";
                for (int i = 0; i < Context.Value.Stack.Count; i++)
                {
                    if (Context.Value.Stack[i].IsNull)
                    {
                        result += $"\n{i}\t null";
                    }
                    else
                    {
                        result += $"\n{i}\t {Context.Value.Stack[i].Type} {Context.Value.Stack[i].GetValue()} # {Context.Value.Stack[i].Tag}";
                    }
                }

                result += $"\n Code:";
                for (int offset = 0; offset < Context.Value.Code.Length; offset++)
                {
                    result += $"\n{offset + Context.Value.CodeSampleStart}\t {Context.Value.Code[offset]}";
                }
            }

            return result;
        }
    }

    [Serializable]
    public class UserException : RuntimeException
    {
        public readonly string Value;

        public UserException(string message, string value) : base(message)
        { this.Value = value; }
    }

    #endregion

    #region InternalException

    [Serializable]
    public class ImpossibleException : System.Exception
    {
        public ImpossibleException()
            : base("This should be impossible. WTF??? Bruh. Uh nuh :) °_° AAAAAAA") { }
        public ImpossibleException(string details)
            : base($"This should be impossible. ({details}) WTF??? Bruh. Uh nuh :) °_° AAAAAAA") { }
        protected ImpossibleException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    /// <summary> If this gets thrown away, it's a <b>big</b> problem. </summary>
    [Serializable]
    public class InternalException : System.Exception
    {
        public readonly string? File;

        public InternalException()
            : base() { }

        public InternalException(string message)
            : base(message) { }

        public InternalException(string message, System.Exception inner)
            : base(message, inner) { }

        public InternalException(string message, string file) : base(message)
        { this.File = file; }

        protected InternalException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }

    /// <summary> If this gets thrown away, it's a <b>big</b> problem. </summary>
    public class EndlessLoopException : InternalException
    {
        public EndlessLoopException() : base("Endless loop") { }
    }

    #endregion

    #region NotExceptionBut

    public class NotExceptionBut
    {
        public virtual Position Position => position;
        public readonly string Message;
        public readonly string? File;

        readonly Position position;

        protected NotExceptionBut(string message, Position position) : this(message, position, null)
        { }
        protected NotExceptionBut(string message, Position position, string? file)
        {
            this.Message = message;
            this.position = position;
            this.File = file;
        }

        public override string ToString()
        {
            if (position.Start.Line == -1)
            { return Message; }
            else if (position.Start.Character == -1)
            { return $"{Message} (at line {position.Start.Line})"; }
            else
            { return $"{Message} (at line {position.Start.Line} and column {position.Start.Character})"; }
        }
    }

    public class Warning : NotExceptionBut
    {
        public Warning(string message, Position position, string file)
            : base(message, position, file) { }
        public Warning(string message, IThingWithPosition? position, string file)
            : base(message, position?.GetPosition() ?? Position.UnknownPosition, file) { }
    }

    /// <summary> It's an exception, but not. </summary>
    public class Error : NotExceptionBut
    {
        public Error(string message, Position position)
            : base(message, position, null) { }
        public Error(string message, Position position, string file)
            : base(message, position, file) { }
        public Error(string message, IThingWithPosition position)
            : base(message, position.GetPosition(), null) { }
        public Error(string message, IThingWithPosition position, string file)
            : base(message, position.GetPosition(), file) { }

        public Exception ToException() => new(this);
    }

    public class Hint : NotExceptionBut
    {
        public Hint(string message, IThingWithPosition position, string file)
            : base(message, position.GetPosition(), file) { }
    }

    public class Information : NotExceptionBut
    {
        public Information(string message, IThingWithPosition position, string file)
            : base(message, position.GetPosition(), file) { }
    }

    #endregion
}

#nullable restore
