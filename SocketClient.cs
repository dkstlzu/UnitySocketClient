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

            
            if (!Socket.Connected)
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

        public void ConnectAsync()
        {
            if (Socket == null) SetSocket();

            if (iPEndPoint == null) SetEndPoint();

            Socket.BeginConnect(iPEndPoint, AfterConnectAsync, null);
        }

        void AfterConnectAsync(IAsyncResult result)
        {
            try
            {
                Socket.EndConnect(result);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
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
            EndPoint EP = (EndPoint)iPEndPoint;
            Socket.ReceiveFrom(bytesReceived, 0, bytesReceived.Length, SocketFlags.None, ref EP);
        }
        
        public void UDPSendAsync(byte[] bytes)
        {
            Socket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, iPEndPoint, AfterUDPSend, null);
        }

        public void UDPSendAsync(string str)
        {
            UDPSendAsync(UTF8FromString(str));
        }

        public void UDPReceiveAsync()
        {
            EndPoint EP = (EndPoint)iPEndPoint;
            Socket.BeginReceiveFrom(bytesReceived, 0, bytesReceived.Length, SocketFlags.None, ref EP, AfterUDPRecieve, EP);
        }

        void AfterUDPSend(IAsyncResult result)
        {
            try
            {
                Socket.EndSendTo(result);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }

        void AfterUDPRecieve(IAsyncResult result)
        {
            try
            {
                EndPoint EP= (EndPoint)result.AsyncState;
                int dataLength = Socket.EndReceiveFrom(result, ref EP);
                if (dataLength > 0)
                {
                    ResponsesQueue.Enqueue(StringFromUTF8(bytesReceived));
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
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
            try
            {
                Socket.EndSend(result, out SendErrorCode);
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
                Debug.LogError(SendErrorCode.ToString());
            }
        }

        void AfterTCPReceive(IAsyncResult result)
        {
            try
            {
                Socket.EndReceive(result, out ReceiveErrorCode);
                ResponsesQueue.Enqueue(StringFromUTF8(bytesReceived));
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
                Debug.LogError(SendErrorCode.ToString());
            }
        }

        byte[] UTF8FromString(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        string StringFromUTF8(byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        public string GetResponse()
        {
            return ResponsesQueue.Dequeue();
        }

    }
}
