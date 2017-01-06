using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KS.USerializer
{
    [Serializable]
    public class ResultEntry
    {
        public List<uint> TypeDefinition;
        public uint Id;
        public byte[] SerialData;
        public volatile object Value;

        public override string ToString()
        {
            return String.Format("Type: {0} Id: {1} Value: {2}", String.Join(",", TypeDefinition), Id, String.Join(",", SerialData));
        }
    }

    public class UObjectSerializer : IObjectSerializer
    {
        protected TypedObject MainObject;
        protected object lockObject = new object();
        protected uint newObjectId = 0;
        protected List<ResultEntry> Results = new List<ResultEntry>();
        public ITypeSerializer TypeSerializer { get; protected set; }
        protected Dictionary<Type, Func<object, byte[]>> PrimitiveTypeSerializer = new Dictionary<Type, Func<object, byte[]>>
                {
                    { typeof(bool), b => BitConverter.GetBytes((bool)b) },
                    { typeof(char), b => BitConverter.GetBytes((char)b) },
                    { typeof(double), b => BitConverter.GetBytes((double)b) },
                    { typeof(float), b => BitConverter.GetBytes((float) b) },
                    { typeof(int), b => BitConverter.GetBytes((int) b) },
                    { typeof(long), b => BitConverter.GetBytes((long)b) },
                    { typeof(short), b => BitConverter.GetBytes((short)b) },
                    { typeof(uint), b => BitConverter.GetBytes((uint)b) },
                    { typeof(ulong), b => BitConverter.GetBytes((ulong)b) },
                    { typeof(ushort), b => BitConverter.GetBytes((ushort)b) },
                    { typeof(sbyte), b => BitConverter.GetBytes((sbyte)b) },
                    { typeof(string), b => Encoding.Unicode.GetBytes((string)b) }
                };

        public UObjectSerializer(ITypeSerializer serializer, TypedObject mainObject)
        {
            TypeSerializer = serializer;
            MainObject = mainObject;
        }

        protected uint GetNewObjectId()
        {
            lock (lockObject)
                return ++newObjectId;
        }

        public List<ResultEntry> Serialize()
        {
            SerializeWithType(MainObject);
            return Results;
        }

        protected uint SerializeWithType(TypedObject mainObject)
        {
            var result = Results.FirstOrDefault(r => r.Value == mainObject.Value);
            if (result != null)
                return result.Id;

            var fastType = TypeSerializer.GetFastType(mainObject.type);

            result = new ResultEntry();
            result.Id = GetNewObjectId();
            result.TypeDefinition = TypeSerializer.GetComplexTypeId(fastType).ToList();
            result.SerialData = fastType.GetSubObjects(mainObject.Value).SelectMany(obj => SerializeSubObject(obj)).ToArray();
            result.Value = mainObject.Value;

            Results.Add(result);
            return result.Id;
        }

        protected byte[] SerializeSubObject(TypedObject subObject)
        {
            if (PrimitiveTypeSerializer.ContainsKey(subObject.type))
                return PrimitiveTypeSerializer[subObject.type](subObject.Value);
            return BitConverter.GetBytes(SerializeWithType(subObject));
        }
    }
}
