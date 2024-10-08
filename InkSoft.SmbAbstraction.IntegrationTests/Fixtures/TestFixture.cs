﻿using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction.IntegrationTests;

public abstract class TestFixture : IDisposable
{
    protected TestFixture()
    {
        SmbCredentialProvider = new SmbCredentialProvider();
        SmbClientFactory = new Smb2ClientFactory();
        FileSystem = new SmbFileSystem(SmbClientFactory, SmbCredentialProvider, null, null);
    }

    public TestFixture WithLoggerFactory(ILoggerFactory loggerFactory)
    {
        LoggerFactory = loggerFactory;
        FileSystem = new SmbFileSystem(SmbClientFactory, SmbCredentialProvider, null, LoggerFactory);
        return this;
    }

    public ILoggerFactory LoggerFactory { get; set; }

    public IFileSystem FileSystem { get; set; }

    public ISmbCredentialProvider SmbCredentialProvider { get; }

    public ISmbClientFactory SmbClientFactory { get; }
    
    public abstract string LocalTempDirectory { get; }
    
    public abstract ShareCredentials ShareCredentials { get; }
    
    public abstract string ShareName { get; }
    
    public abstract string RootPath { get; }
    
    public abstract List<string> Files { get; }
    
    public abstract List<string> Directories { get; }
    
    public abstract PathType PathType { get; }
    
    public virtual void Dispose(){}
}