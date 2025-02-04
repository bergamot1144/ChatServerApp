namespace ChatServerApp.Network
{
    /// <summary>
    /// Класс пользователя (для аутентификации).
    /// </summary>
    public class User
    {
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }
    }
}
