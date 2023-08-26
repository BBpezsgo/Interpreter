using System.Collections.Generic;

namespace ProgrammingLanguage.Bytecode
{
    using System;
    using ProgrammingLanguage.Core;
    using ProgrammingLanguage.Errors;

    internal class BytecodeProcessor
    {
        internal readonly Memory Memory;

        internal Dictionary<string, ExternalFunctionBase> ExternalFunctions;

        internal int CodePointer;
        internal int BasePointer;

        internal Instruction CurrentInstruction => Memory.Code[CodePointer];

        public BytecodeProcessor(Instruction[] code, int basePointer, int heapSize, Dictionary<string, ExternalFunctionBase> externalFunctions)
        {
            ExternalFunctions = externalFunctions;

            BasePointer = basePointer;
            CodePointer = code.Length;

            Memory = new(heapSize, code);
        }

        public void Step() => Step(1);
        public void Step(int num) => CodePointer += num;

        public void Destroy()
        {
            Memory.Stack.Destroy();
            Memory.Stack = null;
            ExternalFunctions = null;
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
                case Opcode.THROW: return THROW();

                case Opcode.CALL_EXTERNAL: return CALL_EXTERNAL();

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
                case Opcode.SET_BASEPOINTER: return SET_BASEPOINTER();

                case Opcode.SET_CODEPOINTER: return SET_CODEPOINTER();

                case Opcode.TYPE_GET: return TYPE_GET();
                case Opcode.TYPE_SET: return TYPE_SET();

                #endregion

                default: throw new InternalException("Unimplemented instruction " + CurrentInstruction.opcode.ToString());
            }
        }

        /// <exception cref="System.Exception"/>
        int GetStackAddress() => CurrentInstruction.AddressingMode switch
        {
            AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
            AddressingMode.RUNTIME => Memory.Stack.Pop().ValueInt,

            AddressingMode.BASEPOINTER_RELATIVE => BasePointer + CurrentInstruction.ParameterInt,
            AddressingMode.RELATIVE => Memory.Stack.Count + CurrentInstruction.ParameterInt,
            AddressingMode.POP => Memory.Stack.Count - 1,

            _ => throw new System.Exception($"Invalid stack addressing mode {CurrentInstruction.AddressingMode}"),
        };

        /// <exception cref="System.Exception"/>
        int GetAddress() => CurrentInstruction.AddressingMode switch
        {
            AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
            AddressingMode.RUNTIME => Memory.Stack.Pop().ValueInt,

            _ => throw new System.Exception($"Invalid heap addressing mode {CurrentInstruction.AddressingMode}"),
        };

        #region Instruction Methods

        #region HEAP Operations

        int HEAP_ALLOC()
        {
            DataItem sizeData = Memory.Stack.Pop();
            int size = sizeData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_ALLOC, got {sizeData.Type}");

            int block = Memory.Heap.Allocate(size);

            Memory.Stack.Push(new DataItem(block, CurrentInstruction.tag));

            Step();
            return 10;
        }

        int HEAP_DEALLOC()
        {
            DataItem pointerData = Memory.Stack.Pop();
            int pointer = pointerData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_DEALLOC, got {pointerData.Type}");

            Memory.Heap.Deallocate(pointer);

            Step();
            return 10;
        }

        int HEAP_GET()
        {
            int address = GetAddress();
            var value = Memory.Heap[address];
            Memory.Stack.Push(value);
            Step();

            return 2;
        }

        int HEAP_SET()
        {
            int address = GetAddress();
            var value = Memory.Stack.Pop();
            Memory.Heap[address] = value;
            Step();

            return 2;
        }

        #endregion

        #region Flow Control

        /// <exception cref="UserException"/>
        int THROW()
        {
            DataItem throwValue = Memory.Stack.Pop();
            string value = null;
            try
            { value = Memory.Heap.GetStringByPointer(throwValue.ValueInt); }
            catch (System.Exception) { }
            throw new UserException("User Exception Thrown", value);
        }

