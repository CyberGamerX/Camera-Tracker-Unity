using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.tvOS;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine.UIElements;
using System.Linq;

public class TestNetworking : MonoBehaviour
{
    [Header("Network Settings")]
    public int udpPort = 8888;
    public int tcpPort = 9999;
    public float broadcastInterval = 5f;

    private UdpClient udpClient;
    private TcpListener tcpListener;
    private TcpClient connectedClient;
    private NetworkStream networkStream;
    private StreamReader reader;
    private StreamWriter writer;
    private byte[] lengthBuffer = new byte[4];

    private Thread udpBroadcastThread;
    private Thread tcpListenerThread;
    private Thread tcpCommunicationThread;
    private bool isRunning = false;

    private object sendLock = new object();
    private object connectionLock = new object();

    [Header("Incoming Data")]
    public Queue<Transformation> transformations = new Queue<Transformation>();
    public Queue<Command> commands = new Queue<Command>();
    public ulong timecode;

    public List<Transformation> visibleTansform = new List<Transformation>();

    [Header("Data Settings")]
    public ulong transformBuffer = 2000;

    public List<RawData> rawData = new List<RawData>();

    void Start()
    {
        timecode = 0;
        isRunning = true;

        // Start UDP broadcast
        udpBroadcastThread = new Thread(UDPBroadcastLoop);
        udpBroadcastThread.IsBackground = true;
        udpBroadcastThread.Start();

        // Start TCP listener
        tcpListenerThread = new Thread(ListenForTCPConnection);
        tcpListenerThread.IsBackground = true;
        tcpListenerThread.Start();
    }

