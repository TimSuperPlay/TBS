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
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public sealed class LanMultiplayer : MonoBehaviour
{
    public static LanMultiplayer Instance { get; private set; }

    private const ushort Port = 7777;
    private const int MaxPlayers = 4;
    private const int BufferSize = 4096;
    private const string SceneReady = "TBS_SCENE_READY_V2";
    private const string ActionRejected = "TBS_ACTION_REJECTED_V2";
    private const string Build = "TBS_BUILD_V3";
    private const string World = "TBS_WORLD_V3";
    private const string WorldRequest = "TBS_WORLD_REQUEST_V3";
    private const string Hello = "TBS_HELLO";
    private const string Snapshot = "TBS_SNAPSHOT";
    private const string Action = "TBS_ACTION";
    private const string MoveApplied = "TBS_MOVE_APPLIED";
    private const string MenuSceneName = "MenuScene";
    private const string GameSceneName = "SampleScene";
    private const string EndTurn = "TBS_END_TURN";
    private const string Kick = "TBS_KICK";
    private enum Phase : byte
    {
        Menu,
        Lobby,
        Match,
        Finished,
        Loading
    }

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

    [SerializeField]
    private string hostAddress = "127.0.0.1";
    [SerializeField]
    private string playerName = "Player";
    [SerializeField]
    [FormerlySerializedAs("turnSeconds")]
    private int turnDurationSeconds = 60;
    [SerializeField]
    [FormerlySerializedAs("initialResources")]
    private int startingResources = 10;
    [SerializeField]
    [FormerlySerializedAs("citiesAndKings")]
    private int actionsPerTurn = 1;
    private NetworkManager networkManager;
    private UnityTransport networkTransport;
    private CustomMessagingManager messageManager;
    private NetworkSceneManager networkSceneManager;
    private readonly Dictionary<ulong, string> sceneSignatures = new Dictionary<ulong, string>();
    private bool sceneLoadingCompleted;
    private bool sceneReadyMessageSent;
    private float sceneLoadTimeoutTime;
    private readonly Dictionary<ulong, Participant> participants = new Dictionary<ulong, Participant>();
    private readonly HashSet<ulong> bannedClientIds = new HashSet<ulong>();
    private Phase matchPhase = Phase.Menu;
    private int currentPlayerSlot = -1;
    private int localPlayerSlot = -1;
    private float turnEndTime;
    private float nextSnapshotTime;
    private int lastAcknowledgedRevision = -1;
    private bool isLocalSpectator;
    private bool waitingForActionResponse;
    private bool actionInProgress;
    private bool worldSynchronized;
    private bool worldStateRequested;
    private float actionEndTime;
    private TilesMovement actingFigure;
    private int turnNumber;
    private int worldMessageNumber;
    private int incomingWorldMessageNumber = -1;
    private int nextWorldChunkIndex;
    private string lastSentWorldJson;
    private string pendingWorldJson;
    private StringBuilder worldJsonBuffer = new StringBuilder();
    public bool IsAuthority
    {
        get
        {
            return networkManager != null && networkManager.IsServer && IsSessionActive;
        }
    }

    public bool CanLocalBuild
    {
        get
        {
            return CanLocalTeamAct((TurnManager.Team)localPlayerSlot);
        }
    }

    private LanWorldReplicator GetWorld()
    {
        if (SceneManager.GetActiveScene().name != GameSceneName || returningToMenu)
        {
            return null;
        }

        if (LanWorldReplicator.Instance == null)
        {
            new GameObject("LAN World").AddComponent<LanWorldReplicator>().Initialize();
        }

        return LanWorldReplicator.Instance;
    }

    private bool usesNetworkRules;
    private bool returningToMenu;
    private bool shutdownStarted;
    private string statusMessage = "Создай игру или подключись к хосту.";
    public bool UsesNetworkRules
    {
        get
        {
            return usesNetworkRules;
        }
    }

    public bool IsSessionActive
    {
        get
        {
            return networkManager != null && networkManager.IsListening && !networkManager.ShutdownInProgress && !returningToMenu
                && matchPhase == Phase.Match;
        }
    }

    public bool IsLocalSpectator
    {
        get
        {
            return isLocalSpectator;
        }
    }

    public int LocalActions
    {
        get
        {
            return FindLocal()?.Actions ?? 0;
        }
    }

    public int LocalResources
    {
        get
        {
            return FindLocal()?.Resources ?? 0;
        }
    }

    public int CurrentActions
    {
        get
        {
            return participants.Values.FirstOrDefault(participant => participant.Slot == currentPlayerSlot)?.Actions ?? 0;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateRuntimeObject()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject root = new GameObject("LAN Multiplayer");
        DontDestroyOnLoad(root);
        root.AddComponent<LanMultiplayer>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        playerName = string.IsNullOrWhiteSpace(playerName) ? SystemInfo.deviceName : playerName;
        Application.runInBackground = true;
        networkManager = gameObject.AddComponent<NetworkManager>();
        networkTransport = gameObject.AddComponent<UnityTransport>();
        if (networkManager.NetworkConfig == null)
        {
            networkManager.NetworkConfig = new NetworkConfig();
        }

        networkManager.NetworkConfig.NetworkTransport = networkTransport;
        networkManager.NetworkConfig.NetworkTopology = NetworkTopologyTypes.ClientServer;
        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.NetworkConfig.ProtocolVersion = 3;
        networkManager.NetworkConfig.EnableSceneManagement = true;
        networkManager.ConnectionApprovalCallback = Approve;
        networkManager.OnClientConnectedCallback += Connected;
        networkManager.OnClientDisconnectCallback += Disconnected;
        networkManager.OnServerStarted += RegisterMessageHandlers;
        networkManager.OnClientStarted += RegisterMessageHandlers;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    private void RegisterMessageHandlers()
    {
        CustomMessagingManager startedMessages = networkManager.CustomMessagingManager;
        if (startedMessages == null)
        {
            throw new InvalidOperationException("NGO запущен без CustomMessagingManager.");
        }

        if (ReferenceEquals(messageManager, startedMessages))
        {
            return;
        }

        messageManager = startedMessages;
        messageManager.RegisterNamedMessageHandler(Build, ReceiveBuild);
        messageManager.RegisterNamedMessageHandler(World, ReceiveWorld);
        messageManager.RegisterNamedMessageHandler(WorldRequest, ReceiveWorldRequest);
        messageManager.RegisterNamedMessageHandler(Hello, ReceiveHello);
        messageManager.RegisterNamedMessageHandler(Snapshot, ReceiveSnapshot);
        messageManager.RegisterNamedMessageHandler(Action, ReceiveAction);
        messageManager.RegisterNamedMessageHandler(MoveApplied, ReceiveMoveApplied);
        messageManager.RegisterNamedMessageHandler(EndTurn, ReceiveEndTurn);
        messageManager.RegisterNamedMessageHandler(Kick, ReceiveKick);
        messageManager.RegisterNamedMessageHandler(SceneReady, ReceiveSceneReady);
        messageManager.RegisterNamedMessageHandler(ActionRejected, ReceiveActionRejected);
        if (networkSceneManager != networkManager.SceneManager)
        {
            if (networkSceneManager != null)
            {
                networkSceneManager.OnLoadEventCompleted -= NetworkSceneLoaded;
                networkSceneManager.OnSceneEvent -= NetworkSceneEvent;
            }

            networkSceneManager = networkManager.SceneManager;
            if (networkSceneManager != null)
            {
                networkSceneManager.OnLoadEventCompleted += NetworkSceneLoaded;
                networkSceneManager.OnSceneEvent += NetworkSceneEvent;
            }
        }
    }

    private void OnDestroy()
    {
        if (networkSceneManager != null)
        {
            networkSceneManager.OnLoadEventCompleted -= NetworkSceneLoaded;
            networkSceneManager.OnSceneEvent -= NetworkSceneEvent;
        }

        if (networkManager == null)
        {
            return;
        }

        networkManager.OnServerStarted -= RegisterMessageHandlers;
        networkManager.OnClientStarted -= RegisterMessageHandlers;
        SceneManager.sceneLoaded -= SceneLoaded;
        networkManager.OnClientConnectedCallback -= Connected;
        networkManager.OnClientDisconnectCallback -= Disconnected;
    }

    private void Update()
    {
        if (networkManager == null)
        {
            return;
        }

        if (returningToMenu)
        {
            UpdateReturnToMenu();
            return;
        }

        if (matchPhase == Phase.Loading && networkManager.IsListening)
        {
            UpdateLoading();
            return;
        }

        if (IsSessionActive)
        {
            GetSceneReplicator();
            if (!networkManager.IsServer && SceneManager.GetActiveScene().name == GameSceneName)
            {
                if (!worldStateRequested)
                {
                    worldStateRequested = true;
                    using (var writer = new FastBufferWriter(8, Allocator.Temp))
                    {
                        messageManager.SendNamedMessage(WorldRequest, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
                    }
                }

                ApplyPendingWorld();
            }
        }

        if (!IsSessionActive || !networkManager.IsServer)
        {
            return;
        }

        if (actionInProgress && Time.unscaledTime >= actionEndTime && (actingFigure == null || !actingFigure.IsAnimating))
        {
            actionInProgress = false;
            actingFigure = null;
            BroadcastWorld(false);
            if (CurrentActions <= 0)
            {
                AdvanceTurn("Действия игрока закончились.");
            }
            else
            {
                BroadcastSnapshot();
            }
        }

        if (!actionInProgress && Time.unscaledTime >= turnEndTime)
        {
            AdvanceTurn("Время хода закончилось.");
        }

        if (Time.unscaledTime >= nextSnapshotTime)
        {
            BroadcastWorld(false);
            BroadcastSnapshot();
            nextSnapshotTime = Time.unscaledTime + .25f;
        }
    }

    private LanSceneReplicator GetSceneReplicator()
    {
        if (!usesNetworkRules || returningToMenu || SceneManager.GetActiveScene().name != GameSceneName)
        {
            return null;
        }

        if (LanSceneReplicator.Instance == null)
        {
            new GameObject("LAN Scene Replicator").AddComponent<LanSceneReplicator>();
        }

        return LanSceneReplicator.Instance;
    }

    private void NetworkSceneLoaded(
        string sceneName,
        LoadSceneMode mode,
        List<ulong> completedClientIds,
        List<ulong> timedOutClientIds
    )
    {
        if (sceneName != GameSceneName || returningToMenu)
        {
            return;
        }

        sceneLoadingCompleted = true;
        if (networkManager.IsServer && matchPhase == Phase.Loading
            && ActivePlayers().Any(participant => !completedClientIds.Contains(participant.Id)))
        {
            FailLoading("Не все игроки загрузили сцену. Пересоздай матч.");
        }
    }

    private void NetworkSceneEvent(SceneEvent sceneEvent)
    {
        if (!networkManager.IsServer && sceneEvent.ClientId == networkManager.LocalClientId
            && sceneEvent.SceneEventType == SceneEventType.SynchronizeComplete
            && SceneManager.GetActiveScene().name == GameSceneName)
        {
            sceneLoadingCompleted = true;
            worldStateRequested = false;
        }
    }

    private void UpdateLoading()
    {
        if (networkManager.IsServer && Time.unscaledTime >= sceneLoadTimeoutTime)
        {
            FailLoading("Истекло время ожидания сцены. Проверь одинаковые сборки у всех игроков.");
            return;
        }

        if (sceneLoadingCompleted && !sceneReadyMessageSent)
        {
            LanSceneReplicator sceneReplicator = GetSceneReplicator();
            if (sceneReplicator == null)
            {
                return;
            }

            sceneReplicator.Refresh();
            string validationError = sceneReplicator.ValidationError;
            string sceneSignature = string.Empty;
            try
            {
                if (sceneReplicator.IsReady)
                {
                    sceneSignature = sceneReplicator.LayoutSignature + GetWorld().Signature;
                }
            }
            catch (Exception exception)
            {
                validationError = exception.Message;
            }

            sceneReadyMessageSent = true;
            if (networkManager.IsServer)
            {
                AcceptSceneReady(networkManager.LocalClientId, sceneSignature, validationError);
            }
            else
            {
                using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
                {
                    writer.WriteValueSafe(sceneSignature);
                    writer.WriteValueSafe(validationError);
                    messageManager.SendNamedMessage(SceneReady, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
                }
            }
        }

        if (networkManager.IsServer)
        {
            TryBeginMatch();
        }
    }

    private void ReceiveSceneReady(ulong clientId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || matchPhase != Phase.Loading || returningToMenu)
        {
            return;
        }

        reader.ReadValueSafe(out string sceneSignature);
        reader.ReadValueSafe(out string validationError);
        AcceptSceneReady(clientId, sceneSignature, validationError);
    }

    private void AcceptSceneReady(ulong clientId, string sceneSignature, string validationError)
    {
        if (matchPhase != Phase.Loading || !participants.TryGetValue(clientId, out Participant participant) || participant.Spectator
            || participant.Banned)
        {
            return;
        }

        if (!string.IsNullOrEmpty(validationError) || string.IsNullOrEmpty(sceneSignature))
        {
            FailLoading(participant.Name + ": " + (string.IsNullOrEmpty(validationError) ? "нет данных фигур." : validationError));
            return;
        }

        sceneSignatures[clientId] = sceneSignature;
        TryBeginMatch();
    }

    private void TryBeginMatch()
    {
        if (matchPhase != Phase.Loading || !sceneLoadingCompleted || !sceneReadyMessageSent)
        {
            return;
        }

        List<Participant> activePlayers = ActivePlayers().OrderBy(participant => participant.Slot).ToList();
        if (activePlayers.Count < 2)
        {
            FailLoading("Для матча нужны хотя бы два игрока.");
            return;
        }

        if (activePlayers.Any(participant => !sceneSignatures.ContainsKey(participant.Id)))
        {
            return;
        }

        LanSceneReplicator sceneReplicator = GetSceneReplicator();
        if (sceneReplicator == null || !sceneReplicator.IsReady)
        {
            return;
        }

        if (activePlayers.Any(participant => sceneSignatures[participant.Id] != sceneReplicator.LayoutSignature + GetWorld().Signature))
        {
            FailLoading("Состав фигур или их команды различаются. Установи одинаковую сцену и пересобери игру на всех ПК.");
            return;
        }

        foreach (Participant participant in activePlayers)
        {
            if (!sceneReplicator.HasTeam((TurnManager.Team)participant.Slot))
            {
                FailLoading("Нет фигуры команды " + (TurnManager.Team)participant.Slot + " для " + participant.Name + ". Проверь FigureTurn.");
                return;
            }

            participant.Resources = startingResources;
            participant.Actions = Math.Max(1, actionsPerTurn);
        }

        matchPhase = Phase.Match;
        currentPlayerSlot = participants[networkManager.LocalClientId].Slot;
        turnEndTime = Time.unscaledTime + Mathf.Max(5, turnDurationSeconds);
        statusMessage = "Все игроки готовы. Первый ход Host.";
        worldSynchronized = true;
        turnNumber++;
        FindFirstObjectByType<TurnManager>()?.BeginNetworkTurn((TurnManager.Team)currentPlayerSlot);
        BroadcastWorld(true);
        BroadcastSnapshot();
    }

    private void FailLoading(string reason)
    {
        matchPhase = Phase.Finished;
        currentPlayerSlot = -1;
        statusMessage = reason.Length > 300 ? reason.Substring(0, 300) : reason;
        Debug.LogError("[LAN] " + statusMessage);
        BroadcastSnapshot();
    }

    public void CreateGame()
    {
        if (returningToMenu || networkManager.IsListening || networkManager.ShutdownInProgress)
        {
            return;
        }

        participants.Clear();
        bannedClientIds.Clear();
        sceneSignatures.Clear();
        sceneLoadingCompleted = sceneReadyMessageSent = false;
        waitingForActionResponse = actionInProgress = worldSynchronized = worldStateRequested = false;
        pendingWorldJson = lastSentWorldJson = null;
        incomingWorldMessageNumber = -1;
        usesNetworkRules = true;
        isLocalSpectator = true;
        localPlayerSlot = currentPlayerSlot = -1;
        networkTransport.SetConnectionData("127.0.0.1", Port, "0.0.0.0");
        if (!networkManager.StartHost())
        {
            usesNetworkRules = false;
            statusMessage = "Host не запустился: проверь UDP-порт 7777.";
            return;
        }

        matchPhase = Phase.Lobby;
        AddParticipant(networkManager.LocalClientId, "Host", false);
        localPlayerSlot = 0;
        isLocalSpectator = false;
        statusMessage = "Лобби создано. Нужен хотя бы один клиент.";
        BroadcastSnapshot();
    }

    public void JoinGame()
    {
        if (returningToMenu || networkManager.IsListening || networkManager.ShutdownInProgress)
        {
            return;
        }

        if (!IPAddress.TryParse(hostAddress.Trim(), out IPAddress address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            statusMessage = "Введи IPv4 хоста, например 192.168.1.25.";
            return;
        }

        sceneSignatures.Clear();
        sceneLoadingCompleted = sceneReadyMessageSent = false;
        waitingForActionResponse = actionInProgress = worldSynchronized = worldStateRequested = false;
        pendingWorldJson = lastSentWorldJson = null;
        incomingWorldMessageNumber = -1;
        usesNetworkRules = true;
        isLocalSpectator = true;
        localPlayerSlot = currentPlayerSlot = -1;
        networkTransport.SetConnectionData(address.ToString(), Port);
        if (networkManager.StartClient())
        {
            statusMessage = "Подключение...";
        }
        else
        {
            usesNetworkRules = false;
            statusMessage = "Client не запустился.";
        }
    }

    public void Disconnect()
    {
        RequestReturnToMenu("Соединение закрыто.");
    }

    private void RequestReturnToMenu(string reason)
    {
        if (!returningToMenu)
        {
            statusMessage = reason;
        }

        returningToMenu = true;
        usesNetworkRules = true;
        isLocalSpectator = true;
        localPlayerSlot = currentPlayerSlot = -1;
        matchPhase = Phase.Menu;
        foreach (TilesMovement figureMovement in FindObjectsByType<TilesMovement>(FindObjectsSortMode.None))
        {
            figureMovement.UnSelect();
        }
    }

    private void UpdateReturnToMenu()
    {
        if (networkManager.IsListening || networkManager.ShutdownInProgress)
        {
            if (!shutdownStarted)
            {
                shutdownStarted = true;
                if (!networkManager.ShutdownInProgress)
                {
                    networkManager.Shutdown();
                }
            }

            return;
        }

        if (SceneManager.GetActiveScene().name == MenuSceneName)
        {
            CompleteReturnToMenu();
        }
        else
        {
            SceneManager.LoadScene(MenuSceneName);
        }
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (returningToMenu && scene.name == MenuSceneName && !networkManager.IsListening && !networkManager.ShutdownInProgress)
        {
            CompleteReturnToMenu();
        }
    }

    private void CompleteReturnToMenu()
    {
        participants.Clear();
        bannedClientIds.Clear();
        messageManager = null;
        matchPhase = Phase.Menu;
        localPlayerSlot = currentPlayerSlot = -1;
        isLocalSpectator = false;
        returningToMenu = false;
        shutdownStarted = false;
        usesNetworkRules = false;
    }

    private void Approve(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response
    )
    {
        bool fullLobby = matchPhase != Phase.Match && networkManager.ConnectedClientsIds.Count >= MaxPlayers;
        response.Approved = !fullLobby && !returningToMenu;
        response.CreatePlayerObject = false;
        response.Pending = false;
        response.Reason = fullLobby ? "Лобби заполнено: максимум четыре игрока." : string.Empty;
    }

    private void Connected(ulong clientId)
    {
        if (returningToMenu)
        {
            return;
        }

        if (networkManager.IsServer)
        {
            if (clientId == networkManager.LocalClientId)
            {
                BroadcastSnapshot();
            }
            else
            {
                statusMessage = "Клиент подключён. Ожидаю его имя...";
            }

            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe(playerName.Trim());
            messageManager.SendNamedMessage(Hello, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        statusMessage = "Подключено. Ожидание лобби...";
    }

    private void Disconnected(ulong clientId)
    {
        if (networkManager.IsServer && clientId != networkManager.LocalClientId)
        {
            bool wasCurrent = participants.TryGetValue(clientId, out Participant disconnectedParticipant)
                && disconnectedParticipant.Slot == currentPlayerSlot;
            participants.Remove(clientId);
            sceneSignatures.Remove(clientId);
            if (matchPhase == Phase.Loading)
            {
                FailLoading("Игрок отключился при загрузке. Пересоздай матч.");
                return;
            }

            if (matchPhase == Phase.Match && wasCurrent)
            {
                AdvanceTurn("Игрок отключился.");
            }
            else
            {
                BroadcastSnapshot();
            }

            return;
        }

        string reason = string.IsNullOrEmpty(networkManager.DisconnectReason) ? "Хост закрыл соединение." : networkManager.DisconnectReason;
        RequestReturnToMenu(reason);
    }

    private void ReceiveHello(ulong clientId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || returningToMenu)
        {
            return;
        }

        reader.ReadValueSafe(out string name);
        bool spectator = matchPhase == Phase.Match || matchPhase == Phase.Loading || matchPhase == Phase.Finished
            || bannedClientIds.Contains(clientId)
            || ActivePlayers().Count() >= MaxPlayers;
        AddParticipant(clientId, string.IsNullOrWhiteSpace(name) ? "Player" : name, spectator);
        BroadcastSnapshot();
    }

    private void AddParticipant(ulong clientId, string name, bool spectator)
    {
        if (participants.TryGetValue(clientId, out Participant existingParticipant))
        {
            existingParticipant.Name = name;
            existingParticipant.Spectator = spectator;
            return;
        }

        int slot = spectator
            ? -1
            : Enumerable.Range(0, MaxPlayers).FirstOrDefault(value => participants.Values.All(participant => participant.Slot != value));
        participants[clientId] = new Participant
        {
            Id = clientId,
            Slot = slot,
            Name = name,
            Spectator = spectator,
            Banned = bannedClientIds.Contains(clientId),
            Resources = startingResources,
            Actions = Math.Max(1, actionsPerTurn)
        };
    }

    public void StartMatch()
    {
        if (!networkManager.IsServer || returningToMenu || matchPhase != Phase.Lobby)
        {
            return;
        }

        List<Participant> activePlayers = ActivePlayers().OrderBy(participant => participant.Slot).ToList();
        if (activePlayers.Count < 2)
        {
            statusMessage = "Для старта нужен Host и хотя бы один клиент.";
            return;
        }

        if (networkManager.SceneManager == null)
        {
            statusMessage = "Недоступна сетевая загрузка сцены.";
            return;
        }

        matchPhase = Phase.Loading;
        currentPlayerSlot = -1;
        turnEndTime = 0;
        sceneLoadTimeoutTime = Time.unscaledTime + 180f;
        sceneSignatures.Clear();
        sceneLoadingCompleted = sceneReadyMessageSent = false;
        waitingForActionResponse = actionInProgress = worldSynchronized = worldStateRequested = false;
        pendingWorldJson = lastSentWorldJson = null;
        incomingWorldMessageNumber = -1;
        statusMessage = "Загрузка сцены и проверка фигур...";
        BroadcastSnapshot();
        SceneEventProgressStatus loadStatus = networkManager.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
        if (loadStatus != SceneEventProgressStatus.Started)
        {
            FailLoading("Не удалось загрузить сцену: " + loadStatus);
        }
    }

    public void CancelMatch()
    {
        if (!networkManager.IsServer || (matchPhase != Phase.Match && matchPhase != Phase.Loading))
        {
            return;
        }

        matchPhase = Phase.Finished;
        statusMessage = "Матч отменён хостом.";
        BroadcastSnapshot();
    }

    public void BanClient(ulong clientId)
    {
        if (!networkManager.IsServer || matchPhase != Phase.Match || returningToMenu || clientId == networkManager.LocalClientId
            || !participants.TryGetValue(clientId, out Participant participant))
        {
            return;
        }

        bannedClientIds.Add(clientId);
        participant.Banned = true;
        participant.Spectator = true;
        participant.Actions = 0;
        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe("Вы забанены до конца матча. Можно переподключиться зрителем.");
            messageManager.SendNamedMessage(Kick, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        if (participant.Slot == currentPlayerSlot)
        {
            AdvanceTurn("Игрок забанен.");
        }
        else
        {
            BroadcastSnapshot();
        }

        networkManager.DisconnectClient(clientId, "Вы исключены хостом до конца матча.");
    }

    public void ReportCheckmate(ulong clientId)
    {
        if (!networkManager.IsServer || !participants.TryGetValue(clientId, out Participant participant))
        {
            return;
        }

        participant.Spectator = true;
        participant.Actions = 0;
        if (participant.Slot == currentPlayerSlot)
        {
            AdvanceTurn("Мат: игрок перешёл в Spectator.");
        }
        else
        {
            BroadcastSnapshot();
        }
    }

    public bool CanLocalTeamAct(TurnManager.Team team)
    {
        Participant localParticipant = FindLocal();
        return IsSessionActive && !isLocalSpectator && localParticipant != null && !localParticipant.Spectator
            && !localParticipant.Banned
            && (int)team == localPlayerSlot
            && localParticipant.Slot == currentPlayerSlot
            && localParticipant.Actions > 0
            && worldSynchronized
            && !waitingForActionResponse
            && !actionInProgress;
    }

    public bool CanLocalFigureAct(TilesMovement figureMovement)
    {
        return figureMovement != null && figureMovement.TryGetComponent(out FigureTurn figureTurn) && CanLocalTeamAct(figureTurn.Team);
    }

    public void NotifyLocalMovement(TilesMovement figureMovement, int targetX, int targetY)
    {
        NotifyLocalAction(figureMovement, LanFigureAction.MoveOrAttack, targetX, targetY);
    }

    public void NotifyLocalAction(TilesMovement figureMovement, LanFigureAction actionType, int targetX, int targetY)
    {
        if (!CanLocalFigureAct(figureMovement))
        {
            return;
        }

        var sceneReplicator = GetSceneReplicator();
        if (sceneReplicator == null || !sceneReplicator.TryGetId(figureMovement, out string figureId))
        {
            statusMessage = "Фигура не зарегистрирована. Проверь FigureTurn и Network Id.";
            return;
        }

        waitingForActionResponse = true;
        if (networkManager.IsServer)
        {
            HandleAction(networkManager.LocalClientId, figureId, actionType, targetX, targetY, turnNumber);
        }
        else
        {
            using (var writer = new FastBufferWriter(BufferSize, Allocator.Temp))
            {
                writer.WriteValueSafe(figureId);
                writer.WriteValueSafe((byte)actionType);
                writer.WriteValueSafe(targetX);
                writer.WriteValueSafe(targetY);
                writer.WriteValueSafe(turnNumber);
                messageManager.SendNamedMessage(Action, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
    }

    private bool CanAccept(ulong clientId, int requestedTurnNumber, out Participant participant)
    {
        participants.TryGetValue(clientId, out participant);
        return networkManager.IsServer && IsSessionActive && !actionInProgress && requestedTurnNumber == turnNumber
            && participant != null
            && !participant.Spectator
            && !participant.Banned
            && participant.Slot == currentPlayerSlot
            && participant.Actions > 0;
    }

    private void ReceiveAction(ulong clientId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || returningToMenu)
        {
            return;
        }

        reader.ReadValueSafe(out string figureId);
        reader.ReadValueSafe(out byte actionType);
        reader.ReadValueSafe(out int targetX);
        reader.ReadValueSafe(out int targetY);
        reader.ReadValueSafe(out int requestedTurnNumber);
        HandleAction(clientId, figureId, (LanFigureAction)actionType, targetX, targetY, requestedTurnNumber);
    }

    private void ReceiveMoveApplied(ulong clientId, FastBufferReader reader)
    {
        if (networkManager.IsServer || clientId != NetworkManager.ServerClientId || returningToMenu)
        {
            return;
        }

        reader.ReadValueSafe(out string figureId);
        reader.ReadValueSafe(out int targetX);
        reader.ReadValueSafe(out int targetY);
        reader.ReadValueSafe(out bool moveAfterAttack);
        if (!IsSessionActive || !worldSynchronized)
        {
            return;
        }

        var figureMovement = GetSceneReplicator()?.GetFigure(figureId);
        if (figureMovement == null)
        {
            RequestReturnToMenu("Не найдена фигура " + figureId + ". Установи одну сборку на всех ПК.");
            return;
        }

        figureMovement.PlayAcceptedAction(targetX, targetY, moveAfterAttack);
    }

    private void HandleAction(
        ulong clientId,
        string figureId,
        LanFigureAction actionType,
        int targetX,
        int targetY,
        int requestedTurnNumber
    )
    {
        if (!CanAccept(clientId, requestedTurnNumber, out Participant participant))
        {
            RejectAction(clientId, "Сейчас нельзя действовать: ход сменился или действие ещё выполняется.");
            return;
        }

        TilesMovement figureMovement = GetSceneReplicator()?.GetFigure(figureId);
        if (figureMovement == null || !figureMovement.gameObject.activeInHierarchy
            || !figureMovement.TryGetComponent(out FigureTurn figureTurn)
            || (int)figureTurn.Team != participant.Slot)
        {
            RejectAction(clientId, "Фигура не найдена или принадлежит другой команде.");
            return;
        }

        if (!LanActionRules.Resolve(
            figureMovement,
            actionType,
            targetX,
            targetY,
            out Health targetHealth,
            out float attackDamage,
            out bool moveAfterAttack,
            out string validationError
        ))
        {
            RejectAction(clientId, validationError);
            return;
        }

        participant.Actions--;
        waitingForActionResponse = false;
        actionInProgress = true;
        actingFigure = figureMovement;
        actionEndTime = Time.unscaledTime + (moveAfterAttack ? figureMovement.ActionDuration : .3f);
        if (targetHealth != null)
        {
            targetHealth.DealDamage(attackDamage);
        }

        figureMovement.PlayAcceptedAction(targetX, targetY, moveAfterAttack);
        using (var writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe(figureId);
            writer.WriteValueSafe(targetX);
            writer.WriteValueSafe(targetY);
            writer.WriteValueSafe(moveAfterAttack);
            messageManager.SendNamedMessageToAll(MoveApplied, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }

        BroadcastWorld(false);
        BroadcastSnapshot();
    }

    public void RequestBuild(Vector2Int cell, int configIndex)
    {
        if (!CanLocalBuild)
        {
            return;
        }

        waitingForActionResponse = true;
        if (networkManager.IsServer)
        {
            HandleBuild(networkManager.LocalClientId, cell, configIndex, turnNumber);
        }
        else
        {
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteValueSafe(cell.x);
                writer.WriteValueSafe(cell.y);
                writer.WriteValueSafe(configIndex);
                writer.WriteValueSafe(turnNumber);
                messageManager.SendNamedMessage(Build, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
    }

    private void ReceiveBuild(ulong clientId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || returningToMenu)
        {
            return;
        }

        reader.ReadValueSafe(out int targetX);
        reader.ReadValueSafe(out int targetY);
        reader.ReadValueSafe(out int buildingIndex);
        reader.ReadValueSafe(out int requestedTurnNumber);
        HandleBuild(clientId, new Vector2Int(targetX, targetY), buildingIndex, requestedTurnNumber);
    }

    private void HandleBuild(ulong clientId, Vector2Int cell, int buildingIndex, int requestedTurnNumber)
    {
        if (!CanAccept(clientId, requestedTurnNumber, out Participant participant))
        {
            RejectAction(clientId, "Сейчас нельзя строить.");
            return;
        }

        var placementController = FindFirstObjectByType<PlacementController>();
        if (placementController == null || !placementController.BuildAuthoritative(cell, buildingIndex, participant.Slot))
        {
            RejectAction(clientId, "Нельзя построить на этой клетке.");
            return;
        }

        participant.Actions--;
        waitingForActionResponse = false;
        actionInProgress = true;
        actingFigure = null;
        actionEndTime = Time.unscaledTime + .1f;
        BroadcastWorld(false);
        BroadcastSnapshot();
    }

    private void BroadcastWorld(bool includeBoard)
    {
        if (!networkManager.IsServer || !IsSessionActive)
        {
            return;
        }

        string worldJson;
        try
        {
            worldJson = GetWorld().Capture(includeBoard);
        }
        catch (Exception exception)
        {
            FailLoading("Ошибка состояния мира: " + exception.Message);
            return;
        }

        if (!includeBoard && worldJson == lastSentWorldJson)
        {
            return;
        }

        lastSentWorldJson = worldJson;
        int messageNumber = ++worldMessageNumber;
        const int chunkSize = 6000;
        int chunkCount = (worldJson.Length + chunkSize - 1) / chunkSize;
        for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
        {
            string chunk = worldJson.Substring(chunkIndex * chunkSize, Math.Min(chunkSize, worldJson.Length - chunkIndex * chunkSize));
            using (var writer = new FastBufferWriter(chunk.Length * 2 + 64, Allocator.Temp))
            {
                writer.WriteValueSafe(messageNumber);
                writer.WriteValueSafe(chunkIndex);
                writer.WriteValueSafe(chunkCount);
                writer.WriteValueSafe(chunk);
                messageManager.SendNamedMessageToAll(World, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
    }

    private void ReceiveWorldRequest(ulong clientId, FastBufferReader reader)
    {
        if (networkManager.IsServer && IsSessionActive && networkManager.ConnectedClientsIds.Contains(clientId))
        {
            BroadcastWorld(true);
        }
    }

    private void ReceiveWorld(ulong clientId, FastBufferReader reader)
    {
        if (networkManager.IsServer || clientId != NetworkManager.ServerClientId || returningToMenu)
        {
            return;
        }

        reader.ReadValueSafe(out int messageNumber);
        reader.ReadValueSafe(out int chunkIndex);
        reader.ReadValueSafe(out int chunkCount);
        reader.ReadValueSafe(out string chunk);
        if (chunkCount < 1 || chunkCount > 512 || chunkIndex < 0 || chunkIndex >= chunkCount || chunk.Length > 6000)
        {
            RequestReturnToMenu("Некорректное состояние мира.");
            return;
        }

        if (chunkIndex == 0)
        {
            incomingWorldMessageNumber = messageNumber;
            nextWorldChunkIndex = 0;
            worldJsonBuffer.Clear();
        }

        if (messageNumber != incomingWorldMessageNumber || chunkIndex != nextWorldChunkIndex++)
        {
            RequestReturnToMenu("Нарушен порядок данных мира. Переподключись.");
            return;
        }

        worldJsonBuffer.Append(chunk);
        if (chunkIndex != chunkCount - 1)
        {
            return;
        }

        pendingWorldJson = worldJsonBuffer.ToString();
        ApplyPendingWorld();
    }

    private void ApplyPendingWorld()
    {
        if (pendingWorldJson == null || SceneManager.GetActiveScene().name != GameSceneName || !sceneLoadingCompleted)
        {
            return;
        }

        try
        {
            GetSceneReplicator();
            GetWorld().Apply(pendingWorldJson);
            worldSynchronized = true;
            pendingWorldJson = null;
        }
        catch (Exception exception)
        {
            RequestReturnToMenu("Не удалось применить мир: " + exception.Message);
        }
    }

    private void RejectAction(ulong clientId, string reason)
    {
        if (clientId == networkManager.LocalClientId)
        {
            waitingForActionResponse = false;
            statusMessage = reason;
            Debug.LogWarning("[LAN] " + reason);
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe(reason);
            messageManager.SendNamedMessage(ActionRejected, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }

    private void ReceiveActionRejected(ulong clientId, FastBufferReader reader)
    {
        if (networkManager.IsServer || clientId != NetworkManager.ServerClientId || returningToMenu)
        {
            return;
        }

        reader.ReadValueSafe(out string reason);
        waitingForActionResponse = false;
        Debug.LogWarning("[LAN] " + reason);
        statusMessage = reason;
    }

    public void EndLocalTurn()
    {
        if (!IsSessionActive || isLocalSpectator || waitingForActionResponse || actionInProgress)
        {
            return;
        }

        if (networkManager.IsServer)
        {
            HandleEndTurn(networkManager.LocalClientId, turnNumber);
        }
        else
        {
            using (FastBufferWriter writer = new FastBufferWriter(16, Allocator.Temp))
            {
                writer.WriteValueSafe(turnNumber);
                messageManager.SendNamedMessage(EndTurn, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
    }

    private void ReceiveEndTurn(ulong clientId, FastBufferReader reader)
    {
        if (!networkManager.IsServer || !IsSessionActive)
        {
            return;
        }

        reader.ReadValueSafe(out int requestedTurnNumber);
        HandleEndTurn(clientId, requestedTurnNumber);
    }

    private void HandleEndTurn(ulong clientId, int requestedTurnNumber)
    {
        if (requestedTurnNumber == turnNumber && IsSessionActive && !actionInProgress
            && participants.TryGetValue(clientId, out Participant participant)
            && participant.Slot == currentPlayerSlot
            && !participant.Spectator
            && !participant.Banned)
        {
            AdvanceTurn("Игрок завершил ход.");
        }
    }

    private void AdvanceTurn(string reason)
    {
        waitingForActionResponse = actionInProgress = false;
        actingFigure = null;
        List<Participant> activePlayers = ActivePlayers().OrderBy(participant => participant.Slot).ToList();
        if (activePlayers.Count <= 1)
        {
            matchPhase = Phase.Finished;
            statusMessage = reason + " Матч завершён.";
            BroadcastSnapshot();
            return;
        }

        int currentPlayerIndex = activePlayers.FindIndex(participant => participant.Slot == currentPlayerSlot);
        Participant nextPlayer = activePlayers[(currentPlayerIndex + 1 + activePlayers.Count) % activePlayers.Count];
        currentPlayerSlot = nextPlayer.Slot;
        nextPlayer.Actions = Math.Max(1, actionsPerTurn);
        turnEndTime = Time.unscaledTime + Mathf.Max(5, turnDurationSeconds);
        turnNumber++;
        FindFirstObjectByType<TurnManager>()?.BeginNetworkTurn((TurnManager.Team)currentPlayerSlot);
        statusMessage = reason + " Ход: " + nextPlayer.Name + ".";
        BroadcastWorld(false);
        BroadcastSnapshot();
    }

    private IEnumerable<Participant> ActivePlayers()
    {
        return participants.Values.Where(participant => !participant.Spectator && !participant.Banned && participant.Slot >= 0);
    }

    private Participant FindLocal()
    {
        participants.TryGetValue(networkManager.LocalClientId, out Participant participant);
        return participant;
    }

    private void BroadcastSnapshot()
    {
        if (!networkManager.IsServer || !networkManager.IsListening)
        {
            return;
        }

        using (FastBufferWriter writer = new FastBufferWriter(BufferSize, Allocator.Temp))
        {
            writer.WriteValueSafe((byte)matchPhase);
            writer.WriteValueSafe(turnNumber);
            writer.WriteValueSafe(actionInProgress);
            writer.WriteValueSafe(currentPlayerSlot);
            writer.WriteValueSafe(Mathf.Max(0, turnEndTime - Time.unscaledTime));
            writer.WriteValueSafe(participants.Count);
            foreach (Participant participant in participants.Values.OrderBy(participant => participant.Id))
            {
                writer.WriteValueSafe(participant.Id);
                writer.WriteValueSafe(participant.Slot);
                writer.WriteValueSafe(participant.Name);
                writer.WriteValueSafe(participant.Spectator);
                writer.WriteValueSafe(participant.Banned);
                writer.WriteValueSafe(participant.Resources);
                writer.WriteValueSafe(participant.Actions);
            }

            writer.WriteValueSafe(statusMessage);
            messageManager.SendNamedMessageToAll(Snapshot, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }

    private void ReceiveSnapshot(ulong clientId, FastBufferReader reader)
    {
        if (networkManager.IsServer || clientId != NetworkManager.ServerClientId || returningToMenu)
        {
            return;
        }

        usesNetworkRules = true;
        reader.ReadValueSafe(out byte phaseValue);
        matchPhase = (Phase)phaseValue;
        reader.ReadValueSafe(out turnNumber);
        reader.ReadValueSafe(out actionInProgress);
        reader.ReadValueSafe(out currentPlayerSlot);
        reader.ReadValueSafe(out float remainingTurnSeconds);
        turnEndTime = Time.unscaledTime + remainingTurnSeconds;
        reader.ReadValueSafe(out int participantCount);
        participants.Clear();
        for (int participantIndex = 0; participantIndex < participantCount; participantIndex++)
        {
            Participant participant = new Participant();
            reader.ReadValueSafe(out participant.Id);
            reader.ReadValueSafe(out participant.Slot);
            reader.ReadValueSafe(out participant.Name);
            reader.ReadValueSafe(out participant.Spectator);
            reader.ReadValueSafe(out participant.Banned);
            reader.ReadValueSafe(out participant.Resources);
            reader.ReadValueSafe(out participant.Actions);
            participants[participant.Id] = participant;
        }

        reader.ReadValueSafe(out statusMessage);
        if (actionInProgress || lastAcknowledgedRevision != turnNumber)
        {
            waitingForActionResponse = false;
        }

        lastAcknowledgedRevision = turnNumber;
        Participant localParticipant = FindLocal();
        localPlayerSlot = localParticipant == null ? -1 : localParticipant.Slot;
        isLocalSpectator = localParticipant == null || localParticipant.Spectator || localParticipant.Banned;
    }

    private void ReceiveKick(ulong clientId, FastBufferReader reader)
    {
        if (networkManager.IsServer || clientId != NetworkManager.ServerClientId)
        {
            return;
        }

        reader.ReadValueSafe(out string reason);
        RequestReturnToMenu(reason);
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(16, 16, 390, 520), GUI.skin.box);
        GUILayout.Label("TBS — локальная сеть v3");
        if (returningToMenu)
        {
            GUILayout.Label("Возврат в меню...");
        }
        else if (!networkManager.IsListening)
        {
            GUILayout.Label("Имя");
            playerName = GUILayout.TextField(playerName);
            GUILayout.Label("IPv4 хоста");
            hostAddress = GUILayout.TextField(hostAddress);
            if (GUILayout.Button("Создать игру (Host)"))
            {
                CreateGame();
            }

            if (GUILayout.Button("Подключиться (Client)"))
            {
                JoinGame();
            }
        }
        else if (matchPhase == Phase.Lobby)
        {
            GUILayout.Label("Лобби: " + ActivePlayers().Count() + "/4 игроков");
            DrawParticipants();
            if (networkManager.IsServer)
            {
                GUILayout.Label("Секунд на ход");
                turnDurationSeconds = Mathf.Clamp(ParseInt(GUILayout.TextField(turnDurationSeconds.ToString()), turnDurationSeconds), 5, 3600);
                GUILayout.Label("Стартовые ресурсы");
                startingResources = Mathf.Max(0, ParseInt(GUILayout.TextField(startingResources.ToString()), startingResources));
                GUILayout.Label("Города + короли = действий");
                actionsPerTurn = Mathf.Max(1, ParseInt(GUILayout.TextField(actionsPerTurn.ToString()), actionsPerTurn));
                if (GUILayout.Button("Начать матч"))
                {
                    StartMatch();
                }
            }
        }
        else if (matchPhase == Phase.Loading)
        {
            GUILayout.Label("Ожидание загрузки и проверки фигур у всех игроков...");
            if (networkManager.IsServer && GUILayout.Button("Отменить матч"))
            {
                CancelMatch();
            }
        }
        else if (matchPhase == Phase.Match)
        {
            Participant currentPlayer = participants.Values.FirstOrDefault(participant => participant.Slot == currentPlayerSlot);
            GUILayout.Label("Ход: " + (currentPlayer == null ? "..." : currentPlayer.Name));
            GUILayout.Label("Время: " + Mathf.CeilToInt(Mathf.Max(0, turnEndTime - Time.unscaledTime)) + " сек.");
            GUILayout.Label("Действий у текущего игрока: " + CurrentActions);
            GUILayout.Label("Ваши ресурсы: " + LocalResources + " | Ваши действия: " + LocalActions);
            GUILayout.Label(localPlayerSlot < 0 ? "Вы зритель" : "Ваша команда: " + (TurnManager.Team)localPlayerSlot);
            GUILayout.Label(isLocalSpectator ? "SPECTATOR" : "PLAYER");
            DrawParticipants();
            if (!isLocalSpectator && localPlayerSlot == currentPlayerSlot && GUILayout.Button("Завершить ход"))
            {
                EndLocalTurn();
            }

            if (networkManager.IsServer && GUILayout.Button("Отменить матч"))
            {
                CancelMatch();
            }

            if (networkManager.IsServer)
            {
                foreach (Participant participant in ActivePlayers().Where(participant => participant.Id != networkManager.LocalClientId).ToList())
                {
                    if (GUILayout.Button("Забанить " + participant.Name))
                    {
                        BanClient(participant.Id);
                    }
                }
            }
        }
        else if (matchPhase == Phase.Finished)
        {
            GUILayout.Label(statusMessage);
        }

        GUILayout.Space(8);
        GUILayout.Label(statusMessage);
        if ((usesNetworkRules || networkManager.IsListening || SceneManager.GetActiveScene().name != MenuSceneName)
            && GUILayout.Button("Выйти в меню"))
        {
            Disconnect();
        }

        GUILayout.EndArea();
    }

    private void DrawParticipants()
    {
        foreach (Participant participant in participants.Values.OrderBy(participant => participant.Slot < 0 ? 99 : participant.Slot))
        {
            string playerStatus;
            if (participant.Banned)
            {
                playerStatus = "BANNED";
            }
            else if (participant.Spectator)
            {
                playerStatus = "Spectator";
            }
            else
            {
                playerStatus = "Player " + (participant.Slot + 1);
            }

            GUILayout.Label(participant.Name + " — " + playerStatus);
        }
    }

    private static int ParseInt(string value, int fallback)
    {
        return int.TryParse(value, out int parsedValue) ? parsedValue : fallback;
    }
}
