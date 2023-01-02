using System;
using System.Runtime.Serialization;

namespace IngameCoding.Errors
{
    using Core;

    using IngameCoding.BBCode;

    using Tokenizer;

    [Serializable]
    public class Exception : System.Exception
    {
        public Position Position => Token == null ? position : Token.GetPosition();
        public BaseToken Token => token;
        public Position position;
        public BaseToken token;
        public string File;

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

        [Obsolete("Don't use this", true)]
        public Exception() { }

        public Exception(string message, Position pos) : base(message)
        {
            this.position = pos;
        }
        public Exception(string message, BaseToken token) : base(message)
        {
            this.position = token.GetPosition();
            this.token = token;
        }

        public Exception(string message, Position pos, string file) : base(message)
        {
            this.position = pos;
            this.File = file;
        }
        public Exception(string message, BaseToken token, string file) : base(message)
        {
            this.position = token.GetPosition();
            this.token = token;
            this.File = file;
        }

        public Exception(string message, System.Exception inner) : base(message, inner) { }
        protected Exception(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class CompilerException : Exception
    {
        public CompilerException(string message) : base(message, Position.UnknownPosition) { }

        protected CompilerException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class ParserException : Exception
    {
        [Obsolete("Don't use this", true)]
        public ParserException(string message) : base(message, Position.UnknownPosition) { }
        public ParserException(string message, Position pos) : base(message, pos) { }
        public ParserException(string message, BaseToken token) : base(message, token) { }
        public ParserException(string message, Position pos, string file) : base(message, pos, file) { }
        public ParserException(string message, BaseToken token, string file) : base(message, token, file) { }

        protected ParserException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class SyntaxException : Exception
    {
        [Obsolete("Don't use this", true)]
        public SyntaxException(string message) : base(message, Position.UnknownPosition) { }
        public SyntaxException(string message, Position pos) : base(message, pos) { }
        public SyntaxException(string message, BaseToken token) : base(message, token) { }
        public SyntaxException(string message, Position pos, string file) : base(message, pos, file) { }
        public SyntaxException(string message, BaseToken token, string file) : base(message, token, file) { }

        protected SyntaxException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class RuntimeException : Exception
    {
        public RuntimeException(string message) : base(message, Position.UnknownPosition) { }

        public RuntimeException(string message, Position pos) : base(message, pos) { }

        public RuntimeException(string message, Position pos, System.Exception inner) : base(message, inner)
        {
            base.position = pos;
        }

        protected RuntimeException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    class SystemException : System.Exception
    {
        public SystemException() : base() { }

        public SystemException(string message) : base(message) { }

        public SystemException(string message, System.Exception inner) : base(message, inner) { }

        protected SystemException(
            SerializationInfo info,
            StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class InternalException : System.Exception
    {
        public InternalException() : base() { }

        public InternalException(string message) : base(message) { }

        public InternalException(string message, System.Exception inner) : base(message, inner) { }

        public InternalException(string message, string currentFile) : this(message)
        { }

        protected InternalException(
            SerializationInfo info,
            StreamingContext context) : base(info, context) { }
    }

    public class EndlessLoopException : InternalException
    {
        public EndlessLoopException() : base("Endless loop") { }
    }

    public class Warning
    {
        public Position position;
        public string Message;
        public string File;

        public Warning(string message, BaseToken token, string file)
        {
            Message = message;
            position = token.GetPosition();
            File = file;
        }
        public Warning(string message, Position position, string file)
        {
            Message = message;
            this.position = position;
            File = file;
        }

        public Warning(string message)
        {
            Message = message;
        }
        public Warning(string message, BaseToken token)
        {
            Message = message;
            position = token.GetPosition();
        }
        public Warning(string message, Position position)
        {
            Message = message;
            this.position = position;
        }

        public string MessageAll
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

    public class Error
    {
        public Position Position => Token == null ? position : Token.GetPosition();

        public Position position;
        public string Message;
        public BaseToken Token;
        public string File;

        public Error(string message, BaseToken token, string file)
        {
            Message = message;
            position = token.GetPosition();
            Token = token;
            File = file;
        }
        public Error(string message, Position position, string file)
        {
            Message = message;
            this.position = position;
            File = file;
        }

        public Error(string message)
        {
            Message = message;
        }
        public Error(string message, BaseToken token)
        {
            Message = message;
            position = token.GetPosition();
            Token = token;
        }
        public Error(string message, Position position)
        {
            Message = message;
            this.position = position;
        }

        public override string ToString() => MessageAll;

        public string MessageAll
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

    public class Hint
    {
        public Position Position => Token == null ? position : Token.GetPosition();

        public Position position;
        public string Message;
        public BaseToken Token;
        public string File;

        public Hint(string message, BaseToken token, string file)
        {
            Message = message;
            position = token.GetPosition();
            Token = token;
            File = file ?? throw new ArgumentNullException(nameof(file));
        }
        public Hint(string message, Position position, string file)
        {
            Message = message;
            this.position = position;
            File = file ?? throw new ArgumentNullException(nameof(file));
        }

        public override string ToString() => MessageAll;

        public string MessageAll
        {
            get
            {
                return $"{Message} at {position.ToMinString()}";

                if (position.Start.Line == -1)
                { return Message; }
                else if (position.Start.Character == -1)
                { return $"{Message} at line {position.Start.Line}"; }
                else
                { return $"{Message} at line {position.Start.Line} at col {position.Start.Character}"; }
            }
        }
    }

    public class Information
    {
        public Position Position => Token == null ? position : Token.GetPosition();

        public Position position;
        public string Message;
        public BaseToken Token;
        public string File;

        public Information(string message, BaseToken token, string file)
        {
            if (token == null) throw new ArgumentException($"'{nameof(token)}' cannot be null.", nameof(token));

            Message = message;
            position = token.GetPosition();
            Token = token;
            File = file ?? throw new ArgumentNullException(nameof(file));
        }
        public Information(string message, Position position, string file)
        {
            Message = message;
            this.position = position;
            File = file ?? throw new ArgumentNullException(nameof(file));
        }

        public override string ToString() => MessageAll;

        public string MessageAll
        {
            get
            {
                return $"{Message} at {position.ToMinString()}";

                if (position.Start.Line == -1)
                { return Message; }
                else if (position.Start.Character == -1)
                { return $"{Message} at line {position.Start.Line}"; }
                else
                { return $"{Message} at line {position.Start.Line} at col {position.Start.Character}"; }
            }
        }
    }
}
