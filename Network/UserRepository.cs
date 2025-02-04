using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ChatServerApp.Network
{
    public class UserRepository
    {
        private readonly Dictionary<string, User> _users;

        public UserRepository(string jsonPath)
        {
            _users = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);
            LoadUsers(jsonPath);
        }

        private void LoadUsers(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"[UserRepository] Файл '{path}' не найден.");
                return;
            }
            try
            {
                string json = File.ReadAllText(path);
                UserData data = JsonConvert.DeserializeObject<UserData>(json);
                if (data?.Users != null)
                {
                    foreach (var user in data.Users)
                    {
                        _users[user.Username] = user;
                    }
                }
                Console.WriteLine($"[UserRepository] Загружено пользователей: {_users.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserRepository] Ошибка: {ex.Message}");
            }
        }

        public User GetUserByUsername(string username)
        {
            _users.TryGetValue(username, out User user);
            return user;
        }
    }
}
