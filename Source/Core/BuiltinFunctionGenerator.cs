using System;
using System.Collections.Generic;

namespace ProgrammingLanguage.Core
{
    using ProgrammingLanguage.Bytecode;
    using ProgrammingLanguage.Errors;

    public class BuiltinFunction : BBCode.Compiler.IHaveKey<string>
    {
        public readonly BBCode.Compiler.Type[] ParameterTypes;
        public readonly string Name;

        public string Key => Name;

        public int ParameterCount => ParameterTypes.Length;
        public readonly bool ReturnSomething;

        internal BytecodeInterpreter BytecodeInterpreter;

        protected Func<DataItem[], DataItem> callback;

        protected BuiltinFunction(string name, BBCode.Compiler.Type[] parameters, bool returnSomething)
        {
            this.Name = name;
            this.ParameterTypes = parameters;
            this.ReturnSomething = returnSomething;
        }

        /// <param name="callback">Callback when the interpreter process this function</param>
        public BuiltinFunction(Action<DataItem[]> callback, string name, BBCode.Compiler.Type[] parameters)
                 : this(name, parameters, false)
        {
            this.callback = new Func<DataItem[], DataItem>((p) =>
            {
                callback?.Invoke(p);
                return DataItem.Null;
            });
        }

        /// <param name="callback">Callback when the interpreter process this function</param>
        public BuiltinFunction(Func<DataItem[], DataItem> callback, string name, BBCode.Compiler.Type[] parameters)
                 : this(name, parameters, true)
        {
            this.callback = callback;
        }

        /// <exception cref="InternalException"></exception>
        public DataItem Callback(DataItem[] parameters)
        {
            if (callback == null)
            { throw new InternalException("Callback is null"); }

            return callback.Invoke(parameters);
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
    }

    public class ManagedBuiltinFunction : BuiltinFunction, IReturnValueConsumer
    {
        public delegate void ReturnEvent(DataItem returnValue);
        public ReturnEvent OnReturn;

        /// <param name="callback">Callback when the interpreter process this function</param>
        public ManagedBuiltinFunction(Action<DataItem[], ManagedBuiltinFunction> callback, string name, BBCode.Compiler.Type[] parameters)
                 : base(name, parameters, true)
        {
            base.callback = new Func<DataItem[], DataItem>((p) =>
            {
                callback?.Invoke(p, this);
                return DataItem.Null;
            });
        }

        public void Return(DataItem returnValue) => OnReturn?.Invoke(returnValue);
    }

    public interface IReturnValueConsumer
    {
        public void Return(DataItem returnValue);
    }

    public static class BuiltinFunctionGenerator
    {
        #region AddBuiltinFunction()

        /// <exception cref="InternalException"></exception>
        /// <exception cref="RuntimeException"></exception>
        public static BuiltinFunction AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, System.Reflection.MethodInfo method)
        {
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            BBCode.Compiler.Type[] parameterTypes = new BBCode.Compiler.Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                System.Reflection.ParameterInfo parameter = parameters[i];
                Type paramType = parameter.ParameterType;
                if (paramType == typeof(int))
                {
                    parameterTypes[i] = BBCode.Compiler.Type.INT;
                }
                else if (paramType == typeof(float))
                {
                    parameterTypes[i] = BBCode.Compiler.Type.FLOAT;
                }
                else
                {
                    throw new InternalException($"Unknown type {paramType.FullName}");
                }
            }

            bool returnSomething = method.ReturnType != typeof(void);

            BuiltinFunction function = new((ps) =>
            {
                if (ps.Length != parameters.Length)
                { throw new RuntimeException($"Wrong number of parameters passed to built-in function {method.Name} ({ps.Length}): expected {parameters.Length}"); }

                var objs = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];
                    if (p.type.Convert() != BBCode.Compiler.Type.INT &&
                        p.type.Convert() != BBCode.Compiler.Type.FLOAT)
                    { throw new RuntimeException($"Invalid parameter type {p.type.ToString().ToLower()}"); }

                    if (p.type.Convert() != parameterTypes[i])
                    { throw new RuntimeException($"Invalid parameter type {p.type.ToString().ToLower()}: expected {parameterTypes[i].ToString().ToLower()}"); }

                    objs[i] = p.Value();
                }

