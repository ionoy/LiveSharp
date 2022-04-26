using System;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading.Tasks;

namespace LiveSharp.RuntimeTests
{
    public partial class NewMembers : TestsBase
    {
        public event EventHandler<string> TestEvent;
        
        private static async Task<T> FromResultWithDelay<T>(T val)
        {
            await Delay(1);
            return val;
        }
        
        private static Task Delay(int milliseconds)
        {
            Log.WriteLine("Delay called");
            return Task.Delay(milliseconds);
        }
    }
    
    public struct NewMembersStruct
    {
        
    }
}