    private void UDPBroadcastLoop()
    {
        try
        {
            udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, udpPort);
            string localIP = GetLocalIPAddress();

            while (isRunning)
            {
                byte[] data = Encoding.UTF8.GetBytes(localIP);
                udpClient.Send(data, data.Length, endPoint);
                Thread.Sleep((int)(broadcastInterval * 1000));
            }
        }
        catch (Exception ex)
        {
            if (isRunning)
                Debug.LogError($"UDP Broadcast Error: {ex}");
        }
    }

    private void ListenForTCPConnection()
    {
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, tcpPort);
            tcpListener.Start();
            Debug.Log($"TCP Listener started on port {tcpPort}");

            while (isRunning)
            {
                if (!tcpListener.Pending())
                {
                    Thread.Sleep(10);
                    continue;
                }

                TcpClient newClient = tcpListener.AcceptTcpClient();
                lock (connectionLock)
                {
                    connectedClient?.Close();
                    connectedClient = newClient;
                    networkStream = connectedClient.GetStream();

                    if (tcpCommunicationThread != null && tcpCommunicationThread.IsAlive)
                        tcpCommunicationThread.Join(); // wait for old thread to stop

                    tcpCommunicationThread = new Thread(HandleTCPCommunication) { IsBackground = true };
                    tcpCommunicationThread.Start();
                }
            }
        }
        catch (Exception ex)
        {
            if (isRunning)
                Debug.LogError($"TCP Listener Error: {ex}");
        }
    }

    private void HandleTCPCommunication()
    {
        try
        {
            byte[] lengthBuffer = new byte[4];

            while (isRunning && connectedClient != null && connectedClient.Connected)
            {
                if (networkStream.DataAvailable)
                {
                    // Step 1: Read message length
                    int read = networkStream.Read(lengthBuffer, 0, 4);
                    if (read < 4)
                    {
                        Debug.LogWarning("Failed to read full length header, disconnecting...");
                        break; // connection corrupted
                    }

                    int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                    if (messageLength <= 0 || messageLength > 10240) // sanity check
                    {
                        Debug.LogWarning($"Invalid message length: {messageLength}, disconnecting...");
                        break;
                    }

                    // Step 2: Read full message
                    byte[] messageBuffer = new byte[messageLength];
                    int totalRead = 0;
                    while (totalRead < messageLength)
                    {
                        int bytesRead = networkStream.Read(messageBuffer, totalRead, messageLength - totalRead);
                        if (bytesRead == 0)
                        {
                            Debug.LogWarning("Disconnected mid-message!");
                            break;
                        }
                        totalRead += bytesRead;
                    }

                    if (totalRead == messageLength)
                    {
                        string jsonLine = Encoding.UTF8.GetString(messageBuffer);

                        try
                        {
                            RawData data = JsonUtility.FromJson<RawData>(jsonLine);
                            rawData.Add(data);
                            Debug.Log($"Received and parsed data: {JsonUtility.ToJson(data)}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"JSON Parse Error: {ex}\nRaw JSON: {jsonLine}");
                        }
                    }
                }
                else
                {
                    Thread.Sleep(5); // Small delay if no data available
                }
            }
        }
        catch (Exception ex)
        {
            if (isRunning)
                Debug.LogError($"TCP Communication Error: {ex}");
        }
        finally
        {
            // Cleanup on disconnect
            lock (connectionLock)
            {
                connectedClient?.Close();
                connectedClient = null;
            }
        }
    }

    public void SendJsonToArduino(Command jsonCommand)
    {
        lock (connectionLock)
        {
            if (connectedClient != null && connectedClient.Connected && networkStream != null)
            {
                lock (sendLock)
                {
                    try
                    {
                        String jsonString = JsonUtility.ToJson(jsonCommand);
                        jsonString = jsonString + "\\n";
                        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);
                        byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

                        networkStream.Write(lengthBytes, 0, lengthBytes.Length);
                        networkStream.Write(jsonBytes, 0, jsonBytes.Length);
                        Debug.Log($"Sent JSON: {jsonString}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to send JSON: {ex}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("No connected Arduino to send JSON to.");
            }
        }
    }

    private string GetLocalIPAddress()
    {
        foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
                return ip.ToString();
        }
        return "127.0.0.1";
    }

    void OnApplicationQuit() => Cleanup();
    void OnDisable() => Cleanup();

    private void Cleanup()
    {
        if (!isRunning)
            return;

        Debug.Log("Cleaning up network connections...");
        isRunning = false;

        try { udpClient?.Close(); } catch (Exception ex) { Debug.LogWarning($"UDP close error: {ex}"); }
        try { tcpListener?.Stop(); } catch (Exception ex) { Debug.LogWarning($"TCP Listener stop error: {ex}"); }

        lock (connectionLock)
        {
            try { connectedClient?.Close(); } catch (Exception ex) { Debug.LogWarning($"TCP Client close error: {ex}"); }
        }

        try
        {
            if (udpBroadcastThread != null && udpBroadcastThread.IsAlive)
                udpBroadcastThread.Join();
            if (tcpListenerThread != null && tcpListenerThread.IsAlive)
                tcpListenerThread.Join();
            if (tcpCommunicationThread != null && tcpCommunicationThread.IsAlive)
                tcpCommunicationThread.Join();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Thread join error: {ex}");
        }
    }

    void FixedUpdate()
    {
        lock (transformations)
        {
            bool exit = false;
            while (!exit && isQueue())
            {
                Transformation oldest = transformations.Peek();
                if (oldest.timecode < timecode - transformBuffer)
                {
                    transformations.Dequeue();
                }
                else
                {
                    exit = true;
                }
            }
            visibleTansform = transformations.ToList();
        }
    }

    public Transformation getOldestTransform()
    {
        if (isQueue())
        {
            lock (transformations)
            {
                Transformation output = transformations.Peek();
                return output;
            }
        }
        else
        {
            return new Transformation();
        }
    }

    public Transformation getDelayedTransform(ulong delay)
    {
        if(delay > transformBuffer)
        {
            delay = transformBuffer - 50;
        }

        Transformation output = new Transformation();
        if (isQueue())
        {
            lock (transformations)
            {
                foreach (Transformation value in transformations)
                {
                    if (value.timecode < timecode - delay)
                    {
                        output = value;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return output;
    }

    public bool isQueue()
    {
        lock (transformations)
        {
            return transformations.Count > 0;
        }
    }
}