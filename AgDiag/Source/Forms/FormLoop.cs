using System;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;

namespace AgDiag
{
    public partial class FormLoop : Form
    {
        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(IntPtr handle);

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        private static extern bool ShowWindow(IntPtr hWind, int nCmdShow);
        private IPEndPoint epAgIO = new IPEndPoint(IPAddress.Any, 17777);
        private IPEndPoint epAgOpenGPS = new IPEndPoint(IPAddress.Any, 15555);
        UdpClient udpAOG;
        UdpClient udpAGIO;

        UdpState AgIOState;
        UdpState AOGState;

        public FormLoop()
        {
            InitializeComponent();
            udpAOG = new UdpClient(new IPEndPoint(IPAddress.Any, 15555));
            udpAGIO = new UdpClient(new IPEndPoint(IPAddress.Any, 17777));
            AgIOState = new UdpState(udpAGIO, epAgIO);
            AOGState = new UdpState(udpAOG, epAgOpenGPS);
        }

        public double secondsSinceStart, lastSecond, currentLat, currentLon;

        private static string ByteArrayToHex(byte[] barray)
        {
            StringBuilder sb = new StringBuilder(barray.Length * 3);
            foreach (byte by in barray)
            {
                sb.AppendFormat("{0:x2}-", by);
            }
            return sb.ToString();
        }

        private void btnDeviceManager_Click(object sender, EventArgs e)
        {
            Process.Start("devmgmt.msc");
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            secondsSinceStart = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;

            DoTraffic();

            // asData (0xfe / 254)
            if ((asData.pgn[asData.sc1to8] & 1) == 1) lblSection1.BackColor = Color.Green;
            else lblSection1.BackColor = Color.Red;
            if ((asData.pgn[asData.sc1to8] & 2) == 2) lblSection2.BackColor = Color.Green;
            else lblSection2.BackColor = Color.Red;
            if ((asData.pgn[asData.sc1to8] & 4) == 4) lblSection3.BackColor = Color.Green;
            else lblSection3.BackColor = Color.Red;
            if ((asData.pgn[asData.sc1to8] & 8) == 8) lblSection4.BackColor = Color.Green;
            else lblSection4.BackColor = Color.Red;

            if ((asData.pgn[asData.sc1to8] & 16) == 16) lblSection5.BackColor = Color.Green;
            else lblSection5.BackColor = Color.Red;
            if ((asData.pgn[asData.sc1to8] & 32) == 32) lblSection6.BackColor = Color.Green;
            else lblSection6.BackColor = Color.Red;
            if ((asData.pgn[asData.sc1to8] & 64) == 64) lblSection7.BackColor = Color.Green;
            else lblSection7.BackColor = Color.Red;
            if ((asData.pgn[asData.sc1to8] & 128) == 128) lblSection8.BackColor = Color.Green;
            else lblSection8.BackColor = Color.Red;

            lblSpeed.Text = (asData.pgn[asData.speedHi] << 8 | asData.pgn[asData.speedLo]).ToString();
            lblSetSteerAngle.Text = ((asData.pgn[asData.steerAngleHi] << 8 | asData.pgn[asData.steerAngleLo]) * 0.01).ToString();
            lblStatus.Text = asData.pgn[asData.status].ToString();

            lblSteerDataPGN.Text = ByteArrayToHex(asData.pgn);

            // asModule (0xfd / 253)
            lblSteerAngleActual.Text = (((Int16)((asModule.pgn[asModule.actualHi] << 8)
                + asModule.pgn[asModule.actualLo])) * 0.01).ToString();

            Int16 tmp = ((Int16)((asModule.pgn[asModule.headHi] << 8)
                + asModule.pgn[asModule.headLo]));
            if (tmp != 9999)
            {
                lblHeading.Text = (tmp * 0.1).ToString();
            }
            else
            {
                lblHeading.Text = "N/A";
            }

            tmp = ((Int16)((asModule.pgn[asModule.rollHi] << 8)
                + asModule.pgn[asModule.rollLo]));
            if (tmp != 8888)
            {
                lblRoll.Text = (tmp * 0.1).ToString();
            }
            else
            {
                lblRoll.Text = "N/A";
            }

            lblPWM.Text = (asModule.pgn[asModule.pwm]).ToString();

            if ((asModule.pgn[asModule.switchStatus] & 1) == 1)
                lblWorkSwitch.BackColor = Color.Red;
            else lblWorkSwitch.BackColor = Color.Green;

            if ((asModule.pgn[asModule.switchStatus] & 2) == 2)
                lblSteerSwitch.BackColor = Color.Red;
            else lblSteerSwitch.BackColor = Color.Green;

            lblPGNFromAutosteerModule.Text = ByteArrayToHex(asModule.pgn);

            //asSet (0xfc / 252)
            lblPGNSteerSettings.Text = ByteArrayToHex(asSet.pgn);
            lblP.Text = asSet.pgn[asSet.gainProportional].ToString();
            lblHiPWM.Text = asSet.pgn[asSet.highPWM].ToString();
            lblLoPWM.Text = asSet.pgn[asSet.lowPWM].ToString();
            lblMinPWM.Text = asSet.pgn[asSet.minPWM].ToString();
            lblCPD.Text = asSet.pgn[asSet.countsPerDegree].ToString();
            lblAckerman.Text = asSet.pgn[asSet.ackerman].ToString();
            lblOffset.Text = (asSet.pgn[asSet.wasOffsetHi] << 8 | asSet.pgn[asSet.wasOffsetLo]).ToString();


            //asSet (0xfb / 251)
            lblPGNAutoSteerConfig.Text = ByteArrayToHex(asConfig.pgn);
            lblSet0.Text = asConfig.pgn[asConfig.set0].ToString();
            lblPulseCount.Text = asConfig.pgn[asConfig.maxPulse].ToString();
            lblMinSpeed.Text = asConfig.pgn[asConfig.minSpeed].ToString();
        }

        private void FormLoop_Load(object sender, EventArgs e)
        {

            timer1.Enabled = true;
            LoadLoopback();
        }

        private void FormLoop_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (loopBackSocket != null)
            {
                try
                {
                    loopBackSocket.Shutdown(SocketShutdown.Both);
                }
                finally { loopBackSocket.Close(); }
            }
        }

        private void DoTraffic()
        {
        }



    }
}

