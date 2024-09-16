using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using SMBLibrary;
using SMBLibrary.Client;
using System.Threading;

namespace InkSoft.SmbAbstraction;

public class SmbConnection : IDisposable
{
    private static Dictionary<int, Dictionary<IPAddress, SmbConnection>> s_instances = new();
    private static readonly object s_connectionLock = new();

    private readonly IPAddress _address;
    private readonly SMBTransportType _transport;
    private readonly ISmbCredential _credential;
    private long ReferenceCount { get; set; }
    private bool IsDesposed { get; set; }
    private int ThreadId { get; }

    public readonly ISMBClient SmbClient;

    private SmbConnection(ISmbClientFactory smbClientFactory, IPAddress address, SMBTransportType transport,
        ISmbCredential credential, int threadId, uint maxBufferSize)
    {
        SmbClient = smbClientFactory.CreateClient(maxBufferSize);
        _address = address;
        _transport = transport;
        _credential = credential;
        ReferenceCount = 1;
        ThreadId = threadId;
    }

    private void Connect()
    {
        ValidateCredential(_credential);

        bool succeeded = SmbClient.Connect(_address, _transport);
        if(!succeeded)
        {
            throw new IOException($"Unable to connect to SMB share.");
        }
        var status = SmbClient.Login(_credential.Domain, _credential.UserName, _credential.Password);

        status.HandleStatus();
    }

    public static SmbConnection CreateSmbConnectionForStream(ISmbClientFactory smbClientFactory,
        IPAddress address, SMBTransportType transport, ISmbCredential credential, uint maxBufferSize)
    {
        if (credential == null)
        {
            throw new ArgumentNullException(nameof(credential));
        }

        // Create new connection
        var instance = new SmbConnection(smbClientFactory, address, transport, credential, -1,
            maxBufferSize);
        instance.Connect();
        return instance;
    }

    public static SmbConnection CreateSmbConnection(ISmbClientFactory smbClientFactory,
        IPAddress address, SMBTransportType transport, ISmbCredential credential, uint maxBufferSize)
    {
        int threadId = Thread.CurrentThread.ManagedThreadId;

        if (credential == null)
        {
            throw new ArgumentNullException(nameof(credential));
        }

        lock (s_connectionLock)
        {
            if(!s_instances.ContainsKey(threadId))
            {
                s_instances.Add(threadId, new());
            }

            SmbConnection instance = null;

            if (s_instances[threadId].ContainsKey(address))
            {
                instance = s_instances[threadId][address];
                if (instance.SmbClient.Connect(instance._address, instance._transport))
                {
                    instance.ReferenceCount += 1;
                    return instance;
                }

                // in case the connection is not connected, dispose it and recreate a new one
                instance.Dispose();
                if (!s_instances.ContainsKey(threadId))
                {
                    s_instances.Add(threadId, new());
                }
            }

            // Create new connection
            instance = new(smbClientFactory, address, transport, credential, threadId,
                maxBufferSize);
            instance.Connect();
            s_instances[threadId].Add(address, instance);
            return instance;
        }
    }

    public void Dispose()
    {
        lock (s_connectionLock)
        {
            if (IsDesposed)
            {
                return;
            }

            if (ReferenceCount == 1)
            {
                try
                {
                    SmbClient.Logoff(); //Once you logout OR disconnect you can't log back in for some reason. TODO come back to this and try to debug more.
                    SmbClient.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    if (ThreadId != -1)
                    {
                        s_instances[ThreadId].Remove(_address);
                        if (s_instances[ThreadId].Count == 0)
                        {
                            s_instances.Remove(ThreadId);
                        }
                    }

                    IsDesposed = true;
                }
            }
            else
            {
                ReferenceCount -= 1;
            }
        }
    }

    private void ValidateCredential(ISmbCredential credential)
    {
        if(credential.Domain == null)
        {
            throw new InvalidCredentialException($"SMB credential is not valid. {nameof(credential.Domain)} cannot be null.");
        }
        else if (credential.UserName == null)
        {
            throw new InvalidCredentialException($"SMB credential is not valid. {nameof(credential.UserName)} cannot be null.");
        }
        else if (credential.Password == null)
        {
            throw new InvalidCredentialException($"SMB credential is not valid. {nameof(credential.Password)} cannot be null.");
        }
    }
}