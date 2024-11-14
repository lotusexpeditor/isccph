using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class ALISServer
{
    private const int RegisterCount = 10;
    private const int RegisterSize = 8; // Maximum 64 bits
    private ulong[] registers;
    private bool running;
    
    public ALISServer()
    {
        registers = new ulong[RegisterCount];
        running = false;
    }

    public void Start(int port)
    {
        using (UdpClient udpServer = new UdpClient(port))
        {
            Console.WriteLine($"Server started on port {port}...");

            while (true)
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, port);
                byte[] receivedBytes = udpServer.Receive(ref remoteEP);
                HandleRequest(receivedBytes, remoteEP, udpServer);
            }
        }
    }

    private void HandleRequest(byte[] receivedBytes, IPEndPoint remoteEP, UdpClient udpServer)
    {
        byte command = receivedBytes[2];
        ushort sessionId = BitConverter.ToUInt16(receivedBytes, 0);
        uint packetunixTimestamp = BitConverter.ToUInt32(receivedBytes, receivedBytes.Length-4);


        byte[] response = null;

        int unixTimestamp = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        if(unixTimestamp!=packetunixTimestamp){
            Console.WriteLine("Dropping command because of timeout or time sync failure: "+command);
            command=0xFF;
        }

        switch (command)
        {
            case 0x01:
                Console.WriteLine("[!]New connection: "+sessionId);
                response = CreateResponse(sessionId, 0x01);
                break;
            case 0x02:
                response = ReadRegistersResponse(sessionId);
                Console.WriteLine("[+]Register read from session: "+sessionId);
                break;
            case 0x03:
                WriteRegisters(receivedBytes, sessionId);
                response = CreateResponse(sessionId, 0x03);
                Console.WriteLine("[!]Register write from session: "+sessionId);
                break;
            case 0x04:
                response = GetStatusResponse(sessionId);
                Console.WriteLine("[+]Status sent to: "+sessionId);
                break;
            case 0x05:
                running = !running; // Toggle running state
                response = CreateResponse(sessionId, running ? 0x05 : 0xFA);
                Console.WriteLine("[!]Run status changed to: "+(running ? "Started" : "Stopped") + " by sessionId: "+sessionId);
                break;
            default:
                response = CreateResponse(sessionId, 0xFF);
                Console.WriteLine("[?] Unknown command "+command+" from: "+sessionId);
                break;
        }

        if (response != null)
        {
            udpServer.Send(response, response.Length, remoteEP);
        }
    }

    private byte[] ReadRegistersResponse(ushort sessionId)
    {
        var response = new List<byte>();
        response.AddRange(BitConverter.GetBytes(sessionId));
        for (int i = 0; i < RegisterCount; i++)
        {
            response.AddRange(BitConverter.GetBytes(registers[i]));
        }
        return response.ToArray();
    }

    private void WriteRegisters(byte[] receivedBytes, ushort sessionId)
    {
        for(int i=3;i<13;i++){
            if(receivedBytes[i]<10){
                Console.WriteLine("[>]Writing to register "+receivedBytes[i]);
                ulong value = BitConverter.ToUInt64(receivedBytes, 13+8*(i-3));
                registers[receivedBytes[i]] = value;
            }
        }
    }

    private byte[] GetStatusResponse(ushort sessionId)
    {
        string statusMessage = $"Version: 1.0, Running: {running}";
        var response = new List<byte>();
        response.AddRange(BitConverter.GetBytes(sessionId));
        response.AddRange(Encoding.UTF8.GetBytes(statusMessage));
        return response.ToArray();
    }

    private byte[] CreateResponse(ushort sessionId, int message)
    {
        var response = new List<byte>();
        response.AddRange(BitConverter.GetBytes(sessionId));
        response.AddRange(new byte[]{(byte)message});
        return response.ToArray();
    }

    public static void Main(string[] args)
    {
        var server = new ALISServer();
        Console.WriteLine("[+] Initial run status is: "+(server.running ? "Started" : "Stopped"));
        server.Start(12345); // Start server on port 12345
    }
}
