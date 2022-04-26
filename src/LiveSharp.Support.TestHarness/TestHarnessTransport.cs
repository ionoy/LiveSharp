using LiveSharp.Debugging;
using LiveSharp.Runtime.Network;
using LiveSharp.Runtime.Debugging;
using LiveSharp.Runtime.Parsing;
using System;
using System.Threading.Tasks;

namespace LiveSharp.Support.TestHarness
{
    public class TestHarnessTransport : ILiveSharpTransport
    {
        public static TestHarnessTransport Instance { get; private set; }
        
        private Action<object, byte[], int> _onBufferReceived;
        private readonly MessageParser _messageParser;
        public object ConnectionObject => this;
        public DebugEventProcessor DebugEventProcessor { get; set; }

        public TestHarnessTransport()
        {
            Instance = this;
            _messageParser = new MessageParser(this);
            _messageParser.MessageParsed += MessageFromRuntimeParsed;
        }

        public Task Connect(string webHost, string tcpHost, int tcpPort, Action<Exception, object> onTransportException, ILiveSharpLogger logger)
        {
            return Task.FromResult(true);
        }

        public void FeedToRuntime(byte[] buffer, byte contentType, int group)
        {
            var message = new ServerMessage(buffer, contentType, MessageType.Broadcast, group);
            FeedToRuntime(message);
        }

        public void FeedToRuntime(ServerMessage message)
        {
            var messageBuffer = message.CreateBuffer();

            _onBufferReceived?.Invoke(this, messageBuffer, messageBuffer.Length);
        }

        public void Send(byte[] buffer, Action onComplete)
        {
            _messageParser.Feed(buffer, buffer.Length);
            onComplete();
        }

        private void MessageFromRuntimeParsed(object? sender, ParserEventArgs<ServerMessage> e)
        {
            if (e.Message.ContentType == ContentTypes.Inspector.DebugEvents) {
                var debugEvents = Shared.Parsing.Deserialize.ObjectArray(e.Message.Content, new LiveSharp.Shared.Parsing.DebugEventParser());
                DebugEventProcessor?.FeedEvents(debugEvents);
            }
        }

        public void StartReceiving(Action<object, byte[], int> onBufferReceived)
        {
            _onBufferReceived = onBufferReceived;
        }

        public void CloseConnection()
        {
            Console.WriteLine("connection fake close");
        }

        public string GetHandshakeHost(string buildTimeIp)
        {
            return "http://" + buildTimeIp;
        }
    }
}