#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public class ContentTypes
    {
        public static byte Multipart = 200;
        
        public class General
        {
            public static byte ProjectInfoXml = 10;
            public static byte RuntimeLog = 20;
        }

        public class Inspector
        {
            public static byte InstanceUpdate = 30;
            public static byte MethodWatch = 40;
            public static byte MethodWatchStart = 50;
            public static byte MethodWatchEnd = 60;
            public static byte InstanceUpdateHtml = 70;
            public static byte PanelUpdate = 80;
            public static byte PanelsClear = 90;
            public static byte DebugEvents = 100;
        }

        public class Dashboard
        {
            public static byte AlwaysOnTop = 1;
            public static byte Quit = 255;
        }
        
        public class LiveSharp
        {
            public const byte DocumentElement = 6;
            public const byte EnableDebugLogging = 7;
            public const byte ResourceUpdated = 8;
            public const byte AssemblyUpdate = 9;
        }

        public class LiveXaml
        {
            public const byte XamlUpdate = 1;
        }
    }
}
