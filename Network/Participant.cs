using System;
using System.Net.Sockets;
using System.Text;

namespace ChatServerApp.Network
{
    public class Participant
    {
        public string Id { get; private set; }
        public string Username { get; private set; }
        public string Role { get; private set; }
        public TcpClient TcpClient { get; private set; }

        private readonly object _sendLock = new object();

        public Participant(string id, string username, string role, TcpClient tcpClient)
        {
            Id = id;
            Username = username;
            Role = role;
            TcpClient = tcpClient;
        }

        /// <summary>
        /// Отправляет сообщение участнику. Если сообщение не заканчивается на перевод строки, добавляет его.
        /// </summary>
        public void SendMessage(string message)
        {
            try
            {
                if (TcpClient.Connected)
                {
                    if (!message.EndsWith("\n"))
                    {
                        message += "\n";
                    }
                    NetworkStream stream = TcpClient.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    lock (_sendLock)
                    {
                        stream.Write(data, 0, data.Length);
                    }
                    Console.WriteLine($"[Server] Sent to {Username} ({Id}): '{message.Replace("\n", "\\n")}'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Server] Error sending message to {Username} ({Id}): {ex.Message}");
            }
        }

    }
}
