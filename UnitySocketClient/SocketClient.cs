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
    public enum PrintMode
    {
        Console,
        UnityEditor,
    }

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

        private Action<object> print = MonoBehaviour.print;
        private Action<string> printLog = Debug.Log;
        private Action<string> printWarning = Debug.LogWarning;
        private Action<string> printError = Debug.LogError;

        public event Action<IAsyncResult> OnConnectComplete;

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

        public void SetPrintMode(PrintMode mode)
        {
            //print = mode switch
            //{
                //PrintMode.Console => Console.WirteLine
            //}
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

            print("Connecting to server");

            Socket.BeginConnect(iPEndPoint, AfterConnectAsync, null);
        }

        public void UnConnect()
        {
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Close();
        }

        ~SocketClient()
        {
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Close();
        }

        void AfterConnectAsync(IAsyncResult result)
        {
            try
            {
                Socket.EndConnect(result);
                print($"Connect finish and result : {result}");
                print($"Connect finish and result : {result.GetType()}");
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

        public void UDPSendAsync(byte[] bytes)
        {
            Socket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, iPEndPoint, AfterUDPSend, null);
        }

        public void UDPSendAsync(string str)
        {
            UDPSendAsync(UTF8FromString(str));
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

        public void UDPReceive()
        {
            EndPoint EP = (EndPoint)iPEndPoint;
            Socket.ReceiveFrom(bytesReceived, 0, bytesReceived.Length, SocketFlags.None, ref EP);
        }
        
        public void UDPReceiveAsync()
        {
            EndPoint EP = (EndPoint)iPEndPoint;
            Socket.BeginReceiveFrom(bytesReceived, 0, bytesReceived.Length, SocketFlags.None, ref EP, AfterUDPRecieve, EP);
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
                    Array.Clear(bytesReceived, 0, bytesReceived.Length);
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
            return TCPSend(UTF8FromString(str));
        }

        public void TCPSendAsync(byte[] bytes)
        {
            Socket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, out SendErrorCode, AfterTCPSend, null);
        }

        public void TCPSendAsync(string str = null)
        {
            TCPSendAsync(UTF8FromString(str));
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

        public SocketError TCPReceive()
        {
            receivedBytesLength = Socket.Receive(bytesReceived, 0, bytesReceived.Length, SocketFlags.None, out ReceiveErrorCode);
            if (ReceiveErrorCode != SocketError.Success)
            {
                Debug.LogFormat(ReceiveErrorCode.ToString());
            }

            return ReceiveErrorCode;
        }

        public void TCPReceiveAsync()
        {
            Socket.BeginReceive(bytesReceived, 0, bytesReceived.Length, SocketFlags.None, out ReceiveErrorCode, AfterTCPReceive, null);
        }

        void AfterTCPReceive(IAsyncResult result)
        {
            try
            {
                Socket.EndReceive(result, out ReceiveErrorCode);
                ResponsesQueue.Enqueue(StringFromUTF8(bytesReceived));
                Array.Clear(bytesReceived, 0, bytesReceived.Length);
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
            return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
        }

        public string GetResponse()
        {
            if (ResponsesQueue.Count == 0)
                return string.Empty;
            else
                return ResponsesQueue.Dequeue();
        }

        void PrintByteArray(byte[] byteArr)
        {
            for (int i = 0; i < byteArr.Length; i++)
            {
                print("Index : " + i + ", Content : " + byteArr[i]);
            }
        }

    }
}
