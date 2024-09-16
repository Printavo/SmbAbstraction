namespace InkSoft.SmbAbstraction.Tests.Path;

public interface IPathTestData
{
    public string Root { get; }
    public string DirectoryAtRoot { get; }
    public string FileAtRoot { get; }
    public string NestedDirectoryAtRoot { get; }
    public string FileInNestedDirectoryAtRoot { get; }
        
}

public class SmbUriTestData : IPathTestData
{
    public string Root => "smb://host/share";
    public string DirectoryAtRoot => "smb://host/share/dir";
    public string DirectoryAtRootWithTrailingSlash => "smb://host/share/dir";
    public string SpaceInDirectoryAtRoot => "smb://host/share/dir dir/file.txt";
    public string FileAtRoot => "smb://host/share/file.txt";
    public string SpaceInFileAtRoot => "smb://host/share/text file.txt";
    public string NestedDirectoryAtRoot => "smb://host/share/dir/nested_dir";
    public string NestedDirectoryAtRootWithTrailingSlash => "smb://host/share/dir/nested_dir/";
    public string FileInNestedDirectoryAtRoot => "smb://host/share/dir/nested_dir/file.txt";
    public string SpaceInFileInNestedDirectoryAtRoot => "smb://host/share/dir/nested_dir/text file.txt";
    public string SpaceInFileAndInNestedDirectoryAtRoot => "smb://host/share/dir/nested dir/text file.txt";
}

public class UncPathTestData : IPathTestData
{
    public string Root => $@"\\host\share";
    public string DirectoryAtRoot => $@"\\host\share\dir";
    public string DirectoryAtRootWithTrailingSlash => $@"\\host\share\dir\";
    public string SpaceInDirectoryAtRoot => @"\\host\share\dir dir\file.txt";
    public string FileAtRoot => $@"\\host\share\file.txt";
    public string SpaceInFileAtRoot => @"\\host\share\text file.txt";
    public string NestedDirectoryAtRoot => $@"\\host\share\dir\nested_dir";
    public string NestedDirectoryAtRootWithTrailingSlash => $@"\\host\share\dir\nested_dir\";
    public string SpaceInNestedDirectoryAtRoot => $@"\\host\share\dir\nested dir";
    public string SpaceInNestedDirectoryAtRootWithTrailingSlash => $@"\\host\share\dir\nested dir\";
    public string FileInNestedDirectoryAtRoot => $@"\\host\share\dir\nested_dir\file.text";
    public string SpaceInFileInNestedDirectoryAtRoot => $@"\\host\share\dir\nested_dir\text file.text";
    public string SpaceInFileAndInNestedDirectoryAtRoot => $@"\\host\share\dir\nested dir\text file.text";
}