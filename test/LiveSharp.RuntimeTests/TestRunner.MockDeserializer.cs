using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Forms.Internals;

namespace LiveSharp.RuntimeTests
{
    public partial class TestRunner
    {
        internal class MockDeserializer : IDeserializer
        {
            public Task<IDictionary<string, object>> DeserializePropertiesAsync()
            {
                return Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>());
            }

            public Task SerializePropertiesAsync(IDictionary<string, object> properties)
            {
                return Task.FromResult(false);
            }
        }
    }
}