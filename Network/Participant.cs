using System;
using System.Net.Sockets;
using System.Text;

namespace ChatServerApp.Network
{
    // Класс для представления одного участника (клиент или модератор)
    public class Participant
    {
        public string Id { get; private set; } // GUID
        public string Username { get; private set; }
        public TcpClient TcpClient { get; private set; }

        public Participant(string id, string username, TcpClient tcpClient)
        {
            Id = id;
            Username = username;
            TcpClient = tcpClient;
        }

        public void SendMessage(string message)
        {
            try
            {
                if (TcpClient.Connected)
                {
                    NetworkStream stream = TcpClient.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    stream.Write(data, 0, data.Length);
                    Console.WriteLine($"[Server] Sent to {Username} ({Id}): {message}");
                }
                else
                {
                    Console.WriteLine($"[Server] Cannot send message, {Username} ({Id}) is disconnected.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error sending message to {Username} ({Id}): {ex.Message}");
            }
        }
    }
}
