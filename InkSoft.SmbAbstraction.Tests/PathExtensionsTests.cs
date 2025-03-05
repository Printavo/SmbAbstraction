using System.Linq;
using Xunit;

namespace InkSoft.SmbAbstraction.Tests.Path;

public class PathExtensionsTests
{
    private readonly IPathTestData _smbUriTestData = new SmbUriTestData();
    private readonly IPathTestData _uncPathTestData = new UncPathTestData();
    private readonly SmbFileSystem _smbFileSystem = new(new Smb2ClientFactory(), new SmbCredentialProvider(), null, null);


    [Theory,
     InlineData(@"C:\MyDir\MySubDir\","myfile.ext", @"C:\MyDir\MySubDir\myfile.ext"),
     InlineData(@"C:\MyDir\MySubDir/","myfile.ext", @"C:\MyDir\MySubDir\myfile.ext"),
     InlineData(@"C:\MyDir\",@"MySubDir\myfile.ext", @"C:\MyDir\MySubDir\myfile.ext"),
     InlineData(@"C:\MyDir/",@"MySubDir\myfile.ext", @"C:\MyDir\MySubDir\myfile.ext"),
     InlineData(@"C:\MyDir\","MySubDir/myfile.ext", @"C:\MyDir\MySubDir\myfile.ext"),
     InlineData(@"C:\MyDir//",@"MySubDir\myfile.ext", @"C:\MyDir\MySubDir\myfile.ext"),
     InlineData(@"C:\MyDir\\","MySubDir/myfile.ext", @"C:\MyDir\MySubDir\myfile.ext"),
     InlineData(@"C:\MyDir",@"MySubDir\myfile.ext", @"C:\MyDir\MySubDir\myfile.ext"),
    ]
    public void PathCombineShouldWorkConsistently(string path1, string path2, string smbRelatedOutput)
    {
        // Default behavior should remain unchanged.
        Assert.Equal(System.IO.Path.Combine(path1, path2), _smbFileSystem.Path.Combine(path1, path2));

        // SmbFileSystem should treat C:\ the same as a file share root.
        path1 = path1.Replace(@"C:\", @"\\server\share\");
        path2 = path2?.Replace(@"C:\", @"\\server\share\");
        smbRelatedOutput = smbRelatedOutput?.Replace(@"C:\", @"\\server\share\");
        Assert.Equal(smbRelatedOutput, _smbFileSystem.Path.Combine(path1, path2));

        // It should also work with smb:// paths in the same way.
        path1 = "smb://"+path1[2..];
        smbRelatedOutput = "smb:"+smbRelatedOutput.Replace('\\', '/');
        
        Assert.Equal(smbRelatedOutput, _smbFileSystem.Path.Combine(path1, path2));
    }

    [Theory,
     InlineData(@"C:\MyDir\MySubDir\myfile.ext", @"C:\MyDir\MySubDir"),
     InlineData(@"C:\MyDir\MySubDir", @"C:\MyDir"),
     InlineData(@"C:\MyDir\", @"C:\MyDir"),
     InlineData(@"C:\MyDir", @"C:\"),
     InlineData(@"C:\", null),
    ]
    public void GetDirectoryNameShouldWorkConsistently(string inputPath, string? outputPath)
    {
        // Default behavior should remain unchanged.
        Assert.Equal(outputPath, _smbFileSystem.Path.GetDirectoryName(inputPath));

        // SmbFileSystem should treat C:\ the same as a file share root.
        inputPath = inputPath.Replace(@"C:\", @"\\server\share\");
        outputPath = outputPath?.Replace(@"C:\", @"\\server\share\");
        Assert.Equal(outputPath, _smbFileSystem.Path.GetDirectoryName(inputPath));

        // It should also work with smb:// paths in the same way.
        inputPath = "smb:"+inputPath.Replace('\\', '/');
        
        if (outputPath != null)
            outputPath = "smb:"+outputPath.Replace('\\', '/');
        
        Assert.Equal(outputPath, _smbFileSystem.Path.GetDirectoryName(inputPath));
    }

    [Fact]
    public void IsSharePath_ReturnsFalse_ForLocalUrl()
    {
        string path = @"C:\jordan\lytle";
        Assert.False(path.IsSharePath());
    }

    [Fact]
    public void IsSharePath_ReturnsTrue_ForSmbUrl()
    {
        foreach (var property in _smbUriTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_smbUriTestData);
            Assert.True(path.IsSharePath());
        }
    }

    [Fact]
    public void IsSharePath_ReturnsTrue_ForUncPath()
    {
        foreach (var property in _uncPathTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_uncPathTestData);
            Assert.True(path.IsSharePath());
        }
    }

    [Fact]
    public void IsSmbUri_ReturnsTrue_ForSmbUrl()
    {
        foreach (var property in _smbUriTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_smbUriTestData);
            Assert.True(path.IsSmbUri());
        }
    }

    [Fact]
    public void IsSmbUri_ReturnsFalse_ForUncPath()
    {
        foreach (var property in _uncPathTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_uncPathTestData);
            Assert.False(path.IsSmbUri());
        }
    }

    [Fact]
    public void IsUncPath_ReturnsTrue_ForUncPath()
    {
        foreach (var property in _uncPathTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_uncPathTestData);
            Assert.True(path.IsUncPath());
        }
    }

    [Fact]
    public void IsUncPath_ReturnsFalse_ForSmbUri()
    {
        foreach (var property in _smbUriTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_smbUriTestData);
            Assert.False(path.IsUncPath());
        }
    }

    [Fact]
    public void BuildSharePath_ReturnsSmbPath_ForSmbPath()
    {
        foreach (var property in _smbUriTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_smbUriTestData);
            string testBuildShareName = "TestBuildSharePath";
            string? builtSharePath = path.BuildSharePath(testBuildShareName);
            string expectedPath = $"smb://{path.Hostname()}/{testBuildShareName}";
            Assert.Equal(expectedPath, builtSharePath);
        }
    }

    [Fact]
    public void BuildSharePath_ReturnsSmbPath_ForUncPath()
    {
        foreach (var property in _uncPathTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_uncPathTestData);
            string? testBuildShareName = "TestBuildSharePath";
            string? builtSharePath = path.BuildSharePath(testBuildShareName);
            string expectedPath = $@"\\{path.Hostname()}\{testBuildShareName}";
            Assert.Equal(expectedPath, builtSharePath);
        }
    }

    [Fact]
    public void HostName_ReturnsHost_ForSmbUrl()
    {
        foreach (var property in _smbUriTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_smbUriTestData);
            string hostName = path.Hostname();
            Assert.Equal("host", hostName);
        }
    }

    [Fact]
    public void HostName_ReturnsHost_ForUncPath()
    {
        foreach (var property in _uncPathTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_uncPathTestData);
            string hostName = path.Hostname();
            Assert.Equal("host", hostName);
        }
    }


    [Fact]
    public void SharePath_ReturnsSharePath_ForSmbUri()
    {
        foreach (var property in _smbUriTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_smbUriTestData);
            string sharePath = path.SharePath();
            Assert.Equal(_smbUriTestData.Root, sharePath);
        }
    }

