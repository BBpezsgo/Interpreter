﻿namespace LanguageCore.Runtime;

[Flags]
public enum ExternalFunctionCheckFlags : byte
{
    None = 0,
    CheckParamLength = 1,
}

public abstract class ExternalFunctionBase : ISimpleReadable
{
    public const ExternalFunctionCheckFlags DefaultFlags = ExternalFunctionCheckFlags.CheckParamLength;

    public ImmutableArray<RuntimeType> Parameters { get; }
    public string? Name { get; }
    public int Id { get; }
    public bool ReturnSomething { get; }
    public ExternalFunctionCheckFlags Flags { get; }

    public bool CheckParameterLength => ((byte)Flags & (byte)ExternalFunctionCheckFlags.CheckParamLength) != 0;

    public BytecodeProcessor? BytecodeInterpreter { get; set; }

    protected ExternalFunctionBase(int id, string? name, IEnumerable<RuntimeType> parameters, bool returnSomething, ExternalFunctionCheckFlags flags)
    {
        Id = id;
        Name = name;
        Parameters = parameters.ToImmutableArray();
        ReturnSomething = returnSomething;
        Flags = flags;
    }

    protected void BeforeCallback(ImmutableArray<DataItem> parameters)
    {
        if (CheckParameterLength && parameters.Length != Parameters.Length)
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
    protected Func<BytecodeProcessor, ImmutableArray<DataItem>, DataItem> callback;

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionSimple(Action<BytecodeProcessor, ImmutableArray<DataItem>> callback, int id, string? name, IEnumerable<RuntimeType> parameters, ExternalFunctionCheckFlags flags)
        : base(id, name, parameters, false, flags)
    {
        this.callback = (sender, v) =>
        {
            callback?.Invoke(sender, v);
            return DataItem.Null;
        };
    }

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionSimple(Func<BytecodeProcessor, ImmutableArray<DataItem>, DataItem> callback, int id, string? name, IEnumerable<RuntimeType> parameters, ExternalFunctionCheckFlags flags)
        : base(id, name, parameters, true, flags)
    {
        this.callback = callback;
    }

    /// <exception cref="InternalException"/>
    public DataItem Call(BytecodeProcessor sender, ImmutableArray<DataItem> parameters)
    {
        base.BeforeCallback(parameters);

        if (callback == null)
        { throw new InternalException("Callback is null"); }

        return callback.Invoke(sender, parameters);
    }
}

public class ExternalFunctionManaged : ExternalFunctionBase
{
    public delegate void ReturnEvent(DataItem returnValue);

    public ReturnEvent? OnReturn;
    readonly Func<ImmutableArray<DataItem>, DataItem> callback;

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionManaged(Action<ImmutableArray<DataItem>, ExternalFunctionManaged> callback, int id, string? name, IEnumerable<RuntimeType> parameters, ExternalFunctionCheckFlags flags)
             : base(id, name, parameters, true, flags)
    {
        this.callback = new Func<ImmutableArray<DataItem>, DataItem>((p) =>
        {
            callback?.Invoke(p, this);
            return DataItem.Null;
        });
    }

    /// <exception cref="InternalException"></exception>
    public void Callback(ImmutableArray<DataItem> parameters)
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

    [RequiresUnreferencedCode("Loading Assembly")]
    public static void LoadAssembly(this Dictionary<int, ExternalFunctionBase> externalFunctions, string path)
        => ExternalFunctionGenerator.LoadAssembly(externalFunctions, System.Reflection.Assembly.LoadFile(path));

    [RequiresUnreferencedCode("Loading Assembly")]
    public static void LoadAssembly(this Dictionary<int, ExternalFunctionBase> externalFunctions, System.Reflection.Assembly assembly)
    {
        foreach (Type type in assembly.GetExportedTypes())
        {
            foreach (System.Reflection.MethodInfo method in type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
            { externalFunctions.AddExternalFunction(method); }
        }
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

    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    static ExternalFunctionSimple AddExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, System.Reflection.MethodInfo method)
        => functions.AddExternalFunction(functions.GenerateId(method.Name), method);

    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    static ExternalFunctionSimple AddExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, int id, System.Reflection.MethodInfo method)
    {
        if (!method.IsStatic)
        { throw new InternalException($"Only static functions can be added as an external function"); }

        ImmutableArray<RuntimeType> parameterTypes = GetTypes(method.GetParameters()).ToImmutableArray();

        ExternalFunctionSimple function;

        if (method.ReturnType != typeof(void))
        {
            function = new((sender, parameters) =>
            {
                ImmutableArray<object?> parameterValues = GetValues(parameters);
                object? returnValue = method.Invoke(null, parameterValues.ToArray());
                if (returnValue is null)
                { return DataItem.Null; }
                else
                { return DataItem.GetValue(returnValue); }
            }, id, method.Name, parameterTypes, ExternalFunctionBase.DefaultFlags);
        }
        else
        {
            function = new((sender, parameters) =>
            {
                object?[] parameterValues = GetValues(parameters).ToArray();
                method.Invoke(null, parameterValues);
            }, id, method.Name, parameterTypes, ExternalFunctionBase.DefaultFlags);
        }

        functions.AddExternalFunction(function);
        return function;
    }

    public static void AddManagedExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, Action<ImmutableArray<DataItem>, ExternalFunctionManaged> callback, ExternalFunctionCheckFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(new ExternalFunctionManaged(callback, functions.GenerateId(name), name, parameterTypes, flags));

    public static void AddManagedExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, Action<ImmutableArray<DataItem>, ExternalFunctionManaged> callback, ExternalFunctionCheckFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(new ExternalFunctionManaged(callback, id, name, parameterTypes, flags));

