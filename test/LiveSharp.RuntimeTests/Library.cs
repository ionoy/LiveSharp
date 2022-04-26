using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace LiveSharp.RuntimeTests
{
    public static class ObjectExtensions
    {
        public static string AsString(this object instance, string indent, HashSet<object> stackOverflowGuard = null)
        {
            stackOverflowGuard = stackOverflowGuard ?? new HashSet<object>();

            if (stackOverflowGuard.Contains(instance))
                return "";

            stackOverflowGuard.Add(instance);

            var newIndent = indent + "  ";
            var type = instance.GetType();
            var propLines = type.GetProperties()
                                .Where(p => p.Name != "Id" && 
                                            p.Name != "Parent" &&
                                            p.Name != "ParentView" &&
                                            p.Name != "RealParent")
                                .OrderBy(p => p.Name)
                                .Select(p => {
                                    try {
                                        var propVal = p.GetValue(instance);
                                        return newIndent + p.Name + ": " + serializeValue(propVal, newIndent);
                                    } catch (Exception e) {
                                        return newIndent + p.Name + ": <error> " + e.Message;
                                    }
                                })
                                .ToArray();
            var props = string.Join(Environment.NewLine, propLines);

            return $"{type.Name} {{" + Environment.NewLine + props + Environment.NewLine + indent + "}";

            string serializeValue(object obj, string serializeIndent)
            {
                if (obj is string s) {
                    return $"\"{s}\"";
                } else if (obj is IEnumerable ie) {
                    return "[" + string.Join(", ", ie.Cast<object>().Select(o => serializeValue(o, serializeIndent))) + "]";
                } else if (obj is null) {
                    return "null";
                } else if (obj is Thickness t) {
                    return $"{t.Top} {t.Right} {t.Bottom} {t.Left}";
                } else if (obj.GetType().FullName.StartsWith("LiveSharp")) {
                    return obj.AsString(serializeIndent, stackOverflowGuard);
                } else return obj.ToString();
            }
        }

        public static string AsString(this object instance)
        {
            return instance.AsString("");
        }
    }

    /*public class BindableObject
    {
        public LayoutOptions HorizontalOptions { get; internal set; }
        public bool IsVisible { get; set; }
        public bool IsEnabled { get; set; }
        public Thickness Margin { get; set; }
        public double Offset { get; set; }
        public void SetValue(object property, object value)
        { }

        public void SetBinding(object property, Binding binding)
        { }

        public string ToString(string indent)
        {
            var newIndent = indent + "  ";
            var type = GetType();
            var propLines = type.GetProperties()
                                .OrderBy(p => p.Name)
                                .Select(p => {
                                    var propVal = p.GetValue(this);
                                    return newIndent + p.Name + ": " + serializeValue(propVal, newIndent);
                                });
            var props = string.Join(Environment.NewLine, propLines);
            
            return $"{type.Name} {{" + Environment.NewLine + props + Environment.NewLine + indent + "}";

            string serializeValue(object obj, string serializeIndent)
            {
                if (obj is BindableObject bo) {
                    return bo.ToString(serializeIndent);
                } else if (obj is string s) {
                    return $"\"{s}\"";
                } else if (obj is IEnumerable ie) {
                    return "[" + string.Join(", ", ie.Cast<object>().Select(o => serializeValue(o, serializeIndent))) + "]";
                } else if (obj is null) {
                    return "null";
                } else return obj.ToString();
            }
        }

        public override string ToString()
        {
            return ToString("");
        }

        public static bool StaticMethod() => true;
        public bool Method() => true;
        public string Method(string s) => s;
        public T GenericMethod<T>() => default(T);
        public T GenericMethod<T>(T val) => val;
    }

    public class Thickness
    {
        public Thickness(double uniform) { }
    }

    public class Grid : BindableObject
    {
        internal static readonly object ColumnProperty;
        internal static readonly object RowProperty;

        public List<RowDefinition> RowDefinitions { get; internal set; } = new List<RowDefinition>();

        public List<BindableObject> Children { get; } = new List<BindableObject>();

        public int RowSpacing { get; internal set; }
    }

    public abstract class WithText : BindableObject
    {
        public string Text { get; set; }
        public double FontSize { get; internal set; }
    }

    public class Label : WithText
    {
        public static readonly object TextProperty;
        public int Margin { get; internal set; }
        public LayoutOptions VerticalOptions { get; internal set; }
    }

    public class Entry : WithText
    {
        public string Placeholder { get; internal set; }
        public int HeightRequest { get; internal set; }
        public object Margin { get; internal set; }
    }

    public class ContentPage : BindableObject
    {
        public BindableObject Content { get; set; }
        public object Obj { get; set; }
    }

    public class RowDefinition : BindableObject
    {
        public GridLength Height { get; set; }
    }

    public struct GridLength
    {
        public static GridLength Auto => new GridLength(1.0, GridUnitType.Auto);
        public static GridLength Star => new GridLength(1.0, GridUnitType.Star);
        public double Value { get;}
        public GridUnitType GridUnitType { get; }
        public bool IsAbsolute => GridUnitType == GridUnitType.Absolute;
        public bool IsAuto => GridUnitType == GridUnitType.Auto;
        public bool IsStar => GridUnitType == GridUnitType.Star;

        public GridLength(double value)
        {
            this = new GridLength(value, GridUnitType.Absolute);
        }

        public GridLength(double value, GridUnitType type)
        {
            if (value < 0.0 || double.IsNaN(value)) 
                throw new ArgumentException("value is less than 0 or is not a number", "value");            
            if (type < GridUnitType.Absolute || type > GridUnitType.Auto) 
                throw new ArgumentException("type is not a valid GridUnitType", "type");            
            Value = value;
            GridUnitType = type;
        }

        public override bool Equals(object obj) => obj != null && obj is GridLength && this.Equals((GridLength)obj);
        private bool Equals(GridLength other) => this.GridUnitType == other.GridUnitType && Math.Abs(this.Value - other.Value) < 4.94065645841247E-324;
        public override int GetHashCode() => this.GridUnitType.GetHashCode() * 397 ^ this.Value.GetHashCode();
        public static implicit operator GridLength(double absoluteValue) => new GridLength(absoluteValue);
        public override string ToString() => string.Format("{0}.{1}", new object[]{ Value, GridUnitType });
    }

    public enum GridUnitType { Absolute, Star, Auto }

    public struct LayoutOptions
    {
        private int _flags;
        public static readonly LayoutOptions Start = new LayoutOptions(LayoutAlignment.Start, false);
        public static readonly LayoutOptions Center = new LayoutOptions(LayoutAlignment.Center, false);
        public static readonly LayoutOptions End = new LayoutOptions(LayoutAlignment.End, false);
        public static readonly LayoutOptions Fill = new LayoutOptions(LayoutAlignment.Fill, false);
        public static readonly LayoutOptions StartAndExpand = new LayoutOptions(LayoutAlignment.Start, true);
        public static readonly LayoutOptions CenterAndExpand = new LayoutOptions(LayoutAlignment.Center, true);
        public static readonly LayoutOptions EndAndExpand = new LayoutOptions(LayoutAlignment.End, true);
        public static readonly LayoutOptions FillAndExpand = new LayoutOptions(LayoutAlignment.Fill, true);

        public LayoutAlignment Alignment {
            get => (LayoutAlignment)(_flags & 3);
            set => _flags = ((_flags & -4) | (int)value);
        }

        public bool Expands {
            get => (_flags & 4) != 0;
            set => _flags = ((_flags & 3) | (value ? 4 : 0));
        }

        public LayoutOptions(LayoutAlignment alignment, bool expands)
        {
            if (alignment < LayoutAlignment.Start || alignment > LayoutAlignment.Fill)
                throw new ArgumentOutOfRangeException();            
            _flags = (int)(alignment | (expands ? ((LayoutAlignment)4) : LayoutAlignment.Start));
        }
    }

    public enum LayoutAlignment { Start = 0, Center = 1, End = 2, Fill = 3 }

    public static class Extensions
    {
        public static T SetFontSize<T>(this T instance, double fontSize) where T : WithText
        {
            instance.FontSize = fontSize;
            return instance;
        }
        
        public static T SetColRow<T>(this T instance, int col, int row, int colSpan, int rowSpan) where T : BindableObject
        {
            instance.SetValue(Grid.ColumnProperty, col);
            instance.SetValue(Grid.RowProperty, row);
            return instance;
        }

        public static T Bind<T>(this T instance, string path, BindingMode mode = BindingMode.OneWay, object converter = null) where T : BindableObject
        {
            instance.SetBinding(Label.TextProperty, new Binding(path, mode));
            return instance;
        }

        public static int ParseInt(this string instance)
        {
            return int.Parse(instance);
        }
    }

    public class FuncConverter<T>
    {
        public FuncConverter(Func<T, object> f)
        { }
    }

    public class Binding
    {
        public string Path { get; }

        public Binding(string path, BindingMode mode)
        {
            Path = path;
        }
    }

    public enum BindingMode
    {
        OneWay, TwoWay
    }*/
}
