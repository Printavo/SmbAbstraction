﻿using System;
using System.IO.Abstractions;

namespace InkSoft.SmbAbstraction;

public class SmbFileSystemWatcherFactory(IFileSystem fileSystem) : FileSystemWatcherFactory(fileSystem)
{
    public new IFileSystemWatcher New(string path)
    {
        if (path.IsSharePath())
        {
            return base.New(path);
        }

        throw new NotSupportedException();
    }
}