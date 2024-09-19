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
            if (string.IsNullOrWhiteSpace(_settings.LocalTempFolder))
                throw new ArgumentException("LocalTempFolder must be set in appsettings.gitignore.json");

            return _settings.LocalTempFolder;
        }
    }

    public override ShareCredentials ShareCredentials => _settings.ShareCredentials;
    public override string ShareName => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? System.IO.Path.GetPathRoot(RootPath) : "/";
    public override string RootPath => FileSystem.Path.Combine(LocalTempDirectory, "testRoot");

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