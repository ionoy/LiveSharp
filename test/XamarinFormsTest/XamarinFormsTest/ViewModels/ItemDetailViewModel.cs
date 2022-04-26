using System;
using System.Diagnostics;
using XamarinFormsTest.Models;

namespace XamarinFormsTest.ViewModels
{
    public class ItemDetailViewModel : BaseViewModel
    {
        private Item _item;

        public Item Item {
            get => _item;
            set {
                Debug.WriteLine("item changed1");
                _item = value;
            }
        }

        public ItemDetailViewModel(Item item = null)
        {
            Title = item?.Text;
            Item = item;
        }
    }
}
