using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LanMultiplayer : MonoBehaviour
{
    public static LanMultiplayer Instance { get; private set; }
    private const ushort Port = 7777;
    private const int MaxPlayers = 4;
    private const int BufferSize = 4096;
    private const string SceneReady = "TBS_SCENE_READY_V2";
    private const string ActionRejected = "TBS_ACTION_REJECTED_V2";
    private const string Hello = "TBS_HELLO";
    private const string Snapshot = "TBS_SNAPSHOT";
    
    private const string Action = "TBS_ACTION";
    private const string MoveApplied = "TBS_MOVE_APPLIED";
    private const string MenuSceneName = "MenuScene";
    private const string GameSceneName = "SampleScene";
    private const string EndTurn = "TBS_END_TURN";
    private const string Kick = "TBS_KICK";

    private enum Phase : byte { Menu, Lobby, Match, Finished, Loading }
    private sealed class Participant
    {
        public ulong Id;
        public int Slot;
        public string Name;
        public bool Spectator;
        public bool Banned;
        public int Resources;
        public int Actions;
    }

    [SerializeField] private string hostAddress = "127.0.0.1";
    [SerializeField] private string playerName = "Player";
    [SerializeField] private int turnSeconds = 60;
    [SerializeField] private int initialResources = 10;
    [SerializeField] private int citiesAndKings = 1;

    private NetworkManager manager;
    private UnityTransport transport;
    private CustomMessagingManager messages;
    private NetworkSceneManager registeredScenes;
    private readonly Dictionary<ulong, string> readyScenes = new Dictionary<ulong, string>();
    private bool sceneLoadCompleted;
    private bool sceneReadySent;
    private float loadingDeadline;
    private readonly Dictionary<ulong, Participant> participants = new Dictionary<ulong, Participant>();
    private readonly HashSet<ulong> bannedIds = new HashSet<ulong>();
    private Phase phase = Phase.Menu;
    private int currentSlot = -1;
    private int localSlot = -1;
    private float turnDeadline;
    private float nextSnapshot;
    private bool localSpectator;
    private bool usesNetworkRules;
    private bool returnToMenuRequested;
    private bool shutdownRequested;
    private string status = "Создай игру или подключись к хосту.";

    public bool UsesNetworkRules => usesNetworkRules;
    public bool IsSessionActive => manager != null && manager.IsListening
        && !manager.ShutdownInProgress && !returnToMenuRequested && phase == Phase.Match;
    public bool IsLocalSpectator => localSpectator;
    public int LocalActions => FindLocal()?.Actions ?? 0;
    public int LocalResources => FindLocal()?.Resources ?? 0;
    public int CurrentActions => participants.Values.FirstOrDefault(item => item.Slot == currentSlot)?.Actions ?? 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeObject()
    {
        if (Instance != null) return;
        GameObject root = new GameObject("LAN Multiplayer");
        DontDestroyOnLoad(root);
        root.AddComponent<LanMultiplayer>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        playerName = string.IsNullOrWhiteSpace(playerName) ? SystemInfo.deviceName : playerName;
        Application.runInBackground = true;
        manager = gameObject.AddComponent<NetworkManager>();
        transport = gameObject.AddComponent<UnityTransport>();
        
        if (manager.NetworkConfig == null)
            manager.NetworkConfig = new NetworkConfig();
        manager.NetworkConfig.NetworkTransport = transport;
        manager.NetworkConfig.NetworkTopology = NetworkTopologyTypes.ClientServer;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.NetworkConfig.ProtocolVersion = 2;
        manager.NetworkConfig.EnableSceneManagement = true;
        manager.ConnectionApprovalCallback = Approve;
        manager.OnClientConnectedCallback += Connected;
        manager.OnClientDisconnectCallback += Disconnected;
        
        manager.OnServerStarted += RegisterMessageHandlers;
        manager.OnClientStarted += RegisterMessageHandlers;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private void RegisterMessageHandlers()
    {
        CustomMessagingManager startedMessages = manager.CustomMessagingManager;
        if (startedMessages == null)
            throw new InvalidOperationException("NGO запущен без CustomMessagingManager.");

        
        if (ReferenceEquals(messages, startedMessages))
            return;

        messages = startedMessages;
        messages.RegisterNamedMessageHandler(Hello, ReceiveHello);
        messages.RegisterNamedMessageHandler(Snapshot, ReceiveSnapshot);
        messages.RegisterNamedMessageHandler(Action, ReceiveAction);
        messages.RegisterNamedMessageHandler(MoveApplied, ReceiveMoveApplied);
        messages.RegisterNamedMessageHandler(EndTurn, ReceiveEndTurn);
        messages.RegisterNamedMessageHandler(Kick, ReceiveKick);
        messages.RegisterNamedMessageHandler(SceneReady, ReceiveSceneReady);
        messages.RegisterNamedMessageHandler(ActionRejected, ReceiveActionRejected);
        if (registeredScenes != manager.SceneManager)
        {
            if (registeredScenes != null) registeredScenes.OnLoadEventCompleted -= NetworkSceneLoaded;
            registeredScenes = manager.SceneManager;
            if (registeredScenes != null) registeredScenes.OnLoadEventCompleted += NetworkSceneLoaded;
        }
    }

    private void OnDestroy()
    {
        if (registeredScenes != null) registeredScenes.OnLoadEventCompleted -= NetworkSceneLoaded;
        if (manager == null) return;
        manager.OnServerStarted -= RegisterMessageHandlers;
        manager.OnClientStarted -= RegisterMessageHandlers;
        SceneManager.sceneLoaded -= SceneLoaded;
        manager.OnClientConnectedCallback -= Connected;
        manager.OnClientDisconnectCallback -= Disconnected;
    }

    private void Update()
    {
        if (manager == null) return;
        if (returnToMenuRequested)
        {
            UpdateReturnToMenu();
            return;
        }
        if (phase == Phase.Loading && manager.IsListening)
        {
            UpdateLoading();
            return;
        }
        if (IsSessionActive) GetSceneReplicator();
        if (!IsSessionActive || !manager.IsServer) return;
        if (Time.unscaledTime >= turnDeadline) AdvanceTurn("Время хода закончилось.");
        if (Time.unscaledTime >= nextSnapshot)
        {
            BroadcastSnapshot();
            nextSnapshot = Time.unscaledTime + .25f;
        }
    }

    private LanSceneReplicator GetSceneReplicator()
    {
        if (!usesNetworkRules || returnToMenuRequested
            || SceneManager.GetActiveScene().name != GameSceneName) return null;
        if (LanSceneReplicator.Instance == null)
            new GameObject("LAN Scene Replicator").AddComponent<LanSceneReplicator>();
        return LanSceneReplicator.Instance;
    }

    private void NetworkSceneLoaded(string sceneName, LoadSceneMode mode,
        List<ulong> completed, List<ulong> timedOut)
    {
        if (sceneName != GameSceneName || returnToMenuRequested) return;
        sceneLoadCompleted = true;
        if (manager.IsServer && phase == Phase.Loading && ActivePlayers().Any(item => !completed.Contains(item.Id)))
            FailLoading("Не все игроки загрузили сцену. Пересоздай матч.");
    }

    private void UpdateLoading()
    {
        if (manager.IsServer && Time.unscaledTime >= loadingDeadline)
        {
            FailLoading("Истекло время ожидания сцены. Проверь одинаковые сборки у всех игроков.");
            return;
        }
        if (sceneLoadCompleted && !sceneReadySent)
        {
            LanSceneReplicator replicator = GetSceneReplicator();
            if (replicator == null) return;
            replicator.Refresh();
            string error = replicator.ValidationError;
            string signature = replicator.IsReady ? replicator.LayoutSignature : string.Empty;
            sceneReadySent = true;
            if (manager.IsServer) AcceptSceneReady(manager.LocalClientId, signature, error);
            else
            {
                using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
                {
                    writer.WriteValueSafe(signature);
                    writer.WriteValueSafe(error);
                    messages.SendNamedMessage(SceneReady, NetworkManager.ServerClientId, writer);
                }
            }
        }
        if (manager.IsServer) TryBeginMatch();
    }

    private void ReceiveSceneReady(ulong id, FastBufferReader reader)
    {
        if (!manager.IsServer || phase != Phase.Loading || returnToMenuRequested) return;
        reader.ReadValueSafe(out string signature);
        reader.ReadValueSafe(out string error);
        AcceptSceneReady(id, signature, error);
    }

    private void AcceptSceneReady(ulong id, string signature, string error)
    {
        if (phase != Phase.Loading || !participants.TryGetValue(id, out Participant item)
            || item.Spectator || item.Banned) return;
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(signature))
        {
            FailLoading(item.Name + ": " + (string.IsNullOrEmpty(error) ? "нет данных фигур." : error));
            return;
        }
        readyScenes[id] = signature;
        TryBeginMatch();
    }

    private void TryBeginMatch()
    {
        if (phase != Phase.Loading || !sceneLoadCompleted || !sceneReadySent) return;
        List<Participant> active = ActivePlayers().OrderBy(item => item.Slot).ToList();
        if (active.Count < 2) { FailLoading("Для матча нужны хотя бы два игрока."); return; }
        if (active.Any(item => !readyScenes.ContainsKey(item.Id))) return;
        LanSceneReplicator replicator = GetSceneReplicator();
        if (replicator == null || !replicator.IsReady) return;
        if (active.Any(item => readyScenes[item.Id] != replicator.LayoutSignature))
        {
            FailLoading("Состав фигур или их команды различаются. Установи одинаковую сцену и пересобери игру на всех ПК.");
            return;
        }
        foreach (Participant item in active)
        {
            if (!replicator.HasTeam((TurnManager.Team)item.Slot))
            {
                FailLoading("Нет фигуры команды " + (TurnManager.Team)item.Slot + " для " + item.Name + ". Проверь FigureTurn.");
                return;
            }
            item.Resources = initialResources;
            item.Actions = Math.Max(1, citiesAndKings);
        }
        phase = Phase.Match;
        currentSlot = participants[manager.LocalClientId].Slot;
        turnDeadline = Time.unscaledTime + Mathf.Max(5, turnSeconds);
        status = "Все игроки готовы. Первый ход Host.";
        BroadcastSnapshot();
    }

    private void FailLoading(string reason)
    {
        phase = Phase.Finished;
        currentSlot = -1;
        status = reason.Length > 300 ? reason.Substring(0, 300) : reason;
        Debug.LogError("[LAN] " + status);
        BroadcastSnapshot();
    }

    public void CreateGame()
    {
        if (returnToMenuRequested || manager.IsListening || manager.ShutdownInProgress) return;
        participants.Clear();
        bannedIds.Clear();
        readyScenes.Clear();
        sceneLoadCompleted = sceneReadySent = false;
        usesNetworkRules = true;
        localSpectator = true;
        localSlot = currentSlot = -1;
        transport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
        if (!manager.StartHost())
        {
            usesNetworkRules = false;
            status = "Host не запустился: проверь UDP-порт 7777.";
            return;
        }
        phase = Phase.Lobby;
        AddParticipant(manager.LocalClientId, "Host", false);
        localSlot = 0;
        localSpectator = false;
        status = "Лобби создано. Нужен хотя бы один клиент.";
        BroadcastSnapshot();
    }

    public void JoinGame()
    {
        if (returnToMenuRequested || manager.IsListening || manager.ShutdownInProgress) return;
        if (!IPAddress.TryParse(hostAddress.Trim(), out IPAddress address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            status = "Введи IPv4 хоста, например 192.168.1.25.";
            return;
        }
        readyScenes.Clear();
        sceneLoadCompleted = sceneReadySent = false;
        usesNetworkRules = true;
        localSpectator = true;
        localSlot = currentSlot = -1;
        transport.SetConnectionData(address.ToString(), Port);
        if (manager.StartClient()) status = "Подключение...";
        else
        {
            usesNetworkRules = false;
            status = "Client не запустился.";
        }
    }

    public void Disconnect()
    {
        RequestReturnToMenu("Соединение закрыто.");
    }

    private void RequestReturnToMenu(string reason)
    {
        
        if (!returnToMenuRequested) status = reason;
        returnToMenuRequested = true;
        usesNetworkRules = true;
        localSpectator = true;
        localSlot = currentSlot = -1;
        phase = Phase.Menu;
        foreach (TilesMovement movement in FindObjectsByType<TilesMovement>(FindObjectsSortMode.None))
            movement.UnSelect();
    }

    private void UpdateReturnToMenu()
    {
        
        if (manager.IsListening || manager.ShutdownInProgress)
        {
            if (!shutdownRequested)
            {
                shutdownRequested = true;
                if (!manager.ShutdownInProgress) manager.Shutdown();
            }
            return;
        }
        if (SceneManager.GetActiveScene().name == MenuSceneName) CompleteReturnToMenu();
        else SceneManager.LoadScene(MenuSceneName);
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (returnToMenuRequested && scene.name == MenuSceneName
            && !manager.IsListening && !manager.ShutdownInProgress) CompleteReturnToMenu();
    }

    private void CompleteReturnToMenu()
    {
        participants.Clear();
        bannedIds.Clear();
        messages = null;
        phase = Phase.Menu;
        localSlot = currentSlot = -1;
        localSpectator = false;
        returnToMenuRequested = false;
        shutdownRequested = false;
        usesNetworkRules = false;
    }

    private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool fullLobby = phase != Phase.Match && manager.ConnectedClientsIds.Count >= MaxPlayers;
        response.Approved = !fullLobby && !returnToMenuRequested;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = fullLobby ? "Лобби заполнено: максимум четыре игрока." : string.Empty;
    }

    private void Connected(ulong id)
    {
        if (returnToMenuRequested) return;
        if (manager.IsServer)
        {
            if (id == manager.LocalClientId) BroadcastSnapshot();
            else status = "Клиент подключён. Ожидаю его имя...";
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe(playerName.Trim());
            messages.SendNamedMessage(Hello, NetworkManager.ServerClientId, writer);
        }
        status = "Подключено. Ожидание лобби...";
    }

    private void Disconnected(ulong id)
    {
        if (manager.IsServer && id != manager.LocalClientId)
        {
            bool wasCurrent = participants.TryGetValue(id, out Participant removed)
                && removed.Slot == currentSlot;
            participants.Remove(id);
            readyScenes.Remove(id);
            if (phase == Phase.Loading) { FailLoading("Игрок отключился при загрузке. Пересоздай матч."); return; }
            if (phase == Phase.Match && wasCurrent) AdvanceTurn("Игрок отключился.");
            else BroadcastSnapshot();
            return;
        }
        
        string reason = string.IsNullOrEmpty(manager.DisconnectReason)
            ? "Хост закрыл соединение." : manager.DisconnectReason;
        RequestReturnToMenu(reason);
    }

    private void ReceiveHello(ulong id, FastBufferReader reader)
    {
        if (!manager.IsServer || returnToMenuRequested) return;
        reader.ReadValueSafe(out string name);
        bool spectator = phase == Phase.Match || phase == Phase.Loading || phase == Phase.Finished || bannedIds.Contains(id) || ActivePlayers().Count() >= MaxPlayers;
        AddParticipant(id, string.IsNullOrWhiteSpace(name) ? "Player" : name, spectator);
        BroadcastSnapshot();
    }

    private void AddParticipant(ulong id, string name, bool spectator)
    {
        if (participants.TryGetValue(id, out Participant existing)) { existing.Name = name; existing.Spectator = spectator; return; }
        int slot = spectator ? -1 : Enumerable.Range(0, MaxPlayers).FirstOrDefault(value => participants.Values.All(item => item.Slot != value));
        participants[id] = new Participant { Id = id, Slot = slot, Name = name, Spectator = spectator, Banned = bannedIds.Contains(id), Resources = initialResources, Actions = Math.Max(1, citiesAndKings) };
    }

    public void StartMatch()
    {
        if (!manager.IsServer || returnToMenuRequested || phase != Phase.Lobby) return;
        List<Participant> active = ActivePlayers().OrderBy(item => item.Slot).ToList();
        if (active.Count < 2) { status = "Для старта нужен Host и хотя бы один клиент."; return; }
        if (manager.SceneManager == null) { status = "Недоступна сетевая загрузка сцены."; return; }
        phase = Phase.Loading;
        currentSlot = -1;
        turnDeadline = 0;
        loadingDeadline = Time.unscaledTime + 180f;
        readyScenes.Clear();
        sceneLoadCompleted = sceneReadySent = false;
        status = "Загрузка сцены и проверка фигур...";
        BroadcastSnapshot();
        SceneEventProgressStatus result = manager.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        if (result != SceneEventProgressStatus.Started) FailLoading("Не удалось загрузить сцену: " + result);
    }

    public void CancelMatch()
    {
        if (!manager.IsServer || (phase != Phase.Match && phase != Phase.Loading)) return;
        phase = Phase.Finished; status = "Матч отменён хостом."; BroadcastSnapshot();
    }

    public void BanClient(ulong id)
    {
        if (!manager.IsServer || phase != Phase.Match || returnToMenuRequested
            || id == manager.LocalClientId || !participants.TryGetValue(id, out Participant item)) return;
        bannedIds.Add(id);
        item.Banned = true;
        item.Spectator = true;
        item.Actions = 0;
        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe("Вы забанены до конца матча. Можно переподключиться зрителем.");
            messages.SendNamedMessage(Kick, id, writer);
        }
        if (item.Slot == currentSlot) AdvanceTurn("Игрок забанен.");
        else BroadcastSnapshot();
        manager.DisconnectClient(id, "Вы исключены хостом до конца матча.");
    }

    
    public void ReportCheckmate(ulong id)
    {
        if (!manager.IsServer || !participants.TryGetValue(id, out Participant item)) return;
        item.Spectator = true; item.Actions = 0;
        if (item.Slot == currentSlot) AdvanceTurn("Мат: игрок перешёл в Spectator."); else BroadcastSnapshot();
    }

    public bool CanLocalTeamAct(TurnManager.Team team)
    {
        Participant local = FindLocal();
        return IsSessionActive && !localSpectator && local != null
            && !local.Spectator && !local.Banned && (int)team == localSlot
            && local.Slot == currentSlot && local.Actions > 0;
    }

    public bool CanLocalFigureAct(TilesMovement movement)
    {
        return movement != null && movement.TryGetComponent(out FigureTurn figureTurn)
            && CanLocalTeamAct(figureTurn.Team);
    }

    public void NotifyLocalMovement(TilesMovement movement, int x, int y)
    {
        if (!CanLocalFigureAct(movement)) return;
        LanSceneReplicator replicator = GetSceneReplicator();
        if (replicator == null || !replicator.TryGetId(movement, out string figureId))
        {
            status = "Фигура не зарегистрирована. Проверь Network Id в FigureTurn и перезапусти матч.";
            Debug.LogError("[LAN] " + status);
            return;
        }
        if (manager.IsServer) HandleAction(manager.LocalClientId, figureId, x, y);
        else
        {
            using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
            {
                writer.WriteValueSafe(figureId);
                writer.WriteValueSafe(x);
                writer.WriteValueSafe(y);
                messages.SendNamedMessage(Action, NetworkManager.ServerClientId, writer);
            }
        }
    }

    
    private void ReceiveAction(ulong id, FastBufferReader reader)
    {
        if (!manager.IsServer || returnToMenuRequested) return;
        reader.ReadValueSafe(out string figureId);
        reader.ReadValueSafe(out int x);
        reader.ReadValueSafe(out int y);
        HandleAction(id, figureId, x, y);
    }

    
    private void ReceiveMoveApplied(ulong id, FastBufferReader reader)
    {
        if (manager.IsServer || id != NetworkManager.ServerClientId || returnToMenuRequested) return;
        reader.ReadValueSafe(out string figureId);
        reader.ReadValueSafe(out int x);
        reader.ReadValueSafe(out int y);
        if (!IsSessionActive) return;
        LanSceneReplicator replicator = GetSceneReplicator();
        if (replicator == null || !replicator.ApplyNetworkMove(figureId, x, y))
            RequestReturnToMenu("Не найдена фигура " + figureId + ". Сцены различаются; установи одну сборку на всех ПК.");
    }

    private void HandleAction(ulong id, string figureId, int x, int y)
    {
        if (!manager.IsServer || !IsSessionActive
            || !participants.TryGetValue(id, out Participant item)
            || item.Spectator || item.Banned || item.Slot != currentSlot || item.Actions <= 0) return;
        LanSceneReplicator replicator = GetSceneReplicator();
        TilesMovement movement = replicator == null ? null : replicator.GetFigure(figureId);
        if (movement == null || !movement.TryGetComponent(out FigureTurn figureTurn)
            || (int)figureTurn.Team != item.Slot)
        {
            RejectAction(id, "Фигура не найдена или принадлежит другой команде. Проверь FigureTurn и Network Id.");
            return;
        }

        if (!replicator.ApplyNetworkMove(figureId, x, y)) return;
        item.Actions--;
        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe(figureId);
            writer.WriteValueSafe(x);
            writer.WriteValueSafe(y);
            messages.SendNamedMessageToAll(MoveApplied, writer);
        }
        if (item.Actions <= 0) AdvanceTurn("Действия игрока закончились.");
        else BroadcastSnapshot();
    }

    private void RejectAction(ulong id, string reason)
    {
        if (id == manager.LocalClientId) { status = reason; Debug.LogWarning("[LAN] " + reason); return; }
        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe(reason);
            messages.SendNamedMessage(ActionRejected, id, writer);
        }
    }

    private void ReceiveActionRejected(ulong id, FastBufferReader reader)
    {
        if (manager.IsServer || id != NetworkManager.ServerClientId || returnToMenuRequested) return;
        reader.ReadValueSafe(out string reason);
        Debug.LogWarning("[LAN] " + reason);
        status = reason;
    }

    public void EndLocalTurn()
    {
        if (!IsSessionActive || localSpectator) return;
        if (manager.IsServer) HandleEndTurn(manager.LocalClientId);
        else using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp)) messages.SendNamedMessage(EndTurn, NetworkManager.ServerClientId, writer);
    }

    private void ReceiveEndTurn(ulong id, FastBufferReader reader) { if (manager.IsServer && IsSessionActive) HandleEndTurn(id); }
    private void HandleEndTurn(ulong id) { if (IsSessionActive && participants.TryGetValue(id, out Participant item) && item.Slot == currentSlot && !item.Spectator && !item.Banned) AdvanceTurn("Игрок завершил ход."); }

    private void AdvanceTurn(string reason)
    {
        List<Participant> active = ActivePlayers().OrderBy(item => item.Slot).ToList();
        if (active.Count <= 1) { phase = Phase.Finished; status = reason + " Матч завершён."; BroadcastSnapshot(); return; }
        int index = active.FindIndex(item => item.Slot == currentSlot);
        Participant next = active[(index + 1 + active.Count) % active.Count];
        currentSlot = next.Slot; next.Actions = Math.Max(1, citiesAndKings); turnDeadline = Time.unscaledTime + Mathf.Max(5, turnSeconds);
        status = reason + " Ход: " + next.Name + "."; BroadcastSnapshot();
    }

    private IEnumerable<Participant> ActivePlayers() { return participants.Values.Where(item => !item.Spectator && !item.Banned && item.Slot >= 0); }
    private Participant FindLocal() { participants.TryGetValue(manager.LocalClientId, out Participant item); return item; }

    private void BroadcastSnapshot()
    {
        if (!manager.IsServer || !manager.IsListening) return;
        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe((byte)phase); writer.WriteValueSafe(currentSlot); writer.WriteValueSafe(Mathf.Max(0, turnDeadline - Time.unscaledTime)); writer.WriteValueSafe(participants.Count);
            foreach (Participant item in participants.Values.OrderBy(item => item.Id))
            {
                writer.WriteValueSafe(item.Id); writer.WriteValueSafe(item.Slot); writer.WriteValueSafe(item.Name); writer.WriteValueSafe(item.Spectator); writer.WriteValueSafe(item.Banned); writer.WriteValueSafe(item.Resources); writer.WriteValueSafe(item.Actions);
            }
            writer.WriteValueSafe(status);
            messages.SendNamedMessageToAll(Snapshot, writer);
        }
    }

    private void ReceiveSnapshot(ulong id, FastBufferReader reader)
    {
        if (manager.IsServer || id != NetworkManager.ServerClientId || returnToMenuRequested) return;
        usesNetworkRules = true;
        reader.ReadValueSafe(out byte phaseValue); phase = (Phase)phaseValue; reader.ReadValueSafe(out currentSlot); reader.ReadValueSafe(out float remaining); turnDeadline = Time.unscaledTime + remaining; reader.ReadValueSafe(out int count); participants.Clear();
        for (int i = 0; i < count; i++)
        {
            Participant item = new Participant(); reader.ReadValueSafe(out item.Id); reader.ReadValueSafe(out item.Slot); reader.ReadValueSafe(out item.Name); reader.ReadValueSafe(out item.Spectator); reader.ReadValueSafe(out item.Banned); reader.ReadValueSafe(out item.Resources); reader.ReadValueSafe(out item.Actions); participants[item.Id] = item;
        }
        reader.ReadValueSafe(out status);
        Participant local = FindLocal(); localSlot = local == null ? -1 : local.Slot; localSpectator = local == null || local.Spectator || local.Banned;
    }

    private void ReceiveKick(ulong id, FastBufferReader reader)
    {
        if (manager.IsServer || id != NetworkManager.ServerClientId) return;
        reader.ReadValueSafe(out string reason);
        RequestReturnToMenu(reason);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 390, 520), GUI.skin.box); GUILayout.Label("TBS — локальная сеть v2");
        if (returnToMenuRequested) GUILayout.Label("Возврат в меню...");
        else if (!manager.IsListening)
        {
            GUILayout.Label("Имя"); playerName = GUILayout.TextField(playerName); GUILayout.Label("IPv4 хоста"); hostAddress = GUILayout.TextField(hostAddress);
            if (GUILayout.Button("Создать игру (Host)")) CreateGame(); if (GUILayout.Button("Подключиться (Client)")) JoinGame();
        }
        else if (phase == Phase.Lobby)
        {
            GUILayout.Label("Лобби: " + ActivePlayers().Count() + "/4 игроков"); DrawParticipants();
            if (manager.IsServer)
            {
                GUILayout.Label("Секунд на ход"); turnSeconds = Mathf.Clamp(ParseInt(GUILayout.TextField(turnSeconds.ToString()), turnSeconds), 5, 3600);
                GUILayout.Label("Стартовые ресурсы"); initialResources = Mathf.Max(0, ParseInt(GUILayout.TextField(initialResources.ToString()), initialResources));
                GUILayout.Label("Города + короли = действий"); citiesAndKings = Mathf.Max(1, ParseInt(GUILayout.TextField(citiesAndKings.ToString()), citiesAndKings));
                if (GUILayout.Button("Начать матч")) StartMatch();
            }
        }
        else if (phase == Phase.Loading)
        {
            GUILayout.Label("Ожидание загрузки и проверки фигур у всех игроков...");
            if (manager.IsServer && GUILayout.Button("Отменить матч")) CancelMatch();
        }
        else if (phase == Phase.Match)
        {
            Participant current = participants.Values.FirstOrDefault(item => item.Slot == currentSlot); GUILayout.Label("Ход: " + (current == null ? "..." : current.Name)); GUILayout.Label("Время: " + Mathf.CeilToInt(Mathf.Max(0, turnDeadline - Time.unscaledTime)) + " сек."); GUILayout.Label("Действий у текущего игрока: " + CurrentActions);
            GUILayout.Label("Ваши ресурсы: " + LocalResources + " | Ваши действия: " + LocalActions);
            GUILayout.Label(localSlot < 0 ? "Вы зритель" : "Ваша команда: " + (TurnManager.Team)localSlot); GUILayout.Label(localSpectator ? "SPECTATOR" : "PLAYER"); DrawParticipants();
            if (!localSpectator && localSlot == currentSlot && GUILayout.Button("Завершить ход")) EndLocalTurn(); if (manager.IsServer && GUILayout.Button("Отменить матч")) CancelMatch();
            if (manager.IsServer) foreach (Participant item in ActivePlayers().Where(item => item.Id != manager.LocalClientId).ToList()) if (GUILayout.Button("Забанить " + item.Name)) BanClient(item.Id);
        }
        else if (phase == Phase.Finished) GUILayout.Label(status);
        GUILayout.Space(8); GUILayout.Label(status); if ((usesNetworkRules || manager.IsListening || SceneManager.GetActiveScene().name != MenuSceneName) && GUILayout.Button("Выйти в меню")) Disconnect(); GUILayout.EndArea();
    }

    private void DrawParticipants() { foreach (Participant item in participants.Values.OrderBy(item => item.Slot < 0 ? 99 : item.Slot)) GUILayout.Label(item.Name + " — " + (item.Banned ? "BANNED" : item.Spectator ? "Spectator" : "Player " + (item.Slot + 1))); }
    private static int ParseInt(string value, int fallback) { return int.TryParse(value, out int result) ? result : fallback; }
}


