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
using System.Security.Principal;

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
        private IPEndPoint epAgIO = new IPEndPoint(IPAddress.Any, 15555);
        private UdpClient udpAGIO;
        private UdpState AgIOState;
        private DateTime lastUpdate = DateTime.Now;
        TimeSpan timeSinceLastUpdate;
        private static bool interactive = true;
        private static string TrackerID = "";
        private static string trackerURL = "";
        private static int sendInterval = 30;
        private static string iniFile = "";

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
tracker /interactive  [id] [baseurl]
";

        private byte[] buffer = new byte[1024];

        static bool IsRunningWithElevatedPrivileges()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);

            // Check if the current user is in the Administrators group
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void ParseCMDLine(string[] args)
        {
            if (args.Length > 0)
            {
                switch (args[0].ToLower())
                {
                    case "/install":
                        if (args.Length == 5)
                        {
                            if (!IsRunningWithElevatedPrivileges())
                            {
                                Console.WriteLine("You need to run this installation elevated (as admin)");
                                Environment.Exit(0);
                            }
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
                                Environment.Exit(0);
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
                                    Environment.Exit(0);
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
                                Environment.Exit(0);
                            }
                            File.WriteAllText(iniFile, id + "\r\n" + trackerURL);
                            Console.WriteLine("Tracker installed");
                            Environment.Exit(0);
                        }
                        else
                        {
                            Console.WriteLine("Incorrect number of arguments - see tracker /help for details");
                            Environment.Exit(0);
                        }
                        break;
                    case "/uninstall":
                        if (args.Length != 2)
                        {
                            Console.WriteLine("Incorrect number of arguments - see /help for details");
                            Environment.Exit(0);
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
                        Environment.Exit(0);
                        break;
                    case "/interactive":
                        interactive = true;
                        break;
                    case "/?":
                    case "/help":
                        Console.WriteLine(helpString);
                        Environment.Exit(0);
                        break;
                }
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
        static void Main(string[] Args)
        {
            IntPtr handle = GetConsoleWindow();

            //bool interactive = false;
            ParseCMDLine(Args);

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
                    interactive = true;
                    Console.WriteLine("Error reading parameters from the ini file - what's up with that?");
                    Console.WriteLine("Format should be ID on first line, URL on second line.");
                    Environment.Exit(0);
                }
            }
            else
            {
                interactive = true;
                if (Args.Length == 3)
                    try
                    {
                        TrackerID = Args[1];
                        trackerURL = Args[2];
                    }
                    catch
                    {
                        Console.WriteLine("Error parsing arguments for interactive session");
                        Environment.Exit(1);
                    }
                else
                {
                    Console.WriteLine("You need to configure this first (or supply argument on command line). Do Tracker /help for info");
                    Environment.Exit(1);
                }
            }
            // check if debugger is attached
            if (!interactive || !System.Diagnostics.Debugger.IsAttached) ShowWindow(handle, SW_HIDE);
            Console.WriteLine("Waiting for data from AOG and time interval " + sendInterval + " to elapse");
            while (true)
            {
                Process[] pname = Process.GetProcessesByName("agopengps");
                
                if (pname.Length == 0)
                {
                    Console.WriteLine("Waiting for AOG to start");
                    System.Threading.Thread.Sleep(5000);
                }
                else
                {
                    break;
                }
            }
            Console.WriteLine("Found AgIO, waiting a few seconds for it to warm up");
            System.Threading.Thread.Sleep(10000);

            Program program = new Program();
            program.udpAGIO = new UdpClient();
            program.udpAGIO.ExclusiveAddressUse = false;
            program.udpAGIO.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            program.udpAGIO.Client.Bind(program.epAgIO);

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
                if (asyncResult.IsCompleted)
                {
                    byte[] receivedData = udpAGIO.EndReceive(asyncResult, ref remoteEndPoint);
                    int msgLen = receivedData.Length;
                    byte[] localMsg = new byte[msgLen];
                    Array.Copy(buffer, localMsg, msgLen);
                    udpAGIO.BeginReceive(ReceiveDataAGIO, AgIOState);
                    ReceiveFromLoopBack(receivedData);
                }
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
                        try
                        {
                            await CallRestApi(lat, lon);
                        }
                        catch ( Exception ex)
                        {
                            Console.WriteLine(ex.Message);
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
                //return; // just to test
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
                    // this is stealthy remember, keep shtum
                    Console.WriteLine($"Exception: {ex.Message}");
                }
            }
        }
    }

}

