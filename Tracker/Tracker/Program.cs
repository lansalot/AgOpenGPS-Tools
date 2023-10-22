using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Xml.Linq;

namespace Tracker
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
    internal class Program
    {
        private IPEndPoint epAgIO = new IPEndPoint(IPAddress.Any, 17777);
        private UdpClient udpAGIO;
        private UdpState AgIOState;

        private byte[] buffer = new byte[1024];

        private void ConnectSockets()
        {
            try //loopback
            {
                udpAGIO.BeginReceive(ReceiveDataAGIO, AgIOState);
            }
            catch (Exception ex)
            {
                //lblStatus.Text = "Error";
            }
        }
        static void Main(string[] args)
        {
            Program program = new Program();
            program.udpAGIO = new UdpClient(new IPEndPoint(IPAddress.Any, 15555));
            program.AgIOState = new UdpState(program.udpAGIO, program.epAgIO);
            program.udpAGIO.BeginReceive(program.ReceiveDataAGIO, program.AgIOState);
            Console.ReadLine();
        }

        private void ReceiveDataAGIO(IAsyncResult asyncResult)
        {
            try
            {
                // Receive all data
                UdpClient udpClient = ((UdpState)asyncResult.AsyncState).Client;
                IPEndPoint remoteEndPoint = ((UdpState)asyncResult.AsyncState).EndPoint;
                byte[] receivedData = udpAGIO.EndReceive(asyncResult, ref remoteEndPoint);
                int msgLen = receivedData.Length;
                byte[] localMsg = new byte[msgLen];
                Array.Copy(buffer, localMsg, msgLen);
                udpAGIO.BeginReceive(ReceiveDataAGIO, AgIOState);
                ReceiveFromLoopBack(receivedData);
            }
            catch (Exception)
            {
                // this isn't how we deal with errors.. but we're being stealthy here
            }
        }

        private void ReceiveFromLoopBack(byte[] data)
        {
            if (data[0] == 0x80 && data[1] == 0x81)
            {
                String v;
                if (data[3] == 0xD6)
                {
                    //the lat lon from AOG
                    v = "Lon: " + BitConverter.ToDouble(data, 5).ToString("N6") +
                        " Lat: " + BitConverter.ToDouble(data, 13).ToString("N6");
                    Console.WriteLine(v);
                }
            }
        }

    }

}
