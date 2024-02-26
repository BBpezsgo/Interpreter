global using ExternalFunctionCollection = System.Collections.Generic.Dictionary<string, LanguageCore.Runtime.ExternalFunctionBase>;
global using ExternalFunctionReadonlyCollection = System.Collections.Generic.IReadOnlyDictionary<string, LanguageCore.Runtime.ExternalFunctionBase>;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace LanguageCore.Runtime;

[Flags]
public enum ExternalFunctionFlags : byte
{
    CheckParamLength = 1,
    CheckParamType = 2,
}

public abstract class ExternalFunctionBase : ISimpleReadable
{
    public readonly Compiler.Type[] ParameterTypes;
    public readonly string Name;

    public int ParameterCount => ParameterTypes.Length;
    public readonly bool ReturnSomething;

    public BytecodeInterpreter? BytecodeInterpreter;

    public readonly ExternalFunctionFlags Flags;

    public bool CheckParameterLength => ((byte)Flags & (byte)ExternalFunctionFlags.CheckParamLength) != 0;
    public bool CheckParameterType => ((byte)Flags & (byte)ExternalFunctionFlags.CheckParamType) != 0;

    public const ExternalFunctionFlags DefaultFlags = ExternalFunctionFlags.CheckParamLength | ExternalFunctionFlags.CheckParamType;

    protected ExternalFunctionBase(string name, Compiler.Type[] parameters, bool returnSomething, ExternalFunctionFlags flags)
    {
        this.Name = name;
        this.ParameterTypes = parameters;
        this.ReturnSomething = returnSomething;
        this.Flags = flags;
    }

    protected void BeforeCallback(DataItem[] parameters)
    {
        if (CheckParameterLength && parameters.Length != ParameterTypes.Length)
        { throw new RuntimeException($"Wrong number of parameters passed to external function {Name} ({parameters.Length}): expected {ParameterTypes.Length}"); }

        if (CheckParameterType)
        { ExternalFunctionGenerator.CheckTypes(parameters, ParameterTypes); }
    }

    public string ToReadable()
    {
        StringBuilder result = new();
        result.Append(Name);
        result.Append('(');
        for (int i = 0; i < ParameterTypes.Length; i++)
        {
            if (i > 0) result.Append(", ");
            result.Append(ParameterTypes[i]);
        }
        result.Append(')');
        return result.ToString();
    }
}

public class ExternalFunctionSimple : ExternalFunctionBase
{
    protected Func<BytecodeProcessor, DataItem[], DataItem> callback;

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionSimple(Action<BytecodeProcessor, DataItem[]> callback, string name, Compiler.Type[] parameters, ExternalFunctionFlags flags)
             : base(name, parameters, false, flags)
    {
        this.callback = (sender, v) =>
        {
            callback?.Invoke(sender, v);
            return DataItem.Null;
        };
    }

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionSimple(Func<BytecodeProcessor, DataItem[], DataItem> callback, string name, Compiler.Type[] parameters, ExternalFunctionFlags flags)
             : base(name, parameters, true, flags)
    {
        this.callback = callback;
    }

    /// <exception cref="InternalException"></exception>
    public DataItem Call(BytecodeProcessor sender, DataItem[] parameters)
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
    readonly Func<DataItem[], DataItem> callback;

    /// <param name="callback">Callback when the interpreter process this function</param>
    public ExternalFunctionManaged(Action<DataItem[], ExternalFunctionManaged> callback, string name, Compiler.Type[] parameters, ExternalFunctionFlags flags)
             : base(name, parameters, true, flags)
    {
        this.callback = new Func<DataItem[], DataItem>((p) =>
        {
            callback?.Invoke(p, this);
            return DataItem.Null;
        });
    }

    /// <exception cref="InternalException"></exception>
    public void Callback(DataItem[] parameters)
    {
        base.BeforeCallback(parameters);
        callback.Invoke(parameters);
    }
}

