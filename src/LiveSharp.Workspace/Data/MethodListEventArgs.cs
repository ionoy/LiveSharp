using System;

namespace LiveSharp.Ide.Data
{
    public class MethodListEventArgs : EventArgs
    {
        public string[] MethodList { get; set; } = new string[0];
    }
}