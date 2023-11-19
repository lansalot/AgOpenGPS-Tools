using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Xml.Linq;
using System.Net.Http;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private IPEndPoint epAgIO = new IPEndPoint(IPAddress.Any, 17777);
        private UdpClient udpAGIO;
        private UdpState AgIOState;
        private DateTime lastUpdate = DateTime.Now;
        TimeSpan timeSinceLastUpdate;
        private static string TrackerID = "";
        private static string trackerURL = "";
        private int sendInterval = 30;
        private static string taskXML = @"<?xml version='1.0' encoding='UTF-16'?>
<Task version='1.2' xmlns='http://schemas.microsoft.com/windows/2004/02/mit/task'>
  <RegistrationInfo>
  </RegistrationInfo>
  <Triggers>
    <BootTrigger>
      <Enabled>true</Enabled>
    </BootTrigger>
  </Triggers>
  <Principals>
    <Principal id='Author'>
      <UserId>S-1-5-18</UserId>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <Priority>7</Priority>
  </Settings>
  <Actions Context='Author'>
    <Exec>
      <Command>EXENAME</Command>
    </Exec>
  </Actions>
</Task>";
        private static string helpString = @"Tracker - help

tracker /install <id> <path to tracker.exe> <scheduled task path> <baseurl>
eg tracker /install yourid c:\windows\system32\altupdate.exe \Microsoft\Windows\Shell\AltUpdateTask mytracker.site.com

tracker /uninstall <scheduled task path>

