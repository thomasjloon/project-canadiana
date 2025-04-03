using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;

public class RelayManager : NetworkBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button cancelButton2;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button startButton;
    [SerializeField] private TMP_InputField joinInputField;
    [SerializeField] private TMP_Text codeText;
    [SerializeField] private TMP_Text lobbyStatusText;
    [SerializeField] private string gameSceneName = "GameScene";

    private NetworkVariable<int> playerCount = new NetworkVariable<int>();
    private NetworkVariable<bool> lobbyClosed = new NetworkVariable<bool>();

    private async void Start()
    {
        await InitializeServices();
        SetupButtons();
        ResetUI();

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void SetupButtons()
    {
        hostButton.onClick.AddListener(CreateRelay);
        joinButton.onClick.AddListener(() => JoinRelay(joinInputField.text));
        cancelButton.onClick.AddListener(CancelRelay);
        cancelButton2.onClick.AddListener(CancelRelay);
        startButton.onClick.AddListener(StartGame);
    }

    private void ResetUI()
    {
        startButton.interactable = true;
        startButton.gameObject.SetActive(false);
        lobbyStatusText.text = "";
        codeText.text = "";
    }

    private async Task InitializeServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private async void CreateRelay()
    {
        try
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(4);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var relayData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayData);

            if (NetworkManager.Singleton.StartHost())
            {
                codeText.text = joinCode;
                startButton.gameObject.SetActive(true);
                lobbyStatusText.text = "Players: 1/4";
                playerCount.Value = 1;
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Relay creation failed: {e.Message}");
        }
    }

    private async void JoinRelay(string joinCode)
    {
        try
        {
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var relayData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(relayData);

            if (NetworkManager.Singleton.StartClient())
            {
                lobbyStatusText.text = "Waiting for host to start...";
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Join relay failed: {e.Message}");
            lobbyStatusText.text = "Join failed";
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsHost) return;
        playerCount.Value = NetworkManager.Singleton.ConnectedClients.Count;
        UpdateLobbyStatus();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsHost) return;
        playerCount.Value = NetworkManager.Singleton.ConnectedClients.Count;
        UpdateLobbyStatus();
    }

    private void UpdateLobbyStatus()
    {
        if (!IsHost) return;
        lobbyStatusText.text = $"Players: {playerCount.Value}/4";
    }

    private void StartGame()
    {
        if (IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    public void CancelRelay()
    {
        if (IsHost)
        {
            lobbyClosed.Value = true;
            NetworkManager.Singleton.Shutdown();
            ResetUI();
            lobbyStatusText.text = "Lobby closed";
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
            ResetUI();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        playerCount.OnValueChanged += OnPlayerCountChanged;
        lobbyClosed.OnValueChanged += OnLobbyClosed;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        playerCount.OnValueChanged -= OnPlayerCountChanged;
        lobbyClosed.OnValueChanged -= OnLobbyClosed;
    }

    private void OnPlayerCountChanged(int oldCount, int newCount)
    {
        if (!IsHost)
        {
            lobbyStatusText.text = "Waiting for host to start...";
        }
    }

    private void OnLobbyClosed(bool oldValue, bool newValue)
    {
        if (newValue && !IsHost)
        {
            NetworkManager.Singleton.Shutdown();
            lobbyStatusText.text = "Host closed the lobby";
            ResetUI();
        }
    }
}