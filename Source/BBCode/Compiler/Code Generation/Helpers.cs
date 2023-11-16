using System;

#pragma warning disable IDE0051 // Remove unused private members

namespace LanguageCore.BBCode.Compiler
{
    using Parser.Statement;
    using Runtime;

    public partial class CodeGeneratorForMain : CodeGenerator
    {
        #region Helper Functions

        int CallRuntime(CompiledVariable address)
        {
            if (address.Type != Type.Integer && !address.Type.IsFunction)
            { throw new CompilerException($"This should be an \"{new CompiledType(Type.Integer)}\" or function pointer and not \"{address.Type}\"", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0);

            AddInstruction(Opcode.GET_BASEPOINTER);

            if (address.Type.InHEAP)
            { AddInstruction(Opcode.HEAP_GET, AddressingMode.ABSOLUTE, address.MemoryAddress); }
            else
            { StackLoad(new ValueAddress(address), address.Type.SizeOnStack); }

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(CompiledParameter address)
        {
            if (address.Type != Type.Integer && !address.Type.IsFunction)
            { throw new CompilerException($"This should be an \"{new CompiledType(Type.Integer)}\" or function pointer and not \"{address.Type}\"", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0);

            AddInstruction(Opcode.GET_BASEPOINTER);

            ValueAddress offset = GetBaseAddress(address);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset.Address);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(StatementWithValue address)
        {
            CompiledType addressType = FindStatementType(address);

            if (addressType != Type.Integer && !addressType.IsFunction)
            { throw new CompilerException($"This should be an \"{new CompiledType(Type.Integer)}\" or function pointer and not \"{addressType}\"", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0); // Saved code pointer

            AddInstruction(Opcode.GET_BASEPOINTER); // Saved base pointer

            GenerateCodeForStatement(address);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 3);
            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int Call(int absoluteAddress)
        {
            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0);

            AddInstruction(Opcode.GET_BASEPOINTER);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, 0);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.ABSOLUTE, absoluteAddress - GeneratedCode.Count);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        void Return()
        {
            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RUNTIME, 0);
            AddInstruction(Opcode.SET_CODEPOINTER, AddressingMode.RUNTIME);
        }

        /// <exception cref="NotImplementedException"/>
        /// <exception cref="CompilerException"/>
        /// <exception cref="InternalException"/>
        int GenerateInitialValue(CompiledType type, Action<int>? afterValue = null)
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size += GenerateInitialValue(field.Type, afterValue);
                    afterValue?.Invoke(size);
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, 0);
                afterValue?.Invoke(0);
                return 1;
            }

