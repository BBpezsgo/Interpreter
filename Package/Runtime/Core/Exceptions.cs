using System.Runtime.Serialization;
using System;

namespace IngameCoding.Errors
{
    using Core;
    using BBCode;

    [Serializable]
    public class Exception : System.Exception
    {
        public Position Position;
        public BaseToken Token;

        public string MessageAll
        {
            get
            {
                if (Position.Line == -1)
                {
                    return Message;
                }
                else if (Position.Col == -1)
                {
                    return $"{Message} at line {Position.Line}";
                }
                else
                {
                    return $"{Message} at line {Position.Line} at col {Position.Col}";
                }
            }
        }

        public Exception() { }
        public Exception(string message, Position pos) : base(message)
        {
            this.Position = pos;
        }
        public Exception(string message, BaseToken token) : base(message)
        {
            this.Position = token.Position;
            this.Token = token;
        }

        public Exception(string message, System.Exception inner) : base(message, inner) { }
        protected Exception(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class ParserException : Exception
    {
        public ParserException(string message) : base(message, new Position(-1)) { }
        public ParserException(string message, Position pos) : base(message, pos) { }
        public ParserException(string message, BaseToken token) : base(message, token) { }

        protected ParserException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class SyntaxException : Exception
    {
        public SyntaxException(string message) : base(message, new Position(-1)) { }
        public SyntaxException(string message, Position pos) : base(message, pos) { }
        public SyntaxException(string message, BaseToken token) : base(message, token) { }

        protected SyntaxException(
          SerializationInfo info,
          StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    public class RuntimeException : Exception
    {
        public RuntimeException(string message) : base(message, new Position(-1)) { }

        public RuntimeException(string message, Position pos) : base(message, pos) { }

        public RuntimeException(string message, Position pos, System.Exception inner) : base(message, inner)
        {
            base.Position = pos;
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
        public Position Position;
        public string Message;

        public string MessageAll
        {
            get
            {
                if (Position.Line == -1)
                {
                    return Message;
                }
                else if (Position.Col == -1)
                {
                    return $"{Message} at line {Position.Line}";
                }
                else
                {
                    return $"{Message} at line {Position.Line} at col {Position.Col}";
                }
            }
        }
    }
}
