﻿using Microsoft.Extensions.Logging;
using PacketTcp.Events;
using PacketTcp.Handler;
using PacketTcp.Managers;
using PacketTcp.Packets;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace PacketTcp;


public class PacketServer(int port,PacketManager manager,ILogger? logger=null) : IDisposable
{
    private readonly Socket _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    private readonly HashSet<Task> clientTasks = [];
    private readonly Dictionary<Guid,Client> _clients = [];
    private readonly Dictionary<Guid, IHandler> _hanlders = [];
    private Thread? _listenThread;
    private Thread? _sendThread;
    private bool _isListening = false;

    private readonly Queue<PacketEvent> packetsNeedToSend = new();

    public bool IsListening => _isListening;
    public HashSet<Client> Clients => _clients.Values.ToHashSet();

    public event PacketHandler? PacketReceived;
    public event PacketHandler? PacketSent;
    public event PacketHandler? PacketSend;
    public event ClientHandler? ClientConnected;
    public event ClientHandler? ClientDisconnected;

    /// <summary>
    /// Start the server and listen for incoming connections.
    /// </summary>
    public void Start()
    {
        if(manager.Option.SyncClientId) this.AddPacketHanlder<RequestClientIDC2SPacket>((e, p) =>
        {
            var packet = new RequestClientIDS2CPacket();
            packet.ClientId = GetID(e.Client);
            e.SendCallback(packet);
        });

        _socket.Bind(new IPEndPoint(IPAddress.Any, port));
        _socket.Listen(manager.Option.MaxClientCount);
        _listenThread = new Thread(ListenThread);
        _sendThread = new Thread(SendThread);
        _listenThread.Start();
        _sendThread.Start();
        _isListening = true;
    }

    /// <summary>
    /// Join the server and wait for it to finish.
    /// </summary>
    public void Join()
    {
        _listenThread?.Join();
        _sendThread?.Join();
        Stop();
        Task.WaitAll(clientTasks);
    }

    /// <summary>
    /// Send a packet to a specific client.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="packet"></param>
    public void Send(Guid client, Packet packet)
    {
        this.Send(_clients[client].Socket, packet);
    }
    /// <summary>
    /// Send a packet to a specific client.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="packet"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Send(Socket client,Packet packet)
    {
        if (!IsListening) throw new InvalidOperationException("Server is not listening.");
        PacketEvent @event = new PacketEvent(packet.GetType(), packet, client);
        PacketSend?.Invoke(@event);
        if (packetsNeedToSend.Count > manager.Option.MaxPacketCountPerClient * _clients.Count) throw new InvalidOperationException("Packet queue is full.");
        lock (packetsNeedToSend)
        {
            packetsNeedToSend.Enqueue(@event);
        }
    }

    /// <summary>
    /// Add a packet handler for a specific packet type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="hanlder"></param>
    /// <returns></returns>
    public Guid AddPacketHanlder<T>(Action<PacketEvent,T> hanlder) where T : Packet
    {
        Guid guid = Guid.NewGuid();
        _hanlders.Add(guid, new PacketReceiveHanlder { PacketType = typeof(T),Handler=hanlder});
        return guid;
    }

    /// <summary>
    /// Remove a packet handler by its ID.
    /// </summary>
    /// <param name="guid"></param>
    public void RemovePacketHandler(Guid guid)
    {
        if (_hanlders.ContainsKey(guid))
        {
            _hanlders.Remove(guid);
        }
    }
    private void Stop()
    {
        if (!IsListening || _listenThread == null) return;
        _isListening = false;
        _listenThread = null;
        _socket.Shutdown(SocketShutdown.Both);
        _socket.Close();
        _socket.Dispose();
    }

    private void ListenThread()
    {
        while (IsListening)
        {
            try
            {
                var client = _socket.Accept();
                Guid clientId = Guid.NewGuid();
                lock (_clients)
                {
                    _clients.Add(clientId,new Client { Id=clientId,Socket=client,PacketServer=this});
                }
                logger?.LogInformation("Client connected: {ClientId}", clientId);
                clientTasks.Add(Task.Run(() => HandleClient(client,clientId)));
                ClientConnected?.Invoke(client, clientId);
            }
            catch (SocketException ex)
            {
                logger?.LogError("Socket exception: {Expection}", ex.Message);
            }
        }
    }
    private void SendThread()
    {
        while (IsListening)
        {
            lock (packetsNeedToSend)
            {
                while (packetsNeedToSend.Count > 0)
                {
                    var packetEvent = packetsNeedToSend.Dequeue();
                    if (packetEvent.IsCancelled) return;
                    var client = packetEvent.Client;
                    byte[] data = manager.SerializePacket(packetEvent.Packet);
                    client.Send(data);
                    PacketSent?.Invoke(packetEvent);
                }
            }
            Thread.Sleep(manager.Option.PacketWaitTime); // Prevent busy waiting
        }
    }
    private void HandleClient(Socket client,Guid clientId)
    {
        byte[] buffer = new byte[manager.Option.MaxPacketSize];
        try
        {
            while (true)
            {
                int bytesRead = client.Receive(buffer);
                if (bytesRead == 0) break; // Client disconnected
                var packet = manager.DeserializePacket(buffer[..bytesRead]);
                var packetEvent = new PacketEvent(packet.PacketType, packet.Packet, client)
                {
                    server = this
                };
                PacketReceived?.Invoke(packetEvent);
                foreach (var handler in _hanlders)
                {
                    if (handler.Value is not PacketReceiveHanlder packetHandler) continue;
                    if (packetHandler.PacketType != packetEvent.Type) continue;
                    packetHandler.Handler.DynamicInvoke(packetEvent, packetEvent.Packet);
                }
            }
        }
        catch (SocketException ex)
        {
            logger?.LogError("Socket exception: {Expection}", ex.Message);
        }
        finally
        {
            lock (_clients)
            {
                _clients.Remove(clientId);
            }
            client.Close();
            logger?.LogInformation("Client disconnected: {ClientId}", clientId);
            ClientDisconnected?.Invoke(client, clientId);
        }
    }
    /// <summary>
    /// Get the ID of the client.
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    public Guid GetID(Socket client)
    {
        return _clients.FirstOrDefault(x => x.Value.Socket == client).Key;
    }

    /// <summary>
    /// Get the client by ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Socket GetClient(Guid id)
    {
        return _clients[id].Socket;
    }
    public void Dispose()
    {
        _socket.Dispose();
        _sendThread?.Join();
        _listenThread?.Join();
        this.Stop();
    }
}
