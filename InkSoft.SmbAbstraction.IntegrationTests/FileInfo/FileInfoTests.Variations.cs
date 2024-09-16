using InkSoft.SmbAbstraction.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace InkSoft.SmbAbstraction.IntegrationTests.FileInfo;

public class UncPathTests(UncPathFixture fixture, ITestOutputHelper outputHelper) : FileInfoTests(fixture, outputHelper), IClassFixture<UncPathFixture>;

public class SmbUriTests(SmbUriFixture fixture, ITestOutputHelper outputHelper) : FileInfoTests(fixture, outputHelper), IClassFixture<SmbUriFixture>;

public class BaseFileSystemTests(LocalFileSystemFixture fixture, ITestOutputHelper outputHelper)
    : FileInfoTests(fixture, outputHelper), IClassFixture<LocalFileSystemFixture>;