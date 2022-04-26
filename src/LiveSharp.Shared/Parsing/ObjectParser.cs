
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

#if LIVESHARP_RUNTIME
using LiveSharp.Runtime.Debugging;
using LiveSharp.Runtime.Parsing;

namespace LiveSharp.Runtime.Parsing
#else
using LiveSharp.Shared.Debugging;
using LiveSharp.Shared.Parsing;

namespace LiveSharp.Shared.Parsing
#endif
{
    public class ObjectParser<T> : StreamingParser<T> where T : new()
    {
        private readonly CompositeParser _compositeParser;
        
        private static readonly PropertyInfo[] Properties = typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToArray();
        private static readonly Action<T, List<byte>> Serializer = CreateSerializer();

        public override bool IsParsingComplete => _compositeParser.IsParsingComplete;

        public ObjectParser()
        {
            if (Properties.Length > 0) {
                var builder = CompositeParser.StartWith(GetParserForType(Properties[0].PropertyType));
                
                foreach (var property in Properties.Skip(1))
                    builder = builder.Next(GetParserForType(property.PropertyType));

                _compositeParser = builder.Build();
            } else {
                _compositeParser = CompositeParser.StartWith(new EmptyParser<T>()).Build();
            }
        }

        public override void Serialize(T value, List<byte> result)
        {
            Serializer(value, result);
        }

        private static Action<T, List<byte>> CreateSerializer()
        {
            var instanceParameter = Expression.Parameter(typeof(T));
            var resultParameter = Expression.Parameter(typeof(List<byte>));
            var propertySerializeCalls = new List<Expression>();
            
            foreach (var prop in Properties) {
                var propertyType = prop.PropertyType;
                var property = Expression.Property(instanceParameter, prop);
                var parser = Expression.Constant(GetParserForType(propertyType));
                var serializeCall = Expression.Call(parser, parser.Type.GetMethod("Serialize"), property, resultParameter);
                
                propertySerializeCalls.Add(serializeCall);
            }

            var body = Expression.Block(propertySerializeCalls);
            var lambda = Expression.Lambda<Action<T, List<byte>>>(body, instanceParameter, resultParameter);
            
            return lambda.Compile();
        }

        private static StreamingParser GetParserForType(Type type)
        {
            if (type == typeof(string))
                return new StringParser();
            if (type == typeof(int))
                return new IntParser();
            if (type == typeof(byte))
                return new ByteParser();
            if (type == typeof(bool))
                return new BoolParser();
            if (type == typeof(byte[]))
                return new ByteArrayParser();
            if (type == typeof(long))
                return new LongParser();

            if (type.IsArray) {
                var elementType = type.GetElementType();
                var parserType = typeof(ArrayParser<>).MakeGenericType(elementType);
                var elementParser = GetParserForType(elementType);
                
                return (StreamingParser)Activator.CreateInstance(parserType, elementParser);
            }

            if (type == typeof(object))
                return new ToStringFormatParser();
            
            throw new NotImplementedException($"Serializer not defined for type {type}");
        }

        public override void Reset()
        {
            _compositeParser.Reset();
        }

        public override int Feed(IReadOnlyList<byte> buffer, int bufferIndex)
        {
            return _compositeParser.Feed(buffer, bufferIndex);
        }

        public override object GetValue()
        {
            return GetObjectValue();
        }

        public T GetObjectValue()
        {
            if (!IsParsingComplete)
                throw new InvalidOperationException("Parsing is not complete");

            var instance = new T();
            var values = _compositeParser.GetValues();

            if (values.Length != Properties.Length)
                throw new InvalidOperationException("Values length != properties length");
            
            for (var i = 0; i < values.Length; i++)
                Properties[i].SetValue(instance, values[i]);

            return instance;
        }
    }
}