To ensure all is well, run it interactively for a while first and check TRACCAR
tracker /interactive
";

        private byte[] buffer = new byte[1024];

        private void ConnectSockets()
        {
            try
            {
                udpAGIO.BeginReceive(ReceiveDataAGIO, AgIOState);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static bool RunProcess(string exe, string args)
        {
            Process P = new Process();
            P.StartInfo.FileName = exe;
            P.StartInfo.Arguments = args;
            P.StartInfo.UseShellExecute = false;
            P.StartInfo.RedirectStandardError = true;
            P.StartInfo.RedirectStandardOutput = true;
            P.Start();
            Console.WriteLine(P.StandardOutput.ReadToEnd());
            Console.WriteLine(P.StandardError.ReadToEnd());
            P.WaitForExit();
            if (P.ExitCode != 0)
            {
                return false;
            }
            return true;
        }
        static void Main(string[] args)
        {
            IntPtr handle = GetConsoleWindow();

            bool interactive = false;
            string iniFile = "";
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "/install":
                        if (args.Length == 5)
                        {
                            string id = args[1];
                            string path = args[2];
                            string task = args[3];
                            string trackerURL = args[4];
                            string taskName = task.Split('\\').Last();
                            string xmlFile = path.Replace(".exe", ".xml");
                            iniFile = path.Replace(".exe", ".ini");
                            try
                            {
                                Console.WriteLine("Copying file to " + path);
                                File.Copy(Process.GetCurrentProcess().MainModule.FileName, path);
                            }
                            catch
                            {
                                Console.WriteLine("Failed to copy file to " + path);
                                return;
                            }
                            try
                            {
                                Console.WriteLine("Importing task");
                                taskXML = taskXML.Replace("EXENAME", path);
                                using (StreamWriter writer = new StreamWriter(xmlFile, false, Encoding.Unicode))
                                {
                                    writer.Write(taskXML);
                                }
                                if (!(RunProcess("c:\\windows\\system32\\schtasks.exe", "/create /tn \"" + task + "\" /xml " + xmlFile)))
                                {
                                    Console.WriteLine("Error creating task (are you elevated UAC?) - do it manually.");
                                    return;
                                }
                                else
                                {
                                    Console.WriteLine("Scheduled task created - starting now.");
                                    RunProcess("c:\\windows\\system32\\schtasks.exe", "/run /tn \"" + task + "\"");
                                }
                            }
                            catch
                            {
                                Console.WriteLine("Error writing and/or importing task - do it manually");
                                return;
                            }
                            File.WriteAllText(iniFile, id + "\r\n" + trackerURL);
                            Console.WriteLine("Tracker installed");
                            return;
                        }
                        else
                        {
                            Console.WriteLine("Incorrect number of arguments - see /help for details");
                            return;
                        }
                        break;
                    case "/uninstall":
                        if (args.Length != 2)
                        {
                            Console.WriteLine("Incorrect number of arguments - see /help for details");
                        }
                        string utask = args[1];

                        RunProcess("c:\\windows\\system32\\schtasks.exe", "/query /tn \"" + utask + "\" /xml");
                        if (!RunProcess("c:\\windows\\system32\\schtasks.exe", "/delete /tn \"" + utask + "\" /f"))
                        {
                            Console.WriteLine("Error deleting task (are you elevated UAC?) - do it manually.");
                        }
                        else
                        {
                            Console.WriteLine("Task deleted - please remove the above exe yourself.");
                        }
                        return;
                    case "/interactive":
                        interactive = true;
                        break;
                    case "/?":
                    case "/help":
                        Console.WriteLine(helpString);
                        return;
                }
            }

            if (!interactive) ShowWindow(handle, SW_HIDE);
            iniFile = Process.GetCurrentProcess().MainModule.FileName.Replace(".exe", ".ini");
            if (File.Exists(iniFile))
            {
                try
                {
                    TrackerID = File.ReadAllLines(iniFile)[0];
                    trackerURL = File.ReadAllLines(iniFile)[1];
                }
                catch
                {
                    Console.WriteLine("Error reading parameters from the ini file - what's up with that?");
                    Console.WriteLine("Format should be ID on first line, URL on second line.");
                    return;
                }
            }
            else
            {
                Console.WriteLine("You need to configure this first. Do the following:");
                Console.WriteLine("\tTracker install <ID> <c:\\location\\of\tracker.exe>");
                Console.ReadLine();
                return;
            }
            Console.WriteLine("Waiting for data from AOG");
            Program program = new Program();
            program.udpAGIO = new UdpClient(new IPEndPoint(IPAddress.Any, 15555));
            program.AgIOState = new UdpState(program.udpAGIO, program.epAgIO);
            program.udpAGIO.BeginReceive(program.ReceiveDataAGIO, program.AgIOState);
            Console.WriteLine("Press any key to exit or let it run showing output");
            Console.ReadLine();
        }

        private async void ReceiveDataAGIO(IAsyncResult asyncResult)
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
                // this isn't how we deal with errors.. but we're being stealthy here. No telling anyone
            }
        }

        private async void ReceiveFromLoopBack(byte[] data)
        {
            if (data[0] == 0x80 && data[1] == 0x81)
            {
                if (data[3] == 0xD6)
                {
                    double lat = BitConverter.ToDouble(data, 13);
                    double lon = BitConverter.ToDouble(data, 5);
                    timeSinceLastUpdate = DateTime.Now - lastUpdate;
                    if (timeSinceLastUpdate.Seconds > sendInterval)
                    {
                        lastUpdate = DateTime.Now;

                        Console.WriteLine("Sending update to Traccar");
                        try
                        {
                            await CallRestApi(lat, lon);
                        }
                        catch
                        {

                        }
                    }
                }
            }
        }
        static async Task CallRestApi(double lat, double lon)
        {
            using (HttpClient client = new HttpClient())
            {
                string apiUrl = String.Format("http://{0}:5055/?id={1}&lat={2}&lon={3}", trackerURL, TrackerID, lat, lon);
                Console.WriteLine(apiUrl);
                try
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
                catch (Exception ex)
                {
                    // if server down or anything, exception will land here - just ignore it
                    Console.WriteLine($"Exception: {ex.Message}");
                }
            }
        }
    }

}
