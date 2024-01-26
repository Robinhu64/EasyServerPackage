using security;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Collections.Concurrent;
using ApplicationLayer;

namespace TransportLayer
{
    public class TransportLayerServer
    {
        //Networking
        static private TcpListener server;
        static public int maxMessages;
        public static  List<ClientStruct> ClientList = new List<ClientStruct>();
        public static List<ClientStruct> UDPClientList = new List<ClientStruct>();
        public static List<SecurityCommen> SecureClientList = new List<SecurityCommen>();

        //Queues
        static Queue<MessageStruct> MessageQueueInput = new Queue<MessageStruct>();
        public static ConcurrentQueue<MessageStruct> MessageQueue = new ConcurrentQueue<MessageStruct>();

        private int BroadcastPort;
        private int ConnectionPort;
        /// <summary>
        /// Init function of the transport layer
        /// </summary>
        /// <param name="BroadCastPort">The port the server will broadcast to clients. Default:7000</param>
        /// <param name="BroadCastReplyPort">The port that the server will listen for replys on broadcast: Default:8006</param>
        /// <param name="ConnectionRequestPort">The port the client will send for connection requests.  Default:8005</param>
        /// <param name="MessagagingPort">The port that will be utilized when the setup is done for communication. Default:8001</param>
        /// <returns>Void</returns>
        public void init(int BroadCastPort,int BroadCastReplyPort, int ConnectionRequestPort, int MessagagingPort, bool enableAutoRemove)
        {
            BroadcastPort = BroadCastPort;
            ConnectionPort = ConnectionRequestPort;
            Thread TcpServerThread = new Thread(() => TcpServer(MessagagingPort));
            Thread BroadcastListenerThread = new Thread(() => BroadCast(BroadCastReplyPort));//default 8006
            Thread DiscoverThread = new Thread(() => Discovery(10, 500));
            BroadcastListenerThread.Start();
            DiscoverThread.Start();
            Thread.Sleep(200);
            TcpServerThread.Start();
            Debug.Log("Discovery mode started");
            if(enableAutoRemove) 
            {
                Thread Messagescanner = new Thread(() => MessageScanner());
                Messagescanner.Start();
            }
        }
        /// <summary>
        /// Starts up the listener and send on connection setup from client 
        /// </summary>
        /// <param name="Port">The instance that represents the port number the connection is setup on. Default:8001</param>
        /// <returns>Void</returns>
        private void TcpServer(int Port)
        {
            server = new TcpListener(IPAddress.Any, Port); //Default port 8001
            server.Start();
            while (true)
            {
                TcpClient newClient = server.AcceptTcpClient();
                Thread j = new Thread(new ParameterizedThreadStart(HandleClientSend));
                j.Start(newClient);
            }
        }
        /// <summary>
        /// Starts up the listener and send on connection setup from client 
        /// </summary>
        /// <param name="address">The IP-address of the client that the connection request needs to be sent to.</param>
        /// <param name="name">Name of the client assigned with the IP-address.</param>
        /// <param name="key">The AES-key for the commumnication</param>
        /// <returns>True if succesfull, False if unsuccesfull</returns>
        public bool SendTCP(IPAddress address, byte[] key, string name)
        {
            TcpClient sendConnection = new TcpClient(address.ToString(), ConnectionPort);//Default 8005
            Stream mystream = sendConnection.GetStream();
            SecurityFunctions.SecureSend("connect to me", mystream, key);
            string confirm = SecurityFunctions.SecureReceive(mystream, key);

            if (confirm == "ok")
            {
                SecureClientList.Add(new SecurityCommen((IPEndPoint)sendConnection.Client.RemoteEndPoint, name, key));
                sendConnection.Close();
                mystream.Close();
                return true;
            }
            else
            {
                sendConnection.Close();
                mystream.Close();
                return false;
            }
        }
        /// <summary>
        /// Send out broadcasting to everyne using UDP 
        /// </summary>
        /// <param name="input">The content of the broadcast.</param>
        /// <param name="Port">The instance that represents the port number for the broadcast. Default: 7000</param>
        /// <returns>Void</returns>
        private void SendBroadCast(string input,int Port)
        {
            UdpClient client = new UdpClient();
            IPEndPoint ip = new IPEndPoint(IPAddress.Broadcast, Port);//Default 7000
            byte[] bytes = Encoding.ASCII.GetBytes(input);
            client.Send(bytes, bytes.Length, ip);
            client.Close();
        }
        /// <summary>
        /// Listens for incoming replies from broadcasts
        /// </summary>
        /// <param name="Port">The instance that represents the port number for the broadcast. Default: 8006</param>
        /// <returns>Void</returns>
        private void BroadCast(int Port)
        {
            TcpListener server = new TcpListener(IPAddress.Any, Port);//Default 8006
            server.Start();
            while (true)
            {
                try
                {
                    TcpClient newClient = server.AcceptTcpClient();
                    StreamReader sReader = new StreamReader(newClient.GetStream(), Encoding.ASCII);
                    string ClientName = sReader.ReadLine();
                    addToListTemp(new ClientStruct(newClient, ClientName));
                    Debug.Log("Someone Connected");
                }
                catch (SocketException e)
                {
                    Debug.Log("SocketException: " + e.Message);
                }
            }

        }
        /// <summary>
        /// Handler for sending from client
        /// </summary>
        /// <param name="obj">TcpClient that you want to send to.</param>
        /// <returns>Void</returns>
        private void HandleClientSend(object obj) 
        {
            TcpClient newClient = (TcpClient)obj;
            Stream stream = newClient.GetStream();

            // Create a cancellation token source
            var cts = new CancellationTokenSource();
            // Create a cancellation token from the source
            var token = cts.Token;
            Thread t = new Thread(() => HandleClientListen(newClient,token));

            try
            {
                string ClientName = GetNameByIP(newClient);
                t.Start();
                MessageStruct item;
                while (true)
                {
                    bool isSuccesful = MessageQueue.TryPeek(out item);
                    if (isSuccesful && (item.Destination == ClientName))
                    {
                        Debug.Log("check before");
                        bool isSuccesful2 = MessageQueue.TryDequeue(out item);
                        if (isSuccesful2)
                        {
                            try
                            {
                                Debug.Log("check here +"+item.Message+ " destination "+item.Destination);
                                SecurityFunctions.SecureSend(item.Message, stream, GetKeyByIP(newClient));
                                ApplicationLayerServer.TypeContainer checkcontainer = JsonUtility.FromJson<ApplicationLayerServer.TypeContainer>(item.Message); //For Done handling
                                if (checkcontainer.JsonData == "Done")
                                {
                                    removeFromList(newClient);
                                    cts.Cancel();

                                }

                            }
                            catch (ObjectDisposedException e)
                            {
                                MessageQueue.Enqueue(item);
                            }
                        }
                    }

                    Thread.Sleep(500);
                }
            }
            catch (Exception e)
            {
                Debug.Log("In catch");
                Debug.Log(e.Message);
                t.Abort();
            }

        }
        /// <summary>
        /// Handler for receiving from client
        /// </summary>
        /// <param name="newClient">TcpClient that you want to receive from.</param>
        /// <param name="token">The cancelation token that can be revoked</param>
        /// <returns>Void</returns>
        private void HandleClientListen(TcpClient newClient, CancellationToken token) 
        {
            StreamReader sReader = new StreamReader(newClient.GetStream(), Encoding.ASCII);
            IPEndPoint IPEnd = (IPEndPoint)newClient.Client.RemoteEndPoint;
            NetworkStream stream = newClient.GetStream();
            bool isRunning = true;
            try
            {
                while (isRunning)
                {
                    Debug.Log("Waitin for message");
                    Debug.Log("key of client: " + Convert.ToBase64String(GetKeyByIP(newClient)));
                    while (!stream.DataAvailable)
                    {
                        token.ThrowIfCancellationRequested();
                    }
                    string Message = SecurityFunctions.SecureReceive(stream, GetKeyByIP(newClient));
                    Debug.Log("Message: " + Message);
                    if (Message == null)
                    {
                        removeFromList(newClient);
                        sReader.Close();
                        stream.Close();
                        break;
                    }
                    Debug.Log("\n" + IPEnd.Address + ": " + Message + "\n");
                    if (Message == "Done")
                    {
                        isRunning = false;
                        removeFromList(newClient);
                        sReader.Close();
                        stream.Close();
                    }
                    else
                    {
                        ApplicationLayerServer.TypeContainer receivedContainer = JsonUtility.FromJson<ApplicationLayerServer.TypeContainer>(Message);
                        ApplicationLayerServer.AddObjectUnique(receivedContainer, newClient);
                    }
                }

            }
            catch (IOException e)
            {
                Debug.Log(e.ToString());
                removeFromListUsingIP(IPEnd.Address);
                sReader.Close();
            }
            catch (OperationCanceledException)
            {
                Debug.Log("Cancellation token thrown");
                sReader.Close();
                stream.Close();
            }

        }
        /// <summary>
        /// Send out broadcasting to everyne using UDP 
        /// </summary>
        /// <param name="broadcastamount">How many times a broadcast need to be sent.</param>
        /// <param name="sleepinMiliseconds">How much time between broadcast needs to be between eachother, in miliseconds</param>
        /// <returns>Void</returns>
        public void Discovery(int broadcastamount, int sleepinMiliseconds)
        {
            for (int k = 0; k < broadcastamount; k++)
            {
                SendBroadCast("Discovery", BroadcastPort);
                Thread.Sleep(sleepinMiliseconds);
            }
        }
        /// <summary>
        /// Clears messages automaticlly out of the queue when the client isn't connected anymore
        /// </summary>
        /// <returns>Void</returns>
        private void MessageScanner() //Transport Layer not currently used + needs to be reworked for objects instead of messages
        {
            while (true)
            {
                MessageStruct item;
                bool inList = false;
                bool isSuccesful = MessageQueue.TryPeek(out item);
                if (isSuccesful)
                {
                    foreach (var i in SecureClientList)
                    {
                        if (i.Name == item.Destination)
                        {
                            inList = true;
                        }
                    }
                }
                if (!inList)
                {
                    bool isSuccesful2 = MessageQueue.TryDequeue(out item);
                    if (isSuccesful2)
                    {
                        Debug.Log(item.Destination + " - " + item.Message + "Removed-------------");
                    }
                }

                Thread.Sleep(200);
            }

        }


