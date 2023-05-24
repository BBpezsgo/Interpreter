using System;
using System.Collections.Generic;

namespace IngameCoding.Bytecode
{
    static class Extensions
    {
        internal static string[] ToStringArray(this DataItem[] items)
        {
            string[] strings = new string[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                strings[i] = items[i].ToString();
            }
            return strings;
        }

        internal static string GetTypeText(this DataType type) => type switch
        {
            DataType.BYTE => "byte",
            DataType.INT => "int",
            DataType.FLOAT => "float",
            DataType.STRING => "string",
            DataType.BOOLEAN => "bool",
            _ => throw new NotImplementedException(),
        };
        internal static string GetTypeText(this DataItem val) => GetTypeText(val.type);

        internal static int GetInt(this StepList<byte> data)
        {
            byte[] d = data.Next(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(d);
            return BitConverter.ToInt32(d, 0);
        }
        internal static float GetFloat(this StepList<byte> data)
        {
            byte[] d = data.Next(4);
            if (BitConverter.IsLittleEndian) Array.Reverse(d);
            return BitConverter.ToSingle(d, 0);
        }
        internal static bool GetBoolean(this StepList<byte> data) => data.Next() == 1;
        internal static string GetString(this StepList<byte> data) => new(data.GetList(d =>
        {
            byte[] chr = data.Next(2);
            if (BitConverter.IsLittleEndian) Array.Reverse(chr);
            return BitConverter.ToChar(chr, 0);
        }));
        internal static T[] GetList<T>(this StepList<byte> data, Func<StepList<byte>, T> converter)
        {
            byte length = data.Next();
            List<T> result = new();
            for (int i = 0; i < length; i++) result.Add(converter.Invoke(data));
            return result.ToArray();
        }
        internal static DataItem GetDataItem(this StepList<byte> data, string tag = "")
        {
            DataType type = (DataType)data.Next();
            switch (type)
            {
                case DataType.STRING:
                    {
                        return new DataItem(data.GetString(), tag);
                    }
                case DataType.INT:
                    {
                        return new DataItem(data.GetInt(), tag);
                    }
                case DataType.FLOAT:
                    {
                        return new DataItem(data.GetFloat(), tag);
                    }
                case DataType.BOOLEAN:
                    {
                        return new DataItem(data.GetBoolean(), tag);
                    }
                default:
                    throw new NotImplementedException();
            }
        }

        internal static byte[] ToByteArray<T>(this T[] self, Func<T, byte[]> converter)
        {
            if (self.Length > byte.MaxValue) throw new NotImplementedException();
            List<byte> result = new()
            { (byte)self.Length };
            foreach (T item in self) result.AddRange(converter?.Invoke(item));
            return result.ToArray();
        }
        internal static byte[] ToByteArray(this string self)
        {
            if (self.Length > byte.MaxValue) throw new NotImplementedException();
            List<byte> result = new()
            { (byte)self.Length };
            foreach (var item in self)
            {
                byte[] chr = BitConverter.GetBytes(item);
                if (BitConverter.IsLittleEndian) Array.Reverse(chr);
                result.AddRange(chr);
            }
            return result.ToArray();
        }
        internal static byte[] ToByteArray(this int self)
        {
            byte[] result = BitConverter.GetBytes(self);
            if (BitConverter.IsLittleEndian) Array.Reverse(result);
            return result;
        }
        internal static byte[] ToByteArray(this float self)
        {
            byte[] result = BitConverter.GetBytes(self);
            if (BitConverter.IsLittleEndian) Array.Reverse(result);
            return result;
        }
        internal static byte[] ToByteArray(this bool self) => new byte[] { (byte)(self ? 1 : 0) };
        internal static byte[] ToByteArray(this DataItem self)
        {
            List<byte> result = new()
            { (byte)self.type };
            switch (self.type)
            {
                case DataType.STRING:
                    {
                        result.AddRange(self.ValueString.ToByteArray());
                        break;
                    }
                case DataType.INT:
                    {
                        result.AddRange(self.ValueInt.ToByteArray());
                        break;
                    }
                case DataType.FLOAT:
                    {
                        result.AddRange(self.ValueFloat.ToByteArray());
                        break;
                    }
                case DataType.BOOLEAN:
                    {
                        result.AddRange(self.ValueBoolean.ToByteArray());
                        break;
                    }
                default:
                    throw new NotImplementedException();
            }
            return result.ToArray();
        }
    }
}
