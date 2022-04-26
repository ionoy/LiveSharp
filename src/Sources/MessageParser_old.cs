using LiveSharp.ServerClient;
using System;
using System.Text;

namespace LiveSharp.ServerClient
{
    internal class MessageParser : ParserBase<ServerMessage>
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

        private ParserState _parserState = ParserState.Init;
        private ServerMessage _message;
        private ushort _expectedChecksum;
        private int _contentLen;
        private object _payload;

        public MessageParser(object payload) : base()
        {
            _payload = payload;
        }

        protected override void Feed(byte b)
        {
            switch (_parserState) {
                case ParserState.Init:
                    if (b == 0xbe) {
                        _innerBuffer.Clear();
                        _parserState = ParserState.Header;
                        _message = new ServerMessage();
                    }
                    break;
                case ParserState.Header:
                    if (b == 0xef)
                        _parserState = ParserState.Type;
                    else
                        ResetParserState();
                    break;
                case ParserState.Type:
                    _message.Type = (MessageType)b;
                    _parserState = ParserState.Parameter;
                    break;
                case ParserState.Parameter:
                    _innerBuffer.Add(b);

                    if (_innerBuffer.Count == 4) {
                        _message.Parameter = BitConverter.ToInt32(_innerBuffer.ToArray(), 0);
                        _innerBuffer.Clear();

                        _parserState = ParserState.ContentType;
                    }
                    break;
                case ParserState.ContentType:
                    _message.ContentType = b;
                    _parserState = ParserState.Length;
                    break;
                case ParserState.Length:
                    _innerBuffer.Add(b);

                    if (_innerBuffer.Count == 4) {
                        _contentLen = BitConverter.ToInt32(_innerBuffer.ToArray(), 0);
                        _innerBuffer.Clear();
                        if (_contentLen == 0) {
                            _message.Content = new byte[0];
                            _parserState = ParserState.Checksum;
                        } else {
                            _parserState = ParserState.Content;
                        }
                    }
                    break;
                case ParserState.Content:
                    _innerBuffer.Add(b);

                    if (_innerBuffer.Count == _contentLen) {
                        _expectedChecksum = Checksum.Fletcher16(_innerBuffer);
                        _message.Content = _innerBuffer.ToArray();
                        _innerBuffer.Clear();
                        _parserState = ParserState.Checksum;
                    }
                    break;
                case ParserState.Checksum:
                    _innerBuffer.Add(b);

                    if (_innerBuffer.Count == 2) {
                        var checksum = BitConverter.ToUInt16(_innerBuffer.ToArray(), 0);
                        _innerBuffer.Clear();

                        if (_expectedChecksum == checksum)
                            RaiseMessageReceived(_message, _payload);

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
