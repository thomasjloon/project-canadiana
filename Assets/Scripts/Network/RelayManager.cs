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
    [Header("UI Elements")]
    [SerializeField] private Button hostButton, joinButton, cancelButton, cancelButton2, startButton;
    [SerializeField] private TMP_InputField joinInputField;
    [SerializeField] private TMP_Text codeText, hostStatusText, clientStatusText;
    [SerializeField] private string gameSceneName = "GameScene";

    private NetworkVariable<int> playerCount = new NetworkVariable<int>(0);
    private NetworkVariable<bool> lobbyClosed = new NetworkVariable<bool>(false);

    private async void Start()
    {
        await InitializeServices();
        SetupButtons();
        ResetUI();

        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void SetupButtons()
    {
        hostButton.onClick.AddListener(() => { ResetUI(); CreateRelay(); });
        joinButton.onClick.AddListener(() => { ResetUI(); JoinRelay(joinInputField.text); });
        cancelButton.onClick.AddListener(CancelRelay);
        cancelButton2.onClick.AddListener(CancelRelay);
        startButton.onClick.AddListener(StartGame);
    }

    private void ResetUI()
    {
        codeText.text = "";
        hostStatusText.text = "";
        clientStatusText.text = "";
        startButton.gameObject.SetActive(false);
    }

    private async Task InitializeServices()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async void CreateRelay()
    {
        try
        {
            var allocation = await RelayService.Instance.CreateAllocationAsync(4);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));

            if (NetworkManager.Singleton.StartHost())
            {
                codeText.text = joinCode;
                hostStatusText.text = "Players: 1/4";
                startButton.gameObject.SetActive(true);
                playerCount.Value = 1;
                lobbyClosed.Value = false;
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Relay creation failed: {e.Message}");
        }
    }

    private async void JoinRelay(string joinCode)
    {
        if (string.IsNullOrEmpty(joinCode))
        {
            clientStatusText.text = "Please enter a code";
            return;
        }

        clientStatusText.text = "Connecting...";
        joinButton.interactable = false;

        try
        {
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, "dtls"));

            if (NetworkManager.Singleton.StartClient())
                clientStatusText.text = "Waiting for host...";
        }
        catch (RelayServiceException)
        {
            clientStatusText.text = "Invalid code";
        }
        finally
        {
            joinButton.interactable = true;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (IsHost)
        {
            playerCount.Value = NetworkManager.Singleton.ConnectedClients.Count;
            hostStatusText.text = $"Players: {playerCount.Value}/4";
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsHost)
        {
            playerCount.Value = NetworkManager.Singleton.ConnectedClients.Count;
            hostStatusText.text = $"Players: {playerCount.Value}/4";
        }
    }

    private void StartGame()
    {
        if (IsHost)
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void CancelRelay()
    {
        if (IsHost)
        {
            lobbyClosed.Value = true;
            NetworkManager.Singleton.Shutdown();
            ResetUI();
            hostStatusText.text = "Lobby closed";
        }
        else if (NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
            ResetUI();
            clientStatusText.text = "Host closed lobby";
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        lobbyClosed.OnValueChanged += OnLobbyClosed;
        playerCount.OnValueChanged += OnPlayerCountChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        lobbyClosed.OnValueChanged -= OnLobbyClosed;
        playerCount.OnValueChanged -= OnPlayerCountChanged;
    }

    private void OnLobbyClosed(bool oldValue, bool newValue)
    {
        if (newValue && !IsHost)
        {
            clientStatusText.text = "Host closed lobby";
            joinInputField.text = "";
            NetworkManager.Singleton.Shutdown();
            ResetUI();
        }
    }


    private void OnPlayerCountChanged(int oldCount, int newCount)
    {
        if (!IsHost)
            clientStatusText.text = $"Waiting for host... ({newCount}/4 players)";
    }
}