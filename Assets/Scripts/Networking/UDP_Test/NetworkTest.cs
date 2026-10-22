using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetworkTest : MonoBehaviour
{
    [Header("Network Settings")]
    public int udpPort = 8888;
    public int tcpPort = 9999;
    public float broadcastInterval = 5f;

    private UdpClient udpClient;
    private TcpListener tcpListener;

    private Thread udpBroadcastThread;
    private Thread tcpListenerThread;
    private bool isRunning;

    void Start()
    {
        isRunning = true;

        // Start UDP broadcast thread
        udpBroadcastThread = new Thread(UDPBroadcastLoop);
        udpBroadcastThread.IsBackground = true;
        udpBroadcastThread.Start();

        // Start TCP listener thread
        tcpListenerThread = new Thread(ListenForIMUStream);
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
            Debug.LogError($"UDP Broadcast Error: {ex}");
        }
    }

    private void ListenForIMUStream()
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

                using (TcpClient client = tcpListener.AcceptTcpClient())
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    Debug.Log("Arduino connected. Receiving IMU data...");

                    while (client.Connected && isRunning)
                    {
                        string line = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] values = line.Split(',');

                        if (values.Length == 6 &&
                            float.TryParse(values[0], out float ax) &&
                            float.TryParse(values[1], out float ay) &&
                            float.TryParse(values[2], out float az) &&
                            float.TryParse(values[3], out float gx) &&
                            float.TryParse(values[4], out float gy) &&
                            float.TryParse(values[5], out float gz))
                        {
                            Debug.Log($"Accel: ({ax}, {ay}, {az})  Gyro: ({gx}, {gy}, {gz})");
                        }
                        else
                        {
                            Debug.LogWarning($"Malformed data: {line}");
                        }
                    }

                    Debug.Log("Arduino disconnected.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"TCP Listener Error: {ex}");
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

    void OnApplicationQuit()
    {
        isRunning = false;

        udpClient?.Close();
        tcpListener?.Stop();

        udpBroadcastThread?.Join();
        tcpListenerThread?.Join();
    }
}