using System;
using System.Collections.Generic;
using System.Text;

namespace LiveSharp.RuntimeTests.Tests
{
    class PropertyTests : TestsBase
    {
        public List<int> List => new List<int>();
        public List<int> List2 { get { return new List<int>(); } }
    }
}
