using System;
using System.ComponentModel;
using System.Diagnostics;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace XamarinFormsTest.Views
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class AboutPage : ContentPage
    {
        public const double _someDouble = 8;
        public AboutPage()
        {
            InitializeComponent();  
            Build();
        }

        public void Build()
        {
            Debug.WriteLine("bbbb");
            BindedLabel.Text = "double: " + _someDouble;
        }
    }
}