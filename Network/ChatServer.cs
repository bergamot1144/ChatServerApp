//using System;
//using System.Collections.Concurrent;
//using System.Linq;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace ChatServerApp.Network
//{
//    /// <summary>
//    /// Основной класс сервера.
//    /// Использует асинхронное принятие клиентов и CancellationToken для корректного завершения.
//    /// </summary>
//    public class ChatServer
//    {
//        private TcpListener _tcpListener;
//        private readonly ConcurrentDictionary<string, ChatRoom> _chatRooms = new ConcurrentDictionary<string, ChatRoom>();
//        private readonly ConcurrentDictionary<string, Participant> _participants = new ConcurrentDictionary<string, Participant>();
//        private readonly ConcurrentDictionary<string, string> _participantToRoomMap = new ConcurrentDictionary<string, string>();
//        private CancellationTokenSource _cts;

//        // Для аутентификации используем репозиторий пользователей
//        private readonly UserRepository _userRepository;

//        public ChatServer(string userJsonPath)
//        {
//            _userRepository = new UserRepository(userJsonPath);

//        }
//        /// <summary>
//        /// Простой метод аутентификации.
//        /// Если пользователь найден и пароль совпадает, возвращается его роль (например, "Client" или "Moderator").
//        /// В противном случае возвращается null.
//        /// </summary>
//        private string AuthenticateUser(string username, string password)
//        {
//            // Пример простой проверки. В реальном приложении следует использовать хэширование.
//            var user = _userRepository.GetUserByUsername(username);
//            if (user != null && user.PasswordHash == password)
//            {
//                return user.Role;
//            }
//            return null;
//        }

//        /// <summary>
//        /// Запуск сервера.
//        /// </summary>
//        public async Task StartAsync(string ipAddress, int port, CancellationToken token)
//        {
//            _tcpListener = new TcpListener(IPAddress.Parse(ipAddress), port);
//            _tcpListener.Start();
//            Console.WriteLine($"[Server] Запущен на {ipAddress}:{port}");

//            try
//            {
//                while (!token.IsCancellationRequested)
//                {
//                    TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
//                    _ = HandleClientAsync(tcpClient);
//                }
//            }
//            catch (Exception ex) when (ex is OperationCanceledException)
//            {
//                Console.WriteLine("[Server] Остановка сервера.");
//            }
//            finally
//            {
//                _tcpListener.Stop();
//            }
//        }

//        public void Stop()
//        {
//            _cts?.Cancel();
//            _tcpListener.Stop();
//            Console.WriteLine("[Server] Сервер остановлен.");
//        }

//        private async Task HandleClientAsync(TcpClient tcpClient)
//        {
//            // Генерируем уникальный идентификатор для участника
//            string participantId = Guid.NewGuid().ToString();
//            Console.WriteLine($"[Server] Новый участник подключился: {participantId}");

//            // Оборачиваем TcpClient и связанные потоки в using для корректного освобождения ресурсов
//            using (tcpClient)
//            using (NetworkStream networkStream = tcpClient.GetStream())
//            using (StreamReader reader = new StreamReader(networkStream, Encoding.UTF8))
//            using (StreamWriter writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true })
//            {
//                Participant participant = null;
//                try
//                {
//                    // Аутентификация: ожидаем сообщение вида "LOGIN:username:password"
//                    string loginMessage = await reader.ReadLineAsync();
//                    if (string.IsNullOrEmpty(loginMessage) || !loginMessage.StartsWith("LOGIN:"))
//                    {
//                        await writer.WriteLineAsync("LOGIN_FAIL:InvalidFormat");
//                        Console.WriteLine($"[Server] Некорректный формат LOGIN сообщения от {participantId}: {loginMessage}");
//                        return;
//                    }

//                    string[] loginParts = loginMessage.Split(':');
//                    if (loginParts.Length != 3)
//                    {
//                        await writer.WriteLineAsync("LOGIN_FAIL:InvalidFormat");
//                        Console.WriteLine($"[Server] Некорректный формат LOGIN сообщения от {participantId}: {loginMessage}");
//                        return;
//                    }

//                    string username = loginParts[1];
//                    string password = loginParts[2];