    [Fact]
    public void SharePath_ReturnsSharePath_ForUncPath()
    {
        foreach (var property in _uncPathTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_uncPathTestData);
            string? sharePath = path.SharePath();
            Assert.Equal(_uncPathTestData.Root, sharePath);
        }
    }

    [Fact]
    public void ShareName_ReturnsShare_ForSmbUri()
    {
        foreach (var property in _smbUriTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_smbUriTestData);
            string shareName = path.ShareName();
            Assert.Equal("share", shareName);
        }
    }

    [Fact]
    public void ShareName_ReturnsShare_ForUncPath()
    {
        foreach (var property in _uncPathTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_uncPathTestData);
            string shareName = path.ShareName();
            Assert.Equal("share", shareName);
        }
    }

    [Fact]
    public void RelativeSharePath_ReturnsPathAfterShareRoot_ForSmbUri()
    {
        foreach (var property in _smbUriTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_smbUriTestData);
            string? relative = RemoveTrailingSeperator(RemoveLeadingSeperator(ReplacePathSeperators(path.Replace(_smbUriTestData.Root, ""), @"\")));
            string relativeSharePath = path.ShareRelativePath();
            Assert.Equal(relative, relativeSharePath);
        }
    }

    [Fact]
    public void RelativeSharePath_ReturnsPathAfterShareRoot_ForUncPath()
    {
        foreach (var property in _uncPathTestData.GetType().GetProperties())
        {
            string? path = (string)property.GetValue(_uncPathTestData);
            string? relative = RemoveTrailingSeperator(RemoveLeadingSeperator(ReplacePathSeperators(path.Replace(_uncPathTestData.Root, ""), @"\")));
            string relativeSharePath = path.ShareRelativePath();
            Assert.Equal(relative, relativeSharePath);
        }
    }

    private string ReplacePathSeperators(string input, string newValue)
    {
        string[] pathSeperators = [@"\", @"/"];
        return pathSeperators.Aggregate(input, (current, pathSeperator) => current.Replace(pathSeperator, newValue));
    }

    private string RemoveLeadingSeperator(string input)
    {
        string[] pathSeparators = [@"\", @"/"];

        foreach (string? pathSeparator in pathSeparators)
        {
            if (input.StartsWith(pathSeparator))
                input = input.Remove(0, 1);
        }

        return input;
    }

    private string RemoveTrailingSeperator(string input)
    {
        string[] pathSeperators = [@"\", @"/"];

        foreach (string? pathSeperator in pathSeperators)
        {
            if (input.EndsWith(pathSeperator))
               input = input.Remove(input.LastIndexOf(pathSeperator), 1);
        }

        return input;
    }
}