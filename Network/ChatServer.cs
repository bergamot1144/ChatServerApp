using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace ChatServerApp.Network
{
    public class ChatServer
    {
        private TcpListener _tcpListener;

        // Словари для отслеживания клиентов и модераторов
        private ConcurrentDictionary<string, TcpClient> _clients = new ConcurrentDictionary<string, TcpClient>();
        private ConcurrentDictionary<string, NetworkStream> _clientStreams = new ConcurrentDictionary<string, NetworkStream>();
        private ConcurrentDictionary<string, ChatRoom> _chatRooms = new ConcurrentDictionary<string, ChatRoom>();
        private ConcurrentDictionary<string, TcpClient> _moderators = new ConcurrentDictionary<string, TcpClient>();

        // Дополнительные словари для хранения clientId -> username и role
        private ConcurrentDictionary<string, string> _clientIdToUsername = new ConcurrentDictionary<string, string>();
        private ConcurrentDictionary<string, string> _clientIdToRole = new ConcurrentDictionary<string, string>();

        private UserRepository _userRepository;

        public void Start(string ipAddress, int port)
        {
            _userRepository = new UserRepository("users.json");

            _tcpListener = new TcpListener(IPAddress.Parse(ipAddress), port);
            _tcpListener.Start();
            Console.WriteLine($"Сервер запущен на {ipAddress}:{port}");

            while (true)
            {
                try
                {
                    var client = _tcpListener.AcceptTcpClient();
                    Thread clientThread = new Thread(() => HandleClient(client));
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при подключении клиента: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            // Генерируем внутренний clientId (GUID) для отслеживания соединения
            string clientId = Guid.NewGuid().ToString();
            _clients.TryAdd(clientId, client);
            _clientStreams.TryAdd(clientId, stream);
            Console.WriteLine($"Клиент {clientId} подключился.");

            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Console.WriteLine($"Получено от {clientId}: {message}");

                    if (message.StartsWith("LOGIN"))
                    {
                        // ОЖИДАЕМ формат "LOGIN:username:password"
                        string[] parts = message.Split(':');
                        if (parts.Length == 3)
                        {
                            string username = parts[1];
                            string password = parts[2];

                            if (AuthenticateUser(username, password))
                            {
                                var user = _userRepository.GetUserByUsername(username);
                                Console.WriteLine($"Пользователь {username} (роль: {user.Role}) успешно вошёл.");

                                // Запоминаем, что данный clientId связан с username
                                _clientIdToUsername[clientId] = username;
                                _clientIdToRole[clientId] = user.Role;

                                if (user.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Create Participant
                                    Participant clientParticipant = new Participant(clientId, username, client);

                                    // Создаём комнату с ключом = clientId (GUID)
                                    string roomId = clientId;
                                    var chatRoom = new ChatRoom(roomId, clientParticipant);  // moderator = null, client = clientParticipant
                                    _chatRooms[roomId] = chatRoom;

                                    Console.WriteLine($"[SERVER] Создана комната {roomId} для клиента (логин={username}). Пока без модератора.");

                                    // Посылаем логин-ОК
                                    SendMessage(client, "LOGIN_OK:Client");
                                }
                                else if (user.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!_moderators.ContainsKey(username))
                                    {
                                        // Добавляем модератора
                                        _moderators.TryAdd(username, client);

                                        // Посылаем логин-ОК
                                        SendMessage(client, "LOGIN_OK:Moderator");

                                        // Отправляем список клиентов
                                        SendClientList(clientId);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Модератор {username} уже в системе.");
                                        SendMessage(client, "LOGIN_FAIL:AlreadyLoggedIn");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Ошибка аутентификации для {username}: неверный логин или пароль.");
                                // Отправим клиенту отказ
                                SendMessage(client, "LOGIN_FAIL:InvalidCredentials");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[Server] Некорректный формат сообщения LOGIN от {clientId}: {message}");
                            SendMessage(client, "LOGIN_FAIL:InvalidFormat");
                        }
                    }
                    else if (message.StartsWith("CLIENT_LIST"))
                    {
                        Console.WriteLine($"Получен запрос CLIENT_LIST от {clientId}.");
                        SendClientList(clientId);
                    }
                    else if (message.StartsWith("CONNECT"))
                    {
                        Console.WriteLine($"Получен запрос CONNECT от {clientId}.");
                        HandleModeratorConnection(message, clientId);
                    }
                    else if (message.StartsWith("BUTTON"))
                    {
                        HandleButtonState(message, clientId);
                    }
                    else
                    {
                        HandleChannelMessage(message, clientId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка клиента {clientId}: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                _clientStreams.TryRemove(clientId, out _);
                _clientIdToUsername.TryRemove(clientId, out _);
                _clientIdToRole.TryRemove(clientId, out _);
                Console.WriteLine($"Клиент {clientId} отключился.");

                // Если это клиент, удалить его из ChatRoom
                // Найти комнату и удалить клиента
                ChatRoom roomToRemove = null;
                foreach (var room in _chatRooms.Values)
                {
                    if (room.Client.Id == clientId)
                    {
                        roomToRemove = room;
                        break;
                    }
                }
                if (roomToRemove != null)
                {
                    // Notify moderator, if any
                    if (roomToRemove.Moderator != null)
                    {
                        roomToRemove.Moderator.SendMessage($"Client {roomToRemove.Client.Username} has disconnected from room {roomToRemove.RoomId}.");
                    }

                    // Remove the room
                    _chatRooms.TryRemove(roomToRemove.RoomId, out _);
                    Console.WriteLine($"Комната {roomToRemove.RoomId} удалена.");
                }

                // Если это был модератор, удалить из _moderators и уведомить клиентов в комнатах
                if (_clientIdToRole.TryGetValue(clientId, out string role) && role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
                {
                    if (_clientIdToUsername.TryGetValue(clientId, out string username))
                    {
                        _moderators.TryRemove(username, out _);
                        Console.WriteLine($"Модератор {username} ({clientId}) отключился.");
                        // Уведомляем клиентов в комнатах, где был этот модератор
                        foreach (var room in _chatRooms.Values)
                        {
                            if (room.Moderator != null && room.Moderator.Id == clientId)
                            {
                                // Notify client about moderator disconnection
                                room.Client.SendMessage($"Moderator {room.Moderator.Username} has disconnected from room {room.RoomId}.");
                                room.SetModerator(null); // Remove moderator
                            }
                        }
                    }
                }

                client.Close();
            }
        }

        private bool AuthenticateUser(string username, string enteredPassword)
        {
            var user = _userRepository.GetUserByUsername(username);
            if (user == null) return false;
            // Предполагается, что PasswordHash хранит пароль в открытом виде для упрощения
            // В реальной системе следует хранить хэш пароля
            return user.PasswordHash == enteredPassword;
        }

        // Отправка списка всех клиентов (теперь не GUID'ы, а логины)
        private void SendClientList(string clientId)
        {
            Console.WriteLine("=== Enter SendClientList ===");
            Console.WriteLine($"Запрошен список клиентов для отправки клиенту c ID = {clientId}");

            var onlyClients = new List<string>();

            Console.WriteLine(">>> Составляем список только тех, у кого роль = Client:");
            foreach (var kvp in _clientIdToUsername)
            {
                string someClientId = kvp.Key;  // GUID
                string username = kvp.Value;

                Console.WriteLine($"Проверяем clientId={someClientId}, username={username}");
                if (_clientIdToRole.TryGetValue(someClientId, out var role))
                {
                    Console.WriteLine($"Найдена роль [{role}] для clientId={someClientId}");
                    if (role.Equals("Client", StringComparison.OrdinalIgnoreCase))
                    {
                        // Добавляем в список
                        onlyClients.Add(username);
                        Console.WriteLine($"Добавлен [{username}] в список onlyClients");
                    }
                    else
                    {
                        Console.WriteLine($"Пропускаем [{username}], т.к. роль = {role}");
                    }
                }
                else
                {
                    Console.WriteLine($"Не удалось найти роль для clientId={someClientId}, пропускаем");
                }
            }

            // Формируем строку CLIENT_LIST
            string clientList = string.Join(",", onlyClients);
            string response = "CLIENT_LIST:" + clientList;
            Console.WriteLine($">>> Итоговый список логинов (только Client) = {clientList}");

            byte[] data = Encoding.UTF8.GetBytes(response);

            try
            {
                // Ищем нужный поток для clientId
                if (_clientStreams.TryGetValue(clientId, out var stream))
                {
                    // Отправляем данные
                    stream.Write(data, 0, data.Length);
                    Console.WriteLine($"Список клиентов (роль=Client) отправлен клиенту {clientId}: {response}");
                }
                else
                {
                    Console.WriteLine($"[ПРЕДУПРЕЖДЕНИЕ] _clientStreams не содержит {clientId}, не можем отправить список!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ОШИБКА] При отправке списка клиенту {clientId}: {ex.Message}");
            }

            Console.WriteLine("=== Exit SendClientList ===\n");
        }

        private string FindClientIdByUsername(string username)
        {
            foreach (var pair in _clientIdToUsername)
            {
                // pair.Key — GUID, pair.Value — логин (например, c123)
                if (pair.Value.Equals(username, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Key; // Вернём GUID
                }
            }
            return null; // не нашли
        }

        private void HandleModeratorConnection(string message, string moderatorGuid)
        {
            // message = "CONNECT:c123"
            string[] parts = message.Split(':');
            if (parts.Length == 2)
            {
                string targetUsername = parts[1]; // "c123"

                // 1) Ищем реальный GUID клиента по логину
                string targetClientGuid = FindClientIdByUsername(targetUsername);

                // 2) Если нашли GUID
                if (!string.IsNullOrEmpty(targetClientGuid))
                {
                    // 3) Пытаемся найти комнату с ключом = targetClientGuid
                    if (_chatRooms.TryGetValue(targetClientGuid, out var chatRoom))
                    {
                        // 4) Получаем модератора Participant
                        if (_clientIdToUsername.TryGetValue(moderatorGuid, out string moderatorUsername))
                        {
                            // Проверяем роль модератора
                            if (_clientIdToRole.TryGetValue(moderatorGuid, out string role) && role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
                            {
                                Participant moderatorParticipant = new Participant(moderatorGuid, moderatorUsername, _clients[moderatorGuid]);

                                // Назначаем модератора в ChatRoom
                                chatRoom.SetModerator(moderatorParticipant);
                                Console.WriteLine($"Модератор {moderatorGuid} ({moderatorUsername}) подключается к комнате {targetClientGuid} (логин={targetUsername})");

                                // 5) Шлём CONNECT_OK
                                SendMessage(_clients[moderatorGuid], $"CONNECT_OK:{targetUsername}");

                                // Уведомляем модератора о текущем состоянии комнаты
                                chatRoom.NotifyModeratorAboutNewClient();
                            }
                            else
                            {
                                Console.WriteLine($"[Server] Participant {moderatorGuid} не имеет роли Moderator.");
                                SendMessage(_clients[moderatorGuid], "CONNECT_FAIL:NotModerator");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[Server] Не удалось найти username для модератора {moderatorGuid}.");
                            SendMessage(_clients[moderatorGuid], "CONNECT_FAIL:UnknownModerator");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[SERVER] Комната для клиента {targetUsername} (guid={targetClientGuid}) не найдена.");
                        SendMessage(_clients[moderatorGuid], $"CONNECT_FAIL:RoomNotFound:{targetUsername}");
                    }
                }
                else
                {
                    Console.WriteLine($"[SERVER] Не нашли GUID для логина {targetUsername}.");
                    SendMessage(_clients[moderatorGuid], $"CONNECT_FAIL:UserNotFound:{targetUsername}");
                }
            }
            else
            {
                Console.WriteLine($"[Server] Некорректный формат сообщения CONNECT от {moderatorGuid}: {message}");
                SendMessage(_clients[moderatorGuid], "CONNECT_FAIL:InvalidFormat");
            }
        }

        private void HandleChannelMessage(string message, string senderId)
        {
            // Определяем, к какой комнате относится отправитель
            foreach (var room in _chatRooms.Values)
            {
                if (room.HasParticipant(senderId))
                {
                    room.BroadcastMessage(message, senderId);
                    return;
                }
            }

            Console.WriteLine($"[Server] Отправитель {senderId} не принадлежит ни к одной комнате.");
        }

        private void HandleButtonState(string message, string senderId)
        {
            string[] parts = message.Split(':');
            if (parts.Length == 3 && parts[0] == "BUTTON")
            {
                Console.WriteLine($"Состояние кнопки от {senderId}: {message}");
                // Определяем, к какой комнате принадлежит отправитель
                foreach (var room in _chatRooms.Values)
                {
                    if (room.HasParticipant(senderId))
                    {
                        room.BroadcastMessage(message, senderId);
                        // Additionally, notify moderator if needed
                        room.NotifyModeratorButtonState(message);
                        return;
                    }
                }

                Console.WriteLine($"[Server] Не найдена комната для отправителя {senderId} при обработке BUTTON сообщения.");
            }
            else
            {
                Console.WriteLine($"[Server] Некорректный формат BUTTON сообщения от {senderId}: {message}");
            }
        }

        private void SendMessage(TcpClient client, string message)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
                Console.WriteLine($"Сообщение отправлено клиенту {client.Client.RemoteEndPoint}: {message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки сообщения: {ex.Message}");
            }
        }

        public void Stop()
        {
            _tcpListener.Stop();
            Console.WriteLine("Сервер остановлен.");
        }
    }
}
