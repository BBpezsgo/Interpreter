﻿namespace LanguageCore.Brainfuck;

public class HeapCodeHelper
{
    public const int BlockSize = 3;
    public const int AddressCarryOffset = 0;
    public const int ValueCarryOffset = 1;
    public const int DataOffset = 2;

    public int Start { get; }
    public int Size { get; }
    public int OffsettedStart => GetOffsettedStart(Start);
    public bool IsUsed { get; private set; }
    public bool AddSmallComments { get; set; }

    CodeHelper _code;

    public HeapCodeHelper(CodeHelper code, int start, int size)
    {
        _code = code;
        Start = start;
        Size = size;
        AddSmallComments = false;
    }

    /*
     *  LAYOUT:
     *  START 0 0 (a c v) (a c v) ...
     *  a: Carrying address
     *  c: Carrying value
     *  v: Value
     */

    public static int GetOffsettedStart(int start) => start + BlockSize;

    void ThrowIfNotInitialized()
    {
        if (Size <= 0)
        { throw new InternalExceptionWithoutContext($"Heap size is {Size}"); }
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OffsettedStart</c>
    /// </summary>
    void GoBack()
    {
        // Go back
        _code += '[';
        _code.ClearCurrent();
        _code.MovePointerUnsafe(-BlockSize);
        _code += ']';

        // Fix overshoot
        _code.MovePointerUnsafe(BlockSize);
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OffsettedStart</c>
    /// </summary>
    void CarryBack()
    {
        // Go back
        _code += '[';
        _code.ClearCurrent();
        _code.MoveValueUnsafe(ValueCarryOffset, ValueCarryOffset - BlockSize);
        _code.MovePointerUnsafe(-BlockSize);
        _code += ']';

        // Fix overshoot
        _code.MovePointerUnsafe(BlockSize);
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// </summary>
    void GoTo()
    {
        // Condition on carrying address
        _code += '[';

        // Copy the address and leave 1 behind
        _code.MoveValueUnsafe(AddressCarryOffset, false, BlockSize + AddressCarryOffset);
        _code.AddValue(1);

        // Move to the next block
        _code.MovePointerUnsafe(BlockSize);

        // Decrement 1 and check if zero
        //   Yes => Destination reached -> leave 1
        //   No => Repeat
        _code += "- ] +";
    }

    /// <summary>
    /// <b>Expected pointer:</b> <c>OffsettedStart</c> or <c>OFFSET_ADDRESS_CARRY</c>
    /// <br/>
    /// <b>Pointer:</b> <c>OFFSET_ADDRESS_CARRY</c>
    /// </summary>
    void CarryTo()
    {
        // Condition on carrying address
        _code += '[';

        // Copy the address and leave 1 behind
        _code.MoveValueUnsafe(AddressCarryOffset, false, BlockSize + AddressCarryOffset);
        _code.AddValue(1);

        // Copy the value
        _code.MoveValueUnsafe(ValueCarryOffset, false, BlockSize + ValueCarryOffset);

        // Move to the next block
        _code.MovePointerUnsafe(BlockSize);

        // Decrement 1 and check if zero
        //   Yes => Destination reached -> leave 1
        //   No => Repeat
        _code += "- ] +";
    }

    public void Init(out PossibleDiagnostic? error)
    {
        error = null;

        if (Size == 0) return;

        if (Size < 0)
        {
            error = new PossibleDiagnostic($"HEAP size must be larger than 0");
            return;
        }

        if (Size > 126)
        {
            error = new PossibleDiagnostic($"HEAP size must be smaller than 127");
            return;
        }

        _code.StartBlock("Initialize HEAP");

        _code.SetValue(OffsettedStart + DataOffset, Size);
        _code.SetPointer(0);

        _code.EndBlock();
    }

    public string? LateInit(out PossibleDiagnostic? diagnostic)
    {
        diagnostic = null;

        if (Size == 0) return null;

        if (Size < 0)
        {
            diagnostic = new PossibleDiagnostic($"HEAP size must be larger than 0");
            return null;
        }

        if (Size > 126)
        {
            diagnostic = new PossibleDiagnostic($"HEAP size must be smaller than 127");
            return null;
        }

        CodeHelper code = new()
        {
            AddSmallComments = AddSmallComments,
            AddComments = AddSmallComments,
        };

        code.StartBlock("Initialize HEAP");

        code.SetValue(OffsettedStart + DataOffset, Size);
        code.SetPointer(0);

        code.EndBlock();

        return code.ToString();
    }

    public void Destroy()
    {
        _code.StartBlock("Destroy HEAP");

        int start = OffsettedStart;
        int end = start + (BlockSize * (Size + 4));

        for (int i = start; i < end; i += BlockSize)
        { _code.ClearValue(i + DataOffset); }

        _code.SetPointer(0);

        _code.EndBlock();
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </para>
    /// <para>
    /// Does not preserve <paramref name="pointerAddress"/> nor <paramref name="valueAddress"/>
    /// </para>
    /// </summary>
    public void Set(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        _code.MoveValue(pointerAddress, OffsettedStart);
        _code.MoveValue(valueAddress, OffsettedStart + 1);

        _code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        _code.MoveValueUnsafe(ValueCarryOffset, true, DataOffset);

        GoBack();

        IsUsed = true;
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void SetAbsolute(int pointer, int value)
    {
        ThrowIfNotInitialized();

        _code.SetValue(OffsettedStart, pointer);
        _code.SetValue(OffsettedStart + 1, value);

        _code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        _code.MoveValueUnsafe(ValueCarryOffset, true, DataOffset);

        GoBack();

        IsUsed = true;
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void Add(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        _code.MoveValue(pointerAddress, OffsettedStart);
        _code.MoveValue(valueAddress, OffsettedStart + 1);

        _code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        _code.MoveAddValueUnsafe(ValueCarryOffset, DataOffset);

        GoBack();

        IsUsed = true;
    }

    /// <summary>
    /// <b>Pointer:</b> <see cref="OffsettedStart"/>
    /// </summary>
    public void Subtract(int pointerAddress, int valueAddress)
    {
        ThrowIfNotInitialized();

        _code.MoveValue(pointerAddress, OffsettedStart);
        _code.MoveValue(valueAddress, OffsettedStart + 1);

        _code.SetPointer(OffsettedStart);

        CarryTo();

        // Copy the carried value to the address
        _code.MoveSubValueUnsafe(ValueCarryOffset, DataOffset);

        GoBack();

        IsUsed = true;
    }

    /// <summary>
    /// <para>
    /// <b>Pointer:</b> <paramref name="resultAddress"/>
    /// </para>
    /// <para>
    /// <b>Note:</b> This will discard <paramref name="pointerAddress"/>
    /// </para>
    /// </summary>
    public void Get(int pointerAddress, int resultAddress, int size)
    {
        for (int offset = 0; offset < size; offset++)
        {
            Get(pointerAddress, resultAddress + offset);
            _code.AddValue(pointerAddress, 1);
        }

        IsUsed = true;
    }

    /// <summary>
    /// <b>Pointer:</b> <paramref name="resultAddress"/>
    /// </summary>
    public void Get(int pointerAddress, int resultAddress)
    {
        ThrowIfNotInitialized();

        _code.ClearValue(OffsettedStart, OffsettedStart + 1);

        _code.MoveValue(pointerAddress, OffsettedStart);

        _code.SetPointer(OffsettedStart);

        GoTo();

        _code.SetValueUnsafe(AddressCarryOffset, 0);
        _code.CopyValueUnsafe(DataOffset, ValueCarryOffset, AddressCarryOffset);
        _code.SetValueUnsafe(AddressCarryOffset, 1);

        CarryBack();

        _code.MoveValue(Start + ValueCarryOffset, resultAddress);
        _code.SetPointer(resultAddress);

        IsUsed = true;
    }
}
