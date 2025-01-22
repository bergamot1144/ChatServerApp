using System;
using ChatServerApp.Network;

namespace ChatServerApp

{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Запуск сервера...");
            ChatServer server = new ChatServer();


            server.Start("127.0.0.1", 9000); // Задайте IP и порт
            Console.WriteLine("Сервер запущен. Нажмите любую клавишу для выхода.");
            Console.ReadKey();
            server.Stop();
        }
    }
}
