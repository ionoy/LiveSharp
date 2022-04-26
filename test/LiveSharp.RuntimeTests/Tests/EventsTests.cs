using System;
using System.Diagnostics;

namespace LiveSharp.RuntimeTests
{
    public class EventsTests : TestsBase
    {
        static int _counter;
        string _instanceId = "#" + _counter++;

        public event EventHandler<string> TestEvent;
        bool _eventHandlerCalled;
        
        public void Test0()
        {
            TestEvent += new EventHandler<string>(TestEventHandler);
            TestEvent(this, "a");
            TestEvent -= new EventHandler<string>(TestEventHandler);

            Assert(_eventHandlerCalled);
            Assert(TestEvent == null);

            _eventHandlerCalled = false;
        }

        public void Test10()
        {
            TestEvent += TestEventHandler;
            Log.WriteLine(_eventHandlerCalled);
            TestEvent(this, "a");
            Log.WriteLine(_eventHandlerCalled);
            TestEvent -= TestEventHandler;
        
            Assert(_eventHandlerCalled, "EventHandler not called");
            Assert(TestEvent == null);

            _eventHandlerCalled = false;
        }

        public void Test11()
        {
            _eventHandlerCalled = false;

            TestEvent += TestEventHandler;
            TestEvent?.Invoke(this, "a");
            TestEvent -= TestEventHandler;

            Assert(_eventHandlerCalled);
            AssertEqual(TestEvent, null);
        }
        
        public void Test12()
        {
            TestEvent += (sender, s) => {
                _eventHandlerCalled = true;
            };

            TestEvent?.Invoke(this, "a");
            TestEvent = null;

            Assert(_eventHandlerCalled);
            
            _eventHandlerCalled = false;
        }
        
        public void Test13()
        {
            var instance = new EventsTests();

            TestEvent += instance.TestEventHandler;
            TestEvent?.Invoke(this, "a");
            TestEvent -= instance.TestEventHandler;

            Assert(instance._eventHandlerCalled);

            _eventHandlerCalled = false;
        }
        
        public void Test14()
        {
            var instance = new EventsTests();

            instance.TestEvent += instance.TestEventHandler;
            if (instance.TestEvent != null) {
                Log.WriteLine("not null");
                instance.TestEvent.Invoke(this, "a");
            } else {
                Log.WriteLine("null");
            }
            instance.TestEvent -= instance.TestEventHandler;

            Assert(instance._eventHandlerCalled, "instance._eventHandlerCalled");

            _eventHandlerCalled = false;
        }
        
//        public void Test15()
//        {
//            var eventHandlerCalled = false;
//            
//            TestEvent += (sender, s) => {
//                eventHandlerCalled = true;
//            };
//
//            TestEvent?.Invoke(this, "a");
//            TestEvent = null;
//
//            Assert(eventHandlerCalled);
//        }

        private void TestEventHandler(object sender, string e)
        {
            _eventHandlerCalled = true;
        }
    }
}