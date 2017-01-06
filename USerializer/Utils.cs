using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace KS.USerializer
{
    public static class Utils
    {
        public static Type GetBaseType(Type type)
        {
            return type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        }

        public static List<FieldInfo> GetAllInstanceFields(Type type)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name).ToList();
        }
    }

}