        int JUMP_BY()
        {
            int relativeAddress = GetAddress();

            Step(relativeAddress);

            return 1;
        }

        int JUMP_BY_IF_FALSE()
        {
            int relativeAddress = GetAddress();

            var condition = Memory.Stack.Pop();

            if (condition.IsFalsy())
            { Step(relativeAddress); }
            else
            { Step(); }

            return 3;
        }

        int EXIT()
        {
            CodePointer = Memory.Code.Length;

            return 1;
        }

        #endregion

        #region Debug Operations

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
            DataItem value = Memory.Stack.Pop();
            value.Tag = CurrentInstruction.tag;
            Memory.Stack.Push(value);
            Step();

            return 1;
        }

        #endregion

        #region Logic Operations

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

        int LOGIC_LT()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide < rightSide, null));

            Step();

            return 4;
        }

        int LOGIC_NOT()
        {
            var v = Memory.Stack.Pop();
            Memory.Stack.Push(!v);
            Step();

            return 3;
        }

        int LOGIC_MT()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide > rightSide, null));

            Step();

            return 4;
        }

        int LOGIC_EQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide == rightSide, null));

            Step();

            return 4;
        }

        int LOGIC_NEQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide != rightSide, null));

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

            Memory.Stack.Push(new DataItem(leftSide <= rightSide, null));

            Step();

            return 4;
        }

        int LOGIC_MTEQ()
        {
            var rightSide = Memory.Stack.Pop();
            var leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide >= rightSide, null));

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

        #endregion

        #region Math Operations

        int MATH_ADD()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide + rightSide);

            Step();

            return 4;
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

        #region Stack Operations

        int PUSH_VALUE()
        {
            DataItem value = CurrentInstruction.ParameterData;
            value.Tag = CurrentInstruction.tag;

            Memory.Stack.Push(value);

            Step();

            return 2;
        }

        int POP_VALUE()
        {
            Memory.Stack.Pop();
            Step();

            return 2;
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

            DataItem value = Memory.Stack[address];
            value.Tag = CurrentInstruction.tag ?? value.Tag;

            Memory.Stack.Push(value);

            Step();

            return 3;
        }

        #endregion

        #region Utility Operations

        int GET_BASEPOINTER()
        {
            Memory.Stack.Push(new DataItem(BasePointer, null));
            Step();
            return 1;
        }

        int SET_BASEPOINTER()
        {
            BasePointer = CurrentInstruction.AddressingMode switch
            {
                AddressingMode.RUNTIME => Memory.Stack.Pop().ValueInt,
                AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
                AddressingMode.RELATIVE => Memory.Stack.Count,
                _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SET_BASEPOINTER}"),
            };

            Step();
            return 1;
        }

        int SET_CODEPOINTER()
        {
            CodePointer = CurrentInstruction.AddressingMode switch
            {
                AddressingMode.RUNTIME => Memory.Stack.Pop().ValueInt,
                AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
                _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SET_CODEPOINTER}"),
            };

            return 1;
        }

        int TYPE_SET()
        {
            RuntimeType targetType = (RuntimeType)(Memory.Stack.Pop().Byte ?? throw new RuntimeException($"Expected byte as target type"));
            DataItem value = Memory.Stack.Pop();

            DataItem newValue;
            unchecked
            {
                newValue = targetType switch
                {
                    RuntimeType.BYTE => value.Type switch
                    {
                        RuntimeType.BYTE => new DataItem((byte)value.ValueByte, value.Tag),
                        RuntimeType.INT => new DataItem((byte)(value.ValueInt % byte.MaxValue), value.Tag),
                        RuntimeType.FLOAT => new DataItem((byte)MathF.Round(value.ValueFloat), value.Tag),
                        RuntimeType.CHAR => new DataItem((byte)value.ValueChar, value.Tag),
                        _ => throw new NotImplementedException(),
                    },
                    RuntimeType.INT => value.Type switch
                    {
                        RuntimeType.BYTE => new DataItem((int)value.ValueByte, value.Tag),
                        RuntimeType.INT => new DataItem((int)value.ValueInt, value.Tag),
                        RuntimeType.FLOAT => new DataItem((int)MathF.Round(value.ValueFloat), value.Tag),
                        RuntimeType.CHAR => new DataItem((int)value.ValueChar, value.Tag),
                        _ => throw new NotImplementedException(),
                    },
                    RuntimeType.FLOAT => value.Type switch
                    {
                        RuntimeType.BYTE => new DataItem((float)value.ValueByte, value.Tag),
                        RuntimeType.INT => new DataItem((float)value.ValueInt, value.Tag),
                        RuntimeType.FLOAT => new DataItem((float)MathF.Round(value.ValueFloat), value.Tag),
                        RuntimeType.CHAR => new DataItem((float)value.ValueChar, value.Tag),
                        _ => throw new NotImplementedException(),
                    },
                    RuntimeType.CHAR => value.Type switch
                    {
                        RuntimeType.BYTE => new DataItem((char)value.ValueByte, value.Tag),
                        RuntimeType.INT => new DataItem((char)value.ValueInt, value.Tag),
                        RuntimeType.FLOAT => new DataItem((char)MathF.Round(value.ValueFloat), value.Tag),
                        RuntimeType.CHAR => new DataItem((char)value.ValueChar, value.Tag),
                        _ => throw new NotImplementedException(),
                    },
                    _ => throw new NotImplementedException(),
                };
            }

            Memory.Stack.Push(newValue);

            Step();
            return 1;
        }

        int TYPE_GET()
        {
            DataItem value = Memory.Stack.Pop();
            byte type = (byte)value.Type;

            Memory.Stack.Push(new DataItem(type));

            Step();
            return 1;
        }

        #endregion

        #region External Calls

        void OnExternalReturnValue(DataItem returnValue)
        {
            returnValue.Tag ??= "return v";
            Memory.Stack.Push(returnValue);
        }

        /// <exception cref="InternalException"/>
        /// <exception cref="RuntimeException"/>
        int CALL_EXTERNAL()
        {
            DataItem functionNameDataItem = Memory.Stack.Pop();
            if (functionNameDataItem.Type != RuntimeType.INT)
            { throw new InternalException($"Instruction CALL_EXTERNAL need a Strint pointer (int) DataItem parameter from the stack, recived {functionNameDataItem.Type} {functionNameDataItem}"); }

            int functionNameLength = Memory.Stack.Pop().Integer ?? throw new InternalException();

            string functionName = (functionNameLength > 0) ? Memory.Heap.GetString(functionNameDataItem.ValueInt, functionNameLength) : Memory.Heap.GetStringByPointer(functionNameDataItem.ValueInt);

            if (!ExternalFunctions.TryGetValue(functionName, out ExternalFunctionBase function))
            { throw new RuntimeException($"Undefined function \"{functionName}\""); }

            List<DataItem> parameters = new();
            for (int i = 0; i < CurrentInstruction.ParameterInt; i++)
            { parameters.Add(Memory.Stack.Pop()); }

            if (function is ExternalFunctionManaged managedFunction)
            {
                managedFunction.OnReturn = OnExternalReturnValue;
                managedFunction.Callback(parameters.ToArray());
            }
            else if (function is ExternalFunctionSimple simpleFunction)
            {
                if (function.ReturnSomething)
                {
                    DataItem returnValue = simpleFunction.Callback(parameters.ToArray());
                    returnValue.Tag ??= $"{function.Name}() result";
                    Memory.Stack.Push(returnValue);
                }
                else
                {
                    simpleFunction.Callback(parameters.ToArray());
                }
            }

            Step();

            return 15;
        }

        #endregion

        #endregion
    }

    internal class Memory
    {
        internal DataStack Stack;
        internal HEAP Heap;
        internal Instruction[] Code;
        internal Stack<string> CallStack;

        public Memory(int heapSize, Instruction[] code)
        {
            Code = code;

            Stack = new DataStack();
            Heap = new HEAP(heapSize);

            CallStack = new Stack<string>();
        }
    }
}
