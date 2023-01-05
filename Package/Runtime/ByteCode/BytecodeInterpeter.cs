using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IngameCoding.Bytecode
{
    using Core;

    using Errors;

    static class ItemEx
    {
        public static string[] ToStringArray(this DataItem[] items)
        {
            string[] strings = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                strings[i] = items[i].ToString();
            }
            return strings;
        }
    }

    internal class CentralProcessingUnit
    {
        internal readonly ControlUnit CU;
        internal readonly MemoryUnit MU;

        internal Dictionary<string, BuiltinFunction> builtinFunctions;

        public int CodePointer
        {
            get { return MU.CodePointer; }
        }

        public CentralProcessingUnit(Instruction[] code, int basePointer, Dictionary<string, BuiltinFunction> builtinFunctions)
        {
            for (int i = 0; i < code.Length; i++)
            {
                code[i].index = i;
                code[i].cpu = this;
            }
            this.builtinFunctions = builtinFunctions;

            MU = new(
                new DataStack() { cpu = this },
                new HEAP(),
                code
            )
            {
                BasePointer = basePointer,
                ReturnAddressStack = new List<int>(),
                CodePointer = code.Length
            };
            CU = new(MU, this);
        }

        public int Clock()
        {
            return CU.Process();
        }

        internal static string GetTypeText(DataItem.Type type)
        {
            return type switch
            {
                DataItem.Type.INT => "int",
                DataItem.Type.FLOAT => "float",
                DataItem.Type.STRING => "string",
                DataItem.Type.BOOLEAN => "bool",
                DataItem.Type.STRUCT => "complex",
                _ => "",
            };
        }
        internal static string GetTypeText(DataItem val) => GetTypeText(val.ValueList.itemTypes) + (val.type == DataItem.Type.LIST ? "[]" : "");

        public void Destroy()
        {
            MU.Stack.Destroy();
            MU.ReturnAddressStack.Clear();
            MU.Stack = null;
            MU.ReturnAddressStack = null;
            this.builtinFunctions = null;
        }
    }

    internal class ControlUnit
    {
        readonly MemoryUnit MU;
        readonly CentralProcessingUnit CPU;

        public ControlUnit(MemoryUnit mu, CentralProcessingUnit cpu)
        {
            MU = mu;
            CPU = cpu;
        }

        internal int Process()
        {
            switch (MU.CurrentInstruction.opcode)
            {
                case Opcode.EXIT:
                    return EXIT();
                case Opcode.PUSH_VALUE:
                    return PUSH_VALUE();
                case Opcode.JUMP_BY_IF_TRUE:
                    return JUMP_BY_IF_TRUE();
                case Opcode.JUMP_BY_IF_FALSE:
                    return JUMP_BY_IF_FALSE();
                case Opcode.JUMP_BY:
                    return JUMP_BY();
                case Opcode.LOAD_VALUE:
                    return LOAD_VALUE();
                case Opcode.STORE_VALUE:
                    return STORE_VALUE();
                case Opcode.LOAD_VALUE_BR:
                    return LOAD_VALUE_BR();
                case Opcode.LOAD_VALUE_R:
                    return LOAD_VALUE_R();
                case Opcode.STORE_VALUE_BR:
                    return STORE_VALUE_BR();
                case Opcode.STORE_VALUE_R:
                    return STORE_VALUE_R();
                case Opcode.LOAD_FIELD:
                    return LOAD_FIELD();
                case Opcode.STORE_FIELD:
                    return STORE_FIELD();
                case Opcode.LOAD_FIELD_R:
                    return LOAD_FIELD_R();
                case Opcode.LOAD_FIELD_BR:
                    return LOAD_FIELD_BR();
                case Opcode.STORE_FIELD_BR:
                    return STORE_FIELD_BR();
                case Opcode.CALL:
                    return CALL();
                case Opcode.RETURN:
                    return RETURN();
                case Opcode.POP_VALUE:
                    return POP_VALUE();
                case Opcode.CALL_BUILTIN:
                    return CALL_BUILTIN();
                case Opcode.MATH_ADD:
                    return MATH_ADD();
                case Opcode.MATH_SUB:
                    return MATH_SUB();
                case Opcode.MATH_MULT:
                    return MATH_MULT();
                case Opcode.MATH_DIV:
                    return MATH_DIV();
                case Opcode.MATH_MOD:
                    return MATH_MOD();
                case Opcode.LOGIC_LT:
                    return LOGIC_LT();
                case Opcode.LOGIC_MT:
                    return LOGIC_MT();
                case Opcode.LOGIC_AND:
                    return LOGIC_AND();
                case Opcode.LOGIC_OR:
                    return LOGIC_OR();
                case Opcode.LOGIC_EQ:
                    return LOGIC_EQ();
                case Opcode.LOGIC_NEQ:
                    return LOGIC_NEQ();
                case Opcode.LOGIC_LTEQ:
                    return LOGIC_LTEQ();
                case Opcode.LOGIC_MTEQ:
                    return LOGIC_MTEQ();
                case Opcode.LOGIC_XOR:
                    return LOGIC_XOR();
                case Opcode.COMMENT:
                    MU.Step();
                    return 1;
                case Opcode.LIST_INDEX:
                    return LIST_INDEX();
                case Opcode.LIST_PUSH_ITEM:
                    return LIST_PUSH_ITEM();
                case Opcode.LIST_ADD_ITEM:
                    return LIST_ADD_ITEM();
                case Opcode.LIST_PULL_ITEM:
                    return LIST_PULL_ITEM();
                case Opcode.LIST_REMOVE_ITEM:
                    return LIST_REMOVE_ITEM();
                case Opcode.TYPE_GET:
                    return TYPE_GET();
                case Opcode.LOAD_VALUE_AS_REF:
                    return LOAD_VALUE_AS_REF();
                case Opcode.STORE_VALUE_AS_REF:
                    return STORE_VALUE_AS_REF();
                case Opcode.LOAD_VALUE_BR_AS_REF:
                    return LOAD_VALUE_BR_AS_REF();
                case Opcode.STORE_VALUE_BR_AS_REF:
                    return STORE_VALUE_BR_AS_REF();
                case Opcode.LOGIC_NOT:
                    return LOGIC_NOT();
                case Opcode.UNKNOWN:
                    throw new InternalException("Unknown command");
                case Opcode.HEAP_GET:
                    return HEAP_GET();
                case Opcode.HEAP_SET:
                    return HEAP_SET();
                case Opcode.DEBUG_SET_TAG:
                    return DEBUG_SET_TAG();
                case Opcode.CS_PUSH:
                    return CS_PUSH();
                case Opcode.CS_POP:
                    return CS_POP();
                default:
                    throw new InternalException("Unimplemented operation " + MU.CurrentInstruction.opcode.ToString());
            }
        }

        #region Commands

        int CS_PUSH()
        {
            MU.CallStack.Push(MU.CurrentInstruction.parameter.ToString());
            MU.Step();

            return 1;
        }

        int CS_POP()
        {
            MU.CallStack.Pop();
            MU.Step();

            return 1;
        }

        int DEBUG_SET_TAG()
        {
            var last = MU.Stack.Last();
            last.Tag = MU.CurrentInstruction.parameter.ToString();
            MU.Stack.Set(MU.Stack.Count - 1, last, true);
            MU.Step();

            return 1;
        }

        int HEAP_GET()
        {
            var v = MU.Heap[(int)MU.CurrentInstruction.parameter];
            MU.Stack.Push(v);
            MU.Step();

            return 2;
        }

        int HEAP_SET()
        {
            var v = MU.Stack.Pop();
            MU.Heap[(int)MU.CurrentInstruction.parameter] = v;
            MU.Step();

            return 2;
        }

        int LOGIC_NOT()
        {
            var v = MU.Stack.Pop();
            MU.Stack.Push(!v);
            MU.Step();

            return 3;
        }

        int STORE_VALUE_AS_REF()
        {
            MU.Stack.Set(MU.Stack.Get((int)MU.CurrentInstruction.parameter).GetRefIndex(), MU.Stack.Last());
            MU.Stack.RemoveAt(MU.Stack.Count - 1);
            MU.Step();

            return 3;
        }
        int LOAD_VALUE_AS_REF()
        {
            var item = MU.Stack.Get((int)MU.CurrentInstruction.parameter);
            item.ValueRef = (int)MU.CurrentInstruction.parameter;
            MU.Stack.Push(item, null);
            MU.Step();

            return 4;
        }

        int STORE_VALUE_BR_AS_REF()
        {
            var index = (int)MU.CurrentInstruction.parameter + MU.BasePointer;
            var item = MU.Stack.Get(index);
            var refIndex = item.GetRefIndex();
            if (refIndex == -1)
            {
                refIndex = index;
            }
            MU.Stack.Set(refIndex, MU.Stack.Last());
            MU.Stack.Pop();
            MU.Step();

            return 5;
        }
        int LOAD_VALUE_BR_AS_REF()
        {
            var item = MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.BasePointer);
            item.ValueRef = (int)MU.CurrentInstruction.parameter + MU.BasePointer;
            MU.Stack.Push(item, null);
            MU.Step();

            return 4;
        }

        int TYPE_GET()
        {
            var v = MU.Stack.Pop();
            MU.Stack.Push(CentralProcessingUnit.GetTypeText(v), "type() result");
            MU.Step();

            return 3;
        }

        int LIST_PUSH_ITEM()
        {
            var newItem = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Add(newItem); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Step();

            return 4;
        }
        int LIST_ADD_ITEM()
        {
            var indexValue = MU.Stack.Pop();
            var newItem = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Add(newItem, indexValue.ValueInt); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Step();

            return 5;
        }
        int LIST_PULL_ITEM()
        {
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Remove(); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Step();

            return 3;
        }
        int LIST_REMOVE_ITEM()
        {
            var indexValue = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();
            if (listValue.type == DataItem.Type.LIST)
            { listValue.ValueList.Remove(indexValue.ValueInt); }
            else
            { throw new RuntimeException("The variable type is not list!"); }
            MU.Step();

            return 4;
        }

        int LIST_INDEX()
        {
            var indexValue = MU.Stack.Pop();
            var listValue = MU.Stack.Pop();

            if (listValue.type == DataItem.Type.LIST)
            {
                if (listValue.ValueList.items.Count <= indexValue.ValueInt || indexValue.ValueInt < 0)
                { throw new RuntimeException("Index was out of range!"); }
                MU.Stack.Push(listValue.ValueList.items[indexValue.ValueInt]);
            }
            else if (listValue.type == DataItem.Type.STRING)
            {
                if (listValue.ValueString.Length <= indexValue.ValueInt || indexValue.ValueInt < 0)
                { throw new RuntimeException("Index was out of range!"); }
                MU.Stack.Push(listValue.ValueString[indexValue.ValueInt].ToString());
            }
            else
            { throw new RuntimeException("The variable type is not list or string!"); }
            MU.Step();

            return 4;
        }

        void OnBuiltinFunctionReturnValue(DataItem returnValue)
        {
            Output.Debug.Debug.Log(returnValue.ToString());
            MU.Stack.Push(returnValue, "return v");
        }

        int CALL_BUILTIN()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No function name given"); }

            if (CPU.builtinFunctions.TryGetValue(MU.CurrentInstruction.additionParameter, out BuiltinFunction builtinFunction))
            {
                List<DataItem> parameters = new();
                for (int i = 0; i < (int)MU.CurrentInstruction.parameter; i++)
                { parameters.Add(MU.Stack.Pop()); }
                if (builtinFunction.ReturnSomething)
                {
                    builtinFunction.OnReturn += OnBuiltinFunctionReturnValue;
                    builtinFunction.Callback(parameters.ToArray());
                    builtinFunction.OnReturn -= OnBuiltinFunctionReturnValue;
                }
                else
                { builtinFunction.Callback(parameters.ToArray()); }
            }
            else
            { throw new RuntimeException("Undefined function \"" + MU.CurrentInstruction.additionParameter + "\""); }

            MU.Step();

            return 15;
        }

        int LOGIC_MT()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide > rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_EQ()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide == rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_NEQ()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide != rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_OR()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide | rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_XOR()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide ^ rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_LTEQ()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide <= rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_MTEQ()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide >= rightSide);

            MU.Step();

            return 4;
        }
        int LOGIC_AND()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide & rightSide);

            MU.Step();

            return 4;
        }

        int POP_VALUE()
        {
            if (MU.Stack.Count > 0) MU.Stack.RemoveAt(MU.Stack.Count - 1);
            MU.Step();

            return 2;
        }

        int RETURN()
        {
            int returnAddress = MU.ReturnAddressStack[^1];
            MU.ReturnAddressStack.RemoveAt(MU.ReturnAddressStack.Count - 1);
            MU.BasePointer = MU.Stack.Pop().ValueInt;
            MU.CodePointer = returnAddress;

            return 3;
        }
        int CALL()
        {
            MU.Stack.Push(MU.BasePointer, "saved base pointer");
            MU.ReturnAddressStack.Add(MU.CodePointer + 1);
            MU.BasePointer = MU.Stack.Count;
            MU.Step((int)MU.CurrentInstruction.parameter);

            return 4;
        }

        int STORE_VALUE_BR()
        {
            var index = (int)MU.CurrentInstruction.parameter + MU.BasePointer;
            var itemToSet = MU.Stack.Get(index).TrySet(MU.Stack.Last());
            MU.Stack.Set(index, itemToSet);

            MU.Stack.Pop();
            MU.Step();

            return 5;
        }
        int LOAD_VALUE_BR()
        {
            MU.Stack.Push(MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.BasePointer), MU.CurrentInstruction.tag);

            MU.Step();

            return 3;
        }

        int STORE_VALUE_R()
        {
            var index = (int)MU.CurrentInstruction.parameter + MU.Stack.Count;
            var itemToSet = MU.Stack.Get(index).TrySet(MU.Stack.Last());
            MU.Stack.Set(index, itemToSet);

            MU.Stack.Pop();
            MU.Step();

            return 5;
        }
        int LOAD_VALUE_R()
        {
            MU.Stack.Push(MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.Stack.Count), MU.CurrentInstruction.tag);

            MU.Step();

            return 3;
        }

        int STORE_FIELD_BR()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            var valueToSet = MU.Stack.Pop();
            string fieldToSet = MU.CurrentInstruction.additionParameter;
            var structItem = MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.BasePointer).ValueStruct;

            if (!structItem.HaveField(fieldToSet))
            { throw new RuntimeException("Field " + fieldToSet + " doesn't exists in this struct."); }

            structItem.SetField(fieldToSet, structItem.GetField(fieldToSet).TrySet(valueToSet));

            MU.Stack.Set((int)MU.CurrentInstruction.parameter + MU.BasePointer, new DataItem(structItem, null));

            MU.Step();

            return 6;
        }
        int LOAD_FIELD_BR()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            var item = MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.BasePointer);
            var field = MU.CurrentInstruction.additionParameter;

            if (item.type == DataItem.Type.STRING)
            {
                var value = item.ValueString;

                if (field == "Length")
                {
                    MU.Stack.Push(value.Length, MU.CurrentInstruction.tag);
                }
                else
                { throw new RuntimeException("Type string does not have field " + field); }
            }
            else if (item.type == DataItem.Type.STRUCT)
            {
                var value = item.ValueStruct;

                if (!value.HaveField(field))
                { throw new RuntimeException("Field " + field + " doesn't exists in this struct."); }

                MU.Stack.Push(value.GetField(field), MU.CurrentInstruction.tag);
            }
            else
            { throw new RuntimeException("Type " + item.type.ToString().ToLower() + " does not have field " + field); }

            MU.Step();

            return 4;
        }

        int JUMP_BY()
        {
            MU.CodePointer += (int)MU.CurrentInstruction.parameter;

            return 1;
        }
        int JUMP_BY_IF_TRUE()
        {
            var condition = MU.Stack.Pop();

            if (condition == true)
            { MU.CodePointer += (int)MU.CurrentInstruction.parameter; }
            else
            { MU.Step(); }

            return 3;
        }
        int JUMP_BY_IF_FALSE()
        {
            var condition = MU.Stack.Pop();

            if (condition == false)
            { MU.CodePointer += (int)MU.CurrentInstruction.parameter; }
            else
            { MU.Step(); }

            return 3;
        }

        int STORE_VALUE()
        {
            MU.Stack.Set((int)MU.CurrentInstruction.parameter, MU.Stack.Last());

            MU.Stack.RemoveAt(MU.Stack.Count - 1);
            MU.Step();

            return 3;
        }
        int LOAD_VALUE()
        {
            MU.Stack.Push(MU.Stack.Get((int)MU.CurrentInstruction.parameter), MU.CurrentInstruction.tag);

            MU.Step();

            return 3;
        }

        int LOAD_FIELD_R()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            var structItem = MU.Stack.Get((int)MU.CurrentInstruction.parameter + MU.Stack.Count).ValueStruct;
            MU.Stack.Pop();

            if (!structItem.HaveField(MU.CurrentInstruction.additionParameter))
            { throw new RuntimeException("Field " + MU.CurrentInstruction.additionParameter + " doesn't exists in this struct."); }

            var fieldValue = structItem.GetField(MU.CurrentInstruction.additionParameter);

            MU.Stack.Push(fieldValue, "field." + MU.CurrentInstruction.additionParameter);

            MU.Step();

            return 4;
        }

        int STORE_FIELD()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            DataItem valueToSet = MU.Stack.Pop();
            string fieldToSet = MU.CurrentInstruction.additionParameter;
            var structItem = MU.Stack.Get((int)MU.CurrentInstruction.parameter).ValueStruct;

            if (!structItem.HaveField(fieldToSet))
            { throw new RuntimeException("Field " + fieldToSet + " doesn't exists in this struct."); }

            structItem.SetField(fieldToSet, structItem.GetField(fieldToSet).TrySet(valueToSet));

            MU.Stack.Set((int)MU.CurrentInstruction.parameter, new DataItem(structItem, null));

            MU.Step();

            return 7;
        }
        int LOAD_FIELD()
        {
            if (MU.CurrentInstruction.additionParameter.Length == 0)
            { throw new InternalException("No field name given"); }

            var structItem = MU.Stack.Get((int)MU.CurrentInstruction.parameter).ValueStruct;

            if (!structItem.HaveField(MU.CurrentInstruction.additionParameter))
            { throw new RuntimeException("Field " + MU.CurrentInstruction.additionParameter + " doesn't exists in this struct."); }

            MU.Stack.Push(structItem.GetField(MU.CurrentInstruction.additionParameter), "field." + MU.CurrentInstruction.additionParameter);

            MU.Step();

            return 4;
        }

        int LOGIC_LT()
        {
            var rightSide = MU.Stack.Pop();
            var leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide < rightSide);

            MU.Step();

            return 4;
        }

        int PUSH_VALUE()
        {
            MU.Stack.Push(new DataItem(MU.CurrentInstruction.parameter, MU.CurrentInstruction.tag), MU.CurrentInstruction.tag);

            MU.Step();

            return 2;
        }

        int MATH_ADD()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide + rightSide);

            MU.Step();

            return 4;
        }

        int EXIT()
        {
            MU.CodePointer = MU.Code.Length;

            return 1;
        }

        int MATH_DIV()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide / rightSide);

            MU.Step();

            return 4;
        }

        int MATH_SUB()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide - rightSide);

            MU.Step();

            return 4;
        }

        int MATH_MULT()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide * rightSide);

            MU.Step();

            return 4;
        }

        int MATH_MOD()
        {
            DataItem rightSide = MU.Stack.Pop();
            DataItem leftSide = MU.Stack.Pop();

            MU.Stack.Push(leftSide % rightSide);

            MU.Step();

            return 4;
        }

        #endregion
    }

