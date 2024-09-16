using InkSoft.SmbAbstraction.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace InkSoft.SmbAbstraction.IntegrationTests.DriveInfo;

public class UncPathTests(UncPathFixture fixture, ITestOutputHelper outputHelper) : DriveInfoTests(fixture, outputHelper), IClassFixture<UncPathFixture>;

public class SmbUriTests(SmbUriFixture fixture, ITestOutputHelper outputHelper) : DriveInfoTests(fixture, outputHelper), IClassFixture<SmbUriFixture>;

public class BaseFileSystemTests(LocalFileSystemFixture fixture, ITestOutputHelper outputHelper)
    : DriveInfoTests(fixture, outputHelper), IClassFixture<LocalFileSystemFixture>;