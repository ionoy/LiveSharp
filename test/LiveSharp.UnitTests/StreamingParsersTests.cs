
using LiveSharp.Runtime.Debugging;
using LiveSharp.Runtime.Parsing;
using NUnit.Framework;
using System.Collections.Generic;

namespace LiveSharp.UnitTests
{
    public class StreamingParsersTests
    {
        [SetUp]
        public void Setup()
        {
        }
        
        [Test]
        public void TestRawSerialization()
        {
            var buffer = new List<byte>();
            var raw = new byte[] { 1, 2, 3, 4, 5 };
            
            new RawParser(0).Serialize(raw, buffer);

            var rawParser = new RawParser(raw.Length);
            rawParser.Feed(buffer, 0);
            
            Assert.IsTrue(rawParser.IsParsingComplete);
            Assert.That(raw, Is.EquivalentTo(rawParser.GetBufferValue()));
        }
        
        [Test]
        public void TestIntSerialization()
        {
            var buffer = new List<byte>();
            new IntParser().Serialize(42, buffer);

            var stringParser = new IntParser();
            stringParser.Feed(buffer, 0);
            
            Assert.IsTrue(stringParser.IsParsingComplete);
            Assert.AreEqual(stringParser.GetIntValue(), 42);
        }

        [Test]
        public void TestStringSerialization()
        {
            var buffer = new List<byte>();
            new StringParser().Serialize("abc", buffer);

            var stringParser = new StringParser();
            stringParser.Feed(buffer, 0);
            
            Assert.IsTrue(stringParser.IsParsingComplete);
            Assert.AreEqual(stringParser.GetStringValue(), "abc");
        }
        
        [Test]
        public void TestObjectSerialization()
        {
            var serialized = new List<byte>();
            var testObject = new TestSerializable();
            new ObjectParser<TestSerializable>().Serialize(testObject, serialized);

            var objectParser = new ObjectParser<TestSerializable>();
            objectParser.Feed(serialized, 0);
            
            Assert.IsTrue(objectParser.IsParsingComplete);

            var deserialized = objectParser.GetObjectValue();
            
            Assert.AreEqual(testObject.TestString, deserialized.TestString);
            Assert.AreEqual(testObject.TestInt, deserialized.TestInt);
            Assert.That(testObject.TestByteArray, Is.EquivalentTo(deserialized.TestByteArray));
        }
        
        [Test]
        public void TestObjectSerializationMultipleObjects()
        {
            var serialized = new List<byte>();
            var testObject = new TestSerializable();
            new ObjectParser<TestSerializable>().Serialize(testObject, serialized);
            var objectParser = new ObjectParser<TestSerializable>();
            
            for (int i = 0; i < 10; i++) {
                objectParser.Feed(serialized, 0);
            
                Assert.IsTrue(objectParser.IsParsingComplete);

                var deserialized = objectParser.GetObjectValue();
            
                Assert.AreEqual(testObject.TestString, deserialized.TestString);
                Assert.AreEqual(testObject.TestInt, deserialized.TestInt);
                Assert.That(testObject.TestByteArray, Is.EquivalentTo(deserialized.TestByteArray));
                
                objectParser.Reset();
            }
        }
        
        [Test]
        public void TestObjectSerializationApi()
        {
            var serialized = new List<byte>();
            var testObject = new TestSerializable();
            new ObjectParser<TestSerializable>().Serialize(testObject, serialized);
            var deserialized = Deserialize.Object<TestSerializable>(serialized);
            
            Assert.AreEqual(testObject.TestString, deserialized.TestString);
            Assert.AreEqual(testObject.TestInt, deserialized.TestInt);
            Assert.That(testObject.TestByteArray, Is.EquivalentTo(deserialized.TestByteArray));
        }
        
        [Test]
        public void TestStartDebugEventParser()
        {
            var sde = new StartDebugEvent {
                Arguments = new object[] {new StreamingParsersTests()},
                InvocationId = 7,
                LocalNames = "a",
                MethodIdentifier = "2 ",
                ParameterNames = ""
            };

            var buffer = Serialize.Object(sde, new DebugEventParser());
            var sde2 = (StartDebugEvent)Deserialize.Object(buffer, new DebugEventParser());
            
            Assert.AreEqual(sde.InvocationId, sde2.InvocationId);
            Assert.AreEqual(sde.LocalNames, sde2.LocalNames);
            Assert.AreEqual(sde.MethodIdentifier, sde2.MethodIdentifier);
            Assert.AreEqual(sde.ParameterNames, sde2.ParameterNames);
        }
        
        [Test]
        public void TestDebugEventArrayParser()
        {
            var source = new DebugEvent[] {
                new StartDebugEvent {
                    Arguments = new object[] { new StreamingParsersTests() },
                    InvocationId = 0,
                    LocalNames = "a,b,c",
                    MethodIdentifier = "StreamingParsersTests TestDebugEventParser ",
                    ParameterNames = ""
                },
                new AssignDebugEvent {
                    InvocationId = 0,
                    SlotIndex = 1,
                    Value = 2
                },
                new ReturnDebugEvent {
                    InvocationId = 0
                }
            };
            var result = Serialize.ObjectArray(source, new DebugEventParser());
            var deserialized = Deserialize.ObjectArray(result, new DebugEventParser());
            
            Assert.AreEqual(deserialized.Count, 3);
            Assert.IsTrue(deserialized[0] is StartDebugEvent sd);
            Assert.IsTrue(deserialized[1] is AssignDebugEvent ad);
            Assert.IsTrue(deserialized[2] is ReturnDebugEvent rd);
        }
        
        class TestSerializable
        {
            public string TestString { get; set; } = "Hello, World!";
            public int TestInt { get; set; } = 42;
            public byte[] TestByteArray { get; set; } = {0, 1, 2, 3, 4, 5, 6};
        }
    }
}