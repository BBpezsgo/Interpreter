using System;
using System.Collections.Generic;

namespace LanguageCore.Runtime
{

    [Flags]
    public enum ExternalFunctionFlags : byte
    {
        CheckParamLength = 1,
        CheckParamType = 2,
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
            { ExternalFunctionGenerator.CheckTypes(parameters, ParameterTypes); }
        }
    }

    public class ExternalFunctionSimple : ExternalFunctionBase
    {
        protected Func<BytecodeProcessor, DataItem[], DataItem> callback;

        /// <param name="callback">Callback when the interpreter process this function</param>
        public ExternalFunctionSimple(Action<BytecodeProcessor, DataItem[]> callback, string name, BBCode.Compiler.Type[] parameters, ExternalFunctionFlags flags)
                 : base(name, parameters, false, flags)
        {
            this.callback = (sender, v) =>
            {
                callback?.Invoke(sender, v);
                return DataItem.Null;
            };
        }

        /// <param name="callback">Callback when the interpreter process this function</param>
        public ExternalFunctionSimple(Func<BytecodeProcessor, DataItem[], DataItem> callback, string name, BBCode.Compiler.Type[] parameters, ExternalFunctionFlags flags)
                 : base(name, parameters, true, flags)
        {
            this.callback = callback;
        }

        /// <exception cref="InternalException"></exception>
        public DataItem Callback(BytecodeProcessor sender, DataItem[] parameters)
        {
            base.BeforeCallback(parameters);

            if (callback == null)
            { throw new InternalException("Callback is null"); }

            return callback.Invoke(sender, parameters);
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

        /// <exception cref="InternalException"/>
        /// <exception cref="RuntimeException"/>
        public static ExternalFunctionSimple AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, System.Reflection.MethodInfo method)
        {
            if (!method.IsStatic)
            { throw new InternalException($"Only static functions can be added as an external function"); }

            BBCode.Compiler.Type[] parameterTypes = GetTypes(method.GetParameters());

            ExternalFunctionSimple function;

            if (method.ReturnType != typeof(void))
            {
                function = new((sender, parameters) =>
                {
                    object[] parameterValues = GetValues(parameters);
                    object returnValue = method.Invoke(null, parameterValues);
                    if (returnValue is null)
                    { return DataItem.Null; }
                    else
                    { return DataItem.GetValue(returnValue, $"{method.Name}() result"); }
                }, method.Name, parameterTypes, ExternalFunctionBase.DefaultFlags);
            }
            else
            {
                function = new((sender, parameters) =>
                {
                    object[] parameterValues = GetValues(parameters);
                    method.Invoke(null, parameterValues);
                }, method.Name, parameterTypes, ExternalFunctionBase.DefaultFlags);
            }

            functions.AddExternalFunction(method.Name, function);
            return function;
        }

        public static void AddManagedExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, BBCode.Compiler.Type[] parameterTypes, Action<DataItem[], ExternalFunctionManaged> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
            => functions.AddExternalFunction(name, new ExternalFunctionManaged(callback, name, parameterTypes, flags));

        public static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, BBCode.Compiler.Type[] parameterTypes, Func<BytecodeProcessor, DataItem[], DataItem> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
            => functions.AddExternalFunction(name, new ExternalFunctionSimple(callback, name, parameterTypes, flags));
        public static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, BBCode.Compiler.Type[] parameterTypes, Action<BytecodeProcessor, DataItem[]> callback, ExternalFunctionFlags flags = ExternalFunctionBase.DefaultFlags)
            => functions.AddExternalFunction(name, new ExternalFunctionSimple(callback, name, parameterTypes, flags));

        static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, ExternalFunctionBase function)
        {
            if (!functions.ContainsKey(name))
            {
                functions.Add(name, function);
            }
            else
            {
                functions[name] = function;
                Output.Warning($"External function function \"{name}\" is already defined, so I'll override it");
            }
        }

        public static void AddExternalFunction(this Dictionary<string, ExternalFunctionBase> functions, string name, Action callback)
        {
            var types = Array.Empty<BBCode.Compiler.Type>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                callback?.Invoke();
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0> callback)
        {
            var types = GetTypes<T0>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(sender, args[0]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1> callback)
        {
            var types = GetTypes<T0, T1>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(sender, args[0]),
                    GetValue<T1>(sender, args[1]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1, T2> callback)
        {
            var types = GetTypes<T0, T1, T2>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(sender, args[0]),
                    GetValue<T1>(sender, args[1]),
                    GetValue<T2>(sender, args[2]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3> callback)
        {
            var types = GetTypes<T0, T1, T2, T3>();

            functions.AddExternalFunction(name, types, (sender, args) =>
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
        public static void AddExternalFunction<T0, T1, T2, T3, T4>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3, T4> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4>();

            functions.AddExternalFunction(name, types, (sender, args) =>
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
        public static void AddExternalFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<string, ExternalFunctionBase> functions, string name, Action<T0, T1, T2, T3, T4, T5> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4, T5>();

            functions.AddExternalFunction(name, types, (sender, args) =>
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
        public static void AddExternalFunction<TResult>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<TResult> callback)
        {
            var types = Array.Empty<BBCode.Compiler.Type>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                TResult result = callback.Invoke();

                return DataItem.GetValue(result, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, TResult>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, TResult> callback)
        {
            var types = GetTypes<T0>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                TResult result = callback.Invoke(
                    GetValue<T0>(sender, args[0]));

                return DataItem.GetValue(result, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, TResult>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, TResult> callback)
        {
            var types = GetTypes<T0, T1>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                TResult result = callback.Invoke(
                    GetValue<T0>(sender, args[0]),
                    GetValue<T1>(sender, args[1]));

                return DataItem.GetValue(result, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, TResult>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, T2, TResult> callback)
        {
            var types = GetTypes<T0, T1, T2>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                TResult result = callback.Invoke(
                    GetValue<T0>(sender, args[0]),
                    GetValue<T1>(sender, args[1]),
                    GetValue<T2>(sender, args[2]));

                return DataItem.GetValue(result, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3, TResult>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, T2, T3, TResult> callback)
        {
            var types = GetTypes<T0, T1, T2, T3>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                TResult result = callback.Invoke(
                    GetValue<T0>(sender, args[0]),
                    GetValue<T1>(sender, args[1]),
                    GetValue<T2>(sender, args[2]),
                    GetValue<T3>(sender, args[3]));

                return DataItem.GetValue(result, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3, T4, TResult>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, T2, T3, T4, TResult> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                TResult result = callback.Invoke(
                    GetValue<T0>(sender, args[0]),
                    GetValue<T1>(sender, args[1]),
                    GetValue<T2>(sender, args[2]),
                    GetValue<T3>(sender, args[3]),
                    GetValue<T4>(sender, args[4]));

                return DataItem.GetValue(result, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddExternalFunction<T0, T1, T2, T3, T4, T5, TResult>(this Dictionary<string, ExternalFunctionBase> functions, string name, Func<T0, T1, T2, T3, T4, T5, TResult> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4, T5>();

            functions.AddExternalFunction(name, types, (sender, args) =>
            {
                CheckParameters(name, types, args);
                TResult result = callback.Invoke(
                    GetValue<T0>(sender, args[0]),
                    GetValue<T1>(sender, args[1]),
                    GetValue<T2>(sender, args[2]),
                    GetValue<T3>(sender, args[3]),
                    GetValue<T4>(sender, args[4]),
                    GetValue<T5>(sender, args[5]));

                return DataItem.GetValue(result, $"{name}() result");
            });
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

        /// <exception cref="RuntimeException"/>
        internal static void CheckTypes(DataItem[] values, BBCode.Compiler.Type[] types)
        {
            int n = Math.Min(values.Length, types.Length);
            for (int i = 0; i < n; i++)
            {
                if (values[i].Type.Convert() != types[i])
                {
                    throw new RuntimeException($"Invalid parameter type {values[i].Type.ToString().ToLower()}: expected {types[i].ToString().ToLower()}");
                }
            }
        }

        internal static object[] GetValues(DataItem[] values)
        {
            object[] result = new object[values.Length];
            for (int i = 0; i < values.Length; i++)
            { result[i] = values[i].GetValue(); }
            return result;
        }

        /// <exception cref="NotImplementedException"/>
        static T GetValue<T>(BytecodeProcessor bytecodeProcessor, DataItem data)
            => (T)GetValue(typeof(T), bytecodeProcessor, data);

        /// <exception cref="NotImplementedException"/>
        static object GetValue(Type type, BytecodeProcessor bytecodeProcessor, DataItem data)
        {
            if (type == typeof(byte))
            { return (byte)(data.Byte ?? (byte)0); }

            if (type == typeof(int))
            { return (int)(data.Integer ?? 0); }

            if (type == typeof(float))
            { return (float)(data.Float); }

            if (type == typeof(bool))
            { return (bool)data.Boolean; }

            if (type == typeof(char))
            { return (char)(data.Integer ?? 0); }

            if (type == typeof(string))
            { return (string)bytecodeProcessor.Memory.Heap.GetStringByPointer(data.Integer.Value); }

            if (type == typeof(uint))
            { return (uint)(data.Integer ?? 0); }

            throw new NotImplementedException($"Type conversion for type {type} not implemented");
        }

        public static void SetInterpreter(this Dictionary<string, ExternalFunctionBase> functions, BytecodeInterpreter interpreter)
        {
            foreach (KeyValuePair<string, ExternalFunctionBase> item in functions)
            { item.Value.BytecodeInterpreter = interpreter; }
        }

        #endregion

        /// <exception cref="RuntimeException"/>
        static void CheckParameters(string functionName, BBCode.Compiler.Type[] required, DataItem[] passed)
        {
            if (passed.Length != required.Length) throw new RuntimeException($"Wrong number of parameters passed to external function '{functionName}' ({passed.Length}) which requires {required.Length}");
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

            if (type_.IsClass)
            { return BBCode.Compiler.Type.INT; }

            if (type_ == typeof(uint))
            { return BBCode.Compiler.Type.INT; }

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
