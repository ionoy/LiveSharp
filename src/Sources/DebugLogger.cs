using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.ServerClient
{
    internal class DebugLogger
    {
        public static void WriteLine(string message, LogType type = LogType.Info)
        {
#if !LIVEXAML_DEBUG
            if (type == LogType.Debug)
                return;
#endif

            Debug.WriteLine("livesharp: " + message);
        }
        internal static void WriteLineInfo(string message)
        {
            WriteLine(message, LogType.Info);
        }

        internal static void WriteImportant(string message)
        {
            WriteLine("=============== LiveSharp Runtime Information ===============", LogType.Info);
            WriteLine(message, LogType.Info);
            WriteLine("==============================================================", LogType.Info);
        }

        internal static void WriteLineDebug(string message)
        {
            WriteLine(message, LogType.Debug);
        }
    }

    internal enum LogType
    {
        Info, Debug
    }
}
