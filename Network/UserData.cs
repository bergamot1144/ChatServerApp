using System;
using System.Collections.Generic;

namespace ChatServerApp.Network
{
    // Класс-обёртка для десериализации JSON
    public class UserData
    {
        public List<User> Users { get; set; }
    }
}
