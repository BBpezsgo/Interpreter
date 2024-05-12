#pragma warning disable IDE0059 // Unnecessary assignment of a value
using System.Collections.Immutable;

namespace Tests;

using LanguageCore;
using LanguageCore.Tokenizing;

[TestClass, TestCategory("Tokenizer")]
public class TokenizerTests
{
    static readonly TokenizerSettings Settings = new()
    {
        DistinguishBetweenSpacesAndNewlines = false,
        JoinLinebreaks = true,
        TokenizeComments = true,
        TokenizeWhitespaces = false,
    };

    /// <exception cref="AssertFailedException"/>
    static void TokenSpecificAssert(ImmutableArray<Token> tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        { TokenSpecificAssert(tokens[i]); }
    }
    /// <exception cref="AssertFailedException"/>
    static void TokenSpecificAssert(Token token)
        => Assert.AreEqual(token.ToOriginalString().Length, token.Position.AbsoluteRange.Size());

    [TestMethod]
    public void Test1()
    {
        string text = "a";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 1)),
                (0, 1)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test2()
    {
        string text = "ab";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 2)),
                (0, 2)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test3()
    {
        string text = "";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test4()
    {
        string text = string.Empty;

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test5()
    {
        string text = " a";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 1), (0, 2)),
                (1, 2)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test6()
    {
        string text = "a\r\n";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 1)),
                (0, 1)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test7()
    {
        string text = " \r\n";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test8()
    {
        string text = " a\r\n";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 1), (0, 2)),
                (1, 2)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test9()
    {
        string text = "\r\na";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((1, 0), (1, 1)),
                (2, 3)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test10()
    {
        string text = "\r\n a";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((1, 1), (1, 2)),
                (3, 4)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test11()
    {
        string text = "\r";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test12()
    {
        string text = "\n";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test13()
    {
        string text = " ";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod, Ignore("CR not supported")]
    public void Test14()
    {
        string text = "\ra";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((1, 0), (1, 1)),
                (1, 2)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod, Ignore("CR not supported")]
    public void Test15()
    {
        string text = "\r a";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((1, 1), (1, 2)),
                (2, 3)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test16()
    {
        string text = "\na";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((1, 0), (1, 1)),
                (1, 2)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test17()
    {
        string text = "\n a";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((1, 1), (1, 2)),
                (2, 3)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test18()
    {
        string text = @"""""";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 2)),
                (0, 2)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test19()
    {
        string text = @"""a""";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 3)),
                (0, 3)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test20()
    {
        string text = "'a'";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 3)),
                (0, 3)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test21()
    {
        string text = "563";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 3)),
                (0, 3)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test22()
    {
        string text = ".563";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 4)),
                (0, 4)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test23()
    {
        string text = ".563f";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 5)),
                (0, 5)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test24()
    {
        string text = @"""\n""";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 4)),
                (0, 4)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        AssertUtils.ContentEquals(tokens, "\n");
        // TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test25()
    {
        string text = @"""\u4685""";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 8)),
                (0, 8)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        AssertUtils.ContentEquals(tokens, "\u4685");
        // TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test26()
    {
        string text = @"'\n'";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 4)),
                (0, 4)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        AssertUtils.ContentEquals(tokens, "\n");
        // TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test27()
    {
        string text = @"'\u4685'";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 8)),
                (0, 8)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        AssertUtils.ContentEquals(tokens, "\u4685");
        // TokenSpecificAssert(tokens);
    }

    [TestMethod]
    [ExpectedException(typeof(TokenizerException))]
    public void Test28()
    {
        string text = @"'\7'";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 4)),
                (0, 4)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    [ExpectedException(typeof(TokenizerException))]
    public void Test29()
    {
        string text = "''";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 2)),
                (0, 2)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test30()
    {
        string text = "// hello";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 8)),
                (0, 8)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        AssertUtils.ContentEquals(tokens, " hello");
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test31()
    {
        string text = "// hello\n";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 8)),
                (0, 8)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        AssertUtils.ContentEquals(tokens, " hello");
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test32()
    {
        string text = "/* hello */";

        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(text, Enumerable.Empty<string>(), settings: Settings).Tokens;
        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 11)),
                (0, 11)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        AssertUtils.ContentEquals(tokens, " hello ");
        // TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test33()
    {
        ImmutableArray<Token> tokens = StringTokenizer.Tokenize(";;", Enumerable.Empty<string>(), settings: Settings).Tokens;

        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 1)),
                (0, 1)
            ),
            new(
                ((0, 1), (0, 2)),
                (1, 2)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        AssertUtils.ContentEquals(tokens, ";", ";");
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    [ExpectedException(typeof(TokenizerException))]
    public void Test34()
    {
        ImmutableArray<Token> tokens = StringTokenizer.Tokenize("0x64_4g", Enumerable.Empty<string>(), settings: Settings).Tokens;

        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 7)),
                (0, 7)
            ),
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }
}

