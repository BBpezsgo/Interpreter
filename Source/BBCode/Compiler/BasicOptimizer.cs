using System;
using System.Collections.Generic;
using System.Linq;

namespace LanguageCore.BBCode.Compiler
{
    using LanguageCore.Runtime;

    internal class BasicOptimizer
    {
        List<Instruction> GeneratedCode;
        IFunctionThing[] FunctionThings;
        PrintCallback PrintCallback;

        int RemoveInstruction(int index)
        {
            List<int> indexes = new();

            foreach (IFunctionThing item in this.FunctionThings)
            { indexes.Add(item.InstructionOffset); }

            int removed = GeneratedCode.RemoveInstruction(index, indexes);

            for (int j = 0; j < this.FunctionThings.Length; j++)
            {
                bool found = false;
                foreach (IFunctionThing element in this.FunctionThings)
                {
                    if (element == null) continue;
                    if (element.IsSame(this.FunctionThings.ElementAt(j)))
                    {
                        element.InstructionOffset = indexes[j];
                        found = true;
                        break;
                    }
                }
                if (!found)
                { throw new KeyNotFoundException($"Key {this.FunctionThings.ElementAt(j)} not found in list {this.FunctionThings}"); }
            }

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

            PrintCallback?.Invoke($"Optimalization: Removed {removedInstructions} instructions", LogType.Debug);
        }

        internal static void Optimize(
            List<Instruction> code,
            IFunctionThing[] functionThings,
            PrintCallback printCallback = null
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
