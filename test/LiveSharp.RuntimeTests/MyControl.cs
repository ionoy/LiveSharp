using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Windows.Input;
using CSharpForMarkup;
using System.Diagnostics;
using Xamarin.Forms;
using Xamarin.Forms.Internals;

namespace LiveSharp.RuntimeTests
{
    public class MyControlBase : ContentPage
    {
        protected int _intFromBase = 10;
        protected static int _staticIntFromBase = 20;
        public int AssertCallCount { get; private set; }
    }

    public class MyControl : MyControlBase
    {
        private int fieldNameMargin = 7;
        private Thickness _fieldMargin = new Thickness(20);
        private Dictionary<string, int> _dictionary = new Dictionary<string, int>();

        static double TestSize = 20;

        public Action Action { get; set; }
        public Func<bool> FuncBool { get; set; }
        public EventHandler EventHandler { get; set; }
        public EventHandler<bool> EventHandlerBool { get; set; }

        private string Method(string v) => v;
        private bool Method() => true;
        private bool StaticMethod() => true;
        private T GenericMethod<T>(T val) => val;
        private T GenericMethod2<T>()
        {
            Debug.WriteLine("AAAAAAAAAAA");
            return default;
        }

        public string StringProperty { get; set; }

        public void Test0()
        {
            var s = "";
            s.ToString(CultureInfo.CurrentCulture);

            var obj = new RowDefinition {Height = 10};
        }

        public void Test1()
        {
            Content = new Grid();
        }

        public void Test2()
        {
            Content = new Grid() {RowSpacing = 10};
        }

        public void Test3()
        {
            Content = new Grid() {RowDefinitions = {new RowDefinition() {Height = 10}}};
        }

        public void Test4()
        {
            var vm = new MyControlViewModel(1, "a");

            Content = new Label() {Text = nameof(vm.RegistrationCode)};
        }

        public void Test5()
        {
            var i = 10;

            Content = new Label() {Text = i % 2 == 0 ? "Even" : "Odd"};
        }

        public void Test6()
        {
            IsVisible = true;
            Content = new Label() {IsVisible = false, Text = "a" + 'b'};
        }

        public void Test7()
        {
            //IsVisible = true;
            //Obj = "".IndexOf('a');
            Content = new Label {
                IsVisible = Method(),
                IsEnabled = StaticMethod(),
                Text = Method("a"),
                FontSize = int.Parse("16"),
                Margin = "".Count(),
                HorizontalOptions = GenericMethod2<LayoutOptions>(),
                VerticalOptions = GenericMethod(LayoutOptions.Center)
            };
        }

        public void Test8()
        {
            Content = new Grid {RowDefinitions = {new RowDefinition {Height = GridLength.Auto}, new RowDefinition { }}};
        }

        public void Test9()
        {
            Content = new Label().MinSize(10, 20);
        }

        public void Test10()
        {
            Content = new Label().Bind("a");
        }

        public void Test11()
        {
            Content = new Grid {RowDefinitions = { }};
        }

        public void Test12()
        {
            var converter = new FuncConverter<bool>(b => b ? "true" : "false");
            Content = new Label();
            Content.Bind("adsf", converter: converter);
        }

        public void Test13()
        {
            Content = new Label();
            Content.Bind("a");
        }

        public void Test14()
        {
            Content = new Grid() {Margin = new Thickness(10)};
        }

        public void Test15()
        {
            Content = new Grid() {RowSpacing = 10};
        }

        public void Test16()
        {
            Content = new Grid() {BackgroundColor = Color.Red};
        }

        public void Test17()
        {
            Content = new Grid() {BackgroundColor = new[] {Color.Red}.FirstOrDefault()};
        }

        public void Test18()
        {
            Content = new StackLayout() {
                Children = {
                    new Label() {Text = "AAAA"},
                    new Label() {Text = "AAAA"},
                    new Label() {Text = "AAAA"},
                    new Label() {Text = "AAAA"},
                    new Label() {Text = "AAAA"},
                    new Label() {Text = "AAAA"},
                    new Label() {Text = "AAAA"},
                    new Label() {Text = "AAAA"}
                }
            };
        }

