using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LanguageCore.Runtime
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct SourceCodeLocation
    {
        public Range<int> Instructions;
        public Position SourcePosition;

        public readonly bool Contains(int instruction) =>
            Instructions.Start <= instruction &&
            Instructions.End >= instruction;

        public override readonly string ToString() => $"({Instructions} -> {SourcePosition.ToMinString()})";
        readonly string GetDebuggerDisplay() => ToString();
    }

    public enum StackElementKind
    {
        Internal,
        Variable,
        Parameter,
    }

    public enum StackElementType
    {
        Value,
        HeapPointer,
        StackPointer,
    }

    public struct StackElementInformations
    {
        public StackElementKind Kind;
        public StackElementType Type;
        public string Tag;

        public int Address;
        public bool BasepointerRelative;
        public int Size;

        public readonly Range<int> GetRange(int basepointer)
        {
            int itemStart = this.Address;
            if (this.BasepointerRelative) itemStart += basepointer;
            int itemEnd = itemStart + this.Size - 1;
            return (itemStart, itemEnd);
        }
    }

    public struct ScopeInformations
    {
        public SourceCodeLocation Location;
        public List<StackElementInformations> Stack;
    }

    public readonly struct CollectedScopeInfo
    {
        public readonly StackElementInformations[] Stack;

        public CollectedScopeInfo(StackElementInformations[] stack)
        {
            Stack = stack;
        }

        public bool TryGet(int basePointer, int stackAddress, out StackElementInformations result)
        {
            for (int i = 0; i < Stack.Length; i++)
            {
                StackElementInformations item = Stack[i];
                Range<int> range = item.GetRange(basePointer);

                if (range.Contains(stackAddress))
                {
                    result = item;
                    return true;
                }
            }
            result = default;
            return false;
        }
    }

    public struct FunctionInformations
    {
        public bool IsValid;
        public Position SourcePosition;
        public string Identifier;
        public string? File;
        public string ReadableIdentifier;
        public bool IsMacro;
        public Range<int> Instructions;

        public override readonly string ToString()
        {
            if (!IsValid) return "<unknown>";
            StringBuilder result = new();

            if (IsMacro)
            { result.Append("macro "); }

            result.Append(ReadableIdentifier);

            return result.ToString();
        }
    }

    public class DebugInformation
    {
        public readonly List<SourceCodeLocation> SourceCodeLocations;
        public readonly List<FunctionInformations> FunctionInformations;
        public readonly List<ScopeInformations> ScopeInformations;
        public readonly Dictionary<int, List<string>> CodeComments;

        public DebugInformation()
        {
            SourceCodeLocations = new List<SourceCodeLocation>();
            FunctionInformations = new List<FunctionInformations>();
            ScopeInformations = new List<ScopeInformations>();
            CodeComments = new Dictionary<int, List<string>>();
        }

        public static int[] TraceBasePointers(DataItem[] stack, int basePointer)
        {
            List<int> result = new();
            TraceBasePointers(result, stack, basePointer);
            return result.ToArray();
        }

        static void TraceBasePointers(List<int> result, DataItem[] stack, int basePointer)
        {
            if (basePointer < 1) return;
            if (basePointer - 1 >= stack.Length) return;
            DataItem item = stack[basePointer - 1];
            if (item.Type != RuntimeType.SInt32) return;
            int num = item.ValueSInt32;
            result.Add(num);
            if (num == basePointer) return;
            TraceBasePointers(result, stack, num);
        }

        public SourceCodeLocation[] GetSourceLocations(int instruction)
        {
            List<SourceCodeLocation> result = new();
            for (int i = 0; i < SourceCodeLocations.Count; i++)
            {
                SourceCodeLocation sourceLocation = SourceCodeLocations[i];
                if (!sourceLocation.Contains(instruction))
                { continue; }
                result.Add(sourceLocation);
            }
            return result.ToArray();
        }

        public bool TryGetSourceLocation(int instruction, out SourceCodeLocation sourceLocation)
        {
            sourceLocation = default;
            bool success = false;

            for (int i = 0; i < SourceCodeLocations.Count; i++)
            {
                SourceCodeLocation _sourceLocation = SourceCodeLocations[i];
                if (!_sourceLocation.Instructions.Contains(instruction))
                { continue; }
                if (success && sourceLocation.Instructions.Size() < _sourceLocation.Instructions.Size())
                { continue; }
                sourceLocation = _sourceLocation;
                success = true;
            }

            return success;
        }

        public FunctionInformations[] GetFunctionInformations(int[] callstack)
        {
            FunctionInformations[] result = new FunctionInformations[callstack.Length];
            for (int i = 0; i < callstack.Length; i++)
            { result[i] = GetFunctionInformations(callstack[i]); }
            return result;
        }

        public FunctionInformations GetFunctionInformations(int codePointer)
        {
            for (int j = 0; j < FunctionInformations.Count; j++)
            {
                FunctionInformations info = FunctionInformations[j];

                if (info.Instructions.Contains(codePointer))
                { return info; }
            }
            return default;
        }

        public ScopeInformations[] GetScopes(int codePointer)
        {
            List<ScopeInformations> result = new();

            for (int i = 0; i < ScopeInformations.Count; i++)
            {
                ScopeInformations scope = ScopeInformations[i];
                if (!scope.Location.Contains(codePointer)) continue;
                result.Add(scope);
            }

            return result.ToArray();
        }

        public CollectedScopeInfo GetScopeInformations(int codePointer)
        {
            ScopeInformations[] scopes = GetScopes(codePointer);
            List<StackElementInformations> result = new();

            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i].Stack == null) continue;
                for (int j = 0; j < scopes[i].Stack.Count; j++)
                {
                    StackElementInformations item = scopes[i].Stack[j];
                    result.Add(item);
                }
            }

            return new CollectedScopeInfo(result.ToArray());
        }
    }
}
