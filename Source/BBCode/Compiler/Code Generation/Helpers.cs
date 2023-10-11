using System;

#pragma warning disable IDE0051 // Remove unused private members

namespace LanguageCore.BBCode.Compiler
{
    using System.Collections.Generic;
    using LanguageCore.Runtime;
    using Parser;
    using Parser.Statement;

    public partial class CodeGenerator : CodeGeneratorBase
    {
        #region Helper Functions

        int CallRuntime(CompiledVariable address)
        {
            if (address.Type != Type.INT && !address.Type.IsFunction)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            if (address.IsStoredInHEAP)
            { AddInstruction(Opcode.HEAP_GET, AddressingMode.ABSOLUTE, address.MemoryAddress); }
            else
            { LoadFromStack(address.MemoryAddress, address.Type.Size, !address.IsGlobal); }

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(CompiledParameter address)
        {
            if (address.Type != Type.INT && !address.Type.IsFunction)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            int offset = GetBaseAddress(address);
            AddInstruction(Opcode.LOAD_VALUE, AddressingMode.BASEPOINTER_RELATIVE, offset);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, -1);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int CallRuntime(StatementWithValue address)
        {
            if (FindStatementType(address) != Type.INT)
            { throw new CompilerException($"This should be an integer", address, CurrentFile); }

            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

            GenerateCodeForStatement(address);

            AddInstruction(Opcode.PUSH_VALUE, GeneratedCode.Count + 2, "GeneratedCode.Count + 2");

            AddInstruction(Opcode.MATH_SUB);

            AddInstruction(Opcode.SET_BASEPOINTER, AddressingMode.RELATIVE, 0);

            int jumpInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.JUMP_BY, AddressingMode.RUNTIME);

            GeneratedCode[returnToValueInstruction].ParameterInt = GeneratedCode.Count;

            return jumpInstruction;
        }

        int Call(int absoluteAddress)
        {
            int returnToValueInstruction = GeneratedCode.Count;
            AddInstruction(Opcode.PUSH_VALUE, 0, "saved code pointer");

            AddInstruction(Opcode.GET_BASEPOINTER);
            AddInstruction(Opcode.DEBUG_SET_TAG, "saved base pointer");

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

        protected override bool GetLocalSymbolType(string symbolName, out CompiledType type)
        {
            if (GetVariable(symbolName, out CompiledVariable variable))
            {
                type = variable.Type;
                return true;
            }

            if (GetParameter(symbolName, out CompiledParameter parameter))
            {
                type = parameter.Type;
                return true;
            }

            type = null;
            return false;
        }

        bool GetVariable(string variableName, out CompiledVariable compiledVariable)
            => CompiledVariables.TryGetValue(variableName, out compiledVariable);

        bool GetParameter(string parameterName, out CompiledParameter parameter)
            => CompiledParameters.TryGetValue(parameterName, out parameter);

        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(TypeInstance type, string tag = "")
        {
            if (Constants.BuiltinTypeMap3.TryGetValue(type.Identifier.Content, out Type builtinType))
            {
                AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(builtinType), tag);
                return 1;
            }

            CompiledType instanceType = FindType(type);

            if (instanceType.IsStruct)
            {
                int size = 0;
                foreach (FieldDefinition field in instanceType.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (instanceType.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            if (instanceType.IsEnum)
            {
                if (instanceType.Enum.Members.Length == 0)
                { throw new CompilerException($"Could not get enum \"{instanceType.Enum.Identifier.Content}\" initial value: enum has no members", instanceType.Enum.Identifier, instanceType.Enum.FilePath); }

                AddInstruction(Opcode.PUSH_VALUE, instanceType.Enum.Members[0].Value, tag);
                return 1;
            }

            if (instanceType.IsFunction)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER, tag);
                return 1;
            }

            throw new CompilerException($"Unknown type definition {instanceType.GetType().Name}", type.Identifier, CurrentFile);
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, string tag = "")
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER, $"(pointer) {tag}");
                return 1;
            }

