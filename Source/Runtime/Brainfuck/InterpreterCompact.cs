namespace LanguageCore.Brainfuck;

public class InterpreterCompact : InterpreterBase<CompactCodeSegment>
{
    public InterpreterCompact(OutputCallback? onOutput = null, InputCallback? onInput = null)
        : base(onOutput, onInput) { }

    protected override ImmutableArray<CompactCodeSegment> ParseCode(string code, bool showProgress, Runtime.DebugInformation? debugInformation)
    {
        code = BrainfuckCode.RemoveNoncodes(code, showProgress, debugInformation);
        return CompactCode.Generate(code, showProgress, debugInformation);
    }

    protected override void Evaluate(CompactCodeSegment instruction)
    {
        switch (instruction.OpCode)
        {
            case OpCodesCompact.Clear:
            {
                Memory[_memoryPointer] = 0;
                break;
            }

            case OpCodesCompact.Add:
            {
                Memory[_memoryPointer] = (byte)(Memory[_memoryPointer] + instruction.Count);
                break;
            }

            case OpCodesCompact.Sub:
            {
                Memory[_memoryPointer] = (byte)(Memory[_memoryPointer] - instruction.Count);
                break;
            }

            case OpCodesCompact.PointerRight:
            {
                _memoryPointer += instruction.Count;
                _memoryPointerRange = RangeUtils.Union(_memoryPointerRange, _memoryPointer);
                _memoryPointer = (_memoryPointer + Memory.Length) % Memory.Length;

                // if (_memoryPointer >= Memory.Length)
                // { throw new BrainfuckRuntimeException("Memory overflow", CurrentContext); }
                break;
            }

            case OpCodesCompact.PointerLeft:
            {
                _memoryPointer -= instruction.Count;
                _memoryPointerRange = RangeUtils.Union(_memoryPointerRange, _memoryPointer);
                _memoryPointer = (_memoryPointer + Memory.Length) % Memory.Length;

                // if (_memoryPointer < 0)
                // { throw new BrainfuckRuntimeException("Memory underflow", CurrentContext); }
                break;
            }

            case OpCodesCompact.BranchStart:
            {
                if (instruction.Count != 1)
                { throw new BrainfuckRuntimeException($"Invalid instruction {instruction}", CurrentContext, DebugInfo); }

                if (Memory[_memoryPointer] == 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer++;
                        if (IsDone) break;
                        if (Code[_codePointer].OpCode == OpCodesCompact.BranchEnd)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException("Wat", CurrentContext, DebugInfo);
                            depth--;
                        }
                        else if (Code[_codePointer].OpCode == OpCodesCompact.BranchStart)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException("Unclosed bracket", CurrentContext, DebugInfo);
                }
                break;
            }

            case OpCodesCompact.BranchEnd:
            {
                if (instruction.Count != 1)
                { throw new BrainfuckRuntimeException($"Invalid instruction {instruction}", CurrentContext, DebugInfo); }

                if (Memory[_memoryPointer] != 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer--;
                        if (IsDone) break;
                        if (Code[_codePointer].OpCode == OpCodesCompact.BranchStart)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException("Wat", CurrentContext, DebugInfo);
                            depth--;
                        }
                        else if (Code[_codePointer].OpCode == OpCodesCompact.BranchEnd)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException("Unexpected closing bracket", CurrentContext, DebugInfo);
                }
                break;
            }

            case OpCodesCompact.Out:
            {
                if (instruction.Count != 1)
                { throw new BrainfuckRuntimeException($"Invalid instruction {instruction}", CurrentContext, DebugInfo); }

                OnOutput?.Invoke(Memory[_memoryPointer]);
                break;
            }

            case OpCodesCompact.In:
            {
                if (instruction.Count != 1)
                { throw new BrainfuckRuntimeException($"Invalid instruction {instruction}", CurrentContext, DebugInfo); }

                Memory[_memoryPointer] = OnInput?.Invoke() ?? 0;
                break;
            }

            case OpCodesCompact.Break:
            {
                _isPaused = true;
                break;
            }

            case OpCodesCompact.Move:
            {
                byte data = Memory[_memoryPointer];
                Memory[_memoryPointer] = 0;
                if (instruction.Arg1 != 0) Memory[(_memoryPointer + instruction.Arg1 + Memory.Length) % Memory.Length] += data;
                if (instruction.Arg2 != 0) Memory[(_memoryPointer + instruction.Arg2 + Memory.Length) % Memory.Length] += data;
                if (instruction.Arg3 != 0) Memory[(_memoryPointer + instruction.Arg3 + Memory.Length) % Memory.Length] += data;
                if (instruction.Arg4 != 0) Memory[(_memoryPointer + instruction.Arg4 + Memory.Length) % Memory.Length] += data;
                break;
            }

            default: throw new BrainfuckRuntimeException($"Unknown instruction {instruction}", CurrentContext, DebugInfo);
        }
    }
}
