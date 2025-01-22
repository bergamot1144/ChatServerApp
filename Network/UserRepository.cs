using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace ChatServerApp.Network
{
    // Класс, отвечающий за чтение пользователей из JSON
    public class UserRepository
    {
        private Dictionary<string, User> _usersByName = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        public UserRepository(string jsonPath)
        {
            LoadUsersFromJson(jsonPath);
        }

        private void LoadUsersFromJson(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"[UserRepository] Файл '{path}' не найден, пользователи не загружены.");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                var userData = JsonConvert.DeserializeObject<UserData>(json);

                if (userData?.Users != null)
                {
                    foreach (var user in userData.Users)
                    {
                        _usersByName[user.Username] = user;
                    }
                }

                Console.WriteLine($"[UserRepository] Загружено пользователей: {_usersByName.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UserRepository] Ошибка при чтении JSON: {ex.Message}");
            }
        }

        // Возвращает объект пользователя или null, если не найден
        public User GetUserByUsername(string username)
        {
            _usersByName.TryGetValue(username, out var user);
            return user;
        }
    }
}