//                    // Здесь должна быть ваша логика аутентификации, например, через UserRepository.
//                    // Предположим, аутентификация прошла успешно, и мы получаем роль (например, "Client" или "Moderator").
//                    string authenticatedRole = AuthenticateUser(username, password); // Метод вашей аутентификации
//                    if (authenticatedRole == null)
//                    {
//                        await writer.WriteLineAsync("LOGIN_FAIL:InvalidCredentials");
//                        Console.WriteLine($"[Server] Неверные учетные данные для {username}.");
//                        return;
//                    }

//                    // Создаём участника
//                    participant = new Participant(participantId, username, authenticatedRole, tcpClient);
//                    _participants.TryAdd(participantId, participant);
//                    await writer.WriteLineAsync($"LOGIN_OK:{authenticatedRole}");
//                    Console.WriteLine($"[Server] Участник {username} ({participantId}) успешно аутентифицирован как {authenticatedRole}");

//                    // Назначение участника в комнату
//                    string roomId = AssignParticipantToRoom(participant);
//                    if (!string.IsNullOrEmpty(roomId))
//                    {
//                        Console.WriteLine($"[Server] Участник {username} ({participantId}) добавлен в комнату {roomId}");
//                    }
//                    else if (authenticatedRole.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
//                    {
//                        Console.WriteLine($"[Server] Модератор {username} зарегистрирован без присоединения к комнате.");
//                    }

//                    // Основной цикл обработки входящих сообщений от участника
//                    while (true)
//                    {
//                        string message = await reader.ReadLineAsync();
//                        if (message == null)
//                        {
//                            Console.WriteLine($"[Server] Участник {username} ({participantId}) отключился.");
//                            break;
//                        }
//                        Console.WriteLine($"[Server] Получено от {username} ({participantId}): {message}");

//                        // Если участник – модератор и отправил команду "CLIENT_LIST", то отправляем список доступных комнат.
//                        if (participant.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase) &&
//                            message.Equals("CLIENT_LIST", StringComparison.OrdinalIgnoreCase))
//                        {
//                            SendRoomListToModerator(participant);
//                            continue;
//                        }

//                        // Если участник – модератор и отправил команду подключения: "CONNECT:<RoomId>"
//                        if (participant.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase) &&
//                            message.StartsWith("CONNECT:", StringComparison.OrdinalIgnoreCase))
//                        {
//                            string roomIdToConnect = message.Substring("CONNECT:".Length).Trim();
//                            if (_chatRooms.TryGetValue(roomIdToConnect, out ChatRoom room))
//                            {
//                                // Добавляем модератора в выбранную комнату
//                                room.AddParticipant(participant);
//                                _participantToRoomMap[participant.Id] = roomIdToConnect;
//                                Console.WriteLine($"[Server] Модератор {username} присоединён к комнате {roomIdToConnect}");
//                                participant.SendMessage($"CONNECT_OK:{roomIdToConnect}");
//                                // Обновляем список комнат для всех модераторов (если нужно)
//                                BroadcastRoomListToModerators();
//                            }
//                            else
//                            {
//                                participant.SendMessage("ERROR:RoomNotFound");
//                            }
//                            continue;
//                        }

//                        // Для остальных сообщений используем стандартную маршрутизацию по комнатам.
//                        HandleChannelMessage(message, participantId);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[Server] Ошибка с участником {participantId}: {ex.Message}");
//                }
//                finally
//                {
//                    RemoveParticipantFromRoom(participantId);
//                    _participants.TryRemove(participantId, out _);
//                    Console.WriteLine($"[Server] Участник {participantId} удалён.");
//                }
//            }
//        }

//        /// <summary>
//        /// Формирует список доступных комнат (где есть клиент, но модератор не присоединён)
//        /// и отправляет его модератору. Каждый элемент списка имеет формат: "ClientUsername".
//        /// </summary>
//        /// <param name="moderator">Участник с ролью модератора.</param>
//        private void SendRoomListToModerator(Participant moderator)
//        {
//            var availableRooms = _chatRooms.Values
//                                   .Where(r => r.Client != null && r.Moderator == null)
//                                   .Select(r => r.Client.Username) // отображаем только имя клиента
//                                   .ToArray();

//            string message = "ROOM_LIST:" + string.Join(",", availableRooms);
//            moderator.SendMessage(message); // метод SendMessage гарантирует добавление \n
//            Console.WriteLine($"[Server] Отправлен список комнат модератору {moderator.Username}: {message}");
//        }



