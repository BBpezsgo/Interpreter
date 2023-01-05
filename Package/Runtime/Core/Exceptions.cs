using System;
using System.Runtime.Serialization;

#pragma warning disable CS0618 // Type or member is obsolete

namespace IngameCoding.Errors
{
    using Core;
    using Tokenizer;

    #region Exception

    [Serializable]
    public class Exception : System.Exception
    {
        public Position Position => position;
        public readonly string File;
        public string MessageAll
        {
            get
            {
                if (Position.Start.Line == -1)
                {
                    return Message;
                }
                else if (Position.Start.Character == -1)
                {
                    return $"{Message} at line {Position.Start.Character}";
                }
                else
                {
                    return $"{Message} at line {Position.Start.Line} at col {Position.Start.Character}";
                }
            }
        }

        readonly Position position;

        [Obsolete("Don't use this", true)]
        public Exception() { }

        [Obsolete("Don't use this", false)]
        public Exception(string message, Position pos) : base(message)
        {
            this.position = pos;
        }
        [Obsolete("Don't use this", false)]
        public Exception(string message, BaseToken token) : this(message, token.GetPosition()) { }
        [Obsolete("Don't use this", false)]
        public Exception(string message, Position pos, string file) : base(message)
        {
            this.position = pos;
            this.File = file;
        }
        [Obsolete("Don't use this", false)]
        public Exception(string message, BaseToken token, string file) : this(message, token.GetPosition(), file) { }

        public Exception(string message, System.Exception inner) : base(message, inner) { }
        protected Exception(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    /// <summary> Thrown by the <see cref="IngameCoding.BBCode.Compiler.CodeGenerator"/> and <see cref="IngameCoding.Core.Interpreter"/> </summary>
    [Serializable]
    public class CompilerException : Exception
    {
        public CompilerException(string message, Position pos) : base(message, pos) { }
        public CompilerException(string message, BaseToken token) : base(message, token) { }
        public CompilerException(string message, Position pos, string file) : base(message, pos, file) { }
        public CompilerException(string message, BaseToken token, string file) : base(message, token, file) { }

        protected CompilerException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    /// <summary> Thrown by the <see cref="IngameCoding.BBCode.Tokenizer"/> </summary>
    [Serializable]
    public class TokenizerException : Exception
    {
        public TokenizerException(string message, Position pos) : base(message, pos) { }
        public TokenizerException(string message, BaseToken token) : base(message, token) { }
        public TokenizerException(string message, Position pos, string file) : base(message, pos, file) { }
        public TokenizerException(string message, BaseToken token, string file) : base(message, token, file) { }

        protected TokenizerException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    /// <summary> Thrown by the <see cref="IngameCoding.BBCode.Parser.Parser"/> </summary>
    [Serializable]
    public class SyntaxException : Exception
    {
        public SyntaxException(string message, Position pos) : base(message, pos) { }
        public SyntaxException(string message, BaseToken token) : base(message, token) { }
        public SyntaxException(string message, Position pos, string file) : base(message, pos, file) { }
        public SyntaxException(string message, BaseToken token, string file) : base(message, token, file) { }

        protected SyntaxException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    /// <summary> Thrown by the <see cref="IngameCoding.Bytecode.BytecodeInterpeter"/> </summary>
    [Serializable]
    public class RuntimeException : Exception
    {
        public RuntimeException(string message) : base(message, Position.UnknownPosition) { }
        public RuntimeException(string message, Position pos) : base(message, pos) { }
        public RuntimeException(string message, System.Exception inner) : base(message, inner) { }
        protected RuntimeException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    #endregion

    #region InternalException

    /// <summary> If this gets thrown away, it's a <b>big</b> problem. </summary>
    [Serializable]
    public class InternalException : System.Exception
    {
        public readonly string File;

        public InternalException() : base() { }

        public InternalException(string message) : base(message) { }

        public InternalException(string message, System.Exception inner) : base(message, inner) { }

        public InternalException(string message, string file) : base(message)
        { this.File = file; }

        protected InternalException(
            SerializationInfo info,
            StreamingContext context) : base(info, context) { }
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
        public readonly string File;

        readonly Position position;

        public NotExceptionBut(string message, Position position, string file)
        {
            this.Message = message;
            this.position = position;
            this.File = file;
        }
        public NotExceptionBut(string message, Position position)
        {
            this.Message = message;
            this.position = position;
            this.File = null;
        }

        public virtual string MessageAll
        {
            get
            {
                if (position.Start.Line == -1)
                { return Message; }
                else if (position.Start.Character == -1)
                { return $"{Message} at line {position.Start.Line}"; }
                else
                { return $"{Message} at line {position.Start.Line} at col {position.Start.Character}"; }
            }
        }
    }

    public class Warning : NotExceptionBut
    {
        public Warning(string message, BaseToken token, string file) : base(message, token.GetPosition(), file)
        { }
        public Warning(string message, Position position, string file) : base(message, position, file)
        { }
        public Warning(string message, BaseToken token) : base(message, token.GetPosition())
        { }
        public Warning(string message, Position position) : base(message, position)
        { }
    }

    /// <summary> It's an exception, but not. </summary>
    public class Error : NotExceptionBut
    {
        public Error(string message, BaseToken token, string file) : base(message, token.GetPosition(), file) { }
        public Error(string message, Position position, string file) : base(message, position, file) { }
        public Error(string message, BaseToken token) : base(message, token.GetPosition()) { }
        public Error(string message, Position position) : base(message, position) { }

        public override string ToString() => MessageAll;

        public Exception ToException() => new Exception(Message, base.Position, File);
    }

    public class Hint : NotExceptionBut
    {
        public Hint(string message, BaseToken token, string file) : base(message, token.GetPosition(), file) { }
        public Hint(string message, Position position, string file) : base(message, position, file) { }

        public override string ToString() => MessageAll;

        public override string MessageAll => $"{Message} at {base.Position.ToMinString()}";
    }

    public class Information : NotExceptionBut
    {
        public Information(string message, BaseToken token, string file) : base(message, token.GetPosition(), file) { }
        public Information(string message, Position position, string file) : base(message, position, file) { }

        public override string ToString() => MessageAll;

        public override string MessageAll => $"{Message} at {base.Position.ToMinString()}";
    }

    #endregion
}
