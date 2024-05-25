namespace LanguageCore.Runtime;

using Compiler;

public abstract class ExternalFunctionBase : ISimpleReadable
{
    public ImmutableArray<RuntimeType> Parameters { get; }
    public string? Name { get; }
    public int Id { get; }
    public bool ReturnSomething { get; }

    public BytecodeProcessor? BytecodeInterpreter { get; set; }

    protected ExternalFunctionBase(int id, string? name, IEnumerable<RuntimeType> parameters, bool returnSomething)
    {
        Id = id;
        Name = name;
        Parameters = parameters.ToImmutableArray();
        ReturnSomething = returnSomething;
    }

    protected void BeforeCallback(ImmutableArray<RuntimeValue> parameters)
    {
        if (parameters.Length != Parameters.Length)
        { throw new RuntimeException($"Wrong number of parameters passed to external function {Name} ({parameters.Length}): expected {Parameters.Length}"); }
    }

    public string ToReadable()
    {
        StringBuilder result = new();
        result.Append(Name);
        result.Append('(');
        for (int i = 0; i < Parameters.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(Parameters[i]);
        }
        result.Append(')');
        return result.ToString();
    }
}

public class ExternalFunctionSimple : ExternalFunctionBase
{
    protected Func<BytecodeProcessor, ImmutableArray<RuntimeValue>, RuntimeValue> callback;

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionSimple(Action<BytecodeProcessor, ImmutableArray<RuntimeValue>> callback, int id, string? name, IEnumerable<RuntimeType> parameters)
        : base(id, name, parameters, false)
    {
        this.callback = (sender, v) =>
        {
            callback?.Invoke(sender, v);
            return default;
        };
    }

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionSimple(Func<BytecodeProcessor, ImmutableArray<RuntimeValue>, RuntimeValue> callback, int id, string? name, IEnumerable<RuntimeType> parameters)
        : base(id, name, parameters, true)
    {
        this.callback = callback;
    }

    /// <exception cref="InternalException"/>
    public RuntimeValue Call(BytecodeProcessor sender, ImmutableArray<RuntimeValue> parameters)
    {
        base.BeforeCallback(parameters);

        if (callback == null)
        { throw new InternalException("Callback is null"); }

        return callback.Invoke(sender, parameters);
    }
}

public class ExternalFunctionManaged : ExternalFunctionBase
{
    public delegate void ReturnEvent(RuntimeValue returnValue);

    public ReturnEvent? OnReturn;
    readonly Func<ImmutableArray<RuntimeValue>, RuntimeValue> callback;

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionManaged(Action<ImmutableArray<RuntimeValue>, ExternalFunctionManaged> callback, int id, string? name, IEnumerable<RuntimeType> parameters)
             : base(id, name, parameters, true)
    {
        this.callback = new Func<ImmutableArray<RuntimeValue>, RuntimeValue>((p) =>
        {
            callback?.Invoke(p, this);
            return default;
        });
    }

    /// <exception cref="InternalException"></exception>
    public void Callback(ImmutableArray<RuntimeValue> parameters)
    {
        base.BeforeCallback(parameters);
        callback.Invoke(parameters);
    }
}

public static unsafe class ExternalFunctionGenerator
{
    public static bool TryGet(this IReadOnlyDictionary<int, ExternalFunctionBase> externalFunctions, string name, [NotNullWhen(true)] out ExternalFunctionBase? result, [NotNullWhen(false)] out WillBeCompilerException? exception)
    {
        result = null;
        exception = null;

        foreach (ExternalFunctionBase function in externalFunctions.Values)
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

    public static int GenerateId(this Dictionary<int, ExternalFunctionBase> functions, string? name)
    {
        int result;

        if (name is not null) result = name.GetHashCode();
        else result = 1;

        while (functions.ContainsKey(result))
        { result++; }

        return result;
    }

    #region AddExternalFunction()

    public static void AddManagedExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, Action<ImmutableArray<RuntimeValue>, ExternalFunctionManaged> callback)
        => functions.AddExternalFunction(new ExternalFunctionManaged(callback, functions.GenerateId(name), name, parameterTypes));

