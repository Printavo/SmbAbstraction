using InkSoft.SmbAbstraction.IntegrationTests.Fixtures;
using System.Threading;
using Xunit;

namespace InkSoft.SmbAbstraction.IntegrationTests.File;

public class UncPathTests(UncPathFixture fixture, ITestOutputHelper outputHelper, CancellationToken testCancellationToken = default): FileTests(fixture, outputHelper, testCancellationToken), IClassFixture<UncPathFixture>;

public class SmbUriTests(SmbUriFixture fixture, ITestOutputHelper outputHelper, CancellationToken testCancellationToken = default): FileTests(fixture, outputHelper, testCancellationToken), IClassFixture<SmbUriFixture>;

public class BaseFileSystemTests(LocalFileSystemFixture fixture, ITestOutputHelper outputHelper, CancellationToken testCancellationToken = default): FileTests(fixture, outputHelper, testCancellationToken), IClassFixture<LocalFileSystemFixture>;