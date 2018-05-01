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
using Timer = System.Timers.Timer;
using Rainmeter;

namespace RaspberryDiscovery
{
    internal class Raspberry
    {
        public string Name { get;}
        public IPAddress Address { get; }

        public Raspberry(string name, IPAddress address)
        {
            Name = name;
            Address = address;
        }

        public override string ToString()
        {
            return $"Raspberry [{Name}: {Address}]";
        }

        protected bool Equals(Raspberry other)
        {
            return Equals(Address, other.Address);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((Raspberry)obj);
        }

        public override int GetHashCode()
        {
            return (Address != null ? Address.GetHashCode() : 0);
        }

        public static bool operator ==(Raspberry r1, Raspberry r2)
        {
            if (r1 == null && r2 == null)
            {
                return true;
            }

            if (r1 == null || r2 == null)
            {
                return false;
            }

            return Equals(r1.Address, r2.Address);
        }

        public static bool operator !=(Raspberry r1, Raspberry r2)
        {
            return !(r1 == r2);
        }
    }

    internal delegate void Log(API.LogType type, string format, params object[] args);

    internal class DiscoveryServer
    {
        private const string ResponseDataSuccess = "Accepted";
        private const string ResponseDataDuplicate = "AlreadyAccepted";
        private const string RquestSendNotifications = "CollectingBroadcasts";

        private readonly List<Raspberry> _raspberries = new List<Raspberry>();
        private readonly Regex _raspberryRegex = new Regex(@"hostname:([\d\w_-]*);?");
        private readonly Log _log;
        private readonly Ping _ping = new Ping();
        private readonly Timer _timer;

        private Thread _currentThread;
        private UdpClient _server;
        private int _port;

        public API Api { get; set; }
        public IList<Raspberry> Raspberries => new List<Raspberry>(_raspberries);
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
                if (args.Reply.Status == IPStatus.Success || !(args.UserState is Raspberry raspberry)) return;

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

        public Raspberry GetByIndex(int index)
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
                        var r = new Raspberry (match.Groups[1].Value, clientEp.Address);

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
            return (DiscoveryServer) GCHandle.FromIntPtr(data).Target;
        }
    }

    public class Plugin
    {
        private const string Separator = "\n";
        private const string EntryFormat = "{0}: {1}";
        private const int EntryMode = 0;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            API api = rm;
            var server = new DiscoveryServer (api.LogF) { Api = api };
            var port = api.ReadInt("ListeningPort", 8888);

            server.Init(port);
            ReadProperties(server, api);

            data = GCHandle.ToIntPtr(GCHandle.Alloc(server));
        }

        private static void ReadProperties(DiscoveryServer server, API rm)
        {
            server.Separator = rm.ReadString("EntriesSeparator", Separator);
            server.EntryFormat = rm.ReadString("EntryFormat", EntryFormat).Replace("\n", "");
            server.EntriesMode = rm.ReadInt("EntriesMode", EntryMode);
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            var server = (DiscoveryServer) data;
            API api = rm;
            var port = api.ReadInt("ListeningPort", 8888);

            server.ReInit(port);
            ReadProperties(server, api);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            var server = (DiscoveryServer) data;
            server.UpdateRegistry();

            if (server.EntriesMode == 0)
            {
                var i = 0;

                foreach (var raspberry in server.Raspberries)
                {
                    server.Api.Execute($"!SetVariable RPI{i++} \"{string.Format(server.EntryFormat, raspberry.Name, raspberry.Address)}\"");
                }
            }
            else
            {
                var reps = new List<string>();

                foreach (var raspberry in server.Raspberries)
                {
                    reps.Add(string.Format(server.EntryFormat, raspberry.Name, raspberry.Address));
                }

                var str = string.Join(server.Separator, reps.ToArray());

                server.Api.Execute($"!SetVariable RPIS \"{str}\"");
            }
            
            return server.ClientsCount;
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            var server = (DiscoveryServer) data;
            server.Close();

            GCHandle.FromIntPtr(data).Free();
        }
    }
}
