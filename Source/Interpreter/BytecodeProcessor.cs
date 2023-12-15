﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LanguageCore.Runtime
{
    public class BytecodeProcessor
    {
        public readonly Memory Memory;

        public Dictionary<string, ExternalFunctionBase> ExternalFunctions;

        public int CodePointer;
        public int BasePointer;

        public Instruction CurrentInstruction => Memory.Code[CodePointer];
        public bool IsDone => CodePointer >= Memory.Code.Length;

        public BytecodeProcessor(Instruction[] code, int heapSize, Dictionary<string, ExternalFunctionBase> externalFunctions)
        {
            ExternalFunctions = externalFunctions;

            BasePointer = 0;
            CodePointer = 0;

            Memory = new(heapSize, code);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Step() => Step(1);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Step(int num) => CodePointer += num;

        /// <exception cref="RuntimeException"></exception>
        /// <exception cref="UserException"></exception>
        /// <exception cref="InternalException"></exception>
        /// <exception cref="Exception"></exception>
        public void Process()
        {
            switch (CurrentInstruction.opcode)
            {
                case Opcode.UNKNOWN: throw new InternalException("Unknown instruction");

                case Opcode.EXIT: EXIT(); break;

                case Opcode.PUSH_VALUE: PUSH_VALUE(); break;
                case Opcode.POP_VALUE: POP_VALUE(); break;

                case Opcode.LOAD_VALUE: LOAD_VALUE(); break;
                case Opcode.STORE_VALUE: STORE_VALUE(); break;

                case Opcode.JUMP_BY_IF_FALSE: JUMP_BY_IF_FALSE(); break;
                case Opcode.JUMP_BY: JUMP_BY(); break;
                case Opcode.THROW: THROW(); break;

                case Opcode.CALL: CALL(); break;
                case Opcode.RETURN: RETURN(); break;

                case Opcode.CALL_EXTERNAL: CALL_EXTERNAL(); break;

                case Opcode.MATH_ADD: MATH_ADD(); break;
                case Opcode.MATH_SUB: MATH_SUB(); break;
                case Opcode.MATH_MULT: MATH_MULT(); break;
                case Opcode.MATH_DIV: MATH_DIV(); break;
                case Opcode.MATH_MOD: MATH_MOD(); break;

                case Opcode.BITS_SHIFT_LEFT: BITSHIFT_LEFT(); break;
                case Opcode.BITS_SHIFT_RIGHT: BITSHIFT_RIGHT(); break;

                case Opcode.BITS_AND: BITS_AND(); break;
                case Opcode.BITS_OR: BITS_OR(); break;
                case Opcode.BITS_XOR: BITS_XOR(); break;
                case Opcode.BITS_NOT: BITS_NOT(); break;

                case Opcode.LOGIC_LT: LOGIC_LT(); break;
                case Opcode.LOGIC_MT: LOGIC_MT(); break;
                case Opcode.LOGIC_EQ: LOGIC_EQ(); break;
                case Opcode.LOGIC_NEQ: LOGIC_NEQ(); break;
                case Opcode.LOGIC_LTEQ: LOGIC_LTEQ(); break;
                case Opcode.LOGIC_MTEQ: LOGIC_MTEQ(); break;
                case Opcode.LOGIC_NOT: LOGIC_NOT(); break;
                case Opcode.LOGIC_OR: LOGIC_OR(); break;
                case Opcode.LOGIC_AND: LOGIC_AND(); break;

                case Opcode.HEAP_GET: HEAP_GET(); break;
                case Opcode.HEAP_SET: HEAP_SET(); break;

                case Opcode.HEAP_ALLOC: HEAP_ALLOC(); break;
                case Opcode.HEAP_DEALLOC: HEAP_DEALLOC(); break;

                case Opcode.GET_BASEPOINTER: GET_BASEPOINTER(); break;
                case Opcode.SET_BASEPOINTER: SET_BASEPOINTER(); break;

                case Opcode.SET_CODEPOINTER: SET_CODEPOINTER(); break;

                case Opcode.TYPE_GET: TYPE_GET(); break;
                case Opcode.TYPE_SET: TYPE_SET(); break;

                default: throw new ImpossibleException();
            }
        }

        /// <exception cref="InternalException"/>
        int GetStackAddress() => CurrentInstruction.AddressingMode switch
        {
            AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
            AddressingMode.RUNTIME => Memory.Stack.Pop().ValueSInt32,

            AddressingMode.BASEPOINTER_RELATIVE => BasePointer + CurrentInstruction.ParameterInt,
            AddressingMode.RELATIVE => Memory.Stack.Count + CurrentInstruction.ParameterInt,
            AddressingMode.POP => Memory.Stack.Count - 1,

            _ => throw new InternalException($"Invalid stack addressing mode {CurrentInstruction.AddressingMode}"),
        };

        /// <exception cref="InternalException"/>
        int GetAddress() => CurrentInstruction.AddressingMode switch
        {
            AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
            AddressingMode.RUNTIME => Memory.Stack.Pop().ValueSInt32,

            _ => throw new InternalException($"Invalid addressing mode {CurrentInstruction.AddressingMode}"),
        };

        /// <exception cref="InternalException"/>
        DataItem GetData() => CurrentInstruction.AddressingMode switch
        {
            AddressingMode.ABSOLUTE => CurrentInstruction.Parameter,
            AddressingMode.RUNTIME => Memory.Stack.Pop(),

            _ => throw new InternalException($"Invalid addressing mode {CurrentInstruction.AddressingMode}"),
        };

        #region Instruction Methods

        #region HEAP Operations

        void HEAP_ALLOC()
        {
            DataItem sizeData = Memory.Stack.Pop();
            int size = sizeData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_ALLOC, got {sizeData.Type}");

            int block = Memory.Heap.Allocate(size);

            Memory.Stack.Push(new DataItem(block));

            Step();
        }

        void HEAP_DEALLOC()
        {
            DataItem pointerData = Memory.Stack.Pop();
            int pointer = pointerData.Integer ?? throw new RuntimeException($"Expected an integer parameter for opcode HEAP_DEALLOC, got {pointerData.Type}");

            Memory.Heap.Deallocate(pointer);

            Step();
        }

        void HEAP_GET()
        {
            int address = GetData().ValueSInt32;
            DataItem value = Memory.Heap[address];
            Memory.Stack.Push(value);
            Step();
        }

        void HEAP_SET()
        {
            int address = GetData().ValueSInt32;
            DataItem value = Memory.Stack.Pop();
            Memory.Heap[address] = value;
            Step();
        }

        #endregion

        #region Flow Control

        /// <exception cref="UserException"/>
        void THROW()
        {
            int pointer = Memory.Stack.Pop().ValueSInt32;
            string? value = null;
            try
            {
                value = Memory.Heap.GetString(pointer + 1, Memory.Heap[pointer].Integer ?? 0);
                Memory.Heap.Deallocate(pointer);
            }
            catch (Exception) { }
            throw new UserException(value ?? "null");
        }

        void CALL()
        {
            int relativeAddress = GetData().ValueSInt32;

            Memory.Stack.Push(new DataItem(CodePointer));

            Step(relativeAddress);
        }

        void RETURN()
        {
            DataItem codePointer = Memory.Stack.Pop();

            CodePointer = codePointer.ValueSInt32;
        }

        void JUMP_BY()
        {
            int relativeAddress = GetData().ValueSInt32;

            Step(relativeAddress);
        }

        void JUMP_BY_IF_FALSE()
        {
            int relativeAddress = GetData().ValueSInt32;

            DataItem condition = Memory.Stack.Pop();

            if (condition.Boolean)
            { Step(); }
            else
            { Step(relativeAddress); }
        }

        void EXIT()
        {
            CodePointer = Memory.Code.Length;
        }

        #endregion

        #region Logic Operations

        void BITSHIFT_LEFT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide << rightSide);

            Step();
        }

        void BITSHIFT_RIGHT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide >> rightSide);

            Step();
        }

        void LOGIC_LT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide < rightSide));

            Step();
        }

        void LOGIC_NOT()
        {
            DataItem v = Memory.Stack.Pop();
            Memory.Stack.Push(!v);
            Step();
        }

        void LOGIC_MT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide > rightSide));

            Step();
        }

        void LOGIC_AND()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide.Boolean && rightSide.Boolean));

            Step();
        }

        void LOGIC_OR()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide.Boolean || rightSide.Boolean));

            Step();
        }

        void LOGIC_EQ()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide == rightSide));

            Step();
        }

        void LOGIC_NEQ()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide != rightSide));

            Step();
        }

        void BITS_OR()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide | rightSide);

            Step();
        }

        void BITS_XOR()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide ^ rightSide);

            Step();
        }

        void BITS_NOT()
        {
            DataItem rightSide = Memory.Stack.Pop();

            Memory.Stack.Push(~rightSide);

            Step();
        }

        void LOGIC_LTEQ()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide <= rightSide));

            Step();
        }

        void LOGIC_MTEQ()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(new DataItem(leftSide >= rightSide));

            Step();
        }

        void BITS_AND()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide & rightSide);

            Step();
        }

        #endregion

        #region Math Operations

        void MATH_ADD()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide + rightSide);

            Step();
        }

        void MATH_DIV()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide / rightSide);

            Step();
        }

        void MATH_SUB()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide - rightSide);

            Step();
        }

        void MATH_MULT()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide * rightSide);

            Step();
        }

        void MATH_MOD()
        {
            DataItem rightSide = Memory.Stack.Pop();
            DataItem leftSide = Memory.Stack.Pop();

            Memory.Stack.Push(leftSide % rightSide);

            Step();
        }

        #endregion

        #region Stack Operations

        void PUSH_VALUE()
        {
            DataItem value = CurrentInstruction.Parameter;
            Memory.Stack.Push(value);
            Step();
        }

        void POP_VALUE()
        {
            Memory.Stack.Pop();
            Step();
        }

        void STORE_VALUE()
        {
            int address = GetStackAddress();
            DataItem value = Memory.Stack.Pop();

            Memory.Stack[address] = value;

            Step();
        }

        void LOAD_VALUE()
        {
            int address = GetStackAddress();

            DataItem value = Memory.Stack[address];

            Memory.Stack.Push(value);

            Step();
        }

        #endregion

        #region Utility Operations

        void GET_BASEPOINTER()
        {
            Memory.Stack.Push(new DataItem(BasePointer));
            Step();
        }

        void SET_BASEPOINTER()
        {
            BasePointer = CurrentInstruction.AddressingMode switch
            {
                AddressingMode.RUNTIME => Memory.Stack.Pop().ValueSInt32,
                AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
                AddressingMode.RELATIVE => Memory.Stack.Count + CurrentInstruction.ParameterInt,
                _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SET_BASEPOINTER}"),
            };

            Step();
        }

        void SET_CODEPOINTER()
        {
            CodePointer = CurrentInstruction.AddressingMode switch
            {
                AddressingMode.RUNTIME => Memory.Stack.Pop().ValueSInt32,
                AddressingMode.ABSOLUTE => CurrentInstruction.ParameterInt,
                _ => throw new RuntimeException($"Invalid {nameof(AddressingMode)} {CurrentInstruction.AddressingMode} for instruction {Opcode.SET_CODEPOINTER}"),
            };
        }

        void TYPE_SET()
        {
            RuntimeType targetType = (RuntimeType)(Memory.Stack.Pop().Byte ?? throw new RuntimeException($"Expected byte as target type"));
            DataItem value = Memory.Stack.Pop();

            DataItem newValue;
            unchecked
            {
                newValue = targetType switch
                {
                    RuntimeType.UInt8 => value.Type switch
                    {
                        RuntimeType.UInt8 => new DataItem((byte)value.ValueUInt8),
                        RuntimeType.SInt32 => new DataItem((byte)(value.ValueSInt32 % byte.MaxValue)),
                        RuntimeType.Single => new DataItem((byte)MathF.Round(value.ValueSingle)),
                        RuntimeType.UInt16 => new DataItem((byte)value.ValueUInt16),
                        _ => throw new ImpossibleException(),
                    },
                    RuntimeType.SInt32 => value.Type switch
                    {
                        RuntimeType.UInt8 => new DataItem((int)value.ValueUInt8),
                        RuntimeType.SInt32 => new DataItem((int)value.ValueSInt32),
                        RuntimeType.Single => new DataItem((int)MathF.Round(value.ValueSingle)),
                        RuntimeType.UInt16 => new DataItem((int)value.ValueUInt16),
                        _ => throw new ImpossibleException(),
                    },
                    RuntimeType.Single => value.Type switch
                    {
                        RuntimeType.UInt8 => new DataItem((float)value.ValueUInt8),
                        RuntimeType.SInt32 => new DataItem((float)value.ValueSInt32),
                        RuntimeType.Single => new DataItem((float)MathF.Round(value.ValueSingle)),
                        RuntimeType.UInt16 => new DataItem((float)value.ValueUInt16),
                        _ => throw new ImpossibleException(),
                    },
                    RuntimeType.UInt16 => value.Type switch
                    {
                        RuntimeType.UInt8 => new DataItem((char)value.ValueUInt8),
                        RuntimeType.SInt32 => new DataItem((char)value.ValueSInt32),
                        RuntimeType.Single => new DataItem((char)MathF.Round(value.ValueSingle)),
                        RuntimeType.UInt16 => new DataItem((char)value.ValueUInt16),
                        _ => throw new ImpossibleException(),
                    },
                    _ => throw new ImpossibleException(),
                };
            }

            Memory.Stack.Push(newValue);

            Step();
        }

        void TYPE_GET()
        {
            DataItem value = Memory.Stack.Pop();
            byte type = (byte)value.Type;

            Memory.Stack.Push(new DataItem(type));

            Step();
        }

        #endregion

        #region External Calls

        void OnExternalReturnValue(DataItem returnValue)
        {
            // returnValue.Tag ??= "return v";
            Memory.Stack.Push(returnValue);
        }

        /// <exception cref="InternalException"/>
        /// <exception cref="RuntimeException"/>
        void CALL_EXTERNAL()
        {
            DataItem functionNameDataItem = Memory.Stack.Pop();
            if (functionNameDataItem.Type != RuntimeType.SInt32)
            { throw new InternalException($"Instruction CALL_EXTERNAL need a String pointer (int) DataItem parameter from the stack, received {functionNameDataItem.Type} {functionNameDataItem}"); }

            string functionName = Memory.Heap.GetString(functionNameDataItem.ValueSInt32 + 1, Memory.Heap[functionNameDataItem.ValueSInt32].ValueSInt32);

            if (!ExternalFunctions.TryGetValue(functionName, out ExternalFunctionBase? function))
            { throw new RuntimeException($"Undefined function \"{functionName}\""); }

            int parameterCount = CurrentInstruction.ParameterInt;

            List<DataItem> parameters = new();
            for (int i = 1; i <= CurrentInstruction.ParameterInt; i++)
            { parameters.Add(Memory.Stack[^i]); }
            parameters.Reverse();

            if (function is ExternalFunctionManaged managedFunction)
            {
                managedFunction.OnReturn = OnExternalReturnValue;
                managedFunction.Callback(parameters.ToArray());
            }
            else if (function is ExternalFunctionSimple simpleFunction)
            {
                if (function.ReturnSomething)
                {
                    DataItem returnValue = simpleFunction.Callback(this, parameters.ToArray());
                    // returnValue.Tag ??= $"{function.Name}() result";
                    Memory.Stack.Push(returnValue);
                }
                else
                {
                    simpleFunction.Callback(this, parameters.ToArray());
                }
            }

            Step();
        }

        #endregion

        #endregion
    }

    public class Memory
    {
        public DataStack Stack;
        public HEAP Heap;
        public Instruction[] Code;

        public Memory(int heapSize, Instruction[] code)
        {
            Code = code;

            Stack = new DataStack();
            Heap = new HEAP(heapSize);
        }
    }
}
