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
                case RuntimeType.BYTE:
                    serializer.Serialize(valueByte);
                    break;
                case RuntimeType.INT:
                    serializer.Serialize(valueInt);
                    break;
                case RuntimeType.FLOAT:
                    serializer.Serialize(valueFloat);
                    break;
                case RuntimeType.CHAR:
                    serializer.Serialize(valueChar);
                    break;
                case RuntimeType.NULL:
                    break;
                default: throw new ImpossibleException();
            }
        }

        public void Deserialize(Deserializer deserializer)
        {
            type = (RuntimeType)deserializer.DeserializeByte();
            switch (type)
            {
                case RuntimeType.BYTE:
                    valueByte = deserializer.DeserializeByte();
                    break;
                case RuntimeType.INT:
                    valueInt = deserializer.DeserializeInt32();
                    break;
                case RuntimeType.FLOAT:
                    valueFloat = deserializer.DeserializeFloat();
                    break;
                case RuntimeType.CHAR:
                    valueChar = deserializer.DeserializeChar();
                    break;
                case RuntimeType.NULL:
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
                RuntimeType.BYTE => Value.Literal(valueByte),
                RuntimeType.INT => Value.Literal(valueInt),
                RuntimeType.FLOAT => Value.Literal(valueFloat),
                RuntimeType.CHAR => Value.Literal(valueChar),
                RuntimeType.NULL => "null",
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
                case RuntimeType.BYTE:
                    valueByte = (byte)(data["Value"].Int ?? 0);
                    break;
                case RuntimeType.INT:
                    valueInt = data["Value"].Int ?? 0;
                    break;
                case RuntimeType.FLOAT:
                    valueFloat = data["Value"].Float ?? 0f;
                    break;
                case RuntimeType.CHAR:
                    valueChar = (char)(data["Value"].Int ?? 0);
                    break;
                case RuntimeType.NULL:
                    break;
                default: throw new ImpossibleException();
            }
        }
    }
}
