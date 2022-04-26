using System;
using System.Diagnostics;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace XamarinFormsSample.Views
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class AboutPage : ContentPage
	{
		public AboutPage ()
		{
			InitializeComponent ();

		    Build();
		}

        public void Build()
        {
            Debug.WriteLine("Build called");  
            Content = Builder.BuildLabel();
        }
	}
     
    class Builder
    {
        public static View BuildLabel()
        {
            Debug.WriteLine("BuildLabel called"); 
            return new Label {
                Text = new string('a', 10)
            };
        }
    }
}  