        public void Test19()
        {
            AnchorX = _intFromBase;
        }

        public void Test20()
        {
            string registrationCodeStr;
            MyControlViewModel vm = new MyControlViewModel(1, "a"), vm2 = new MyControlViewModel(2);
            //var vm2 = new MyControlViewModel(1, "a");
            registrationCodeStr = "Registration code";

            Content = new Grid {
                RowSpacing = 0,
                RowDefinitions = {new RowDefinition {Height = GridLength.Auto}, new RowDefinition { }},
                Children = {
                    new Label {Margin = fieldNameMargin, HorizontalOptions = LayoutOptions.FillAndExpand,}.MinWidth(15)
                        .RowCol(0, 2)
                        .Bind(nameof(vm.RegistrationPrompt)),
                    new Label {
                        Text = registrationCodeStr, VerticalOptions = LayoutOptions.End, Margin = fieldNameMargin
                    }.MinWidth(20).RowCol(0, 1),
                    new Label {
                            HorizontalOptions = LayoutOptions.End,
                            VerticalOptions = LayoutOptions.End,
                            Margin = fieldNameMargin
                        }
                        .MinWidth(13)
                        .RowCol(1, 2)
                        .Bind(nameof(vm.RegistrationCodeValidationMessage)),
                    new Entry {Placeholder = null, HeightRequest = 44, Margin = _fieldMargin}
                        .MinHeight(15)
                        .Bind(nameof(vm.RegistrationCode), BindingMode.TwoWay)
                        .RowCol(0, 2)
                }
            };
        }

        public void Test21()
        {
            object s = "asdf";
            Title = s as string;
        }

        public void Test22()
        {
            int i = 512;
            AnchorX = i++;
            AnchorY = i--;
        }

        public void Test23()
        {
            int i = 512;
            AnchorX = ++i;
            AnchorY = --i;
        }

        public void Test24()
        {
            bool a = true;
            IsEnabled = !a;
        }

        public void Test25()
        {
            int i = 3;
            AnchorX = -i;
        }

        public void Test26()
        {
            int i = 512;
            AnchorX = i >> 2;
        }

        public void Test27()
        {
            int i = 512;
            AnchorX = i << 2;
        }

        public void Test28()
        {
            var i = 512;
            i <<= 2;
            AnchorX = i;
        }

        public void Test29()
        {
            var i = 512;
            i >>= 2;
            AnchorX = i;
        }

        public void Test30()
        {
            AnchorX += 12;
            AnchorY -= 12;
            RotationX = RotationY = 1;
            RotationX *= 3.4;
            RotationY /= 2.2;
        }

        public void Test31()
        {
            AnchorX = 234;
            AnchorX %= 211;
        }

        public void Test32()
        {
            IsEnabled = false;
            IsEnabled |= true;
        }

        public void Test33()
        {
            IsEnabled &= false;
        }

        public void Test34()
        {
            string a = null;
            Title = a ?? "defff";
        }

        public void Test35()
        {
            var a = "asdfasdf";
            IsEnabled = a is int;
        }

        public void Test36()
        {
            var a = new[] {1, 2};
            AnchorX = a[1];
        }

        public void Test37()
        {
            AnchorX = (1 + 2) * 3;
        }

        public void Test38()
        {
            if (false) {
                AnchorX = 2;
            }
            else {
                AnchorX = 5;
            }
        }

        public void Test39()
        {
            if (true) {
                AnchorX = 1235;
            }
        }

        public void Test40()
        {
            AnchorX = _staticIntFromBase;
        }

        public void Test41()
        {
            var entry = new Label();
            Content = entry.FontSize(13);
        }

        public void Test42()
        {
            Appearing += MyControl_Appearing;
            Appearing += MyControl_Appearing;
            Appearing -= MyControl_Appearing;
        }

        public void Test42a()
        {
            var ctrol = new MyControl();
            ctrol.Appearing += MyControl_Appearing;
            ctrol.Appearing += MyControl_Appearing;
            ctrol.Appearing -= MyControl_Appearing;
        }

        public void Test42b()
        {
            var ctrol = new MyControl();
            Appearing += ctrol.MyControl_Appearing;
        }

