using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;

using UnityEngine;

namespace UnitySocketClient
{
    [System.Serializable]
    public class SocketClient
    {
        public Socket Socket;

        public AddressFamily AddressFamily = AddressFamily.InterNetwork;
        public SocketType SocketType = SocketType.Stream;
        public ProtocolType ProtocolType = ProtocolType.Tcp;
        public string IP;
        public int Port;
        private IPEndPoint iPEndPoint;

        private Byte[] bytesReceived = new Byte[256];
        private int receivedBytesLength = 0;
        public Queue<string> ResponsesQueue = new Queue<string>();
        private string Response = "not yet";

        public SocketError SendErrorCode;
        public SocketError ReceiveErrorCode;


        public SocketClient() { }
        public SocketClient(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, IPEndPoint iPEndPoint) 
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
            SetSocket();

            this.iPEndPoint = iPEndPoint;
            SetEndPoint();
        }

        public SocketClient(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, string iP, int port) 
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
            SetSocket();

            IP = iP;
            Port = port;
            SetEndPoint();
        }

        public void SetSocket()
        {
            Socket = new Socket(AddressFamily, SocketType, ProtocolType);
        }

        public void SetEndPoint()
        {
            iPEndPoint = new IPEndPoint(IPAddress.Parse(IP), Port);
            MonoBehaviour.print(iPEndPoint);
        }

        public bool Connect()
        {
            if (Socket == null) SetSocket();

            if (iPEndPoint == null) SetEndPoint();

            Socket.Connect(iPEndPoint);

            if (Socket == null)
            {
                Debug.LogFormat("Connection failed");
                return false;
            }
            else
            {
                Debug.LogFormat("Connection Success");
                return true;
            }
        }
        public void UDPSend(byte[] bytes)
        {
            Socket.SendTo(bytes, iPEndPoint);
        }

        public void UDPSend(string str)
        {
            UDPSend(UTF8FromString(str));
        }

        public void UDPReceive()
        {
            
        }

        public SocketError TCPSend(byte[] bytes)
        {
            Socket.Send(bytes, 0, bytes.Length, SocketFlags.None, out SendErrorCode);

            if (SendErrorCode != SocketError.Success)
            {
                Debug.LogFormat(SendErrorCode.ToString());
            }

            return SendErrorCode;
        }

        public SocketError TCPSend(string str)
        {
            byte[] bytesSent = new byte[str.Length * sizeof(char)];

            int index = 0;
            foreach (char c in str)
            {
                Array.Copy(BitConverter.GetBytes(c), 0, bytesSent, index * sizeof(char), sizeof(char));

                index++;
            }

            return TCPSend(bytesSent);
        }

        SocketError TCPReceive()
        {
            receivedBytesLength = Socket.Receive(bytesReceived, 0, bytesReceived.Length, SocketFlags.None, out ReceiveErrorCode);
            if (ReceiveErrorCode != SocketError.Success)
            {
                Debug.LogFormat(ReceiveErrorCode.ToString());
            }

            return ReceiveErrorCode;
        }

        public void TCPSendAsync(byte[] bytes)
        {
            Socket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, out SendErrorCode, AfterTCPSend, null);
        }

        public void TCPSendAsync(string str = null)
        {
            byte[] bytesSent = new byte[str.Length * sizeof(char)];

            int index = 0;
            foreach (char c in str)
            {
                Array.Copy(BitConverter.GetBytes(c), 0, bytesSent, index * sizeof(char), sizeof(char));

                index++;
            }

            TCPSendAsync(bytesSent);
        }

        void TCPReceiveAsync()
        {
            Socket.BeginReceive(bytesReceived, 0, bytesReceived.Length, SocketFlags.None, out ReceiveErrorCode, AfterTCPReceive, null);
        }

        void AfterTCPSend(IAsyncResult result)
        {
            Socket.EndSend(result, out SendErrorCode);
            Debug.LogFormat("End Send {0}", SendErrorCode);
        }

        void AfterTCPReceive(IAsyncResult result)
        {
            Socket.EndReceive(result, out ReceiveErrorCode);
            Response = Encoding.ASCII.GetString(bytesReceived, 0, receivedBytesLength);
            if (ReceiveErrorCode == SocketError.Success)
            {
                Debug.LogFormat($"End Receive succeed : {Response}");
            } else
            {
                Debug.LogFormat($"End Receive fail : {ReceiveErrorCode.ToString()}");
            }
        }

        byte[] UTF8FromString(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        public string GetResponse()
        {
            return ResponsesQueue.Dequeue();
        }

    }
}
