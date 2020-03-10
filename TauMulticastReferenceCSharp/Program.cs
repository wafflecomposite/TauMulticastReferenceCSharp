using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TauMulticastReferenceCSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            var tau_multicast = new TauMulticast(logs: true, debug: true);
            tau_multicast.Connect();

            string user_input = "";

            while (true) {
                user_input = Console.ReadLine();

                if (user_input == "q")
                {
                    break;
                }
                else if (user_input == "r a")
                {
                    tau_multicast.MulticastAnnouncerConsoleWrite = true;
                }
                else if (user_input == "r d")
                {
                    tau_multicast.MulticastDataConsoleWrite = true;
                }
                else if (user_input == "r m")
                {
                    tau_multicast.MulticastMappingConsoleWrite = true;
                }
                else if (user_input == "r db")
                {
                    tau_multicast.MulticastDebugConsoleWrite = true;
                }
                else if (user_input == "r l")
                {
                    tau_multicast.MulticastLogsConsoleWrite = true;
                }
                else if (user_input == "r s")
                {
                    tau_multicast.MulticastAnnouncerConsoleWrite = false;
                    tau_multicast.MulticastDataConsoleWrite = false;
                    tau_multicast.MulticastMappingConsoleWrite = false;
                    tau_multicast.MulticastDebugConsoleWrite = false;
                    tau_multicast.MulticastLogsConsoleWrite = false;
                }
                else {
                    Console.WriteLine("unknown input");
                }
            }
            
        }
    }
}
