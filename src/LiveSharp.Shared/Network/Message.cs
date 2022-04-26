using LiveSharp.ServerClient;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;

#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public class ServerMessage
    {
        public byte[] Content { get; protected set; }
        public byte ContentType { get; protected set; }
        public MessageType MessageType { get; protected set; }
        public int Parameter { get; protected set; }
        
        public ServerMessage() {}

        public ServerMessage(byte[] content, byte contentType, MessageType messageType, int parameter)
        {
            Content = content;
            ContentType = contentType;
            MessageType = messageType;
            Parameter = parameter;
        }

        public virtual byte[] CreateBuffer()
        {
            var parameterBytes = BitConverter.GetBytes(Parameter);
            var compressedContent = CompressBuffer(Content) ?? new byte[0];
            var contentLen = compressedContent.Length;
            var contentLengthBytes = BitConverter.GetBytes(contentLen);
            
            var result = new byte[2 + // header
                                  1 + // type
                                  4 + // parameter
                                  1 + // content type
                                  4 + // content len val
                                  contentLen + // content
                                  2 // checksum
                                  ];

            result[0] = 0xbe;
            result[1] = 0xef;
            result[2] = (byte)MessageType;

            Array.Copy(parameterBytes, 0, result, 3, parameterBytes.Length);

            result[7] = ContentType;

            Array.Copy(contentLengthBytes, 0, result, 8, contentLengthBytes.Length);
            Array.Copy(compressedContent, 0, result, 12, contentLen);

            var checksum = BitConverter.GetBytes(Checksum.Fletcher16(compressedContent));

            result[result.Length - 2] = checksum[0];
            result[result.Length - 1] = checksum[1];

            return result;
        }

        private static byte[] CompressBuffer(byte[] buffer)
        {
            using (var compressedStream = new MemoryStream())
            {
                using (var gZipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    gZipStream.Write(buffer, 0, buffer.Length);
                }
                
                return compressedStream.ToArray();
            }
        }

        public static byte[] DecompressBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return new byte[0];

            byte[] contentBuffer;

            using (var compressedContent = new MemoryStream(buffer))
            using (var gzip = new GZipStream(compressedContent, CompressionMode.Decompress))
            using (var outputStream = new MemoryStream())
            {
                gzip.CopyTo(outputStream);
                contentBuffer = outputStream.ToArray();
            }

            return contentBuffer;
        }

        public virtual string GetContentText()
        {
            return Encoding.Unicode.GetString(Content);
        }

        public override string ToString()
        {
            return $"ServerMessage: {Parameter} {ContentType} {MessageType}";
        }
    }
}
