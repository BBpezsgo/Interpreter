using System;
using System.Runtime.Serialization;

namespace LanguageCore.Brainfuck
{
    public readonly struct RuntimeContext
    {
        public readonly int MemoryPointer;
        public readonly int CodePointer;

        public RuntimeContext(int memoryPointer, int codePointer)
        {
            MemoryPointer = memoryPointer;
            CodePointer = codePointer;
        }
    }

    [Serializable]
    public class BrainfuckRuntimeException : Exception
    {
        public readonly RuntimeContext RuntimeContext;

        public BrainfuckRuntimeException(string message, RuntimeContext context) : base(message)
        {
            RuntimeContext = context;
        }

        protected BrainfuckRuntimeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            RuntimeContext = (RuntimeContext)info.GetValue("RuntimeContext", typeof(RuntimeContext))!;
        }
    }
}
