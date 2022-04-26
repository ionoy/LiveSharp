using LiveSharp.BlazorTest.App.Data;
using NUnit.Framework;
using System;
using System.Text;
using System.Threading;
using Index = LiveSharp.BlazorTest.App.Pages.Index;

namespace LiveSharp.BlazorTest
{
    [SetUpFixture]
    public class SetUp
    {
        private WeatherForecast _temp;
        private ServerProcess _serverProcess;

        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            try {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            
                _serverProcess = ServerProcess.Instance;
            
                _serverProcess.DoAndWaitForOutput(() => _serverProcess.Start(), "Welcome to LiveSharp!");
            
                Thread.Sleep(1000);
            
                _serverProcess.DoAndWaitForOutput(() => _temp = new WeatherForecast(), "Finished: Initial compilation");
            } catch (Exception e) {
                Console.WriteLine(e);
                _serverProcess?.Dispose();
            }
        }

        [OneTimeTearDown]
        public void RunAfterAnyTests()
        {
            _serverProcess.Dispose();
        }
    }
}