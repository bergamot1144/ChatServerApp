using System;

namespace ChatServerApp.Network
{
    // Класс для представления одного пользователя
    public class User
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; } // Для упрощения можно хранить пароль, но лучше хранить хэш
        public string Role { get; set; }         // "Client" или "Moderator"
    }
}
