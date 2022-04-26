using System;
using System.Diagnostics;
using System.Windows.Input;

using Xamarin.Forms;

namespace XamarinFormsTest.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        public AboutViewModel()
        {
            Title = "About";

            OpenWebCommand = new Command(() => {
                Debug.WriteLine("hello");
                NewMethod();
            });
        }

        private void NewMethod()
        {
            Debug.WriteLine("hello from new method 222");
        }

        public ICommand OpenWebCommand { get; }

        public string OtherProp {
            get {
                return "sdfsdf1s21";
            }
            set {
                Debug.WriteLine(value);
            }
        }
    }
}