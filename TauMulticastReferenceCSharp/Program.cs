using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// .NET Framework >= 4.0 REQUIRED
// This is an example of TauMulticast.cs and TauObjects.cs usage in the form of a CLI tool.

namespace TauMulticastReferenceCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            var tau_multicast = new TauMulticast(logs: true, debug: true); //logs and debug threads should be enabled on initialization if needed

            Thread AnnouncerReadingThread, //packet reading threads
            DataReadingThread,
            MappingReadingThread,
            LogsReadingThread,
            DebugReadingThread;

            bool AnnouncerRead = false, //packet reading bools ('should we read packet' flag for every thread)
            DataRead = false,
            MappingRead = false,
            LogsRead = false,
            DebugRead = false;

            // this is example of datapacket reading
            // usually it's the only packet you're really interested in since it contains all necessary sensor data
            // it doesn't have to be a thread, for example you can read packet every frame
            DataReadingThread = new Thread(() =>
            {
                while (true)
                {
                    if (DataRead) //if reading flag is on
                    {
                        TauObjects.DataPacket datapacket = tau_multicast.GetDataPacket(); //get packet
                        if (datapacket != null) //check if it's null before trying to get data from it - this could well be the case if there was connection problems
                        {
                            // just print the data for now, refer to the TauObjects.DataPacket class to actually use sensor data
                            Console.WriteLine(String.Format("= = = = = = = = = =\n{0}", datapacket.ToString()));
                        }
                        else
                        {
                            Console.WriteLine("= = = [datapacket is null] = = =\n");
                        }
                    }
                    // doesn't have to be DataThreadSleep, it's just lowest value that still makes sense. 
                    // no matter how long the sleep between requests, you will get latest packet available
                    Thread.Sleep(tau_multicast.DataThreadSleep);
                }
            }){ IsBackground = true };
            DataReadingThread.Start();

            AnnouncerReadingThread = new Thread(() =>
            {
                while (true) { 
                    if (AnnouncerRead) {
                        TauObjects.AnnouncerDataObj announcerpacket = tau_multicast.GetAnnouncerPacket();
                        if (announcerpacket != null) {
                            Console.WriteLine(String.Format("= = = = = = = = = =\n{0}", announcerpacket.ToString()));
                        }
                        else
                        {
                            Console.WriteLine("= = = [announcerpacket is null] = = =\n");
                        }

                    }
                    Thread.Sleep(1000);
                }
            }){ IsBackground = true };
            AnnouncerReadingThread.Start();

            MappingReadingThread = new Thread(() =>
            {
                while (true)
                {
                    if (MappingRead)
                    {
                        TauObjects.MappingPacket mappingpacket = tau_multicast.GetMappingPacket();
                        if (mappingpacket != null)
                        {
                            Console.WriteLine(String.Format("= = = = = = = = = =\n{0}", mappingpacket.ToString()));
                        }
                        else
                        {
                            Console.WriteLine("= = = [mappingpacket is null] = = =\n");
                        }

                    }
                    Thread.Sleep(tau_multicast.MappingThreadSleep);
                }
            }){ IsBackground = true };
            MappingReadingThread.Start();

            LogsReadingThread = new Thread(() =>
            {
                while (true)
                {
                    if (LogsRead)
                    {
                        List<string> logs = tau_multicast.GetLogs();
                        if (logs.Count > 0) { 
                            foreach (var logstr in logs)
                            {
                                Console.WriteLine(logstr);
                            }
                        }
                    }
                    Thread.Sleep(tau_multicast.LogsThreadSleep);
                }
            }){ IsBackground = true };
            LogsReadingThread.Start();

            DebugReadingThread = new Thread(() =>
            {
                while (true)
                {
                    if (DebugRead)
                    {
                        TauObjects.DebugPacket debugpacket = tau_multicast.GetDebugPacket();
                        if (debugpacket != null)
                        {
                            Console.WriteLine(String.Format("= = = = = = = = = =\n{0}", debugpacket.ToString()));
                        }
                        else
                        {
                            Console.WriteLine("= = = [debugpacket is null] = = =\n");
                        }

                    }
                    Thread.Sleep(tau_multicast.DebugThreadSleep);
                }
            }){ IsBackground = true };
            DebugReadingThread.Start();

            string user_input = "";

            while (true) {

                user_input = Console.ReadLine();

                if (user_input == "q")
                {
                    break;
                }

                else if (user_input == "c") //connect
                {
                    Console.WriteLine(String.Format("Connect() state: {0}", tau_multicast.Connect())); //normally should return 0
                }

                else if (user_input == "d") //disconnect
                {
                    Console.WriteLine(String.Format("Disconnect() state: {0}", tau_multicast.Disconnect())); //normally should return 0
                }

                else if (user_input == "s")  //status (connectionstatus)
                {
                    Console.WriteLine(String.Format("ConnectionStatus state: {0}", tau_multicast.ConnectionStatus)); //0 - not connected, 1 - connected, 2 - disconnect pending
                }

                else if (user_input == "r a") // read announcer packets
                {
                    AnnouncerRead = true;
                }

                else if (user_input == "r d") // read data packets
                {
                    DataRead = true;
                }

                else if (user_input == "r m") // read mapping packets
                {
                    MappingRead = true;
                }

                else if (user_input == "r db") // read debug packets
                {
                    DebugRead = true;
                }

                else if (user_input == "r l") // read logs
                {
                    LogsRead = true;
                }

                else if (user_input == "r s") // read stop
                {
                    AnnouncerRead = false;
                    DataRead = false;
                    MappingRead = false;
                    DebugRead = false;
                    LogsRead = false;
                }

                else {
                    Console.WriteLine("unknown input");
                }
            }
            
        }
    }
}
