namespace LanguageCore.ASM;

public class DataSectionBuilder : SectionBuilder
{
    readonly List<string> DataLabels;

    public DataSectionBuilder() : base()
    {
        this.DataLabels = new List<string>();
    }

    bool HasLabel(string dataLabel)
    {
        for (int i = 0; i < DataLabels.Count; i++)
        {
            if (string.Equals(DataLabels[i], dataLabel, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public string NewString(string data, string? name = null, int labelLength = 16)
    {
        string label = AssemblyCode.GenerateLabel("d_" + name + "_", labelLength, HasLabel);
        DataLabels.Add(label);
        AppendText(' ', Indent);
        AppendTextLine($"{label}:");
        AppendText(' ', Indent + IndentIncrement);
        if (Utils.Escape(ref data))
        { AppendTextLine($"db `{data}`, 0"); }
        else
        { AppendTextLine($"db \"{data}\", 0"); }
        return label;
    }
}
