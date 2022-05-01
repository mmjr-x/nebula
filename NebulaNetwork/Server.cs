﻿using HarmonyLib;
using NebulaAPI;
using NebulaModel;
using NebulaModel.DataStructures;
using NebulaModel.Networking;
using NebulaModel.Networking.Serialization;
using NebulaModel.Packets.GameHistory;
using NebulaModel.Packets.GameStates;
using NebulaModel.Packets.Universe;
using NebulaModel.Utils;
using NebulaNetwork.STUNT;
using NebulaWorld;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;
using LumiSoft.Net.STUN.Client;
using NebulaModel.Logger;

namespace NebulaNetwork
{
    public class Server : NetworkProvider, IServer
    {
        private const float GAME_RESEARCH_UPDATE_INTERVAL = 2;
        private const float STATISTICS_UPDATE_INTERVAL = 1;
        private const float LAUNCH_UPDATE_INTERVAL = 4;
        private const float DYSONSPHERE_UPDATE_INTERVAL = 2;
        private const float WARNING_UPDATE_INTERVAL = 1;

        private float gameResearchHashUpdateTimer = 0;
        private float productionStatisticsUpdateTimer = 0;
        private float dysonLaunchUpateTimer = 1;
        private float dysonSphereUpdateTimer = 0;
        private float warningUpdateTimer = 0;

        private WebSocketServer socket;

        private readonly int port;
        private readonly bool loadSaveFile;

        public int Port => port;

        public Server(int port, bool loadSaveFile = false) : base(new PlayerManager())
        {
            this.port = port;
            this.loadSaveFile = loadSaveFile;
        }

        public override void Start()
        {
            if (loadSaveFile)
            {
                SaveManager.LoadServerData();
            }

            foreach (Assembly assembly in AssembliesUtils.GetNebulaAssemblies())
            {
                PacketUtils.RegisterAllPacketNestedTypesInAssembly(assembly, PacketProcessor);
            }
            PacketUtils.RegisterAllPacketProcessorsInCallingAssembly(PacketProcessor, true);

            foreach (Assembly assembly in NebulaModAPI.TargetAssemblies)
            {
                PacketUtils.RegisterAllPacketNestedTypesInAssembly(assembly, PacketProcessor);
                PacketUtils.RegisterAllPacketProcessorsInAssembly(assembly, PacketProcessor, true);
            }
#if DEBUG
            PacketProcessor.SimulateLatency = true;
#endif

            // STUNT stuff
            Socket stuntSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            stuntSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            //var iPEndPoint = new IPEndPoint(IPAddress.Any, 0);
            //stuntSocket.Bind(iPEndPoint);
            stuntSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
            STUN_Result result = STUNT_Client.Query("stun.stunprotocol.org", 3478, stuntSocket);
            Log.Info("STUNT info:");
            Log.Info($"NetType: {result.NetType}");
            Log.Info($"LocalEndPoint: {stuntSocket.LocalEndPoint}");
            Log.Info($"PublicEndPoint: {result.PublicEndPoint}");

            if (result.PublicEndPoint != null)
            {             
                //var localIp = stuntSocket.LocalEndPoint.ToString().Split(':')[0];
                //var localPort = stuntSocket.LocalEndPoint.ToString().Split(':')[1];
                var localIPEndPoint = (stuntSocket.LocalEndPoint as IPEndPoint);
                Log.Info($"Stun was successfull so specifying local adress to be bound ({localIPEndPoint.Address}:{localIPEndPoint.Port})");
                socket = new WebSocketServer(localIPEndPoint.Address, localIPEndPoint.Port);
                socket.ReuseAddress = true;
            } else
            {
                Log.Info($"Stun was unsuccessfull so continuing normally");
                socket = new WebSocketServer(System.Net.IPAddress.IPv6Any, port);
            }

            //socket = new WebSocketServer(System.Net.IPAddress.IPv6Any, port);
            DisableNagleAlgorithm(socket);
            WebSocketService.PacketProcessor = PacketProcessor;
            WebSocketService.PlayerManager = PlayerManager;
            socket.AddWebSocketService<WebSocketService>("/socket", wse => new WebSocketService());
            try
            {
                socket.KeepClean = Config.Options.CleanupInactiveSessions;
                socket.Start();
            }catch(System.InvalidOperationException e)
            {
                InGamePopup.ShowError("Error", "An error occurred while hosting the game: " + e.Message, "Close");
                Stop();
                return;
            }

            ((LocalPlayer)Multiplayer.Session.LocalPlayer).IsHost = true;

            ((LocalPlayer)Multiplayer.Session.LocalPlayer).SetPlayerData(new PlayerData(
                PlayerManager.GetNextAvailablePlayerId(),
                GameMain.localPlanet?.id ?? -1,
                Config.Options.GetMechaColors(),
                !string.IsNullOrWhiteSpace(Config.Options.Nickname) ? Config.Options.Nickname : GameMain.data.account.userName), loadSaveFile);

            NebulaModAPI.OnMultiplayerGameStarted?.Invoke();
        }

        public override void Stop()
        {
            socket?.Stop();

            NebulaModAPI.OnMultiplayerGameEnded?.Invoke();
        }

        public override void Dispose()
        {
            Stop();
        }

        public override void SendPacket<T>(T packet)
        {
            PlayerManager.SendPacketToAllPlayers(packet);
        }

        public override void SendPacketToLocalStar<T>(T packet)
        {
            PlayerManager.SendPacketToLocalStar(packet);
        }