public static unsafe class ExternalFunctionGenerator
{
    [RequiresUnreferencedCode("Loading Assembly")]
    public static void LoadAssembly(this ExternalFunctionCollection externalFunctions, string path)
        => ExternalFunctionGenerator.LoadAssembly(externalFunctions, System.Reflection.Assembly.LoadFile(path));

    [RequiresUnreferencedCode("Loading Assembly")]
    public static void LoadAssembly(this ExternalFunctionCollection externalFunctions, System.Reflection.Assembly assembly)
    {
        Type[] exportedTypes = assembly.GetExportedTypes();

        foreach (Type type in exportedTypes)
        {
            System.Reflection.MethodInfo[] methods = type.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            foreach (System.Reflection.MethodInfo method in methods)
            { externalFunctions.AddExternalFunction(method); }
        }
    }

    #region AddExternalFunction()

    /// <exception cref="InternalException"/>
    /// <exception cref="RuntimeException"/>
    static ExternalFunctionSimple AddExternalFunction(this ExternalFunctionCollection functions, System.Reflection.MethodInfo method)
    {
        if (!method.IsStatic)
        { throw new InternalException($"Only static functions can be added as an external function"); }

        Compiler.Type[] parameterTypes = GetTypes(method.GetParameters());

        ExternalFunctionSimple function;

        if (method.ReturnType != typeof(void))
        {
            function = new((sender, parameters) =>
            {
                object?[] parameterValues = GetValues(parameters);
                object? returnValue = method.Invoke(null, parameterValues);
                if (returnValue is null)
                { return DataItem.Null; }
                else
                { return DataItem.GetValue(returnValue); }
            }, method.Name, parameterTypes, ExternalFunctionBase.DefaultFlags);
        }
        else
        {
            function = new((sender, parameters) =>
            {
                object?[] parameterValues = GetValues(parameters);
                method.Invoke(null, parameterValues);
            }, method.Name, parameterTypes, ExternalFunctionBase.DefaultFlags);
        }

        functions.AddExternalFunction(method.Name, function);
        return function;
    }

