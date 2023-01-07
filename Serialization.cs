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

            throw new NotImplementedException();
        }
        internal System.Int32 DeserializeInt32()
        {
            var data = this.data.Get(currentIndex, 4);
            currentIndex += 4;
            if (BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
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
        internal System.String DeserializeString()
        {
            int length = DeserializeInt32();
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
        internal void Serialize<T>(ISerializable<T> v)
        {
            v.Serialize(this);
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
