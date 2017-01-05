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

        /// <summary>
        /// Serialize a subobject.
        /// Creates a new reference-entry in the Results list.
        /// It will break down subObject to primitive fields or to subsubObject references.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        protected UInt32 Serialize(Type type, Object value)
        {
            // if subobject already serialized then return with id.
            // solves circular references in object graph.
            var result = Results.FirstOrDefault(r => r.value == value);
            if (result != null)
                return result.Id;

            // new subObject entry
            lock (lockObject)
                result = new USerializedResult()
                {
                    Id = ++newObjectId,
                    value = value
                };

            // serialize type of subObject
            result.TypeDefinition = SerializeType(type);

            // prepare all fields for serialization
            var FieldWrappers = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name).ToList();

            // helper method to create byte array from a primitive field or a subsubObject-reference 
            var serializeObj = new Func<Type, Object, Byte[]>((t, v) =>
            {
                if (PrimitiveTypeSerializer.ContainsKey(t))
                    return PrimitiveTypeSerializer[t](v);
                return BitConverter.GetBytes(Serialize(t, v));
            });

            // Arrays must be handled specially
            if (!type.IsArray)
                // No array. Serialize all fields - either by reference, or by value
                result.SerialData = FieldWrappers.SelectMany(f => serializeObj(f.FieldType, f.GetValue(value))).ToArray();
            else
            {
                // Arrays must be handled differently, since the array entries are not fields, still contain information needed to serialize
                var array = ((Array)value);
                var list = new List<Byte>();
                // Serialize array length
                list.AddRange(BitConverter.GetBytes((UInt32)array.Length));

                // Serialize all entry of the array - will be either reference or primitive
                foreach (var obj in array)
                    list.AddRange(serializeObj(type.GetElementType(), obj));

                // assemble serialized array
                result.SerialData = list.ToArray();
            }

            // complete subObject entry
            Results.Add(result);

            // return Id for reference purposes
            return result.Id;
        }

        /// <summary>
        /// Returns an array of integers to identify exact type.
        /// Type serialization suffers from generic types
        /// Generic base type gets one Id.
        /// Complex generic type created by listing up all the id-s what build up the type
        /// * 1 = List<T>
        /// * 2 = int
        /// * 1, 2 = List<int>
        /// </summary>
        /// <param name="type">Complex generic type</param>
        /// <returns>Id list</returns>
        protected List<UInt32> SerializeType(Type type)
        {
            // not generic. Simple.
            if (!type.IsGenericType)
                return new List<UInt32>() { typeCache[type] };

            // it is generic type. Type list must be generated recursively
            var result = new List<UInt32>();

            // first the Id of the base generic class
            result.Add(typeCache[type.GetGenericTypeDefinition()]);

            // then generate Id list for all the builder types
            foreach (var generic in type.GetGenericArguments())
                result.AddRange(SerializeType(generic));

            return result;
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
            // Backward search on the type dictionary for the baseType
            var baseType = typeCache.First(t => t.Value == typeIdList[0]).Key;

            // Get rest of the Id-s and create a typeList. This will be the generic list.
            // prepare for recursion.
            // recursion is a must, since the generic types can be nicely nested.
            var restTypeIdList = typeIdList.Skip(1).ToList();
            var parameterTypes = new List<Type>();

            // BaseType knows how many generic parameters we are waiting for. Just get them in order.
            // This foreach is only to count.
            // Could type order vary? No. It is fix. Field order varies sometimes,
            // but Generic Type order is always strictly the same as defined in code.
            foreach (var type in baseType.GetGenericArguments())
            {
                // Subtype creation. Recursive.
                var sub = DeserializeType(restTypeIdList);
                parameterTypes.Add(sub.Item1);

                // return value shortens the Id list.
                // Must. Not. Parallelize. !!!
                restTypeIdList = sub.Item2;
            }

            // Generic SubType Created. Woho!!!
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
