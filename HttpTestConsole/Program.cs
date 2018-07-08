using System;
using System.Net;
using System.Threading;
using HttpTest;

namespace HttpTestConsole
{
    class Program
    {
        static int Main(string[] args)
        {
            int res = 0;
            if (args.Length == 0)
                //Запускаем дефолтный тест - без авторизации
                res = RunDefaulSequence();
            else if (args.Length == 2)
                //Запускаем тест авторизации
                res = RunLoginSequence(args[0], args[1]);
            else
                //Выводим справку
                WriteHelpText();

            Console.WriteLine($"Код возврата: {res.ToString()}");
            return res;
        }

        static int RunDefaulSequence()
        {
            Console.WriteLine("\r\nВыполняется обход разделов по умолчанию\r\n");

            int res = 0;
                UserState state = new UserState();
                foreach (string url in GetSteps())
                {
                try
                {
                    state = HttpTest.HttpTest.Navigate(new Uri(url), HttpRequestTypeEnum.GET, state);
                    foreach (Cookie cookie in state.Cookie)
                        if (cookie.ToString().Length > 50) 
                            Console.WriteLine($"\tCookie: {cookie.ToString().Substring(0, 50)}...");
                        else
                            Console.WriteLine($"\tCookie: {cookie.ToString()}");
                    Thread.Sleep(500);
                    Console.WriteLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Исключение: {e.Message}");
                    res--;
                }
            }
            return res;
        }

        static int RunLoginSequence(string userName, string password)
        {
            UserState userState = new UserState(userName, password);
            userState = HttpTest.HttpTest.Login(userState, true);
            if (userState.Result)
                return 0;
            return -1;
        }

        HttpTest.UserState Login(string userName, string password)
        {
            HttpTest.UserState userState = new UserState(userName, password);
            return HttpTest.HttpTest.Login(userState, true );
        }

        static string[] GetSteps()
        {
            return new string[]
            {
                "http://my.kaspersky.ru/",
                "https://my.kaspersky.com/dashboard/",
                "https://my.kaspersky.com/MyPasswords",
                "https://my.kaspersky.com/MyKids/",
                "https://my.kaspersky.com/VPN/Promo/",
                "https://my.kaspersky.com/dr/Store"
            };
        }

        static void WriteHelpText()
        {
            Console.WriteLine("ИСПОЛЬЗОВАНИЕ:\r\n");
                        Console.WriteLine("Запуск без параметров \t- выполнение проходов по главным разделам \r\n\t\t\t сайта без выполнения авторизации");
            Console.WriteLine("Запуск с параметрами \t - последовательное выполнение авторизации и выхода\r\n");
            Console.WriteLine("\tHttpTestConsole [username password]");
            Console.WriteLine("Здесь:");
            Console.WriteLine("\tusername\t- Имя пользователя(логин)");
            Console.WriteLine("\tusername\t- Пароль\r\n");
        }
    }
}
