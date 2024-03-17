﻿namespace LanguageCore.Brainfuck;

public partial class Interpreter : InterpreterBase<OpCodes>
{
    public Interpreter(OutputCallback? onOutput = null, InputCallback? onInput = null)
        : base(onOutput, onInput) { }

    protected override OpCodes[] ParseCode(string code, bool showProgress, Runtime.DebugInformation? debugInformation)
    {
        code = BrainfuckCode.RemoveNoncodes(code, showProgress, debugInformation);
        return CompactCode.ToOpCode(code);
    }

    /// <exception cref="BrainfuckRuntimeException"/>
    protected override void Evaluate(OpCodes instruction)
    {
        switch (instruction)
        {
            case OpCodes.Add:
                Memory[_memoryPointer]++;
                break;
            case OpCodes.Sub:
                Memory[_memoryPointer]--;
                break;
            case OpCodes.PointerRight:
                if (_memoryPointer++ >= Memory.Length)
                { throw new BrainfuckRuntimeException($"Memory overflow", CurrentContext); }
                break;
            case OpCodes.PointerLeft:
                if (_memoryPointer-- <= 0)
                { throw new BrainfuckRuntimeException($"Memory underflow", CurrentContext); }
                break;
            case OpCodes.BranchStart:
                if (Memory[_memoryPointer] == 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer++;
                        if (IsDone) break;
                        if (Code[_codePointer] == OpCodes.BranchEnd)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException($"Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer] == OpCodes.BranchStart)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException($"Unclosed bracket", CurrentContext);
                }
                break;
            case OpCodes.BranchEnd:
                if (Memory[_memoryPointer] != 0)
                {
                    int depth = 0;
                    while (!IsDone)
                    {
                        _codePointer--;
                        if (IsDone) break;
                        if (Code[_codePointer] == OpCodes.BranchStart)
                        {
                            if (depth == 0) return;
                            if (depth < 0) throw new BrainfuckRuntimeException($"Wat", CurrentContext);
                            depth--;
                        }
                        else if (Code[_codePointer] == OpCodes.BranchEnd)
                        { depth++; }
                    }
                    throw new BrainfuckRuntimeException($"Unexpected closing bracket", CurrentContext);
                }
                break;
            case OpCodes.Out:
                OnOutput?.Invoke(Memory[_memoryPointer]);
                break;
            case OpCodes.In:
                Memory[_memoryPointer] = OnInput?.Invoke() ?? 0;
                break;
            case OpCodes.Break:
                _isPaused = true;
                break;
            default:
                throw new BrainfuckRuntimeException($"Unknown instruction {Code[_codePointer]}", CurrentContext);
        }
    }
}
