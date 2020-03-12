using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;

namespace TauMulticastReferenceCSharp
{

    class TauMulticast
    {
        public int ConnectionStatus { get; private set; } = 0; //0 - not connected, 1 - connected, 2 - disconnect pending

        public static IPAddress MulticastAnnouncerGroupAddress = IPAddress.Parse("239.255.255.151");
        public static int MulticastAnnouncerPort = 16061;

        private MemoryStream AnnouncerMemoryStream,
            DataMemoryStream,
            MappingMemoryStream,
            DebugMemoryStream;

        private BinaryReader DataMemoryStreamReader, DebugMemoryStreamReader;

        private TauObjects.AnnouncerDataSerializer AnnouncerDataSerializer = new TauObjects.AnnouncerDataSerializer();
        private TauObjects.AnnouncerDataObj AnnouncerData = new TauObjects.AnnouncerDataObj();

        private TauObjects.DataPacket datapacket = new TauObjects.DataPacket();
        private TauObjects.DataPacket datapacket_temp = new TauObjects.DataPacket();
        private TauObjects.DebugPacket debugpacket = new TauObjects.DebugPacket();
        private TauObjects.DebugPacket debugpacket_temp = new TauObjects.DebugPacket();
        private TauObjects.MappingPacket mappingpacket = new TauObjects.MappingPacket();

        private UdpClient MulticastAnnouncerClient,
            MulticastDataClient,
            MulticastMappingClient,
            MulticastLogsClient,
            MulticastDebugClient;

        private Thread MulticastAnnouncerThread,  // thread collection for reading from udpclients
            MulticastDataThread,
            MulticastMappingThread,
            MulticastLogsThread,
            MulticastDebugThread;

        public int AnnouncerThreadSleep = 100;
        public int DataThreadSleep = 5;
        public int MappingThreadSleep = 100;
        public int LogsThreadSleep = 1000;
        public int DebugThreadSleep = 5;

        private bool EnableLogsThread = false;
        private bool EnableDebugThread = false;

        private List<string> Logs = new List<string>();
        private int LogsMaxLen = 100;

        private List<System.Net.IPAddress> NICaddresses;  //part of windows multicast workaround - list of network interface addresses

        public TauMulticast(bool logs = false, bool debug = false) {
            InitializeNICaddresses();
            EnableLogsThread = logs;
            EnableDebugThread = debug;
        }

        public int Connect()
        {
            if (ConnectionStatus == 0)
            {
                ConnectionStatus = 1;
                MulticastAnnouncerThread = new Thread(() => MulticastAnnouncerTask()) { IsBackground = true };
                MulticastAnnouncerThread.Start();
                MulticastDataThread = new Thread(() => MulticastDataTask()) { IsBackground = true };
                MulticastDataThread.Start();
                MulticastMappingThread = new Thread(() => MulticastMappingTask()) { IsBackground = true };
                MulticastMappingThread.Start();

                if (EnableLogsThread)
                {
                    MulticastLogsThread = new Thread(() => MulticastLogsTask()) { IsBackground = true };
                    MulticastLogsThread.Start();
                }

                if (EnableDebugThread)
                {
                    MulticastDebugThread = new Thread(() => MulticastDebugTask()) { IsBackground = true };
                    MulticastDebugThread.Start();
                }

                return 0; //success
            }
            else if (ConnectionStatus == 1)
            {
                return 1; //can't connect, already connected state
            }
            else if (ConnectionStatus == 2)
            {
                return 2; //can't connect, disconnect pending
            }
            else
            {
                throw new Exception("TauMulticast.Connect: ConnectionStatus unexpected value");
            }
        }

        public int Disconnect()
        {
            if (ConnectionStatus == 1) {
                ConnectionStatus = 2;
                while (MulticastAnnouncerThread.IsAlive && MulticastDataThread.IsAlive && MulticastMappingThread.IsAlive) {
                    Thread.Sleep(5);
                }

                if (EnableLogsThread)
                {
                    while (MulticastLogsThread.IsAlive)
                    {
                        Thread.Sleep(5);
                    }
                }

                if (EnableDebugThread)
                {
                    while (MulticastDebugThread.IsAlive)
                    {
                        Thread.Sleep(5);
                    }
                }
                ConnectionStatus = 0;
                return 0;
            }
            else if (ConnectionStatus == 0)
            {
                return 1; //can't disconnect, not connected state
            }
            else if (ConnectionStatus == 2)
            {
                return 2; //can't disconnect, disconnect pending
            }
            else
            {
                throw new Exception("TauMulticast.Disconnect: ConnectionStatus unexpected value");
            }
        }