    public static void AddManagedExternalFunction(this ExternalFunctionCollection functions, string name, Compiler.Type[] parameterTypes, Action<DataItem[], ExternalFunctionManaged> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(name, new ExternalFunctionManaged(callback, name, parameterTypes, flags));

    public static void AddSimpleExternalFunction(this ExternalFunctionCollection functions, string name, Compiler.Type[] parameterTypes, Func<BytecodeProcessor, DataItem[], DataItem> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(name, new ExternalFunctionSimple(callback, name, parameterTypes, flags));
    public static void AddSimpleExternalFunction(this ExternalFunctionCollection functions, string name, Compiler.Type[] parameterTypes, Action<BytecodeProcessor, DataItem[]> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
        => functions.AddExternalFunction(name, new ExternalFunctionSimple(callback, name, parameterTypes, flags));

    static void AddExternalFunction(this ExternalFunctionCollection functions, string name, ExternalFunctionBase function)
    {
        if (!functions.TryAdd(name, function))
        {
            functions[name] = function;
            Output.LogWarning($"External function function \"{name}\" is already defined, so I'll override it");
        }
    }

    public static void AddExternalFunction(this ExternalFunctionCollection functions, string name, Action callback)
    {
        Compiler.Type[] types = Array.Empty<Compiler.Type>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            callback?.Invoke();
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0>(this ExternalFunctionCollection functions, string name, Action<T0?> callback)
    {
        Compiler.Type[] types = GetTypes<T0>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]));
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1>(this ExternalFunctionCollection functions, string name, Action<T0?, T1?> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]));
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2>(this ExternalFunctionCollection functions, string name, Action<T0?, T1?, T2?> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1, T2>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]));
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3>(this ExternalFunctionCollection functions, string name, Action<T0?, T1?, T2?, T3?> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1, T2, T3>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]));
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4>(this ExternalFunctionCollection functions, string name, Action<T0?, T1?, T2?, T3?, T4?> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1, T2, T3, T4>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            callback?.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]),
                GetValue<T4>(sender, args[4]));
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this ExternalFunctionCollection functions, string name, Action<T0?, T1?, T2?, T3?, T4?, T5?> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1, T2, T3, T4, T5>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
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
    public static void AddExternalFunction<TResult>(this ExternalFunctionCollection functions, string name, Func<TResult> callback)
    {
        Compiler.Type[] types = Array.Empty<Compiler.Type>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            TResult result = callback.Invoke()!;

            return DataItem.GetValue(result);
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, TResult>(this ExternalFunctionCollection functions, string name, Func<T0?, TResult> callback)
    {
        Compiler.Type[] types = GetTypes<T0>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]))!;

            return DataItem.GetValue(result);
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, TResult>(this ExternalFunctionCollection functions, string name, Func<T0?, T1?, TResult> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]))!;

            return DataItem.GetValue(result);
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, TResult>(this ExternalFunctionCollection functions, string name, Func<T0?, T1?, T2?, TResult> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1, T2>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]))!;

            return DataItem.GetValue(result);
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, TResult>(this ExternalFunctionCollection functions, string name, Func<T0?, T1?, T2?, T3?, TResult> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1, T2, T3>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]))!;

            return DataItem.GetValue(result);
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, TResult>(this ExternalFunctionCollection functions, string name, Func<T0?, T1?, T2?, T3?, T4?, TResult> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1, T2, T3, T4>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]),
                GetValue<T4>(sender, args[4]))!;

            return DataItem.GetValue(result);
        });
    }
    /// <exception cref="NotImplementedException"/>
    public static void AddExternalFunction<T0, T1, T2, T3, T4, T5, TResult>(this ExternalFunctionCollection functions, string name, Func<T0?, T1?, T2?, T3?, T4?, T5?, TResult> callback)
    {
        Compiler.Type[] types = GetTypes<T0, T1, T2, T3, T4, T5>();

        functions.AddSimpleExternalFunction(name, types, (sender, args) =>
        {
            CheckParameters(name, types, args);
            TResult result = callback.Invoke(
                GetValue<T0>(sender, args[0]),
                GetValue<T1>(sender, args[1]),
                GetValue<T2>(sender, args[2]),
                GetValue<T3>(sender, args[3]),
                GetValue<T4>(sender, args[4]),
                GetValue<T5>(sender, args[5]))!;

            return DataItem.GetValue(result);
        });
    }

    /// <exception cref="RuntimeException"/>
    public static void CheckTypes(DataItem[] values, RuntimeType[] types)
    {
        int n = Math.Min(values.Length, types.Length);
        for (int i = 0; i < n; i++)
        {
            if (values[i].Type != types[i])
            {
                throw new RuntimeException($"Invalid parameter type {values[i].Type}: expected {types[i]}");
            }
        }
    }

    /// <exception cref="RuntimeException"/>
    public static void CheckTypes(DataItem[] values, Compiler.Type[] types)
    {
        int n = Math.Min(values.Length, types.Length);
        for (int i = 0; i < n; i++)
        {
            if (values[i].Type.Convert() != types[i])
            {
                throw new RuntimeException($"Invalid parameter type {values[i].Type}: expected {types[i]}");
            }
        }
    }

    public static object?[] GetValues(DataItem[] values)
    {
        object?[] result = new object?[values.Length];
        for (int i = 0; i < values.Length; i++)
        { result[i] = values[i].GetValue(); }
        return result;
    }

    /// <exception cref="InvalidCastException"/>
    static T? GetValue<T>(BytecodeProcessor bytecodeProcessor, DataItem data)
    {
        if (typeof(T) == typeof(string))
        { return (T?)(object?)bytecodeProcessor.Memory.Heap.GetStringByPointer(data.ToInt32(null)); }

        return data.ToType<T>(null);
    }

    public static void SetInterpreter(this ExternalFunctionCollection functions, BytecodeInterpreter interpreter)
    {
        foreach (KeyValuePair<string, ExternalFunctionBase> item in functions)
        { item.Value.BytecodeInterpreter = interpreter; }
    }

    #endregion

    /// <exception cref="RuntimeException"/>
    static void CheckParameters(string functionName, Compiler.Type[] required, DataItem[] passed)
    {
        if (passed.Length != required.Length) throw new RuntimeException($"Wrong number of parameters passed to external function '{functionName}' ({passed.Length}) which requires {required.Length}");
    }

    #region GetTypes<>()

    /// <exception cref="NotImplementedException"/>
    static Compiler.Type[] GetTypes<T0>() => new Compiler.Type[1]
    {
        GetType<T0>(),
    };
    /// <exception cref="NotImplementedException"/>
    static Compiler.Type[] GetTypes<T0, T1>() => new Compiler.Type[2]
    {
        GetType<T0>(),
        GetType<T1>(),
    };
    /// <exception cref="NotImplementedException"/>
    static Compiler.Type[] GetTypes<T0, T1, T2>() => new Compiler.Type[3]
    {
        GetType<T0>(),
        GetType<T1>(),
        GetType<T2>(),
    };
    /// <exception cref="NotImplementedException"/>
    static Compiler.Type[] GetTypes<T0, T1, T2, T3>() => new Compiler.Type[4]
    {
        GetType<T0>(),
        GetType<T1>(),
        GetType<T2>(),
        GetType<T3>(),
    };
    /// <exception cref="NotImplementedException"/>
    static Compiler.Type[] GetTypes<T0, T1, T2, T3, T4>() => new Compiler.Type[5]
    {
        GetType<T0>(),
        GetType<T1>(),
        GetType<T2>(),
        GetType<T3>(),
        GetType<T4>(),
    };
    /// <exception cref="NotImplementedException"/>
    static Compiler.Type[] GetTypes<T0, T1, T2, T3, T4, T5>() => new Compiler.Type[6]
    {
        GetType<T0>(),
        GetType<T1>(),
        GetType<T2>(),
        GetType<T3>(),
        GetType<T4>(),
        GetType<T5>(),
    };

    /// <exception cref="NotImplementedException"/>
    static Compiler.Type GetType<T>()
    {
        Type type_ = typeof(T);

        if (type_ == typeof(byte))
        { return Compiler.Type.Byte; }

        if (type_ == typeof(int))
        { return Compiler.Type.Integer; }

        if (type_ == typeof(float))
        { return Compiler.Type.Float; }

        if (type_ == typeof(char))
        { return Compiler.Type.Char; }

        if (type_.IsClass)
        { return Compiler.Type.Integer; }

        if (type_ == typeof(uint))
        { return Compiler.Type.Integer; }

        if (type_ == typeof(IntPtr))
        { return Compiler.Type.Integer; }

        if (type_ == typeof(UIntPtr))
        { return Compiler.Type.Integer; }

        throw new NotImplementedException($"Type conversion for type {typeof(T)} not implemented");
    }

    static Compiler.Type[] GetTypes(params System.Reflection.ParameterInfo[] parameters)
    {
        Compiler.Type[] result = new Compiler.Type[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        { result[i] = GetType(parameters[i].ParameterType); }
        return result;
    }

    /*
    static Compiler.Type[] GetTypes(params Type[] types)
    {
        Compiler.Type[] result = new Compiler.Type[types.Length];
        for (int i = 0; i < types.Length; i++)
        { result[i] = GetType(types[i]); }
        return result;
    }
    */

    static Compiler.Type GetType(Type type)
    {
        if (type == typeof(byte))
        { return Compiler.Type.Byte; }

        if (type == typeof(int))
        { return Compiler.Type.Integer; }

        if (type == typeof(float))
        { return Compiler.Type.Float; }

        if (type == typeof(float))
        { return Compiler.Type.Float; }

        if (type == typeof(void))
        { return Compiler.Type.Void; }

        throw new InternalException($"Unknown type {type.FullName}");
    }

    #endregion
}
