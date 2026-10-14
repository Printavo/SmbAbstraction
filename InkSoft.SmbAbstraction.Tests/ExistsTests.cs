using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Xunit;

namespace InkSoft.SmbAbstraction.Tests;

public class ExistsTests
{
    private readonly ErrorCapturingLoggerFactory _loggerFactory = new();
    private readonly SmbFileSystem _smbFileSystem;

    public ExistsTests() => _smbFileSystem = new(new Smb2ClientFactory(), new SmbCredentialProvider(), null, _loggerFactory);

    [Theory,
     InlineData(@"\\127.0.0.1\share"),
     InlineData(@"\\127.0.0.1\share\"),
     InlineData(@"\\127.0.0.1"),
     InlineData(@"\\127.0.0.1\"),
     InlineData("smb://127.0.0.1/share"),
     InlineData("smb://127.0.0.1/share/"),
     InlineData("smb://127.0.0.1"),
    ]
    public void FileExistsReturnsFalseForShareAndHostRootsWithoutLoggingErrors(string path)
    {
        Assert.False(_smbFileSystem.File.Exists(path));
        Assert.Empty(_loggerFactory.Errors);
    }

    [Theory,
     InlineData(@"\\127.0.0.1"),
     InlineData(@"\\127.0.0.1\"),
     InlineData("smb://127.0.0.1"),
    ]
    public void DirectoryExistsReturnsFalseForHostRootWithoutLoggingErrors(string path)
    {
        Assert.False(_smbFileSystem.Directory.Exists(path));
        Assert.Empty(_loggerFactory.Errors);
    }

    [Theory,
     InlineData(@"\\127.0.0.1\share"),
     InlineData(@"\\127.0.0.1\share\"),
     InlineData("smb://127.0.0.1/share"),
    ]
    public void DirectoryExistsForShareRootGetsPastPathParsing(string path)
    {
        // There's no credential registered, so the lookup itself fails, but it shouldn't fail while parsing the share root.
        Assert.False(_smbFileSystem.Directory.Exists(path));
        var error = Assert.Single(_loggerFactory.Errors);
        Assert.IsType<SmbException>(error);
    }

    private sealed class ErrorCapturingLoggerFactory : ILoggerFactory, ILogger
    {
        public List<Exception?> Errors { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                Errors.Add(exception);
        }
    }
}
