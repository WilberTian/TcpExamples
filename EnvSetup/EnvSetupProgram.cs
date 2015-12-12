using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using PcapDotNet.Base;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using PcapDotNet.Packets.Http;

using TcpUtils;

namespace EnvSetup
{
    class EnvSetupProgram
    {
        static void Main(string[] args)
        {
            // Take the selected adapter
            PacketDevice selectedDevice = Utils.GetNICDevice();

            // Open the output device
            using (PacketCommunicator communicator = selectedDevice.Open(System.Int32.MaxValue, // name of the device
                                                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                                                         1)) // read timeout
            {
                EndPointInfo endPointInfo = new EndPointInfo();
                endPointInfo.SourceMac = "08:00:27:00:C0:D5";
                endPointInfo.DestinationMac = "08:00:27:70:A6:AE";
                endPointInfo.SourceIp = "192.168.56.101";
                endPointInfo.DestinationIp = "192.168.56.102";
                endPointInfo.SourcePort = 3330;
                endPointInfo.DestinationPort = 8081;

                using (BerkeleyPacketFilter filter = communicator.CreateFilter("tcp port " + endPointInfo.DestinationPort))
                {
                    // Set the filter
                    communicator.SetFilter(filter);
                }

                communicator.SendPacket(Utils.BuildTcpPacket(endPointInfo, TcpControlBits.Synchronize, null));
                PacketHandler(communicator, endPointInfo);
            }

            Console.WriteLine("Press Enter to Quit!");
            Console.ReadLine();

        }

        private static void PacketHandler(PacketCommunicator communicator, EndPointInfo endPointInfo)
        {
            Packet packet = null;

            do
            {
                PacketCommunicatorReceiveResult result = communicator.ReceivePacket(out packet);

                switch (result)
                {
                    case PacketCommunicatorReceiveResult.Timeout:
                        // Timeout elapsed
                        continue;
                    case PacketCommunicatorReceiveResult.Ok:
                        Utils.PacketInfoPrinter(packet);
                        break;
                    default:
                        throw new InvalidOperationException("The result " + result + " should never be reached here");
                }
            } while (true);
        }
    }
}
