using LiveSharp.Shared.Infrastructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Parsing
#else
namespace LiveSharp.Shared.Parsing
#endif
{
    public class ToStringFormatParser : StreamingParser<object>
    {
        private StringParser _stringParser;

        public ToStringFormatParser()
        {
            _stringParser = new StringParser();
        }

        public override bool IsParsingComplete => _stringParser.IsParsingComplete;

        public override void Serialize(object obj, List<byte> result)
        {
            var formatted = LogFormatData(obj);
            new StringParser().Serialize(formatted, result);
        }

        public override void Reset()
        {
            _stringParser.Reset();
        }

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            return _stringParser.Feed(buffer, bufferIndex);
        }

        public string GetStringValue() => _stringParser.GetStringValue();
        public override object GetValue() => GetStringValue();
        
        public string LogFormatData(object data, bool needEncode = true)
        {
            if (data is null)
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
                if (type.GenericTypeArguments.Length > 0)
                    data = "Task<" + string.Join(", ", type.GenericTypeArguments.Select(t => TypeExtensions.GetTypeName(t))) + ">";
                else
                    data = "Task";
            } else {
                data = "{" + data + "}";
            }

            if (needEncode)
                return WebUtility.HtmlEncode(data.ToString());
            
            return data.ToString();

            string formatList(IList array)
            {
                var serializedObjects = array
                    .OfType<object>()
                    .Take(100)
                    .Select((o, i) => i + ": " + LogFormatData(o, false))
                    .ToArray();
                return
                    $"Count = {array.Count}{Environment.NewLine}[{string.Join(", ", serializedObjects)}]";
            }

            string formatDictionary(IDictionary dict)
            {
                var serializedObjects = dict.OfType<DictionaryEntry>().Take(100)
                    .Select(e => LogFormatData(e.Key, false) + ": " + LogFormatData(e.Value));
                return
                    $"Count = {dict.Count}{Environment.NewLine}[{string.Join(", ", serializedObjects)}]";
            }
        }
    }
}