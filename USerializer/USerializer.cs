using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KS.USerializer
{
    public class USerializedResult
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

    public class USerialization
    {
        protected static Object lockObject = new Object();
        protected static UInt32 newTypeId = 0;

        protected static Dictionary<Type, Func<object, byte[]>> PrimitiveTypeSerializer = new Dictionary<Type, Func<object, byte[]>>
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

        protected static DefaultDictionary<Type, UInt32> typeCache =
            new DefaultDictionary<Type, UInt32>(t =>
            {
                lock (lockObject)
                    return ++newTypeId;
            });

        protected Type type;
        protected Object value;
        protected UInt32 newObjectId = 0;

        public List<USerializedResult> Results { get; protected set; } = new List<USerializedResult>();

        public USerialization(Type type, Object value)
        {
            this.type = type;
            this.value = value;
        }

        public static USerialization Create(Type type, Object value) => new USerialization(type, value);
        public static USerialization Create<T>(T value) => new USerialization(typeof(T), value);

        public List<USerializedResult> Serialize()
        {
            Serialize(type, value);
            return Results;
        }

        protected UInt32 Serialize(Type type, Object value)
        {
            var result = Results.FirstOrDefault(r => r.value == value);
            if (result != null)
                return result.Id;

            lock (lockObject)
                result = new USerializedResult()
                {
                    Id = ++newObjectId,
                    value = value
                };

            result.TypeDefinition = SerializeType(type);

            var FieldWrappers = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name).ToList();

            var serializeObj = new Func<Type, Object, Byte[]>((t, v) =>
            {
                if (PrimitiveTypeSerializer.ContainsKey(t))
                    return PrimitiveTypeSerializer[t](v);
                return BitConverter.GetBytes(Serialize(t, v));
            });

            if (!type.IsArray)
                result.SerialData = FieldWrappers.SelectMany(f => serializeObj(f.FieldType, f.GetValue(value))).ToArray();
            else
            {
                var array = ((Array)value);
                var list = new List<Byte>();
                list.AddRange(BitConverter.GetBytes((UInt32)array.Length));
                foreach (var obj in array)
                    list.AddRange(serializeObj(type.GetElementType(), obj));
                result.SerialData = list.ToArray();
            }

            Results.Add(result);

            return result.Id;
        }

        protected List<UInt32> SerializeType(Type type)
        {
            if (!type.IsGenericType)
                return new List<UInt32>() { typeCache[type] };

            var result = new List<UInt32>();
            result.Add(typeCache[type.GetGenericTypeDefinition()]);
            foreach (var generic in type.GetGenericArguments())
                result.AddRange(SerializeType(generic));
            return result;
        }

        protected Tuple<Type, List<UInt32>> DeserializeType(List<UInt32> typeIdList)
        {
            var baseType = typeCache.First(t => t.Value == typeIdList[0]).Key;

            var restTypeIdList = typeIdList.Skip(1).ToList();
            var parameterTypes = new List<Type>();
            foreach (var type in baseType.GetGenericArguments())
            {
                var sub = DeserializeType(restTypeIdList);
                parameterTypes.Add(sub.Item1);
                restTypeIdList = sub.Item2;
            }
            var result = baseType.MakeGenericType(parameterTypes.ToArray());
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
 * List<int>() { 1, 2, 3, 4, 5, 6, 7 }
 * [ 131, 132, 1 (id), 7 (len), [1, 2, 3, 4, 5, 6, 7](values) ]
 * 
 * 133 = class TcpClient
 * 134 = class IPEndpoint
 * 133, 134 = class TcpClient<IPEndpoint> {int, int, int, class IPEndpoint, string, char, int}
 * 
 * [ 134 (IPEndpoint), 3 (id) [ , , , , ] (values) ]
 * [ 134 (IPEndpoint), 5 (id) [ , , , , ] (values) ]
 * [ 133, 2 (id), [ 1, 2, 3, 3 (ref), "", '', 42 ] ]
 * [ 133, 4 (id), [ 1, 2, 3, 5 (ref), "", '', 43 ] ]
 * [ 131, 133, 1 (id), [ 2 (len), [ 2 (ref), 4 (ref) ] ] (values) ]
 *******/