                method.Invoke(null, objs);
            }, method.Name, parameterTypes);

            if (!builtinFunctions.ContainsKey(method.Name))
            {
                builtinFunctions.Add(method.Name, function);
            }
            else
            {
                builtinFunctions[method.Name] = function;
                Output.Output.Warning($"The built-in function '{method.Name}' is already defined, so I'll override it");
            }

            return function;
        }

        public static void AddManagedBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, BBCode.Compiler.Type[] parameterTypes, Action<DataItem[], ManagedBuiltinFunction> callback)
        {
            ManagedBuiltinFunction function = new(callback, name, parameterTypes);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Output.Warning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }
        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, BBCode.Compiler.Type[] parameterTypes, Func<DataItem[], DataItem> callback)
        {
            BuiltinFunction function = new(callback, name, parameterTypes);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Output.Warning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }
        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<DataItem> callback)
        {
            BuiltinFunction function = new(p =>
            {
                return callback.Invoke();
            }, name, Array.Empty<BBCode.Compiler.Type>());

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Output.Warning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }
        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, BBCode.Compiler.Type[] parameterTypes, Action<DataItem[]> callback)
        {
            BuiltinFunction function = new(callback, name, parameterTypes);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Output.Warning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }

        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action callback)
        {
            var types = Array.Empty<BBCode.Compiler.Type>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke();
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddBuiltinFunction<T0>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0> callback)
        {
            var types = GetTypes<T0>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(args[0]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddBuiltinFunction<T0, T1>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0, T1> callback)
        {
            var types = GetTypes<T0, T1>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);
                callback?.Invoke(
                    GetValue<T0>(args[0]),
                    GetValue<T1>(args[1]));
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddBuiltinFunction<T0, T1, T2>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0, T1, T2> callback)
        {
            var types = GetTypes<T0, T1, T2>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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
        public static void AddBuiltinFunction<T0, T1, T2, T3>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0, T1, T2, T3> callback)
        {
            var types = GetTypes<T0, T1, T2, T3>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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
        public static void AddBuiltinFunction<T0, T1, T2, T3, T4>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0, T1, T2, T3, T4> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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
        public static void AddBuiltinFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0, T1, T2, T3, T4, T5> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4, T5>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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

        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<object> callback)
        {
            var types = Array.Empty<BBCode.Compiler.Type>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                Array.Reverse(args);
                CheckParameters(name, types, args);

                object resultData = callback?.Invoke();
                return GetValue(resultData, $"{name}() result");
            });
        }
        /// <exception cref="NotImplementedException"/>
        public static void AddBuiltinFunction<T0>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, object> callback)
        {
            var types = GetTypes<T0>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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
        public static void AddBuiltinFunction<T0, T1>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, T1, object> callback)
        {
            var types = GetTypes<T0, T1>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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
        public static void AddBuiltinFunction<T0, T1, T2>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, T1, T2, object> callback)
        {
            var types = GetTypes<T0, T1, T2>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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
        public static void AddBuiltinFunction<T0, T1, T2, T3>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, T1, T2, T3, object> callback)
        {
            var types = GetTypes<T0, T1, T2, T3>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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
        public static void AddBuiltinFunction<T0, T1, T2, T3, T4>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, T1, T2, T3, T4, object> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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
        public static void AddBuiltinFunction<T0, T1, T2, T3, T4, T5>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, T1, T2, T3, T4, T5, object> callback)
        {
            var types = GetTypes<T0, T1, T2, T3, T4, T5>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
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

        public static void SetInterpreter(this Dictionary<string, BuiltinFunction> builtinFunctions, BytecodeInterpreter interpreter)
        {
            foreach (KeyValuePair<string, BuiltinFunction> item in builtinFunctions)
            { item.Value.BytecodeInterpreter = interpreter; }
        }

        #endregion

        /// <exception cref="RuntimeException"/>
        static void CheckParameters(string functionName, BBCode.Compiler.Type[] requied, DataItem[] passed)
        {
            if (passed.Length != requied.Length) throw new RuntimeException($"Wrong number of parameters passed to builtin function '{functionName}' ({passed.Length}) wich requies {requied.Length}");

            /*
            for (int i = 0; i < requied.Length; i++)
            {
                if (passed[i].type != requied[i]) throw new RuntimeException($"Wrong type of parameter passed to builtin function '{functionName}'. Parameter index: {i} Requied type: {requied[i].ToString().ToLower()} Passed type: {passed[i].type.ToString().ToLower()}");
            }
            */
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

        #endregion
    }
}