[TestClass, TestCategory("Token Utils")]
public class TokenTests
{
    [TestMethod]
    public void Test1()
    {
        Token token = StringTokenizer.Tokenize("ab", Enumerable.Empty<string>()).Tokens[0];

        (Token? a, Token? b) = token.Slice(1);

        Assert.IsNotNull(a);
        Assert.IsNotNull(b);

        Assert.AreEqual("a", a.Content);
        Assert.AreEqual("b", b.Content);

        Assert.AreEqual(new Range<SinglePosition>((0, 0), (0, 1)), a.Position.Range);
        Assert.AreEqual(new Range<SinglePosition>((0, 1), (0, 2)), b.Position.Range);

        Assert.AreEqual(new Range<int>(0, 1), a.Position.AbsoluteRange);
        Assert.AreEqual(new Range<int>(1, 2), b.Position.AbsoluteRange);
    }

    [TestMethod]
    public void Test2()
    {
        Token token = StringTokenizer.Tokenize("a", Enumerable.Empty<string>()).Tokens[0];

        (Token? a, Token? b) = token.Slice(1);

        Assert.IsNotNull(a);
        Assert.IsNull(b);

        Assert.AreEqual("a", a.Content);

        Assert.AreEqual(new Range<SinglePosition>((0, 0), (0, 1)), a.Position.Range);

        Assert.AreEqual(new Range<int>(0, 1), a.Position.AbsoluteRange);
    }

    [TestMethod]
    public void Test3()
    {
        Token token = Token.Empty;

        (Token? a, Token? b) = token.Slice(0);

        Assert.IsNull(a);
        Assert.IsNull(b);
    }

    [TestMethod]
    public void Test4()
    {
        Token token = StringTokenizer.Tokenize("abcd", Enumerable.Empty<string>()).Tokens[0];

        (Token? a, Token? b) = token.Slice(2);

        Assert.IsNotNull(a);
        Assert.IsNotNull(b);

        Assert.AreEqual("ab", a.Content);
        Assert.AreEqual("cd", b.Content);

        Assert.AreEqual(new Range<SinglePosition>((0, 0), (0, 2)), a.Position.Range);
        Assert.AreEqual(new Range<SinglePosition>((0, 2), (0, 4)), b.Position.Range);

        Assert.AreEqual(new Range<int>(0, 2), a.Position.AbsoluteRange);
        Assert.AreEqual(new Range<int>(2, 4), b.Position.AbsoluteRange);
    }

    [TestMethod]
    public void Test5()
    {
        Token token = StringTokenizer.Tokenize("abc", Enumerable.Empty<string>()).Tokens[0];

        (Token? a, Token? b) = token.Slice(1);

        Assert.IsNotNull(a);
        Assert.IsNotNull(b);

        Assert.AreEqual("a", a.Content);
        Assert.AreEqual("bc", b.Content);

        Assert.AreEqual(new Range<SinglePosition>((0, 0), (0, 1)), a.Position.Range);
        Assert.AreEqual(new Range<SinglePosition>((0, 1), (0, 3)), b.Position.Range);

        Assert.AreEqual(new Range<int>(0, 1), a.Position.AbsoluteRange);
        Assert.AreEqual(new Range<int>(1, 3), b.Position.AbsoluteRange);
    }

    [TestMethod]
    public void Test6()
    {
        Token token = StringTokenizer.Tokenize("0x545fadf3", Enumerable.Empty<string>()).Tokens[0];
    }

    [TestMethod]
    public void Test7()
    {
        Token token = StringTokenizer.Tokenize("abc", Enumerable.Empty<string>()).Tokens[0];

        (Token? a, Token? b) = token.Slice(3);

        Assert.IsNotNull(a);
        Assert.IsNotNull(b);

        Assert.AreEqual("abc", a.Content);
        Assert.AreEqual("", b.Content);

        Assert.AreEqual(new Range<SinglePosition>((0, 0), (0, 3)), a.Position.Range);
        Assert.AreEqual(new Range<SinglePosition>((0, 3), (0, 3)), b.Position.Range);

        Assert.AreEqual(new Range<int>(0, 3), a.Position.AbsoluteRange);
        Assert.AreEqual(new Range<int>(3, 3), b.Position.AbsoluteRange);
    }
}
