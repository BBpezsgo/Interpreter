using System.Reflection;

namespace LanguageCore.Runtime;

public readonly struct ExternalFunctionManaged : IExternalFunction
{
    public string? Name { get; }
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public MethodInfo Method { get; }

    public ExternalFunctionManaged(Delegate method, int id, string? name, int parametersSize, int returnValueSize)
    {
        Method = method.Method;
        Id = id;
        Name = name;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
    }

    #region Generators

    public static ExternalFunctionManaged Create(int id, string? name, Action callback) => new(callback, id, name, 0, 0);

    public static ExternalFunctionManaged Create<T0>(int id, string? name, Action<T0> callback)
        where T0 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0>(), 0);

    public static ExternalFunctionManaged Create<T0, T1>(int id, string? name, Action<T0, T1> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1>(), 0);

    public static ExternalFunctionManaged Create<T0, T1, T2>(int id, string? name, Action<T0, T1, T2> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2>(), 0);

    public static ExternalFunctionManaged Create<T0, T1, T2, T3>(int id, string? name, Action<T0, T1, T2, T3> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3>(), 0);

    public static ExternalFunctionManaged Create<T0, T1, T2, T3, T4>(int id, string? name, Action<T0, T1, T2, T3, T4> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4>(), 0);

    public static ExternalFunctionManaged Create<T0, T1, T2, T3, T4, T5>(int id, string? name, Action<T0, T1, T2, T3, T4, T5> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4, T5>(), 0);

    public static ExternalFunctionManaged Create<TResult>(int id, string? name, Func<TResult> callback)
        where TResult : unmanaged
        => new(callback, id, name, 0, ExternalFunctionGenerator.SizeOf<TResult>());

    public static ExternalFunctionManaged Create<T0, TResult>(int id, string? name, Func<T0, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0>(), ExternalFunctionGenerator.SizeOf<TResult>());

    public static ExternalFunctionManaged Create<T0, T1, TResult>(int id, string? name, Func<T0, T1, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1>(), ExternalFunctionGenerator.SizeOf<TResult>());

    public static ExternalFunctionManaged Create<T0, T1, T2, TResult>(int id, string? name, Func<T0, T1, T2, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2>(), ExternalFunctionGenerator.SizeOf<TResult>());

    public static ExternalFunctionManaged Create<T0, T1, T2, T3, TResult>(int id, string? name, Func<T0, T1, T2, T3, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3>(), ExternalFunctionGenerator.SizeOf<TResult>());

    public static ExternalFunctionManaged Create<T0, T1, T2, T3, T4, TResult>(int id, string? name, Func<T0, T1, T2, T3, T4, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4>(), ExternalFunctionGenerator.SizeOf<TResult>());

    public static ExternalFunctionManaged Create<T0, T1, T2, T3, T4, T5, TResult>(int id, string? name, Func<T0, T1, T2, T3, T4, T5, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => new(callback, id, name, ExternalFunctionGenerator.SizeOf<T0, T1, T2, T3, T4, T5>(), ExternalFunctionGenerator.SizeOf<TResult>());

    #endregion
}
