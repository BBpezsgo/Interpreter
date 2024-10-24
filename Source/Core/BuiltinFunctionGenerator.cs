using System.Runtime.CompilerServices;
using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

public delegate void ExternalFunctionSyncCallback(ReadOnlySpan<byte> arguments, Span<byte> returnValue);
public delegate void ExternalFunctionSyncScopedCallback(Span<byte> scope, ReadOnlySpan<byte> arguments, Span<byte> returnValue);
public delegate ExternalFunctionAsyncReturnChecker ExternalFunctionAsyncCallback(ref ProcessorState processor, ReadOnlySpan<byte> arguments);
public delegate bool ExternalFunctionAsyncReturnChecker(ref ProcessorState processor, out ReadOnlySpan<byte> returnValue);

#if UNITY
[Unity.Burst.BurstCompile]
#endif
public static class ExternalFunctionExtensions
{
    public static string ToReadable(this IExternalFunction externalFunction) => $"<{externalFunction.ReturnValueSize}bytes> {externalFunction.Name ?? externalFunction.Id.ToString()}(<{externalFunction.ParametersSize}bytes>)";
}

public interface IExternalFunction
{
    public string? Name { get; }
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
}

public unsafe struct ExternalFunctionScopedSync : IExternalFunction
{
    public readonly string? Name => null;
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public nint Scope { get; set; }
    public delegate*<nint, ReadOnlySpan<byte>, Span<byte>, void> Callback { get; }

    public ExternalFunctionScopedSync(delegate*<nint, ReadOnlySpan<byte>, Span<byte>, void> callback, int id, int parametersSize, int returnValueSize, nint scope)
    {
        Callback = callback;
        Id = id;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
        Scope = scope;
    }
}

#if UNITY
// [Unity.Burst.BurstCompile]
#endif
[SuppressMessage("Style", "IDE0008:Use explicit type")]
[SuppressMessage("Style", "IDE0053:Use expression body for lambda expression")]
public readonly struct ExternalFunctionSync : IExternalFunction
{
    public string? Name { get; }
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public ExternalFunctionSyncCallback Callback { get; }

    public ExternalFunctionSync(ExternalFunctionSyncCallback callback, int id, string? name, int parametersSize, int returnValueSize)
    {
        Callback = callback;
        Id = id;
        Name = name;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
    }

    #region Generators

    /// <exception cref="NotImplementedException"/>
    public static ExternalFunctionSync Create(int id, string? name, Action callback)
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            callback.Invoke();
        }, id, name, 0, 0);
    }

    /// <exception cref="NotImplementedException"/>
    public static ExternalFunctionSync Create<T0>(int id, string? name, Action<T0> callback)
        where T0 : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            var _args = ExternalFunctionGenerator.TakeParameters<T0>(args);
            callback.Invoke(
                _args);
        }, id, name, ExternalFunctionGenerator.SizeOf<T0>(), 0);
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1>(), 0);
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2>(), 0);
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3>(), 0);
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4>(), 0);
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4, T5>(), 0);
    }

    /// <exception cref="NotImplementedException"/>
    public static ExternalFunctionSync Create<TResult>(int id, string? name, Func<TResult> callback)
        where TResult : unmanaged
    {
        return new ExternalFunctionSync((ReadOnlySpan<byte> args, Span<byte> returnValue) =>
        {
            TResult result = callback.Invoke();
            result.AsBytes().CopyTo(returnValue);
        }, id, name, 0, ExternalFunctionGenerator.SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
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
        }, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4, T5>(), ExternalFunctionGenerator.SizeOf<TResult>());
    }

    #endregion
}

#if UNITY
// [Unity.Burst.BurstCompile]
#endif
public readonly struct ExternalFunctionAsync : IExternalFunction
{
    public string? Name { get; }
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public ExternalFunctionAsyncCallback Callback { get; }

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionAsync(ExternalFunctionAsyncCallback callback, int id, string? name, int parametersSize, int returnValueSize)
    {
        Callback = callback;
        Id = id;
        Name = name;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
    }
}

