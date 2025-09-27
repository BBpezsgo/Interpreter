namespace LanguageCore.Runtime;

public delegate void KeyConsumer(char key);

public sealed class VirtualIO : IO
{
    public delegate void OnStdOutEventHandler(char data);
    public delegate void OnInputEventHandler();

    public event OnStdOutEventHandler? OnStdOut;
    /// <summary>
    /// Will be invoked when the code needs input<br/>
    /// Call <see cref="SendKey(char)"/> after this invoked
    /// </summary>
    public event OnInputEventHandler? OnNeedInput;

    public bool IsAwaitingInput => _keyConsumer is not null;

    KeyConsumer? _keyConsumer;

    /// <summary>
    /// Provides input to the interpreter<br/>
    /// <lv>WARNING:</lv> Call it only after <see cref="OnNeedInput"/> invoked!
    /// </summary>
    /// <param name="key">
    /// The input value
    /// </param>
    public void SendKey(char key)
    {
        if (_keyConsumer == null) return;
        _keyConsumer.Invoke(key);
        _keyConsumer = null;
    }

    public override void Register(List<IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction(new ExternalFunctionAsync((ref ProcessorState processor, ReadOnlySpan<byte> parameters) =>
        {
            if (OnNeedInput == null) throw new RuntimeException($"Event \"{OnNeedInput}\" does not have listeners");
            ProcessorState _processor = processor;
            char? consumedKey = null;
            _keyConsumer = key => consumedKey = key;
            OnNeedInput.Invoke();
            return (ref ProcessorState processor, Span<byte> returnValue) =>
            {
                if (consumedKey.HasValue)
                {
                    returnValue.Set(consumedKey.Value);
                    return true;
                }
                return false;
            };
        }, externalFunctions.GenerateId(ExternalFunctionNames.StdIn), ExternalFunctionNames.StdIn, 0, sizeof(char)));

        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdOut), ExternalFunctionNames.StdOut, (char @char) =>
        {
            OnStdOut?.Invoke(@char);
        }));
    }
}
