using Microsoft.Extensions.Logging;
using PacketTcp.Events;
using PacketTcp.Handler;
using PacketTcp.Managers;
using PacketTcp.Packets;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace PacketTcp;


public class PacketServer(int port,PacketManager manager,ILogger? logger=null)
{
    private readonly Socket _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
    private readonly HashSet<Task> _clientTasks = [];
    private readonly Dictionary<Guid,Client> _clients = [];
    private readonly Dictionary<Socket, Client> _clientSockets = [];
    private readonly Dictionary<Guid, IHandler> _hanlders = [];
    private Thread? _listenThread;
    private Thread? _sendThread;
    private bool _isListening = false;

    private readonly Queue<PacketEvent> _packetsNeedToSend = new();

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
            packet.ClientId = e.Client.Id;
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
        Task.WaitAll(_clientTasks);
    }

    /// <summary>
    /// Send a packet to specific socket.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="packet"></param>
    public void Send(Client client,Packet packet)
    {
        if (!IsListening) return;
        PacketEvent @event = new PacketEvent(packet.GetType(), packet, client);
        PacketSend?.Invoke(@event);
        if (_packetsNeedToSend.Count > manager.Option.MaxPacketCountPerClient * _clients.Count) throw new InvalidOperationException("Packet queue is full.");
        lock (_packetsNeedToSend)
        {
            _packetsNeedToSend.Enqueue(@event);
        }
    }

    /// <summary>
    /// Send a packet to a specific socket.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="packet"></param>
    public void Send(Guid client, Packet packet)
    {
        this.Send(_clients[client], packet);
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
    public void Stop()
    {
        if (!IsListening || _listenThread == null) return;
        _isListening = false;
        _listenThread = null;
        _sendThread = null;

        lock (_clientSockets) _clientSockets.Clear();
        lock (_clients) _clients.Clear();
        lock (_clientTasks) _clientTasks.Clear();
        lock (_packetsNeedToSend) _packetsNeedToSend.Clear();

        _socket.Close();
        _socket.Dispose();
    }

    private void ListenThread()
    {
        while (IsListening)
        {
            try
            {
                var socket = _socket.Accept();
                Guid clientId = Guid.NewGuid();
                Client client = new Client { Id = clientId, Socket = socket, PacketServer = this };
                lock (_clients)
                {
                    _clients.Add(clientId,client);
                }
                lock (_clientSockets)
                {
                    _clientSockets.Add(socket, client);
                }
                logger?.LogInformation("Client connected: {ClientId}", clientId);
                _clientTasks.Add(Task.Run(() => HandleClient(socket,clientId)));
                ClientConnected?.Invoke(client);
            }
            catch (SocketException ex)
            {
                logger?.LogError("Socket exception: {Expection}", ex.Message);
            }
            catch (ObjectDisposedException ex)
            {
                logger?.LogError("Socket disposed: {Expection}",ex.Message);
                break;
            }
        }
    }
    private void SendThread()
    {
        try
        {
            while (IsListening)
            {
                lock (_packetsNeedToSend)
                {
                    while (_packetsNeedToSend.Count > 0)
                    {
                        var packetEvent = _packetsNeedToSend.Dequeue();
                        if (packetEvent.IsCancelled) return;
                        var client = packetEvent.Client;
                        byte[] data = manager.SerializePacket(packetEvent.Packet, true);
                        client.Socket.Send(data);
                        PacketSent?.Invoke(packetEvent);
                    }
                }
                Thread.Sleep(manager.Option.PacketWaitTime); // Prevent busy waiting
            }
        }
        catch (ObjectDisposedException)
        {

        }
        finally
        {

        }
    }
    private void HandleClient(Socket socket,Guid clientId)
    {
        byte[] buffer = new byte[manager.Option.MaxPacketSize];
        BufferResolver resolver = new BufferResolver(ref buffer);
        try
        {
            while (IsListening)
            {
                int bytesRead = socket.Receive(buffer);
                if (bytesRead == 0) break; // Client disconnected

                Queue<byte[]> packets = resolver.Resolve(bytesRead);

                while(packets.Count>0)
                {
                    var packet = manager.DeserializePacket(packets.Dequeue());
                    var packetEvent = new PacketEvent(packet.PacketType, packet.Packet, _clientSockets[socket])
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
        }
        catch (SocketException ex)
        {
            logger?.LogError("Socket exception: {Expection}", ex.Message);
        }
        catch (Exception other)
        {
            logger?.LogError("Exception: {Expection}", other.Message);
        }
        finally
        {
            ClientDisconnected?.Invoke(_clients[clientId]);
            lock (_clients)
            {
                _clients.Remove(clientId);
            }
            lock (_clientSockets)
            {
                _clientSockets.Remove(socket);
            }
            socket.Close();
            logger?.LogInformation("Client disconnected: {ClientId}", clientId);
        }
    }
    /// <summary>
    /// Get the ID of the socket.
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    public Guid GetID(Socket client)
    {
        return _clients.FirstOrDefault(x => x.Value.Socket == client).Key;
    }

    /// <summary>
    /// Get the socket by ID.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public Client GetClient(Guid id)
    {
        return _clients[id];
    }
}
