using InkSoft.SmbAbstraction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;

class Program
{
public static void Main(string[] args)
{
    var app = CreateHostBuilder(args).Build();
    
    var fileSystem = app.Services.GetRequiredService<IFileSystem>();
    
    // Do stuff with fileSystem before app.Run for misc. testing outside DI...

    app.Run();
}

public static IHostBuilder CreateHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging => { logging.AddConsole(); logging.AddDebug(); })
    .ConfigureServices((hostContext, services) => services
        .AddSingleton<ISmbCredentialProvider>(p => new SmbCredentialProvider())
        // If you don't want to use SmbFileSystem as the default IFileSystem, you can use something like following line instead for specifically requesting it via [FromKeyedServices(nameof(SmbFileSystem))]
        // without using the SmbFileSystem type. This makes it such that you can unit-test code with MockFileSystem by registering MockFileSystem to the keyed service instead of an actual SmbFileSystem.
        .AddKeyedSingleton<IFileSystem>(nameof(SmbFileSystem), (p, _) => new SmbFileSystem(new Smb2ClientFactory(), p.GetRequiredService<ISmbCredentialProvider>(), null, p.GetRequiredService<ILoggerFactory>()))
        // Otherwise, this makes SmbFileSystem the default IFileSystem:
        .AddSingleton<IFileSystem>(p => new SmbFileSystem(
            new Smb2ClientFactory(),
            p.GetRequiredService<ISmbCredentialProvider>(),
            null,
            p.GetRequiredService<ILoggerFactory>()
        )));
}
