using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using UnityEngine;
using TransportLayer;


namespace ApplicationLayer
{
    public class ApplicationLayerServer
    {
        public static List<ObjectMessage> ObjectList = new List<ObjectMessage>();
        /// <summary>
        /// Sending an object to the receipient.
        /// </summary>
        /// <param name="T">The class of the object that needs to be transmitted.</param>
        /// <param name="destination">The name of the client that needs to receive the object</param>
        /// <returns>Void</returns>
        public void ObjectSend<T>(T data, string destination)
        {
            TypeContainer container = new TypeContainer
            {
                TypeName = data.GetType().FullName,
                JsonData = JsonUtility.ToJson(data)
            };
            string typeIdentifierJson = JsonUtility.ToJson(container);
            TransportLayerServer.MessageQueue.Enqueue(new TransportLayerServer.MessageStruct(destination, typeIdentifierJson));
        }
        /// <summary>
        /// Receiving an object from the sender.
        /// </summary>
        /// <param name="T">The class of the object that needs to be received.</param>
        /// <returns>The object of the class and the IP-address of the sender of the object, they will be both null if no object from that class has been received</returns>
        public (T, IPAddress) ObjectReceive<T>()
        {
            Debug.Log("Objctlist length: " + ObjectList.Count.ToString());
            for (int i = 0; i < ObjectList.Count; i++)
            {
                TypeContainer receivedContainer = ObjectList[i].container;
                if (receivedContainer.TypeName == typeof(T).FullName)
                {
                    T testvar = JsonUtility.FromJson<T>(receivedContainer.JsonData);
                    IPAddress IP = ObjectList[i].ip;
                    ObjectList.RemoveAt(i);
                    return (testvar, IP);
                }
            }
            return (default(T), null);

        }
        /// <summary>
        /// Will add an extra object to the list, if ther already exist an object of the same type from that sender the oldest one will be removed.
        /// </summary>
        /// <param name="TypeContainer">The TypeContainer that contains the message that needs to be uniqily added. </param>
        /// <param name="TcpClient">The TcpClient of the sender of the container</param>
        /// <returns>Void</returns>
        static public void AddObjectUnique(TypeContainer Message, TcpClient newClient) 
        {
            IPEndPoint IP = (IPEndPoint)newClient.Client.RemoteEndPoint;
            for (int i = 0; i < ObjectList.Count; i++)
            {
                if ((ObjectList[i].container.TypeName.ToString() == Message.TypeName.ToString()) && (ObjectList[i].ip.ToString() == IP.Address.ToString()))
                {
                    ObjectList.RemoveAt(i);
                    break;
                }

            }
            ObjectList.Add(new ObjectMessage(IP.Address, Message));
        }
        public class ObjectMessage
        {
            public IPAddress ip;
            public TypeContainer container;

            public ObjectMessage(IPAddress ipAddress, TypeContainer typecontainer)
            {
                ip = ipAddress;
                container = typecontainer;
            }
        }
        public class TypeContainer
        {
            public string TypeName;
            public string JsonData;
        }
    }
}

