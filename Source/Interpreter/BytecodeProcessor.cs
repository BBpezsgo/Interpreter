using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    using IngameCoding.Core;
    using IngameCoding.Errors;

    internal class BytecodeProcessor
    {
        internal readonly Memory Memory;

        internal Dictionary<string, BuiltinFunction> BuiltinFunctions;

        internal int CodePointer;
        internal int BasePointer;

        internal Instruction CurrentInstruction => Memory.Code[CodePointer];

        public BytecodeProcessor(Instruction[] code, int basePointer, int heapSize, Dictionary<string, BuiltinFunction> builtinFunctions)
        {
            this.BuiltinFunctions = builtinFunctions;

            BasePointer = basePointer;
            CodePointer = code.Length;

            Memory = new(
                heapSize,
                code,
                this
            );
        }

        /// <summary>
        /// Returns the program size
        /// </summary>
        public int End() => Memory.Code.Length;

        public void Step() => Step(1);
        public void Step(int num) => CodePointer += num;

        public void Destroy()
        {
            Memory.Stack.Destroy();
            Memory.ReturnAddressStack.Clear();
            Memory.Stack = null;
            Memory.ReturnAddressStack = null;
            this.BuiltinFunctions = null;
        }

        /// <exception cref="RuntimeException"></exception>
        /// <exception cref="UserException"></exception>
        /// <exception cref="InternalException"></exception>
        /// <exception cref="System.Exception"></exception>
        public int Clock()
        {
            switch (CurrentInstruction.opcode)
            {
                case Opcode.UNKNOWN: throw new InternalException("Unknown instruction");
                case Opcode.COMMENT: Step(); return 1;

                #region Instructions
                case Opcode.EXIT: return EXIT();

                case Opcode.PUSH_VALUE: return PUSH_VALUE();
                case Opcode.POP_VALUE: return POP_VALUE();

                case Opcode.LOAD_VALUE: return LOAD_VALUE();
                case Opcode.STORE_VALUE: return STORE_VALUE();

                case Opcode.JUMP_BY_IF_FALSE: return JUMP_BY_IF_FALSE();
                case Opcode.JUMP_BY: return JUMP_BY();
                case Opcode.CALL: return CALL();
                case Opcode.RETURN: return RETURN();
                case Opcode.THROW: return THROW();

                case Opcode.CALL_BUILTIN: return CALL_BUILTIN();

                case Opcode.MATH_ADD: return MATH_ADD();
                case Opcode.MATH_SUB: return MATH_SUB();
                case Opcode.MATH_MULT: return MATH_MULT();
                case Opcode.MATH_DIV: return MATH_DIV();
                case Opcode.MATH_MOD: return MATH_MOD();

                case Opcode.BITSHIFT_LEFT: return BITSHIFT_LEFT();
                case Opcode.BITSHIFT_RIGHT: return BITSHIFT_RIGHT();

                case Opcode.LOGIC_LT: return LOGIC_LT();
                case Opcode.LOGIC_MT: return LOGIC_MT();
                case Opcode.LOGIC_AND: return LOGIC_AND();
                case Opcode.LOGIC_OR: return LOGIC_OR();
                case Opcode.LOGIC_EQ: return LOGIC_EQ();
                case Opcode.LOGIC_NEQ: return LOGIC_NEQ();
                case Opcode.LOGIC_LTEQ: return LOGIC_LTEQ();
                case Opcode.LOGIC_MTEQ: return LOGIC_MTEQ();
                case Opcode.LOGIC_XOR: return LOGIC_XOR();
                case Opcode.LOGIC_NOT: return LOGIC_NOT();

                case Opcode.HEAP_GET: return HEAP_GET();
                case Opcode.HEAP_SET: return HEAP_SET();

                case Opcode.HEAP_ALLOC: return HEAP_ALLOC();
                case Opcode.HEAP_DEALLOC: return HEAP_DEALLOC();

                case Opcode.DEBUG_SET_TAG: return DEBUG_SET_TAG();
                case Opcode.CS_PUSH: return CS_PUSH();
                case Opcode.CS_POP: return CS_POP();

                case Opcode.GET_BASEPOINTER: return GET_BASEPOINTER();

                #endregion

                default: throw new InternalException("Unimplemented instruction " + CurrentInstruction.opcode.ToString());
            }
        }

        /// <exception cref="System.Exception"/>
        int GetStackAddress() => CurrentInstruction.AddressingMode switch
        {
            AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
            AddressingMode.BASEPOINTER_RELATIVE => BasePointer + CurrentInstruction.ParameterInt,
            AddressingMode.RELATIVE => Memory.Stack.Count + CurrentInstruction.ParameterInt,
            AddressingMode.POP => Memory.Stack.Count - 1,
            AddressingMode.RUNTIME => Memory.Stack.Pop().ValueInt,
            _ => throw new System.Exception($"Invalid stack addressing mode {CurrentInstruction.AddressingMode}"),
        };

        /// <exception cref="System.Exception"/>
        int GetHeapAddress() => CurrentInstruction.AddressingMode switch
        {
            AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
            AddressingMode.RUNTIME => Memory.Stack.Pop().ValueInt,
            _ => throw new System.Exception($"Invalid heap addressing mode {CurrentInstruction.AddressingMode}"),
        };

        #region Instruction Methods

        int HEAP_ALLOC()
        {
            DataItem sizeData = Memory.Stack.Pop();
            int size = sizeData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_ALLOC, got {sizeData.type}");

            int block = Memory.Heap.Allocate(size);

            Memory.Stack.Push(block, CurrentInstruction.tag);

            Step();
            return 10;
        }

        int HEAP_DEALLOC()
        {
            DataItem pointerData = Memory.Stack.Pop();
            int pointer = pointerData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_DEALLOC, got {pointerData.type}");

            Memory.Heap.Deallocate(pointer);

            Step();
            return 10;
        }

        /// <exception cref="UserException"/>
        int THROW()
        {
            DataItem throwValue = Memory.Stack.Pop();
            string v = null;
            try
            { v = Memory.Heap.GetStringByPointer(throwValue.ValueInt); }
            catch (System.Exception) { }
            throw new UserException("User Exception Thrown", v);
        }

        /*
        /// <exception cref="RuntimeException"/>
        int FIND_HEAP_FREE_SPACE()
        {
            DataItem sizeNeededData = Memory.Stack.Pop();

            if (sizeNeededData.type != RuntimeType.INT)
            { throw new RuntimeException($"fuck you"); }

            int sizeNeeded = sizeNeededData.ValueInt;

            int currentFreeSize = 0;
            int freeSizeStarted = 0;
            bool found = false;
            for (int i = 0; i < Memory.Heap.Size; i++)
            {
                if (Memory.Heap[i].IsNull)
                {
                    currentFreeSize++;
                    if (currentFreeSize >= sizeNeeded)
                    {
                        found = true;
                        break;
                    }
                }
                else
                {
                    currentFreeSize = 0;
                    freeSizeStarted = i + 1;
                }
            }
            if (!found) throw new RuntimeException("Out of HEAP memory");

            Memory.Stack.Push(freeSizeStarted, CurrentInstruction.tag);

            Step();

            return 5;
        }
        */

        int GET_BASEPOINTER()
        {
            Memory.Stack.Push(BasePointer);
            Step();
            return 1;
        }

        int CS_PUSH()
        {
            Memory.CallStack.Push(CurrentInstruction.tag);
            Step();

            return 1;
        }

        int CS_POP()
        {
            Memory.CallStack.Pop();
            Step();

            return 1;
        }

        int DEBUG_SET_TAG()
        {
            var last = Memory.Stack.Last();
            last.Tag = CurrentInstruction.tag;
            Memory.Stack.Set(Memory.Stack.Count - 1, last, true);
            Step();

            return 1;
        }

        int HEAP_GET()
        {
            int address = GetHeapAddress();
            var v = Memory.Heap[address];
            Memory.Stack.Push(v);
            Step();

            return 2;
        }

        int HEAP_SET()
        {
            int address = GetHeapAddress();
            var v = Memory.Stack.Pop();
            Memory.Heap[address] = v;
            Step();

            return 2;
        }

        int LOGIC_NOT()
        {
            var v = Memory.Stack.Pop();
            Memory.Stack.Push(!v);
            Step();

            return 3;
        }

        void OnExternalReturnValue(DataItem returnValue)
        {
            Memory.Stack.Push(returnValue, "return v");
        }

        /// <exception cref="InternalException"/>
        /// <exception cref="RuntimeException"/>
        int CALL_BUILTIN()
        {
            DataItem functionNameDataItem = Memory.Stack.Pop();
            if (functionNameDataItem.type != RuntimeType.INT)
            { throw new InternalException($"Instruction CALL_BUILTIN need a Strint pointer (int) DataItem parameter from the stack, recived {functionNameDataItem.type} {functionNameDataItem}"); }

            string functionName = Memory.Heap.GetStringByPointer(functionNameDataItem.ValueInt);

            if (!BuiltinFunctions.TryGetValue(functionName, out BuiltinFunction builtinFunction))
            { throw new RuntimeException($"Undefined function \"{functionName}\""); }

            List<DataItem> parameters = new();
            for (int i = 0; i < CurrentInstruction.ParameterInt; i++)
            { parameters.Add(Memory.Stack.Pop()); }

            if (builtinFunction is ManagedBuiltinFunction managedBuiltinFunction)
            {
                managedBuiltinFunction.OnReturn = OnExternalReturnValue;
                managedBuiltinFunction.Callback(parameters.ToArray());
            }
            else
            {
                if (builtinFunction.ReturnSomething)
                {
                    var returnValue = builtinFunction.Callback(parameters.ToArray());
                    Memory.Stack.Push(returnValue, "return v");
                }
                else
                {
                    builtinFunction.Callback(parameters.ToArray());
                }
            }

            Step();

            return 15;
        }

        int LOGIC_MT()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide > rightSide);

            Step();

            return 4;
        }
        int LOGIC_EQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide == rightSide);

            Step();

            return 4;
        }
        int LOGIC_NEQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide != rightSide);

            Step();

            return 4;
        }
        int LOGIC_OR()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide | rightSide);

            Step();

            return 4;
        }
        int LOGIC_XOR()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide ^ rightSide);

            Step();

            return 4;
        }
        int LOGIC_LTEQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide <= rightSide);

            Step();

            return 4;
        }
        int LOGIC_MTEQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide >= rightSide);

            Step();

            return 4;
        }
        int LOGIC_AND()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide & rightSide);

            Step();

            return 4;
        }

        int POP_VALUE()
        {
            if (Memory.Stack.Count > 0) Memory.Stack.RemoveAt(Memory.Stack.Count - 1);
            Step();

            return 2;
        }

        int RETURN()
        {
            int returnAddress = Memory.ReturnAddressStack.Pop();
            BasePointer = Memory.Stack.Pop().ValueInt;
            CodePointer = returnAddress;

            return 3;
        }
        int CALL()
        {
            Memory.Stack.Push(BasePointer, "saved base pointer");
            Memory.ReturnAddressStack.Push(CodePointer + 1);
            BasePointer = Memory.Stack.Count;
            Step(CurrentInstruction.ParameterInt);

            return 4;
        }

        int JUMP_BY()
        {
            CodePointer += CurrentInstruction.ParameterInt;

            return 1;
        }
        int JUMP_BY_IF_FALSE()
        {
            var condition = Memory.Stack.Pop();

            if (condition.IsFalsy())
            { CodePointer += CurrentInstruction.ParameterInt; }
            else
            { Step(); }

            return 3;
        }

        int STORE_VALUE()
        {
            int address = GetStackAddress();
            var value = Memory.Stack.Pop();

            Memory.Stack.Set(address, value);

            Step();

            return 3;
        }
        int LOAD_VALUE()
        {
            int address = GetStackAddress();

            Memory.Stack.Push(Memory.Stack[address], CurrentInstruction.tag);

            Step();

            return 3;
        }

        int LOGIC_LT()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide < rightSide);

            Step();

            return 4;
        }

        int PUSH_VALUE()
        {
            DataItem value = CurrentInstruction.ParameterData;
            value.Tag = CurrentInstruction.tag;

            Memory.Stack.Push(value);

            Step();

            return 2;
        }

        int BITSHIFT_LEFT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(DataItem.BitshiftLeft(leftSide, rightSide));

            Step();

            return 4;
        }
        int BITSHIFT_RIGHT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(DataItem.BitshiftRight(leftSide, rightSide));

            Step();

            return 4;
        }

        int MATH_ADD()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide + rightSide);

            Step();

            return 4;
        }

        int EXIT()
        {
            CodePointer = Memory.Code.Length;

            return 1;
        }

        int MATH_DIV()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide / rightSide);

            Step();

            return 4;
        }

        int MATH_SUB()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide - rightSide);

            Step();

            return 4;
        }

        int MATH_MULT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide * rightSide);

            Step();

            return 4;
        }

        int MATH_MOD()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide % rightSide);

            Step();

            return 4;
        }

        #endregion
    }

    internal class Memory
    {
        internal DataStack Stack;
        internal HEAP Heap;
        internal Stack<int> ReturnAddressStack;
        internal Instruction[] Code;
        internal Stack<string> CallStack;

        public Memory(int heapSize, Instruction[] code, BytecodeProcessor processor)
        {
            Code = code;

            Stack = new DataStack(processor);
            Heap = new HEAP(heapSize);

            CallStack = new Stack<string>();
            ReturnAddressStack = new Stack<int>();
        }
    }
}
