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
    private static readonly Dictionary<int, Dictionary<IPAddress, SmbConnection>> s_instances = new();
    
    private static readonly object s_connectionLock = new();

    private readonly IPAddress _address;
    
    private readonly SMBTransportType _transport;
    
    private readonly ISmbCredential _credential;

    private long _referenceCount = 1;

    private readonly int _threadId;

    public ISMBClient SmbClient { get; }

    private SmbConnection(ISmbClientFactory smbClientFactory, IPAddress address, SMBTransportType transport, ISmbCredential credential, int threadId, SmbFileSystemOptions? smbFileSystemOptions)
    {
        SmbClient = smbClientFactory.CreateClient(smbFileSystemOptions);
        _address = address;
        _transport = transport;
        _credential = credential;
        _threadId = threadId;
    }

    private void Connect()
    {
        if (_credential.Domain == null)
            throw new InvalidCredentialException($"SMB credential is not valid. {nameof(_credential.Domain)} cannot be null.");

        if (_credential.Username == null)
            throw new InvalidCredentialException($"SMB credential is not valid. {nameof(_credential.Username)} cannot be null.");

        if (_credential.Password == null)
            throw new InvalidCredentialException($"SMB credential is not valid. {nameof(_credential.Password)} cannot be null.");

        if(!SmbClient.Connect(_address, _transport))
            throw new IOException("Unable to connect to SMB share.");

        SmbClient.Login(_credential.Domain, _credential.Username, _credential.Password).AssertSuccess();
    }

    public static SmbConnection CreateSmbConnectionForStream(ISmbClientFactory smbClientFactory, IPAddress address, SMBTransportType transport, ISmbCredential credential, SmbFileSystemOptions? smbFileSystemOptions)
    {
        #if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(credential, nameof(credential));
        #else        
            if (credential == null)
                throw new ArgumentNullException(nameof(credential));
        #endif

        // Create new connection
        var instance = new SmbConnection(smbClientFactory, address, transport, credential, -1, smbFileSystemOptions);
        instance.Connect();
        return instance;
    }

    public static SmbConnection CreateSmbConnection(ISmbClientFactory smbClientFactory, IPAddress address, SMBTransportType transport, ISmbCredential credential, SmbFileSystemOptions? smbFileSystemOptions)
    {
        #if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(credential, nameof(credential));
        #else        
            if (credential == null)
                throw new ArgumentNullException(nameof(credential));
        #endif

        int threadId = Environment.CurrentManagedThreadId;

        lock (s_connectionLock)
        {
            if (!s_instances.ContainsKey(threadId))
                s_instances.Add(threadId, new());

            SmbConnection instance;

            if (s_instances[threadId].ContainsKey(address))
            {
                instance = s_instances[threadId][address];
                if (instance.SmbClient.Connect(instance._address, instance._transport))
                {
                    instance._referenceCount += 1;
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
            instance = new(smbClientFactory, address, transport, credential, threadId, smbFileSystemOptions);
            instance.Connect();
            s_instances[threadId].Add(address, instance);
            return instance;
        }
    }

    private bool _isDisposed;

    public void Dispose()
    {
        lock (s_connectionLock)
        {
            if (_isDisposed)
                return;

            if (_referenceCount == 1)
            {
                try
                {
                    SmbClient.Logoff(); // TODO: Come back to this and try to debug more. Once you log out OR disconnect, you can't log back in for some reason.
                    SmbClient.Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    if (_threadId != -1)
                    {
                        s_instances[_threadId].Remove(_address);
                        if (s_instances[_threadId].Count == 0)
                        {
                            s_instances.Remove(_threadId);
                        }
                    }

                    _isDisposed = true;
                }
            }
            else
            {
                _referenceCount -= 1;
            }
        }

        GC.SuppressFinalize(this);
    }
}