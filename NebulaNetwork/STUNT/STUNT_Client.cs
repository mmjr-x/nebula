using LumiSoft.Net.STUN.Client;
using LumiSoft.Net.STUN.Message;
using System;
using System.Net;
using System.Net.Sockets;

namespace NebulaNetwork.STUNT
{
    //
    // Summary:
    //     This class implements STUN client. Defined in RFC 3489.
    public class STUNT_Client
    {
        //
        // Summary:
        //     Gets NAT info from STUN server.
        //
        // Parameters:
        //   host:
        //     STUN server name or IP.
        //
        //   port:
        //     STUN server port. Default port is 3478.
        //
        //   socket:
        //     UDP socket to use.
        //
        // Returns:
        //     Returns UDP netwrok info.
        //
        // Exceptions:
        //   T:System.Exception:
        //     Throws exception if unexpected error happens.
        public static STUN_Result Query(string host, int port, Socket socket)
        {
            if (host == null)
            {
                throw new ArgumentNullException("host");
            }

            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }

            if (port < 1)
            {
                throw new ArgumentException("Port value must be >= 1 !");
            }

            //if (socket.ProtocolType != ProtocolType.Udp)
            //{
            //  throw new ArgumentException("Socket must be UDP socket !");
            //}

            IPEndPoint remoteEndPoint = new IPEndPoint(System.Net.Dns.GetHostAddresses(host)[0], port);
            socket.ReceiveTimeout = 3000;
            socket.SendTimeout = 3000;
            STUN_Message sTUN_Message = new STUN_Message();
            sTUN_Message.Type = STUN_MessageType.BindingRequest;
            STUN_Message sTUN_Message2 = DoTransaction(sTUN_Message, socket, remoteEndPoint);
            if (sTUN_Message2 == null)
            {
                return new STUN_Result(STUN_NetType.UdpBlocked, null);
            }

            STUN_Message sTUN_Message3 = new STUN_Message();
            sTUN_Message3.Type = STUN_MessageType.BindingRequest;
            sTUN_Message3.ChangeRequest = new STUN_t_ChangeRequest(changeIP: true, changePort: true);
            STUN_Message sTUN_Message4;
            if (socket.LocalEndPoint.Equals(sTUN_Message2.MappedAddress))
            {
                sTUN_Message4 = DoTransaction(sTUN_Message3, socket, remoteEndPoint);
                if (sTUN_Message4 != null)
                {
                    return new STUN_Result(STUN_NetType.OpenInternet, sTUN_Message2.MappedAddress);
                }

                return new STUN_Result(STUN_NetType.SymmetricUdpFirewall, sTUN_Message2.MappedAddress);
            }

            sTUN_Message4 = DoTransaction(sTUN_Message3, socket, remoteEndPoint);
            if (sTUN_Message4 != null)
            {
                return new STUN_Result(STUN_NetType.FullCone, sTUN_Message2.MappedAddress);
            }

            //STUN_Message sTUN_Message5 = new STUN_Message();
            //sTUN_Message5.Type = STUN_MessageType.BindingRequest;
            //STUN_Message sTUN_Message6 = DoTransaction(sTUN_Message5, socket, sTUN_Message2.ChangedAddress);
            //if (sTUN_Message6 == null)
            //{
            //  throw new Exception("STUN Test I(II) dind't get resonse !");
            //}

            //if (!sTUN_Message6.MappedAddress.Equals(sTUN_Message2.MappedAddress))
            //{
            //  return new STUN_Result(STUN_NetType.Symmetric, sTUN_Message2.MappedAddress);
            //}

            STUN_Message sTUN_Message7 = new STUN_Message();
            sTUN_Message7.Type = STUN_MessageType.BindingRequest;
            sTUN_Message7.ChangeRequest = new STUN_t_ChangeRequest(changeIP: false, changePort: true);
            STUN_Message sTUN_Message8 = DoTransaction(sTUN_Message7, socket, sTUN_Message2.ChangedAddress);
            if (sTUN_Message8 != null)
            {
                return new STUN_Result(STUN_NetType.RestrictedCone, sTUN_Message2.MappedAddress);
            }

            return new STUN_Result(STUN_NetType.PortRestrictedCone, sTUN_Message2.MappedAddress);
        }

        private void GetSharedSecret()
        {
        }

        //
        // Summary:
        //     Does STUN transaction. Returns transaction response or null if transaction failed.
        //
        // Parameters:
        //   request:
        //     STUN message.
        //
        //   socket:
        //     Socket to use for send/receive.
        //
        //   remoteEndPoint:
        //     Remote end point.
        //
        // Returns:
        //     Returns transaction response or null if transaction failed.
        private static STUN_Message DoTransaction(STUN_Message request, Socket socket, IPEndPoint remoteEndPoint)
        {
            //Console.Write("Hello world");
            byte[] buffer = request.ToByteData();
            //DateTime now = DateTime.Now;
            //while (now.AddSeconds(2.0) > DateTime.Now)
            //{
            try
            {
                //socket.SendTo(buffer, remoteEndPoint);
                socket.Connect(remoteEndPoint);
                socket.Send(buffer);
                //if (socket.Poll(100, SelectMode.SelectRead))
                if (true)
                {
                    //Console.Write("Are we getting here?");
                    byte[] array = new byte[512];
                    socket.Receive(array);
                    STUN_Message sTUN_Message = new STUN_Message();
                    sTUN_Message.Parse(array);
                    if (request.TransactionID.Equals(sTUN_Message.TransactionID))
                    {
                        //socket.Shutdown(SocketShutdown.Both);
                        //socket.Disconnect(false);
                        return sTUN_Message;
                    }
                }

                //socket.Shutdown(SocketShutdown.Both);
                //socket.Disconnect(false);
            }
            catch
            {
                //socket.Shutdown(SocketShutdown.Both);
                //socket.Disconnect(false);
            }
            //}

            return null;
        }
    }
}