//        /// <summary>
//        /// Формирует список активных клиентов и отправляет его модератору.
//        /// Список состоит из имён (Username) участников с ролью "Client".
//        /// </summary>
//        /// <param name="moderator">Участник с ролью модератора, который сделал запрос.</param>
//        private void SendClientListToModerator(Participant moderator)
//        {
//            var clients = _participants.Values
//                            .Where(p => p.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
//                            .Select(p => p.Username)
//                            .ToArray();

//            // Формируем сообщение и добавляем '\n' в конец
//            string message = "CLIENT_LIST:" + string.Join(",", clients) + "\n";
//            moderator.SendMessage(message);
//            Console.WriteLine($"[Server] Отправлен список клиентов модератору {moderator.Username}: {message.Trim()}");
//        }
//        /// <summary>
//        /// Рассылает всем модераторам обновлённый список активных клиентов.
//        /// Список формируется из пользователей с ролью "Client".
//        /// </summary>
//        private void BroadcastClientListToModerators()
//        {
//            var clients = _participants.Values
//                            .Where(p => p.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
//                            .Select(p => p.Username)
//                            .ToArray();

//            string message = "CLIENT_LIST:" + string.Join(",", clients);
//            foreach (var moderator in _participants.Values.Where(p => p.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase)))
//            {
//                // Метод SendMessage теперь гарантированно добавляет "\n" в конце, если его нет.
//                moderator.SendMessage(message);
//                Console.WriteLine($"[Server] Рассылка списка модераторам {moderator.Username}: {message}");
//            }
//        }









//        /// <summary>
//        /// Назначение участника в комнату.
//        /// Если подходящая комната не найдена – создаётся новая.
//        /// </summary>
//        private string AssignParticipantToRoom(Participant participant)
//        {
//            Console.WriteLine($"[Server] Попытка присвоить участника {participant.Username} с ролью {participant.Role}.");
//            string roomId = string.Empty;

//            if (participant.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
//            {
//                // Для клиента: ищем комнату с модератором и без клиента или создаём новую.
//                var room = _chatRooms.Values.FirstOrDefault(r => r.Moderator != null && r.Client == null);
//                if (room != null)
//                {
//                    room.AddParticipant(participant);
//                    _participantToRoomMap.TryAdd(participant.Id, room.RoomId);
//                    roomId = room.RoomId;
//                    Console.WriteLine($"[Server] Client {participant.Username} присвоен к существующей комнате {room.RoomId}");
//                }
//                else
//                {
//                    roomId = "Room_" + Guid.NewGuid().ToString();
//                    ChatRoom newRoom = new ChatRoom(roomId);
//                    newRoom.AddParticipant(participant);
//                    _chatRooms.TryAdd(roomId, newRoom);
//                    _participantToRoomMap.TryAdd(participant.Id, roomId);
//                    Console.WriteLine($"[Server] Создана новая комната {roomId} для участника {participant.Username}");
//                }
//                // При добавлении клиента рассылаем обновлённый список доступных комнат
//                BroadcastRoomListToModerators();
//            }
//            else if (participant.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
//            {
//                // Для модератора НЕ присоединяем его автоматически к комнате.
//                // Мы просто регистрируем его как участника без привязки к комнате.
//                // Для этого можно сохранить модератора отдельно (например, в _participants) без вызова AssignParticipantToRoom.
//                _participants.TryAdd(participant.Id, participant);
//                Console.WriteLine($"[Server] Модератор {participant.Username} зарегистрирован, но не присоединён к комнате.");
//                // Возвращаем специальное значение, например, пустую строку или null
//                roomId = string.Empty;
//            }
//            else
//            {
//                // Если роль неизвестна, можно создать отдельную комнату (или вернуть ошибку)
//                roomId = "Room_" + Guid.NewGuid().ToString();
//                ChatRoom newRoom = new ChatRoom(roomId);
//                newRoom.AddParticipant(participant);
//                _chatRooms.TryAdd(roomId, newRoom);
//                _participantToRoomMap.TryAdd(participant.Id, roomId);
//                Console.WriteLine($"[Server] Создана новая комната {roomId} для участника {participant.Username}");
//            }
//            return roomId;
//        }



