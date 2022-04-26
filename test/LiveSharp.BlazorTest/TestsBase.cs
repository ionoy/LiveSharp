using Bunit;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Index = LiveSharp.BlazorTest.App.Pages.Index;

namespace LiveSharp.BlazorTest
{
    public class TestsBase
    {
        private const string RootDirectory = @"..\..\..\..\LiveSharp.BlazorTest.App\";
        private static readonly string ProjectDirectory = Path.Combine(Environment.CurrentDirectory, RootDirectory);
        
        protected void TestComponentUpdate<TComponent>(string filename, 
            Func<IRenderedComponent<TComponent>, bool> before, 
            Func<IRenderedComponent<TComponent>, bool> after) 
                where TComponent : IComponent
        {
                using var ctx = new TestContext();

                TestUpdate(filename,
                    () => {
                        
                        var componentBefore = ctx.RenderComponent<TComponent>();
                        if (!before(componentBefore))
                            throw new Exception("Before test failed with rendered markup: " + componentBefore.Markup);
                    },
                    () => {
                        var componentAfter = ctx.RenderComponent<TComponent>();
                        if (!after(componentAfter))
                            throw new Exception("After test failed with rendered markup: " + componentAfter.Markup);
                    });
        }

        protected void TestCodeUpdate(string filename, Func<bool> before, Func<bool> after)
        {
            TestUpdate(filename,
                () => {
                    if (!before())
                        throw new Exception("Before test failed");
                },
                () => {
                    if (!after())
                        throw new Exception("After test failed");
                });
        }

        private void TestUpdate(string filename, Action before, Action after)
        {
            try {
                filename = Path.Combine(ProjectDirectory, filename);

                NUnit.Framework.TestContext.Progress.WriteLine($"Testing before {filename} update");
                before();
                NUnit.Framework.TestContext.Progress.WriteLine($"Before test passed");
                
                NUnit.Framework.TestContext.Progress.WriteLine($"Writing changes to {filename}");
                
                ServerProcess.Instance.DoAndWaitForOutput(
                    () => File.WriteAllText(filename, File.ReadAllText(filename + ".update")), 
                    "Received code update");

                NUnit.Framework.TestContext.Progress.WriteLine($"Testing after {filename} update");
                after();
                NUnit.Framework.TestContext.Progress.WriteLine($"After test passed");

            } finally {
                NUnit.Framework.TestContext.Progress.WriteLine($"Reverting {filename} to original");
                
                var originalText = File.ReadAllText(filename + ".original");
                
                ServerProcess.Instance.DoAndWaitForOutput(
                    () => File.WriteAllText(filename, originalText),
                    "Received code update");
            }
        }
    }
}
