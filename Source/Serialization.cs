using System;
using System.Collections.Generic;

namespace IngameCoding.Serialization
{
    static class Extensions
    {
        internal static T[] Get<T>(this T[] array, int startIndex, int length)
        {
            List<T> result = new();
            for (int i = startIndex; i < length + startIndex; i++)
            { result.Add(array[i]); }
            return result.ToArray();
        }
    }

    internal class Deserializer
    {
        readonly byte[] data = Array.Empty<byte>();
        int currentIndex;

        internal Deserializer(byte[] data)
        {
            this.data = data;
            this.currentIndex = 0;
        }

        internal T[] DeserializeArray<T>()
        {
            int length = DeserializeInt32();
            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = (T)Deserialize<T>();
            }
            return result;
        }
        internal T[] DeserializeObjectArray<T>() where T : ISerializable<T>
        {
            int length = DeserializeInt32();
            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = (T)DeserializeObject<T>();
            }
            return result;
        }
        internal T[] DeserializeObjectArray<T>(Func<Deserializer, T> callback)
        {
            int length = DeserializeInt32();
            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = callback.Invoke(this);
            }
            return result;
        }
        internal object Deserialize<T>()
        {
            if (typeof(T) == typeof(System.Int32))
            { return DeserializeInt32(); }
            if (typeof(T) == typeof(System.Int16))
            { return DeserializeInt16(); }
            if (typeof(T) == typeof(System.Char))
            { return DeserializeChar(); }
            if (typeof(T) == typeof(System.String))
            { return DeserializeString(); }
            if (typeof(T) == typeof(System.Boolean))
            { return DeserializeBoolean(); }
            if (typeof(T) == typeof(System.Single))
            { return DeserializeFloat(); }
            if (typeof(T) == typeof(System.Byte))
            { return DeserializeByte(); }

            throw new NotImplementedException();
        }
        internal System.Int32 DeserializeInt32()
        {
            var data = this.data.Get(currentIndex, 4);
            currentIndex += 4;
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }
        internal System.Byte DeserializeByte()
        {
            var data = this.data.Get(currentIndex, 1);
            currentIndex += 1;
            return data[0];
        }
        internal System.Char DeserializeChar()
        {
            var data = this.data.Get(currentIndex, 2);
            currentIndex += 2;
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToChar(data, 0);
        }
        internal System.Int16 DeserializeInt16()
        {
            var data = this.data.Get(currentIndex, 2);
            currentIndex += 2;
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToInt16(data, 0);
        }
        internal System.Single DeserializeFloat()
        {
            var data = this.data.Get(currentIndex, 4);
            currentIndex += 4;
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToSingle(data, 0);
        }
        internal System.Boolean DeserializeBoolean()
        {
            var data = this.data.Get(currentIndex, 1);
            currentIndex++;
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToBoolean(data, 0);
        }
        internal System.String DeserializeString()
        {
            int length = DeserializeInt32();
            if (length == -1) return null;
            if (length == 0) return System.String.Empty;
            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = DeserializeChar();
            }
            return new System.String(result);
        }
        internal ISerializable<T> DeserializeObject<T>() where T : ISerializable<T>
        {
            var instance = (ISerializable<T>)Activator.CreateInstance(typeof(T));
            instance.Deserialize(this);
            return instance;
        }
        ISerializable<T> DeserializeObjectUnsafe<T>()
        {
            var instance = (ISerializable<T>)Activator.CreateInstance(typeof(T));
            instance.Deserialize(this);
            return instance;
        }
        internal T DeserializeObject<T>(Func<Deserializer, T> callback)
        {
            return callback.Invoke(this);
        }
        internal Dictionary<TKey, TValue> DeserializeDictionary<TKey, TValue>(bool keyIsObj, bool valIsObj)
        {
            int length = DeserializeInt32();
            if (length == -1) return null;
            Dictionary<TKey, TValue> result = new();

            for (int i = 0; i < length; i++)
            {
                var key = keyIsObj ? (TKey)DeserializeObjectUnsafe<TKey>() : (TKey)Deserialize<TKey>();
                var value = valIsObj ? (TValue)DeserializeObjectUnsafe<TValue>() : (TValue)Deserialize<TValue>();
                result.Add(key, value);
            }

            return result;
        }
    }

    internal class Serializer
    {
        readonly List<byte> result = new();

        internal byte[] Result => result.ToArray();

        internal void Serialize(System.Int32 v)
        {
            var result = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(result);
            this.result.AddRange(result);
        }
        internal void Serialize(System.Single v)
        {
            var result = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(result);
            this.result.AddRange(result);
        }
        internal void Serialize(System.Boolean v)
        {
            this.result.Add(BitConverter.GetBytes(v)[0]);
        }
        internal void Serialize(System.Byte v)
        {
            this.result.Add(v);
        }
        internal void Serialize(System.Int16 v)
        {
            var result = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(result);
            this.result.AddRange(result);
        }
        internal void Serialize(System.Char v)
        {
            var result = BitConverter.GetBytes(v);
            if (BitConverter.IsLittleEndian) Array.Reverse(result);
            this.result.AddRange(result);
        }
        internal void Serialize(System.String v)
        {
            if (v == null)
            {
                Serialize(-1);
                return;
            }
            Serialize(v.Length);
            for (int i = 0; i < v.Length; i++)
            { Serialize(v[i]); }
        }
        internal void Serialize(System.Int16[] v)
        {
            Serialize(v.Length);
            for (int i = 0; i < v.Length; i++)
            { Serialize(v[i]); }
        }
        internal void Serialize(System.Int32[] v)
        {
            Serialize(v.Length);
            for (int i = 0; i < v.Length; i++)
            { Serialize(v[i]); }
        }
        internal void Serialize(System.String[] v)
        {
            Serialize(v.Length);
            for (int i = 0; i < v.Length; i++)
            { Serialize(v[i]); }
        }
        internal void Serialize(System.Char[] v)
        {
            Serialize(v.Length);
            for (int i = 0; i < v.Length; i++)
            { Serialize(v[i]); }
        }
        internal void SerializeObjectArray<T>(ISerializable<T>[] v)
        {
            Serialize(v.Length);
            for (int i = 0; i < v.Length; i++)
            { SerializeObject(v[i]); }
        }
        internal void SerializeObjectArray<T>(T[] v, Action<Serializer, T> callback)
        {
            Serialize(v.Length);
            for (int i = 0; i < v.Length; i++)
            { callback.Invoke(this, v[i]); }
        }
        internal void SerializeObject<T>(ISerializable<T> v)
        {
            v.Serialize(this);
        }
        internal void SerializeObject<T>(T v, Action<Serializer, T> callback)
        {
            callback.Invoke(this, v);
        }
        void Serialize(object v)
        {
            if (v is System.Int16 int16)
            { Serialize(int16); }
            else if (v is System.Int32 int32)
            { Serialize(int32); }
            else if (v is System.Char @char)
            { Serialize(@char); }
            else if (v is System.String @string)
            { Serialize(@string); }
            else if (v is System.Single single)
            { Serialize(single); }
            else if (v is System.Boolean boolean)
            { Serialize(boolean); }
            else if (v is System.Byte @byte)
            { Serialize(@byte); }
            else
            { throw new NotImplementedException(); }
        }
        internal void Serialize<TKey, TValue>(Dictionary<TKey, TValue> v, bool keyIsObj, bool valIsObj)
            where TKey : struct, IConvertible
            where TValue : struct, IConvertible
        {
            if (v.Count == 0) { Serialize(-1); return; }
            Serialize(v.Count);

            foreach (var pair in v)
            {
                if (keyIsObj)
                {
                    SerializeObject((ISerializable<TKey>)pair.Key);
                }
                else
                {
                    Serialize(pair.Key);
                }
                if (valIsObj)
                {
                    SerializeObject((ISerializable<TValue>)pair.Value);
                }
                else
                {
                    Serialize(pair.Value);
                }
            }
        }
        internal void Serialize<TKey>(Dictionary<TKey, string> v, bool keyIsObj)
            where TKey : struct, IConvertible
        {
            if (v.Count == 0) { Serialize(-1); return; }
            Serialize(v.Count);

            foreach (var pair in v)
            {
                if (keyIsObj)
                {
                    SerializeObject((ISerializable<TKey>)pair.Key);
                }
                else
                {
                    Serialize(pair.Key);
                }
                Serialize(pair.Value);
            }
        }
        internal void Serialize<TValue>(Dictionary<string, TValue> v, bool valIsObj)
            where TValue : struct, IConvertible
        {
            if (v.Count == 0) { Serialize(-1); return; }
            Serialize(v.Count);

            foreach (var pair in v)
            {
                Serialize(pair.Key);
                if (valIsObj)
                {
                    SerializeObject((ISerializable<TValue>)pair.Value);
                }
                else
                {
                    Serialize(pair.Value);
                }
            }
        }
    }

    internal interface ISerializable<T>
    {
        void Serialize(Serializer serializer);
        void Deserialize(Deserializer deserializer);
    }

    #region Output