    public static void AddManagedExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, Action<ImmutableArray<RuntimeValue>, ExternalFunctionManaged> callback)
        => functions.AddExternalFunction(new ExternalFunctionManaged(callback, id, name, parameterTypes));

    public static void AddSimpleExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, Func<BytecodeProcessor, ImmutableArray<RuntimeValue>, RuntimeValue> callback)
        => functions.AddExternalFunction(new ExternalFunctionSimple(callback, id, name, parameterTypes));

    public static void AddSimpleExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, Func<BytecodeProcessor, ImmutableArray<RuntimeValue>, RuntimeValue> callback)
        => functions.AddExternalFunction(new ExternalFunctionSimple(callback, functions.GenerateId(name), name, parameterTypes));

    public static void AddSimpleExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, Action<BytecodeProcessor, ImmutableArray<RuntimeValue>> callback)
        => functions.AddExternalFunction(new ExternalFunctionSimple(callback, id, name, parameterTypes));

    public static void AddSimpleExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, Action<BytecodeProcessor, ImmutableArray<RuntimeValue>> callback)
        => functions.AddExternalFunction(new ExternalFunctionSimple(callback, functions.GenerateId(name), name, parameterTypes));

    static void AddExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, ExternalFunctionBase function)
    {
        if (!functions.TryAdd(function.Id, function))
        {
            functions[function.Id] = function;
            Debug.WriteLine($"External function \"{function.Name}\" with id {function.Id} is already defined, so I'll override it");
        }
    }

    public static void AddExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, string name, Action callback)
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    public static void AddExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action callback)
    {
        ImmutableArray<RuntimeType> types = ImmutableArray<RuntimeType>.Empty;

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke();
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0> callback)
        where T0 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0> callback)
        where T0 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                ValueUtils.To<T0>(sender, args[0]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0, T1> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0, T1> callback)
        where T0 : unmanaged
        where T1 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0, T1, T2> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0, T1, T2> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]),
                ValueUtils.To<T2>(sender, args[2]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0, T1, T2, T3> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]),
                ValueUtils.To<T2>(sender, args[2]),
                ValueUtils.To<T3>(sender, args[3]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3, T4> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0, T1, T2, T3, T4> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3, T4>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]),
                ValueUtils.To<T2>(sender, args[2]),
                ValueUtils.To<T3>(sender, args[3]),
                ValueUtils.To<T4>(sender, args[4]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3, T4, T5> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0, T1, T2, T3, T4, T5> callback)
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3, T4, T5>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]),
                ValueUtils.To<T2>(sender, args[2]),
                ValueUtils.To<T3>(sender, args[3]),
                ValueUtils.To<T4>(sender, args[4]),
                ValueUtils.To<T5>(sender, args[5]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<TResult> callback)
        where TResult : notnull
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<TResult> callback)
        where TResult : notnull
    {
        ImmutableArray<RuntimeType> types = ImmutableArray<RuntimeType>.Empty;

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke();

            return ValueUtils.From(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                ValueUtils.To<T0>(sender, args[0]));

            return ValueUtils.From(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0, T1, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0, T1, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]));

            return ValueUtils.From(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0, T1, T2, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0, T1, T2, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]),
                ValueUtils.To<T2>(sender, args[2]));

            return ValueUtils.From(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0, T1, T2, T3, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0, T1, T2, T3, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]),
                ValueUtils.To<T2>(sender, args[2]),
                ValueUtils.To<T3>(sender, args[3]));

            return ValueUtils.From(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0, T1, T2, T3, T4, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0, T1, T2, T3, T4, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3, T4>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]),
                ValueUtils.To<T2>(sender, args[2]),
                ValueUtils.To<T3>(sender, args[3]),
                ValueUtils.To<T4>(sender, args[4]));

            return ValueUtils.From(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0, T1, T2, T3, T4, T5, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0, T1, T2, T3, T4, T5, TResult> callback)
        where TResult : notnull
        where T0 : unmanaged
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3, T4, T5>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                ValueUtils.To<T0>(sender, args[0]),
                ValueUtils.To<T1>(sender, args[1]),
                ValueUtils.To<T2>(sender, args[2]),
                ValueUtils.To<T3>(sender, args[3]),
                ValueUtils.To<T4>(sender, args[4]),
                ValueUtils.To<T5>(sender, args[5]));

            return ValueUtils.From(result);
        });
    }

    public static void SetInterpreter(this IReadOnlyDictionary<int, ExternalFunctionBase> functions, BytecodeProcessor interpreter)
    {
        foreach (KeyValuePair<int, ExternalFunctionBase> item in functions)
        { item.Value.BytecodeInterpreter = interpreter; }
    }

    #endregion

    /// <exception cref="RuntimeException"/>
    static void CheckParameters(int id, string? functionName, ImmutableArray<RuntimeType> required, ImmutableArray<RuntimeValue> passed)
    {
        if (passed.Length != required.Length)
        { throw new RuntimeException($"Wrong number of parameters passed to external function \"{functionName}\" (with id {id}) ({passed.Length}) which requires {required.Length}"); }
    }

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
}

