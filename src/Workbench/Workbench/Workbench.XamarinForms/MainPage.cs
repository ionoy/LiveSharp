using System;
using Xamarin.Forms;

namespace Workbench.XamarinForms
{
    public class MainPage : ContentPage
    {
        public MainPage()
        {
            Content = BuildContent();
        }

        public View BuildContent()
        {
            var interval = 300;
            var field = new AbsoluteLayout();
            var rnd = new Random();

            Device.StartTimer(TimeSpan.FromMilliseconds(interval), createNewButton);

            bool createNewButton()
            {
                var button = new Button {
                    WidthRequest = 30,
                    HeightRequest = 30,
                    CornerRadius = 15,
                    BackgroundColor = Color.Black
                };
                field.Children.Add(button, new Point(rnd.Next() % Width, rnd.Next() % Height));
                return true;
            }

            return field;
        }
    }
}