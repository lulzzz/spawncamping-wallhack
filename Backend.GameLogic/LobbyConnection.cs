﻿namespace Backend.GameLogic
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Backend.Utils.Networking.Extensions;
    using System.ComponentModel.Composition;
    
    public class LobbyConnection
    {
        private readonly TcpClient tcpClient;

        public LobbyConnection(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
        }

        public async Task Handlerequest()
        {
            Socket socket = tcpClient.Client;

            var clientId = await JoinLobbyAsync(socket);

            if (clientId > 100)
            {
                await socket.SendErrorAsync("Sorry, not permitted");
                return;
            }

            Console.WriteLine("Connect from client {0}", clientId);
        }



        public async Task<int> JoinLobbyAsync(Socket socket)
        {
            var clientId = await socket.ReadInt32Async();

            return clientId;
        }
    }
}
