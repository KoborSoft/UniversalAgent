using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KS.USerializer
{
    /// <summary>
    /// TODO On all levels. Wrong. Sketch. Under construction.
    /// </summary>
    public class UObjectDeserializer
    {
        private IEnumerable<byte> serializedObject;
        public ITypeSerializer TypeSerializer { get; protected set; }

        public UObjectDeserializer(ITypeSerializer typeSerializer, IEnumerable<byte> serializedObject)
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
        protected Tuple<Type, List<uint>> DeserializeType(List<uint> typeIdList)
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
            foreach (var type in baseType.TemplateType.GetGenericArguments())
            {
                // Subtype creation. Recursive.
                var sub = DeserializeType(restTypeIdList);
                parameterTypes.Add(sub.Item1);

                // return value shortens the Id list.
                // Must. Not. Parallelize. !!!
                restTypeIdList = sub.Item2;
            }

            // Generic SubType Created. Woho!!!
            var result = baseType.TemplateType.MakeGenericType(parameterTypes.ToArray());
            return new Tuple<Type, List<uint>>(result, restTypeIdList);
        }

        internal object Deserialize()
        {
            throw new NotImplementedException();
        }
    }
}
