namespace LanguageCore.Runtime;

#if UNITY_BURST
[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
public delegate void ExternalFunctionUnity(nint scope, nint arguments, nint returnValue);
#endif

public delegate void ExternalFunctionScopedSyncCallback(nint scope, nint arguments, nint returnValue);

[Flags]
public enum ExternalFunctionScopedSyncFlags : byte
{
    None = 0,
    MSILPointerMarshal = 1 << 1,
    MSILSafe = 1 << 2,
}

public unsafe struct ExternalFunctionScopedSync : IExternalFunction
{
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public nint Scope { get; set; }
#if UNITY_BURST
    nint _callback;
    public delegate* unmanaged[Cdecl]<nint, nint, nint, void> Callback
    {
        readonly get => (delegate* unmanaged[Cdecl]<nint, nint, nint, void>)_callback;
        set => _callback = (nint)value;
    }
#else
    public ExternalFunctionScopedSyncCallback Callback { get; }
#endif
    public ExternalFunctionScopedSyncFlags Flags { get; set; }

    readonly string? IExternalFunction.Name => null;

    public ExternalFunctionScopedSync(
#if UNITY_BURST
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> callback,
#else
        ExternalFunctionScopedSyncCallback callback,
#endif
        int id,
        int parametersSize,
        int returnValueSize,
        nint scope,
        ExternalFunctionScopedSyncFlags flags = ExternalFunctionScopedSyncFlags.None)
    {
#if UNITY_BURST
        _callback = (nint)callback;
#else
        Callback = callback;
#endif
        Id = id;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
        Scope = scope;
        Flags = flags;
    }

    // This required by my Unity-DOTS project!!!!
    public readonly IExternalFunction ToManaged(string name)
    {
        return new ExternalFunctionSync((_, _) =>
        {
            throw new InvalidOperationException();
        }, () =>
        {
            throw new InvalidOperationException();
        }, Id, name, ParametersSize, ReturnValueSize);
    }

    public override readonly string ToString() => $"<{ReturnValueSize}b> {Id}(<{ParametersSize}b>)";
}
