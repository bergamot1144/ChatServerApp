using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatServerApp.Network
{
    /// <summary>
    /// Серверная логика: аутентификация, создание комнат, хранение активных клиентов.
    /// </summary>
    public class ChatServer
    {
        private TcpListener _tcpListener;
        private ConcurrentDictionary<string, ChatRoom> _chatRooms = new ConcurrentDictionary<string, ChatRoom>();
        private ConcurrentDictionary<string, Participant> _participants = new ConcurrentDictionary<string, Participant>();
        // Коллекция активных клиентов (с ролью "Client")
        private ConcurrentDictionary<string, Participant> _activeClients = new ConcurrentDictionary<string, Participant>();
        private UserRepository _userRepository;

        public ChatServer(string ipAddress, int port, string userJsonPath)
        {
            _tcpListener = new TcpListener(IPAddress.Parse(ipAddress), port);
            _userRepository = new UserRepository(userJsonPath);
        }

        public async Task StartAsync()
        {
            _tcpListener.Start();
            Console.WriteLine($"[Server] Запущен на {_tcpListener.LocalEndpoint}");
            while (true)
            {
                TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient)
        {
            string participantId = Guid.NewGuid().ToString();
            Console.WriteLine($"[Server] Новый участник подключился: {participantId}");
            using (tcpClient)
            using (NetworkStream networkStream = tcpClient.GetStream())
            using (var reader = new System.IO.StreamReader(networkStream, Encoding.UTF8))
            using (var writer = new System.IO.StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true })
            {
                Participant participant = null;
                try
                {
                    // Ожидаем логин: "LOGIN:username:password"
                    string loginMessage = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(loginMessage) || !loginMessage.StartsWith("LOGIN:"))
                    {
                        await writer.WriteLineAsync("LOGIN_FAIL:InvalidFormat");
                        Console.WriteLine($"[Server] Некорректный формат LOGIN сообщения от {participantId}: {loginMessage}");
                        return;
                    }
                    string[] parts = loginMessage.Split(':');
                    if (parts.Length != 3)
                    {
                        await writer.WriteLineAsync("LOGIN_FAIL:InvalidFormat");
                        Console.WriteLine($"[Server] Некорректный формат LOGIN сообщения от {participantId}: {loginMessage}");
                        return;
                    }
                    string username = parts[1];
                    string password = parts[2];
                    string role = AuthenticateUser(username, password);
                    if (role == null)
                    {
                        await writer.WriteLineAsync("LOGIN_FAIL:InvalidCredentials");
                        Console.WriteLine($"[Server] Неверные учетные данные для {username}.");
                        return;
                    }
                    participant = new Participant(participantId, username, role, tcpClient);
                    _participants.TryAdd(participantId, participant);
                    await writer.WriteLineAsync($"LOGIN_OK:{role}");
                    Console.WriteLine($"[Server] Участник {username} ({participantId}) успешно аутентифицирован как {role}");

                    // Если клиент, добавляем в активные клиенты и создаём для него комнату.
                    if (role.Equals("Client", StringComparison.OrdinalIgnoreCase))
                    {
                        _activeClients.TryAdd(participantId, participant);
                        string roomId = "Room_" + Guid.NewGuid().ToString();
                        ChatRoom newRoom = new ChatRoom(roomId);
                        newRoom.AddParticipant(participant);
                        _chatRooms.TryAdd(roomId, newRoom);
                        Console.WriteLine($"[Server] Создана новая комната {roomId} для клиента {participant.Username}");
                        BroadcastActiveClientsToModerators();
                    }
                    else if (role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[Server] Модератор {participant.Username} зарегистрирован.");
                    }

                    // Основной цикл обработки сообщений
                    while (true)
                    {
                        string message = await reader.ReadLineAsync();
                        if (message == null)
                        {
                            Console.WriteLine($"[Server] Участник {username} ({participantId}) отключился.");
                            break;
                        }
                        Console.WriteLine($"[Server] Получено от {username} ({participantId}): {message}");

                        // Если модератор отправляет "CLIENT_LIST", отправляем список активных клиентов.
                        if (role.Equals("Moderator", StringComparison.OrdinalIgnoreCase) &&
                            message.Equals("CLIENT_LIST", StringComparison.OrdinalIgnoreCase))
                        {
                            SendActiveClientsListToModerator(participant);
                            continue;
                        }
                        // Если модератор отправляет "CONNECT:<clientUsername>"
                        if (role.Equals("Moderator", StringComparison.OrdinalIgnoreCase) &&
                            message.StartsWith("CONNECT:", StringComparison.OrdinalIgnoreCase))
                        {
                            string targetClientUsername = message.Substring("CONNECT:".Length).Trim();
                            Participant targetClient = _activeClients.Values.FirstOrDefault(c =>
                                c.Username.Equals(targetClientUsername, StringComparison.OrdinalIgnoreCase));
                            if (targetClient != null)
                            {
                                participant.SendMessage($"CONNECT_OK:{targetClientUsername}");
                                Console.WriteLine($"[Server] Модератор {participant.Username} подключается к клиенту {targetClientUsername}");
                            }
                            else
                            {
                                participant.SendMessage("ERROR:ClientNotFound");
                            }
                            continue;
                        }

                        // Маршрутизируем прочие сообщения, если требуется.
                        HandleChannelMessage(message, participantId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server] Ошибка с участником {participantId}: {ex.Message}");
                }
                finally
                {
                    if (participant != null && participant.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
                    {
                        _activeClients.TryRemove(participantId, out _);
                        BroadcastActiveClientsToModerators();
                    }
                    _participants.TryRemove(participantId, out _);
                    Console.WriteLine($"[Server] Участник {participantId} удалён.");
                }
            }
        }

        // Метод аутентификации, использующий UserRepository.
        private string AuthenticateUser(string username, string password)
        {
            var user = _userRepository.GetUserByUsername(username);
            if (user != null && user.PasswordHash == password)
            {
                return user.Role;
            }
            return null;
        }

        // Отправка списка активных клиентов модератору.
        private void SendActiveClientsListToModerator(Participant moderator)
        {
            var activeClients = _activeClients.Values.Select(c => c.Username).ToArray();
            string message = "CLIENT_LIST:" + string.Join(",", activeClients);
            moderator.SendMessage(message);
            Console.WriteLine($"[Server] Отправлен список активных клиентов модератору {moderator.Username}: {message}");
        }

        // Рассылка списка активных клиентов всем модераторам.
        private void BroadcastActiveClientsToModerators()
        {
            foreach (var mod in _participants.Values.Where(p => p.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase)))
            {
                SendActiveClientsListToModerator(mod);
            }
        }

        // Пример маршрутизации сообщений по комнатам (если требуется).
        private void HandleChannelMessage(string message, string senderId)
        {
            foreach (var room in _chatRooms.Values)
            {
                if (room.Client != null && room.Client.Id == senderId)
                {
                    if (room.Moderator != null)
                        room.Moderator.SendMessage(message);
                    room.Client.SendMessage(message);
                    return;
                }
                if (room.Moderator != null && room.Moderator.Id == senderId)
                {
                    if (room.Client != null)
                        room.Client.SendMessage(message);
                    room.Moderator.SendMessage(message);
                    return;
                }
            }
        }
    }
}
