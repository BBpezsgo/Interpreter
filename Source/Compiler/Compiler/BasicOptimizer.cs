using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameCoding.BBCode.Compiler
{
    using IngameCoding.Bytecode;

    internal class BasicOptimizer
    {
        List<Instruction> GeneratedCode;
        IFunctionThing[] FunctionThings;
        Action<string, Output.LogType> PrintCallback;

        void Optimize()
        {
            int removedInstructions = 0;
            int changedInstructions = 0;
            for (int i = GeneratedCode.Count - 1; i >= 0; i--)
            {
                var instruction = GeneratedCode[i];
                if (instruction.opcode == Opcode.JUMP_BY || instruction.opcode == Opcode.JUMP_BY_IF_FALSE)
                {
                    if (instruction.Parameter is int jumpBy)
                    {
                        if (jumpBy == 1)
                        {
                            List<int> indexes = new();

                            foreach (var item in this.FunctionThings)
                            { indexes.Add(item.InstructionOffset); }

                            changedInstructions += GeneratedCode.RemoveInstruction(i, indexes);
                            removedInstructions++;

                            for (int j = 0; j < this.FunctionThings.Length; j++)
                            { this.FunctionThings.GetDefinition<IFunctionThing, IFunctionThing>(this.FunctionThings.ElementAt(j)).InstructionOffset = indexes[j]; }
                        }
                    }
                }
            }
            PrintCallback?.Invoke($"Optimalization: Removed {removedInstructions} & changed {changedInstructions} instructions", Output.LogType.Debug);
        }

        internal static void Optimize(
            List<Instruction> code,
            IFunctionThing[] functionThings,
            Action<string, Output.LogType> printCallback = null
            )
        {
            BasicOptimizer basicOptimizer = new()
            {
                GeneratedCode = code,
                FunctionThings = functionThings,
                PrintCallback = printCallback,
            };
            basicOptimizer.Optimize();
        }
    }
}