#if UNITY
[Unity.Burst.BurstCompile]
#endif
[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members")]
[SuppressMessage("Style", "IDE0059:Unnecessary assignment of a value")]
public static unsafe class ExternalFunctionGenerator
{
    public static bool TryGet(this IReadOnlyList<IExternalFunction> externalFunctions, string name, [NotNullWhen(true)] out IExternalFunction? result, [NotNullWhen(false)] out WillBeCompilerException? exception)
    {
        result = null;
        exception = null;

        foreach (IExternalFunction function in externalFunctions)
        {
            if (function.Name == name)
            {
                if (result is not null)
                {
                    exception = new WillBeCompilerException($"External function with name \"{name}\" not found: duplicated function names");
                    return false;
                }

                result = function;
            }
        }

        if (result is null)
        {
            exception = new WillBeCompilerException($"External function with name \"{name}\" not found");
            return false;
        }

        return true;
    }

    public static int GenerateId(this List<IExternalFunction> functions, string? name = null)
    {
        int result;

        if (name is not null) result = name.GetHashCode();
        else result = 1;

        while (functions.Any(v => v.Id == result))
        { result++; }

        return result;
    }

    #region AddExternalFunction()

    public static void AddExternalFunction(this List<IExternalFunction> functions, IExternalFunction function)
    {
        int i = functions.FindIndex(v => v.Id == function.Id);
        if (i != -1)
        { functions[i] = function; }
        else
        { functions.Add(function); }
    }

    #endregion

    #region GetTypes<>()

    /// <exception cref="NotImplementedException"/>
    static ImmutableArray<RuntimeType> GetTypes<T0>() => ImmutableArray.Create(
        GetType<T0>()
    );
    /// <exception cref="NotImplementedException"/>
    static ImmutableArray<RuntimeType> GetTypes<T0, T1>() => ImmutableArray.Create(
        GetType<T0>(),
        GetType<T1>()
    );
    /// <exception cref="NotImplementedException"/>
    static ImmutableArray<RuntimeType> GetTypes<T0, T1, T2>() => ImmutableArray.Create(
        GetType<T0>(),
        GetType<T1>(),
        GetType<T2>()
    );
    /// <exception cref="NotImplementedException"/>
    static ImmutableArray<RuntimeType> GetTypes<T0, T1, T2, T3>() => ImmutableArray.Create(
        GetType<T0>(),
        GetType<T1>(),
        GetType<T2>(),
        GetType<T3>()
    );
    /// <exception cref="NotImplementedException"/>
    static ImmutableArray<RuntimeType> GetTypes<T0, T1, T2, T3, T4>() => ImmutableArray.Create(
        GetType<T0>(),
        GetType<T1>(),
        GetType<T2>(),
        GetType<T3>(),
        GetType<T4>()
    );
    /// <exception cref="NotImplementedException"/>
    static ImmutableArray<RuntimeType> GetTypes<T0, T1, T2, T3, T4, T5>() => ImmutableArray.Create(
        GetType<T0>(),
        GetType<T1>(),
        GetType<T2>(),
        GetType<T3>(),
        GetType<T4>(),
        GetType<T5>()
    );

    /// <exception cref="NotImplementedException"/>
    static RuntimeType GetType<T>()
    {
        Type type_ = typeof(T);

        if (type_ == typeof(byte))
        { return RuntimeType.U8; }

        if (type_ == typeof(sbyte))
        { return RuntimeType.I8; }

        if (type_ == typeof(char))
        { return RuntimeType.Char; }

        if (type_ == typeof(ushort))
        { return RuntimeType.Char; }

        if (type_ == typeof(short))
        { return RuntimeType.I16; }

        if (type_ == typeof(int))
        { return RuntimeType.I32; }

        if (type_ == typeof(uint))
        { return RuntimeType.U32; }

        if (type_ == typeof(float))
        { return RuntimeType.F32; }

        if (type_.IsClass)
        { return RuntimeType.I32; }

        if (type_ == typeof(IntPtr))
        { return RuntimeType.I32; }

        if (type_ == typeof(UIntPtr))
        { return RuntimeType.I32; }

        throw new NotImplementedException($"Type conversion for type {typeof(T)} not implemented");
    }

    #endregion

    #region SizeOf

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T0>()
        where T0 : unmanaged
        => sizeof(T0);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T0, T1>()
        where T0 : unmanaged
        where T1 : unmanaged
        => sizeof(T0) + sizeof(T1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T0, T1, T2>()
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => sizeof(T0) + sizeof(T1) + sizeof(T2);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T0, T1, T2, T3>()
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => sizeof(T0) + sizeof(T1) + sizeof(T2) + sizeof(T3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T0, T1, T2, T3, T4>()
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => sizeof(T0) + sizeof(T1) + sizeof(T2) + sizeof(T3) + sizeof(T4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SizeOf<T0, T1, T2, T3, T4, T5>()
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => sizeof(T0) + sizeof(T1) + sizeof(T2) + sizeof(T3) + sizeof(T4) + sizeof(T5);

    #endregion

    #region TakeParameters

    public static T0 TakeParameters<T0>(ReadOnlySpan<byte> data)
        where T0 : unmanaged
    {
        T0 p0;

        int ptr = 0;

        p0 = data.Slice(ptr, sizeof(T0)).To<T0>();
        ptr += sizeof(T0);

        return p0;
    }

    public static (T0 P0, T1 P1) TakeParameters<T0, T1>(ReadOnlySpan<byte> data)
        where T0 : unmanaged
        where T1 : unmanaged
    {
        T0 p0;
        T1 p1;

        int ptr = 0;

        p1 = data.Slice(ptr, sizeof(T1)).To<T1>();
        ptr += sizeof(T1);

        p0 = data.Slice(ptr, sizeof(T0)).To<T0>();
        ptr += sizeof(T0);

        return (p0, p1);
    }

    public static (T0 P0, T1 P1, T2 P2) TakeParameters<T0, T1, T2>(ReadOnlySpan<byte> data)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
    {
        T0 p0;
        T1 p1;
        T2 p2;

        int ptr = 0;

        p2 = data.Slice(ptr, sizeof(T2)).To<T2>();
        ptr += sizeof(T2);

        p1 = data.Slice(ptr, sizeof(T1)).To<T1>();
        ptr += sizeof(T1);

        p0 = data.Slice(ptr, sizeof(T0)).To<T0>();
        ptr += sizeof(T0);

        return (p0, p1, p2);
    }

    public static (T0 P0, T1 P1, T2 P2, T3 P3) TakeParameters<T0, T1, T2, T3>(ReadOnlySpan<byte> data)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        T0 p0;
        T1 p1;
        T2 p2;
        T3 p3;

        int ptr = 0;

        p3 = data.Slice(ptr, sizeof(T3)).To<T3>();
        ptr += sizeof(T3);

        p2 = data.Slice(ptr, sizeof(T2)).To<T2>();
        ptr += sizeof(T2);

        p1 = data.Slice(ptr, sizeof(T1)).To<T1>();
        ptr += sizeof(T1);

        p0 = data.Slice(ptr, sizeof(T0)).To<T0>();
        ptr += sizeof(T0);

        return (p0, p1, p2, p3);
    }

    public static (T0 P0, T1 P1, T2 P2, T3 P3, T4 P4) TakeParameters<T0, T1, T2, T3, T4>(ReadOnlySpan<byte> data)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        T0 p0;
        T1 p1;
        T2 p2;
        T3 p3;
        T4 p4;

        int ptr = 0;

        p4 = data.Slice(ptr, sizeof(T4)).To<T4>();
        ptr += sizeof(T4);

        p3 = data.Slice(ptr, sizeof(T3)).To<T3>();
        ptr += sizeof(T3);

        p2 = data.Slice(ptr, sizeof(T2)).To<T2>();
        ptr += sizeof(T2);

        p1 = data.Slice(ptr, sizeof(T1)).To<T1>();
        ptr += sizeof(T1);

        p0 = data.Slice(ptr, sizeof(T0)).To<T0>();
        ptr += sizeof(T0);

        return (p0, p1, p2, p3, p4);
    }

    public static (T0 P0, T1 P1, T2 P2, T3 P3, T4 P4, T5 P5) TakeParameters<T0, T1, T2, T3, T4, T5>(ReadOnlySpan<byte> data)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        T0 p0;
        T1 p1;
        T2 p2;
        T3 p3;
        T4 p4;
        T5 p5;

        int ptr = 0;

        p5 = data.Slice(ptr, sizeof(T5)).To<T5>();
        ptr += sizeof(T5);

        p4 = data.Slice(ptr, sizeof(T4)).To<T4>();
        ptr += sizeof(T4);

        p3 = data.Slice(ptr, sizeof(T3)).To<T3>();
        ptr += sizeof(T3);

        p2 = data.Slice(ptr, sizeof(T2)).To<T2>();
        ptr += sizeof(T2);

        p1 = data.Slice(ptr, sizeof(T1)).To<T1>();
        ptr += sizeof(T1);

        p0 = data.Slice(ptr, sizeof(T0)).To<T0>();
        ptr += sizeof(T0);

        return (p0, p1, p2, p3, p4, p5);
    }

    #endregion
}
