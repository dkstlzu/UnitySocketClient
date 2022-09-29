using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

using UnityEngine;

namespace UnitySocketClient
{
    public class EmptyPacketException : System.Exception
    {
        public override string Message => "Got Empty Packet terminate auto receive";
    }

    [System.Serializable]
    public class SocketClient : IDisposable
    {
        public Socket Socket;

        public AddressFamily AddressFamily = AddressFamily.InterNetwork;
        public SocketType SocketType = SocketType.Stream;
        public ProtocolType ProtocolType = ProtocolType.Tcp;
        public string IP;
        public int TCPPort;
        public int UDPPort;
        public bool AutoRecieve;
        IPEndPoint _IPEndPoint;
        public bool LogMessages;

        private Byte[] bytesReceived = new Byte[2048];
        private int receivedBytesLength = 0;
        private int _bytesBufferLength;
        public int BytesBufferLength
        {
            get
            {
                return _bytesBufferLength;
            }
            set
            {
                _bytesBufferLength = Mathf.Min(bytesReceived.Length, value);
            }
        }

        public SocketError SendErrorCode;
        public SocketError ReceiveErrorCode;

        private Action<object> print = MonoBehaviour.print;
        private Action<string> printError = Debug.LogError;

        public event Action OnConnectComplete;
        public event Action OnDisconnected;
        public event Action<Byte[]> OnBytesRecieved;
        public event Action<string> OnDataRecievedAsString;

        List<IAsyncResult> _IAsyncResultList = new List<IAsyncResult>();

        public SocketClient() { }
        public SocketClient(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, IPEndPoint ipEndPoint) 
        {
            Init(addressFamily, SocketType, protocolType, ipEndPoint);
        }

        public SocketClient(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, string iP, int port) 
        {
            IP = iP;
            TCPPort = port;
            IPEndPoint _ipEndPoint = new IPEndPoint(IPAddress.Parse(iP), port);
            Init(addressFamily, SocketType, protocolType, _ipEndPoint);
        }

        void Init(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, IPEndPoint ipEndPoint)
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
            SetSocket();

            _IPEndPoint = ipEndPoint;
        }

        public void SetSocket()
        {
            Socket = new Socket(AddressFamily, SocketType, ProtocolType);
            print($"AddressFamily : {AddressFamily}, SocketType : {SocketType}, ProtocolType {ProtocolType}");
        }

        public void SetTCPEndPoint()
        {
            _IPEndPoint = new IPEndPoint(IPAddress.Parse(IP), TCPPort);
            print("SetTCPEndPoint : " + _IPEndPoint);
        }

        public void SetUDPEndPoint()
        {
            UDPPort = GetOpenPort();

            if (UDPPort == 0)
            {
                UDPPort = GetOpenPort(8500, 500);
            }

            if (UDPPort == 0)
            {
                Debug.LogError("Can not assign open port to udp");
            } else
            {
                print($"UDPPort assigned {UDPPort}");
            }
        }

        public static int GetOpenPort(int start = 8000, int checkRange = 500)
        {
            int startingAtPort = start;
            int maxNumberOfPortsToCheck = checkRange;
            IEnumerable<int> range = Enumerable.Range(startingAtPort, maxNumberOfPortsToCheck);
            IEnumerable<int> portsInUse = 
                from p in range
                    join used in System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners()
                        on p equals used.Port
                            select p;

            int FirstFreeUDPPortInRange = range.Except(portsInUse).FirstOrDefault();

            return FirstFreeUDPPortInRange;
        }