public sealed class LanSceneReplicator : MonoBehaviour
{
    public static LanSceneReplicator Instance { get; private set; }
    private readonly Dictionary<string, TilesMovement> figures = new Dictionary<string, TilesMovement>(StringComparer.Ordinal);
    public bool IsReady { get; private set; }
    public string ValidationError { get; private set; } = string.Empty;
    public string LayoutSignature { get; private set; } = string.Empty;

    private void Awake() { Instance = this; Refresh(); }
    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Invalid(string reason)
    {
        figures.Clear();
        IsReady = false;
        ValidationError = reason.Length > 200 ? reason.Substring(0, 200) : reason;
    }

    public void Refresh()
    {
        figures.Clear();
        IsReady = false;
        ValidationError = LayoutSignature = string.Empty;
        string scenePath = SceneManager.GetActiveScene().path;
        foreach (TilesMovement movement in FindObjectsByType<TilesMovement>(FindObjectsSortMode.None))
        {
            if (movement.gameObject.scene.path != scenePath) continue;
            if (!movement.TryGetComponent(out FigureTurn turn))
            {
                Invalid(movement.name + ": отсутствует FigureTurn."); return;
            }
            if (turn.Movement != movement)
            {
                Invalid(movement.name + ": Tiles Movement в FigureTurn ссылается на другую фигуру."); return;
            }
            if ((int)turn.Team < 0 || (int)turn.Team > 3)
            {
                Invalid(movement.name + ": неверная команда FigureTurn."); return;
            }
            string id = turn.NetworkId;
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || id.Any(ch => !char.IsLetterOrDigit(ch) && ch != '-' && ch != '_'))
            {
                Invalid(movement.name + ": задай Network Id в FigureTurn (до 64 букв, цифр, - или _)."); return;
            }
            if (figures.ContainsKey(id))
            {
                Invalid("Повтор Network Id " + id + ": " + figures[id].name + " и " + movement.name + "."); return;
            }
            figures.Add(id, movement);
        }
        if (figures.Count == 0) { Invalid("В сцене нет управляемых фигур."); return; }
        string layout = string.Join("\n", figures.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair =>
            pair.Key + ":" + ((int)pair.Value.GetComponent<FigureTurn>().Team).ToString(CultureInfo.InvariantCulture)
            + ":" + pair.Value.name.Length.ToString(CultureInfo.InvariantCulture) + ":" + pair.Value.name));
        using (SHA256 hash = SHA256.Create())
            LayoutSignature = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(layout))).Replace("-", string.Empty);
        IsReady = true;
    }

    public bool HasTeam(TurnManager.Team team) => IsReady && figures.Values.Any(movement =>
        movement != null && movement.GetComponent<FigureTurn>().Team == team);

    public bool TryGetId(TilesMovement movement, out string id)
    {
        id = string.Empty;
        if (!IsReady || movement == null || !movement.TryGetComponent(out FigureTurn turn)) return false;
        id = turn.NetworkId;
        return figures.TryGetValue(id, out TilesMovement registered) && registered == movement;
    }

    public TilesMovement GetFigure(string id)
    {
        return IsReady && id != null && figures.TryGetValue(id, out TilesMovement movement) ? movement : null;
    }

    public bool ApplyNetworkMove(string id, int x, int y)
    {
        TilesMovement movement = GetFigure(id);
        if (movement == null) return false;
        movement.ApplyNetworkPosition(x, y);
        return true;
    }
}
