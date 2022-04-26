namespace LiveSharp.Common
{
    public class InjectedMethodInfo
    {
        public string ProjectName { get; set; }
        public string ContainingTypeName { get; set; }
        public string MethodName { get; set; }
        public string Parameters { get; set; }
        public int Id { get; set; }

        public string GetMethodIdentifier(bool includeTypeName = true)
        {
            if (includeTypeName)
                return $"{ContainingTypeName} {MethodName} {Parameters}";
            else
                return $"{MethodName} {Parameters}";
        }
    }
}