#if false
    internal static class ArithmeticLogicUnit
    {
        internal enum OpCode
        {
            Add,
            Subtract,
            Negate,
            Increment,
            Decrement,
            And,
            Or,
            Xor,
            Not,
        }

        internal static int Process(OpCode opCode, int A, int B)
        {
            return opCode switch
            {
                OpCode.Add => A + B,
                OpCode.Subtract => A - B,
                OpCode.Negate => 0 - A,
                OpCode.Increment => A + 1,
                OpCode.Decrement => A - 1,
                OpCode.And => A & B,
                OpCode.Or => A | B,
                OpCode.Xor => A ^ B,
                OpCode.Not => ~A,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }

        internal static bool Process(OpCode opCode, bool A)
        {
            return opCode switch
            {
                OpCode.Not => !A,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }

        internal static bool Process(OpCode opCode, bool A, bool B)
        {
            return opCode switch
            {
                OpCode.And => A & B,
                OpCode.Or => A | B,
                OpCode.Xor => A ^ B,
                OpCode.Not => !A,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }

        internal static float Process(OpCode opCode, float A, float B)
        {
            return opCode switch
            {
                OpCode.Add => A + B,
                OpCode.Subtract => A - B,
                OpCode.Negate => 0 - A,
                OpCode.Increment => A + 1,
                OpCode.Decrement => A - 1,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }

        internal static int Process(OpCode opCode, int A)
        {
            return opCode switch
            {
                OpCode.Negate => 0 - A,
                OpCode.Increment => A + 1,
                OpCode.Decrement => A - 1,
                OpCode.Not => ~A,
                _ => throw new SystemException($"Invalid ALU OpCode '{opCode}'"),
            };
        }
    }
#endif

    internal class MemoryUnit
    {
        internal DataStack Stack;
        internal HEAP Heap;
        internal List<int> ReturnAddressStack;
        internal Instruction[] Code;
        internal Stack<string> CallStack;

        internal int CodePointer;
        internal int BasePointer;

        internal Instruction CurrentInstruction => Code[CodePointer];

        public MemoryUnit(DataStack stack, HEAP heap, Instruction[] code)
        {
            Stack = stack;
            Code = code;
            Heap = heap;
            CallStack = new Stack<string>();
        }

        public int End() => Code.Length;

        public void Step() => Step(1);
        public void Step(int num) => CodePointer += num;
    }

    public class Stack<T>
    {
        protected readonly List<T> stack;

        public Stack() => stack = new List<T>();

        /// <summary>
        /// Gives the last item, and then remove
        /// </summary>
        /// <returns>The last item</returns>
        public virtual T Pop()
        {
            T val = this.stack[^1];
            this.stack.RemoveAt(this.stack.Count - 1);
            return val;
        }
        /// <returns>Adds a new item to the end</returns>
        public virtual void Push(T value) => this.stack.Add(value);
        /// <summary>Removes a specific item</summary>
        public virtual void RemoveAt(int index) => this.stack.RemoveAt(index);
        /// <returns>The number of items</returns>
        public virtual int Count => this.stack.Count;
        /// <summary>Adds a list to the end</summary>
        public virtual void PushRange(List<T> list) => PushRange(list.ToArray());
        /// <summary>Adds an array to the end</summary>
        public virtual void PushRange(T[] list)
        { foreach (T item in list) Push(item); }
        /// <returns>The last item</returns>
        public virtual T Last() => this.stack.Last();

        public virtual T[] ToArray() => this.stack.ToArray();

        public virtual T this[int i]
        {
            get => this.stack[i];
            set => this.stack[i] = value;
        }
    }

    public class DataStack : Stack<DataItem>
    {
        internal int UsedVirtualMemory
        {
            get
            {
                static int CalculateItemSize(DataItem item)
                {
                    switch (item.type)
                    {
                        case DataItem.Type.INT:
                            return 4;
                        case DataItem.Type.FLOAT:
                            return 4;
                        case DataItem.Type.STRING:
                            return 4 + System.Text.ASCIIEncoding.ASCII.GetByteCount(item.ValueString);
                        case DataItem.Type.BOOLEAN:
                            return 1;
                        case DataItem.Type.STRUCT:
                            if (item.ValueStruct is DataItem.Struct valStruct)
                            {
                                int result = 0;
                                foreach (var field in valStruct.fields)
                                {
                                    result += System.Text.ASCIIEncoding.ASCII.GetByteCount(field.Key);
                                    result += CalculateItemSize(field.Value);
                                }
                                result += 4;
                                return result;
                            }
                            break;
                        case DataItem.Type.LIST:
                            {
                                var result = 0;
                                foreach (var element in item.ValueList.items)
                                { result += CalculateItemSize(element); }
                                result += 4;
                                return result;
                            }
                    }
                    return 0;
                }

                int result = 0;
                for (int i = 0; i < stack.Count; i++)
                { result += CalculateItemSize(stack[i]); }
                return result;
            }
        }

        internal CentralProcessingUnit cpu;

        public void Destroy() => stack.Clear();

        /// <summary>
        /// Gives the last item, and then remove
        /// </summary>
        /// <returns>The last item</returns>
        public override DataItem Pop()
        {
            DataItem val = this.stack[^1];
            this.stack.RemoveAt(this.stack.Count - 1);
            return val;
        }
        /// <returns>Adds a new item to the end</returns>
        public override void Push(DataItem value)
        {
            var item = value;
            item.stack = this;
            this.stack.Add(item);
        }
        /// <returns>Adds a new item to the end</returns>
        public void Push(DataItem value, string tag)
        {
            var item = value;
            item.stack = this;
            item.Tag = tag;
            this.stack.Add(item);
        }
        /// <returns>Adds a new item to the end</returns>
        public void Push(int value, string tag = null) => Push(new DataItem(value, tag));
        /// <returns>Adds a new item to the end</returns>
        public void Push(float value, string tag = null) => Push(new DataItem(value, tag));
        /// <returns>Adds a new item to the end</returns>
        public void Push(string value, string tag = null) => Push(new DataItem(value, tag));
        /// <returns>Adds a new item to the end</returns>
        public void Push(bool value, string tag = null) => Push(new DataItem(value, tag));
        /// <returns>Adds a new item to the end</returns>
        public void Push(IStruct value, string tag = null) => Push(new DataItem(value, tag));
        /// <summary>Adds a list to the end</summary>
        public override void PushRange(List<DataItem> list) => PushRange(list.ToArray());
        /// <summary>Adds a list to the end</summary>
        public void PushRange(List<int> list)
        {
            var newList = new List<DataItem>();
            for (int i = 0; i < list.Count; i++)
            {
                newList.Add(new DataItem(list[i], null));
            }
            PushRange(newList);
        }
        /// <summary>Adds an array to the end</summary>
        public override void PushRange(DataItem[] list)
        { foreach (DataItem item in list) Push(item); }
        /// <summary>Adds a list to the end</summary>
        public void PushRange(int[] list, string tag = "")
        {
            var newList = new List<DataItem>();
            for (int i = 0; i < list.Length; i++)
            {
                newList.Add(new DataItem(list[i], (tag.Length > 0) ? tag : null));
            }
            PushRange(newList);
        }
        /// <summary>Sets a specific item's value</summary>
        public void Set(int index, DataItem val, bool overrideTag = false)
        {
            DataItem item = val;
            item.stack = this;
            if (!overrideTag)
            {
                item.Tag = stack[index].Tag;
            }
            this.stack[index] = item;
        }
        /// <returns>A specific item</returns>
        public DataItem Get(int index) => this.stack[index];
    }
    internal class HEAP
    {
        readonly DataItem[] heap;

        internal HEAP(int size = 0)
        {
            this.heap = new DataItem[size];
        }

        internal int Size => this.heap.Length;
        internal DataItem this[int i]
        {
            get => heap[i];
            set => heap[i] = value;
        }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public struct DataItem
    {
        const int MaxReferenceDepth = 16;

        public class UnassignedStruct : IStruct
        {
            public bool HaveField(string field) => throw new RuntimeException("Struct is null");
            public void SetField(string field, DataItem value) => throw new RuntimeException("Struct is null");
            public DataItem GetField(string field) => throw new RuntimeException("Struct is null");

            public override string ToString() => "struct {...}";
        }

        public class Struct : IStruct
        {
            internal readonly Dictionary<string, DataItem> fields = new();

            public Struct(Dictionary<string, DataItem> fields)
            {
                this.fields = fields;
            }

            public bool HaveField(string field) => fields.ContainsKey(field);
            public void SetField(string field, DataItem value) => fields[field] = value;
            public DataItem GetField(string field) => fields[field];

            public override string ToString() => "struct {...}";
        }

        public class List
        {
            public Type itemTypes;
            public List<DataItem> items = new();

            public List(Type type)
            {
                this.itemTypes = type;
            }

            internal void Add(DataItem newItem)
            {
                if (itemTypes == newItem.type)
                {
                    items.Add(newItem);
                }
                else
                {
                    throw new RuntimeException($"Wrong type ({newItem.type}) of item pushed to the list with type {itemTypes}");
                }
            }

            internal void Add(DataItem newItem, int i)
            {
                if (itemTypes == newItem.type)
                {
                    items.Insert(i, newItem);
                }
                else
                {
                    throw new RuntimeException($"Wrong type ({newItem.type}) of item added to the list with type {itemTypes}");
                }
            }

            internal void Remove()
            {
                if (items.Count > 0)
                {
                    items.RemoveAt(items.Count - 1);
                }
            }

            internal void Remove(int i)
            {
                items.RemoveAt(i);
            }

            public override string ToString()
            {
                return $"[{(int)itemTypes}]";
            }

            public List(string raw)
            {
                var num = raw[1..^1];
                this.itemTypes = (Type)int.Parse(num);
            }
        }

        public enum Type
        {
            INT,
            FLOAT,
            STRING,
            BOOLEAN,
            STRUCT,
            LIST,
            RUNTIME,
        }

        public Type type;

        #region Value Fields

        int? valueInt;
        float? valueFloat;
        string valueString;
        bool? valueBoolean;
        IStruct valueStruct;
        List valueList;
        int? valueRef;

        #endregion

        bool IsReference;

        public DataStack stack;

        #region Value Properties

        public int ValueInt
        {
            get
            {
                if (type == Type.INT)
                { return GetRef().valueInt.Value; }

                throw new RuntimeException("Can't cast " + type.ToString() + " to INT");
            }
            set
            {
                SetRef(value);
            }
        }
        public float ValueFloat
        {
            get
            {
                if (type == Type.FLOAT)
                {
                    return valueFloat.Value;
                }
                throw new RuntimeException("Can't cast " + type.ToString() + " to FLOAT");
            }
            set
            {
                SetRef(value);
            }
        }
        public string ValueString
        {
            get
            {
                if (type == Type.STRING)
                {
                    return valueString;
                }
                throw new RuntimeException("Can't cast " + type.ToString() + " to STRING");
            }
            set
            {
                SetRef(value);
            }
        }
        public bool ValueBoolean
        {
            get
            {
                if (type == Type.BOOLEAN)
                {
                    return valueBoolean.Value;
                }
                throw new RuntimeException("Can't cast " + type.ToString() + " to BOOLEAN");
            }
            set
            {
                SetRef(value);
            }
        }
        public IStruct ValueStruct
        {
            get
            {
                if (type == Type.STRUCT)
                {
                    return valueStruct;
                }
                throw new RuntimeException("Can't cast " + type.ToString() + " to STRUCT");
            }
            set
            {
                SetRef(value);
            }
        }
        public List ValueList
        {
            get
            {
                if (type == Type.LIST)
                {
                    return valueList;
                }
                throw new RuntimeException("Can't cast " + type.ToString() + " to LIST");
            }
            set
            {
                SetRef(value);
            }
        }
        public int ValueRef
        {
            get
            {
                if (IsReference) return valueRef.Value;
                throw new RuntimeException("Can't cast " + type.ToString() + " to REF");
            }
            set
            {
                this.IsReference = true;

                this.valueInt = null;
                this.valueFloat = null;
                this.valueString = null;
                this.valueBoolean = null;
                this.valueStruct = null;
                this.valueList = null;
                this.valueRef = value;
            }
        }

        #endregion

        #region Get/Set Ref

        DataItem GetRef(int currentDepth = 0)
        {
            if (currentDepth >= MaxReferenceDepth)
            { throw new RuntimeException("Reference depth exceeded"); }

            if (IsReference)
            {
                return stack.Get(ValueRef).GetRef(currentDepth + 1);
            }
            else
            {
                return this;
            }
        }
        void SetRef(int value, int currentDepth = 0)
        {
            if (currentDepth >= MaxReferenceDepth)
            { throw new RuntimeException("Reference depth exceeded"); }

            if (IsReference)
            {
                stack[ValueRef].SetRef(value, currentDepth + 1);
            }
            else
            {
                this.type = Type.INT;
                this.valueInt = value;
                this.valueFloat = null;
                this.valueString = null;
                this.valueBoolean = null;
                this.valueStruct = null;
                this.valueList = null;
                this.valueRef = null;
            }
        }
        void SetRef(float value, int currentDepth = 0)
        {
            if (currentDepth >= MaxReferenceDepth)
            { throw new RuntimeException("Reference depth exceeded"); }

            if (IsReference)
            {
                stack[ValueRef].SetRef(value, currentDepth + 1);
            }
            else
            {
                this.type = Type.FLOAT;
                this.valueInt = null;
                this.valueFloat = value;
                this.valueString = null;
                this.valueBoolean = null;
                this.valueStruct = null;
                this.valueList = null;
                this.valueRef = null;
            }
        }
        void SetRef(string value, int currentDepth = 0)
        {
            if (currentDepth >= MaxReferenceDepth)
            { throw new RuntimeException("Reference depth exceeded"); }

            if (IsReference)
            {
                stack[ValueRef].SetRef(value, currentDepth + 1);
            }
            else
            {
                this.type = Type.STRING;
                this.valueInt = null;
                this.valueFloat = null;
                this.valueString = value;
                this.valueBoolean = null;
                this.valueStruct = null;
                this.valueList = null;
                this.valueRef = null;
            }
        }
        void SetRef(bool value, int currentDepth = 0)
        {
            if (currentDepth >= MaxReferenceDepth)
            { throw new RuntimeException("Reference depth exceeded"); }

            if (IsReference)
            {
                stack[ValueRef].SetRef(value, currentDepth + 1);
            }
            else
            {
                this.type = Type.BOOLEAN;
                this.valueInt = null;
                this.valueFloat = null;
                this.valueString = null;
                this.valueBoolean = value;
                this.valueStruct = null;
                this.valueList = null;
                this.valueRef = null;
            }
        }
        void SetRef(IStruct value, int currentDepth = 0)
        {
            if (currentDepth >= MaxReferenceDepth)
            { throw new RuntimeException("Reference depth exceeded"); }

            if (IsReference)
            {
                stack[ValueRef].SetRef(value, currentDepth + 1);
            }
            else
            {
                this.type = Type.STRUCT;
                this.valueInt = null;
                this.valueFloat = null;
                this.valueString = null;
                this.valueBoolean = null;
                this.valueStruct = value;
                this.valueList = null;
                this.valueRef = null;
            }
        }
        void SetRef(List value, int currentDepth = 0)
        {
            if (currentDepth >= MaxReferenceDepth)
            { throw new RuntimeException("Reference depth exceeded"); }

            if (IsReference)
            {
                stack[ValueRef].SetRef(value, currentDepth + 1);
            }
            else
            {
                this.type = Type.LIST;
                this.valueInt = null;
                this.valueFloat = null;
                this.valueString = null;
                this.valueBoolean = null;
                this.valueStruct = null;
                this.valueList = value;
                this.valueRef = null;
            }
        }

        #endregion

        /// <summary>Only for debugging</summary>
        public string Tag { get; internal set; }

        #region Constructors

        public DataItem(int value, string tag)
        {
            this.type = Type.INT;

            this.IsReference = false;

            this.valueInt = value;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;
            this.valueRef = null;

            this.stack = null;

            this.Tag = tag;
        }
        public DataItem(float value, string tag)
        {
            this.type = Type.FLOAT;

            this.IsReference = false;

            this.valueInt = null;
            this.valueFloat = value;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;
            this.valueRef = null;

            this.stack = null;

            this.Tag = tag;
        }
        public DataItem(string value, string tag)
        {
            this.type = Type.STRING;

            this.IsReference = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = value;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;
            this.valueRef = null;

            this.stack = null;

            this.Tag = tag;
        }
        public DataItem(bool value, string tag)
        {
            this.type = Type.BOOLEAN;

            this.IsReference = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = value;
            this.valueStruct = null;
            this.valueList = null;
            this.valueRef = null;

            this.stack = null;

            this.Tag = tag;
        }
        public DataItem(IStruct value, string tag)
        {
            this.type = Type.STRUCT;

            this.IsReference = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = value;
            this.valueList = null;
            this.valueRef = null;

            this.stack = null;

            this.Tag = tag;
        }
        public DataItem(List value, string tag)
        {
            this.type = Type.LIST;

            this.IsReference = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = value;
            this.valueRef = null;

            this.stack = null;

            this.Tag = tag;
        }
        public DataItem(BBCode.TypeToken type1, string tag)
        {
            this.type = Type.INT;

            this.IsReference = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;
            this.valueRef = null;

            switch (type1.typeName)
            {
                case BBCode.BuiltinType.INT:
                    this.type = Type.INT;
                    this.valueInt = 0;
                    break;
                case BBCode.BuiltinType.FLOAT:
                    this.type = Type.FLOAT;
                    this.valueFloat = 0f;
                    break;
                case BBCode.BuiltinType.STRING:
                    this.type = Type.STRING;
                    this.valueString = "";
                    break;
                case BBCode.BuiltinType.BOOLEAN:
                    this.type = Type.BOOLEAN;
                    this.valueBoolean = false;
                    break;
                case BBCode.BuiltinType.STRUCT:
                    // TODO: Ezt tesztelni:
                    this.type = Type.STRUCT;
                    this.valueStruct = new UnassignedStruct();
                    break;
            }

            this.stack = null;

            this.Tag = tag;
        }
        public DataItem(object value, string tag)
        {
            this.type = Type.INT;

            this.IsReference = false;

            this.valueInt = null;
            this.valueFloat = null;
            this.valueString = null;
            this.valueBoolean = null;
            this.valueStruct = null;
            this.valueList = null;
            this.valueRef = null;

            this.stack = null;

            this.Tag = tag;

            if (value is int a)
            {
                this.type = Type.INT;
                this.valueInt = a;
            }
            else if (value is float b)
            {
                this.type = Type.FLOAT;
                this.valueFloat = b;
            }
            else if (value is string c)
            {
                this.type = Type.STRING;
                this.valueString = c;
            }
            else if (value is bool d)
            {
                this.type = Type.BOOLEAN;
                this.valueBoolean = d;
            }
            else if (value is IStruct e)
            {
                this.type = Type.STRUCT;
                this.valueStruct = e;
            }
            else if (value is List f)
            {
                this.type = Type.LIST;
                this.ValueList = f;
            }
            else
            {
                throw new Errors.RuntimeException($"Unknown type {value.GetType().FullName}");
            }
        }

        #endregion

        #region TrySet()

        public DataItem TrySet(DataItem value)
        {
            switch (type)
            {
                case Type.INT:
                    switch (value.type)
                    {
                        case Type.INT:
                            return new DataItem(value.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(Math.Round(value.ValueFloat), null);
                        case Type.STRING:
                            return new DataItem(int.Parse(value.ValueString), null);
                    }
                    break;
                case Type.FLOAT:
                    switch (value.type)
                    {
                        case Type.INT:
                            return new DataItem(value.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(value.ValueFloat, null);
                    }
                    break;
                case Type.STRING:
                    switch (value.type)
                    {
                        case Type.INT:
                            return new DataItem(value.ValueInt.ToString(), null);
                        case Type.FLOAT:
                            return new DataItem(value.ValueFloat.ToString(), null);
                        case Type.STRING:
                            return new DataItem(value.ValueString, null);
                        case Type.BOOLEAN:
                            return new DataItem(value.ValueBoolean.ToString(), null);
                    }
                    break;
                case Type.BOOLEAN:
                    switch (value.type)
                    {
                        case Type.BOOLEAN:
                            return new DataItem(value.ValueBoolean, null);
                    }
                    break;
                case Type.STRUCT:
                    switch (value.type)
                    {
                        case Type.STRUCT:
                            return new DataItem(value.ValueStruct, null);
                    }
                    break;
                case Type.LIST:
                    switch (value.type)
                    {
                        case Type.LIST:
                            return new DataItem(value.ValueList, null);
                    }
                    break;
                case Type.RUNTIME:
                    return value;
            }
            throw new RuntimeException("Can't cast from " + value.type.ToString() + " to " + type.ToString());
        }
        public DataItem TrySet(int value)
        {
            return type switch
            {
                Type.INT => new DataItem(value, null),
                Type.FLOAT => new DataItem(value, null),
                Type.STRING => new DataItem(value.ToString(), null),
                _ => throw new RuntimeException("Can't cast from " + "INT" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(float value)
        {
            return type switch
            {
                Type.INT => new DataItem(Math.Round(value), null),
                Type.FLOAT => new DataItem(value, null),
                Type.STRING => new DataItem(value.ToString(), null),
                _ => throw new RuntimeException("Can't cast from " + "FLOAT" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(bool value)
        {
            return type switch
            {
                Type.STRING => new DataItem(value.ToString(), null),
                Type.BOOLEAN => new DataItem(value, null),
                _ => throw new RuntimeException("Can't cast from " + "BOOLEAN" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(string value)
        {
            return type switch
            {
                Type.STRING => new DataItem(value, null),
                Type.INT => new DataItem(int.Parse(value), null),
                _ => throw new RuntimeException("Can't cast from " + "STRING" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(IStruct value)
        {
            return type switch
            {
                Type.STRUCT => new DataItem(value, null),
                _ => throw new RuntimeException("Can't cast from " + "STRUCT" + " to " + type.ToString()),
            };
        }
        public DataItem TrySet(List value)
        {
            return type switch
            {
                Type.LIST => new DataItem(value, null),
                _ => throw new RuntimeException("Can't cast from " + "LIST" + " to " + type.ToString()),
            };
        }

        #endregion

        #region Operators

        public static DataItem operator +(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt + rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt + rightSide.ValueFloat, null);
                        case Type.STRING:
                            return new DataItem(leftSide.ToStringValue() + rightSide.ValueString, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat + rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueFloat + rightSide.ValueFloat, null);
                        case Type.STRING:
                            return new DataItem(leftSide.ToStringValue() + rightSide.ValueString, null);
                    }
                    break;
                case Type.STRING:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueString + rightSide.ToStringValue(), null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueString + rightSide.ToStringValue(), null);
                        case Type.STRING:
                            return new DataItem(leftSide.ValueString + rightSide.ValueString, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do + operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static DataItem operator -(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt - rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt - rightSide.ValueFloat, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat - rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueFloat - rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do - operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static DataItem operator *(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt * rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt * rightSide.ValueFloat, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat * rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueFloat * rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do * operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static DataItem operator /(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt / rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt / rightSide.ValueFloat, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat / rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueFloat / rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do / operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static DataItem operator %(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueInt % rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueInt % rightSide.ValueFloat, null);
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return new DataItem(leftSide.ValueFloat % rightSide.ValueInt, null);
                        case Type.FLOAT:
                            return new DataItem(leftSide.ValueFloat % rightSide.ValueFloat, null);
                    }
                    break;
            }

            throw new RuntimeException("Can't do % operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static bool operator <(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt < rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt < rightSide.ValueFloat;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat < rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueFloat < rightSide.ValueFloat;
                    }
                    break;
            }

            throw new RuntimeException("Can't do < operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static bool operator >(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt > rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt > rightSide.ValueFloat;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat > rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueFloat > rightSide.ValueFloat;
                    }
                    break;
            }

            throw new RuntimeException("Can't do > operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static bool operator <=(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt <= rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt <= rightSide.ValueFloat;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat <= rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueFloat <= rightSide.ValueFloat;
                    }
                    break;
            }

            throw new RuntimeException("Can't do <= operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static bool operator >=(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt >= rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt >= rightSide.ValueFloat;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat >= rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueFloat >= rightSide.ValueFloat;
                    }
                    break;
            }

            throw new RuntimeException("Can't do >= operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static bool operator ==(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt == rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt == rightSide.ValueFloat;
                        case Type.STRING:
                            return leftSide.ValueInt.ToString() == rightSide.ValueString;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat == rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueFloat == rightSide.ValueFloat;
                        case Type.STRING:
                            return leftSide.ValueFloat.ToString() == rightSide.ValueString;
                    }
                    break;
                case Type.STRING:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueString == rightSide.ValueInt.ToString();
                        case Type.FLOAT:
                            return leftSide.ValueString == rightSide.ValueFloat.ToString();
                        case Type.STRING:
                            return leftSide.ValueString == rightSide.ValueString;
                    }
                    break;
                case Type.BOOLEAN:
                    if (rightSide.type == Type.BOOLEAN)
                    {
                        return leftSide.ValueBoolean == rightSide.ValueBoolean;
                    }
                    break;
            }

            throw new RuntimeException("Can't do == operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }
        public static bool operator !=(DataItem leftSide, DataItem rightSide)
        {
            switch (leftSide.type)
            {
                case Type.INT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueInt != rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueInt != rightSide.ValueFloat;
                        case Type.STRING:
                            return leftSide.ValueInt.ToString() != rightSide.ValueString;
                    }
                    break;
                case Type.FLOAT:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueFloat != rightSide.ValueInt;
                        case Type.FLOAT:
                            return leftSide.ValueFloat != rightSide.ValueFloat;
                        case Type.STRING:
                            return leftSide.ValueFloat.ToString() != rightSide.ValueString;
                    }
                    break;
                case Type.STRING:
                    switch (rightSide.type)
                    {
                        case Type.INT:
                            return leftSide.ValueString != rightSide.ValueInt.ToString();
                        case Type.FLOAT:
                            return leftSide.ValueString != rightSide.ValueFloat.ToString();
                        case Type.STRING:
                            return leftSide.ValueString != rightSide.ValueString;
                    }
                    break;
                case Type.BOOLEAN:
                    if (rightSide.type == Type.BOOLEAN)
                    {
                        return leftSide.ValueBoolean != rightSide.ValueBoolean;
                    }
                    break;
            }

            throw new RuntimeException("Can't do != operation with type " + leftSide.type.ToString() + " and " + rightSide.type.ToString());
        }

        public static bool operator ==(DataItem leftSide, int rightSide) => (leftSide == new DataItem(rightSide, null));
        public static bool operator !=(DataItem leftSide, int rightSide) => (leftSide != new DataItem(rightSide, null));

        public static bool operator ==(DataItem leftSide, bool rightSide) => (leftSide == new DataItem(rightSide, null));
        public static bool operator !=(DataItem leftSide, bool rightSide) => (leftSide != new DataItem(rightSide, null));

        public static bool operator !(DataItem leftSide)
        {
            if (leftSide.type == Type.BOOLEAN)
            {
                return !leftSide.ValueBoolean;
            }
            throw new RuntimeException("Can't do ! operation with type " + leftSide.type.ToString());
        }
        public static bool operator |(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == Type.BOOLEAN && rightSide.type == Type.BOOLEAN)
            {
                return (leftSide.ValueBoolean | rightSide.ValueBoolean);
            }
            throw new RuntimeException("Can't do | operation with type " + leftSide.type.ToString() + " and BOOLEAN");
        }
        public static bool operator &(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == Type.BOOLEAN && rightSide.type == Type.BOOLEAN)
            {
                return (leftSide.ValueBoolean & rightSide.ValueBoolean);
            }
            throw new RuntimeException("Can't do & operation with type " + leftSide.type.ToString() + " and BOOLEAN");
        }
        public static bool operator ^(DataItem leftSide, DataItem rightSide)
        {
            if (leftSide.type == Type.BOOLEAN && rightSide.type == Type.BOOLEAN)
            {
                return (leftSide.ValueBoolean ^ rightSide.ValueBoolean);
            }
            throw new RuntimeException("Can't do ^ operation with type " + leftSide.type.ToString() + " and BOOLEAN");
        }

        public static bool operator true(DataItem leftSide)
        {
            if (leftSide.type == Type.BOOLEAN)
            {
                return leftSide.ValueBoolean;
            }
            throw new RuntimeException("Can't do true operation with type " + leftSide.type.ToString());
        }
        public static bool operator false(DataItem leftSide)
        {
            if (leftSide.type == Type.BOOLEAN)
            {
                return leftSide.ValueBoolean;
            }
            throw new RuntimeException("Can't do true operation with type " + leftSide.type.ToString());
        }

        #endregion

        public int GetRefIndex(int currentDepth = 0)
        {
            if (currentDepth >= MaxReferenceDepth)
            { throw new RuntimeException("Reference depth exceeded"); }

            if (IsReference)
            {
                var x = stack.Get(ValueRef).GetRefIndex(currentDepth + 1);
                if (x == -1)
                { return ValueRef; }
                else
                { throw new RuntimeException("Reference not found"); }
            }
            else
            { return -1; }
        }

        public string ToStringValue()
        {
            string retStr = type switch
            {
                Type.INT => (IsReference ? "ref " : "") + ValueInt.ToString(),
                Type.FLOAT => (IsReference ? "ref " : "") + ValueFloat.ToString().Replace(',', '.'),
                Type.STRING => (IsReference ? "ref " : "") + ValueString,
                Type.BOOLEAN => (IsReference ? "ref " : "") + ValueBoolean.ToString(),
                Type.STRUCT => (IsReference ? "ref " : "") + "{ ... }",
                Type.LIST => (IsReference ? "ref " : "") + "[ ... ]",
                _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
            };
            return retStr;
        }

        public override string ToString()
        {
            string retStr = type switch
            {
                Type.INT => IsReference ? $"ref <{ValueRef}>" : ValueInt.ToString(),
                Type.FLOAT => IsReference ? $"ref <{ValueRef}>" : ValueFloat.ToString().Replace(',', '.') + "f",
                Type.STRING => IsReference ? $"ref <{ValueRef}>" : $"\"{ValueString}\"",
                Type.BOOLEAN => IsReference ? $"ref <{ValueRef}>" : ValueBoolean.ToString(),
                Type.STRUCT => IsReference ? $"ref <{ValueRef}>" : $"{ValueStruct.GetType().Name} {{...}}",
                Type.LIST => IsReference ? $"ref <{ValueRef}>" : "[...]",
                _ => throw new RuntimeException("Can't parse " + type.ToString() + " to STRING"),
            };
            if (!string.IsNullOrEmpty(this.Tag))
            {
                retStr = retStr + " #" + this.Tag;
            }
            return retStr;
        }

        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(type);
            hash.Add(valueInt);
            hash.Add(valueFloat);
            hash.Add(valueString);
            hash.Add(valueBoolean);
            hash.Add(valueStruct);
            hash.Add(valueList);
            hash.Add(valueRef);
            hash.Add(IsReference);
            return hash.ToHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DataItem item &&
                   type == item.type &&
                   valueInt == item.valueInt &&
                   valueFloat == item.valueFloat &&
                   valueString == item.valueString &&
                   valueBoolean == item.valueBoolean &&
                   EqualityComparer<IStruct>.Default.Equals(valueStruct, item.valueStruct) &&
                   EqualityComparer<List>.Default.Equals(valueList, item.valueList) &&
                   valueRef == item.valueRef &&
                   IsReference == item.IsReference;
        }
    }

    public interface IStruct
    {
        public bool HaveField(string field);
        public void SetField(string field, DataItem value);
        public DataItem GetField(string field);
    }

    public struct BytecodeInterpreterSettings
    {
        internal int ClockCyclesPerUpdate;
        internal int InstructionLimit;
        internal int StackMaxSize;

        public static BytecodeInterpreterSettings Default => new()
        {
            ClockCyclesPerUpdate = 2,
            InstructionLimit = 1024,
            StackMaxSize = 128,
        };
    }

    public class BytecodeInterpeter
    {
        internal class InterpeterDetails
        {
            readonly BytecodeInterpeter bytecodeInterpeter;
            public int CodePointer => bytecodeInterpeter.CPU.CodePointer;
            public int BasePointer => bytecodeInterpeter.CPU.MU.BasePointer;
            public int[] ReturnAddressStack => bytecodeInterpeter.CPU.MU.ReturnAddressStack.ToArray();
            public DataItem[] Stack => bytecodeInterpeter.CPU.MU.Stack.ToArray();
            public int StackMemorySize => bytecodeInterpeter.CPU.MU.Stack.UsedVirtualMemory;

            public InterpeterDetails(BytecodeInterpeter bytecodeInterpeter)
            {
                this.bytecodeInterpeter = bytecodeInterpeter;
            }
        }

        BytecodeInterpreterSettings settings;

        CentralProcessingUnit CPU;
        int[] arguments;

        // Running control
        bool enable;
        bool currentlyRunning;
        public bool IsRunning => currentlyRunning;
        bool destroyed;
        bool IsCall;
        int remainingClockCycles;

        // Safely
        int lastInstrPointer = -1;
        int endlessSafe;

        readonly InterpeterDetails details;
        internal InterpeterDetails Details => details;

        public BytecodeInterpeter(Instruction[] code, Dictionary<string, BuiltinFunction> builtinFunctions, BytecodeInterpreterSettings settings)
        {
            this.settings = settings;

            CPU = new CentralProcessingUnit(code, 0, builtinFunctions);

            arguments = Array.Empty<int>();

            enable = false;
            currentlyRunning = false;
            destroyed = false;
            IsCall = false;
            remainingClockCycles = this.settings.ClockCyclesPerUpdate;

            endlessSafe = 0;
            lastInstrPointer = -1;

            details = new InterpeterDetails(this);
        }

        public void Jump(int instructionOffset)
        {
            if (destroyed) return;
            if (currentlyRunning) return;

            currentlyRunning = true;
            enable = true;
            IsCall = false;

            CPU.MU.CodePointer = instructionOffset;
            CPU.MU.BasePointer = CPU.MU.Stack.Count;
        }

        public void Call(int instructionOffset, params int[] arguments)
        {
            if (destroyed) return;
            if (currentlyRunning) return;

            this.arguments = arguments;
            currentlyRunning = true;
            enable = true;
            IsCall = true;

            CPU.MU.CodePointer = instructionOffset;
            CPU.MU.Stack.Push(0, "return value");
            CPU.MU.Stack.PushRange(this.arguments, "arg");

            CPU.MU.Stack.Push(0, "saved base pointer");
            CPU.MU.ReturnAddressStack.Add(CPU.MU.End());
            CPU.MU.BasePointer = CPU.MU.Stack.Count;
        }

        void ExecuteNext()
        {
            if (destroyed) return;

            if (endlessSafe > settings.InstructionLimit)
            {
                CPU.MU.CodePointer = CPU.MU.Code.Length;
                throw new RuntimeException("Instruction limit reached!", GetContext());
            }

            if (CPU.MU.Stack.Count > settings.StackMaxSize)
            {
                throw new RuntimeException("Stack overflow!", GetContext());
            }

            if (CPU.CodePointer < CPU.MU.End())
            {
                if (CPU.MU.CurrentInstruction.opcode == Opcode.COMMENT)
                {
                    CPU.MU.Step();
                    ExecuteNext();
                    return;
                }

                endlessSafe++;

                try
                {
                    remainingClockCycles -= Math.Max(1, CPU.Clock());
                }
                catch (RuntimeException error)
                {
                    error.Context = GetContext();
                    throw;
                }

                if (lastInstrPointer == CPU.CodePointer)
                {
                    Output.Debug.Debug.LogWarning($"Possible endless loop! Instruction: " + CPU.MU.CurrentInstruction.ToString());
                }

                lastInstrPointer = CPU.CodePointer;

                currentlyRunning = true;

                if (remainingClockCycles > 0)
                {
                    ExecuteNext();
                }
            }
            else
            {
                Shutdown(out _);
            }
        }

        public void Destroy()
        {
            if (destroyed) return;
            CPU.Destroy();
            CPU = null;
            destroyed = true;
        }

        void Shutdown(out int result)
        {
            result = -1;
            if (destroyed) return;

            if (IsCall)
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    if (CPU.MU.Stack.Count - 1 > i)
                        CPU.MU.Stack.RemoveAt(CPU.MU.Stack.Count - 1);
                }
                result = CPU.MU.Stack.Pop().ValueInt;
            }

            lastInstrPointer = -1;
            endlessSafe = 0;
            currentlyRunning = false;
            enable = false;
        }

        public void Tick()
        {
            if (!enable || destroyed) return;
            remainingClockCycles = Math.Min(remainingClockCycles + settings.ClockCyclesPerUpdate, settings.ClockCyclesPerUpdate);
            ExecuteNext();
        }

        public void AddValueToStack(DataItem value)
        {
            if (destroyed) return;
            CPU.MU.Stack.Push(value);
        }

        public void CallStackPush(string data) => this.CPU.MU.CallStack.Push(data);
        public void CallStackPop() => this.CPU.MU.CallStack.Pop();

        internal Context GetContext() => new()
        {
            CallStack = this.CPU.MU.CallStack.ToArray(),
            ExecutedInstructionCount = this.endlessSafe,
            CodePointer = this.CPU.CodePointer,
        };

        public struct Context
        {
            public string[] CallStack;
            public int CodePointer;
            public int ExecutedInstructionCount;
        }
    }
}