        public void Bind(int port = -1)
        {
            if (Socket == null) SetSocket();

            if (_IPEndPoint == null) SetTCPEndPoint();

            try
            {
                if (port >= 0)
                {
                    Socket.Bind(new IPEndPoint(IPAddress.Any, port));
                } else
                {
                  Socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                }

                UDPPort = ((IPEndPoint)Socket.LocalEndPoint).Port;

                if (AutoRecieve) UDPReceiveAsync();
            } catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        public bool Connect()
        {
            if (Socket == null) SetSocket();

            if (_IPEndPoint == null) SetTCPEndPoint();

            Socket.Connect(_IPEndPoint);

            
            if (!Socket.Connected)
            {
                print("Connection failed");
                return false;
            }
            else
            {
                print("Connection Success");
                OnConnectComplete?.Invoke();
                return true;
            }
        }

        public IAsyncResult ConnectAsync()
        {
            if (Socket == null) SetSocket();

            if (_IPEndPoint == null) SetTCPEndPoint();

            print("Connecting to server");

            var ar = Socket.BeginConnect(_IPEndPoint, AfterConnectAsync, null);

            _IAsyncResultList.Add(ar);

            return ar;
        }

        void AfterConnectAsync(IAsyncResult result)
        {
            try
            {
                Socket.EndConnect(result);
                print("Connection Success");
                _IAsyncResultList.Remove(result);
                OnConnectComplete?.Invoke();
                if (AutoRecieve)
                    TCPReceiveAsync();
            }
            catch (Exception e)
            {
                printError("Connection Failed" + e.ToString());
            }
        }

        public void UnConnect()
        {
            OnDisconnected?.Invoke();
            Socket.Shutdown(SocketShutdown.Both);
            Socket.Close();
        }

        public void UDPSend(byte[] bytes)
        {
            Socket.SendTo(bytes, _IPEndPoint);
        }

        public void UDPSend(string str)
        {
            UDPSend(UTF8FromString(str));
        }

        public IAsyncResult UDPSendAsync(byte[] bytes)
        {
            var ar = Socket.BeginSendTo(bytes, 0, bytes.Length, SocketFlags.None, _IPEndPoint, AfterUDPSend, null);

            _IAsyncResultList.Add(ar);

            return ar;
        }

        public IAsyncResult UDPSendAsync(string str)
        {
            return UDPSendAsync(UTF8FromString(str));
        }

        void AfterUDPSend(IAsyncResult result)
        {
            try
            {
                Socket.EndSendTo(result);
                _IAsyncResultList.Remove(result);
            }
            catch (Exception e)
            {
                printError(e.ToString());
            }
        }

        public string UDPReceive()
        {
            EndPoint EP = (EndPoint)new IPEndPoint(IPAddress.Any, UDPPort);
            string Response = string.Empty;
            receivedBytesLength = Socket.ReceiveFrom(bytesReceived, 0, BytesBufferLength, SocketFlags.None, ref EP);
            if (receivedBytesLength > 0)
            {
                Response = StringFromUTF8(bytesReceived);
                if (LogMessages) print("UDPReceive : " + Response);
            }
            Array.Clear(bytesReceived, 0, BytesBufferLength);
            return Response;
        }
        
        public IAsyncResult UDPReceiveAsync()
        {
            EndPoint EP = (EndPoint)new IPEndPoint(IPAddress.Any, UDPPort);
            var ar = Socket.BeginReceiveFrom(bytesReceived, 0, BytesBufferLength, SocketFlags.None, ref EP, AfterUDPRecieve, EP);

            _IAsyncResultList.Add(ar);
            
            return ar;
        }

        void AfterUDPRecieve(IAsyncResult result)
        {
            try
            {
                EndPoint EP = (EndPoint)result.AsyncState;
                int dataLength = Socket.EndReceiveFrom(result, ref EP);
                if (dataLength > 0)
                {
                    string reply = StringFromUTF8(bytesReceived);
                    if (reply == string.Empty) throw new EmptyPacketException();
                    if (LogMessages) print("UDPReceiveAsync : " + reply);
                    _IAsyncResultList.Remove(result);
                    OnBytesRecieved?.Invoke(bytesReceived);
                    OnDataRecievedAsString?.Invoke(reply);
                    Array.Clear(bytesReceived, 0, BytesBufferLength);
                    if (AutoRecieve) UDPReceiveAsync();
                }
            } catch(EmptyPacketException e)
            {
                printError($"At {ProtocolType} : " + e.ToString());
                AutoRecieve = false;
            } catch (Exception e)
            {
                printError(e.ToString());
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

        public IAsyncResult TCPSendAsync(byte[] bytes)
        {
            var ar = Socket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, out SendErrorCode, AfterTCPSend, null);

            _IAsyncResultList.Add(ar);
            
            return ar;
        }

        public IAsyncResult TCPSendAsync(string str = null)
        {
            return TCPSendAsync(UTF8FromString(str));
        }

        void AfterTCPSend(IAsyncResult result)
        {
            try
            {
                Socket.EndSend(result, out SendErrorCode);
                _IAsyncResultList.Remove(result);
            }
            catch (Exception e)
            {
                printError(SendErrorCode.ToString() + " : " + e.ToString());
            }
        }

        public string TCPReceive()
        {
            receivedBytesLength = Socket.Receive(bytesReceived, 0, BytesBufferLength, SocketFlags.None, out ReceiveErrorCode);
            string Response = string.Empty;
            if (ReceiveErrorCode != SocketError.Success)
            {
                Debug.LogFormat(ReceiveErrorCode.ToString());
            } else if (receivedBytesLength > 0)
            {
                Response = StringFromUTF8(bytesReceived);
                if (LogMessages) print("TCPRecieve : " + Response);
            }
            Array.Clear(bytesReceived, 0 , BytesBufferLength);

            return Response;
        }

        public IAsyncResult TCPReceiveAsync(int length = 0)
        {
            if (length > 0)
                BytesBufferLength = length;

            var ar = Socket.BeginReceive(bytesReceived, 0, BytesBufferLength, SocketFlags.None, out ReceiveErrorCode, AfterTCPReceive, null);

            _IAsyncResultList.Add(ar);
            
            return ar;
        }

        void AfterTCPReceive(IAsyncResult result)
        {
            try
            {
                Socket.EndReceive(result, out ReceiveErrorCode);
                string reply = StringFromUTF8(bytesReceived);
                if (reply == string.Empty) throw new EmptyPacketException();
                if (LogMessages) print("TCPReceiveAsync : " + _IPEndPoint + "\n" + reply);
                _IAsyncResultList.Remove(result);
                OnBytesRecieved?.Invoke(bytesReceived);
                OnDataRecievedAsString?.Invoke(reply);                
                Array.Clear(bytesReceived, 0, BytesBufferLength);
                if (AutoRecieve) TCPReceiveAsync();
            } catch (EmptyPacketException e)
            {
                printError($"At {ProtocolType} : " + e.ToString());
                AutoRecieve = false;
            } catch (Exception e)
            {
                printError(ReceiveErrorCode.ToString() + $" of {ProtocolType} : " + e.ToString());
            }
        }

        byte[] UTF8FromString(string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        string StringFromUTF8(byte[] bytes)
        {
            string fullString = Encoding.UTF8.GetString(bytes, 0, bytes.Length);
            int indexOfNullChar = fullString.IndexOf('\0');
            if (indexOfNullChar >= 0)
            {
                fullString = fullString.Substring(0, indexOfNullChar);
            }
            return fullString;
        }

        void PrintByteArray(byte[] byteArr)
        {
            for (int i = 0; i < byteArr.Length; i++)
            {
                print("Index : " + i + ", Content : " + byteArr[i]);
            }
        }

        public void Dispose()
        {
            OnDisconnected?.Invoke();

            AutoRecieve = false;
            foreach(IAsyncResult ar in _IAsyncResultList)
            {
                ar.AsyncWaitHandle.Dispose();
            }
            _IAsyncResultList.Clear();
            UnConnect();
            Socket.Dispose();
        }
    }
}
