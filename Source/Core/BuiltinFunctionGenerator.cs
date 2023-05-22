using IngameCoding.BBCode;
using IngameCoding.BBCode.Compiler;
using IngameCoding.Bytecode;
using IngameCoding.Errors;

using System;
using System.Collections.Generic;

namespace IngameCoding.Core
{
    public class BuiltinFunction
    {
        public readonly TypeToken[] ParameterTypes;
        public readonly string Name;

        readonly Action<DataItem[], BuiltinFunction> callback;

        public delegate void ReturnEventHandler(DataItem returnValue);
        public event ReturnEventHandler OnReturn;

        public int ParameterCount => ParameterTypes.Length;
        public bool ReturnSomething;

        internal BytecodeInterpreter BytecodeInterpreter;

        public void RaiseReturnEvent(DataItem returnValue)
        {
            OnReturn?.Invoke(returnValue);
        }

        public BuiltinFunction()
        { }

        /// <summary>
        /// Function without return value
        /// </summary>
        /// <param name="callback">Callback when the interpreter process this function</param>
        public BuiltinFunction(Action<DataItem[], BuiltinFunction> callback, string name, TypeToken[] parameters, bool returnSomething = false)
        {
            this.Name = name;
            this.ParameterTypes = parameters;
            this.callback = callback;
            this.ReturnSomething = returnSomething;
        }

        /// <summary>
        /// Function without return value
        /// </summary>
        /// <param name="callback">Callback when the interpreter process this function</param>
        public BuiltinFunction(Action<DataItem[]> callback, string name, TypeToken[] parameters, bool returnSomething = false)
        {
            this.Name = name;
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

        internal object ReadableID()
        {
            string result = Name;
            result += "(";
            for (int j = 0; j < ParameterTypes.Length; j++)
            {
                if (j > 0) { result += ", "; }
                result += ParameterTypes[j].Type.ToString().ToLower();
            }
            result += ")";
            return result;
        }
    }

    public static class BuiltinFunctionGenerator
    {
        #region AddBuiltinFunction()

        public static BuiltinFunction AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, System.Reflection.MethodInfo method)
        {
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            TypeToken[] parameterTypes = new TypeToken[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                System.Reflection.ParameterInfo parameter = parameters[i];
                Type paramType = parameter.ParameterType;
                if (paramType == typeof(string))
                {
                    parameterTypes[i] = TypeToken.CreateAnonymous("string", BuiltinType.STRING);
                }
                else if (paramType == typeof(int))
                {
                    parameterTypes[i] = TypeToken.CreateAnonymous("int", BuiltinType.INT);
                }
                else if (paramType == typeof(float))
                {
                    parameterTypes[i] = TypeToken.CreateAnonymous("float", BuiltinType.FLOAT);
                }
                else if (paramType == typeof(bool))
                {
                    parameterTypes[i] = TypeToken.CreateAnonymous("bool", BuiltinType.BOOLEAN);
                }
                else
                {
                    throw new InternalException($"Unknown type {paramType.FullName}");
                }
            }

            BuiltinFunction function = new((ps) =>
            {
                if (ps.Length != parameters.Length)
                { throw new RuntimeException($"Wrong number of parameters passed to built-in function {method.Name} ({ps.Length}): expected {parameters.Length}"); }

                var objs = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    var p = ps[i];
                    if (p.type != DataType.INT &&
                        p.type != DataType.FLOAT &&
                        p.type != DataType.STRING &&
                        p.type != DataType.BOOLEAN)
                    { throw new RuntimeException($"Invalid parameter type {p.type.ToString().ToLower()}"); }
                    if (p.type != parameterTypes[i].Type.Convert().Convert())
                    { throw new RuntimeException($"Invalid parameter type {p.type.ToString().ToLower()}: expected {parameterTypes[i].Type.ToString().ToLower()}"); }
                    objs[i] = p.Value();
                }

                method.Invoke(null, objs);
            }, method.Name, parameterTypes, method.ReturnType != typeof(void));

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

        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, TypeToken[] parameterTypes, Func<DataItem[], DataItem> callback)
        {
            BuiltinFunction function = new(new Action<DataItem[], BuiltinFunction>((p, self) =>
            {
                DataItem x = callback(p);
                if (self.BytecodeInterpreter == null)
                {
                    Output.Output.Error($"The built-in function '{name}' can not return a value: bytecode interpreter is null");
                    return;
                }
                self.BytecodeInterpreter.AddValueToStack(x);
            }), name, parameterTypes, true);

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
            BuiltinFunction function = new(new Action<DataItem[], BuiltinFunction>((_, self) =>
            {
                var x = callback();
                if (self.BytecodeInterpreter == null)
                {
                    Output.Output.Error($"The built-in function '{name}' can not return a value: bytecode interpreter is null");
                    return;
                }
                self.BytecodeInterpreter.AddValueToStack(x);
            }), name, Array.Empty<TypeToken>(), true);

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
        public static void AddBuiltinFunction(this Dictionary<string, BuiltinFunction> builtinFunctions, string name, TypeToken[] parameterTypes, Action<DataItem[]> callback, bool ReturnSomething = false)
        {
            BuiltinFunction function = new(callback, name, parameterTypes, ReturnSomething);

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

        public static void SetInterpreter(this Dictionary<string, BuiltinFunction> builtinFunctions, BytecodeInterpreter interpreter)
        {
            foreach (KeyValuePair<string, BuiltinFunction> item in builtinFunctions)
            { item.Value.BytecodeInterpreter = interpreter; }
        }

        #endregion

        static void CheckParameters(string functionName, TypeToken[] requied, DataItem[] passed)
        {
            if (passed.Length != requied.Length) throw new RuntimeException($"Wrong number of parameters passed to builtin function '{functionName}' ({passed.Length}) wich requies {requied.Length}");

            for (int i = 0; i < requied.Length; i++)
            {
                if (!passed[i].EqualType(requied[i].Type.Convert())) throw new RuntimeException($"Wrong type of parameter passed to builtin function '{functionName}'. Parameter index: {i} Requied type: {requied[i].Type.ToString().ToLower()} Passed type: {passed[i].type.ToString().ToLower()}");
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
