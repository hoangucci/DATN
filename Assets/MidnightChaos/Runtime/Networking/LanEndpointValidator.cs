using System.Net;
using System.Net.Sockets;

namespace MidnightChaos.Networking
{
    public static class LanEndpointValidator
    {
        public const ushort DefaultPort = 7777;

        public static bool TryValidateIpv4(string rawAddress, out string address, out string error)
        {
            address = rawAddress?.Trim() ?? string.Empty;

            if (!IPAddress.TryParse(address, out IPAddress parsed) ||
                parsed.AddressFamily != AddressFamily.InterNetwork)
            {
                error = "IPv4 không hợp lệ. Ví dụ: 192.168.1.10";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryValidatePort(string rawPort, out ushort port, out string error)
        {
            if (!ushort.TryParse(rawPort?.Trim(), out port) || port == 0)
            {
                error = "Port phải nằm trong khoảng 1-65535.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
