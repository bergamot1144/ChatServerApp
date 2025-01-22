using System;

namespace ChatServerApp.Network
{
    // Класс для представления комнаты чата
    public class ChatRoom
    {
        public string RoomId { get; private set; }
        public Participant Moderator { get; private set; }
        public Participant Client { get; private set; }

        public ChatRoom(string roomId, Participant client)
        {
            RoomId = roomId;
            Client = client;
            Moderator = null; // Initially no moderator
        }

        public void SetModerator(Participant moderator)
        {
            if (Moderator == null)
            {
                Moderator = moderator;
                Console.WriteLine($"[Server] Moderator {Moderator.Username} ({Moderator.Id}) set for room {RoomId}.");
            }
            else
            {
                Console.WriteLine($"[Server] Room {RoomId} already has a moderator.");
            }
        }

        public bool HasParticipant(string participantId)
        {
            return (Moderator != null && Moderator.Id == participantId) ||
                   (Client != null && Client.Id == participantId);
        }

        public void BroadcastMessage(string message, string senderId)
        {
            Console.WriteLine($"[Server] Message from {senderId} in room {RoomId}: {message}");

            if (Moderator != null && Moderator.Id != senderId)
            {
                Moderator.SendMessage(message);
            }

            if (Client != null && Client.Id != senderId)
            {
                Client.SendMessage(message);
            }
        }

        public void SendMessageToParticipant(string participantId, string message)
        {
            if (Moderator != null && Moderator.Id == participantId)
            {
                Moderator.SendMessage(message);
            }
            else if (Client != null && Client.Id == participantId)
            {
                Client.SendMessage(message);
            }
            else
            {
                Console.WriteLine($"[Server] Participant {participantId} not found in room {RoomId}.");
            }
        }

        public void NotifyModeratorAboutNewClient()
        {
            if (Moderator != null)
            {
                string notification = $"Client {Client.Username} has joined the room {RoomId}.";
                Moderator.SendMessage(notification);
                Console.WriteLine($"[Server] Notified Moderator {Moderator.Username} about new client {Client.Username}.");
            }
        }

        public void NotifyModeratorButtonState(string buttonState)
        {
            if (Moderator != null)
            {
                string notification = $"BUTTON_STATE:{buttonState}";
                Moderator.SendMessage(notification);
                Console.WriteLine($"[Server] Notified Moderator {Moderator.Username} about button state: {buttonState}.");
            }
        }
    }
}
