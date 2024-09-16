using InkSoft.SmbAbstraction.IntegrationTests.Fixtures;
using Xunit;

namespace InkSoft.SmbAbstraction.IntegrationTests.Stream;

public class UncPathTests(UncPathFixture fixture) : StreamTests(fixture), IClassFixture<UncPathFixture>;

public class SmbUriTests(SmbUriFixture fixture) : StreamTests(fixture), IClassFixture<SmbUriFixture>;

public class BaseFileSystemTests(LocalFileSystemFixture fixture) : StreamTests(fixture), IClassFixture<LocalFileSystemFixture>;