using System;
using System.Collections.Generic;

namespace ProgrammingLanguage.Core
{
    using System.Linq;
    using ProgrammingLanguage.Bytecode;
    using ProgrammingLanguage.Errors;

    [Flags]
    public enum ExternalFunctionFlags : byte
    {
        CheckParamLength    = 1,
        CheckParamType      = 2,
    }

    public abstract class ExternalFunctionBase : BBCode.Compiler.IHaveKey<string>
    {
        public readonly BBCode.Compiler.Type[] ParameterTypes;
        public readonly string Name;

        public string Key => Name;

        public int ParameterCount => ParameterTypes.Length;
        public readonly bool ReturnSomething;

        internal BytecodeInterpreter BytecodeInterpreter;

        public readonly ExternalFunctionFlags Flags;

        public bool CheckParameterLength => ((byte)Flags & (byte)ExternalFunctionFlags.CheckParamLength) != 0;
        public bool CheckParameterType => ((byte)Flags & (byte)ExternalFunctionFlags.CheckParamType) != 0;

        public const ExternalFunctionFlags DefaultFlags = ExternalFunctionFlags.CheckParamLength | ExternalFunctionFlags.CheckParamType;

        protected ExternalFunctionBase(string name, BBCode.Compiler.Type[] parameters, bool returnSomething, ExternalFunctionFlags flags)
        {
            this.Name = name;
            this.ParameterTypes = parameters;
            this.ReturnSomething = returnSomething;
            this.Flags = flags;
        }

        internal object ID
        {
            get
            {
                string result = Name;
                result += "(";
                for (int j = 0; j < ParameterTypes.Length; j++)
                {
                    if (j > 0) { result += ", "; }
                    result += ParameterTypes[j].ToString().ToLower();
                }
                result += ")";
                return result;
            }
        }

        protected void BeforeCallback(DataItem[] parameters)
        {
            if (CheckParameterLength && parameters.Length != ParameterTypes.Length)
            { throw new RuntimeException($"Wrong number of parameters passed to external function {Name} ({parameters.Length}): expected {ParameterTypes.Length}"); }

            if (CheckParameterType)
            { ExternalFunctionGenerator.CheckTypes(parameters, ParameterTypes.Select(v => v.Convert()).ToArray()); }
        }
    }

    public class ExternalFunctionSimple : ExternalFunctionBase
    {
        protected Func<DataItem[], DataItem> callback;

        /// <param name="callback">Callback when the interpreter process this function</param>
        public ExternalFunctionSimple(Action<DataItem[]> callback, string name, BBCode.Compiler.Type[] parameters, ExternalFunctionFlags flags)
                 : base(name, parameters, false, flags)
        {
            this.callback = (v) =>
            {
                callback?.Invoke(v);
                return DataItem.Null;
            };
        }

        /// <param name="callback">Callback when the interpreter process this function</param>
        public ExternalFunctionSimple(Func<DataItem[], DataItem> callback, string name, BBCode.Compiler.Type[] parameters, ExternalFunctionFlags flags)
                 : base(name, parameters, true, flags)
        {
            this.callback = callback;
        }

        /// <exception cref="InternalException"></exception>
        public DataItem Callback(DataItem[] parameters)
        {
            base.BeforeCallback(parameters);

            if (callback == null)
            { throw new InternalException("Callback is null"); }

            return callback.Invoke(parameters);
        }
    }

    public class ExternalFunctionManaged : ExternalFunctionBase, IReturnValueConsumer
    {
        public delegate void ReturnEvent(DataItem returnValue);

        public ReturnEvent OnReturn;
        readonly Func<DataItem[], DataItem> callback;

        /// <param name="callback">Callback when the interpreter process this function</param>
        public ExternalFunctionManaged(Action<DataItem[], ExternalFunctionManaged> callback, string name, BBCode.Compiler.Type[] parameters, ExternalFunctionFlags flags)
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

            if (callback == null)
            { throw new InternalException("Callback is null"); }

            callback.Invoke(parameters);
        }

