using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KS.USerializer
{
    public interface ITypeCache
    {
        TypeCacheEntry GetTypeCacheEntry(Type type);
        TypeCacheEntry GetTypeCacheById(uint v);
    }

    public interface ITypeSerializer
    {
        ITypeCache TypeCache { get; }
        FastType GetFastType(Type type);
        IEnumerable<uint> GetComplexTypeId(FastType fastType);
    }

    public interface IObjectSerializer
    {
        ITypeSerializer TypeSerializer { get; }
        List<ResultEntry> Serialize();
    }
}
