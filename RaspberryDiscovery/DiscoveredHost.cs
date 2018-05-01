using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace RaspberryDiscovery
{
    internal class DiscoveredHost
    {
        public string Name { get; }
        public IPAddress Address { get; }

        public DiscoveredHost(string name, IPAddress address)
        {
            Name = name;
            Address = address;
        }

        public override string ToString()
        {
            return $"DiscoveredHost [{Name}: {Address}]";
        }

        protected bool Equals(DiscoveredHost other)
        {
            return Equals(Address, other.Address);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((DiscoveredHost)obj);
        }

        public override int GetHashCode()
        {
            return (Address != null ? Address.GetHashCode() : 0);
        }

        public static bool operator ==(DiscoveredHost r1, DiscoveredHost r2)
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

        public static bool operator !=(DiscoveredHost r1, DiscoveredHost r2)
        {
            return !(r1 == r2);
        }
    }
}