        public void Return(DataItem returnValue) => OnReturn?.Invoke(returnValue);
    }

    public interface IReturnValueConsumer
    {
        public void Return(DataItem returnValue);
    }

    public static class ExternalFunctionGenerator
    {
        #region AddExternalFunction()

        /// <exception cref="InternalException"></exception>
        /// <exception cref="RuntimeException"></exception>
        public static ExternalFunctionSimple AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, System.Reflection.MethodInfo method)
        {
            BBCode.Compiler.Type[] parameterTypes = GetTypes(method.GetParameters());

            bool returnSomething = method.ReturnType != typeof(void);

            ExternalFunctionSimple function;
            if (returnSomething)
            {
                function = new((parameters) =>
                {
                    object[] parameterValues = GetValues(parameters);
                    object returnValue = method.Invoke(null, parameterValues);
                    if (returnValue is null)
                    {
                        return DataItem.Null;
                    }
                    else
                    {
                        return GetValue(returnValue, $"{method.Name}() result");
                    }
                }, method.Name, parameterTypes, ExternalFunctionBase.DefaultFlags);
            }
            else
            {
                function = new((parameters) =>
                {
                    object[] parameterValues = GetValues(parameters);
                    method.Invoke(null, parameterValues);
                }, method.Name, parameterTypes, ExternalFunctionBase.DefaultFlags);
            }

            if (!functions.ContainsKey(method.Name))
            {
                functions.Add(method.Name, function);
            }
            else
            {
                functions[method.Name] = function;
                Output.Output.Warning($"External function '{method.Name}' is already defined, so I'll override it");
            }

            return function;
        }

        /// <exception cref="RuntimeException"/>
        internal static void CheckTypes(DataItem[] values, RuntimeType[] types)
        {
            int n = Math.Min(values.Length, types.Length);
            for (int i = 0; i < n; i++)
            {
                if (values[i].Type != types[i])
                {
                    throw new RuntimeException($"Invalid parameter type {values[i].Type.ToString().ToLower()}: expected {types[i].ToString().ToLower()}");
                }
            }
        }

        internal static object[] GetValues(DataItem[] values)
        {
            object[] result = new object[values.Length];
            for (int i = 0; i < values.Length; i++)
            { result[i] = values[i].Value(); }
            return result;
        }

        public static void AddManagedExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, BBCode.Compiler.Type[] parameterTypes, Action<DataItem[], ExternalFunctionManaged> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
        {
            ExternalFunctionManaged function = new(callback, name, parameterTypes, flags);

            if (!functions.ContainsKey(name))
            {
                functions.Add(name, function);
            }
            else
            {
                functions[name] = function;
                Output.Output.Warning($"External function function '{name}' is already defined, so I'll override it");
            }
        }
        public static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, BBCode.Compiler.Type[] parameterTypes, Func<DataItem[], DataItem> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
        {
            ExternalFunctionSimple function = new(callback, name, parameterTypes, flags);

            if (!functions.ContainsKey(name))
            {
                functions.Add(name, function);
            }
            else
            {
                functions[name] = function;
                Output.Output.Warning($"External function function '{name}' is already defined, so I'll override it");
            }
        }
        public static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<DataItem> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
        {
            ExternalFunctionSimple function = new(p =>
            {
                return callback.Invoke();
            }, name, Array.Empty<BBCode.Compiler.Type>(), flags);

            if (!functions.ContainsKey(name))
            {
                functions.Add(name, function);
            }
            else
            {
                functions[name] = function;
                Output.Output.Warning($"External function function '{name}' is already defined, so I'll override it");
            }
        }
        public static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, BBCode.Compiler.Type[] parameterTypes, Action<DataItem[]> callback, ExternalFunctionFlags checkParameterLength = ExternalFunctionBase.DefaultFlags)
        {
            ExternalFunctionSimple function = new(callback, name, parameterTypes, checkParameterLength);

            if (!functions.ContainsKey(name))
            {
                functions.Add(name, function);
            }
            else
            {
                functions[name] = function;
                Output.Output.Warning($"External function function '{name}' is already defined, so I'll override it");
            }
        }

        public static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, Action callback)
        {
            var types = Array.Empty<BBCode.Compiler.Type>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke();
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0> callback)
        {
            var types = GetTypes<T0>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(args[0]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1> callback)
        {
            var types = GetTypes<T0, T1>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1, T2> callback)
        {
            var types = GetTypes<T0, T1, T2>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]),
                    GetValue<T2>(args[2]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3> callback)
        {
            var types = GetTypes<T0, T1, T2, T3>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]),
                    GetValue<T2>(args[2]),
                    GetValue<T3>(args[3]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3, T4> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]),
                    GetValue<T2>(args[2]),
                    GetValue<T3>(args[3]),
                    GetValue<T4>(args[4]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3, T4, T5> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4, T5>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]),
                    GetValue<T2>(args[2]),
                    GetValue<T3>(args[3]),
                    GetValue<T4>(args[4]),
                    GetValue<T5>(args[5]));
            });
        }

        static T GetValue<T>(DataItem data)
        {
            Type type = typeof(T);

            if (type == typeof(byte))
            { return (T)(object)(data.Byte ?? (byte)0); }

            if (type == typeof(int))
            { return (T)(object)(data.Integer ?? 0); }

            if (type == typeof(float))
            { return (T)(object)(data.Float ?? 0f); }

            if (type == typeof(bool))
            { return (T)(object)((data.Integer ?? 0) != 0); }

            if (type == typeof(char))
            { return (T)(object)(char)(data.Integer ?? 0); }

            throw new NotImplementedException($"Type conversion for type {typeof(T)} not implemented");
        }

        static DataItem GetValue(object value, string tag)
        {
            if (value is null) throw new NotImplementedException($"Value is null");

            if (value is byte @byte)
            { return new DataItem(@byte, tag); }

            if (value is int @int)
            { return new DataItem(@int, tag); }

            if (value is float @float)
            { return new DataItem(@float, tag); }

            if (value is bool @bool)
            { return new DataItem(@bool, tag); }

            if (value is char @char)
            { return new DataItem(@char, tag); }

            throw new NotImplementedException($"Type conversion for type {value.GetType()} not implemented");
        }

        public static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<object> callback)
        {
            var types = Array.Empty<BBCode.Compiler.Type>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);

                object resultData = callback?.Invoke();
                return GetValue(resultData, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, object> callback)
        {
            var types = GetTypes<T0>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);

                object resultData = callback?.Invoke(
                    GetValue<T0>(args[0])
                    );

                return GetValue(resultData, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, object> callback)
        {
            var types = GetTypes<T0, T1>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);

                object resultData = callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1])
                    );

                return GetValue(resultData, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, T2, object> callback)
        {
            var types = GetTypes<T0, T1, T2>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);

                object resultData = callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]),
                    GetValue<T2>(args[2])
                    );

                return GetValue(resultData, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, T2, T3, object> callback)
        {
            var types = GetTypes<T0, T1, T2, T3>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);

                object resultData = callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]),
                    GetValue<T2>(args[2]),
                    GetValue<T3>(args[3])
                    );

                return GetValue(resultData, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, T2, T3, T4, object> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);

                object resultData = callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]),
                    GetValue<T2>(args[2]),
                    GetValue<T3>(args[3]),
                    GetValue<T4>(args[4])
                    );

                return GetValue(resultData, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, T2, T3, T4, T5, object> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4, T5>();

            functions.AddExternalFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);

                object resultData = callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]),
                    GetValue<T2>(args[2]),
                    GetValue<T3>(args[3]),
                    GetValue<T4>(args[4]),
                    GetValue<T5>(args[5])
                    );

                return GetValue(resultData, $"{name}() result");
            });
        }

        public static void SetInterpreter(this Dictionary<string, ExternalFunctionBase> functions, BytecodeInterpreter interpreter)
        {
            foreach (KeyValuePair<string, ExternalFunctionBase> item in functions)
            { item.Value.BytecodeInterpreter = interpreter; }
        }

        #endregion

        /// <exception cref="RuntimeException"/>
        static void CheckParameters(string functionName, BBCode.Compiler.Type[] requied, DataItem[] passed)
        {
            if (passed.Length != requied.Length) throw new RuntimeException($"Wrong number of parameters passed to external function '{functionName}' ({passed.Length}) wich requies {requied.Length}");
        }

        #region GetTypes<>()

        /// <exception cref="NotImplementedException"/>
        static BBCode.Compiler.Type[] GetTypes<T0>() => new BBCode.Compiler.Type[1]
        {
            GetType<T0>(),
        };
        /// <exception cref="NotImplementedException"/>
        static BBCode.Compiler.Type[] GetTypes<T0, T1>() => new BBCode.Compiler.Type[2]
        {
            GetType<T0>(),
            GetType<T1>(),
        };
        /// <exception cref="NotImplementedException"/>
        static BBCode.Compiler.Type[] GetTypes<T0, T1, T2>() => new BBCode.Compiler.Type[3]
        {
            GetType<T0>(),
            GetType<T1>(),
            GetType<T2>(),
        };
        /// <exception cref="NotImplementedException"/>
        static BBCode.Compiler.Type[] GetTypes<T0, T1, T2, T3>() => new BBCode.Compiler.Type[4]
        {
            GetType<T0>(),
            GetType<T1>(),
            GetType<T2>(),
            GetType<T3>(),
        };
        /// <exception cref="NotImplementedException"/>
        static BBCode.Compiler.Type[] GetTypes<T0, T1, T2, T3, T4>() => new BBCode.Compiler.Type[5]
        {
            GetType<T0>(),
            GetType<T1>(),
            GetType<T2>(),
            GetType<T3>(),
            GetType<T4>(),
        };
        /// <exception cref="NotImplementedException"/>
        static BBCode.Compiler.Type[] GetTypes<T0, T1, T2, T3, T4, T5>() => new BBCode.Compiler.Type[6]
        {
            GetType<T0>(),
            GetType<T1>(),
            GetType<T2>(),
            GetType<T3>(),
            GetType<T4>(),
            GetType<T5>(),
        };

        /// <exception cref="NotImplementedException"/>
        static BBCode.Compiler.Type GetType<T>()
        {
            var type_ = typeof(T);

            if (type_ == typeof(byte))
            { return BBCode.Compiler.Type.BYTE; }
            if (type_ == typeof(int))
            { return BBCode.Compiler.Type.INT; }
            if (type_ == typeof(float))
            { return BBCode.Compiler.Type.FLOAT; }
            if (type_ == typeof(char))
            { return BBCode.Compiler.Type.CHAR; }

            throw new NotImplementedException($"Type conversion for type {typeof(T)} not implemented");
        }

        static BBCode.Compiler.Type[] GetTypes(params System.Reflection.ParameterInfo[] parameters)
        {
            BBCode.Compiler.Type[] result = new BBCode.Compiler.Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            { result[i] = GetType(parameters[i].ParameterType); }
            return result;
        }

        static BBCode.Compiler.Type[] GetTypes(params Type[] types)
        {
            BBCode.Compiler.Type[] result = new BBCode.Compiler.Type[types.Length];
            for (int i = 0; i < types.Length; i++)
            { result[i] = GetType(types[i]); }
            return result;
        }

        static BBCode.Compiler.Type GetType(Type type)
        {
            if (type == typeof(byte))
            { return BBCode.Compiler.Type.BYTE; }

            if (type == typeof(int))
            { return BBCode.Compiler.Type.INT; }

            if (type == typeof(float))
            { return BBCode.Compiler.Type.FLOAT; }

            if (type == typeof(float))
            { return BBCode.Compiler.Type.FLOAT; }

            if (type == typeof(void))
            { return BBCode.Compiler.Type.VOID; }

            throw new InternalException($"Unknown type {type.FullName}");
        }

        #endregion
    }
}
