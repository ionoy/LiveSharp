using System;
using LiveSharp.RuntimeTests.Helpers;

namespace LiveSharp.RuntimeTests.Tests
{
    public class TypeResolutionTests : TestsBase
    {
        public void Test10()
        {
            var v = new View();
            var viewModel = new VM();
            
            v.OneWayBind(viewModel, vm => vm.String, v => v.String);
            v.OneWayBind(viewModel, vm => vm.String, v => v.String);
            v.OneWayBind(viewModel, vm => vm.DateTimeOffset, v => v.String, o => o.ToString());
            v.OneWayBind(viewModel, vm => vm.String, v => v.String);
            v.OneWayBind(viewModel, vm => vm.DateTimeOffset, v => v.String, o => o.ToString());
            v.OneWayBind(viewModel, vm => vm.String, v => v.String);
            v.OneWayBind(viewModel, vm => vm.DateTimeOffset, v => v.String, o => o.ToString());
        }
        
        
    }

    class View
    {
        public string String { get; set; }
    }

    class VM
    {
        public string String { get; set; }
        public DateTimeOffset? DateTimeOffset { get; set; }
    }
}