#if false

        static void Log(string message) { Console.WriteLine(message); }

        static void Print(int value) { Console.ForegroundColor = ConsoleColor.Green; Console.Write(value); Console.ResetColor(); }
        static void Print(short value) { Console.ForegroundColor = ConsoleColor.Green; Console.Write(value); Console.ResetColor(); }
        static void Print(byte value) { Console.ForegroundColor = ConsoleColor.Green; Console.Write(value); Console.ResetColor(); }
        static void Print(long value) { Console.ForegroundColor = ConsoleColor.Green; Console.Write(value); Console.ResetColor(); }
        static void Print(float value) { Console.ForegroundColor = ConsoleColor.Green; Console.Write(value); Console.ResetColor(); }
        static void Print(double value) { Console.ForegroundColor = ConsoleColor.Green; Console.Write(value); Console.ResetColor(); }

        static void Print(string value) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write($"\"{value}\""); Console.ResetColor(); }
        static void Print(char value) { Console.ForegroundColor = ConsoleColor.Yellow; Console.Write($"'{value}'"); Console.ResetColor(); }

        static void Print(bool value) { Console.ForegroundColor = ConsoleColor.Blue; Console.Write(value ? "true" : "false"); Console.ResetColor(); }

        static void Print(int[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }
        static void Print(byte[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }
        static void Print(short[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }
        static void Print(long[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }
        static void Print(float[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }
        static void Print(double[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }

        static void Print(string[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }
        static void Print(char[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }

        static void Print(bool[] value)
        {
            Console.Write("[ ");
            for (int i = 0; i < value.Length; i++)
            { if (i > 0) Console.Write(", "); Print(value[i]); }
            Console.Write(" ]");
        }

        static void Print(object value)
        {
            {
                if (value is byte) { Print((byte)value); return; }
                if (value is short) { Print((short)value); return; }
                if (value is int) { Print((int)value); return; }
                if (value is long) { Print((long)value); return; }
                if (value is float) { Print((float)value); return; }
                if (value is char) { Print((char)value); return; }
                if (value is string) { Print((string)value); return; }
                if (value is byte[]) { Print((byte[])value); return; }
                if (value is short[]) { Print((short[])value); return; }
                if (value is int[]) { Print((int[])value); return; }
                if (value is long[]) { Print((long[])value); return; }
                if (value is float[]) { Print((float[])value); return; }
                if (value is char[]) { Print((char[])value); return; }
                if (value is string[]) { Print((string[])value); return; }
            }

            var type = value.GetType();
            var fields = type.GetFields();

            Console.Write("{ ");
            for (int i = 0; i < fields.Length; i++)
            {
                System.Reflection.FieldInfo field = fields[i];
                Console.Write(field.Name + ": ");
                Print(field.GetValue(value));
                Console.Write("; ");
            }

            Console.Write("}");
        }

#endif
    #endregion
}