        public override void SendPacketToLocalPlanet<T>(T packet)
        {
            PlayerManager.SendPacketToLocalPlanet(packet);
        }

        public override void SendPacketToPlanet<T>(T packet, int planetId)
        {
            PlayerManager.SendPacketToPlanet(packet, planetId);
        }

        public override void SendPacketToStar<T>(T packet, int starId)
        {
            PlayerManager.SendPacketToStar(packet, starId);
        }

        public override void SendPacketToStarExclude<T>(T packet, int starId, INebulaConnection exclude)
        {
            PlayerManager.SendPacketToStarExcept(packet, starId, (NebulaConnection)exclude);
        }

        public override void Update()
        {
            PacketProcessor.ProcessPacketQueue();

            if (Multiplayer.Session.IsGameLoaded)
            {
                gameResearchHashUpdateTimer += Time.deltaTime;
                productionStatisticsUpdateTimer += Time.deltaTime;
                dysonLaunchUpateTimer += Time.deltaTime;
                dysonSphereUpdateTimer += Time.deltaTime;
                warningUpdateTimer += Time.deltaTime;

                if (gameResearchHashUpdateTimer > GAME_RESEARCH_UPDATE_INTERVAL)
                {
                    gameResearchHashUpdateTimer = 0;
                    if (GameMain.data.history.currentTech != 0)
                    {
                        TechState state = GameMain.data.history.techStates[GameMain.data.history.currentTech];
                        SendPacket(new GameHistoryResearchUpdatePacket(GameMain.data.history.currentTech, state.hashUploaded, state.hashNeeded));
                    }
                }

                if (productionStatisticsUpdateTimer > STATISTICS_UPDATE_INTERVAL)
                {
                    productionStatisticsUpdateTimer = 0;
                    Multiplayer.Session.Statistics.SendBroadcastIfNeeded();
                }

                if (dysonLaunchUpateTimer > LAUNCH_UPDATE_INTERVAL)
                {
                    dysonLaunchUpateTimer = 0;
                    Multiplayer.Session.Launch.SendBroadcastIfNeeded();
                }

                if (dysonSphereUpdateTimer > DYSONSPHERE_UPDATE_INTERVAL)
                {
                    dysonSphereUpdateTimer = 0;
                    Multiplayer.Session.DysonSpheres.UpdateSphereStatusIfNeeded();
                }

                if (warningUpdateTimer > WARNING_UPDATE_INTERVAL)
                {
                    warningUpdateTimer = 0;
                    Multiplayer.Session.Warning.SendBroadcastIfNeeded();
                }
            }            
        }

        private void DisableNagleAlgorithm(WebSocketServer socketServer)
        {
            TcpListener listener = AccessTools.FieldRefAccess<WebSocketServer, TcpListener>("_listener")(socketServer);
            listener.Server.NoDelay = true;
        }

        private class WebSocketService : WebSocketBehavior
        {
            public static IPlayerManager PlayerManager;
            public static NetPacketProcessor PacketProcessor;

            public WebSocketService() { }

            public WebSocketService(IPlayerManager playerManager, NetPacketProcessor packetProcessor)
            {
                PlayerManager = playerManager;
                PacketProcessor = packetProcessor;
            }

            protected override void OnOpen()
            {
                if (Multiplayer.Session.IsGameLoaded == false && Multiplayer.Session.IsInLobby == false)
                {
                    // Reject any connection that occurs while the host's game is loading.
                    Context.WebSocket.Close((ushort)DisconnectionReason.HostStillLoading, "Host still loading, please try again later.");
                    return;
                }

                NebulaModel.Logger.Log.Info($"Client connected ID: {ID}");
                NebulaConnection conn = new NebulaConnection(Context.WebSocket, Context.UserEndPoint, PacketProcessor);
                PlayerManager.PlayerConnected(conn);
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                PacketProcessor.EnqueuePacketForProcessing(e.RawData, new NebulaConnection(Context.WebSocket, Context.UserEndPoint, PacketProcessor));
            }

            protected override void OnClose(CloseEventArgs e)
            {
                // If the reason of a client disconnect is because we are still loading the game,
                // we don't need to inform the other clients since the disconnected client never
                // joined the game in the first place.
                if (e.Code == (short)DisconnectionReason.HostStillLoading)
                {
                    return;
                }

                NebulaModel.Logger.Log.Info($"Client disconnected: {ID}, reason: {e.Reason}");
                UnityDispatchQueue.RunOnMainThread(() =>
                {
                    // This is to make sure that we don't try to deal with player disconnection
                    // if it is because we have stopped the server and are not in a multiplayer game anymore.
                    if (Multiplayer.IsActive)
                    {
                        PlayerManager.PlayerDisconnected(new NebulaConnection(Context.WebSocket, Context.UserEndPoint, PacketProcessor));
                    }
                });
            }

            protected override void OnError(ErrorEventArgs e)
            {
                // TODO: seems like clients erroring out in the sync process can lock the host with the joining player message, maybe this fixes it
                NebulaModel.Logger.Log.Info($"Client disconnected because of an error: {ID}, reason: {e.Exception}");
                UnityDispatchQueue.RunOnMainThread(() =>
                {
                    // This is to make sure that we don't try to deal with player disconnection
                    // if it is because we have stopped the server and are not in a multiplayer game anymore.
                    if (Multiplayer.IsActive)
                    {
                        PlayerManager.PlayerDisconnected(new NebulaConnection(Context.WebSocket, Context.UserEndPoint, PacketProcessor));
                    }
                });
            }
        }
    }
}
