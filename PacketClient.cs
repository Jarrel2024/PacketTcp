﻿using Microsoft.Extensions.Logging;
using PacketTcp.Events;
using PacketTcp.Handler;
using PacketTcp.Managers;
using PacketTcp.Packets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PacketTcp;
/// <summary>
/// PacketClient is a class that represents a client that can connect to a server and send and receive packets.
/// </summary>
/// <param name="manager"></param>
/// <param name="logger"></param>
public class PacketClient(PacketManager manager,ILogger? logger = null)
{
    private readonly Socket _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    private readonly Queue<PacketEvent> packetsNeedToSend = new();
    private readonly Dictionary<Guid,Action<PacketEvent>> _callbacks = new();
    private Thread? _receiveThread;
    private Thread? _sendThread;
    private Client? _client;
    public bool IsConnected => _socket.Connected;
    public event PacketHandler? PacketReceived;
    public event PacketHandler? PacketSent;
    public event PacketHandler? PacketSend;
    public event ClientHandler? ClientConnected;
    public event ClientHandler? ClientDisconnected;

    public Client Client { get=>_client??throw new InvalidOperationException("Client is not connected."); }
    /// <summary>
    /// Connect to server
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public Client Connect(string ip, int port)
    {
        if (IsConnected) throw new InvalidOperationException("Client is already connected.");
        _socket.Connect(ip, port);
        StartConnect();
        return Client;
    }

    /// <summary>
    /// Connect to server
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    /// <exception cref="InvalidOperationException"></exception>

    public async Task<Client> ConnectAsync(string ip, int port)
    {
        if (IsConnected) throw new InvalidOperationException("Client is already connected.");
        await _socket.ConnectAsync(ip, port);
        StartConnect();
        return Client;
    }

    private void StartConnect()
    {
        _receiveThread = new Thread(ReceiveThread);
        _sendThread = new Thread(SendThread);
        _receiveThread.Start();
        _sendThread.Start();
        if (manager.Option.SyncClientId)
        {
            var res = SendAsync<RequestClientIDS2CPacket>(new RequestClientIDC2SPacket()).Result;
            if (res == null) throw new Exception("Failed to get client id.");
            _client = new Client { Id = res.ClientId, Socket = _socket, PacketClient = this };
        }
        else
        {
            _client = new Client { Id = Guid.Empty, Socket = _socket, PacketClient = this };
        }
        logger?.LogInformation($"Connected to server {(_client.Id == Guid.Empty ? "unknown" : _client.Id.ToString())}.");
        ClientConnected?.Invoke(_client);
    }

    /// <summary>
    /// Join the client and wait for it to finish.
    /// </summary>

    public void Join()
    {
        _receiveThread?.Join();
        _sendThread?.Join();
        Stop();
    }

    /// <summary>
    /// Send a packet to the server.
    /// </summary>
    /// <param name="packet"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void Send(Packet packet)
    {
        if (!IsConnected) throw new InvalidOperationException("Client is not connected.");
        byte[] data = manager.SerializePacket(packet,true);
        PacketEvent @event = new PacketEvent(packet.GetType(), packet, _client!);
        PacketSend?.Invoke(@event);
        if (packetsNeedToSend.Count > manager.Option.MaxPacketCount) throw new InvalidOperationException("Packet queue is full.");
        lock (packetsNeedToSend)
        {
            packetsNeedToSend.Enqueue(@event);
        }
        logger?.BeginScope($"Sending packet {packet.GetType().Name} to server.");
    }

    /// <summary>
    /// Send a packet to the server and wait for a response.
    /// </summary>
    /// <param name="packet"></param>
    /// <returns></returns>
    public async Task<T?> SendAsync<T>(Packet packet,int timeout = -1) where T : Packet
    {
        Send(packet);
        Packet? callback = null;
        lock (_callbacks)
        {
            _callbacks[packet.PakcetId!.Value] = @event=>callback=@event.Packet;
        }
        logger?.LogInformation($"Waiting for packet {packet.GetType().Name} from server.");
        Task<Packet?> task = Task.Run(() =>
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (callback == null)
            {
                Thread.Sleep(manager.Option.PacketWaitTime);
                if (stopwatch.ElapsedMilliseconds > timeout && timeout > -1)
                {
                    logger?.BeginScope($"Timeout waiting for packet {packet.GetType().Name} from server.");
                    break;
                }
            }
            lock (_callbacks)
            {
                _callbacks.Remove(packet.PakcetId!.Value);
            }
            return callback;
        });
        logger?.LogInformation($"Received packet {callback?.GetType().Name} from server.");
        return (T?)await task;
    }

    private void ReceiveThread()
    {
        byte[] buffer = new byte[manager.Option.MaxPacketSize];
        BufferResolver resolver = new BufferResolver(ref buffer);
        try
        {
            while (IsConnected)
            {
                int bytesRead = _socket.Receive(buffer);
                if (bytesRead == 0) break;
                logger?.LogInformation($"Received {bytesRead} bytes from server.");

                Queue<byte[]> packets = resolver.Resolve(bytesRead);

                while (packets.Count > 0)
                {
                    var (packetType, packet) = manager.DeserializePacket(packets.Dequeue());
                    PacketEvent @event = new PacketEvent(packetType, packet, _client!);

                    if (_callbacks.TryGetValue(packet.PakcetId!.Value, out var action))
                    {
                        action.Invoke(@event);
                    }

                    PacketReceived?.Invoke(@event);
                }
            }
        }
        finally
        {
            lock (_callbacks)
            {
                _callbacks.Clear();
            }
            Stop();
        }
    }

    private void SendThread()
    {
        while (IsConnected)
        {
            PacketEvent @event;
            if (packetsNeedToSend.Count == 0)
            {
                Thread.Sleep(manager.Option.PacketWaitTime);
                continue;
            }
            lock (packetsNeedToSend)
            {
                @event = packetsNeedToSend.Dequeue();
            }
            byte[] data = manager.SerializePacket(@event.Packet,true);
            _socket.Send(data);
            PacketSent?.Invoke(@event);
        }
        Stop();
    }
    public void Stop()
    {
        if (!IsConnected || _receiveThread == null) return;
        logger?.LogInformation("Client disconnected.");
        ClientDisconnected?.Invoke(_client!);
        _receiveThread = null;
        _sendThread = null;
        _socket.Shutdown(SocketShutdown.Both);
        _socket.Close();
        _socket.Dispose();
    }
}
