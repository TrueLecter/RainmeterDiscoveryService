﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Rainmeter;

namespace RaspberryDiscovery
{
    internal delegate void Log(API.LogType type, string format, params object[] args);

    public class Plugin
    {
        private const string Separator = "\n";
        private const string EntryFormat = "{0}: {1}";
        private const int EntryMode = 1;

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
            server.NoDevicesDetected = rm.ReadString("NoDevicesDetected", "No devices detected");
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
                for (var j = server.ClientsCount; j < server.LastClientsCountSpotted; j++)
                {
                    server.Api.Execute($"!SetVariable RPI{j} \"\"");
                }

                var i = 0;

                foreach (var raspberry in server.Raspberries)
                {
                    server.Api.Execute($"!SetVariable RPI{i++} \"{string.Format(server.EntryFormat, raspberry.Name, raspberry.Address)}\"");
                }
            }
            else
            {
                var str = server.ClientsCount == 0 ? 
                    server.NoDevicesDetected : 
                    string.Join(server.Separator, 
                        server.Raspberries.Select(
                            raspberry => string.Format(server.EntryFormat, raspberry.Name, raspberry.Address)
                        ).ToArray()
                    );

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
