﻿#if LIVESHARP_RUNTIME
namespace LiveSharp.Runtime.Network
#else
namespace LiveSharp.Shared.Network
#endif
{
    public class BroadcastGroups
    {
        public static int LiveSharp = 1;
        public static int LiveXaml = 4;
        public static int General = 5;
        public static int Inspector = 6;
        public static int Dashboard = 7;
        public static int LiveBlazor = 8;
        public static int Heartbeat = 255;
    }
}