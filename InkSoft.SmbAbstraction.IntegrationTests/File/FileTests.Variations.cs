using InkSoft.SmbAbstraction.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace InkSoft.SmbAbstraction.IntegrationTests.File;

public class UncPathTests(UncPathFixture fixture, ITestOutputHelper outputHelper) : FileTests(fixture, outputHelper), IClassFixture<UncPathFixture>;

public class SmbUriTests(SmbUriFixture fixture, ITestOutputHelper outputHelper) : FileTests(fixture, outputHelper), IClassFixture<SmbUriFixture>;

public class BaseFileSystemTests(LocalFileSystemFixture fixture, ITestOutputHelper outputHelper)
    : FileTests(fixture, outputHelper), IClassFixture<LocalFileSystemFixture>;