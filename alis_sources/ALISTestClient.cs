using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

public class ALISTestClient
{
    private const int RegisterCount = 10;
    private const int RegisterSize = 8; // Maximum 64 bits
    private const ushort CRC16Polynomial = 0xA001;
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;
    private ushort sessionId;

    public ALISTestClient()
    {
        udpClient = new UdpClient();
    }

    public bool NewConnection(string hostname, int port)
    {
        Console.WriteLine("Creating NewConnection...");
        try
        {
            remoteEndPoint = new IPEndPoint(IPAddress.Parse(hostname), port);
            sessionId = (ushort)new Random().Next(1, ushort.MaxValue); // Random session ID
            SendMessage(CreateMessage(new byte[]{0x01}, BitConverter.GetBytes(sessionId)));
            ReceiveMessage();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
            return false;
        }
    }

    public List<ulong> ReadRegisters()
    {
        Console.WriteLine("Sending ReadRegisters...");
        SendMessage(CreateMessage(new byte[]{0x02}, BitConverter.GetBytes(sessionId)));
        return ReceiveRegisters();
    }

    public void WriteRegisters(int[] indexes, ulong[] values)
    {
        Console.WriteLine("Sending WriteRegisters...");
        if (indexes.Length != values.Length)
            throw new ArgumentException("Indexes and values length must match.");

        var message = new List<byte>();

        foreach (var index in indexes)
        {
            message.AddRange(new byte[]{(byte)index});
        }
        for(int i=0; i<10-indexes.Length;i++){
            message.AddRange(new byte[]{0xff});
        }
        foreach (var value in values)
        {
            Console.WriteLine(value);
            message.AddRange(BitConverter.GetBytes(value));
        }

        message=CreateMessage(BitConverter.GetBytes(sessionId), new byte[]{0x03}, message);
        SendMessage(message);
        ReceiveMessage();
    }

    public string GetStatus()
    {
        Console.WriteLine("Sending GetStatus...");
        SendMessage(CreateMessage(new byte[]{0x04}, BitConverter.GetBytes(sessionId)));
        return ReceiveStatus();
    }

    public void StopStart()
    {
        Console.WriteLine("Sending StopStart...");
        SendMessage(CreateMessage(new byte[]{0x05}, BitConverter.GetBytes(sessionId)));
        Console.WriteLine("StopStart toggled.");
    }

    private List<ulong> ReceiveRegisters()
    {
        var response = ReceiveMessage();

        List<ulong> registers = new List<ulong>();
        for (int i = 0; i < RegisterCount; i++)
        {
            ulong registerValue = BitConverter.ToUInt64(response, i * RegisterSize + 2);
            registers.Add(registerValue);
        }
        return registers;
    }

    private string ReceiveStatus()
    {
        var response = ReceiveMessage();
        return Encoding.UTF8.GetString(response);
    }

    private List<byte> CreateMessage(byte[] payload, byte[] command, List<byte> payload2)
    {
        var message = new List<byte>();
        message.AddRange(payload);
        message.AddRange(command);
        message.AddRange(payload2);

        ushort crc = CalculateCRC(message.ToArray());
        message.AddRange(BitConverter.GetBytes(crc));
        int unixTimestamp = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        byte[] timestampBytes = BitConverter.GetBytes(unixTimestamp);
        message.AddRange(timestampBytes);
        return message;
    }

    private List<byte> CreateMessage(byte[] command, byte[] payload)
    {
        var message = new List<byte>();
        message.AddRange(payload);
        message.AddRange(command);
        ushort crc = CalculateCRC(message.ToArray());
        message.AddRange(BitConverter.GetBytes(crc));
        int unixTimestamp = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        byte[] timestampBytes = BitConverter.GetBytes(unixTimestamp);
        message.AddRange(timestampBytes);
        return message;
    }

    private void SendMessage(List<byte> message)
    {
        udpClient.Send(message.ToArray(), message.Count, remoteEndPoint);
    }

    private byte[] ReceiveMessage()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        byte[] buffer = udpClient.Receive(ref remoteEP);

        byte[] message=new byte[buffer.Length -2];
        for(int i=2; i<buffer.Length; i++){//-2 len on chksm
            message[i-2]=buffer[i];
        }
        return buffer;
    }

    private ushort CalculateCRC(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc = (ushort)((crc >> 1) ^ CRC16Polynomial);
                }
                else
                {
                    crc = (ushort)(crc >> 1);
                }
            }
        }
        return crc;
    }

    public void Close()
    {
        udpClient.Close();
        Console.WriteLine("Connection closed.");
    }
}



public class Program
{
    public static void Main(string[] args)
    {
        var client = new ALISTestClient();

        if (client.NewConnection("127.0.0.1", 12345))
        {
            Console.WriteLine("Connected successfully.");
        }
        else
        {
            Console.WriteLine("Failed to connect.");
            return;
        }

        var registers = client.ReadRegisters();
        Console.WriteLine("Registers: " + string.Join(", ", registers));

        int[] indexesToWrite = { 3, 7, 3 };
        ulong[] valuesToWrite = { 0xFFFFFFFFFFFFFAF, 0x1234567890ABCDEF, 0x0BADCAFEFEEDC0DE };
        client.WriteRegisters(indexesToWrite, valuesToWrite);

        registers = client.ReadRegisters();
        Console.WriteLine("Updated Registers: " + string.Join(", ", registers));

        string status = client.GetStatus();
        Console.WriteLine("Status: " + status);

        client.StopStart();

        // Clean up
        client.Close();
    }
}
