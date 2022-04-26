using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace XamarinWorkbench
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            Build();
        }

        public void Build()
        {
            Content = new Label {
                Text = "Hello, World!"
            };
        }
            
    }
}