        //functions for listhandling
        private void addToListTemp(ClientStruct input) //Transport Layer
        {
            IPEndPoint ipep = (IPEndPoint)input.IP;
            bool unique = true;
            foreach (ClientStruct a in UDPClientList)
            {
                if (a.IP.Address.ToString() == ipep.Address.ToString())
                {
                    unique = false;
                }
            }
            if (unique == true)
            {
                UDPClientList.Add(input);
            }
        }
        private void removeFromList(TcpClient input) //Transport Layer
        {
            IPEndPoint ipep = (IPEndPoint)input.Client.RemoteEndPoint;
            bool remove = false;
            int pos = 0;
            for (int i = 0; i < SecureClientList.Count; i++)
            {
                if (SecureClientList[i].Ip.Address.ToString() == ipep.Address.ToString())
                {
                    remove = true;
                    pos = i;
                }
            }
            if (remove == true)
            {
                SecureClientList.RemoveAt(pos);
            }
        }
        private static void removeFromListUsingIP(IPAddress input) //Transport Layer
        {
            bool remove = false;
            int pos = 0;
            for (int i = 0; i < SecureClientList.Count; i++)
            {
                if (SecureClientList[i].Ip.Address.ToString() == input.ToString())
                {
                    remove = true;
                    pos = i;
                }
            }
            if (remove == true)
            {
                SecureClientList.RemoveAt(pos);
            }
        }
        private byte[] GetKeyByIP(TcpClient tcpClient) //Transport Layer
        {
            IPEndPoint ipendpoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
            IPAddress ip = ipendpoint.Address;
            for (int i = 0; i < SecureClientList.Count; i++)
            {
                if (SecureClientList[i].Ip.Address.ToString() == ip.ToString())
                {
                    Debug.Log("you are the best");
                    return SecureClientList[i].Key;
                }
            }
            return null;
        }
        private string GetNameByIP(TcpClient tcpClient) //Transport Layer
        {
            IPEndPoint ipendpoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint;
            IPAddress ip = ipendpoint.Address;
            for (int i = 0; i < SecureClientList.Count; i++)
            {
                if (SecureClientList[i].Ip.Address.ToString() == ip.ToString())
                {
                    return SecureClientList[i].Name;
                }
            }
            return null;
        }
        public static void RemoveClient(string Destination)
        {
            ApplicationLayerServer.TypeContainer container = new ApplicationLayerServer.TypeContainer
            {
                TypeName = "ControlMessage",
                JsonData = "Done"
            };
            string DoneMessage = JsonUtility.ToJson(container);
            MessageQueue.Enqueue(new MessageStruct(Destination, DoneMessage));
        }
        public static IPAddress GetIPByName(string name) //Transport Layer
        {
            IPAddress ip = IPAddress.Parse("0");
            foreach (var item in UDPClientList)
            {
                if (item.Name == name)
                {
                    ip = item.IP.Address;
                }
            }
            return ip;
        }

        public struct MessageStruct
        {
            public string Destination;
            public string Message;
            public MessageStruct(string destination, string message)
            {
                Destination = destination;
                Message = message;
            }
        }
        public struct ClientStruct
        {
            public IPEndPoint IP;
            public string Name;
            public ClientStruct(TcpClient x, string y)
            {
                IPEndPoint temp = (IPEndPoint)x.Client.RemoteEndPoint;
                IP = temp;
                if (y == null)
                {
                    Name = "";
                }
                else
                {
                    Name = y;
                }
            }
        }
    }
}

