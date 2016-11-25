using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Kastchei;

namespace Kastchei.Demo
{
    class Program
    {
        const string ENDPOINT = "ws://localhost:4000/socket/websocket?username=Demo";

        static void Main(string[] args)
        {
            using (var manager = new SocketManager(ENDPOINT))
            {
                Console.WriteLine("Retrieved manager");

                using (var channel = manager.Channel("control:Demo"))
                {
                    Console.WriteLine("Got channel...");

                    //channel.Join<Dictionary<string,string>>("ok", (x) => Console.WriteLine("Join 'control:demo' responded OK"));
                    channel.Join()
                           .MatchOn<Dictionary<string, string>>("ok")
                           .Subscribe(x => Console.WriteLine("Join 'control:demo' responded OK"));

                    using (var ch2 = manager.Channel("room:lobby"))
                    {
                        Console.WriteLine("Got lobby channel...");

                        ch2.Join()
                           .MatchOn<Dictionary<string, string>>("ok")
                           .Subscribe(x => Console.WriteLine("Join 'room:lobby' responded OK"));

                        ch2.Send("new:msg", new Dictionary<string, string> { { "body", "Hi there!" } })
                           .MatchOn<Dictionary<string, string>>("ok")
                           .Subscribe(x => Console.WriteLine("Message received"));

                        ch2.On<string>("new:msg").Subscribe(x => Console.WriteLine($"new:msg {x}"));

                        Thread.Sleep(99990000);
                    }
                }
            }

            Thread.Sleep(2000);
        }
    }
}
