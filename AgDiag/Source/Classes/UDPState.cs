using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AgDiag
{
    public class UdpState
    {
        public UdpClient Client { get; }
        public IPEndPoint EndPoint { get; }

        public UdpState(UdpClient client, IPEndPoint endPoint)
        {
            Client = client;
            EndPoint = endPoint;
        }
    }
}
