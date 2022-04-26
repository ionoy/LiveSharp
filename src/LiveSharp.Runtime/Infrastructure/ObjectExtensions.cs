using System;

namespace LiveSharp.Runtime.Infrastructure
{
    public static class ObjectExtensions
    {
        public static T[] Append<T>(this T obj, T[] array)
        {
            var newArray = new T[array.Length + 1];

            newArray[0] = obj;
            
            Array.Copy(array, 0, newArray, 1, array.Length);
            
            return newArray;
        }
        
        public static T[] Append<T>(this T[] array, T obj)
        {
            var newArray = new T[array.Length + 1];

            newArray[newArray.Length - 1] = obj;
            
            Array.Copy(array, newArray, array.Length);
            
            return newArray;
        }
    }
}