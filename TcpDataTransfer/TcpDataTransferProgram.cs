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

namespace TcpDataTransfer
{
    class TcpDataTransferProgram
    {
        private static TCPStatus tcpStatus = TCPStatus.CLOSED;

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
                endPointInfo.SourcePort = 3331;
                endPointInfo.DestinationPort = 8081;

                using (BerkeleyPacketFilter filter = communicator.CreateFilter("tcp port " + endPointInfo.DestinationPort))
                {
                    // Set the filter
                    communicator.SetFilter(filter);
                }

                bool clientToSendFin = false;

                List<TcpOption> tcpOptionList = new List<TcpOption>();
                tcpOptionList.Add(new TcpOptionMaximumSegmentSize(800));

                communicator.SendPacket(Utils.BuildTcpPacket(endPointInfo, TcpControlBits.Synchronize, tcpOptionList));
                tcpStatus = TCPStatus.SYN_SEND;
                PacketHandler(communicator, endPointInfo, clientToSendFin);

            }

            Console.WriteLine("Press Enter to Quit!");
            Console.ReadLine();


        }

        private static void PacketHandler(PacketCommunicator communicator, EndPointInfo endPointInfo, bool clientToSendFin = true)
        {
            Packet packet = null;
            bool running = true;

            do
            {
                PacketCommunicatorReceiveResult result = communicator.ReceivePacket(out packet);

                switch (result)
                {
                    case PacketCommunicatorReceiveResult.Timeout:
                        // Timeout elapsed
                        continue;
                    case PacketCommunicatorReceiveResult.Ok:
                        bool isRecvedPacket = (packet.Ethernet.IpV4.Destination.ToString() == endPointInfo.SourceIp) ? true : false;

                        if (isRecvedPacket)
                        {
                            switch (packet.Ethernet.IpV4.Tcp.ControlBits)
                            {
                                case (TcpControlBits.Synchronize | TcpControlBits.Acknowledgment):
                                    if (tcpStatus == TCPStatus.SYN_SEND)
                                    {
                                        Utils.PacketInfoPrinter(packet);
                                        Packet ack = Utils.BuildTcpResponsePacket(packet, TcpControlBits.Acknowledgment);
                                        communicator.SendPacket(ack);
                                        tcpStatus = TCPStatus.ESTABLISHED;
                                    }
                                    break;
                                case (TcpControlBits.Fin | TcpControlBits.Acknowledgment):
                                    if (tcpStatus == TCPStatus.FIN_WAIT_2)
                                    {
                                        Utils.PacketInfoPrinter(packet);
                                        Packet ack = Utils.BuildTcpResponsePacket(packet, TcpControlBits.Acknowledgment);
                                        communicator.SendPacket(ack);
                                        tcpStatus = TCPStatus.TIME_WAIT;
                                    }
                                    else if (tcpStatus == TCPStatus.ESTABLISHED)
                                    {

                                        Utils.PacketInfoPrinter(packet);
                                        Packet ack = Utils.BuildTcpResponsePacket(packet, TcpControlBits.Acknowledgment);
                                        communicator.SendPacket(ack);
                                        tcpStatus = TCPStatus.CLOSE_WAIT;
                                    }
                                    break;
                                case TcpControlBits.Acknowledgment:
                                    if (tcpStatus == TCPStatus.FIN_WAIT_1)
                                    {
                                        tcpStatus = TCPStatus.FIN_WAIT_2;
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
                                    }
                                    else if (tcpStatus == TCPStatus.LAST_ACK)
                                    {
                                        tcpStatus = TCPStatus.CLOSED;
                                        Utils.PacketInfoPrinter(packet, tcpStatus);

                                        running = false;
                                    }
                                    else if (tcpStatus == TCPStatus.ESTABLISHED)
                                    {
                                        //print the data received from server
                                        Console.WriteLine(packet.Ethernet.IpV4.Tcp.Payload.ToString());
                                        communicator.SendPacket(Utils.BuildTcpResponsePacket(packet, TcpControlBits.Acknowledgment));

                                    }
                                    break;
                                case (TcpControlBits.Acknowledgment | TcpControlBits.Push):
                                    if (tcpStatus == TCPStatus.ESTABLISHED)
                                    {
                                        //print the data received from server
                                        Console.WriteLine(packet.Ethernet.IpV4.Tcp.Payload.ToString());
                                        communicator.SendPacket(Utils.BuildTcpResponsePacket(packet, TcpControlBits.Acknowledgment));

                                    }
                                    break;
                                default:
                                    Utils.PacketInfoPrinter(packet);
                                    break;
                            }
                        }
                        else
                        {
                            switch (packet.Ethernet.IpV4.Tcp.ControlBits)
                            {
                                case TcpControlBits.Synchronize:
                                    if (tcpStatus == TCPStatus.SYN_SEND)
                                    {
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
                                    }
                                    break;
                                case TcpControlBits.Acknowledgment:
                                    if (tcpStatus == TCPStatus.ESTABLISHED)
                                    {
                                        Utils.PacketInfoPrinter(packet, tcpStatus);

                                        if (clientToSendFin)
                                            running = false;
                                    }
                                    else if (tcpStatus == TCPStatus.TIME_WAIT)
                                    {
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
                                        running = false;
                                    }
                                    else if (tcpStatus == TCPStatus.CLOSE_WAIT)
                                    {
                                        Utils.PacketInfoPrinter(packet, tcpStatus);

                                        StringBuilder dataString = new StringBuilder();
                                        for (int i = 0; i < 30; i++)
                                        {
                                            dataString.Append("data from server");
                                        }
                                        Packet data = Utils.BuildTcpPacket(endPointInfo, TcpControlBits.Acknowledgment, null, true, dataString.ToString());
                                        communicator.SendPacket(data);

                                        Packet fin = Utils.BuildTcpPacket(endPointInfo, TcpControlBits.Fin | TcpControlBits.Acknowledgment);
                                        communicator.SendPacket(fin);
                                        tcpStatus = TCPStatus.LAST_ACK;
                                    }
                                    break;
                                case (TcpControlBits.Fin | TcpControlBits.Acknowledgment):
                                    if (tcpStatus == TCPStatus.FIN_WAIT_1)
                                    {
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
                                    }
                                    else if (tcpStatus == TCPStatus.LAST_ACK)
                                    {
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
                                    }
                                    break;
                                default:
                                    Utils.PacketInfoPrinter(packet);
                                    break;
                            }
                        }
                        break;
                    default:
                        throw new InvalidOperationException("The result " + result + " should never be reached here");
                }
            } while (running);

        }

    }
}
