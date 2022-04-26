using System.Collections.Concurrent;

namespace LiveSharp.Runtime
{
    public class MemberValueByInstanceMap
    {
        protected static readonly object NullObject = new object();
        protected readonly string _memberName;
        protected readonly ConcurrentDictionary<object, object> _membersByInstance = new ConcurrentDictionary<object, object>();

        public MemberValueByInstanceMap(string memberName) => _memberName = memberName;

        public object GetValue(object instance)
        {
            return _membersByInstance.GetOrAdd(instance ?? NullObject, _ => null);
        }

        public void SetValue(object instance, object value)
        {
            _membersByInstance[instance ?? NullObject] = value;
        }
    }

    class MemberValueByInstanceMapStaticWrapper<T>
    {
        private MemberValueByInstanceMap _map;

        public MemberValueByInstanceMapStaticWrapper(MemberValueByInstanceMap map)
        {
            _map = map;
        }

        public T this[object instance]
        {
            get => (T)_map.GetValue(instance);
            set => _map.SetValue(instance, value);
        }
    }
}