using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

public delegate ReadOnlySpan<byte> ExternalFunctionSyncCallback(BytecodeProcessor bytecodeProcessor, ReadOnlySpan<byte> arguments);
public delegate void ManagedExternalFunctionAsyncBlockCallback(ReadOnlySpan<byte> arguments, ManagedExternalFunctionAsyncBlockReturnCallback callback);
public delegate void ManagedExternalFunctionAsyncBlockReturnCallback(ReadOnlySpan<byte> returnValue);

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
}

public readonly struct ExternalFunctionAsyncBlock : IExternalFunction
{
    public string? Name { get; }
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
    public ManagedExternalFunctionAsyncBlockCallback Callback { get; }

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionAsyncBlock(ManagedExternalFunctionAsyncBlockCallback callback, int id, string? name, int parametersSize, int returnValueSize)
    {
        Callback = callback;
        Id = id;
        Name = name;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
    }
}

[SuppressMessage("Style", "IDE0008:Use explicit type")]
[SuppressMessage("CodeQuality", "IDE0051:Remove unused private members")]
public static unsafe class ExternalFunctionGenerator
{
    public static bool TryGet(this IReadOnlyDictionary<int, IExternalFunction> externalFunctions, string name, [NotNullWhen(true)] out IExternalFunction? result, [NotNullWhen(false)] out WillBeCompilerException? exception)
    {
        result = null;
        exception = null;

        foreach (IExternalFunction function in externalFunctions.Values)
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

    public static int GenerateId(this Dictionary<int, IExternalFunction> functions, string? name)
    {
        int result;

        if (name is not null) result = name.GetHashCode();
        else result = 1;

        while (functions.ContainsKey(result))
        { result++; }

        return result;
    }

    #region AddExternalFunction()

    public static void AddManagedExternalFunction(this Dictionary<int, IExternalFunction> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, ManagedExternalFunctionAsyncBlockCallback callback, int returnValueSize)
        => functions.AddExternalFunction(new ExternalFunctionAsyncBlock(callback, functions.GenerateId(name), name, SizeOf(parameterTypes), returnValueSize));

    public static void AddManagedExternalFunction(this Dictionary<int, IExternalFunction> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, ManagedExternalFunctionAsyncBlockCallback callback)
        => functions.AddExternalFunction(new ExternalFunctionAsyncBlock(callback, id, name, SizeOf(parameterTypes), 0));

    public static void AddSimpleExternalFunction(this Dictionary<int, IExternalFunction> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, ExternalFunctionSyncCallback callback, int returnValueSize)
        => functions.AddExternalFunction(new ExternalFunctionSync(callback, id, name, SizeOf(parameterTypes), returnValueSize));

    public static void AddSimpleExternalFunction(this Dictionary<int, IExternalFunction> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, ExternalFunctionSyncCallback callback, int returnValueSize)
        => functions.AddExternalFunction(new ExternalFunctionSync(callback, functions.GenerateId(name), name, SizeOf(parameterTypes), returnValueSize));

    public static void AddSimpleExternalFunction(this Dictionary<int, IExternalFunction> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, ExternalFunctionSyncCallback callback)
        => functions.AddExternalFunction(new ExternalFunctionSync(callback, id, name, SizeOf(parameterTypes), 0));

    public static void AddSimpleExternalFunction(this Dictionary<int, IExternalFunction> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, ExternalFunctionSyncCallback callback)
        => functions.AddExternalFunction(new ExternalFunctionSync(callback, functions.GenerateId(name), name, SizeOf(parameterTypes), 0));

    public static void AddManagedExternalFunction(this Dictionary<int, IExternalFunction> functions, string? name, int parametersSize, ManagedExternalFunctionAsyncBlockCallback callback, int returnValueSize)
        => functions.AddExternalFunction(new ExternalFunctionAsyncBlock(callback, functions.GenerateId(name), name, parametersSize, returnValueSize));

    public static void AddManagedExternalFunction(this Dictionary<int, IExternalFunction> functions, int id, string? name, int parametersSize, ManagedExternalFunctionAsyncBlockCallback callback)
        => functions.AddExternalFunction(new ExternalFunctionAsyncBlock(callback, id, name, parametersSize, 0));

    public static void AddSimpleExternalFunction(this Dictionary<int, IExternalFunction> functions, int id, string? name, int parametersSize, ExternalFunctionSyncCallback callback, int returnValueSize)
        => functions.AddExternalFunction(new ExternalFunctionSync(callback, id, name, parametersSize, returnValueSize));

    public static void AddSimpleExternalFunction(this Dictionary<int, IExternalFunction> functions, string? name, int parametersSize, ExternalFunctionSyncCallback callback, int returnValueSize)
        => functions.AddExternalFunction(new ExternalFunctionSync(callback, functions.GenerateId(name), name, parametersSize, returnValueSize));

    public static void AddSimpleExternalFunction(this Dictionary<int, IExternalFunction> functions, int id, string? name, int parametersSize, ExternalFunctionSyncCallback callback)
        => functions.AddExternalFunction(new ExternalFunctionSync(callback, id, name, parametersSize, 0));

    public static void AddSimpleExternalFunction(this Dictionary<int, IExternalFunction> functions, string? name, int parametersSize, ExternalFunctionSyncCallback callback)
        => functions.AddExternalFunction(new ExternalFunctionSync(callback, functions.GenerateId(name), name, parametersSize, 0));

    static void AddExternalFunction(this Dictionary<int, IExternalFunction> functions, IExternalFunction function)
    {
        if (!functions.TryAdd(function.Id, function))
        { functions[function.Id] = function; }
    }

    public static void AddExternalFunction(this Dictionary<int, IExternalFunction> functions, string name, Action callback)
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    public static void AddExternalFunction(this Dictionary<int, IExternalFunction> functions, int id, string? name, Action callback)
    {
        ImmutableArray<RuntimeType> types = ImmutableArray<RuntimeType>.Empty;

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            callback.Invoke();
            return default;
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0>(this Dictionary<int, IExternalFunction> functions, string name, Action<T0> callback)
        where T0 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Action<T0> callback)
        where T0 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0>(args);
            callback.Invoke(
                _args);
            return default;
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1>(this Dictionary<int, IExternalFunction> functions, string name, Action<T0, T1> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Action<T0, T1> callback)
        where T0 : unmanaged
        where T1 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0, T1>(args);
            callback.Invoke(
                _args.P0,
                _args.P1);
            return default;
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2>(this Dictionary<int, IExternalFunction> functions, string name, Action<T0, T1, T2> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Action<T0, T1, T2> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1, T2>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0, T1, T2>(args);
            callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2);
            return default;
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<int, IExternalFunction> functions, string name, Action<T0, T1, T2, T3> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Action<T0, T1, T2, T3> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1, T2, T3>(), (sender, args) =>
        {
            var _args = DeconstructParameters<T0, T1, T2, T3>(args);
            callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3);
            return default;
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<int, IExternalFunction> functions, string name, Action<T0, T1, T2, T3, T4> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Action<T0, T1, T2, T3, T4> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1, T2, T3, T4>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0, T1, T2, T3, T4>(args);
            callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3,
                _args.P4);
            return default;
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<int, IExternalFunction> functions, string name, Action<T0, T1, T2, T3, T4, T5> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Action<T0, T1, T2, T3, T4, T5> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1, T2, T3, T4, T5>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0, T1, T2, T3, T4, T5>(args);
            callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3,
                _args.P4,
                _args.P5);
            return default;
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<TResult>(this Dictionary<int, IExternalFunction> functions, string? name, Func<TResult> callback)
        where TResult : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<TResult>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Func<TResult> callback)
        where TResult : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, 0, (sender, args) =>
        {
            TResult result = callback.Invoke();

            return result.ToBytes();
        }, SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, TResult>(this Dictionary<int, IExternalFunction> functions, string? name, Func<T0, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, TResult>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Func<T0, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0>(args);
            TResult result = callback.Invoke(
                _args);

            return result.ToBytes();
        }, SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, TResult>(this Dictionary<int, IExternalFunction> functions, string? name, Func<T0, T1, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, TResult>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Func<T0, T1, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0, T1>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1);

            return result.ToBytes();
        }, SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, TResult>(this Dictionary<int, IExternalFunction> functions, string? name, Func<T0, T1, T2, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, TResult>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Func<T0, T1, T2, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1, T2>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0, T1, T2>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2);

            return result.ToBytes();
        }, SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, TResult>(this Dictionary<int, IExternalFunction> functions, string? name, Func<T0, T1, T2, T3, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, TResult>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Func<T0, T1, T2, T3, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1, T2, T3>(), (sender, args) =>
        {
            var _args = DeconstructParameters<T0, T1, T2, T3>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3);

            return result.ToBytes();
        }, SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, TResult>(this Dictionary<int, IExternalFunction> functions, string? name, Func<T0, T1, T2, T3, T4, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, TResult>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Func<T0, T1, T2, T3, T4, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1, T2, T3, T4>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0, T1, T2, T3, T4>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3,
                _args.P4);

            return result.ToBytes();
        }, SizeOf<TResult>());
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5, TResult>(this Dictionary<int, IExternalFunction> functions, string? name, Func<T0, T1, T2, T3, T4, T5, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5, TResult>(this Dictionary<int, IExternalFunction> functions, int id, string? name, Func<T0, T1, T2, T3, T4, T5, TResult> callback)
        where TResult : unmanaged
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        functions.AddSimpleExternalFunction(id, name, SizeOf<T0, T1, T2, T3, T4, T5>(), (sender, args) =>
        {
            var _args = DeconstructValues<T0, T1, T2, T3, T4, T5>(args);
            TResult result = callback.Invoke(
                _args.P0,
                _args.P1,
                _args.P2,
                _args.P3,
                _args.P4,
                _args.P5);

            return result.ToBytes();
        }, SizeOf<TResult>());
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
        { return RuntimeType.Byte; }

        if (type_ == typeof(int))
        { return RuntimeType.Integer; }

        if (type_ == typeof(float))
        { return RuntimeType.Single; }

        if (type_ == typeof(char))
        { return RuntimeType.Char; }

        if (type_.IsClass)
        { return RuntimeType.Integer; }

        if (type_ == typeof(uint))
        { return RuntimeType.Integer; }

        if (type_ == typeof(IntPtr))
        { return RuntimeType.Integer; }

        if (type_ == typeof(UIntPtr))
        { return RuntimeType.Integer; }

        throw new NotImplementedException($"Type conversion for type {typeof(T)} not implemented");
    }

    #endregion

    #region Other

    static int SizeOf(ImmutableArray<RuntimeType> types)
    {
        int size = 0;
        foreach (RuntimeType type in types)
        {
            size += type switch
            {
                RuntimeType.Null => 0,
                RuntimeType.Byte => 1,
                RuntimeType.Char => 2,
                RuntimeType.Integer => 4,
                RuntimeType.Single => 4,
                _ => throw new UnreachableException(),
            };
        }
        return size;
    }

    static int SizeOf<T0>()
        where T0 : unmanaged
        => sizeof(T0);

    static int SizeOf<T0, T1>()
        where T0 : unmanaged
        where T1 : unmanaged
        => sizeof(T0) + sizeof(T1);

    static int SizeOf<T0, T1, T2>()
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => sizeof(T0) + sizeof(T1) + sizeof(T2);

    static int SizeOf<T0, T1, T2, T3>()
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => sizeof(T0) + sizeof(T1) + sizeof(T2) + sizeof(T3);

    static int SizeOf<T0, T1, T2, T3, T4>()
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => sizeof(T0) + sizeof(T1) + sizeof(T2) + sizeof(T3) + sizeof(T4);

    static int SizeOf<T0, T1, T2, T3, T4, T5>()
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => sizeof(T0) + sizeof(T1) + sizeof(T2) + sizeof(T3) + sizeof(T4) + sizeof(T5);

    static T0 DeconstructValues<T0>(ReadOnlySpan<byte> data)
        where T0 : unmanaged
    {
        T0 p0;

        int ptr = 0;

        p0 = data.Slice(ptr, sizeof(T0)).To<T0>();
        ptr += sizeof(T0);

        return p0;
    }

    static (T0 P0, T1 P1) DeconstructValues<T0, T1>(ReadOnlySpan<byte> data)
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

    static (T0 P0, T1 P1, T2 P2) DeconstructValues<T0, T1, T2>(ReadOnlySpan<byte> data)
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

    static (T0 P0, T1 P1, T2 P2, T3 P3) DeconstructParameters<T0, T1, T2, T3>(ReadOnlySpan<byte> data)
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

    static (T0 P0, T1 P1, T2 P2, T3 P3, T4 P4) DeconstructValues<T0, T1, T2, T3, T4>(ReadOnlySpan<byte> data)
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

    static (T0 P0, T1 P1, T2 P2, T3 P3, T4 P4, T5 P5) DeconstructValues<T0, T1, T2, T3, T4, T5>(ReadOnlySpan<byte> data)
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
