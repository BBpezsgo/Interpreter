namespace LanguageCore.Brainfuck;

public partial class Interpreter : InterpreterBase<OpCodes>
{
    public Interpreter(OutputCallback? onOutput = null, InputCallback? onInput = null)
        : base(onOutput, onInput) { }

    protected override OpCodes[] ParseCode(string code, bool showProgress, Runtime.DebugInformation? debugInfo)
    {
        code = BrainfuckCode.RemoveNoncodes(code, showProgress);
        return CompactCode.ToOpCode(code);
    }

    /// <exception cref="BrainfuckRuntimeException"/>
    protected override void Evaluate(OpCodes instruction)
    {
        switch (instruction)
        {
            case OpCodes.ADD:
                Memory[_memoryPointer]++;
                break;
            case OpCodes.SUB:
                Memory[_memoryPointer]--;
                break;
            case OpCodes.POINTER_R:
                if (_memoryPointer++ >= Memory.Length)
                { throw new BrainfuckRuntimeException($"Memory overflow", CurrentContext); }
                break;
            case OpCodes.POINTER_L:
                if (_memoryPointer-- <= 0)
                { throw new BrainfuckRuntimeException($"Memory underflow", CurrentContext); }
                break;
            case OpCodes.BRANCH_START:
                if (Memory[_memoryPointer] == 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer++;
                        if (IsDone) break;
                        if (Code[_codePointer] == OpCodes.BRANCH_END)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException($"Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer] == OpCodes.BRANCH_START)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException($"Unclosed bracket", CurrentContext);
                }
                break;
            case OpCodes.BRANCH_END:
                if (Memory[_memoryPointer] != 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer--;
                        if (IsDone) break;
                        if (Code[_codePointer] == OpCodes.BRANCH_START)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException($"Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer] == OpCodes.BRANCH_END)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException($"Unexpected closing bracket", CurrentContext);
                }
                break;
            case OpCodes.OUT:
                OnOutput?.Invoke(Memory[_memoryPointer]);
                break;
            case OpCodes.IN:
                Memory[_memoryPointer] = OnInput?.Invoke() ?? 0;
                break;
            default:
                throw new BrainfuckRuntimeException($"Unknown instruction {Code[_codePointer]}", CurrentContext);
        }
    }
}
