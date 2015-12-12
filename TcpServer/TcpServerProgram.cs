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

namespace TcpServer
{
    class TcpServerProgram
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
                endPointInfo.DestinationMac = "";
                endPointInfo.SourceIp = "192.168.56.101";
                endPointInfo.DestinationIp = "";
                endPointInfo.SourcePort = 3333;
                endPointInfo.DestinationPort = 0;

                using (BerkeleyPacketFilter filter = communicator.CreateFilter("tcp port " + endPointInfo.SourcePort))
                {
                    // Set the filter
                    communicator.SetFilter(filter);
                }

                tcpStatus = TCPStatus.LISTENING;
                PacketHandler(communicator, endPointInfo);
            }

            Console.WriteLine("Press Enter to Quit!");
            Console.ReadLine();


        }

        private static void PacketHandler(PacketCommunicator communicator, EndPointInfo endPointInfo)
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
                                case TcpControlBits.Synchronize:
                                    if (tcpStatus == TCPStatus.LISTENING)
                                    {
                                        endPointInfo.DestinationMac = packet.Ethernet.Source.ToString();
                                        endPointInfo.DestinationIp = packet.Ethernet.IpV4.Source.ToString();
                                        endPointInfo.DestinationPort = packet.Ethernet.IpV4.Tcp.SourcePort;

                                        Utils.PacketInfoPrinter(packet);
                                        Packet synAck = Utils.BuildTcpResponsePacket(packet, TcpControlBits.Synchronize | TcpControlBits.Acknowledgment);
                                        communicator.SendPacket(synAck);
                                        tcpStatus = TCPStatus.SYN_RECEIVED;
                                    }
                                    break;
                                case TcpControlBits.Acknowledgment:
                                    if (tcpStatus == TCPStatus.SYN_RECEIVED)
                                    {
                                        tcpStatus = TCPStatus.ESTABLISHED;
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
                                    }
                                    else if (tcpStatus == TCPStatus.LAST_ACK)
                                    {
                                        tcpStatus = TCPStatus.CLOSED;
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
                                        tcpStatus = TCPStatus.LISTENING;
                                    }
                                    else if (tcpStatus == TCPStatus.FIN_WAIT_1)
                                    {
                                        tcpStatus = TCPStatus.FIN_WAIT_2;
                                        Utils.PacketInfoPrinter(packet);
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
                                default:
                                    Utils.PacketInfoPrinter(packet);
                                    break;
                            }
                        }
                        else
                        {
                            switch (packet.Ethernet.IpV4.Tcp.ControlBits)
                            {
                                case (TcpControlBits.Synchronize | TcpControlBits.Acknowledgment):
                                    if (tcpStatus == TCPStatus.SYN_RECEIVED)
                                    {
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
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
                                case TcpControlBits.Acknowledgment:
                                    if (tcpStatus == TCPStatus.TIME_WAIT)
                                    {
                                        Utils.PacketInfoPrinter(packet, tcpStatus);
                                    }
                                    else if (tcpStatus == TCPStatus.CLOSE_WAIT)
                                    {
                                        
                                        Utils.PacketInfoPrinter(packet, tcpStatus);

                                        Packet fin = Utils.BuildTcpPacket(endPointInfo, TcpControlBits.Fin | TcpControlBits.Acknowledgment);
                                        communicator.SendPacket(fin);
                                        tcpStatus = TCPStatus.LAST_ACK;
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