        private void InitializeNICaddresses() {
            //part of windows multicast workaround - set list of network interface addresses (NICaddresses)
            NICaddresses = new List<System.Net.IPAddress>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.SupportsMulticast)
                {
                    foreach (var MAaddress in ni.GetIPProperties().UnicastAddresses)
                    {
                        string adr = MAaddress.Address.ToString();
                        if (adr != "127.0.0.1" && !adr.Contains("::") && !NICaddresses.Contains(MAaddress.Address))
                        {
                            NICaddresses.Add(MAaddress.Address);
                        }
                    }
                    foreach (var MAaddress in ni.GetIPProperties().MulticastAddresses)
                    {
                        string adr = MAaddress.Address.ToString();
                        if (adr != "127.0.0.1" && !adr.Contains("::") && !NICaddresses.Contains(MAaddress.Address))
                        {
                            NICaddresses.Add(MAaddress.Address);
                        }
                    }
                }
            }
        }

        private IPEndPoint ClientJoinMulticast(UdpClient client, IPAddress GroupAddress, int GroupPort) {
            IPEndPoint localEp = new IPEndPoint(IPAddress.Any, GroupPort);
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(localEp);

            if (System.Environment.OSVersion.ToString().ToLower().Contains("windows")) //why, Microsoft, WHY
            {
                //Console.WriteLine("Windows OS detected. Going the hard way...");
                /* 
                * the beginning of a glorious multicast workaround, mostly needed for Windows.
                * instead of all this, there's just got to be a line "client.JoinMulticastGroup(multicastaddress);"
                * but it works awfully unstable because of metric issue
                * see https://personalnexus.wordpress.com/2015/08/02/multicast-messages-on-windows-server-2008-r2-microsoft-failover-cluster/
                */

                foreach (var NICaddress in NICaddresses)
                {
                    try
                    {
                        client.JoinMulticastGroup(GroupAddress, NICaddress);
                        Console.WriteLine("[TAU]Successful MulticastGroup connection: " + GroupAddress.ToString() + ":" + GroupPort.ToString() + " on " + NICaddress.ToString());
                    }
                    catch (System.Net.Sockets.SocketException e)
                    {
                        //Console.WriteLine("[TAU]System.Net.Sockets.SocketException: " + e.Message);
                        //Task is to join whatever possible. Ignore if can't
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("[TAU]Exception: " + e.Message);
                    }
                }

            }
            else //for EVERY OTHER OS. A line of code that just has to work, just works. Do you read me, Microsoft?
            {
                client.JoinMulticastGroup(GroupAddress);
            }

            return localEp;
        }

        private void MulticastAnnouncerTask() {
            MulticastAnnouncerClient = new UdpClient();
            IPEndPoint localEp = ClientJoinMulticast(MulticastAnnouncerClient, MulticastAnnouncerGroupAddress, MulticastAnnouncerPort);

            AnnouncerMemoryStream = new MemoryStream();

            while (ConnectionStatus == 1) { 
                if (MulticastAnnouncerClient.Available > 0)
                {
                    byte[] receivedBytes = MulticastAnnouncerClient.Receive(ref localEp);

                    AnnouncerMemoryStream.Write(receivedBytes, 0, receivedBytes.Length);
                    AnnouncerMemoryStream.Position = 0;

                    lock (AnnouncerData) {
                        
                        AnnouncerData = (TauObjects.AnnouncerDataObj)AnnouncerDataSerializer.JsonSerializer.ReadObject(AnnouncerMemoryStream);
                        AnnouncerData.HubIP = localEp.Address.ToString();
                        AnnouncerData.Initialized = true;
                    }
                    AnnouncerMemoryStream.SetLength(0);
                }

                Thread.Sleep(AnnouncerThreadSleep);
            }
        }

        private void MulticastDataTask()
        {
            MulticastDataClient = new UdpClient();

            while (AnnouncerData == null || AnnouncerData.Initialized == false) {
                Thread.Sleep(AnnouncerThreadSleep);
            }

            string[] splitted_group = AnnouncerData.MulticastDataGroup.Split(':');

            IPAddress MulticastDataGroupAddress = IPAddress.Parse(splitted_group[0]);
            int MulticastDataPort = int.Parse(splitted_group[1]);

            IPEndPoint localEp = ClientJoinMulticast(MulticastDataClient, MulticastDataGroupAddress, MulticastDataPort);

            DataMemoryStream = new MemoryStream();
            DataMemoryStreamReader = new BinaryReader(DataMemoryStream);

            byte[] receivedBytes = new byte[1432];


            while (ConnectionStatus == 1)
            {
                if (MulticastDataClient.Available > 0)
                {
                    receivedBytes = MulticastDataClient.Receive(ref localEp);

                    DataMemoryStream.Write(receivedBytes, 0, receivedBytes.Length);
                    DataMemoryStream.Position = 0;

                    if (mappingpacket != null && mappingpacket.initialized)
                    {
                        lock (mappingpacket)
                        {
                            datapacket_temp.ParseUpdate(DataMemoryStreamReader, mappingpacket);
                        }
                    }
                    else {
                        datapacket_temp.ParseUpdate(DataMemoryStreamReader);
                    }

                    DataMemoryStream.SetLength(0);

                    lock (datapacket) {
                        datapacket.CopyFrom(datapacket_temp);
                    }
                }

                Thread.Sleep(DataThreadSleep);
            }
        }

        private void MulticastMappingTask()
        {
            MulticastMappingClient = new UdpClient();

            while (AnnouncerData == null || AnnouncerData.Initialized == false)
            {
                Thread.Sleep(AnnouncerThreadSleep);
            }

            string[] splitted_group = AnnouncerData.MulticastMappingGroup.Split(':');

            IPAddress MulticastMappingGroupAddress = IPAddress.Parse(splitted_group[0]);
            int MulticastMappingPort = int.Parse(splitted_group[1]);

            IPEndPoint localEp = ClientJoinMulticast(MulticastMappingClient, MulticastMappingGroupAddress, MulticastMappingPort);

            MappingMemoryStream = new MemoryStream();

            while (ConnectionStatus == 1)
            {
                if (MulticastMappingClient.Available > 0)
                {
                    byte[] receivedBytes = MulticastMappingClient.Receive(ref localEp);

                    lock (mappingpacket) {
                        mappingpacket.Parse(receivedBytes);
                    }
                }

                Thread.Sleep(MappingThreadSleep);
            }
        }

        private void MulticastLogsTask()
        {
            MulticastLogsClient = new UdpClient();

            while (AnnouncerData == null || AnnouncerData.Initialized == false)
            {
                Thread.Sleep(AnnouncerThreadSleep);
            }

            string[] splitted_group = AnnouncerData.MulticastLogsGroup.Split(':');

            IPAddress MulticastLogsGroupAddress = IPAddress.Parse(splitted_group[0]);
            int MulticastLogsPort = int.Parse(splitted_group[1]);

            IPEndPoint localEp = ClientJoinMulticast(MulticastLogsClient, MulticastLogsGroupAddress, MulticastLogsPort);

            while (ConnectionStatus == 1)
            {
                if (MulticastLogsClient.Available > 0)
                {
                    byte[] receivedBytes = MulticastLogsClient.Receive(ref localEp);

                    string receivedString = Encoding.UTF8.GetString(receivedBytes, 0, receivedBytes.Length);

                    lock (Logs) {

                        Logs.Add(receivedString);
                        if (Logs.Count > LogsMaxLen) {
                            Logs.RemoveAt(0);
                        }
                    }

                }

                Thread.Sleep(LogsThreadSleep);
            }
        }

        private void MulticastDebugTask()
        {
            MulticastDebugClient = new UdpClient();

            while (AnnouncerData == null || AnnouncerData.Initialized == false)
            {
                Thread.Sleep(AnnouncerThreadSleep);
            }

            string[] splitted_group = AnnouncerData.MulticastDebugGroup.Split(':');

            IPAddress MulticastDebugGroupAddress = IPAddress.Parse(splitted_group[0]);
            int MulticastDebugPort = int.Parse(splitted_group[1]);

            IPEndPoint localEp = ClientJoinMulticast(MulticastDebugClient, MulticastDebugGroupAddress, MulticastDebugPort);

            DebugMemoryStream = new MemoryStream();
            DebugMemoryStreamReader = new BinaryReader(DebugMemoryStream);

            byte[] receivedBytes = new byte[1432];

            while (ConnectionStatus == 1)
            {
                if (MulticastDebugClient.Available > 0)
                {
                    receivedBytes = MulticastDebugClient.Receive(ref localEp);

                    DebugMemoryStream.Write(receivedBytes, 0, receivedBytes.Length);
                    DebugMemoryStream.Position = 0;

                    debugpacket_temp.ParseUpdate(DebugMemoryStreamReader);

                    DebugMemoryStream.SetLength(0);

                    lock (debugpacket)
                    {
                        debugpacket.CopyFrom(debugpacket_temp);
                    }
                }

                Thread.Sleep(DebugThreadSleep);
            }
        }

        public TauObjects.AnnouncerDataObj GetAnnouncerPacket() {
            if (AnnouncerData != null && AnnouncerData.Initialized)
            {
                lock (AnnouncerData) { return AnnouncerData; }
            }
            else {
                return null;
            }
        }

        public TauObjects.DataPacket GetDataPacket()
        {
            if (datapacket != null && datapacket.initialized)
            {
                lock (datapacket) { return datapacket; }
            }
            else {
                return null;
            }
            
        }

        public TauObjects.MappingPacket GetMappingPacket()
        {
            if (mappingpacket != null && mappingpacket.initialized)
            {
                lock (mappingpacket) { return mappingpacket; }
            }
            else {
                return null;
            }
            
        }

        public List<string> GetLogs() {
            lock (Logs) { 
                List<string> logscopy = new List<string>(Logs);
                Logs.Clear();
                return logscopy;
            }
        }

        public TauObjects.DebugPacket GetDebugPacket()
        {
            if (debugpacket != null && debugpacket.initialized)
            {
                lock (debugpacket) { return debugpacket; }
            }
            else {
                return null;
            }
            
        }
    }
}
