using UnityEngine;

namespace FartRaft
{
    // Door to online play. The real room code lives in OnlineRoom, which only compiles
    // after Fart Raft > 4. Install Online Play adds the Unity Relay packages.
    public static class Online
    {
        public static bool Installed
        {
            get
            {
#if FART_RAFT_ONLINE
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsHost =>
#if FART_RAFT_ONLINE
            OnlineRoom.Hosting;
#else
            false;
#endif

        public static bool IsClient =>
#if FART_RAFT_ONLINE
            OnlineRoom.Joined;
#else
            false;
#endif

        public static bool InRoom => IsHost || IsClient;

        public static int LocalSeat =>
#if FART_RAFT_ONLINE
            OnlineRoom.LocalSeatIndex;
#else
            0;
#endif

        public static int RemotePlayers =>
#if FART_RAFT_ONLINE
            OnlineRoom.RemoteCount;
#else
            0;
#endif

        public static void Attach(GameManager game)
        {
#if FART_RAFT_ONLINE
            OnlineRoom.Attach(game);
#endif
        }

        public static void Open(GameManager game)
        {
            game.ShowOnlinePanel();
            if (!Installed)
                game.SetOnlineStatus("One-time setup: Unity menu Fart Raft > 4. Install Online Play. Then Edit > Project Settings > Services, link the project, and turn Relay on.");
        }

        public static void Host(GameManager game)
        {
#if FART_RAFT_ONLINE
            OnlineRoom.BeginHost(game);
#else
            Open(game);
#endif
        }

        public static void Join(GameManager game, string code)
        {
#if FART_RAFT_ONLINE
            OnlineRoom.BeginJoin(game, code);
#else
            Open(game);
#endif
        }

        public static void Leave(GameManager game)
        {
#if FART_RAFT_ONLINE
            OnlineRoom.Leave(game);
#else
            if (game != null && game.onlinePanel != null) game.onlinePanel.SetActive(false);
#endif
        }

        public static void HostBegan(int playerCount)
        {
#if FART_RAFT_ONLINE
            OnlineRoom.SendStart(playerCount);
#endif
        }

        public static void TellStage(int stage)
        {
#if FART_RAFT_ONLINE
            OnlineRoom.SendStage(stage);
#endif
        }

        public static void SendFinish(string text)
        {
#if FART_RAFT_ONLINE
            OnlineRoom.SendFinish(text);
#endif
        }
    }
}
