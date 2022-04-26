using System.Linq.Expressions;
using System.Reflection;

namespace LiveSharp.Runtime
{
    class MethodRefAccess
    {
        public Expression Target { get; set; }
        public MethodInfo Method { get; set; }

        public MethodRefAccess(Expression target, MethodInfo method)
        {
            Target = target;
            Method = method;
        }
    }
}