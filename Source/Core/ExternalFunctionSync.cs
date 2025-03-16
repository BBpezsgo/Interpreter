using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public delegate void ExternalFunctionSyncCallback(ReadOnlySpan<byte> arguments, Span<byte> returnValue);

[SuppressMessage("Style", "IDE0008:Use explicit type")]
[SuppressMessage("Style", "IDE0053:Use expression body for lambda expression")]
public readonly struct ExternalFunctionSync : IExternalFunction
{
    public string? Name { get; }
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public ExternalFunctionSyncCallback MarshaledCallback { get; }
    public nint UnmanagedCallback { get; }

    public ExternalFunctionSync(ExternalFunctionSyncCallback callback, Delegate unmanagedCallback, int id, string? name, int parametersSize, int returnValueSize)
    {
        MarshaledCallback = callback;
        if (callback is not null && callback.Target is not null && callback.Method.IsStatic && callback.Method is not DynamicMethod)
        { UnmanagedCallback = Marshal.GetFunctionPointerForDelegate(unmanagedCallback); }
        Id = id;
        Name = name;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
    }

    #region Generators

    public static ExternalFunctionSync Create(int id, string? name, Action callback)
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            callback.Invoke();
        }, callback, id, name, 0, 0);
    }

    public static ExternalFunctionSync Create<T0>(int id, string? name, Action<T0> callback)
        where T0 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0>(args);
            callback.Invoke(
                _args);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0>(), 0);
    }

    public static ExternalFunctionSync Create<T0, T1>(int id, string? name, Action<T0, T1> callback)
        where T0 : unmanaged
        where T1 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1>(args);
            callback.Invoke(
                _args.P0,
                _args.P1);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1>(), 0);
    }

    public static ExternalFunctionSync Create<T0, T1, T2>(int id, string? name, Action<T0, T1, T2> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1, T2>(args);
            callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2>(), 0);
    }

    public static ExternalFunctionSync Create<T0, T1, T2, T3>(int id, string? name, Action<T0, T1, T2, T3> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1, T2, T3>(args);
            callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3>(), 0);
    }

    public static ExternalFunctionSync Create<T0, T1, T2, T3, T4>(int id, string? name, Action<T0, T1, T2, T3, T4> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1, T2, T3, T4>(args);
            callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3,
                _args.P4);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4>(), 0);
    }

    public static ExternalFunctionSync Create<T0, T1, T2, T3, T4, T5>(int id, string? name, Action<T0, T1, T2, T3, T4, T5> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1, T2, T3, T4, T5>(args);
            callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3,
                _args.P4,
                _args.P5);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4, T5>(), 0);
    }

    public static ExternalFunctionSync Create<TResult>(int id, string? name, Func<TResult> callback)
        where TResult : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            TResult result = callback.Invoke();
            result.AsBytes().CopyTo(returnValue);
        }, callback, id, name, 0, ExternalFunctionGenerator.SizeOf<TResult>());
    }

    public static ExternalFunctionSync Create<T0, TResult>(int id, string? name, Func<T0, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0>(args);
            TResult result = callback.Invoke(
                _args);

            result.AsBytes().CopyTo(returnValue);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    public static ExternalFunctionSync Create<T0, T1, TResult>(int id, string? name, Func<T0, T1, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1);

            result.AsBytes().CopyTo(returnValue);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    public static ExternalFunctionSync Create<T0, T1, T2, TResult>(int id, string? name, Func<T0, T1, T2, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1, T2>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2);

            result.AsBytes().CopyTo(returnValue);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    public static ExternalFunctionSync Create<T0, T1, T2, T3, TResult>(int id, string? name, Func<T0, T1, T2, T3, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1, T2, T3>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3);

            result.AsBytes().CopyTo(returnValue);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    public static ExternalFunctionSync Create<T0, T1, T2, T3, T4, TResult>(int id, string? name, Func<T0, T1, T2, T3, T4, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1, T2, T3, T4>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3,
                _args.P4);

            result.AsBytes().CopyTo(returnValue);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    public static ExternalFunctionSync Create<T0, T1, T2, T3, T4, T5, TResult>(int id, string? name, Func<T0, T1, T2, T3, T4, T5, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0, T1, T2, T3, T4, T5>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3,
                _args.P4,
                _args.P5);

            result.AsBytes().CopyTo(returnValue);
        }, callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4, T5>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    #endregion
}