            if (type.IsStackArray)
            {
                int stackSize = type.StackArraySize;

                int size = 0;
                for (int i = 0; i < stackSize; i++)
                {
                    size += GenerateInitialValue(type.StackArrayOf, $"tag[{i}]");
                }
                return size;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
            return 1;
        }
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="CompilerException"></exception>
        /// <exception cref="InternalException"></exception>
        int GenerateInitialValue(CompiledType type, Action<int> afterValue, string tag = "")
        {
            if (type.IsStruct)
            {
                int size = 0;
                foreach (CompiledField field in type.Struct.Fields)
                {
                    size++;
                    AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(field.Type), $"{tag}{(string.IsNullOrWhiteSpace(tag) ? "" : ".")}{field.Identifier}");
                    afterValue?.Invoke(size);
                }
                return size;
            }

            if (type.IsClass)
            {
                AddInstruction(Opcode.PUSH_VALUE, LanguageCore.Utils.NULL_POINTER, $"(pointer) {tag}");
                afterValue?.Invoke(0);
                return 1;
            }

            AddInstruction(Opcode.PUSH_VALUE, GetInitialValue(type), tag);
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

        #region HEAP Helpers

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

        void StoreToStack(int destination, int size, bool basepointerRelative)
        {
            for (int i = size - 1; i >= 0; i--)
            {
                AddInstruction(Opcode.STORE_VALUE, basepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE, destination + i);
            }
        }
        void CopyToStack(int to, int size, bool basepointerRelative)
        {
            // TODO: Optimize this

            AddComment($"Copy to stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                int currentReversedOffset = size - currentOffset - 1;

                AddComment($"Element {currentOffset}:");

                int loadFrom = (-currentReversedOffset) - 1;
                int storeTo = to + currentOffset;

                AddInstruction(Opcode.LOAD_VALUE, AddressingMode.RELATIVE, loadFrom);

                AddInstruction(Opcode.STORE_VALUE, basepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE, storeTo);
            }
            AddComment("}");
        }
        void LoadFromStack(int from, int size, bool basepointerRelative)
        {
            AddComment($"Load from stack: {{");
            for (int currentOffset = 0; currentOffset < size; currentOffset++)
            {
                AddComment($"Element {currentOffset}:");

                int loadFrom = from + currentOffset;

                AddInstruction(Opcode.LOAD_VALUE, basepointerRelative ? AddressingMode.BASEPOINTER_RELATIVE : AddressingMode.ABSOLUTE, loadFrom);
            }
            AddComment("}");
        }

        #endregion

        #region GetDataOffset

        bool GetDataOffset(StatementWithValue value, out int offset)
        {
            offset = default;

            if (value is Field field)
            { return GetDataOffset(field, out offset); }

            if (value is IndexCall indexCall)
            { return GetDataOffset(indexCall, out offset); }

            if (value is Identifier)
            { offset = 0; return true; }

            throw new NotImplementedException();
        }
        bool GetDataOffset(Field value, out int offset)
        {
            offset = default;
            if (!GetDataOffset(value.PrevStatement, out int previousOffset))
            { return false; }

            int currentOffset;
            CompiledType prevType = FindStatementType(value.PrevStatement);

            if (prevType.IsStruct)
            {
                if (!prevType.Struct.FieldOffsets.TryGetValue(value.FieldName.Content, out currentOffset))
                { return false; }
            }
            else if (prevType.IsClass)
            {
                CompiledClass @class = prevType.Class;

                @class.AddTypeArguments(TypeArguments);
                @class.AddTypeArguments(prevType.TypeParameters);
                IReadOnlyDictionary<string, int> fieldOffsets = @class.FieldOffsets;
                @class.ClearTypeArguments();

                if (!fieldOffsets.TryGetValue(value.FieldName.Content, out currentOffset))
                { return false; }
            }
            else
            { throw new NotImplementedException(); }

            offset = previousOffset + currentOffset;
            return true;
        }
        bool GetDataOffset(IndexCall value, out int offset)
        {
            offset = default;
            if (!GetDataOffset(value.PrevStatement, out int previousOffset))
            { return false; }

            CompiledType prevType = FindStatementType(value.PrevStatement);

            if (!prevType.IsStackArray)
            { return false; }

            if (!TryCompute(value.Expression, RuntimeType.INT, out DataItem index))
            { throw new NotImplementedException(); }

            int currentOffset = index.ValueInt * prevType.StackArrayOf.SizeOnStack;

            offset = previousOffset + currentOffset;
            return true;
        }

