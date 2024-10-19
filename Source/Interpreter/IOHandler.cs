namespace LanguageCore.Runtime;

public delegate void KeyConsumer(char key);

public class IOHandler
{
    public delegate void OnStdErrorEventHandler(char data);
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

    public static IOHandler Create(List<IExternalFunction> externalFunctions)
    {
        IOHandler ioHandler = new();

        externalFunctions.AddExternalFunction(new ExternalFunctionAsync((ref ProcessorState processor, ReadOnlySpan<byte> parameters) =>
        {
            if (ioHandler.OnNeedInput == null) throw new RuntimeException($"Event {ioHandler.OnNeedInput} does not have listeners");
            ProcessorState _processor = processor;
            char? consumedKey = null;
            ioHandler._keyConsumer = (char key) => consumedKey = key;
            ioHandler.OnNeedInput.Invoke();
            return (ref ProcessorState processor, out ReadOnlySpan<byte> returnValue) =>
            {
                if (consumedKey.HasValue)
                {
                    returnValue = consumedKey.Value.ToBytes();
                    return true;
                }
                returnValue = ReadOnlySpan<byte>.Empty;
                return false;
            };
        }, externalFunctions.GenerateId(ExternalFunctionNames.StdIn), ExternalFunctionNames.StdIn, 0, sizeof(char)));

        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdOut), ExternalFunctionNames.StdOut, (char @char) => ioHandler.OnStdOut?.Invoke(@char)));

        return ioHandler;
    }
}
