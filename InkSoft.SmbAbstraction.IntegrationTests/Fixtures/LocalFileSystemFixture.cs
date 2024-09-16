using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace InkSoft.SmbAbstraction.IntegrationTests.Fixtures;

public class LocalFileSystemFixture : TestFixture
{
    private readonly IntegrationTestSettings _settings = new();

    public LocalFileSystemFixture() => _settings.Initialize();

    public override string LocalTempDirectory
    {
        get
        {
            if (!string.IsNullOrEmpty(_settings.LocalTempFolder))
            {
                return _settings.LocalTempFolder;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.IO.Directory.CreateDirectory($@"C:\temp\tests");
                return $@"C:\temp\tests";
            }
            else
            {
                System.IO.Directory.CreateDirectory($@"{Environment.GetEnvironmentVariable("HOME")}/temp/tests");
                return $@"{Environment.GetEnvironmentVariable("HOME")}/temp/tests";
            }
        }
    }

    public override ShareCredentials ShareCredentials => _settings.ShareCredentials;
    public override string ShareName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? System.IO.Path.GetPathRoot(RootPath) : "/";
    public override string RootPath => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $@"C:\temp" : $@"{Environment.GetEnvironmentVariable("HOME")}/temp";

    public override List<string> Files
    {
        get
        {
            foreach(string? file in _settings.Shares.First().Files)
            {
                string? path = System.IO.Path.Combine(RootPath, file);
                var fileStream = System.IO.File.Create(path);
                fileStream.Close();
            }

            return _settings.Shares.First().Files;
        }
    }

    public override List<string> Directories
    {
        get
        {
            foreach (string? directory in _settings.Shares.First().Directories)
            {
                string? path = System.IO.Path.Combine(RootPath, directory);
                System.IO.Directory.CreateDirectory(path);
            }

            return _settings.Shares.First().Directories;
        }
    }

    public override PathType PathType => PathType.HostFileSystem;
}