namespace LanguageCore.Runtime;

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

    ManagedExternalFunctionAsyncBlockReturnCallback? _keyConsumer;

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
        _keyConsumer.Invoke(key.AsBytes());
        _keyConsumer = null;
    }

    public static IOHandler Create(Dictionary<int, IExternalFunction> externalFunctions)
    {
        IOHandler ioHandler = new();

        externalFunctions.AddManagedExternalFunction(ExternalFunctionNames.StdIn, 0, (ReadOnlySpan<byte> parameters, ManagedExternalFunctionAsyncBlockReturnCallback callback) =>
        {
            if (ioHandler.OnNeedInput == null) throw new RuntimeException($"Event {ioHandler.OnNeedInput} does not have listeners");
            ioHandler._keyConsumer = callback;
            ioHandler.OnNeedInput.Invoke();
        }, sizeof(char));

        externalFunctions.AddExternalFunction(ExternalFunctionNames.StdOut, (char @char) => ioHandler.OnStdOut?.Invoke(@char));

        return ioHandler;
    }
}