//        /// <summary>
//        /// Удаление участника из комнаты.
//        /// Если комната пуста – удаляем её.
//        /// </summary>
//        private void RemoveParticipantFromRoom(string participantId)
//        {
//            if (_participantToRoomMap.TryRemove(participantId, out var roomId))
//            {
//                if (_chatRooms.TryGetValue(roomId, out var room))
//                {
//                    // Запоминаем роль перед удалением, чтобы потом решить, стоит ли обновлять список
//                    string removedRole = room.Moderator != null && room.Moderator.Id == participantId ? "Moderator" :
//                                         room.Client != null && room.Client.Id == participantId ? "Client" : string.Empty;

//                    room.RemoveParticipant(participantId);
//                    Console.WriteLine($"[Server] Участник {participantId} удален из комнаты {room.RoomId}.");

//                    // Если комната пуста, удаляем ее
//                    if (room.Moderator == null && room.Client == null)
//                    {
//                        _chatRooms.TryRemove(room.RoomId, out _);
//                        Console.WriteLine($"[Server] Комната {room.RoomId} удалена, так как она пуста.");
//                    }

//                    // Если удален клиент, рассылаем обновленный список модераторам
//                    if (removedRole == "Client")
//                    {
//                        BroadcastClientListToModerators();
//                    }
//                }
//            }
//        }


//        /// <summary>
//        /// Маршрутизация входящих сообщений к нужной комнате.
//        /// </summary>
//        private void HandleChannelMessage(string message, string senderId)
//        {
//            var room = _chatRooms.Values.FirstOrDefault(r => r.HasParticipant(senderId));
//            if (room != null)
//            {
//                room.SendMessageToAll(message, senderId);
//            }
//            else
//            {
//                Console.WriteLine($"[Server] Отправитель {senderId} не принадлежит ни к одной комнате.");
//            }
//        }


//        private void BroadcastRoomListToModerators()
//        {
//            foreach (var mod in _participants.Values.Where(p => p.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase)))
//            {
//                SendRoomListToModerator(mod);
//            }
//        }

