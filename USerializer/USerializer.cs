using System;
using System.Collections.Generic;
using System.Linq;

namespace KS.USerializer
{
    public class USerializer
    {
        public ITypeSerializer TypeSerializer { get; protected set; } = new UTypeSerializer(new UTypeCache());
        public IEnumerable<byte> Serialize<T>(T value) => Serialize(typeof(T), value);
        public IEnumerable<byte> Serialize(Type type, object value)
        {
            var result = new List<byte>();
            var resultEntries = new UObjectSerializer(TypeSerializer, new TypedObject(type, value)).Serialize();

            result.AddRange(BitConverter.GetBytes((uint)resultEntries.Count));
            foreach (var resultEntry in resultEntries)
            {
                result.AddRange(resultEntry.TypeDefinition.SelectMany(td => BitConverter.GetBytes(td)));
                result.AddRange(BitConverter.GetBytes(resultEntry.Id));
                result.AddRange(BitConverter.GetBytes((uint)resultEntry.SerialData.Length));
                result.AddRange(resultEntry.SerialData);
            }
            return result;
        }

        public object Deserialize(IEnumerable<byte> serializedObject)
        {
            return new UObjectDeserializer(TypeSerializer, serializedObject).Deserialize();
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
