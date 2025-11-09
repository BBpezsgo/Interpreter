namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public class DebugInformation : IDuplicatable<DebugInformation>
{
    public readonly List<SourceCodeLocation> SourceCodeLocations;
    public readonly List<FunctionInformation> FunctionInformation;
    public readonly List<ScopeInformation> ScopeInformation;
    public readonly Dictionary<int, List<string>> CodeComments;
    public readonly Dictionary<Uri, ImmutableArray<Tokenizing.Token>> OriginalFiles;
    public StackOffsets StackOffsets;

    public DebugInformation(IEnumerable<KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>> originalFiles)
    {
        SourceCodeLocations = new List<SourceCodeLocation>();
        FunctionInformation = new List<FunctionInformation>();
        ScopeInformation = new List<ScopeInformation>();
        CodeComments = new Dictionary<int, List<string>>();
        OriginalFiles = new Dictionary<Uri, ImmutableArray<Tokenizing.Token>>(originalFiles);
        StackOffsets = new StackOffsets(0, 0);
    }

    public void OffsetCodeFrom(int from, int offset)
    {
        {
            Dictionary<int, List<string>> newCodeComments = new();
            foreach ((int key, List<string> comments) in CodeComments)
            {
                int offsetted = key;
                if (key >= from) offsetted += offset;
                if (newCodeComments.TryGetValue(offsetted, out List<string>? existing)) existing.AddRange(comments);
                else newCodeComments.Add(offsetted, comments);
            }
            CodeComments.Clear();
            CodeComments.AddRange(newCodeComments);
        }

        for (int i = 0; i < SourceCodeLocations.Count; i++)
        {
            SourceCodeLocation loc = SourceCodeLocations[i];

            if (loc.Instructions.Start > from)
            { loc.Instructions.Start += offset; }
            if (loc.Instructions.End > from)
            { loc.Instructions.End += offset; }

            SourceCodeLocations[i] = loc;
        }

        for (int i = 0; i < FunctionInformation.Count; i++)
        {
            FunctionInformation func = FunctionInformation[i];

            if (func.Instructions.Start > from)
            { func.Instructions.Start += offset; }
            if (func.Instructions.End > from)
            { func.Instructions.End += offset; }

            FunctionInformation[i] = func;
        }

        for (int i = 0; i < ScopeInformation.Count; i++)
        {
            ScopeInformation scope = ScopeInformation[i];

            if (scope.Location.Instructions.Start > from)
            { scope.Location.Instructions.Start += offset; }
            if (scope.Location.Instructions.End > from)
            { scope.Location.Instructions.End += offset; }

            ScopeInformation[i] = scope;
        }
    }

    public DebugInformation Duplicate()
    {
        DebugInformation copy = new(OriginalFiles);

        copy.SourceCodeLocations.AddRange(SourceCodeLocations);
        copy.FunctionInformation.AddRange(FunctionInformation);
        copy.ScopeInformation.AddRange(ScopeInformation);
        copy.CodeComments.AddRange(CodeComments);

        return copy;
    }
}
