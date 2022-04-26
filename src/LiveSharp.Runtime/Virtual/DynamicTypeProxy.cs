using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Xml.Linq;
using LiveSharp.Runtime.Virtual;

namespace LiveSharp.Runtime
{
    internal class DynamicTypeProxy
    {
        static readonly ConcurrentDictionary<string, object> Members = new ConcurrentDictionary<string, object>();
        static readonly ConcurrentDictionary<object, object> StaticWrapperCache = new ConcurrentDictionary<object, object>();

        static readonly ConcurrentQueue<Type> DynamicTypes = new ConcurrentQueue<Type>(new [] {
            typeof(VirtualType0), typeof(VirtualType0), typeof(VirtualType0), typeof(VirtualType0), typeof(VirtualType0),
            typeof(VirtualType0), typeof(VirtualType0), typeof(VirtualType0), typeof(VirtualType0), typeof(VirtualType0) });
        static readonly ConcurrentDictionary<Type, string> DynamicTypeMap = new ConcurrentDictionary<Type, string>();
        
        public Type GetDynamicType(string typeName)
        {
            return typeof(VirtualType0);
        }
    }
}