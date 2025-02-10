//using System;
//using ChatServerApp.Network;

//namespace ChatServerApp.Network
//{
//    /// <summary>
//    /// Класс, представляющий комнату чата.
//    /// В комнате может находиться один модератор и один клиент.
//    /// </summary>
//    public class ChatRoom
//    {
//        public string RoomId { get; private set; }
//        public Participant Moderator { get; private set; }
//        public Participant Client { get; private set; }

//        public ChatRoom(string roomId)
//        {
//            RoomId = roomId;
//        }

//        /// <summary>
//        /// Проверка, присутствует ли участник с данным ID в комнате.
//        /// </summary>
//        public bool HasParticipant(string participantId)
//        {
//            return (Moderator != null && Moderator.Id == participantId) ||
//                   (Client != null && Client.Id == participantId);
//        }

//        /// <summary>
//        /// Добавление участника в комнату.
//        /// Если роль участника – Moderator или Client, проверяется наличие.
//        /// </summary>
//        public void AddParticipant(Participant participant)
//        {
//            if (participant.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
//            {
//                if (Moderator == null)
//                {
//                    Moderator = participant;
//                }
//                else
//                {
//                    throw new InvalidOperationException("Moderator уже присутствует в этой комнате.");
//                }
//            }
//            else if (participant.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
//            {
//                if (Client == null)
//                {
//                    Client = participant;
//                }
//                else
//                {
//                    throw new InvalidOperationException("Client уже присутствует в этой комнате.");
//                }
//            }
//        }

//        /// <summary>
//        /// Удаление участника из комнаты.
//        /// </summary>
//        public void RemoveParticipant(string participantId)
//        {
//            if (Moderator != null && Moderator.Id == participantId)
//            {
//                Moderator = null;
//            }
//            else if (Client != null && Client.Id == participantId)
//            {
//                Client = null;
//            }
//        }

//        /// <summary>
//        /// Отправляет сообщение всем участникам комнаты.
//        /// Форматируется с учётом имени отправителя.
//        /// </summary>
//        public void SendMessageToAll(string message, string senderId)
//        {
//            Console.WriteLine($"[Server] Message from {senderId} in room {RoomId}: {message}");

//            string senderName = GetSenderName(senderId);
//            string formattedMessage = $"[{senderName}]: {message}\n";

//            if (Moderator != null)
//            {
//                Moderator.SendMessage(formattedMessage);
//                Console.WriteLine($"[Server] Sent to Moderator {Moderator.Username}: {formattedMessage.Trim()}");
//            }

//            if (Client != null)
//            {
//                Client.SendMessage(formattedMessage);
//                Console.WriteLine($"[Server] Sent to Client {Client.Username}: {formattedMessage.Trim()}");
//            }
//        }

//        /// <summary>
//        /// Получение имени отправителя по его ID.
//        /// </summary>
//        private string GetSenderName(string senderId)
//        {
//            if (Moderator != null && Moderator.Id == senderId)
//            {
//                return Moderator.Username;
//            }
//            else if (Client != null && Client.Id == senderId)
//            {
//                return Client.Username;
//            }
//            else
//            {
//                return "Unknown";
//            }
//        }
//    }
//}
using System;

namespace ChatServerApp.Network
{
    /// <summary>
    /// Класс комнаты чата.
    /// </summary>
    public class ChatRoom
    {
        public string RoomId { get; private set; }
        public Participant Client { get; private set; }
        public Participant Moderator { get; private set; }

        public ChatRoom(string roomId)
        {
            RoomId = roomId;
        }

        public void AddParticipant(Participant participant)
        {
            if (participant.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
            {
                if (Client == null)
                {
                    Client = participant;
                }
                else
                {
                    throw new InvalidOperationException("Client already exists in this room.");
                }
            }
            else if (participant.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
            {
                if (Moderator == null)
                {
                    Moderator = participant;
                }
                else
                {
                    throw new InvalidOperationException("Moderator already exists in this room.");
                }
            }
        }

        public void RemoveParticipant(string participantId)
        {
            if (Client != null && Client.Id == participantId)
                Client = null;
            if (Moderator != null && Moderator.Id == participantId)
                Moderator = null;
        }
    }
}
