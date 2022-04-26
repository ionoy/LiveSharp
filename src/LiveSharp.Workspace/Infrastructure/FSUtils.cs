using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveSharp.Infrastructure
{
    class FSUtils
    {
        public static async Task<(bool, string)> ReadFileTextAsync(string filePath)
        {
            string text = null;

            for (int i = 0; i < 100; i++)
            {
                try
                {
                    text = File.ReadAllText(filePath);
                    return (true, text);
                }
                catch
                {
                    await Task.Delay(1);
                }
            }

            return (false, "");
        }
        public static async Task<string[]> ReadFileLinesAsync(string filePath)
        {
            for (int i = 0; i < 100; i++)
            {
                try
                {
                    return File.ReadAllLines(filePath);
                }
                catch
                {
                    await Task.Delay(1);
                }
            }

            throw new Exception("Couldn't read lines from " + filePath);
        }
    }
}
