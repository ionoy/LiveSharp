namespace BlazorWorkbench.Data
{
    public class GenericType
    {
        public static string GenericMethod<T>(T value)
        {
            return value?.ToString();
        }
    }
    
    public class GenericType2<T>
    {
        public static string GenericMethod<T, T2>(T val1, T2 val2) 
        {
            return val1?.ToString() + " " + val2?.ToString();
        }
    }
}