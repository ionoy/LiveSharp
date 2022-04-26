using System;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorWorkbench.Data
{
    public class WeatherForecastService
    {
        public string ServiceName { get; set; } = "This is a weather forecast service!";
        public string ServiceNssasasm2e { get; set; } = "This is a weather forecast service!";
        
        private static readonly string[] Summaries = new[] {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        public Task<WeatherForecast[]> GetForecastAsync(DateTime startDate)
        {
            var rng = new Random();
            return Task.FromResult(Enumerable.Range(1, 5).Select(index => new WeatherForecast {
                Date = startDate.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = "good"
            }).ToArray());
        }
    }

    class A
    {
        public void M(B b)
        {
            b.Call(this);
        }
        public void M0(B b)
        {
            b.Call(this);
        }

        public void Call(B b0)
        {
            
        }
        public void Z(B b)
        {
            var a = Enumerable.Range(0, 10).Select(i => i * 42).Sum();
            b.Call(this);
        }
    }

    class B
    {
        public void Call(A a)
        {
            
            a.Call(this);
            a.M0(this);
        }
        
        public void Call2(A a)
        {
            a.Call(this);
            a.Z(this);
        }
    }
}