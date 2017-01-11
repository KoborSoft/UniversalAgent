using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Serializer
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            var ser = new ObjectSerializer<string>();
            ser.Register(t => true, (t, o) => SerialFormatter(t, o));
            var result = ser.Serialize(new TcpClient());
        }

        private string SerialFormatter(Type t, object o)
        {
            throw new NotImplementedException();
        }
    }

    internal class ObjectSerializer<TResult>
    {
        public ObjectSerializer()
        {
        }

        protected List<Tuple<Func<Type, bool>, Func<Type, object, TResult>>> list = new List<Tuple<Func<Type, bool>, Func<Type, object, TResult>>>();
        public void Register(Func<Type, bool> predicate, Func<Type, object, TResult> formatter)
        {
            list.Add(new Tuple<Func<Type, bool>, Func<Type, object, TResult>>(predicate, formatter));
        }

        public TResult Serialize<T>(T o)
        {
            return list.FirstOrDefault(l => l.Item1(typeof(T))).Item2(typeof(T), o);
        }
    }
}
