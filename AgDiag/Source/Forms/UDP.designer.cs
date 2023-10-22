using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace AgDiag
{
    public class CTraffic
    {
        public int cntr;
        public int cntrPGNFromAOG = 0;
        public int cntrPGNToAOG = 0;

        public int cntrUDPOut = 0;
        public int cntrUDPIn = 0;

        public bool isTrafficOn = true;

        public int enableCounter = 0;
        public int AOGPackets = 0, AGIOPackets = 0;
    }



    public partial class FormLoop
    {

        // Server socket
        private Socket loopBackSocket;

        private CTraffic traffic = new CTraffic();

        // Data stream
        private byte[] buffer = new byte[1024];


        private void LoadLoopback()
        {
            try //loopback
            {
                udpAGIO.BeginReceive(ReceiveDataAGIO, AgIOState);
                udpAOG.BeginReceive(ReceiveDataAOG, AOGState);
            }
            catch (Exception ex)
            {
                //lblStatus.Text = "Error";
                MessageBox.Show("Load Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReceiveDataAGIO(IAsyncResult asyncResult)
        {
            try
            {
                // Receive all data
                traffic.AGIOPackets++;
                UdpClient udpClient = ((UdpState)asyncResult.AsyncState).Client;
                IPEndPoint remoteEndPoint = ((UdpState)asyncResult.AsyncState).EndPoint;
                byte[] receivedData = udpAGIO.EndReceive(asyncResult, ref remoteEndPoint);
                int msgLen = receivedData.Length;
                byte[] localMsg = new byte[msgLen];
                Array.Copy(buffer, localMsg, msgLen);
                udpAGIO.BeginReceive(ReceiveDataAGIO, AgIOState);
                BeginInvoke((MethodInvoker)(() => ReceiveFromLoopBack(receivedData)));
            }
            catch (Exception)
            {
                //MessageBox.Show("ReceiveData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ReceiveDataAOG(IAsyncResult asyncResult)
        {
            try
            {
                // Receive all data
                traffic.AOGPackets++;
                UdpClient udpClient = ((UdpState)asyncResult.AsyncState).Client;
                IPEndPoint remoteEndPoint = ((UdpState)asyncResult.AsyncState).EndPoint;
                byte[] receivedData = udpAOG.EndReceive(asyncResult, ref remoteEndPoint);
                int msgLen = receivedData.Length;
                byte[] localMsg = new byte[msgLen];
                Array.Copy(buffer, localMsg, msgLen);
                udpAOG.BeginReceive(ReceiveDataAOG, AOGState);
                BeginInvoke((MethodInvoker)(() => ReceiveFromLoopBack(receivedData)));
            }
            catch (Exception)
            {
                //MessageBox.Show("ReceiveData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private String ReturnScaledString(object value, Double scale)
        {
            switch (value)
            {
                case ushort ushortVal:
                    {
                        if (ushortVal != ushort.MaxValue)
                            return (ushortVal * scale).ToString("N2");
                        else
                            return "N/A";
                        break;
                    }
                case short shortVal:
                    {
                        if (shortVal != short.MaxValue)
                            return shortVal.ToString("N2");
                        else
                            return "N/A";
                    }
                case Single singleVal:
                    {
                        if (singleVal != Single.MaxValue)
                            return (singleVal * scale).ToString("N2");
                        else
                            return "N/A";
                    }
                default:
                    {
                        return "NOTSUPPORTED";
                    }
            }
        }
        private void ReceiveFromLoopBack(byte[] data)
        {
            traffic.cntrPGNFromAOG += data.Length;
            if (data[0] == 0x80 && data[1] == 0x81)
            {
                switch (data[3])
                {
                    //the lat lon from AOG
                    case 0xD6: // 214
                        {
                            lblGPS.Text = "Lon: " + BitConverter.ToDouble(data, 5).ToString("N6") +
                                " Lat: " + BitConverter.ToDouble(data, 13).ToString("N6") +
                                " Alt: " + ReturnScaledString(BitConverter.ToSingle(data, 37), 1) + "m" +
                                "\r\nSpeed: " + BitConverter.ToSingle(data, 29).ToString("N2") + "m/s" +
                                "\r\nFix: " + data[43].ToString() +
                                " Sats: " + BitConverter.ToUInt16(data, 41) +
                                " HDOP: " + ReturnScaledString(BitConverter.ToUInt16(data, 44), 0.01) +
                                " Age: " + ReturnScaledString(BitConverter.ToUInt16(data, 46), 0.01) +
                                "\r\nimuHead: " + ReturnScaledString(BitConverter.ToUInt16(data, 48), 0.1) +
                                " imuRoll: " + ReturnScaledString(BitConverter.ToInt16(data, 50), 0.1) +
                                "\r\nimuPitch: " + ReturnScaledString(BitConverter.ToInt16(data, 52), 1);
                                //" imuYaw: " + ReturnScaledString(BitConverter.ToInt16(data, 54), 0.1);


                            break;
                        }
                    case 0xD0: // 208
                        {
                            int encAngle = BitConverter.ToInt32(data, 5);
                            currentLat = (encAngle / (0x7FFFFFFF / 90.0));

                            encAngle = BitConverter.ToInt32(data, 9);
                            currentLon = (encAngle / (0x7FFFFFFF / 180.0));

                            break;
                        }
                    case 0xfd: // 253
                        {
                            for (int i = 5; i < data.Length; i++)
                            {
                                asModule.pgn[i] = data[i];
                            }

                            break;
                        }
                    case 0xfe: // 254
                        {

                            for (int i = 5; i < data.Length; i++)
                            {
                                asData.pgn[i] = data[i];
                            }

                            break;
                        }
                    case 0xfc: // 252
                        {

                            for (int i = 5; i < data.Length; i++)
                            {
                                asSet.pgn[i] = data[i];
                            }

                            break;
                        }
                    case 0xfb: // 251
                        {

                            for (int i = 5; i < data.Length; i++)
                            {
                                asConfig.pgn[i] = data[i];
                            }

                            break;
                        }

                    default:
                        {
                            break;
                        }
                }
            }
            else
            {
                traffic.cntr += data.Length;
            }
            lblAOG.Text = traffic.AOGPackets.ToString();
            lblAGIO.Text = traffic.AGIOPackets.ToString();  
        }

    }

}
