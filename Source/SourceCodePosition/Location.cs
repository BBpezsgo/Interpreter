namespace LanguageCore;

public readonly struct Location :
    IEquatable<Location>,
    ILocated,
    IPositioned,
    IInFile
{
    public Position Position { get; }
    public Uri File { get; }

    Location ILocated.Location => this;

    public Location(Position position, Uri file)
    {
        Position = position;
        File = file;
    }

    public override string ToString() => $"{File}:{Position.Range.Start.Line}:{Position.Range.Start.Character}";
    public override int GetHashCode() => HashCode.Combine(Position, File);
    public override bool Equals([NotNullWhen(true)] object? obj) => obj is Location other && Equals(other);
    public bool Equals(Location other) => Position.Equals(other.Position) && File.Equals(other.File);
    internal Location After() => new(Position.After(), File);
    internal Location Before() => new(Position.Before(), File);
    internal Location NextLine() => new(Position.NextLine(), File);

    public Location Union(Location other)
    {
        if (File != other.File) return this;
        return new Location(Position.Union(Position, other.Position), File);
    }

    public static bool operator ==(Location left, Location right) => left.Equals(right);
    public static bool operator !=(Location left, Location right) => !left.Equals(right);
}
