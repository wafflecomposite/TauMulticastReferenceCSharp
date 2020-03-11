﻿using System;
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

        public int GeneralConnectionStatus = 1; //0 - not connected, 1 - connected, 2 - disconnect request pending

        public static IPAddress MulticastAnnouncerGroupAddress = IPAddress.Parse("239.255.255.151");
        public static int MulticastAnnouncerPort = 16061;

        private MemoryStream AnnouncerMemoryStream,
            DataMemoryStream,
            MappingMemoryStream,
            DebugMemoryStream;

        private BinaryReader DataMemoryStreamReader, DebugMemoryStreamReader;

        private TauObjects.AnnouncerDataSerializer AnnouncerDataSerializer = new TauObjects.AnnouncerDataSerializer();
        public TauObjects.AnnouncerDataObj AnnouncerData = new TauObjects.AnnouncerDataObj();

        public TauObjects.DataPacket datapacket = new TauObjects.DataPacket();
        public TauObjects.DataPacket datapacket_temp = new TauObjects.DataPacket();
        public TauObjects.DebugPacket debugpacket = new TauObjects.DebugPacket();
        public TauObjects.DebugPacket debugpacket_temp = new TauObjects.DebugPacket();
        public TauObjects.MappingPacket mappingpacket = new TauObjects.MappingPacket();

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

        public bool MulticastAnnouncerConsoleWrite,
            MulticastDataConsoleWrite,
            MulticastMappingConsoleWrite,
            MulticastLogsConsoleWrite,
            MulticastDebugConsoleWrite;

        public int AnnouncerThreadSleep = 100;
        public int DataThreadSleep = 5;
        public int MappingThreadSleep = 100;
        public int LogsThreadSleep = 10;
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

        public void Connect()
        {
            MulticastAnnouncerThread = new Thread(() => MulticastAnnouncerTask()) { IsBackground = true };
            MulticastAnnouncerThread.Start();
            MulticastDataThread = new Thread(() => MulticastDataTask()) { IsBackground = true };
            MulticastDataThread.Start();
            MulticastMappingThread = new Thread(() => MulticastMappingTask()) { IsBackground = true };
            MulticastMappingThread.Start();

            if (EnableLogsThread) {
                MulticastLogsThread = new Thread(() => MulticastLogsTask()) { IsBackground = true };
                MulticastLogsThread.Start();
            }

            if (EnableDebugThread)
            {
                MulticastDebugThread = new Thread(() => MulticastDebugTask()) { IsBackground = true };
                MulticastDebugThread.Start();
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
                        Console.WriteLine("Successful connection: " + GroupAddress.ToString() + ":" + GroupPort.ToString() + " on " + NICaddress.ToString());
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

            while (GeneralConnectionStatus == 1) { 
                if (MulticastAnnouncerClient.Available > 0)
                {
                    byte[] receivedBytes = MulticastAnnouncerClient.Receive(ref localEp);

                    AnnouncerMemoryStream.Write(receivedBytes, 0, receivedBytes.Length);
                    AnnouncerMemoryStream.Position = 0;

                    lock (AnnouncerData) {
                        
                        AnnouncerData = (TauObjects.AnnouncerDataObj)AnnouncerDataSerializer.JsonSerializer.ReadObject(AnnouncerMemoryStream);
                        AnnouncerData.HubIP = localEp.Address.ToString();
                        AnnouncerData.Initialized = true;

                        if (MulticastAnnouncerConsoleWrite)
                        {
                            Console.WriteLine(String.Format("= = = = = = = = = =\nIP: {0}\n{1}", localEp.Address.ToString(), AnnouncerData.ToString()));
                        }
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


            while (GeneralConnectionStatus == 1)
            {
                if (MulticastDataClient.Available > 0)
                {
                    receivedBytes = MulticastDataClient.Receive(ref localEp);
                    //int resp_length = MulticastDataClient.Client.ReceiveFrom(receivedBytes, ref localEp);

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

                        if (MulticastDataConsoleWrite)
                        {
                            //Console.WriteLine(String.Format("= = = = = = = = = =\n{0}", datapacket_temp.ToString()));
                            Console.WriteLine(String.Format("= = = = = = = = = =\n{0}", datapacket.ToString()));
                        }
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

            while (GeneralConnectionStatus == 1)
            {
                if (MulticastMappingClient.Available > 0)
                {
                    byte[] receivedBytes = MulticastMappingClient.Receive(ref localEp);

                    lock (mappingpacket) {
                        mappingpacket.Parse(receivedBytes);

                        if (MulticastMappingConsoleWrite)
                        {
                            Console.WriteLine(String.Format("= = = = = = = = = =\n{0}", mappingpacket.ToString()));
                        }
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

            while (GeneralConnectionStatus == 1)
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

                        if (MulticastLogsConsoleWrite)
                        {
                            foreach (var logstr in Logs) {
                                Console.WriteLine(receivedString);
                            }
                            Logs.Clear();
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

            while (GeneralConnectionStatus == 1)
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

                        if (MulticastDebugConsoleWrite)
                        {
                            Console.WriteLine(String.Format("= = = = = = = = = =\n{0}", debugpacket.ToString()));
                        }
                    }
                }

                Thread.Sleep(DebugThreadSleep);
            }
        }
    }
}
