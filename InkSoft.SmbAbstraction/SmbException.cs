using System;

namespace InkSoft.SmbAbstraction;

[Serializable]
public class SmbException : Exception
{
    public SmbException(){}

    public SmbException(string message) : base(message){}

    public SmbException(string message, Exception exception) : base(message, exception){}
}