using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KS.USerializer
{
    public class TypeCacheEntry : ITypeCacheEntry
    {
        public uint Id { get; protected set; }
        public bool IsGeneric { get; protected set; }
        public Type BaseType { get; protected set; }
        public List<FieldInfo> Fields { get; protected set; }
        public bool IsArray { get; protected set; }

        public TypeCacheEntry(Type type, uint id)
        {
            Id = id;
            IsGeneric = type.IsGenericType;
            IsArray = type.IsArray;
            BaseType = type;

            if (!IsGeneric)
                Fields = Utils.GetAllInstanceFields(BaseType);
        }
    }

    public class UTypeCache : ITypeCache
    {
        protected object lockObject = new object();
        protected uint newTypeId = 0;
        protected Dictionary<Type, ITypeCacheEntry> typeCache = new Dictionary<Type, ITypeCacheEntry>();

        protected uint GetNewTypeId()
        {
            lock (lockObject)
                return ++newTypeId;
        }

        public ITypeCacheEntry GetTypeCacheEntry(Type type)
        {
            Type BaseType = Utils.GetBaseType(type);
            if (typeCache.ContainsKey(BaseType))
                return typeCache[BaseType];
            var baseTypeCache = new TypeCacheEntry(BaseType, GetNewTypeId());
            typeCache[BaseType] = baseTypeCache;
            return baseTypeCache;
        }

        public ITypeCacheEntry GetTypeCacheById(uint typeId)
        {
            return typeCache.Values.First(tc => tc.Id == typeId);
        }
    }
}
