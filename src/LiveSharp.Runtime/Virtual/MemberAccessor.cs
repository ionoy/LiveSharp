namespace LiveSharp.Runtime
{
    public struct VirtualMemberAccessor<T>
    {
        public T this[object instance, VirtualFieldInfo info] {
            get {
                return (T)info[instance];
            }
            set {
                info[instance] = value;
            }
        }

        public T this[object instance, VirtualPropertyInfo info] {
            get {
                return (T)info[instance];
            }
            set {
                info[instance] = value;
            }
        }
    }
}