        public void Test42c()
        {
            var ctrol = new MyControl();
            ctrol.Appearing += ctrol.MyControl_Appearing;
        }

        public void Test43()
        {
            Appearing += new System.EventHandler(MyControl_Appearing);
        }

        public void Test44()
        {
            Appearing += MyControl.MyControl_Appearing_Static;
        }

        public void Test44a()
        {
            Appearing += MyControl_Appearing_Static;
        }

        public void Test45()
        {
            var ctrol = new MyControl();
            Appearing += new System.EventHandler(ctrol.MyControl_Appearing);
        }

        public void Test46()
        {
            Action = () => { };
        }

        public void Test46a()
        {
            FuncBool = () => true;
        }

        public void Test46b()
        {
            EventHandler = (s, e) => { };
        }

        public void Test46c()
        {
            EventHandlerBool = (e, b) => { };
        }

        public void Test47()
        {
            WidthRequest = 10.0 - 1;
            HeightRequest = 10.0 + 1;
            Padding = new Thickness(10.0 / 2, 10.0 * 2);
            Title = (10m + 22u).ToString();
        }

        public void Test48()
        {
            var lv = new ListView { }.Invoke(list => list.ItemSelected += List_ItemSelected);
        }

        public void Test49()
        {
            var a = typeof(string);
        }

        public void Test50()
        {
            Opacity = default(int);
        }

        //public void Test51()
        //{
        //    var a = new DelegateA(() => { });
        //}

        public void Test52()
        {
            MethodForTest();
        }

        public void Test53()
        {
            Action a = AddAction;
            ADelegate b = AddAction;

            var mc = new MyControl();

            Action c = mc.AddAction;
            ADelegate d = mc.AddAction;

            MethodAcceptsAction(AddAction);
            MethodAcceptsCustomDelegate(AddAction);

            MethodAcceptsAction(mc.AddAction);
            MethodAcceptsCustomDelegate(mc.AddAction);
        }

        public void Test54()
        {
            StringProperty += MethodWithParams("1", "2");
            StringProperty += MethodWithParams("1", "2", "3");

            var a = "2";
            StringProperty += MethodWithParams2("1", a, "3");
        }

        public void Test55()
        {
            var btn = new Button {CommandParameter = true};
        }

        public void Test56()
        {
            WidthRequest = GetValue() * GetValueWithParams(4);

            double GetValue() => 16;

            double GetValueWithParams(int c)
            {
                return 32 * c;
            }
        }

        public void Test57()
        {
            int closureInt = 8;

            HeightRequest = GetClosureValue();

            VoidFunction();

            double GetClosureValue()
            {
                return closureInt;
            }

            void VoidFunction()
            {
                closureInt *= 2;
                HeightRequest += closureInt;
            }
        }

        public void Test58()
        {
            Func<int> a = ReturningDelegateMethod;
            HeightRequest = a();
        }

        public void Test59()
        {
            ReturningDelegate a = ReturningDelegateMethod;
            HeightRequest = a();
        }

        public void Test60()
        {
            Content = new Button {Text = nameof(Test60) + " Live"};
        }

        public void Test61()
        {
            var condition = true;
            MethodForTest(arg0: condition ? nameof(Test61) : null);
        }

        public void Test62()
        {
            var converter = new FuncConverter<bool>(v => v ? Color.Red : Color.Transparent);
        }

        public void Test63()
        {
            Content = new Button {Command = new Command(AddAction)};
        }

        public void Test64()
        {
            Content = new ListView().Invoke(list =>
                list.ItemSelected += (sender, e) => ((ListView)sender).SelectedItem = null);
        }

        public void Test65()
        {
            object webView = null;
            Content = new WebView { }.Assign(out webView);
            StringProperty = (webView != null).ToString();
        }

        public void Test66()
        {
            StringProperty = "abc"?.ToString();
        }

        public void Test67()
        {
            BackgroundColor = (1 + 1) == 2 ? (Xamarin.Forms.Color)Color.DarkSalmon : (Xamarin.Forms.Color)Color.Accent;
        }

        public void Test68()
        {
            _dictionary["A"] = 100;
            WidthRequest = _dictionary["A"];
        }

