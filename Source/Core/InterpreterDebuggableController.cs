using IngameCoding.Bytecode;
using IngameCoding.Output;

using System;

namespace IngameCoding.Core
{
    public class InterpreterDebuggabble : Interpreter
    {
        int _breakpoint = int.MinValue;

        public int Breakpoint
        {
            get => _breakpoint;
            set
            {
                if (value == int.MinValue)
                {
                    _breakpoint = int.MinValue;
                    return;
                }

                int endlessSafe = 8;

                _breakpoint = value;
                while (this.details.CompilerResult.compiledCode[_breakpoint].opcode == Bytecode.Opcode.COMMENT)
                {
                    _breakpoint++;

                    if (endlessSafe-- < 0) throw new Errors.EndlessLoopException();
                }
            }
        }

        public void Step()
        {
            Breakpoint = bytecodeInterpreter.CodePointer + 1;
            Continue();
            Breakpoint = int.MinValue;
        }

        public void StepInto()
        {
            Breakpoint = int.MinValue;
            Update();
            Breakpoint = int.MinValue;
        }

        /// <exception cref="Errors.EndlessLoopException"></exception>
        public void Continue()
        {
            int endlessSafe = 1024;
            while (true)
            {
                Update();
                if (bytecodeInterpreter.CodePointer == Breakpoint) break;

                if (endlessSafe-- < 0) throw new Errors.EndlessLoopException();
            }
        }

        /// <summary>
        /// It prepares the interpreter to run some code
        /// </summary>
        /// <param name="compiledCode"></param>
        public override void ExecuteProgram(Instruction[] compiledCode, BytecodeInterpreterSettings bytecodeInterpreterSettings)
        {
            BytecodeInterpreterSettings settigns = bytecodeInterpreterSettings;
            settigns.ClockCyclesPerUpdate = 1;
            base.ExecuteProgram(compiledCode, settigns);
        }
    }
}
