using System.IO;
using System.Text.Json;
using LanguageCore.Compiler;
using LanguageCore.Parser;
using LanguageCore.Runtime;

namespace LanguageCore.Profiling;

public sealed class GoogleProfiler : Profiler
{
    sealed class Node
    {
        public required int Id;
        public required string FunctionName;
        public string? File;
        public int? Line;
        public int? Column;
        public int? Allocated;
        public required Dictionary<string, Node> ChildrenByKey;
        public readonly List<int> ChildIds = new();
    }

    readonly Node _root;
    readonly List<Node> _allNodes = new();
    readonly List<int> _samples = new();
    readonly List<int> _heapSamples = new();
    readonly List<ulong> _tickDeltas = new();
    int _nextNodeId = 1;
    ulong _lastTick;
    TimeSpan _startTs;
    bool _first = true;
    IReadOnlyList<CallTraceItem> _previousStacktrace = Array.Empty<CallTraceItem>();
    Node? _lastNode = null;
    int heapAllocInstruction = -1;
    int heapSize = 0;

    public GoogleProfiler(CompiledDebugInformation debugInformation, double tickToMicroseconds = 1.0) : base(debugInformation, tickToMicroseconds)
    {
        _root = NewNode(null, "(root)", null, null, null, null);
    }

    Node NewNode(Node? parent, string functionName, string? file, int? line, int? column, int? allocated)
    {
        Node node = new()
        {
            Id = _nextNodeId++,
            FunctionName = functionName,
            File = file,
            Line = line,
            Column = column,
            ChildrenByKey = new(),
            Allocated = allocated,
        };
        _allNodes.Add(node);
        parent?.ChildIds.Add(node.Id);
        return node;
    }

    public override void Sample(in ProcessorState state, ulong tick)
    {
        heapSize = state.Memory.Length;

        if (_debugInformation.TryGetFunctionInformation(state.Registers.CodePointer, out FunctionInformation f) && (state.Registers.CodePointer < f.FrameInstructions.Start || state.Registers.CodePointer >= f.FrameInstructions.End)) return;

        List<CallTraceItem> stacktrace = new();
        DebugUtils.TraceStack(state.Memory, state.Registers.BasePointer, _debugInformation.StackOffsets, stacktrace);
        stacktrace.Reverse();
        stacktrace.Add(new CallTraceItem(state.Registers.BasePointer, state.Registers.CodePointer));

        int? allocated = null;

        {
            if ((heapAllocInstruction == -1 || heapAllocInstruction == state.Registers.CodePointer)
                && _debugInformation.TryGetFunctionInformation(state.Registers.CodePointer, out FunctionInformation f0)
                && f0.Function is not null
                && f0.Function.Attributes.TryGetAttribute(AttributeConstants.BuiltinIdentifier, out AttributeUsage? a)
                && a.TryGetValue(out string? p0)
                && p0 == "alloc"
                && f0.Function.Parameters.Length == 1
                && f0.Function.Parameters[0].Type.SameAs(BasicType.I32))
            {
                CollectedScopeInfo scope = _debugInformation.GetScopeInformation(state.Registers.CodePointer);
                StackElementInformation sizeParameter = scope.Stack.FirstOrDefault(v => v.Identifier == f0.Function.Parameters[0].Identifier && v.Kind == StackElementKind.Parameter);
                StackElementInformation returnValue = scope.Stack.FirstOrDefault(v => v.Identifier == "Return Value" && v.Kind == StackElementKind.Internal);
                if (sizeParameter.Identifier is not null
                    && sizeParameter.Type.SameAs(BasicType.I32)
                    && state.Memory.TryGet<int>(sizeParameter.AbsoluteAddress(stacktrace[^1].BasePointer, 0), out int size))
                {
                    if (heapAllocInstruction == -1) heapAllocInstruction = state.Registers.CodePointer;
                    allocated = size;
                }
            }
        }

        if (false && _previousStacktrace.Count == stacktrace.Count)
        {
            bool same = true;
            for (int i = 0; i < stacktrace.Count - 1; i++)
            {
                if (stacktrace[i].InstructionPointer != _previousStacktrace[i].InstructionPointer)
                {
                    same = false;
                    break;
                }
            }

            if (same)
            {
                _tickDeltas[^1] += tick - _lastTick;
                _lastTick = tick;
                return;
            }
        }

        _previousStacktrace = stacktrace;

        Node current = _root;
        foreach (CallTraceItem frame in stacktrace)
        {
            if (frame.InstructionPointer <= 0 || frame.InstructionPointer >= state.Code.Length) continue;

            string name = GetFrameName(frame);
            Location location = GetLocation(frame);
            string key = $"{name} {(location.IsDefault ? "" : location.Position.Range.Start.Line.ToString())}".TrimEnd();

            if (!current.ChildrenByKey.TryGetValue(key, out Node? child))
            {
                child = NewNode(current, name, location.IsDefault ? null : location.File.IsFile ? location.File.LocalPath : location.File.ToString(), location.IsDefault ? null : location.Position.Range.Start.Line, location.IsDefault ? null : location.Position.Range.Start.Character, allocated);
                current.ChildrenByKey[key] = child;
            }

            current = child;
        }

        if (_first)
        {
            _startTs = TickToTimestamp(tick);
            _tickDeltas.Add(0);
            _first = false;
        }
        else
        {
            if (false && _lastNode == current)
            {
                _tickDeltas[^1] += tick - _lastTick;
                _lastTick = tick;
                return;
            }

            _tickDeltas.Add(tick - _lastTick);
        }

        _samples.Add(current.Id);
        if (allocated.HasValue) _heapSamples.Add(current.Id);
        _lastTick = tick;
        _lastNode = current;
    }

