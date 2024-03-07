namespace LanguageCore.Brainfuck;

public partial class InterpreterCompact : InterpreterBase<CompactCodeSegment>
{
    public InterpreterCompact(OutputCallback? onOutput = null, InputCallback? onInput = null)
        : base(onOutput, onInput) { }

    protected override CompactCodeSegment[] ParseCode(string code, bool showProgress, Runtime.DebugInformation? debugInfo)
    {
        code = BrainfuckCode.RemoveNoncodes(code, showProgress);
        return CompactCode.Generate(code, showProgress, debugInfo);
    }

    /// <exception cref="BrainfuckRuntimeException"/>
    /// <exception cref="NotImplementedException"/>
    protected override void Evaluate(CompactCodeSegment instruction)
    {
        switch (instruction.OpCode)
        {
            case OpCodesCompact.CLEAR:
            {
                Memory[_memoryPointer] = 0;
                break;
            }

            case OpCodesCompact.ADD:
            {
                Memory[_memoryPointer] = (byte)(Memory[_memoryPointer] + instruction.Count);
                break;
            }

            case OpCodesCompact.SUB:
            {
                Memory[_memoryPointer] = (byte)(Memory[_memoryPointer] - instruction.Count);
                break;
            }

            case OpCodesCompact.POINTER_R:
            {
                _memoryPointer += instruction.Count;
                _memoryPointerRange = Range.Union(_memoryPointerRange, _memoryPointer);
                if (_memoryPointer >= Memory.Length)
                { throw new BrainfuckRuntimeException("Memory overflow", CurrentContext); }
                break;
            }

            case OpCodesCompact.POINTER_L:
            {
                _memoryPointer -= instruction.Count;
                _memoryPointerRange = Range.Union(_memoryPointerRange, _memoryPointer);
                if (_memoryPointer < 0)
                { throw new BrainfuckRuntimeException("Memory underflow", CurrentContext); }
                break;
            }

            case OpCodesCompact.BRANCH_START:
            {
                if (instruction.Count != 1)
                { throw new NotImplementedException(); }
                if (Memory[_memoryPointer] == 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer++;
                        if (IsDone) break;
                        if (Code[_codePointer].OpCode == OpCodesCompact.BRANCH_END)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException("Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer].OpCode == OpCodesCompact.BRANCH_START)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException("Unclosed bracket", CurrentContext);
                }
                break;
            }

            case OpCodesCompact.BRANCH_END:
            {
                if (instruction.Count != 1)
                { throw new NotImplementedException(); }
                if (Memory[_memoryPointer] != 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer--;
                        if (IsDone) break;
                        if (Code[_codePointer].OpCode == OpCodesCompact.BRANCH_START)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException("Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer].OpCode == OpCodesCompact.BRANCH_END)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException("Unexpected closing bracket", CurrentContext);
                }
                break;
            }

            case OpCodesCompact.OUT:
            {
                if (instruction.Count != 1)
                { throw new NotImplementedException(); }
                OnOutput?.Invoke(Memory[_memoryPointer]);
                break;
            }

            case OpCodesCompact.IN:
            {
                if (instruction.Count != 1)
                { throw new NotImplementedException(); }
                Memory[_memoryPointer] = OnInput?.Invoke() ?? 0;
                break;
            }

            case OpCodesCompact.MOVE:
            {
                byte data = Memory[_memoryPointer];
                Memory[_memoryPointer] = 0;
                if (instruction.Arg1 != 0) Memory[_memoryPointer + instruction.Arg1] += data;
                if (instruction.Arg2 != 0) Memory[_memoryPointer + instruction.Arg2] += data;
                if (instruction.Arg3 != 0) Memory[_memoryPointer + instruction.Arg3] += data;
                if (instruction.Arg4 != 0) Memory[_memoryPointer + instruction.Arg4] += data;
                break;
            }

            default: throw new BrainfuckRuntimeException($"Unknown instruction {instruction}", CurrentContext);
        }
    }
}
