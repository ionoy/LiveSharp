using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LaunchPad.Runtime.Web;

public class LaunchPadHost : IHost, IAsyncDisposable
{
    private static IHost? _currentHost;
    private readonly IServiceProvider _serviceProvider;
    private static readonly SemaphoreSlim SemaphoreSlim = new (0);
    private Lazy<IHost> _parentHost => new(() => _serviceProvider.GetServices<IHost>().FirstOrDefault(h => h.GetType() != typeof(LaunchPadHost)));
    private IHost ParentHost => _parentHost.Value;
    
    public LaunchPadHost(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        var currentHost = _currentHost;

        _currentHost = null;

        if (currentHost != null)
        {
            Console.WriteLine("before stop");
            await currentHost.StopAsync(cancellationToken);
            Console.WriteLine("After stop");
        }
        
        _currentHost = this;
        await ParentHost.StartAsync(cancellationToken);
        
        //if (currentHost != null)
        //_semaphoreSlim.Release();
        
        await SemaphoreSlim.WaitAsync();
        Console.WriteLine("!!!Start async finished");
    }
    
    public async Task StopAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        await ParentHost.StopAsync(cancellationToken);
        Console.WriteLine("!!!Stop async called");
    }
    
    public IServiceProvider Services => ParentHost.Services;
    
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Dispose() => ParentHost.Dispose();
}