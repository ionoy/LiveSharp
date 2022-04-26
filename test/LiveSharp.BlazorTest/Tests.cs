using LiveSharp.BlazorTest.App.Data;
using NUnit.Framework;
using System;
using Index = LiveSharp.BlazorTest.App.Pages.Index;

namespace LiveSharp.BlazorTest
{
    public class Tests : TestsBase
    {
        [Test]
        public void TestIndexPageUpdate()
        {
            TestComponentUpdate<Index>(@"Pages\Index.razor",
                indexPage => indexPage.Markup.Contains("<h1>Hello, world!</h1>"),
                indexPage => indexPage.Markup.Contains("<h1>Hello, world from LiveSharp!</h1>"));
        }

        [Test]
        public void TestPropertyGetterUpdate()
        {
            var temperatureC = 10;
            var weatherForecast = new WeatherForecast {TemperatureC = temperatureC};

            TestCodeUpdate(@"Data\WeatherForecast.cs",
                () => 
                    weatherForecast.TemperatureF == (32 + (int)(temperatureC/ 0.5556)),
                () => 
                    weatherForecast.TemperatureF == (320 + (int)(temperatureC / 0.5556)));
        }
    }
}