using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Internals;

namespace LiveSharp.RuntimeTests
{
    public partial class TestRunner
    {
        internal class MockPlatformServices : IPlatformServices
        {
            Action<Action> _invokeOnMainThread;
            Action<Uri> _openUriAction;
            Func<Uri, CancellationToken, Task<Stream>> _getStreamAsync;
            public MockPlatformServices(Action<Action> invokeOnMainThread = null, Action<Uri> openUriAction = null, Func<Uri, CancellationToken, Task<Stream>> getStreamAsync = null)
            {
                _invokeOnMainThread = invokeOnMainThread;
                _openUriAction = openUriAction;
                _getStreamAsync = getStreamAsync;
            }

            public string GetHash(string input)
            {
                return input;
            }

            public string GetMD5Hash(string input)
            {
                throw new NotImplementedException();
            }
            static int hex(int v)
            {
                if (v < 10)
                    return '0' + v;
                var a = v - 10;
                return 'a' + a;
            }

            public double GetNamedSize(NamedSize size, Type targetElement, bool useOldSizes)
            {
                switch (size) {
                    case NamedSize.Default:
                        return 10;
                    case NamedSize.Micro:
                        return 4;
                    case NamedSize.Small:
                        return 8;
                    case NamedSize.Medium:
                        return 12;
                    case NamedSize.Large:
                        return 16;
                    default:
                        throw new ArgumentOutOfRangeException("size");
                }
            }

            public Color GetNamedColor(string name)
            {
                return Color.Accent;
            }

            public void OpenUriAction(Uri uri)
            {
                if (_openUriAction != null)
                    _openUriAction(uri);
                else
                    throw new NotImplementedException();
            }

            public bool IsInvokeRequired {
                get { return false; }
            }

            public OSAppTheme RequestedTheme => OSAppTheme.Light;

            public string RuntimePlatform { get; set; }

            public void BeginInvokeOnMainThread(Action action)
            {
                if (_invokeOnMainThread == null)
                    action();
                else
                    _invokeOnMainThread(action);
            }

            public Ticker CreateTicker()
            {
                return new MockTicker();
            }

            public void StartTimer(TimeSpan interval, Func<bool> callback)
            {
                Timer timer = null;
                TimerCallback onTimeout = o => BeginInvokeOnMainThread(() => {
                    if (callback())
                        return;

                    timer.Dispose();
                });
                timer = new Timer(onTimeout, null, interval, interval);
            }

            public Task<Stream> GetStreamAsync(Uri uri, CancellationToken cancellationToken)
            {
                if (_getStreamAsync == null)
                    throw new NotImplementedException();
                return _getStreamAsync(uri, cancellationToken);
            }

            public Assembly[] GetAssemblies()
            {
                return new Assembly[0];
            }

            public IIsolatedStorageFile GetUserStoreForApplication()
            {
                throw new NotImplementedException();
            }

            public void QuitApplication()
            {
                throw new NotImplementedException();
            }

            public SizeRequest GetNativeSize(VisualElement view, double widthConstraint, double heightConstraint)
            {
                return new SizeRequest(new Size(widthConstraint, heightConstraint));
            }
        }
    }
}