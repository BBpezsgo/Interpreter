using LanguageCore;

namespace Tests;

[TestClass, TestCategory("Code Position")]
public class PositionTests
{
    [TestMethod]
    public void Test01()
    {
        Position v = new(new Range<SinglePosition>((0, 0), (0, 2)), new Range<int>(0, 2));
        (Position a, Position b) = v.Slice(1);

        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 0), (0, 1)), new Range<int>(0, 1)), a);
        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 1), (0, 2)), new Range<int>(1, 2)), b);
    }

    [TestMethod]
    public void Test02()
    {
        Position v = new(new Range<SinglePosition>((0, 0), (0, 3)), new Range<int>(0, 3));
        (Position a, Position b) = v.Slice(1);

        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 0), (0, 1)), new Range<int>(0, 1)), a);
        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 1), (0, 3)), new Range<int>(1, 3)), b);
    }

    [TestMethod]
    public void Test03()
    {
        Position v = new(new Range<SinglePosition>((0, 0), (0, 3)), new Range<int>(0, 3));
        (Position a, Position b) = v.Slice(2);

        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 0), (0, 2)), new Range<int>(0, 2)), a);
        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 2), (0, 3)), new Range<int>(2, 3)), b);
    }

    [TestMethod]
    public void Test04()
    {
        Position v = new(new Range<SinglePosition>((0, 1), (0, 4)), new Range<int>(1, 4));
        (Position a, Position b) = v.Slice(2);

        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 1), (0, 3)), new Range<int>(1, 3)), a);
        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 3), (0, 4)), new Range<int>(3, 4)), b);
    }

    [TestMethod]
    public void Test05()
    {
        Position v = new(new Range<SinglePosition>((0, 1), (0, 4)), new Range<int>(1, 4));
        (Position a, Position b) = v.Slice(0);

        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 1), (0, 1)), new Range<int>(1, 1)), a);
        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 1), (0, 4)), new Range<int>(1, 4)), b);
    }

    [TestMethod]
    public void Test06()
    {
        Position v = new(new Range<SinglePosition>((0, 1), (0, 4)), new Range<int>(1, 4));
        (Position a, Position b) = v.Slice(3);

        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 1), (0, 4)), new Range<int>(1, 4)), a);
        Assert.AreEqual(new Position(new Range<SinglePosition>((0, 4), (0, 4)), new Range<int>(4, 4)), b);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test07()
    {
        Position v = new(new Range<SinglePosition>((0, 1), (0, 4)), new Range<int>(1, 4));
        _ = v.Slice(4);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void Test08()
    {
        Position v = new(new Range<SinglePosition>((0, 1), (0, 4)), new Range<int>(1, 4));
        _ = v.Slice(-1);
    }

    [TestMethod]
    public void Test09()
    {
        Position v = new(new Range<SinglePosition>((0, 0), (0, 0)), new Range<int>(0, 0));
        (Position a, Position b) = v.Slice(0);

        Assert.AreEqual(Position.Zero, a);
        Assert.AreEqual(Position.Zero, b);
    }
}
