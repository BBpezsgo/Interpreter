namespace IngameCoding.Terminal
{
    public class TerminalInterpreter
    {
        public enum LogType
        {
            /// <summary>
            /// Used by:
            /// <list type="bullet">
            /// <item>Tokenizer</item>
            /// <item>Parser</item>
            /// <item>Compiler</item>
            /// <item>Interpreter</item>
            /// </list>
            /// </summary>
            System,
            /// <summary>
            /// Used by:
            /// <list type="bullet">
            /// <item>The code</item>
            /// </list>
            /// </summary>
            Normal,
            /// <summary>
            /// Used by:
            /// <list type="bullet">
            /// <item>Tokenizer</item>
            /// <item>Parser</item>
            /// <item>Compiler</item>
            /// <item>Interpreter</item>
            /// <item>The code</item>
            /// </list>
            /// </summary>
            Warning,
            /// <summary>
            /// Used by:
            /// <list type="bullet">
            /// <item>Compiler</item>
            /// <item>Interpreter</item>
            /// <item>The code</item>
            /// </list>
            /// </summary>
            Error,
            /// <summary>
            /// Used by:
            /// <list type="bullet">
            /// <item>Tokenizer</item>
            /// <item>Parser</item>
            /// <item>Compiler</item>
            /// <item>Interpreter</item>
            /// </list>
            /// </summary>
            Debug,
        }
    }
}
