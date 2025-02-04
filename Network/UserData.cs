using System.Collections.Generic;

namespace ChatServerApp.Network
{
    /// <summary>
    /// Класс-обёртка для десериализации JSON с пользователями.
    /// </summary>
    public class UserData
    {
        public List<User> Users { get; set; }
    }
}
