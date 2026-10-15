#if FART_RAFT_ONLINE
using System;
using System.Threading.Tasks;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace FartRaft
{
    // One phone hosts the raft. Friends join with a code and only send a joystick.
    [DefaultExecutionOrder(50)]
    public class OnlineRoom : MonoBehaviour
    {
        public static bool Hosting { get; private set; }
        public static bool Joined { get; private set; }
        public static int LocalSeatIndex { get; private set; }
        public static int RemoteCount { get; private set; }

        static OnlineRoom instance;

        GameManager game;
        NetworkDriver driver;
        readonly NetworkConnection[] slots = new NetworkConnection[3];
        NetworkConnection server;
        bool welcomed;
        float sendTimer;
        float helloTimer;
        string joinCode = "";
        int hostStage;

        const byte StateMsg = 1;
        const byte StickMsg = 2;
        const byte HelloMsg = 3;
        const byte WelcomeMsg = 4;
        const byte StartMsg = 5;
        const byte FinishMsg = 6;
        const byte StageMsg = 7;

        public static void Attach(GameManager manager)
        {
            if (instance != null) instance.game = manager;
        }

        public static void BeginHost(GameManager manager) => Room().StartHost(manager);
        public static void BeginJoin(GameManager manager, string code) => Room().StartJoin(manager, code);

        public static void Leave(GameManager manager)
        {
            if (instance == null) return;
            instance.Shutdown();
            if (manager != null) manager.SetOnlineStatus("Left the room.");
        }

        public static void SendStart(int count)
        {
            if (instance != null && Hosting) instance.BroadcastStart((byte)count);
        }

        public static void SendStage(int stage)
        {
            if (instance == null || !Hosting) return;
            instance.hostStage = stage;
            instance.BroadcastStage((byte)stage);
        }

        public static void SendFinish(string text)
        {
            if (instance != null && Hosting) instance.BroadcastFinish(text);
        }

        static OnlineRoom Room()
        {
            if (instance != null) return instance;
            var go = new GameObject("OnlineRoom");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<OnlineRoom>();
            return instance;
        }

        async void StartHost(GameManager manager)
        {
            game = manager;
            manager.ShowOnlinePanel();
            Shutdown();
            manager.SetOnlineStatus("Starting a room...");
            try
            {
                await SignIn();
                var allocation = await RelayService.Instance.CreateAllocationAsync(3);
                BindHost(allocation);
                joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                Hosting = true;
                Joined = false;
                LocalSeatIndex = 0;
                hostStage = manager.stageIndex;
                manager.SetOnlineCode(joinCode);
                manager.SetOnlineStatus("Tell your friends this code. They tap ONLINE, type it, and tap JOIN.");
            }
            catch (Exception ex)
            {
                Shutdown();
                manager.SetOnlineStatus(Short(ex));
            }
        }

        async void StartJoin(GameManager manager, string code)
        {
            game = manager;
            manager.ShowOnlinePanel();
            code = string.IsNullOrWhiteSpace(code) ? "" : code.Trim().ToUpperInvariant();
            if (code.Length < 4)
            {
                manager.SetOnlineStatus("Type the host's code first.");
                return;
            }
            Shutdown();
            manager.SetOnlineStatus("Joining " + code + "...");
            try
            {
                await SignIn();
                var allocation = await RelayService.Instance.JoinAllocationAsync(code);
                BindClient(allocation);
                Joined = true;
                Hosting = false;
                joinCode = code;
                welcomed = false;
                helloTimer = 0f;
                manager.SetOnlineStatus("Connecting to the host...");
            }
            catch (Exception ex)
            {
                Shutdown();
                manager.SetOnlineStatus(Short(ex));
            }
        }

        static async Task SignIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        static string Short(Exception ex)
        {
            string message = ex.Message ?? "Online failed.";
            if (message.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Link this project first: Edit > Project Settings > Services. Then turn Relay on in the Unity dashboard.";
            if (message.Length > 160) message = message.Substring(0, 160);
            return message;
        }

        void BindHost(Allocation allocation)
        {
            var serverData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref serverData);
            driver = NetworkDriver.Create(settings);
            if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0 || driver.Listen() != 0)
                throw new InvalidOperationException("Could not open the room.");
        }

        void BindClient(JoinAllocation allocation)
        {
            var serverData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref serverData);
            driver = NetworkDriver.Create(settings);
            if (driver.Bind(NetworkEndpoint.AnyIpv4) != 0)
                throw new InvalidOperationException("Could not join the room.");
            server = driver.Connect();
        }

        void Update()
        {
            if (game == null) game = GameManager.Instance;
            if (!driver.IsCreated) return;
            driver.ScheduleUpdate().Complete();
            if (Hosting) UpdateHost();
            else if (Joined) UpdateClient();
        }

        void UpdateHost()
        {
            NetworkConnection incoming;
            while ((incoming = driver.Accept()) != default)
            {
                int free = FreeSlot();
                if (free < 0) driver.Disconnect(incoming);
                else slots[free] = incoming;
            }

            int alive = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsCreated) continue;
                DataStreamReader stream;
                NetworkEvent.Type type;
                while ((type = driver.PopEventForConnection(slots[i], out stream)) != NetworkEvent.Type.Empty)
                {
                    if (type == NetworkEvent.Type.Data) ReadClient(i, stream);
                    else if (type == NetworkEvent.Type.Disconnect) slots[i] = default;
                }
                if (slots[i].IsCreated) alive++;
            }
            if (alive != RemoteCount)
            {
                RemoteCount = alive;
                if (game != null) game.SetOnlineHeadcount(alive + 1);
            }

            sendTimer += Time.deltaTime;
            if (sendTimer < 0.05f) return;
            sendTimer = 0f;
            BroadcastState();
        }

        int FreeSlot()
        {
            for (int i = 0; i < slots.Length; i++)
                if (!slots[i].IsCreated) return i;
            return -1;
        }

        void UpdateClient()
        {
            if (!server.IsCreated) return;
            DataStreamReader stream;
            NetworkEvent.Type type;
            while ((type = server.PopEvent(driver, out stream)) != NetworkEvent.Type.Empty)
            {
                if (type == NetworkEvent.Type.Data) ReadHost(stream);
                else if (type == NetworkEvent.Type.Disconnect)
                {
                    if (game != null) game.SetOnlineStatus("The host left.");
                    Shutdown();
                    return;
                }
            }

            if (!welcomed)
            {
                helloTimer += Time.deltaTime;
                if (helloTimer > 0.5f)
                {
                    helloTimer = 0f;
                    SendHello();
                }
                return;
            }
            SendStick();
        }

        void ReadClient(int slot, DataStreamReader stream)
        {
            if (stream.Length < 2 || game == null) return;
            byte kind = stream.ReadByte();
            int seat = slot + 1;
            if (kind == HelloMsg)
            {
                int character = stream.ReadByte();
                game.ApplyRemoteStick(seat, Vector2.zero, character);
                SendWelcome(slots[slot], (byte)seat);
                game.SetOnlineStatus(RemoteCount + " in the room. Code " + joinCode);
            }
            else if (kind == StickMsg && stream.Length >= 10)
            {
                float x = stream.ReadFloat();
                float y = stream.ReadFloat();
                int character = stream.ReadByte();
                game.ApplyRemoteStick(seat, new Vector2(x, y), character);
            }
        }

        void ReadHost(DataStreamReader stream)
        {
            if (stream.Length < 1 || game == null) return;
            byte kind = stream.ReadByte();
            if (kind == WelcomeMsg && stream.Length >= 3)
            {
                LocalSeatIndex = stream.ReadByte();
                hostStage = stream.ReadByte();
                welcomed = true;
                game.SetOnlineStatus("You are player " + (LocalSeatIndex + 1) + ". Wait for the host to press PLAY.");
                if (hostStage != game.stageIndex) game.GoToStage(hostStage);
            }
            else if (kind == StartMsg && stream.Length >= 2)
            {
                game.StartJoined(stream.ReadByte());
            }
            else if (kind == StateMsg)
            {
                ApplyState(stream);
            }
            else if (kind == FinishMsg)
            {
                game.ShowNetFinish(ReadText(stream));
            }
            else if (kind == StageMsg && stream.Length >= 2)
            {
                int stage = stream.ReadByte();
                if (stage != game.stageIndex) game.GoToStage(stage);
            }
        }

        void ApplyState(DataStreamReader stream)
        {
            if (stream.Length < 24) return;
            var pos = new Vector3(stream.ReadFloat(), stream.ReadFloat(), stream.ReadFloat());
            float yaw = stream.ReadFloat();
            bool hostRacing = stream.ReadByte() == 1;
            float hostTime = stream.ReadFloat();
            int count = stream.ReadByte();
            var net = new GameManager.NetSeat[4];
            for (int i = 0; i < 4; i++)
            {
                net[i].active = stream.ReadByte() == 1;
                net[i].angle = stream.ReadFloat();
                net[i].character = stream.ReadByte();
            }
            game.ApplyNetState(pos, yaw, hostRacing, hostTime, net, count);
        }

        void SendHello()
        {
            if (!server.IsCreated || driver.BeginSend(server, out var writer) != 0) return;
            writer.WriteByte(HelloMsg);
            writer.WriteByte((byte)Mathf.Clamp(game != null ? game.LocalCharacter() : 0, 0, 255));
            driver.EndSend(writer);
        }

        void SendStick()
        {
            if (game == null || !server.IsCreated || driver.BeginSend(server, out var writer) != 0) return;
            Vector2 stick = game.LocalStick();
            writer.WriteByte(StickMsg);
            writer.WriteFloat(stick.x);
            writer.WriteFloat(stick.y);
            writer.WriteByte((byte)Mathf.Clamp(game.LocalCharacter(), 0, 255));
            driver.EndSend(writer);
        }

        void SendWelcome(NetworkConnection conn, byte seat)
        {
            if (!conn.IsCreated || driver.BeginSend(conn, out var writer) != 0) return;
            writer.WriteByte(WelcomeMsg);
            writer.WriteByte(seat);
            writer.WriteByte((byte)Mathf.Clamp(hostStage, 0, 255));
            driver.EndSend(writer);
        }

        void BroadcastStart(byte count)
        {
            ForEachClient((conn) =>
            {
                if (driver.BeginSend(conn, out var writer) != 0) return;
                writer.WriteByte(StartMsg);
                writer.WriteByte(count);
                driver.EndSend(writer);
            });
        }

        void BroadcastStage(byte stage)
        {
            ForEachClient((conn) =>
            {
                if (driver.BeginSend(conn, out var writer) != 0) return;
                writer.WriteByte(StageMsg);
                writer.WriteByte(stage);
                driver.EndSend(writer);
            });
        }

        void BroadcastFinish(string text)
        {
            ForEachClient((conn) =>
            {
                if (driver.BeginSend(conn, out var writer) != 0) return;
                writer.WriteByte(FinishMsg);
                WriteText(ref writer, text);
                driver.EndSend(writer);
            });
        }

        void BroadcastState()
        {
            if (game == null || !game.TryNetSnapshot(out Vector3 pos, out float yaw, out bool racing, out float time, out int count, out GameManager.NetSeat[] seats))
                return;
            ForEachClient((conn) =>
            {
                if (driver.BeginSend(conn, out var writer) != 0) return;
                writer.WriteByte(StateMsg);
                writer.WriteFloat(pos.x);
                writer.WriteFloat(pos.y);
                writer.WriteFloat(pos.z);
                writer.WriteFloat(yaw);
                writer.WriteByte((byte)(racing ? 1 : 0));
                writer.WriteFloat(time);
                writer.WriteByte((byte)count);
                for (int i = 0; i < 4; i++)
                {
                    writer.WriteByte((byte)(seats[i].active ? 1 : 0));
                    writer.WriteFloat(seats[i].angle);
                    writer.WriteByte((byte)Mathf.Clamp(seats[i].character, 0, 255));
                }
                driver.EndSend(writer);
            });
        }

        void ForEachClient(Action<NetworkConnection> send)
        {
            if (!driver.IsCreated) return;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsCreated) send(slots[i]);
        }

        static void WriteText(ref DataStreamWriter writer, string text)
        {
            if (string.IsNullOrEmpty(text)) text = "";
            if (text.Length > 80) text = text.Substring(0, 80);
            writer.WriteByte((byte)text.Length);
            for (int i = 0; i < text.Length; i++) writer.WriteByte((byte)text[i]);
        }

        static string ReadText(DataStreamReader stream)
        {
            if (stream.Length < 1) return "";
            int length = stream.ReadByte();
            var chars = new char[length];
            for (int i = 0; i < length && stream.Length > 0; i++) chars[i] = (char)stream.ReadByte();
            return new string(chars);
        }

        void Shutdown()
        {
            Hosting = false;
            Joined = false;
            welcomed = false;
            RemoteCount = 0;
            LocalSeatIndex = 0;
            if (driver.IsCreated)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i].IsCreated) driver.Disconnect(slots[i]);
                    slots[i] = default;
                }
                if (server.IsCreated) driver.Disconnect(server);
                driver.Dispose();
                driver = default;
            }
            server = default;
        }

        void OnDestroy()
        {
            Shutdown();
            if (instance == this) instance = null;
        }
    }
}
#endif
