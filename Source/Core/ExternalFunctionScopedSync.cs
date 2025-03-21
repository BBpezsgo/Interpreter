namespace LanguageCore.Runtime;
#if UNITY_BURST
[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)] 
public delegate void ExternalFunctionUnity(nint scope, nint arguments, nint returnValue);
#endif

public unsafe struct ExternalFunctionScopedSync
{
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public nint Scope { get; set; }
#if UNITY_BURST
    readonly nint _callback;
    public readonly delegate* unmanaged[Cdecl]<nint, nint, nint, void> Callback => (delegate* unmanaged[Cdecl]<nint, nint, nint, void>)_callback;
#else
    public delegate*<nint, System.ReadOnlySpan<byte>, System.Span<byte>, void> Callback { get; }
#endif

    public ExternalFunctionScopedSync(
#if UNITY_BURST
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> callback,
#else
        delegate*<nint, System.ReadOnlySpan<byte>, System.Span<byte>, void> callback,
#endif
        int id,
        int parametersSize,
        int returnValueSize,
        nint scope)
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
