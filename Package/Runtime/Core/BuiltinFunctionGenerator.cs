using IngameCoding.BBCode;
using IngameCoding.Bytecode;
using IngameCoding.Errors;

using System;
using System.Collections.Generic;

namespace IngameCoding.Core
{
    public class BuiltinFunction
    {
        public readonly TypeToken[] ParameterTypes;

        readonly Action<DataItem[], BuiltinFunction> callback;

        public delegate void ReturnEventHandler(DataItem returnValue);
        public event ReturnEventHandler OnReturn;

        public int ParameterCount => ParameterTypes.Length;
        public bool ReturnSomething;

        internal BytecodeInterpeter BytecodeInterpeter;

        public void RaiseReturnEvent(DataItem returnValue)
        {
            OnReturn?.Invoke(returnValue);
        }

        public BuiltinFunction()
        { }

        /// <summary>
        /// Function without return value
        /// </summary>
        /// <param name="callback">Callback when the interpeter process this function</param>
        public BuiltinFunction(Action<DataItem[], BuiltinFunction> callback, TypeToken[] parameters, bool returnSomething = false)
        {
            this.ParameterTypes = parameters;
            this.callback = callback;
            this.ReturnSomething = returnSomething;
        }

        /// <summary>
        /// Function without return value
        /// </summary>
        /// <param name="callback">Callback when the interpeter process this function</param>
        public BuiltinFunction(Action<DataItem[]> callback, TypeToken[] parameters, bool returnSomething = false)
        {
            this.ParameterTypes = parameters;
            this.callback = new Action<DataItem[], BuiltinFunction>((a, b) =>
            { callback?.Invoke(a); });
            this.ReturnSomething = returnSomething;
        }

        public void Callback(DataItem[] parameters)
        {
            if (ReturnSomething)
            {
                if (callback != null)
                {
                    callback(parameters, this);
                }
                else
                {
                    throw new InternalException("Callback is null");
                }
            }
            else
            {
                callback(parameters, this);
            }
        }
    }