    public override void WriteTo(string path)
    {
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read, 512);
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();

        writer.WriteStartArray("nodes");
        foreach (Node node in _allNodes)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", node.Id);

            writer.WriteStartObject("callFrame");
            writer.WriteString("functionName", node.FunctionName);
            writer.WriteString("scriptId", "0");
            writer.WriteString("url", node.File ?? "");
            writer.WriteNumber("lineNumber", node.Line ?? -1);
            writer.WriteNumber("columnNumber", node.Column ?? -1);
            writer.WriteEndObject();

            if (node.ChildIds.Count > 0)
            {
                writer.WriteStartArray("children");
                foreach (int childId in node.ChildIds)
                    writer.WriteNumberValue(childId);
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteNumber("startTime", _startTs.TotalMilliseconds);
        writer.WriteNumber("endTime", TickToTimestamp(_lastTick).TotalMilliseconds);

        writer.WriteStartArray("samples");
        foreach (int nodeId in _samples) writer.WriteNumberValue(nodeId);
        writer.WriteEndArray();

        writer.WriteStartArray("timeDeltas");
        foreach (ulong delta in _tickDeltas) writer.WriteNumberValue(TickToTimestamp(delta).TotalMilliseconds);
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    public void WriteHeapProfileTo(string path)
    {
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read, 512);
        using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false });

        writer.WriteStartObject();

        HashSet<int> skipNodes = new(_allNodes.Count);
        while (true)
        {
            bool done = true;
            foreach (Node node in _allNodes)
            {
                if (skipNodes.Contains(node.Id)) continue;
                if (node.Allocated.HasValue) continue;
                int childCount = node.ChildIds.Count(v => !skipNodes.Contains(v));
                if (childCount > 0) continue;
                done = false;
                skipNodes.Add(node.Id);
            }
            if (done) break;
        }

        void WriteNode(Utf8JsonWriter writer, Node node)
        {
            writer.WriteStartObject();

            writer.WriteStartObject("callFrame");
            writer.WriteString("functionName", node.FunctionName);
            writer.WriteString("scriptId", "0");
            writer.WriteString("url", node.File ?? "");
            writer.WriteNumber("lineNumber", node.Line ?? -1);
            writer.WriteNumber("columnNumber", node.Column ?? -1);
            writer.WriteEndObject();

            writer.WriteNumber("selfSize", (node.Allocated ?? 0) * 1024);
            writer.WriteNumber("id", node.Id);

            writer.WriteStartArray("children");
            foreach (int childId in node.ChildIds)
            {
                if (skipNodes.Contains(childId)) continue;
                WriteNode(writer, _allNodes.First(v => v.Id == childId));
            }
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        writer.WritePropertyName("head");
        WriteNode(writer, _root);

        writer.WriteStartArray("samples");
        for (int i = 0; i < _heapSamples.Count; i++)
        {
            writer.WriteStartObject();
            writer.WriteNumber("size", heapSize * 1024);
            writer.WriteNumber("nodeId", _heapSamples[i]);
            writer.WriteNumber("ordinal", i + 1);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