        #endregion

        #region Addressing Helpers

        const int TagsBeforeBasePointer = 2;

        int ReturnValueOffset => -(ParametersSize + 1 + TagsBeforeBasePointer);
        const int ReturnFlagOffset = 0;
        const int SavedBasePointerOffset = -1;
        const int SavedCodePointerOffset = -2;

        int GetDataAddress(StatementWithValue value, out AddressingMode addressingMode)
        {
            if (value is IndexCall indexCall)
            { return GetDataAddress(indexCall, out addressingMode); }

            if (value is Identifier identifier)
            { return GetDataAddress(identifier, out addressingMode); }

            if (value is Field field)
            { return GetDataAddress(field, out addressingMode); }

            throw new NotImplementedException();
        }

        static int GetDataAddress(CompiledVariable val, out AddressingMode addressingMode)
        {
            if (val.IsStoredInHEAP)
            { throw new InternalException($"This should never occur: trying to GetDataAddress of variable \"{val.VariableName.Content}\" which's type is {val.Type}"); }

            addressingMode = val.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE;

            return val.MemoryAddress;
        }
        int GetDataAddress(Identifier variable, out AddressingMode addressingMode)
        {
            if (GetParameter(variable.Content, out CompiledParameter param))
            {
                addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                return GetBaseAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            { return GetDataAddress(val, out addressingMode); }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
        }
        int GetDataAddress(Field field, out AddressingMode addressingMode)
        {
            var prevType = FindStatementType(field.PrevStatement);

            IReadOnlyDictionary<string, int> fieldOffsets;

            if (prevType.IsStruct)
            { fieldOffsets = prevType.Struct.FieldOffsets; }
            else if (prevType.IsClass)
            { fieldOffsets = prevType.Class.FieldOffsets; }
            else
            { throw new NotImplementedException(); }

            if (fieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
            {
                if (field.PrevStatement is Identifier _prevVar)
                {
                    if (GetParameter(_prevVar.Content, out CompiledParameter param))
                    {
                        addressingMode = AddressingMode.BASEPOINTER_RELATIVE;

                        return GetBaseAddress(param, fieldOffset);
                    }
                    return fieldOffset + GetDataAddress(_prevVar, out addressingMode);
                }

                return GetDataAddress(field.PrevStatement, out addressingMode) + fieldOffset;
            }

            throw new NotImplementedException();
        }
        int GetDataAddress(IndexCall indexCall, out AddressingMode addressingMode)
        {
            var prevType = FindStatementType(indexCall.PrevStatement);

            if (!prevType.IsStackArray)
            { throw new NotImplementedException(); }

            if (!TryCompute(indexCall.Expression, RuntimeType.INT, out DataItem index))
            { throw new NotImplementedException(); }

            int address = GetDataAddress(indexCall.PrevStatement, out addressingMode);

            int currentOffset = index.ValueInt * prevType.StackArrayOf.SizeOnStack;

            return address + currentOffset;
        }

        int GetFieldOffset(Field field)
        {
            CompiledType prevType = FindStatementType(field.PrevStatement);

            if (prevType.IsStruct)
            {
                CompiledStruct @struct = prevType.Struct;

                if (@struct.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Identifier) return fieldOffset;
                    if (field.PrevStatement is Field prevField) return fieldOffset + GetFieldOffset(prevField);
                }

                throw new NotImplementedException();
            }

            if (prevType.IsClass)
            {
                CompiledClass @class = prevType.Class;

                @class.AddTypeArguments(TypeArguments);
                @class.AddTypeArguments(prevType.TypeParameters);

                if (@class.FieldOffsets.TryGetValue(field.FieldName.Content, out int fieldOffset))
                {
                    if (field.PrevStatement is Identifier) return fieldOffset;
                    if (field.PrevStatement is Field prevField) return fieldOffset + GetFieldOffset(prevField);
                }

                @class.ClearTypeArguments();

                throw new NotImplementedException();
            }

            throw new NotImplementedException();
        }

        int GetBaseAddress(StatementWithValue statement, out AddressingMode addressingMode, out bool inHeap)
        {
            if (statement is Identifier identifier)
            { return GetBaseAddress(identifier, out addressingMode, out inHeap); }

            if (statement is Field field)
            { return GetBaseAddress(field, out addressingMode, out inHeap); }

            if (statement is IndexCall indexCall)
            { return GetBaseAddress(indexCall, out addressingMode, out inHeap); }

            throw new NotImplementedException();
        }

        int GetBaseAddress(CompiledParameter parameter) => -(ParametersSizeBefore(parameter.Index) + TagsBeforeBasePointer);
        int GetBaseAddress(CompiledParameter parameter, int offset) => -(ParametersSizeBefore(parameter.Index) - offset + TagsBeforeBasePointer);
        static int GetBaseAddress(CompiledVariable variable, out AddressingMode addressingMode, out bool inHeap)
        {
            addressingMode = variable.IsGlobal ? AddressingMode.ABSOLUTE : AddressingMode.BASEPOINTER_RELATIVE;
            inHeap = variable.IsStoredInHEAP;
            return variable.MemoryAddress;
        }
        int GetBaseAddress(Identifier variable, out AddressingMode addressingMode, out bool inHeap)
        {
            if (GetParameter(variable.Content, out CompiledParameter param))
            {
                addressingMode = AddressingMode.BASEPOINTER_RELATIVE;
                inHeap = false;
                return GetBaseAddress(param);
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            { return GetBaseAddress(val, out addressingMode, out inHeap); }

            throw new CompilerException($"Variable \"{variable.Content}\" not found", variable, CurrentFile);
        }
        int GetBaseAddress(Field statement, out AddressingMode addressingMode, out bool inHeap) => GetBaseAddress(statement.PrevStatement, out addressingMode, out inHeap);
        int GetBaseAddress(IndexCall statement, out AddressingMode addressingMode, out bool inHeap) => GetBaseAddress(statement.PrevStatement, out addressingMode, out inHeap);

        bool IsItInHeap(Identifier variable)
        {
            if (GetParameter(variable.Content, out var parameter))
            {
                return parameter.Type.InHEAP;
            }

            if (GetVariable(variable.Content, out CompiledVariable val))
            {
                return val.IsStoredInHEAP;
            }

            throw new CompilerException($"Local symbol \"{variable.Content}\" not found", variable, CurrentFile);
        }
        bool IsItInHeap(IndexCall indexCall)
        {
            if (indexCall.PrevStatement is Identifier prevVar) return IsItInHeap(prevVar);
            if (indexCall.PrevStatement is Field prevField) return IsItInHeap(prevField);
            if (indexCall.PrevStatement is IndexCall prevIndexCall) return IsItInHeap(prevIndexCall);

            throw new NotImplementedException();
        }
        bool IsItInHeap(Field field)
        {
            if (field.PrevStatement is Identifier prevVar) return IsItInHeap(prevVar);
            if (field.PrevStatement is Field prevField) return IsItInHeap(prevField);
            if (field.PrevStatement is IndexCall prevIndexCall) return IsItInHeap(prevIndexCall);

            throw new NotImplementedException();
        }

        #endregion
    }
}