static class ValueUtils
{
    /// <exception cref="NotImplementedException"/>
    public static RuntimeValue From<T>(T value) where T : notnull => value switch
    {
        byte v => new RuntimeValue(v),
        int v => new RuntimeValue(v),
        float v => new RuntimeValue(v),
        bool v => new RuntimeValue(v),
        char v => new RuntimeValue(v),
        _ => throw new NotImplementedException($"Cannot convert {value.GetType()} to {typeof(RuntimeValue)}"),
    };

    /// <inheritdoc/>
    /// <exception cref="InvalidCastException"/>
    public static T To<T>(BytecodeProcessor bytecodeProcessor, RuntimeValue value)
        where T : unmanaged
    {
        if (typeof(T) == typeof(byte)) return UnsafeCast<byte, T>((byte)value.Int);
        if (typeof(T) == typeof(sbyte)) return UnsafeCast<sbyte, T>((sbyte)value.Int);
        if (typeof(T) == typeof(short)) return UnsafeCast<short, T>((short)value.Int);
        if (typeof(T) == typeof(ushort)) return UnsafeCast<ushort, T>((ushort)value.Int);
        if (typeof(T) == typeof(int)) return UnsafeCast<int, T>(value.Int);
        if (typeof(T) == typeof(uint)) return UnsafeCast<uint, T>((uint)value.Int);
        if (typeof(T) == typeof(long)) return UnsafeCast<long, T>(value.Int);
        if (typeof(T) == typeof(ulong)) return UnsafeCast<ulong, T>((ulong)value.Int);
        if (typeof(T) == typeof(float)) return UnsafeCast<float, T>(value.Single);
        if (typeof(T) == typeof(decimal)) return UnsafeCast<decimal, T>((decimal)value.Single);
        if (typeof(T) == typeof(double)) return UnsafeCast<double, T>(value.Single);
        if (typeof(T) == typeof(bool)) return UnsafeCast<bool, T>(value.Int != 0);
        if (typeof(T) == typeof(char)) return UnsafeCast<char, T>((char)value.Int);

        if (typeof(T) == typeof(IntPtr))
        {
            if (IntPtr.Size == 4)
            { return UnsafeCast<IntPtr, T>(new IntPtr(value.Int)); }
            else
            { return UnsafeCast<IntPtr, T>(new IntPtr((long)value.Int)); }
        }

        if (typeof(T) == typeof(UIntPtr))
        {
            if (UIntPtr.Size == 4)
            { return UnsafeCast<UIntPtr, T>(new UIntPtr((uint)value.Int)); }
            else
            { return UnsafeCast<UIntPtr, T>(new UIntPtr((ulong)value.Int)); }
        }

        throw new NotImplementedException($"Type conversion {typeof(T)} not implemented");
    }

    static unsafe TTo UnsafeCast<TFrom, TTo>(TFrom value)
        where TFrom : unmanaged
        where TTo : unmanaged
        => *(TTo*)&value;
}