        public ICommand OpenWebCommand { get; set; }

        public void Test69()
        {
            OpenWebCommand = new Command(() => Device.OpenUri(new Uri("https://xamarin.com/platform")));
        }

        public void Test70()
        {
            var IsVisible = Device.Info.CurrentOrientation != DeviceOrientation.Landscape;
            var grid = new Grid() {RowDefinitions = new RowDefinitionCollection {new RowDefinition()}};

            grid.RowDefinitions[0].Height = Device.Info.CurrentOrientation == DeviceOrientation.Landscape
                ? new GridLength(X, GridUnitType.Absolute)
                : new GridLength(Y, GridUnitType.Star);
        }
        public View Test71()
        {
            var ret = new Label
            {
                HorizontalOptions = LayoutOptions.End,
                FontSize          = 14,
                FontAttributes    = FontAttributes.None
            }
            .Row(E.One)
            .Col(E.Two)
            .Bind(Label.TextProperty,
                "LatestMessage.DateTimeUtc",
                convert: (DateTime dateTimeUtc) =>
                {
                    var str = DateTime.Today.Equals(dateTimeUtc.Date)
                        ? "t"
                        : "d";
                    var cti = new CultureInfo("en-GB");
                    var dte = dateTimeUtc.ToString(str, cti);
                    var ret = $"{dte}";

                    return ret;
                });

            return ret;
        }

        public void Test72()
        {

            var progressColumn = new ColumnDefinition();
            progressColumn.SetBinding(
                ColumnDefinition.WidthProperty,
                path: "Path",
                converter: new FuncToTConverter<double, GridLength>(
                    (percent) =>
                    {
                        return percent == 0
                            ? GridLength.Auto
                            : new GridLength(percent, GridUnitType.Star);
                    }));

            var progressGrid = new Grid
            {
                ColumnDefinitions =
                {
                    progressColumn
                },
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Star }
                },
                ColumnSpacing = 0,
            };

            var progressView = new Frame
            {
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand,
                HasShadow = false,
            };

            var remainingView = new ContentView
            {
                HorizontalOptions = LayoutOptions.FillAndExpand,
                VerticalOptions = LayoutOptions.FillAndExpand,
            };
            progressGrid.Children.Add(progressView, left: 0, top: 0);
            progressGrid.Children.Add(remainingView, left: 1, top: 0);

            var primaryTextLabel = new Label
            {
                VerticalTextAlignment = TextAlignment.Center,
                BackgroundColor = Color.Transparent,
                TextColor = Color.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = Device.GetNamedSize(NamedSize.Small, typeof(Label)),
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
            };
            primaryTextLabel.SetBinding(
                Label.HorizontalTextAlignmentProperty,
                path: "Path",
                converter: new FuncToTConverter<double, TextAlignment>(
                    (percent) =>
                    {
                        // Center when we have a fixed-size column. Align right when it will grow.
                        return percent == 0 ? TextAlignment.Center : TextAlignment.End;
                    }));
            progressView.Content = primaryTextLabel;
        }

        private string MethodWithParams(params string[] strings)
        {
            return string.Join("", strings);
        }

        private string MethodWithParams2(string a, params string[] strings)
        {
            return a + string.Join("", strings);
        }

        delegate void ADelegate();
        delegate int ReturningDelegate();

        async void AddAction() { }

        public int ReturningDelegateMethod() => 2;

        void MethodAcceptsAction(Action action) { }
        void MethodAcceptsCustomDelegate(ADelegate action) { }

        private void MethodForTest(string arg0 = null)
        {

        }

        private string MethodForTestAcceptsFunc(Func<bool, string> func)
        {
            return func(true);
        }

        private void List_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void MyControl_Appearing(object sender, System.EventArgs e)
        {
        }

        private static void MyControl_Appearing_Static(object sender, System.EventArgs e)
        {
        }

        enum E
        {
            One = 1,
            Two = 2
        }

    }

    public class FuncToTConverter<TSource, TDestination> : IValueConverter
    {
        private Func<TSource, TDestination> _func;

        public FuncToTConverter(Func<TSource, TDestination> func)
        {
            _func = func;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return _func((TSource)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}