using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KS.USerializer
{
    //public interface ITypeSerializer
    //{
    //    TypeCache GetTypeCache(Type type);
    //    List<UInt32> GetComplexTypeId(Type type);
    //    List<UInt32> GetComplexTypeId(Type type, TypeCache cache);
    //    List<Object> GetSubObjects(TypeCache cache, Object mainObject);
    //    Type GetTypeById(UInt32 typeId);
    //}

    //public interface IObjectSerializer
    //{
    //    ITypeSerializer TypeSerializer { get; }
    //    UInt32 GetNewObjectId();
    //    Byte[] SerializeSubObject(Type type, Object subObject);
    //}

    public class SubObjectEntry
    {
        public List<UInt32> TypeDefinition;
        public UInt32 Id;
        public Byte[] SerialData;
        public object value;

        public override string ToString()
        {
            return String.Format("Type: {0} Id: {1} Value: {2}", String.Join(",", TypeDefinition), Id, String.Join(",", SerialData));
        }
    }

    public class BaseTypeCache
    {
        public UInt32 Id;
        public bool IsGeneric;
        public Type BaseType;
        public List<FieldInfo> Fields;
        public UInt32 GenericParameterCount;
        public bool IsArray;

        public BaseTypeCache(Type type, UInt32 id)
        {
            Id = id;
            IsGeneric = type.IsGenericType;
            IsArray = type.IsArray;
            BaseType = type;

            if (IsGeneric)
            {
                GenericParameterCount = (UInt32)type.GetGenericArguments().Count();
            }
            else
            {
                Fields = BaseType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name).ToList();
                GenericParameterCount = 0;
            }
        }

        public static Type GetBaseType(Type type)
        {
            return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        }
    }

    public class ComplexTypeCache : IDisposable
    {
        public Type ComplexType;
        public BaseTypeCache CachedType;
        public List<FieldInfo> Fields;
        public ComplexTypeCache(Type type, BaseTypeCache cache)
        {
            ComplexType = type;
            CachedType = cache;
            Fields = cache.Fields;
            if (cache.IsGeneric)
                Fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name).ToList();
        }

        public void Dispose() { Fields.Clear(); }
    }

    public class UTypeSerializer //: ITypeSerializer
    {
        protected Object lockObject = new Object();
        protected UInt32 newTypeId = 0;
        protected Dictionary<Type, BaseTypeCache> typeCache = new Dictionary<Type, BaseTypeCache>();

        protected UInt32 GetNewTypeId()
        {
            lock (lockObject)
                return ++newTypeId;
        }

        protected BaseTypeCache GetBaseTypeCache(Type type)
        {
            Type BaseType = BaseTypeCache.GetBaseType(type);
            if (typeCache.ContainsKey(BaseType))
                return typeCache[BaseType];
            var baseTypeCache = new BaseTypeCache(BaseType, GetNewTypeId());
            typeCache[BaseType] = baseTypeCache;
            return baseTypeCache;
        }

        public ComplexTypeCache GetTypeCache(Type type) => new ComplexTypeCache(type, GetBaseTypeCache(type));

        public List<uint> GetComplexTypeId(ComplexTypeCache cache)
        {
            var result = new List<uint>() { cache.CachedType.Id };
            if (cache.CachedType.IsGeneric)
                foreach (var generic in cache.ComplexType.GetGenericArguments())
                    using (var typeCache = GetTypeCache(generic))
                        result.AddRange(GetComplexTypeId(typeCache));

            return result;
        }

        public IEnumerable<TypedObject> GetSubObjects(ComplexTypeCache cache, object mainObject)
        {
            if (mainObject == null)
                yield break;
            if (!cache.CachedType.IsArray)
            {
                foreach (var f in cache.Fields)
                    yield return new TypedObject(f.FieldType, f.GetValue(mainObject));
            }
            else
            {
                var array = ((Array)mainObject);
                var arrayType = cache.ComplexType.GetElementType();
                yield return new TypedObject(typeof(Int32), array.Length);
                foreach (var obj in array)
                    yield return new TypedObject(arrayType, obj);
            }
        }

        public BaseTypeCache GetTypeById(uint typeId)
        {
            return typeCache.Values.First(tc => tc.Id == typeId);
        }
    }

    public class TypedObject
    {
        public Type type { get; protected set; }
        public Object value { get; protected set; }
        public TypedObject(Type type, Object value) { this.type = type; this.value = value; }
    }

    public class UObjectSerializer //: IObjectSerializer
    {
        protected TypedObject mainObject;
        protected Object lockObject = new Object();
        protected UInt32 newObjectId = 0;
        protected List<SubObjectEntry> Results = new List<SubObjectEntry>();

        public UTypeSerializer TypeSerializer { get; protected set; }

        protected UObjectSerializer(Type type, Object value) { mainObject = new TypedObject(type, value); }

        public static UObjectSerializer Create(Type type, Object value) => new UObjectSerializer(type, value);
        public static UObjectSerializer Create<T>(T value) => new UObjectSerializer(typeof(T), value);

        public List<SubObjectEntry> Serialize()
        {
            Serialize(mainObject);
            return Results;
        }

        public uint GetNewObjectId()
        {
            lock (lockObject)
                return ++newObjectId;
        }

        protected UInt32 Serialize(TypedObject subObject)
        {
            var result = Results.FirstOrDefault(r => r.value == subObject.value);
            if (result != null)
                return result.Id;

            var cache = TypeSerializer.GetTypeCache(subObject.type);

            result = new SubObjectEntry();

            result.Id = GetNewObjectId();
            result.TypeDefinition = TypeSerializer.GetComplexTypeId(cache);
            result.SerialData = TypeSerializer.GetSubObjects(cache, subObject.value).SelectMany(o => SerializeSubObject(o)).ToArray();
            result.value = subObject.value;

            Results.Add(result);
            return result.Id;
        }

        public byte[] SerializeSubObject(TypedObject subObject)
        {
            if (PrimitiveTypeSerializer.ContainsKey(subObject.type))
                return PrimitiveTypeSerializer[subObject.type](subObject.value);
            return BitConverter.GetBytes(Serialize(subObject));
        }

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
    }

    public class UObjectDeserializer
    {
        public UTypeSerializer TypeSerializer { get; protected set; }
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

            var baseType = TypeSerializer.GetTypeById(typeIdList[0]);
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
    }

    public class DefaultDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        protected Func<TKey, TValue> p;
        public DefaultDictionary(Func<TKey, TValue> p) { this.p = p; }

        public new TValue this[TKey key]
        {
            get
            {
                if (!ContainsKey(key))
                    base[key] = p(key);
                return base[key];
            }
            set { base[key] = value; }
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
