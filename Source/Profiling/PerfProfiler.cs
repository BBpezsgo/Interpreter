using System.IO;
using LanguageCore.Runtime;

namespace LanguageCore.Profiling;

public class PerfProfiler : Profiler
{
    sealed class ProfilerSample : IEquatable<ProfilerSample>
    {
        public ulong Tick { get; }
        public ImmutableArray<ProfilerStackTraceItem> Trace { get; }

        public ProfilerSample(ulong tick, ImmutableArray<ProfilerStackTraceItem> trace)
        {
            Tick = tick;
            Trace = trace;
        }

        public override int GetHashCode() => base.GetHashCode();
        public override bool Equals(object? obj) => Equals(obj as ProfilerSample);
        public bool Equals(ProfilerSample? other) => other is not null && Tick == other.Tick && Trace.SequenceEqual(other.Trace, (a, b) => a.Equals(b));
    }

    readonly struct ProfilerStackTraceItem : IEquatable<ProfilerStackTraceItem>
    {
        public int Instruction { get; }
        public string FunctionName { get; }
        public string? Source { get; }

        public ProfilerStackTraceItem(int instruction, string functionName, string? source)
        {
            Instruction = instruction;
            FunctionName = functionName;
            Source = source;
        }

        public override bool Equals(object? obj) => obj is ProfilerStackTraceItem item && Equals(item);
        public bool Equals(ProfilerStackTraceItem other) =>
            Instruction == other.Instruction
            && FunctionName == other.FunctionName
            && Source == other.Source;
        public override int GetHashCode() => base.GetHashCode();

        public static bool operator ==(ProfilerStackTraceItem left, ProfilerStackTraceItem right) => left.Equals(right);
        public static bool operator !=(ProfilerStackTraceItem left, ProfilerStackTraceItem right) => !left.Equals(right);
    }

    readonly List<ProfilerSample> _samples = new();

    public PerfProfiler(CompiledDebugInformation debugInformation, double tickToMicroseconds = 1.0) : base(debugInformation, tickToMicroseconds)
    {

    }

    public override void Sample(in ProcessorState state, ulong tick)
    {
        if (_debugInformation.TryGetFunctionInformation(state.Registers.CodePointer, out FunctionInformation f) && (state.Registers.CodePointer < f.FrameInstructions.Start || state.Registers.CodePointer >= f.FrameInstructions.End)) return;

        List<CallTraceItem> stacktrace = new();
        DebugUtils.TraceStack(state.Memory, state.Registers.BasePointer, _debugInformation.StackOffsets, stacktrace);
        stacktrace.Reverse();
        stacktrace.Add(new CallTraceItem(state.Registers.BasePointer, state.Registers.CodePointer));

        List<ProfilerStackTraceItem> profilerStackTraceItems = new(stacktrace.Count);
        foreach (CallTraceItem frame in stacktrace)
        {
            if (frame.InstructionPointer <= 0 || frame.InstructionPointer >= state.Code.Length) continue;

            string name = GetFrameName(frame, true);
            string? source = GetLocation(frame).ToString();

            profilerStackTraceItems.Add(new(frame.InstructionPointer, name, source));
        }

        if (_samples.Count > 0 && _samples[^1].Equals(new(_samples[^1].Tick, profilerStackTraceItems.ToImmutableArray()))) return;

        _samples.Add(new ProfilerSample(tick, profilerStackTraceItems.ToImmutableArray()));
    }

    public override void WriteTo(string filename)
    {
        using FileStream file = new(filename, FileMode.Create, FileAccess.Write);
        using StreamWriter writer = new(file);

        foreach (ProfilerSample sample in _samples)
        {
            writer.WriteLine($"bblang 0/0 {TickToTimestamp(sample.Tick).TotalMilliseconds.ToString("0.000000", CultureInfo.InvariantCulture)}: 1 cpu/cycles/Pu:");
            foreach (ProfilerStackTraceItem item in sample.Trace)
            {
                writer.WriteLine($"	    0x{Convert.ToString(item.Instruction, 16)} {item.FunctionName} ({item.Source})");
            }
            writer.WriteLine();
        }
    }
}