//    }
//}

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
    public class ChatServer
    {
        private TcpListener _tcpListener;
        private ConcurrentDictionary<string, ChatRoom> _chatRooms = new ConcurrentDictionary<string, ChatRoom>();
        private ConcurrentDictionary<string, Participant> _participants = new ConcurrentDictionary<string, Participant>();
        private ConcurrentDictionary<string, string> _participantToRoomMap = new ConcurrentDictionary<string, string>();
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
                    // Ожидаем сообщение для логина: "LOGIN:username:password"
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

                    // Назначаем участника в комнату:
                    // Для клиента - создаем/присоединяем комнату;
                    // Для модератора - не присоединяем автоматически.
                    string roomId = AssignParticipantToRoom(participant);
                    if (!string.IsNullOrEmpty(roomId))
                    {
                        Console.WriteLine($"[Server] Участник {username} ({participantId}) добавлен в комнату {roomId}");
                    }
                    else if (role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[Server] Модератор {username} зарегистрирован без присоединения к комнате.");
                    }

                    // Основной цикл чтения сообщений от участника.
                    while (true)
                    {
                        string message = await reader.ReadLineAsync();
                        if (message == null)
                        {
                            Console.WriteLine($"[Server] Участник {username} ({participantId}) отключился.");
                            break;
                        }
                        Console.WriteLine($"[Server] Получено от {username} ({participantId}): {message}");

                        // Если участник – модератор и отправил "CLIENT_LIST", отправляем список комнат.
                        if (role.Equals("Moderator", StringComparison.OrdinalIgnoreCase) &&
                            message.Equals("CLIENT_LIST", StringComparison.OrdinalIgnoreCase))
                        {
                            SendRoomListToModerator(participant);
                            continue;
                        }
                        // Если модератор отправил команду подключения "CONNECT:<RoomId>"
                        if (role.Equals("Moderator", StringComparison.OrdinalIgnoreCase) &&
                            message.StartsWith("CONNECT:", StringComparison.OrdinalIgnoreCase))
                        {
                            string roomIdToConnect = message.Substring("CONNECT:".Length).Trim();
                            if (_chatRooms.TryGetValue(roomIdToConnect, out ChatRoom room))
                            {
                                try
                                {
                                    room.AddParticipant(participant);
                                    _participantToRoomMap[participant.Id] = roomIdToConnect;
                                    Console.WriteLine($"[Server] Модератор {username} присоединён к комнате {roomIdToConnect}");
                                    participant.SendMessage($"CONNECT_OK:{roomIdToConnect}");
                                    BroadcastRoomListToModerators();
                                }
                                catch (Exception ex)
                                {
                                    participant.SendMessage("ERROR:" + ex.Message);
                                }
                            }
                            else
                            {
                                participant.SendMessage("ERROR:RoomNotFound");
                            }
                            continue;
                        }

                        // Иначе маршрутизируем сообщение в соответствующую комнату.
                        HandleChannelMessage(message, participantId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server] Ошибка с участником {participantId}: {ex.Message}");
                }
                finally
                {
                    RemoveParticipantFromRoom(participantId);
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

        // Метод назначения участника в комнату.
        // Если роль Client, создаём или присоединяем комнату.
        // Если роль Moderator, не присоединяем автоматически.
        private string AssignParticipantToRoom(Participant participant)
        {
            string roomId = string.Empty;
            if (participant.Role.Equals("Client", StringComparison.OrdinalIgnoreCase))
            {
                // Ищем комнату, где еще нет клиента.
                var room = _chatRooms.Values.FirstOrDefault(r => r.Client == null);
                if (room != null)
                {
                    room.AddParticipant(participant);
                    roomId = room.RoomId;
                    _participantToRoomMap.TryAdd(participant.Id, roomId);
                    Console.WriteLine($"[Server] Client {participant.Username} присоединён к комнате {roomId}");
                }
                else
                {
                    roomId = "Room_" + Guid.NewGuid().ToString();
                    ChatRoom newRoom = new ChatRoom(roomId);
                    newRoom.AddParticipant(participant);
                    _chatRooms.TryAdd(roomId, newRoom);
                    _participantToRoomMap.TryAdd(participant.Id, roomId);
                    Console.WriteLine($"[Server] Создана новая комната {roomId} для клиента {participant.Username}");
                }
                // После добавления клиента рассылаем обновлённый список комнат модераторам.
                BroadcastRoomListToModerators();
            }
            else if (participant.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase))
            {
                // Для модератора не присоединяем автоматически.
                Console.WriteLine($"[Server] Модератор {participant.Username} зарегистрирован без присоединения к комнате.");
            }
            else
            {
                roomId = "Room_" + Guid.NewGuid().ToString();
                ChatRoom newRoom = new ChatRoom(roomId);
                newRoom.AddParticipant(participant);
                _chatRooms.TryAdd(roomId, newRoom);
                _participantToRoomMap.TryAdd(participant.Id, roomId);
                Console.WriteLine($"[Server] Создана новая комната {roomId} для участника {participant.Username}");
            }
            return roomId;
        }

        // Метод отправки списка комнат модератору.
        // Формируем список доступных комнат: там, где есть клиент, но нет модератора.
        // Отправляем имена клиентов в качестве идентификаторов.
        private void SendRoomListToModerator(Participant moderator)
        {
            var availableRooms = _chatRooms.Values
                .Where(r => r.Client != null && r.Moderator == null)
                .Select(r => r.Client.Username)
                .ToArray();
            string message = "ROOM_LIST:" + string.Join(",", availableRooms);
            moderator.SendMessage(message);
            Console.WriteLine($"[Server] Отправлен список комнат модератору {moderator.Username}: {message}");
        }

        // Рассылает список комнат всем модераторам.
        private void BroadcastRoomListToModerators()
        {
            foreach (var mod in _participants.Values.Where(p => p.Role.Equals("Moderator", StringComparison.OrdinalIgnoreCase)))
            {
                SendRoomListToModerator(mod);
            }
        }

        // Метод маршрутизации сообщений по комнатам.
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

        // Метод удаления участника из комнаты.
        private void RemoveParticipantFromRoom(string participantId)
        {
            if (_participantToRoomMap.TryRemove(participantId, out string roomId))
            {
                if (_chatRooms.TryGetValue(roomId, out ChatRoom room))
                {
                    room.RemoveParticipant(participantId);
                    Console.WriteLine($"[Server] Участник {participantId} удалён из комнаты {roomId}.");
                    if (room.Client == null && room.Moderator == null)
                    {
                        _chatRooms.TryRemove(roomId, out _);
                        Console.WriteLine($"[Server] Комната {roomId} удалена, так как она пуста.");
                    }
                    else
                    {
                        // Если был удалён клиент, обновляем список комнат.
                        BroadcastRoomListToModerators();
                    }
                }
            }
        }
    }
}