    public static void AddSimpleExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, Func<BytecodeProcessor, ImmutableArray<DataItem>, DataItem> callback, ExternalFunctionCheckFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(new ExternalFunctionSimple(callback, id, name, parameterTypes, flags));

    public static void AddSimpleExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, Func<BytecodeProcessor, ImmutableArray<DataItem>, DataItem> callback, ExternalFunctionCheckFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(new ExternalFunctionSimple(callback, functions.GenerateId(name), name, parameterTypes, flags));

    public static void AddSimpleExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, ImmutableArray<RuntimeType> parameterTypes, Action<BytecodeProcessor, ImmutableArray<DataItem>> callback, ExternalFunctionCheckFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(new ExternalFunctionSimple(callback, id, name, parameterTypes, flags));

    public static void AddSimpleExternalFunction(this Dictionary<int, ExternalFunctionBase> functions, string? name, ImmutableArray<RuntimeType> parameterTypes, Action<BytecodeProcessor, ImmutableArray<DataItem>> callback, ExternalFunctionCheckFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(new ExternalFunctionSimple(callback, functions.GenerateId(name), name, parameterTypes, flags));

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
    public static void AddExternalFunction<T0>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0?> callback)
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0?> callback)
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0?, T1?> callback)
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0?, T1?> callback)
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0?, T1?, T2?> callback)
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0?, T1?, T2?> callback)
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0?, T1?, T2?, T3?> callback)
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0?, T1?, T2?, T3?> callback)
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0?, T1?, T2?, T3?, T4?> callback)
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0?, T1?, T2?, T3?, T4?> callback)
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3, T4>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]),
                GetValue<T4>(sender, args[4]));
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<int, ExternalFunctionBase> functions, string name, Action<T0?, T1?, T2?, T3?, T4?, T5?> callback)
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Action<T0?, T1?, T2?, T3?, T4?, T5?> callback)
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3, T4, T5>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]),
                GetValue<T4>(sender, args[4]),
                GetValue<T5>(sender, args[5]));
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

            return DataItem.GetValue(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0?, TResult> callback)
        where TResult : notnull
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0?, TResult> callback)
        where TResult : notnull
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]));

            return DataItem.GetValue(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0?, T1?, TResult> callback)
        where TResult : notnull
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0?, T1?, TResult> callback)
        where TResult : notnull
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]));

            return DataItem.GetValue(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0?, T1?, T2?, TResult> callback)
        where TResult : notnull
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0?, T1?, T2?, TResult> callback)
        where TResult : notnull
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]));

            return DataItem.GetValue(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0?, T1?, T2?, T3?, TResult> callback)
        where TResult : notnull
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0?, T1?, T2?, T3?, TResult> callback)
        where TResult : notnull
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]));

            return DataItem.GetValue(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0?, T1?, T2?, T3?, T4?, TResult> callback)
        where TResult : notnull
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0?, T1?, T2?, T3?, T4?, TResult> callback)
        where TResult : notnull
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3, T4>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]),
                GetValue<T4>(sender, args[4]));

            return DataItem.GetValue(result);
        });
    }

    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5, TResult>(this Dictionary<int, ExternalFunctionBase> functions, string? name, Func<T0?, T1?, T2?, T3?, T4?, T5?, TResult> callback)
        where TResult : notnull
        => functions.AddExternalFunction(functions.GenerateId(name), name, callback);
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5, TResult>(this Dictionary<int, ExternalFunctionBase> functions, int id, string? name, Func<T0?, T1?, T2?, T3?, T4?, T5?, TResult> callback)
        where TResult : notnull
    {
        ImmutableArray<RuntimeType> types = GetTypes<T0, T1, T2, T3, T4, T5>();

        functions.AddSimpleExternalFunction(id, name, types, (sender, args) =>
        {
            CheckParameters(id, name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]),
                GetValue<T4>(sender, args[4]),
                GetValue<T5>(sender, args[5]));

            return DataItem.GetValue(result);
        });
    }

    public static ImmutableArray<object?> GetValues(ImmutableArray<DataItem> values)
    {
        object?[] result = new object[values.Length];
        for (int i = 0; i < values.Length; i++)
        { result[i] = values[i].GetValue(); }
        return ImmutableArray.Create(result);
    }

    /// <exception cref="InvalidCastException"/>
    static T? GetValue<T>(BytecodeProcessor bytecodeProcessor, DataItem data)
    {
        if (typeof(T) == typeof(string))
        { return (T?)(object?)HeapUtils.GetString(bytecodeProcessor.Memory, (int)data); }

        return data.ToType<T>();
    }

    public static void SetInterpreter(this IReadOnlyDictionary<int, ExternalFunctionBase> functions, BytecodeProcessor interpreter)
    {
        foreach (KeyValuePair<int, ExternalFunctionBase> item in functions)
        { item.Value.BytecodeInterpreter = interpreter; }
    }

    #endregion

    /// <exception cref="RuntimeException"/>
    static void CheckParameters(int id, string? functionName, ImmutableArray<RuntimeType> required, ImmutableArray<DataItem> passed)
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

    static IEnumerable<RuntimeType> GetTypes(IEnumerable<System.Reflection.ParameterInfo> parameters)
        => parameters.Select(v => GetType(v.ParameterType).Convert());

    static Compiler.BasicType GetType(Type type)
    {
        if (type == typeof(byte)) return Compiler.BasicType.Byte;
        else if (type == typeof(int)) return Compiler.BasicType.Integer;
        else if (type == typeof(float)) return Compiler.BasicType.Float;
        else if (type == typeof(float)) return Compiler.BasicType.Float;
        else if (type == typeof(void)) return Compiler.BasicType.Void;
        else throw new InternalException($"Unknown type {type.FullName}");
    }

    #endregion
}
