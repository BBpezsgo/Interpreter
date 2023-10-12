using System.Collections.Generic;
using System.Diagnostics;

namespace LanguageCore.Runtime
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct SourceCodeLocation
    {
        public Range<int> Instructions;
        public Position SourcePosition;

        public readonly bool Contains(int instruction) =>
            Instructions.Start >= instruction &&
            Instructions.End <= instruction;

        public override readonly string ToString() => $"({Instructions} -> {SourcePosition.ToMinString()})";
        readonly string GetDebuggerDisplay() => ToString();
    }

    public struct FunctionInformations
    {
        public bool IsValid;
        public Position SourcePosition;
        public string Identifier;
        public string File;
        public string ReadableIdentifier;
        public bool IsMacro;

        public override readonly string ToString()
        {
            if (!IsValid) return "<unknown>";
            return $"{ReadableIdentifier}";
        }
    }

    public class DebugInformation
    {
        public readonly List<SourceCodeLocation> SourceCodeLocations;
        public readonly Dictionary<int, FunctionInformations> FunctionInformations;

        public DebugInformation()
        {
            SourceCodeLocations = new List<SourceCodeLocation>();
            FunctionInformations = new Dictionary<int, FunctionInformations>();
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
                if (success && sourceLocation.Instructions.Sum() < _sourceLocation.Instructions.Sum())
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
            {
                if (FunctionInformations.TryGetValue(callstack[i], out FunctionInformations functionInformations))
                { result[i] = functionInformations; }
            }
            return result;
        }
    }
}
