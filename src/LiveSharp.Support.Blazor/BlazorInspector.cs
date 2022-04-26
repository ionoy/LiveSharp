using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LiveSharp.Support.Blazor.Infrastructure;

namespace LiveSharp.Support.Blazor
{
    public class BlazorInspector : ILiveSharpInspector
    {
        private readonly ILiveSharpRuntime _runtime;
        private readonly ConditionalWeakTable<object, object> _components = new ConditionalWeakTable<object, object>();
        private int _instanceIds;
        private string _oldContent;

        public BlazorInspector(ILiveSharpRuntime runtime)
        {
            _runtime = runtime;
        }
    
        public void BuildRenderTreeCalled(object instance)
        {
            if (!_components.TryGetValue(instance, out var key)) {
                key = _instanceIds++;
                _components.Add(instance, key);
            }
            
            Render();
        }

        public void Render()
        {
            var sb = new StringBuilder();
            
            RemoveDisposedComponents();

            var deduplicated = _components
                .Select(c => c.Key)
                .GroupBy(c => c.GetType())
                .Select(g => g.First());
            
            foreach (var component in deduplicated) {
                sb.AppendLine(RenderInstance(component));
            }

            var newContent = sb.ToString();
            
            if (newContent == _oldContent)
                return;
            
            _runtime.UpdateDiagnosticPanel("Inspector", newContent);
            _oldContent = newContent;
        }

        private void RemoveDisposedComponents()
        {
            var componentsToRemove = _components.Where(c => c.Key is IDisposable)
                .Where(c => c.Is("Microsoft.AspNetCore.Components.OwningComponentBase"))
                .Where(c => (bool) c.GetPropertyValue("IsDisposed"))
                .ToArray();

            foreach (var toRemove in componentsToRemove) 
                _components.Remove(toRemove);
        }

        private string RenderInstance(object instance)
        {
            var type = instance.GetType();
            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            
            var fields = type.GetFields(bindingFlags);
            var properties = type.GetProperties(bindingFlags);
            
            var result = new StringBuilder();
    
            result.Append("<div class='inspector-instance'>");
            result.Append($"<div class='instance-type'>{type.Name}</div>");
            
            foreach (var field in fields.Where(f => !f.Name.EndsWith("k__BackingField"))) {
                try {
                    var memberValue = field.IsStatic ? field.GetValue(null) : field.GetValue(instance);
                    result.Append($"<p class='member'><span class='member-name'>{field.Name}</span><span class='member-value'>{FormatData(memberValue)}</span></p>");
                } catch { }
            }
            foreach (var property in properties.Where(p => p.CanRead)) {
                try {
                    var memberValue = property.GetValue(instance);
                    result.Append($"<p class='member'><span class='member-name'>{property.Name}</span><span class='member-value'>{FormatData(memberValue)}</span></p>");
                }
                catch { }
            }
            result.Append("</div>");
            
            return result.ToString();
        }
    
        static string FormatData(object data, bool encode = true)
        {
            try {
                if (data == null)
                    data = "null";
                else if (data is string)
                    data = "\"" + data + "\"";
                else if (data is char)
                    data = "'" + data + "'";
                else if (data is IList list)
                    data = formatList(list);
                else if (data is IDictionary dict)
                    data = formatDictionary(dict);
                else if (data is IEnumerable)
                    data = data.GetType().GetTypeName();
                else if (data is Task) {
                    var type = data.GetType();
                    if (type.GenericTypeArguments?.Length > 0)
                        data = "Task<" + string.Join(", ", type.GenericTypeArguments.Select(t => t.GetTypeName())) + ">";
                    else
                        data = "Task";
                } else {
                    var type = data.GetType();
            
                    if (type.IsPrimitive) {
                        data = data.ToString();
                    } else if (type.GetMethod("ToString", BindingFlags.Instance | BindingFlags.DeclaredOnly) != null) {
                        data = data.ToString();
                    } else {
                        data = data.ToString();
                        //data = serializeObject(data);
                    }
                }
    
                if (encode)
                    return WebUtility.HtmlEncode(data.ToString());
            
                return data.ToString();
            }
            catch (Exception e) {
                return $"<exception {e?.Message ?? "unknown"}>";
            }

            string formatList(IList array)
            {
                var serializedObjects = array.OfType<object>().Take(10).Select((o, i) => i + ": " + FormatData(o, false));
                return
                    $"Count = {array.Count}{Environment.NewLine}[{string.Join(", ", serializedObjects)}]";
            }
            
            string formatDictionary(IDictionary dict)
            {
                var serializedObjects = dict.OfType<DictionaryEntry>().Take(10).Select(e => FormatData(e.Key, false) + ": " + FormatData(e.Value, false));
                return $"Count = {dict.Count}{Environment.NewLine}[{string.Join(", ", serializedObjects)}]";
            }
            
            // string serializeObject(object obj)
            // {
            //     try {
            //         var objectType = obj.GetType();
            //         var properties = objectType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            //         var propertyValues = properties.Select(p => p.Name + ": " + FormatData(p.GetValue(obj), false));
            //         return "{ " + string.Join(", ", propertyValues) + " }";
            //     }
            //     catch (Exception e) {
            //         return "<Exception: " + e.Message + ">";
            //     }
            // }
        }
    }
}