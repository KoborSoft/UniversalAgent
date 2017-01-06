using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KS.USerializer
{
    public class TypedObject
    {
        public Type type { get; protected set; }
        public object Value { get; protected set; }
        public TypedObject(Type type, object value) { this.type = type; Value = value; }
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

    public class UTypeSerializer : ITypeSerializer
    {
        public ITypeCache TypeCache { get; protected set; }

        public UTypeSerializer(ITypeCache cache) { TypeCache = cache; }

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
}
