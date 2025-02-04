//using System;
//using System.Threading;
//using System.Threading.Tasks;
//using ChatServerApp.Network;

//namespace ChatServerApp
//{
//    class Program
//    {
//        static async Task Main(string[] args)
//        {
//            // Указываем путь к JSON-файлу с пользователями
//            string userJsonPath = "users.json"; // Файл users.json должен находиться рядом с исполняемым файлом

//            ChatServer server = new ChatServer(userJsonPath);
//            CancellationTokenSource cts = new CancellationTokenSource();

//            // Запускаем сервер на указанном IP и порте
//            Task serverTask = server.StartAsync("127.0.0.1", 9000, cts.Token);

//            Console.WriteLine("Нажмите Enter для остановки сервера...");
//            Console.ReadLine();

//            cts.Cancel();
//            await serverTask;
//            Console.WriteLine("Сервер завершил работу.");
//        }
//    }
//}

using System;
using System.Threading.Tasks;
using ChatServerApp.Network;

namespace ChatServerApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Укажите путь к файлу users.json (поместите его в рабочую директорию)
            string userJsonPath = "users.json";
            ChatServer server = new ChatServer("127.0.0.1", 9000, userJsonPath);
            await server.StartAsync();
        }
    }
}
