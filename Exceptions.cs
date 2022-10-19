using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace IngameCoding
{
    public struct Vector2Int
    {
        public int x;
        public int y;

        public Vector2Int(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    public class BaseToken
    {
        public int startOffset;
        public int endOffset;
        public int lineNumber;

        public int startOffsetTotal;
        public int endOffsetTotal;
        public Position Position => new Position(lineNumber, startOffset, new Vector2Int(startOffsetTotal, endOffsetTotal));
    }

    public struct Position
    {
        readonly bool unknown;
        int line;
        readonly int col;

        public bool Unknown => unknown;
        public int Line
        {
            get { return line; }
            set { line = value; }
        }
        public int Col => col;

        public Vector2Int AbsolutePosition;

        public Position(int line)
        {
            if (line > -1)
            {
                this.unknown = false;
                this.line = line;
            }
            else
            {
                this.unknown = true;
                this.line = -1;
            }
            this.col = -1;
            this.AbsolutePosition = new Vector2Int(-1, -1);
        }

        public Position(int line, int character)
        {
            this.unknown = false;
            this.line = line;
            this.col = character;
            this.AbsolutePosition = new Vector2Int(-1, -1);
        }

        public Position(int line, int character, Vector2Int absolutePosition)
        {
            this.unknown = false;
            this.line = line;
            this.col = character;
            this.AbsolutePosition = absolutePosition;
        }
    }


    [System.Serializable]
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
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [System.Serializable]
    public class ParserException : Exception
    {
        public ParserException(string message) : base(message, new Position(-1)) { }
        public ParserException(string message, Position pos) : base(message, pos) { }
        public ParserException(string message, BaseToken token) : base(message, token) { }

        protected ParserException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [System.Serializable]
    public class SyntaxException : Exception
    {
        public readonly string[] CompilerCallstack;

        public SyntaxException(string message) : base(message, new Position(-1)) { }
        public SyntaxException(string message, Position pos) : base(message, pos) { }
        public SyntaxException(string message, BaseToken token) : base(message, token) { }

        public SyntaxException(string message, string[] compilerCallstack) : base(message, new Position(-1)) { this.CompilerCallstack = compilerCallstack; }
        public SyntaxException(string message, Position pos, string[] compilerCallstack) : base(message, pos) { this.CompilerCallstack = compilerCallstack; }
        public SyntaxException(string message, BaseToken token, string[] compilerCallstack) : base(message, token) { this.CompilerCallstack = compilerCallstack; }

        protected SyntaxException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [System.Serializable]
    public class RuntimeException : Exception
    {
        public RuntimeException(string message) : base(message, new Position(-1)) { }

        public RuntimeException(string message, Position pos) : base(message, pos) { }

        public RuntimeException(string message, Position pos, System.Exception inner) : base(message, inner)
        {
            base.Position = pos;
        }

        protected RuntimeException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [System.Serializable]
    public class SystemException : System.Exception
    {
        public SystemException() : base() { }

        public SystemException(string message) : base(message) { }

        public SystemException(string message, System.Exception inner) : base(message, inner) { }

        protected SystemException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }

    [System.Serializable]
    public class InternalException : System.Exception
    {
        public InternalException() : base() { }

        public InternalException(string message) : base(message) { }

        public InternalException(string message, System.Exception inner) : base(message, inner) { }

        protected InternalException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
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
