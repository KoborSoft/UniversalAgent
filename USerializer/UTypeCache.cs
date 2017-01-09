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
        public Type TemplateType { get; protected set; }
        public List<FieldInfo> CachedFields { get; protected set; }
        public bool IsArray { get; protected set; }

        public TypeCacheEntry(Type type, uint id)
        {
            Id = id;
            IsGeneric = type.IsGenericType;
            IsArray = type.IsArray;
            TemplateType = type;

            if (!IsGeneric)
                CachedFields = Utils.GetAllInstanceFields(TemplateType);
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
            Type templateType = Utils.GetTemplateType(type);
            var typeCache = GetTypeCache(templateType) ?? RegisterTypeCache(templateType);
            return new FastType(type, typeCache);
        }

        protected ITypeCacheEntry GetTypeCache(Type templateType)
        {
            return (typeCache.ContainsKey(templateType)) ? typeCache[templateType] : null;
        }

        protected ITypeCacheEntry RegisterTypeCache(Type templateType)
        {
            var typeCache = new TypeCacheEntry(templateType, GetNewTypeId());
            this.typeCache[templateType] = typeCache;
            return typeCache;
        }

        public ITypeCacheEntry GetTypeCacheById(uint typeId)
        {
            return typeCache.Values.First(tc => tc.Id == typeId);
        }
    }
}