            if (type.IsStackArray)
            {
                int stackSize = type.StackArraySize;

                int size = 0;
                for (int i = 0; i < stackSize; i++)
                {
                    size += GenerateInitialValue(type.StackArrayOf, afterValue);
                    afterValue?.Invoke(size);
                }
                return size;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type));
            afterValue?.Invoke(0);
            return 1;
        }

        int GenerateInitialValue2(CompiledType type, Action<int> afterValue)
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type));
                    afterValue?.Invoke(size);
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, 0);
                afterValue?.Invoke(0);
                return 1;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type));
            afterValue?.Invoke(0);
            return 1;
        }

        int ParametersSize
        {
            get
            {
                int sum = 0;

                for (int i = 0; i < CompiledParameters.Count; i++)
                {
                    sum += CompiledParameters[i].Type.SizeOnStack;
                }

                return sum;
            }
        }
        int ParametersSizeBefore(int beforeThis)
        {
            int sum = 0;

            for (int i = 0; i < CompiledParameters.Count; i++)
            {
                if (CompiledParameters[i].Index < beforeThis) continue;

                sum += CompiledParameters[i].Type.SizeOnStack;
            }

            return sum;
        }

        #endregion

        #region Memory Helpers

        protected override void StackLoad(ValueAddress address)
        {
            if (address.IsReference)
            {
                AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);
                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RUNTIME);
                throw new NotImplementedException();
            }

            if (address.InHeap)
            {
                throw new NotImplementedException();
            }

            switch (address.AddressingMode)
            {
                case AddressingMode.ABSOLUTE:
                case AddressingMode.BASEPOINTER_RELATIVE:
                case AddressingMode.RELATIVE:
                    AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode, address.Address);
                    break;

                case AddressingMode.POP:
                    AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode);
                    break;

                case AddressingMode.RUNTIME:
                    AddInstruction(Opcode.PUSH_VALUE, address.Address);
                    AddInstruction(Opcode.LOAD_VALUE, address.AddressingMode);
                    break;
                default: throw new ImpossibleException();
            }
        }
        protected override void StackStore(ValueAddress address)
        {
            if (address.IsReference)
            {
                throw new NotImplementedException();
            }

            if (address.InHeap)
            {
                throw new NotImplementedException();
            }

            switch (address.AddressingMode)
            {
                case AddressingMode.ABSOLUTE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.ABSOLUTE, address.Address);
                    break;
                case AddressingMode.BASEPOINTER_RELATIVE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.BASEPOINTER_RELATIVE, address.Address);
                    break;
                case AddressingMode.RELATIVE:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.RELATIVE, address.Address);
                    break;
                case AddressingMode.POP:
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.POP);
                    break;
                case AddressingMode.RUNTIME:
                    AddInstruction(Opcode.PUSH_VALUE, address.Address);
                    AddInstruction(Opcode.STORE_VALUE, AddressingMode.RUNTIME);
                    break;
                default: throw new ImpossibleException();
            }
        }

        void BoxData(int to, int size)
        {
            AddComment($"Box: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                int currentReversedOffset = size - currentOffset - 1;

                AddComment($"Element {currentOffset}:");

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, (-currentReversedOffset) - 1);

                AddInstruction(Opcode.PUSH_VALUE, to + size);
                AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
            }
            AddComment("}");
        }
        void UnboxData(int from, int size)
        {
            AddComment($"Unbox: {{");

            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddComment($"Element {currentOffset}:");

                AddInstruction(Opcode.PUSH_VALUE, currentOffset + from);
                AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
            }
            AddComment("}");
        }

        void CheckPointerNull(bool preservePointer = true, string exceptionMessage = "null pointer")
        {
            if (!CheckNullPointers) return;
            AddComment($"Check for pointer zero {{");
            if (preservePointer)
            { AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, -1); }
            AddInstruction(Opcode.LOGIC_NOT);
            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY_IF_FALSE);
            GenerateCodeForLiteralString(exceptionMessage);
            AddInstruction(Opcode.THROW);
            GeneratedCode[jumpInstruction].ParameterInt = GeneratedCode.Count - jumpInstruction;
            AddComment($"}}");
        }

        void HeapLoad(ValueAddress pointerAddress, int offset)
        {
            StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.BasepointerRelative, pointerAddress.IsReference, false));

            CheckPointerNull();

            AddInstruction(Opcode.PUSH_VALUE, offset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_GET, AddressingMode.RUNTIME);
        }
        void HeapStore(ValueAddress pointerAddress, int offset)
        {
            StackLoad(new ValueAddress(pointerAddress.Address, pointerAddress.BasepointerRelative, pointerAddress.IsReference, false));

            CheckPointerNull();

            AddInstruction(Opcode.PUSH_VALUE, offset);
            AddInstruction(Opcode.MATH_ADD);
            AddInstruction(Opcode.HEAP_SET, AddressingMode.RUNTIME);
        }

        #endregion

        #region Addressing Helpers

        public const int TagsBeforeBasePointer = 2;

        public int ReturnValueOffset => -(ParametersSize + 1 + TagsBeforeBasePointer);
        public const int ReturnFlagOffset = 0;
        public const int SavedBasePointerOffset = -1;
        public const int SavedCodePointerOffset = -2;

        protected override ValueAddress GetBaseAddress(CompiledParameter parameter)
        {
            int address = -(ParametersSizeBefore(parameter.Index) + TagsBeforeBasePointer);
            return new ValueAddress(parameter, address);
        }
        protected override ValueAddress GetBaseAddress(CompiledParameter parameter, int offset)
        {
            int address = -(ParametersSizeBefore(parameter.Index) - offset + TagsBeforeBasePointer);
            return new ValueAddress(parameter, address);
        }

        #endregion
    }
}