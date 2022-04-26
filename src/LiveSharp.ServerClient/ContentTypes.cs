using System;
using System.Collections.Generic;
using System.Text;

namespace LiveSharp.ServerClient
{
    public class ContentTypes
    {
        public static byte Multipart = 200;
        
        public class General
        {
            public static byte ProjectInfoXml = 10;
            public static byte RuntimeLog = 20;
            public static byte DebugEvents = 21;
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
        }

        public class Dashboard
        {
            public static byte AlwaysOnTop = 1;
            public static byte Quit = 255;
        }
        
        public class LiveSharp
        {
            public const byte UpdatedMethodBody = 1;
            public const byte BatchStart = 2;
            public const byte BatchEnd = 3;
            public const byte MemberInitializer = 4;
            public const byte TypeInfoElement = 5;
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
