using LiveSharp.ServerClient;
using System;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public class MessageParser : ParserBase<ServerMessage>
    {
        private enum ParserState
        {
            Init,
            Header,
            Type,
            Parameter,
            ContentType,
            Length,
            Content,
            Checksum
        };

        private byte _messageContentType;
        private MessageType _messageType;
        private ParserState _parserState = ParserState.Init;
        private ushort _expectedChecksum;
        private byte[] _messageContentCompressed;
        private int _contentLen;
        private object _payload;
        private int _messageParameter;

        public MessageParser(object payload) : base()
        {
            _payload = payload;
        }

        protected override void Feed(byte b)
        {
            switch (_parserState) {
                case ParserState.Init:
                    if (b == 0xbe) {
                        InnerBuffer.Clear();
                        _parserState = ParserState.Header;
                    }
                    break;
                case ParserState.Header:
                    if (b == 0xef)
                        _parserState = ParserState.Type;
                    else
                        ResetParserState();
                    break;
                case ParserState.Type:
                    _messageType = (MessageType)b;
                    _parserState = ParserState.Parameter;
                    break;
                case ParserState.Parameter:
                    InnerBuffer.Add(b);

                    if (InnerBuffer.Count == 4) {
                        _messageParameter = BitConverter.ToInt32(InnerBuffer.ToArray(), 0);
                        InnerBuffer.Clear();

                        _parserState = ParserState.ContentType;
                    }
                    break;
                case ParserState.ContentType:
                    _messageContentType = b;
                    _parserState = ParserState.Length;
                    break;
                case ParserState.Length:
                    InnerBuffer.Add(b);

                    if (InnerBuffer.Count == 4) {
                        _contentLen = BitConverter.ToInt32(InnerBuffer.ToArray(), 0);
                        InnerBuffer.Clear();
                        if (_contentLen == 0) {
                            _messageContentCompressed = new byte[0];
                            _parserState = ParserState.Checksum;
                        } else {
                            _parserState = ParserState.Content;
                        }
                    }
                    break;
                case ParserState.Content:
                    InnerBuffer.Add(b);

                    if (InnerBuffer.Count == _contentLen) {
                        _expectedChecksum = Checksum.Fletcher16(InnerBuffer);
                        _messageContentCompressed = InnerBuffer.ToArray();
                        InnerBuffer.Clear();
                        _parserState = ParserState.Checksum;
                    }
                    break;
                case ParserState.Checksum:
                    InnerBuffer.Add(b);

                    if (InnerBuffer.Count == 2) {
                        var checksum = BitConverter.ToUInt16(InnerBuffer.ToArray(), 0);
                        InnerBuffer.Clear();

                        if (_expectedChecksum == checksum)
                        {
                            var content = ServerMessage.DecompressBuffer(_messageContentCompressed);

                            if (_messageContentType == ContentTypes.Multipart) {
                                ResetParserState();
                                Feed(content, content.Length);
                                return;
                            }

                            var message = new ServerMessage(content, _messageContentType, _messageType, _messageParameter);

                            RaiseMessageReceived(message, _payload);
                        }

                        ResetParserState();
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override void ResetParserState()
        {
            _parserState = ParserState.Init;
            _expectedChecksum = 0;
        }
    }
}
