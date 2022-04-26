using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using XamarinFormsTest.Models;
using XamarinFormsTest.Views;
using XamarinFormsTest.ViewModels;
using System.Diagnostics;

namespace XamarinFormsTest.Views
{
    // Learn more about making custom code visible in the Xamarin.Forms previewer
    // by visiting https://aka.ms/xamarinforms-previewer
    [DesignTimeVisible(false)]
    public partial class ItemsPage : ContentPage
    { 
        ItemsViewModel viewModel;

        public ItemsPage()
        {
            InitializeComponent();

            BindingContext = viewModel = new ItemsViewModel();
        }

        public void Build()
        {
            var label = new Label {
                Text = "Hello, world1",
                Style = Styles.LabelStyle
            };
            
            Content = label;
        }
        async void OnItemSelected(object sender, SelectedItemChangedEventArgs args)
        {
            var item = args.SelectedItem as Item;
            if (item == null)
                return;

            await Navigation.PushAsync(new ItemDetailPage(new ItemDetailViewModel(item)));

            // Manually deselect item.
            ItemsListView.SelectedItem = null;
        }

        async void AddItem_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new NavigationPage(new NewItemPage()));
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            //
            //viewModel.LoadItemsCommandNext.Execute(null);
        }
    }
    
    class Styles
    {
        public static Style LabelStyle => new Style(typeof(Label)) {
            Setters = {
                new Setter() {
                    Property = Label.TextColorProperty,
                    Value = Color.Red
                }
            }
        };
    }
}