using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KS.USerializer
{
    public class TypeCacheEntry
    {
        public uint Id;
        public bool IsGeneric;
        public Type BaseType;
        public List<FieldInfo> Fields;
        public bool IsArray;

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
        protected Dictionary<Type, TypeCacheEntry> typeCache = new Dictionary<Type, TypeCacheEntry>();

        protected uint GetNewTypeId()
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
}
