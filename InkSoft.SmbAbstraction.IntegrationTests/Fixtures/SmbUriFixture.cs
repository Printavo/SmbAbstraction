using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace InkSoft.SmbAbstraction.IntegrationTests.Fixtures;

public class SmbUriFixture : TestFixture
{

    private readonly IntegrationTestSettings _settings = new();

    public SmbUriFixture() => _settings.Initialize();


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

    public override string ShareName => RootPath.ShareName();
    public override string RootPath => _settings.Shares.First().GetRootPath(PathType.SmbUri);

    public override List<string> Files => _settings.Shares.First().Files;

    public override List<string> Directories => _settings.Shares.First().Directories;

    public override PathType PathType => PathType.SmbUri;
}