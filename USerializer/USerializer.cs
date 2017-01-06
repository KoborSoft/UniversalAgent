using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KS.USerializer
{
    public static class Utils
    {
        public static Type GetBaseType(Type type)
        {
            return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        }

        public static List<FieldInfo> GetAllInstanceFields(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name).ToList();
        }
    }
    public class TypeCacheEntry
    {
        public UInt32 Id;
        public bool IsGeneric;
        public Type BaseType;
        public List<FieldInfo> Fields;
        public bool IsArray;

        public TypeCacheEntry(Type type, UInt32 id)
        {
            Id = id;
            IsGeneric = type.IsGenericType;
            IsArray = type.IsArray;
            BaseType = type;

            if (!IsGeneric)
                Fields = Utils.GetAllInstanceFields(BaseType);
        }
    }

    public class UTypeCache
    {
        protected Object lockObject = new Object();
        protected UInt32 newTypeId = 0;
        protected Dictionary<Type, TypeCacheEntry> typeCache = new Dictionary<Type, TypeCacheEntry>();

        protected UInt32 GetNewTypeId()
        {
            lock (lockObject)
                return ++newTypeId;
        }

        public TypeCacheEntry GetTypeCacheEntry(Type type)
        {
            Type BaseType = Utils.GetBaseType(type);
            if (typeCache.ContainsKey(BaseType))
                return typeCache[BaseType];
            var baseTypeCache = new TypeCacheEntry(BaseType, GetNewTypeId());
            typeCache[BaseType] = baseTypeCache;
            return baseTypeCache;
        }

        public TypeCacheEntry GetTypeCacheById(uint typeId)
        {
            return typeCache.Values.First(tc => tc.Id == typeId);
        }
    }

    public class FastType : IDisposable
    {
        public Type RealType;
        public TypeCacheEntry Cache;
        public List<FieldInfo> Fields;
        public FastType(Type type, TypeCacheEntry cache)
        {
            RealType = type;
            Cache = cache;
            Fields = cache.Fields;
            if (cache.IsGeneric)
                Fields = Utils.GetAllInstanceFields(type);
        }

        protected IEnumerable<TypedObject> GetArrayElements(Array array)
        {
            var arrayType = RealType.GetElementType();
            yield return new TypedObject(typeof(Int32), array.Length);
            foreach (var obj in array)
                yield return new TypedObject(arrayType, obj);
        }

        protected IEnumerable<TypedObject> GetFields(object mainObject)
        {
            foreach (var f in Fields)
                yield return new TypedObject(f.FieldType, f.GetValue(mainObject));
        }

        public IEnumerable<TypedObject> GetSubObjects(object mainObject)
        {
            if (mainObject == null)
                return new List<TypedObject>();
            return Cache.IsArray
                ? GetArrayElements((Array)mainObject)
                : GetFields(mainObject);
        }

        public void Dispose() { Fields.Clear(); }
    }

    public class UTypeSerializer //: ITypeSerializer
    {
        public UTypeCache TypeCache = new UTypeCache();

        public FastType GetFastType(Type type) => new FastType(type, TypeCache.GetTypeCacheEntry(type));

        public IEnumerable<uint> GetComplexTypeId(FastType fastType)
        {
            yield return fastType.Cache.Id;
            if (fastType.Cache.IsGeneric)
                foreach (var generic in fastType.RealType.GetGenericArguments())
                    using (var typeCache = GetFastType(generic))
                        foreach (var r in GetComplexTypeId(typeCache))
                            yield return r;
        }
    }

    [Serializable]
    public class ResultEntry
    {
        public List<UInt32> TypeDefinition;
        public UInt32 Id;
        public Byte[] SerialData;
        public volatile object Value;

        public override string ToString()
        {
            return String.Format("Type: {0} Id: {1} Value: {2}", String.Join(",", TypeDefinition), Id, String.Join(",", SerialData));
        }
    }

    public class TypedObject
    {
        public Type type { get; protected set; }
        public Object Value { get; protected set; }
        public TypedObject(Type type, Object value) { this.type = type; this.Value = value; }
    }

    public class USerializer
    {
        public UTypeSerializer TypeSerializer { get; protected set; } = new UTypeSerializer();
        public IEnumerable<Byte> Serialize<T>(T value) => Serialize(typeof(T), value);
        public IEnumerable<Byte> Serialize(Type type, Object value)
        {
            var result = new List<Byte>();
            var resultEntries = new UObjectSerializer(TypeSerializer, new TypedObject(type, value)).Serialize();

            result.AddRange(BitConverter.GetBytes((UInt32)resultEntries.Count));
            foreach (var resultEntry in resultEntries)
            {
                result.AddRange(resultEntry.TypeDefinition.SelectMany(td => BitConverter.GetBytes(td)));
                result.AddRange(BitConverter.GetBytes(resultEntry.Id));
                result.AddRange(BitConverter.GetBytes((UInt32)resultEntry.SerialData.Length));
                result.AddRange(resultEntry.SerialData);
            }
            return result;
        }

        public Object Deserialize(IEnumerable<Byte> serializedObject)
        {
            return new UObjectDeserializer(TypeSerializer, serializedObject).Deserialize();
        }
    }

    public class UObjectSerializer //: IObjectSerializer
    {
        protected TypedObject MainObject;
        protected Object lockObject = new Object();
        protected UInt32 newObjectId = 0;
        protected List<ResultEntry> Results = new List<ResultEntry>();
        protected UTypeSerializer TypeSerializer;
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

        public UObjectSerializer(UTypeSerializer serializer, TypedObject mainObject)
        {
            TypeSerializer = serializer;
            MainObject = mainObject;
        }

        public uint GetNewObjectId()
        {
            lock (lockObject)
                return ++newObjectId;
        }

        public List<ResultEntry> Serialize()
        {
            SerializeWithType(MainObject);
            return Results;
        }

        protected UInt32 SerializeWithType(TypedObject mainObject)
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

    public class UObjectDeserializer
    {
        private IEnumerable<byte> serializedObject;
        protected UTypeSerializer TypeSerializer;

        public UObjectDeserializer(UTypeSerializer typeSerializer, IEnumerable<byte> serializedObject)
        {
            TypeSerializer = typeSerializer;
            this.serializedObject = serializedObject;
        }

        /// <summary>
        /// Deserializalize type.
        /// Not finished.
        /// Builds complex generic type
        /// Must be propagated to different class.
        /// Problem: common static container needed to serialize and deserialize types.
        /// Bad design?
        /// :-)
        /// </summary>
        /// <param name="typeIdList"></param>
        /// <returns></returns>
        protected Tuple<Type, List<UInt32>> DeserializeType(List<UInt32> typeIdList)
        {

            var baseType = TypeSerializer.TypeCache.GetTypeCacheById(typeIdList[0]);
            // Get rest of the Id-s and create a typeList. This will be the generic list.
            // prepare for recursion.
            // recursion is a must, since the generic types can be nicely nested.
            var restTypeIdList = typeIdList.Skip(1).ToList();
            var parameterTypes = new List<Type>();

            // BaseType knows how many generic parameters we are waiting for. Just get them in order.
            // This foreach is only to count.
            // Could type order vary? No. It is fix. Field order varies sometimes,
            // but Generic Type order is always strictly the same as defined in code.
            foreach (var type in baseType.BaseType.GetGenericArguments())
            {
                // Subtype creation. Recursive.
                var sub = DeserializeType(restTypeIdList);
                parameterTypes.Add(sub.Item1);

                // return value shortens the Id list.
                // Must. Not. Parallelize. !!!
                restTypeIdList = sub.Item2;
            }

            // Generic SubType Created. Woho!!!
            var result = baseType.BaseType.MakeGenericType(parameterTypes.ToArray());
            return new Tuple<Type, List<uint>>(result, restTypeIdList);
        }

        internal object Deserialize()
        {
            throw new NotImplementedException();
        }
    }
}

/* Scheme generation
 * 131 = List<>
 * 132 = int
 * 131, 132 = List<int>
 * 
 * List<int>() { 1, 2, 3, 4, 5, 6, 7 }
 * [ 131, 132, 1 (id), 7 (len), [1, 2, 3, 4, 5, 6, 7](values) ]
 * 
 * 133 = class TcpClient
 * 134 = class IPEndpoint
 * 133, 134 = class TcpClient<IPEndpoint> {int, int, int, class IPEndpoint, string, char, int}
 * 
 * List<TcpClient>() { new TcpClient(), new TcpClient() }
 * [ 134 (IPEndpoint), 3 (id) [ , , , , ] (values) ]
 * [ 134 (IPEndpoint), 5 (id) [ , , , , ] (values) ]
 * [ 133, 2 (id), [ 1, 2, 3, 3 (ref), "", '', 42 ] ]
 * [ 133, 4 (id), [ 1, 2, 3, 5 (ref), "", '', 43 ] ]
 * [ 131, 133, 1 (id), [ 2 (len), [ 2 (ref), 4 (ref) ] ] (values) ]
 *******/
