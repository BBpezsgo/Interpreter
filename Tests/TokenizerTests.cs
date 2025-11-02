using System.Collections.Immutable;
using LanguageCore;
using LanguageCore.Tokenizing;

namespace Tests;

[TestClass, TestCategory("Internals")]
public class TokenizerTests
{
    static readonly TokenizerSettings Settings = new()
    {
        DistinguishBetweenSpacesAndNewlines = false,
        JoinLinebreaks = true,
        TokenizeComments = true,
        TokenizeWhitespaces = false,
    };

    static void TokenSpecificAssert(ImmutableArray<Token> tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        { TokenSpecificAssert(tokens[i]); }
    }

    static void TokenSpecificAssert(Token token)
        => Assert.AreEqual(token.ToOriginalString().Length, token.Position.AbsoluteRange.Size());

    [TestMethod]
    public void Test01()
    {
        string text = "a";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    public void Test02()
    {
        string text = "ab";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    public void Test03()
    {
        string text = "";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test04()
    {
        string text = string.Empty;
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test05()
    {
        string text = " a";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    public void Test06()
    {
        string text = "a\r\n";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    public void Test07()
    {
        string text = " \r\n";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test08()
    {
        string text = " a\r\n";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    public void Test09()
    {
        string text = "\r\na";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test12()
    {
        string text = "\n";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod]
    public void Test13()
    {
        string text = " ";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

        Position[] positions = Array.Empty<Position>();

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }

    [TestMethod, Ignore("CR not supported")]
    public void Test14()
    {
        string text = "\ra";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void Test28()
    {
        string text = @"'\7'";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void Test29()
    {
        string text = "''";
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(text, diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize(";;", diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void Test34()
    {
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize("0x64_4g", diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void Test35()
    {
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize("\"\\ubruh\"", diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void Test36()
    {
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize("\'\\ubruh\'", diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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

    [TestMethod]
    public void Test37()
    {
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize("/=", diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    public void Test38()
    {
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize("/**/", diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
    public void Test39()
    {
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize("/-", diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

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
        TokenSpecificAssert(tokens);
    }

    [TestMethod, Ignore("I'm tired")]
    public void Test40()
    {
        DiagnosticsCollection diagnostics = new();

        ImmutableArray<Token> tokens = Tokenizer.Tokenize("/***/", diagnostics, null, settings: Settings).Tokens;
        diagnostics.Throw();

        Position[] positions = new Position[]
        {
            new(
                ((0, 0), (0, 5)),
                (0, 5)
            )
        };

        AssertUtils.PositionEquals(tokens, positions);
        TokenSpecificAssert(tokens);
    }
}

[TestClass, TestCategory("Internals")]
public class TokenTests
{
    [TestMethod]
    public void Test01()
    {
        DiagnosticsCollection diagnostics = new();

        Token token = Tokenizer.Tokenize("ab", diagnostics, null).Tokens[0];
        diagnostics.Throw();

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
    public void Test02()
    {
        DiagnosticsCollection diagnostics = new();

        Token token = Tokenizer.Tokenize("a", diagnostics, null).Tokens[0];
        diagnostics.Throw();

        (Token? a, Token? b) = token.Slice(1);

        Assert.IsNotNull(a);
        Assert.IsNull(b);

        Assert.AreEqual("a", a.Content);

        Assert.AreEqual(new Range<SinglePosition>((0, 0), (0, 1)), a.Position.Range);

        Assert.AreEqual(new Range<int>(0, 1), a.Position.AbsoluteRange);
    }

    [TestMethod]
    public void Test03()
    {
        Token token = Token.Empty;

        (Token? a, Token? b) = token.Slice(0);

        Assert.IsNull(a);
        Assert.IsNull(b);
    }

    [TestMethod]
    public void Test04()
    {
        DiagnosticsCollection diagnostics = new();

        Token token = Tokenizer.Tokenize("abcd", diagnostics, null).Tokens[0];
        diagnostics.Throw();

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
    public void Test05()
    {
        DiagnosticsCollection diagnostics = new();

        Token token = Tokenizer.Tokenize("abc", diagnostics, null).Tokens[0];
        diagnostics.Throw();

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
    public void Test06()
    {
        DiagnosticsCollection diagnostics = new();

        _ = Tokenizer.Tokenize("0x545fadf3", diagnostics, null).Tokens[0];
        diagnostics.Throw();
    }

    [TestMethod]
    public void Test07()
    {
        DiagnosticsCollection diagnostics = new();

        Token token = Tokenizer.Tokenize("abc", diagnostics, null).Tokens[0];
        diagnostics.Throw();

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
