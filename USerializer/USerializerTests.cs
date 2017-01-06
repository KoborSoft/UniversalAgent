using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using Xunit;

namespace KS.USerializer
{
    public interface TestInterface<out R> where R : new()
    {
        R RProperty { get; }
    }
    public struct TestStruct<T, U> : TestInterface<U> where U : new()
    {
        public T TValue;
        public U UValue;

        public U RProperty { get; }

        public T TProperty { get; }
        public U UProperty { get; set; }
    }


    public class TestClass<T, U> where T : new() where U : new()
    {
        public T TValue = new T();
        public U UValue = new U();
        public T TProperty { get; } = new T();
        public U UProperty { get; set; } = new U();
    }

    public class InheritedClass<T, U> : TestClass<T, TcpClient>, TestInterface<U> where U : new() where T : new()
    {
        public U RProperty { get; } = new U();
    }

    public class USerializerTests
    {
        public static USerializer serializer = new USerializer();
        public USerializerTests()
        {
        }

        [Theory]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(List<string>))]
        [InlineData(typeof(TcpClient))]
        [InlineData(typeof(List<InheritedClass<TestClass<int, int>, TestStruct<int, int>>>))]
        [InlineData(typeof(InheritedClass<TestClass<List<int>, int>, TestStruct<int, int>>))]
        public void TestMethod1(Type type)
        {
            var obj = type.GetConstructor(new Type[] { }).Invoke(new Object[] { });
            //foreach (var i in Enumerable.Range(0, 10000)) {
            var serObj = serializer.Serialize(type, obj);
            //}
        }

        [Fact]
        public void TestMethod2()
        {
            var obj = new List<int>() { 1, 2, 3, 4, 5, 6, 7 };
            var serObj = serializer.Serialize(obj);
        }
    }
}
