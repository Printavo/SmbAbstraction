using System;

namespace InkSoft.SmbAbstraction;

public interface ISmbCredential : IDisposable
{
    string Domain { get; }
    
    string Username { get; }
    
    string Password { get; }
    
    string Host { get; }
    
    string? ShareName { get; }

    string? Path { get; }
}