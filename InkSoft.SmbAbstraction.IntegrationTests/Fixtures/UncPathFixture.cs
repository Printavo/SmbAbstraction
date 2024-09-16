﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace InkSoft.SmbAbstraction.IntegrationTests.Fixtures;

public class UncPathFixture : TestFixture
{
    private readonly IntegrationTestSettings _settings = new();

    public UncPathFixture() => _settings.Initialize();


    public override string LocalTempDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(_settings.LocalTempFolder))
                return _settings.LocalTempFolder;

            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $@"C:\temp" : $"{Environment.GetEnvironmentVariable("HOME")}/";
        }
    }

    public override ShareCredentials ShareCredentials => _settings.ShareCredentials;

    public override string ShareName => RootPath.ShareName(); 
    public override string RootPath => _settings.Shares.First().GetRootPath(PathType.UncPath);

    public override List<string> Files => _settings.Shares.First().Files;

    public override List<string> Directories => _settings.Shares.First().Directories;

    public override PathType PathType => PathType.UncPath;
}