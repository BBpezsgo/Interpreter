using LanguageCore.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore.Profiling;

public abstract class Profiler
{
    protected readonly CompiledDebugInformation _debugInformation;
    readonly double _tickToMilliseconds;

    public Profiler(CompiledDebugInformation debugInformation, double tickToMilliseconds = 0.001)
    {
        _tickToMilliseconds = tickToMilliseconds;
        _debugInformation = debugInformation;
    }

    protected string GetFrameName(CallTraceItem item, bool squareBrackets = false)
    {
        if (_debugInformation.TryGetFunctionInformation(item.InstructionPointer, out FunctionInformation f))
        {
            if (f.IsTopLevelStub) return squareBrackets ? "[top_level_statements]" : "(top_level_statements)";
            else if (f.Function is CompiledLambda) return squareBrackets ? "[lambda]" : "(lambda)";
            else if (f.Function is CompiledFunctionDefinition w) return w.Identifier;
        }

        return squareBrackets ? "[unknown]" : "(unknown)";
    }

    protected Location GetLocation(CallTraceItem item)
    {
        if (_debugInformation.TryGetFunctionInformation(item.InstructionPointer, out FunctionInformation f))
        {
            if (f.IsTopLevelStub)
            {
                if (_debugInformation.TryGetSourceLocation(item.InstructionPointer, out SourceCodeLocation l0, true))
                {
                    SourceCodeLocation l1 = _debugInformation.SourceCodeLocations.FirstOrDefault(v => v.Location.File == l0.Location.File);
                    if (l1.Location.File is not null)
                    {
                        return l1.Location;
                    }
                    return default;
                }
            }

            if (f.File is null)
            {
                return default;
            }
            return new Location(f.SourcePosition, f.File);
        }

        if (_debugInformation.TryGetSourceLocation(item.InstructionPointer, out SourceCodeLocation l, true))
        {
            return l.Location;
        }

        return default;
    }

    protected TimeSpan TickToTimestamp(ulong ticks) => TimeSpan.FromMilliseconds(ticks * _tickToMilliseconds);

    public abstract void Sample(in ProcessorState state, ulong tick);
    public abstract void WriteTo(string path);
}
