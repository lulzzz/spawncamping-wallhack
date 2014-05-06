﻿namespace Backend.GameServer
{
    using Backend.GameLogic;
    using Backend.Utils.Networking;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class GameServerProgram
    {
        static void Main(string[] args)
        {
            Console.Title = "Backend.GameServer";

            if (args.Length != 2)
            {
                Console.Error.WriteLine("Provide ip address (such as 127.0.0.1) and TCP port as command line args");
                return;
            }

            IPAddress address;
            if (!IPAddress.TryParse(args[0], out address)) {
                Console.Error.WriteLine("\"{0}\" is not a valid IP address", args[0]);
                return;
            }
            int port;
            if (!int.TryParse(args[1], out port)) {
                Console.Error.WriteLine("\"{0}\" is not a valid TCP port", args[1]);
                return;
            }

            var ipEndPoint = new IPEndPoint(address, port);

            Console.WriteLine("Listen on {0}", ipEndPoint);

            var cts = new CancellationTokenSource();
            var server = new AsyncServerHost(ipEndPoint);

            var gameServerImpl = new GameServerImpl();
            Task t = server.Start(gameServerImpl, cts.Token);

            Console.WriteLine("Launched game server process on {0}", ipEndPoint);
            t.Wait();
        }
    }
}