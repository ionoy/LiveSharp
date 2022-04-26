using System;
using System.Linq;
using System.Reflection;
using LiveSharp;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

[assembly:LiveSharpInjectRuleBaseClass("Xamarin.Forms.ContentPage", "Xamarin.Forms.ContentView")]

namespace Workbench.XamarinForms
{
	public partial class App : Application
	{
        [LiveSharpStart]
		public App ()
		{
			InitializeComponent();

			MainPage = new MainPage();


		    LiveSharpContext.AddUpdateHandler(ctx => {
		        var instances = ctx.UpdatedMethods
		            .SelectMany(method => method.Instances)
		            .Distinct()
		            .ToArray();

		        Device.BeginInvokeOnMainThread(() => {
		            foreach (var instance in instances) {
		                try {
		                    var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
		                    if (instance is ContentPage page && instance.GetType().GetMethod("BuildContent", bindingFlags) is MethodInfo mi)
		                        page.Content = (View)mi.Invoke(instance, null);
		                    else if (instance is ContentView view && instance.GetType().GetMethod("BuildContent", bindingFlags) is MethodInfo mi2)
		                        view.Content = (View)mi2.Invoke(instance, null);
		                } catch (TargetInvocationException e) {
		                    var inner = e.InnerException;
		                    while (inner is TargetInvocationException tie)
		                        inner = tie.InnerException;
		                    if (inner != null)
		                        throw inner;
		                    throw;
		                }
		            }
		        });
		    });
        }
	}
}
