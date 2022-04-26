using System.Linq;
using System.Reflection;

namespace LiveSharp.RuntimeTests.Infrastructure
{
    public static class ReflectionExtensions
    {
        public static string GetMethodIdentifier(this MethodInfo methodInfo)
        {
            var parameters = methodInfo.GetParameters();
            var parameterString = string.Join(" ", parameters.Select(p => p.ParameterType.FullName));
            return
                $"{methodInfo.DeclaringType.FullName} {methodInfo.Name} {parameterString}";
        }
    }
}