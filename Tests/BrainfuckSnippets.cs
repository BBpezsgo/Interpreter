﻿namespace Tests;

using LanguageCore.Brainfuck;

[TestClass, TestCategory("Brainfuck Snippets")]
public class BrainfuckSnippets
{
    static byte[] MakeMemory(int length, params int[] values)
    {
        byte[] result = new byte[length];

        for (int i = 0; i < values.Length; i++)
        {
            result[i] = (byte)values[i];
        }

        return result;
    }
    static void InitializeValues(CompiledCode code, params int[] values)
    {
        for (int offset = 0; offset < values.Length; offset++)
        {
            code.SetValue(offset, values[offset]);
        }
    }

    #region LOGIC

    [TestMethod]
    [DataRow(3, 6, 0)]
    [DataRow(9, 6, 1)]
    [DataRow(6, 6, 0)]
    [DataRow(0, 255, 1)]
    [DataRow(255, 0, 0)]
    [DataRow(1, 255, 1)]
    [DataRow(255, 1, 0)]
    public void LOGIC_LT(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.LOGIC_LT(0, 1, 2, 3);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, y);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(0, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(3, 6, 1)]
    [DataRow(9, 6, 0)]
    [DataRow(6, 6, 0)]
    [DataRow(0, 255, 0)]
    [DataRow(255, 0, 1)]
    [DataRow(1, 255, 0)]
    [DataRow(255, 1, 1)]
    public void LOGIC_MT(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.LOGIC_MT(0, 1, 2, 3, 4);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, 0, 0, expected);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(2, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(3, 6, 1)]
    [DataRow(9, 6, 0)]
    [DataRow(6, 6, 1)]
    [DataRow(0, 255, 1)]
    [DataRow(255, 0, 0)]
    [DataRow(1, 255, 1)]
    [DataRow(255, 1, 0)]
    [DataRow(255, 255, 1)]
    public void LOGIC_LTEQ(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.LOGIC_LTEQ(0, 1, 2, 3);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, y);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(0, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(3, 6, 0)]
    [DataRow(9, 6, 0)]
    [DataRow(6, 6, 1)]
    [DataRow(255, 255, 1)]
    [DataRow(255, 254, 0)]
    [DataRow(255, 0, 0)]
    [DataRow(0, 255, 0)]
    public void LOGIC_EQ(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.LOGIC_EQ(0, 1, 2, 3);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, y);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(3, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(3, 6, 1)]
    [DataRow(9, 6, 1)]
    [DataRow(6, 6, 0)]
    [DataRow(255, 255, 0)]
    [DataRow(255, 254, 1)]
    [DataRow(255, 0, 1)]
    [DataRow(0, 255, 1)]
    public void LOGIC_NEQ(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.LOGIC_NEQ(0, 1, 2, 3);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, y);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(3, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 0)]
    public void LOGIC_NOT(int x, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x);
        code.LOGIC_NOT(0, 1);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(0, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(0, 0, 0)]
    [DataRow(0, 1, 1)]
    [DataRow(1, 0, 1)]
    [DataRow(1, 1, 1)]
    public void LOGIC_OR(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.LOGIC_OR(0, 1, 2);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, 0);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(0, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(0, 0, 0)]
    [DataRow(0, 1, 0)]
    [DataRow(1, 0, 0)]
    [DataRow(1, 1, 1)]
    public void LOGIC_AND(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.LOGIC_AND(0, 1, 2, 3);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, y);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(3, interpreter.MemoryPointer);
    }

    #endregion

    #region BITS

    [TestMethod]
    [DataRow(0, ~0)]
    [DataRow(1, ~1)]
    [DataRow(16, ~16)]
    [DataRow(255, ~255)]
    public void BITS_NOT(int x, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x);
        code.BITS_NOT(0, 1);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(1, interpreter.MemoryPointer);
    }

    #endregion

    #region MATH

    [TestMethod]
    [DataRow(6, 3, 2)]
    [DataRow(6, 6, 1)]
    [DataRow(255, 16, 16)]
    public void MATH_DIV(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.MATH_DIV(0, 1, 2, 3, 4, 5);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, y);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(2, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(6, 3, 0)]
    [DataRow(6, 6, 0)]
    [DataRow(255, 16, 0)]
    [DataRow(7, 3, 1)]
    [DataRow(3, 2, 1)]
    public void MATH_MOD(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.MATH_MOD(0, 1, 2);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, y);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(0, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(6, 36)]
    [DataRow(0, 0)]
    [DataRow(1, 1)]
    [DataRow(2, 2)]
    [DataRow(16, 16)]
    public void MATH_MUL_SELF(int x, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x);
        code.MATH_MUL_SELF(0, 1, 2);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(1, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(6, 2, 36)]
    [DataRow(0, 0, 0)]
    [DataRow(1, 0, 1)]
    [DataRow(0, 1, 0)]
    [DataRow(2, 4, 16)]
    [DataRow(2, 7, 128)]
    public void MATH_POW(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.MATH_POW(0, 1, 2, 3, 4);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(2, interpreter.MemoryPointer);
    }

    [TestMethod]
    [DataRow(6, 6, 36)]
    [DataRow(0, 0, 0)]
    [DataRow(1, 0, 0)]
    [DataRow(0, 1, 0)]
    [DataRow(2, 4, 8)]
    [DataRow(16, 16, 255)]
    public void MULTIPLY(int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.MULTIPLY(0, 1, 2, 3);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, expected, y);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(3, interpreter.MemoryPointer);
    }

    #endregion

    [TestMethod]
    [DataRow(6, 36)]
    [DataRow(0, 0)]
    [DataRow(1, 0)]
    [DataRow(0, 1)]
    [DataRow(2, 4)]
    [DataRow(16, 15)]
    [DataRow(0, 255)]
    [DataRow(255, 0)]
    [DataRow(255, 1)]
    [DataRow(1, 255)]
    public void SWAP(int x, int y)
    {
        CompiledCode code = new();
        InitializeValues(code, x, y);
        code.SWAP(0, 1, 2);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, y, x);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(2, interpreter.MemoryPointer);
    }

    [TestMethod]

    [DataRow(0, 0, 0, 0)]
    [DataRow(0, 0, 1, 0)]
    [DataRow(0, 1, 0, 1)]
    [DataRow(0, 1, 1, 1)]
    [DataRow(1, 0, 0, 0)]
    [DataRow(1, 0, 1, 1)]
    [DataRow(1, 1, 0, 0)]
    [DataRow(1, 1, 1, 1)]

    [DataRow(0, 16, 255, 16)]
    [DataRow(1, 16, 255, 255)]
    [DataRow(0, 255, 16, 255)]
    [DataRow(1, 255, 16, 16)]
    public void MUX(int a, int x, int y, int expected)
    {
        CompiledCode code = new();
        InitializeValues(code, a, x, y);
        code.MUX(0, 1, 2, 3);

        Interpreter interpreter = new(code.Code.ToString(), null, null);
        interpreter.Run();

        byte[] expectedMemory = MakeMemory(interpreter.Memory.Length, 0, 0, 0, expected);
        AssertUtils.AreEqual(expectedMemory, interpreter.Memory.ToArray());
        Assert.AreEqual(0, interpreter.MemoryPointer);
    }
}