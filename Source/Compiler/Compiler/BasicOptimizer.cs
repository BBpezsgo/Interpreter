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

        int RemoveInstruction(int index)
        {
            List<int> indexes = new();

            foreach (IFunctionThing item in this.FunctionThings)
            { indexes.Add(item.InstructionOffset); }

            int removed = GeneratedCode.RemoveInstruction(index, indexes);

            for (int j = 0; j < this.FunctionThings.Length; j++)
            { this.FunctionThings.GetDefinition<IFunctionThing, IFunctionThing>(this.FunctionThings.ElementAt(j)).InstructionOffset = indexes[j]; }

            return removed;
        }

        void Optimize()
        {
            int removedInstructions = 0;
            int changedInstructions = 0;

            for (int i = GeneratedCode.Count - 1; i >= 0; i--)
            {
                Instruction instruction = GeneratedCode[i];

                if (instruction.opcode == Opcode.JUMP_BY)
                {
                    if ((instruction.Parameter.Integer ?? 0) == 1)
                    {
                        changedInstructions += RemoveInstruction(i);
                        removedInstructions++;
                    }
                }

            }

            PrintCallback?.Invoke($"Optimalization: Removed {removedInstructions} instructions", Output.LogType.Debug);
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
