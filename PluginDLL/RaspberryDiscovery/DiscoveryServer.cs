using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using Rainmeter;
using Timer = System.Timers.Timer;

namespace RaspberryDiscovery
{
    internal class DiscoveryServer
    {
        private const string ResponseDataSuccess = "Accepted";
        private const string ResponseDataDuplicate = "AlreadyAccepted";
        private const string RquestSendNotifications = "CollectingBroadcasts";

        private readonly List<DiscoveredHost> _raspberries = new List<DiscoveredHost>();
        private readonly Regex _raspberryRegex = new Regex(@"hostname:([\d\w_-]*);?");
        private readonly Log _log;
        private readonly Ping _ping = new Ping();
        private readonly Timer _timer;

        private Thread _currentThread;
        private UdpClient _server;
        private int _port;

        public API Api { get; set; }
        public IList<DiscoveredHost> Raspberries => new List<DiscoveredHost>(_raspberries);
        public int ClientsCount => _raspberries.Count;

        public int LastClientsCountSpotted { get; set; } = -1;
        public string Separator { get; set; }
        public string EntryFormat { get; set; }
        public int EntriesMode { get; set; }


        public DiscoveryServer(Log log)
        {
            _log = log;

            _ping.PingCompleted += (sender, args) =>
            {
                if (args.Reply.Status == IPStatus.Success || !(args.UserState is DiscoveredHost raspberry)) return;

                Log(API.LogType.Debug, $"{args.UserState} went unresponsible! Removing from registry...");
                _raspberries.Remove(raspberry);
            };

            _timer = new Timer
            {
                Interval = 60 * 1000,
                AutoReset = true
            };

            _timer.Elapsed += SendOwnBroadcast;
        }

        private void SendOwnBroadcast(object o, ElapsedEventArgs elapsedEventArgs)
        {
            var client = new UdpClient();
            var ip = new IPEndPoint(IPAddress.Broadcast, _port);
            var bytes = Encoding.ASCII.GetBytes(RquestSendNotifications);
            client.EnableBroadcast = true;
            client.Send(bytes, bytes.Length, ip);
            client.Close();
        }

        public void UpdateRegistry()
        {
            LastClientsCountSpotted = ClientsCount;

            foreach (var raspberry in _raspberries)
            {
                try
                {
                    _ping.SendAsync(raspberry.Address, 5, raspberry);
                }
                catch (Exception e)
                {
                    Log(API.LogType.Warning, "Error while pinging {0}: {1}", raspberry.Address, e.Message);
                }
            }
        }

        public DiscoveredHost GetByIndex(int index)
        {
            return index > _raspberries.Count ? null : _raspberries[index];
        }

        private void Log(API.LogType type, string format, params object[] args)
        {
            _log?.Invoke(type, format, args);
        }

        private void Listen(object serverRaw)
        {
            if (!(serverRaw is UdpClient server))
            {
                return;
            }

            while (_server != null)
            {
                try
                {
                    var clientEp = new IPEndPoint(IPAddress.Any, _port);
                    var clientRequestData = server.Receive(ref clientEp);
                    var clientRequest = Encoding.ASCII.GetString(clientRequestData);
                    var match = _raspberryRegex.Match(clientRequest);

                    if (match.Success)
                    {
                        var r = new DiscoveredHost(match.Groups[1].Value, clientEp.Address);

                        if (_raspberries.Contains(r))
                        {
                            SendString(_server, ResponseDataDuplicate, clientEp);
                            continue;
                        }

                        _raspberries.Add(r);
                        SendString(_server, ResponseDataSuccess, clientEp);
                        Log(API.LogType.Debug, $"We got a new client! {_raspberries[_raspberries.Count - 1]}");
                    }
                    else
                    {
                        Log(API.LogType.Debug, $"Someone tried to connect, but the message {clientRequest} is not that we are listening for");
                    }
                }
                catch (Exception e)
                {
                    Log(API.LogType.Error, "Error while listening: {0}\n{1}", e.Message, e.StackTrace);
                }
            }
        }

        private static void SendString(UdpClient client, string data, IPEndPoint endPoint)
        {
            var dataBytes = Encoding.ASCII.GetBytes(data);
            client.Send(dataBytes, dataBytes.Length, endPoint);
        }

        public void Close()
        {
            try
            {
                _server?.Close();
                _server = null;
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                _currentThread?.Abort();
            }
            catch (Exception e)
            {
                Log(API.LogType.Error, "Error while interrupting thread: {0}\n{1}", e.Message, e.StackTrace);
            }

            _currentThread = null;
            _timer.Close();

            Log(API.LogType.Debug, "Stopped.");
        }

        public void ReInit(int port = 8888)
        {
            if (port == _port) return;

            Close();
            Init(port);
        }

        public void Init(int port = 8888)
        {
            _port = port;
            _server = new UdpClient(_port) { EnableBroadcast = true };
            _currentThread = new Thread(Listen);
            _currentThread.Start(_server);
            _timer.Start();

            Log(API.LogType.Debug, "Init call finished.");
        }

        public static explicit operator DiscoveryServer(IntPtr data)
        {
            return (DiscoveryServer)GCHandle.FromIntPtr(data).Target;
        }
    }
}
