﻿namespace Backend.LobbyServer
{
    using System;
    using System.ComponentModel.Composition.Hosting;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    using Backend.GameLogic;
    using Backend.Utils.Networking;

    class LobbyServerProgram
    {
        static void Main(string[] args)
        {
            Console.Title = "Backend.LobbyServer";

            var cts = new CancellationTokenSource();

            var compositionContainer = new CompositionContainer(new AggregateCatalog(
                // new TypeCatalog(typeof(LobbyServiceBackplane), typeof(LobbyServerImpl), typeof(EnvironmentSettingsProvider))
                new AssemblyCatalog(typeof(DevelopmentSettings.EnvironmentSettingsProvider).Assembly),
                new AssemblyCatalog(typeof(LobbyServiceBackplane).Assembly)
                ));
            var settings = compositionContainer.GetExportedValue<ILobbyServiceSettings>();

            var lobbyServerImpl = compositionContainer.GetExportedValue<LobbyServerImpl>();
            var server = new AsyncServerHost(settings.IPEndPoint);
            Task t = server.Start(lobbyServerImpl.HandleClient, cts.Token);

            Console.WriteLine("Loby server launched");
            Console.ReadLine();
            cts.Cancel();

            lobbyServerImpl.ShutDownAsync().Wait();
        }
    }
}