using System;
using System.IO;
using System.Reactive.Linq;

namespace LiveSharp.SequenceRecorder
{
    class Program
    {
        private static string _fileToWatch;
        private static int _counter;
        private static string _outDirectory;

        static void Main(string[] args)
        {
            _fileToWatch = args[0];
            _outDirectory = args[1];

            var sequenceSubDirName = Guid.NewGuid().ToString("N");

            _outDirectory = Path.Combine(_outDirectory, sequenceSubDirName);

            if (!Directory.Exists(_outDirectory))
                Directory.CreateDirectory(_outDirectory);
            
            SavePoint();
            
            var watcher =
                new FileSystemWatcher(Path.GetDirectoryName(_fileToWatch)) {
                    Filter = Path.GetFileName(_fileToWatch),
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName, 
                    EnableRaisingEvents = true
                };

            var observable = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(h => watcher.Changed += h, h => watcher.Changed -= h);

            var subscribe = observable.Throttle(TimeSpan.FromMilliseconds(50))
                      .Subscribe(p => FileChanged(p.Sender, p.EventArgs));

            Console.WriteLine($"watcher created at {_fileToWatch}");
            
            Console.ReadKey();
            subscribe.Dispose();
        }

        private static void FileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine(e.ChangeType);
            SavePoint("." + ++_counter);
        }

        private static void SavePoint(string suffix = "")
        {
            var contents = File.ReadAllText(_fileToWatch);

            var filename = Path.GetFileName(_fileToWatch);
            var outFilepath = Path.Combine(_outDirectory, filename + suffix);
            
            File.WriteAllText(outFilepath, contents);
            
            Console.WriteLine("Saved to " + outFilepath);
        }
    }
}