using DataUtilities.ReadableFileFormat;
using DataUtilities.Serializer;

namespace LanguageCore.Runtime
{
    public partial struct DataItem : ISerializable<DataItem>, IFullySerializableText
    {
        public readonly void Serialize(Serializer serializer)
        {
            serializer.Serialize((byte)type);
            switch (type)
            {
                case RuntimeType.UInt8:
                    serializer.Serialize(valueUInt8);
                    break;
                case RuntimeType.SInt32:
                    serializer.Serialize(valueSInt32);
                    break;
                case RuntimeType.Single:
                    serializer.Serialize(valueSingle);
                    break;
                case RuntimeType.UInt16:
                    serializer.Serialize(valueUInt16);
                    break;
                case RuntimeType.Null:
                    break;
                default: throw new ImpossibleException();
            }
        }

        public void Deserialize(Deserializer deserializer)
        {
            type = (RuntimeType)deserializer.DeserializeByte();
            switch (type)
            {
                case RuntimeType.UInt8:
                    valueUInt8 = deserializer.DeserializeByte();
                    break;
                case RuntimeType.SInt32:
                    valueSInt32 = deserializer.DeserializeInt32();
                    break;
                case RuntimeType.Single:
                    valueSingle = deserializer.DeserializeFloat();
                    break;
                case RuntimeType.UInt16:
                    valueUInt16 = deserializer.DeserializeChar();
                    break;
                case RuntimeType.Null:
                    break;
                default: throw new ImpossibleException();
            }
        }

        public readonly Value SerializeText()
        {
            Value result = Value.Object();

            result["Type"] = Value.Literal(type.ToString());
            result["Value"] = type switch
            {
                RuntimeType.UInt8 => Value.Literal(valueUInt8),
                RuntimeType.SInt32 => Value.Literal(valueSInt32),
                RuntimeType.Single => Value.Literal(valueSingle),
                RuntimeType.UInt16 => Value.Literal(valueUInt16),
                RuntimeType.Null => "null",
                _ => throw new ImpossibleException(),
            };

            return result;
        }

        public void DeserializeText(Value data)
        {
            if (!System.Enum.TryParse(data["Type"].String ?? "", out type))
            { return; }

            switch (type)
            {
                case RuntimeType.UInt8:
                    valueUInt8 = (byte)(data["Value"].Int ?? 0);
                    break;
                case RuntimeType.SInt32:
                    valueSInt32 = data["Value"].Int ?? 0;
                    break;
                case RuntimeType.Single:
                    valueSingle = data["Value"].Float ?? 0f;
                    break;
                case RuntimeType.UInt16:
                    valueUInt16 = (char)(data["Value"].Int ?? 0);
                    break;
                case RuntimeType.Null:
                    break;
                default: throw new ImpossibleException();
            }
        }
    }
}
