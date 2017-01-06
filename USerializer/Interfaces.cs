using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace KS.USerializer
{
    public interface ITypeCacheEntry
    {
        uint Id { get; }
        bool IsGeneric { get; }
        Type BaseType { get; }
        List<FieldInfo> Fields { get; }
        bool IsArray { get; }

    }

    public interface ITypeCache
    {
        ITypeCacheEntry GetTypeCacheEntry(Type type);
        ITypeCacheEntry GetTypeCacheById(uint v);
    }

    public interface IFastType : IDisposable
    {
        Type RealType { get; }
        ITypeCacheEntry Cache { get; }
        List<FieldInfo> Fields { get; }
        IEnumerable<TypedObject> GetSubObjects(object mainObject);
    }

    public interface ITypeSerializer
    {
        ITypeCache TypeCache { get; }
        IFastType GetFastType(Type type);
        IEnumerable<uint> GetComplexTypeId(IFastType fastType);
    }

    public interface IResultEntry
    {
        List<uint> TypeDefinition { get; set; }
        uint Id { get; set; }
        byte[] SerialData { get; set; }
        object Value { get; set; }
    }

    public interface IObjectSerializer
    {
        ITypeSerializer TypeSerializer { get; }
        List<IResultEntry> Serialize();
    }
}
