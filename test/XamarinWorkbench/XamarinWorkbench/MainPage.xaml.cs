using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Runtime.CompilerServices;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Xamarin.Forms;
using Xamarin.Forms.Markup;
using Xamarin.Forms.Markup.LeftToRight;
using XamarinWorkbench.Annotations;

namespace XamarinWorkbench
{
    public partial class MainPage : ContentPage, IViewFor<AuditOverviewViewModel>
    {
        private Label _auditType, _location, _dueDate;

        public MainPage()
        {
            BindingContext = new Model("b");
            InitializeComponent();
            Custom();
        }

        private string DueDate(DateTimeOffset? date)
        {
            if (date == null)
                return "";

            return $"{date.Value:D} ({date.Value})";
        }

        private void Custom()
        {
            Title = "Audit Overview23sdaf";
            BackgroundColor = Color.Red;
            var model = new Model("a");

            Content = new StackLayout
            {
                BackgroundColor = Color.Blue,
                Children =
                {
                    BuildHeader(model),

                    BuildContent(),
                }
            };
        }

        private View BuildContent()
        {
            return new ContentView {
                BackgroundColor = Color.Green, Margin = new Thickness(5, 0, 5, 0), Padding = new Thickness(10)
            };
        }

        private View BuildHeader(Model model)
        {
            return new ContentView {
                //BackgroundColor = Color.Black,
                Margin = new Thickness(5, 0, 5, 0),
                Padding = new Thickness(10),
                Content = new StackLayout {
                    Children = {
                        // Audit type
                        new Label()
                            .Bold()
                            .Assign(out _auditType),

                        // info row
                        new Grid {
                            ColumnDefinitions = GridRowsColumns.Columns.Define(GridLength.Auto, GridLength.Star),
                            Children = {
                                // icon
                                new Label {Text = "a", TextColor = Color.Purple,}
                                    .Column(0)
                                    .TextCenterVertical(),

                                // due date
                                new Label {
                                        Text = model.Name
                                    }
                                    .Column(1)
                                    .TextCenterVertical()
                                    .Assign(out _dueDate),

                                // location
                                new Label {
                                        TextColor = Color.White
                                    }
                                    .Assign(out _location),
                            }
                        },

                        // progress row
                        new Grid {
                            ColumnDefinitions = GridRowsColumns.Columns.Define(GridLength.Auto, GridLength.Star, GridLength.Auto),
                            Children = {
                            }
                        },

                        // action row
                        new Grid {ColumnDefinitions = GridRowsColumns.Columns.Define(GridLength.Auto), Children = { }}
                    }
                }
            };
        }

        object IViewFor.ViewModel {
            get => ViewModel;
            set => ViewModel = (AuditOverviewViewModel) value;
        }

        public AuditOverviewViewModel ViewModel { get; set; }
    }

    public class AuditOverviewViewModel
    {
        public AuditOverviewViewModel()
        {
        }

        [Reactive]
        public Guid AuditId { get; set; } = Guid.Empty;// todo: come from routing or view or ...

        [Reactive]
        public string AuditTypeName { get; private set; }

        [Reactive]
        public DateTimeOffset? DueDate { get; private set; }

        [Reactive]
        public string LocationName { get; private set; }

        [Reactive]
        public string Owner { get; private set; }

        [Reactive]
        public DateTimeOffset StartDate { get; private set; }
    }

}