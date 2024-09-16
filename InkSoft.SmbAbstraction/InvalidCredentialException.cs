using System;

namespace InkSoft.SmbAbstraction;

[Serializable]
public class InvalidCredentialException : Exception
{
    public InvalidCredentialException(){}

    public InvalidCredentialException(string message) : base(message){}        
}