    public static class BuiltinFunctionGenerator
    {
        #region AddBuiltinFunction()

        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, TypeToken[] parameterTypes, Func<DataItem[], DataItem> callback)
        {
            BuiltinFunction function = new(new Action<DataItem[], BuiltinFunction>((p, self) =>
            {
                DataItem x = callback(p);
                if (self.BytecodeInterpeter == null)
                {
                    Output.Terminal.Output.LogError($"The built-in function '{name}' can not return a value: bytecode interpeter is null");
                    return;
                }
                self.BytecodeInterpeter.AddValueToStack(x);
            }), parameterTypes, true);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Terminal.Output.LogWarning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }
        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<DataItem> callback)
        {
            BuiltinFunction function = new(new Action<DataItem[], BuiltinFunction>((_, self) =>
            {
                var x = callback();
                if (self.BytecodeInterpeter == null)
                {
                    Output.Terminal.Output.LogError($"The built-in function '{name}' can not return a value: bytecode interpeter is null");
                    return;
                }
                self.BytecodeInterpeter.AddValueToStack(x);
            }), Array.Empty<TypeToken>(), true);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Terminal.Output.LogWarning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }
        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, TypeToken[] parameterTypes, Action<DataItem[]> callback, bool ReturnSomething = false)
        {
            BuiltinFunction function = new(callback, parameterTypes, ReturnSomething);

            if (!builtinFunctions.ContainsKey(name))
            {
                builtinFunctions.Add(name, function);
            }
            else
            {
                builtinFunctions[name] = function;
                Output.Terminal.Output.LogWarning($"The built-in function '{name}' is already defined, so I'll override it");
            }
        }

        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action callback)
        {
            var types = Array.Empty<TypeToken>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                CheckParameters(name, types, args);
                callback?.Invoke();
            });
        }
        public static void AddBuiltinFunction<T0>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0> callback)
        {
            var types = GetTypes<T0>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                CheckParameters(name, types, args);
                callback?.Invoke((T0)args[0].Value());
            });
        }
        public static void AddBuiltinFunction<T0, T1>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0, T1> callback)
        {
            var types = GetTypes<T0, T1>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                CheckParameters(name, types, args);
                callback?.Invoke((T0)args[0].Value(), (T1)args[1].Value());
            });
        }
        public static void AddBuiltinFunction<T0, T1, T2>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Action<T0, T1, T2> callback)
        {
            var types = GetTypes<T0, T1, T2>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                CheckParameters(name, types, args);
                callback?.Invoke((T0)args[0].Value(), (T1)args[1].Value(), (T2)args[2].Value());
            });
        }

        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<object> callback)
        {
            var types = Array.Empty<TypeToken>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                CheckParameters(name, types, args);
                return new DataItem(callback?.Invoke(), $"{name}() result");
            });
        }
        public static void AddBuiltinFunction<T0>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, object> callback)
        {
            var types = GetTypes<T0>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                CheckParameters(name, types, args);
                return new DataItem(callback?.Invoke((T0)args[0].Value()), $"{name}() result");
            });
        }
        public static void AddBuiltinFunction<T0, T1>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, T1, object> callback)
        {
            var types = GetTypes<T0, T1>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                CheckParameters(name, types, args);
                return new DataItem(callback?.Invoke((T0)args[0].Value(), (T1)args[1].Value()), $"{name}() result");
            });
        }
        public static void AddBuiltinFunction<T0, T1, T2>(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, Func<T0, T1, T2, object> callback)
        {
            var types = GetTypes<T0, T1, T2>();

            builtinFunctions.AddBuiltinFunction(name, types, (args) =>
            {
                CheckParameters(name, types, args);
                return new DataItem(callback?.Invoke((T0)args[0].Value(), (T1)args[1].Value(), (T2)args[2].Value()), $"{name}() result");
            });
        }

        public static void SetInterpeter(this Dictionary<string, BuiltinFunction> builtinFunctions, BytecodeInterpeter interpeter)
        {
            foreach (KeyValuePair<string, BuiltinFunction> item in builtinFunctions)
            { item.Value.BytecodeInterpeter = interpeter; }
        }

        #endregion

        static void CheckParameters(string functionName, TypeToken[] requied, DataItem[] passed)
        {
            if (passed.Length != requied.Length) throw new RuntimeException($"Wrong number of parameters passed to builtin function '{functionName}' ({passed.Length}) wich requies {requied.Length}");

            for (int i = 0; i < requied.Length; i++)
            {
                if (!passed[i].EqualType(requied[i].typeName)) throw new RuntimeException($"Wrong type of parameter passed to builtin function '{functionName}'. Parameter index: {i} Requied type: {requied[i].typeName.ToString().ToLower()} Passed type: {passed[i].type.ToString().ToLower()}");
            }
        }

        #region GetTypes<>()

        static TypeToken[] GetTypes<T0>() => new TypeToken[1]
        {
            GetType<T0>(),
        };
        static TypeToken[] GetTypes<T0, T1>() => new TypeToken[2]
        {
            GetType<T0>(),
            GetType<T1>(),
        };
        static TypeToken[] GetTypes<T0, T1, T2>() => new TypeToken[3]
        {
            GetType<T0>(),
            GetType<T1>(),
            GetType<T2>(),
        };
        static TypeToken[] GetTypes<T0, T1, T2, T3>() => new TypeToken[4]
        {
            GetType<T0>(),
            GetType<T1>(),
            GetType<T2>(),
            GetType<T3>(),
        };
        static TypeToken[] GetTypes<T0, T1, T2, T3, T4>() => new TypeToken[5]
        {
            GetType<T0>(),
            GetType<T1>(),
            GetType<T2>(),
            GetType<T3>(),
            GetType<T4>(),
        };
        static TypeToken[] GetTypes<T0, T1, T2, T3, T4, T5>() => new TypeToken[6]
        {
            GetType<T0>(),
            GetType<T1>(),
            GetType<T2>(),
            GetType<T3>(),
            GetType<T4>(),
            GetType<T5>(),
        };

        static TypeToken GetType<T>()
        {
            var type_ = typeof(T);

            if (type_ == typeof(int))
            { return TypeToken.CreateAnonymous("int", BuiltinType.INT); }
            if (type_ == typeof(float))
            { return TypeToken.CreateAnonymous("float", BuiltinType.FLOAT); }
            if (type_ == typeof(bool))
            { return TypeToken.CreateAnonymous("bool", BuiltinType.BOOLEAN); }
            if (type_ == typeof(string))
            { return TypeToken.CreateAnonymous("string", BuiltinType.STRING); }

            return null;
        }

        #endregion
    }
}
