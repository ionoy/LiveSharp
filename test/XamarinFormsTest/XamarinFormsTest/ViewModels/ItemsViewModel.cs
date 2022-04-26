using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Xamarin.Forms;

using XamarinFormsTest.Models;
using XamarinFormsTest.Views;

namespace XamarinFormsTest.ViewModels
{
    class ItemWithINPC : BaseViewModel
    {
        public ItemWithINPC()
        {
            var a = "a";
        }
    }
    
    public class ItemsViewModel : BaseViewModel
    {
         
        public ObservableCollection<Item> Items { get; set; }
        public Command LoadItemsCommand { get; set; }
        public string ButtonText => "add1322";
        public string ButtonText6 => "six1334aa";
        public ItemsViewModel()
        {
            Title = "";
            Items = new ObservableCollection<Item>();

            LoadItemsCommand = new Command(async () => await ExecuteLoadItemsCommand());
            LoadItemsCommand.Execute(null);
            
            MessagingCenter.Subscribe<NewItemPage, Item>(this, "AddItem", async (obj, item) => {
                var newItem = item as Item;
                Items.Add(newItem);
                await DataStore.AddItemAsync(newItem);
            });
        }

        async Task ExecuteLoadItemsCommand()
        {
            if (IsBusy)
                return;

            IsBusy = true;
 
            Items.Clear();
            var items = await DataStore.GetItemsAsync(true);

            foreach (var item in items) {
                Items.Add(item);
            }

            Title = "hiazz";
            
            await Task.Delay(1200);
            
            IsBusy = false;
        }
    }
}