using System;

namespace InkSoft.SmbAbstraction;

public interface ISmbCredential : IDisposable
{
    string Domain { get; }
    string UserName { get; }
    string Password { get; }
    string Path { get; }
    string Host { get; }
    string ShareName { get; }
}