namespace LanguageCore.Parser;

public static class ExportableExtensions
{
    public static bool CanUse(this IExportable self, Uri? sourceFile)
    {
        if (self.IsExported) return true;
        if (sourceFile == null) return true;
        if (sourceFile == self.File) return true;
        return false;
    }
}

public interface IExportable : IInFile
{
    bool IsExported { get; }
}

public interface IHaveType
{
    TypeInstance Type { get; }
}

public interface IReferenceableTo<TReference> : IInFile, IReferenceableTo
{
    new TReference? Reference { get; set; }
    object? IReferenceableTo.Reference
    {
        get => Reference;
        set => Reference = (TReference?)value;
    }
}

public interface IReferenceableTo : IInFile
{
    object? Reference { get; set; }
}

public enum LiteralType
{
    Integer,
    Float,
    String,
    Char,
}
