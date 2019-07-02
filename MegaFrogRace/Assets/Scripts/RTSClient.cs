// Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Collections.Generic;
using System;
using System.Globalization;
using UnityEngine;
using RTSGame;
using Aws.GameLift.Realtime.Event;
using Aws.GameLift.Realtime;
using System.IO;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.Runtime;
using Amazon.CognitoIdentity;
using Amazon;
using System.Text;
using System.Net.NetworkInformation;
using System.Net;
using System.Linq;

// This data structure is returned by the client service when a game match is found
[System.Serializable]
public class PlayerSessionObject
{
    public string PlayerSessionId;
    public string PlayerId;
    public string GameSessionId;
    public string FleetId;
    public string CreationTime;
    public string Status;
    public string IpAddress;
    public string Port;
}

// RTSClient is responsible for the connection to the GameLift Realtime server and for communication between
// the game and the server
public class RTSClient : MonoBehaviour
{
    // from MegaFrogRaceServer.js
    const int LOGICAL_PLAYER_OP_CODE = 100; // payload "(int)logicalPlayerID"
    const int START_COUNTDOWN_OP_CODE = 101; // payload "(float)frogHopTime"
    const int MOVE_PLAYER_OP_CODE = 102;    // payload "(int)playerToMove:(float)distance"
    const int WINNER_DETERMINED_OP_CODE = 103; //payload "(int)winningPlayer:(int)losingPlayer"

    const int SCENE_READY_OP_CODE = 200;
    const int HOP_OP_CODE = 201;

    // Used to parse server's data
    private CultureInfo _serverCulture = CultureInfo.CreateSpecificCulture("en-US");
    
    // reference to the game controller which will render the game state
    public GameController GameController;

    // Start is called before the first frame update
    void Start()
    {
        UnityInitializer.AttachToGameObject(this.gameObject);

        IsConnectedToServer = false;
        _localSimulation = StateManager.isServerSimulated;
        if (_localSimulation)
        {
            _hopTime = _localSimHopTime;
            IsConnectedToServer = true;
        }
        else
        {
            if(StateManager.startMode == StateManager.StartMode.LocalServer)
            {
                ConnectToLocalServer();
            }
            else
            {
                ConnectToGameLiftServer();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (_localSimulation)
        {
            LocalSimUpdate();
        }
        else
        {
            RunMainThreadQueueActions();
        }
    }

    public bool IsConnectedToServer { get; set; }

    public void DisconnectFromServer()
    {
        if(_client != null && _client.Connected)
        {
            _client.Disconnect();
        }
    }

    // Called when the user wants the frog to hop
    public void HopButtonPressed()
    {
        if(_localSimulation)
        {
            LocalSimHopPressed(_logicalPlayerID);
        }
        else
        {
            // inform server the hop button was pressed by local player
            Debug.Log("SendingEvent: SCENE_READY_OP_CODE");
            _client.SendEvent(HOP_OP_CODE);
        }
    }

    // used to inform the server when the scene is loaded and we know a connection
    // to the server exists. When both clients are connected and ready, the server
    // will tell the client to start the game
    public void SceneReady()
    {
        if(_localSimulation)
        {
            NotifyStartCountdown();
        }
        else
        {
            Debug.Log("SendingEvent: SCENE_READY_OP_CODE");
            var e = new Aws.GameLift.Realtime.Command.ClientEvent(SCENE_READY_OP_CODE, _client.Session.ConnectedPeerId);
            Debug.Log($"Event Target Player = {e.TargetPlayer}");
            Debug.Log($"Event Target Group = {e.TargetGroup}");
            _client.SendEvent(SCENE_READY_OP_CODE);
        }
    }

    // game server notifies us that we should start the countdown
    // and then allow input from player
    public void NotifyStartCountdown()
    {
        GameController.StartCountDown();
    }

    // game server notfies us that we should move a player and by how much
    public void NotifyMovePlayer(int playerID, float newPosition)
    {
        GameController.FrogHopTo(playerID, newPosition);
    }

    // game server notifies us that someone just won
    public void NotifyWinnerDetermined(int winningPlayerID, int losingPlayerID)
    {
        GameController.WinCelebration(winningPlayerID, losingPlayerID);
    }

    private const string DEFAULT_ENDPOINT = "127.0.0.1";
    private const int DEFAULT_TCP_PORT = 3001;
    private const int DEFAULT_UDP_PORT = 8921;

    // given a starting and ending range, finds an open UDP port to use as the listening port
    private int FindAvailableUDPPort(int firstPort, int lastPort)
    {
        var UDPEndPoints = IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners();
        List<int> usedPorts = new List<int>();
        usedPorts.AddRange(from n in UDPEndPoints where n.Port >= firstPort && n.Port <= lastPort select n.Port);
        usedPorts.Sort();
        for(int testPort = firstPort; testPort <= lastPort; ++testPort)

        {
            if(!usedPorts.Contains(testPort))
            {
                return testPort;
            }
        }
        return -1;
    }

    // common code whether we are connecting to a GameLift hosted server or
    // a local server
    private IEnumerator ConnectToServer(string ipAddr, int port, string tokenUID)
    {
        ClientLogger.LogHandler = (x) => Debug.Log(x);
        ConnectionToken token = new ConnectionToken(tokenUID, null);

        ClientConfiguration clientConfiguration = ClientConfiguration.Default();

        _client = new Aws.GameLift.Realtime.Client(clientConfiguration);
        _client.ConnectionOpen += new EventHandler(OnOpenEvent);
        _client.ConnectionClose += new EventHandler(OnCloseEvent);
        _client.DataReceived += new EventHandler<DataReceivedEventArgs>(OnDataReceived);
        _client.ConnectionError += new EventHandler<Aws.GameLift.Realtime.Event.ErrorEventArgs>(OnConnectionErrorEvent);

        int UDPListenPort = FindAvailableUDPPort(DEFAULT_UDP_PORT, DEFAULT_UDP_PORT + 20);
        if (UDPListenPort == -1)
        {
            Debug.Log("Unable to find an open UDP listen port");
            yield break;
        }
        else
        {
            Debug.Log($"UDP listening on port: {UDPListenPort}");
        }

        Debug.Log($"[client] Attempting to connect to server ip: {ipAddr} TCP port: {port} Player Session ID: {tokenUID}");
        _client.Connect(string.IsNullOrEmpty(ipAddr) ? DEFAULT_ENDPOINT : ipAddr, port, UDPListenPort, token);

        while (true)
        {
            if (_client.ConnectedAndReady)
            {
                IsConnectedToServer = true;
                Debug.Log("[client] Connected to server");
                break;
            }
            yield return null;
        }
    }

    private void ConnectToLocalServer()
    {
        var ipAddrPath = Application.dataPath + "/serverip.txt";
        var ipAddrReader = new StreamReader(ipAddrPath);
        var ipAddr = ipAddrReader.ReadToEnd();
        ipAddrReader.Close();
        
        // typically you'd use the player ID from an authentication system like GameLift,
        // however for our demo purposes, just pick a random unique ID
        StartCoroutine(ConnectToServer(ipAddr, DEFAULT_TCP_PORT, Guid.NewGuid().ToString()));
    }

    public void ActionConnectToServer(string ipAddr, int port, string tokenUID)
    {
        StartCoroutine(ConnectToServer(ipAddr, port, tokenUID));
    }

    // calls our game service Lambda function to get connection info for the Realtime server
    private void ConnectToGameLiftServer()
    {
        Debug.Log("Reaching out to client service Lambda function");

        AWSConfigs.AWSRegion = "us-east-1"; // Your region here
        AWSConfigs.HttpClient = AWSConfigs.HttpClientOption.UnityWebRequest;
        // paste this in from the Amazon Cognito Identity Pool console
        CognitoAWSCredentials credentials = new CognitoAWSCredentials(
            "us-east-1:a00a0aa0-a000-0000-0aa0-0aa00a0009a0", // Your identity pool ID here
            RegionEndpoint.USEast1 // Your region here
        );

        AmazonLambdaClient client = new AmazonLambdaClient(credentials, RegionEndpoint.USEast1);
        InvokeRequest request = new InvokeRequest
        {
            FunctionName = "ConnectClientToServer",
            InvocationType = InvocationType.RequestResponse
        };


        client.InvokeAsync(request,
            (response) =>
            {
                if(response.Exception == null)
                {
                    if(response.Response.StatusCode == 200)
                    {
                        var payload = Encoding.ASCII.GetString(response.Response.Payload.ToArray()) + "\n";
                        var playerSessionObj = JsonUtility.FromJson<PlayerSessionObject>(payload);

                        if (playerSessionObj.FleetId == null)
                        {
                            Debug.Log($"Error in Lambda: {payload}");
                        }
                        else
                        {
                            QForMainThread(ActionConnectToServer, playerSessionObj.IpAddress, Int32.Parse(playerSessionObj.Port), playerSessionObj.PlayerSessionId);
                        }
                    }
                }
                else
                {
                    Debug.LogError(response.Exception);
                }
            });
    }

    private void OnOpenEvent(object sender, EventArgs e) 
    {
        Debug.Log("[server-sent] OnOpenEvent");
    }

    private void OnCloseEvent(object sender, EventArgs e)
    {
        Debug.Log("[server-sent] OnCloseEvent");
        GameController.QuitToMainMenu();
    }

    private void OnConnectionErrorEvent(object sender, Aws.GameLift.Realtime.Event.ErrorEventArgs e)
    {
        Debug.Log($"[client] Connection Error! : ");
        GameController.QuitToMainMenu();
    }

    private void OnDataReceived(object sender, DataReceivedEventArgs e)
    {
        string data = System.Text.Encoding.Default.GetString(e.Data);
        Debug.Log($"[server-sent] OnDataReceived - Sender: {e.Sender} OpCode: {e.OpCode} data: {data}");

        switch (e.OpCode)
        {
            case Constants.LOGIN_RESPONSE_OP_CODE:
                _peerID = e.Sender;
                Debug.Log($"[client] peer ID : {_peerID}");
                break;

            case LOGICAL_PLAYER_OP_CODE:
                {
                    int logicalPlayer = -1;
                    if (int.TryParse(data, out logicalPlayer))
                    {
                        if (logicalPlayer == 0 || logicalPlayer == 1)
                        {
                            _logicalPlayerID = logicalPlayer;
                            Debug.Log($"Logical player ID assigned: {_logicalPlayerID}");
                        }
                        else
                        {
                            Debug.LogWarning($"Server tried to assign a logical player out of range: {logicalPlayer}");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Unable to parse logicalPlayer!");
                    }
                    break;
                }

            case START_COUNTDOWN_OP_CODE:
                {
                    float serverHopTime = 0.0f;
                    if (float.TryParse(data, NumberStyles.Number, _serverCulture, out serverHopTime))
                    {
                        _hopTime = serverHopTime;
                    }
                    else
                    {
                        Debug.LogWarning("Unable to parse server hop time!");
                    }
                    QForMainThread(NotifyStartCountdown);
                    break;
                }

            case MOVE_PLAYER_OP_CODE:
                {
                    int logicalPlayer = -1;
                    float distance = 0.0f;
                    string[] parts = data.Split(':');
                    if(!int.TryParse(parts[0], out logicalPlayer))
                    {
                        Debug.LogWarning("Unable to parse logicalPlayer!");
                    }
                    if(!float.TryParse(parts[1], NumberStyles.Number, _serverCulture, out distance))
                    {
                        Debug.LogWarning("Unable to parse distance!");
                    }
                    QForMainThread(NotifyMovePlayer, logicalPlayer, distance);
                    break;
                }

            case WINNER_DETERMINED_OP_CODE:
                {
                    int winner = -1;
                    int loser = -1;
                    string[] parts = data.Split(':');
                    if (!int.TryParse(parts[0], out winner))
                    {
                        Debug.LogWarning("Unable to parse winner!");
                    }
                    if (!int.TryParse(parts[1], out loser))
                    {
                        Debug.LogWarning("Unable to parse loser!");
                    }
                    QForMainThread(NotifyWinnerDetermined, winner, loser);
                    break;
                }
        }
    }
        
    public float GetHopTime()
    {
        return _hopTime;
    }

    private bool _localSimulation = false;
    private int _logicalPlayerID = 0;
    private int _peerID = -1;   // invalid peer id
    private float _hopTime = 0.0f;

    private Aws.GameLift.Realtime.Client _client;

    private Queue<Action> _mainThreadQueue = new Queue<Action>();

    private void QForMainThread(Action fn)
    {
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(() => { fn(); });
        }
    }

    private void QForMainThread<T1>(Action<T1> fn, T1 p1)
    {
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(() => { fn(p1); });
        }
    }

    private void QForMainThread<T1, T2>(Action<T1, T2> fn, T1 p1, T2 p2)
    {
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(() => { fn(p1, p2); });
        }
    }

    private void QForMainThread<T1, T2, T3>(Action<T1, T2, T3> fn, T1 p1, T2 p2, T3 p3)
    {
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(() => { fn(p1, p2, p3); });
        }
    }


    private void RunMainThreadQueueActions()
    {
        // as our server messages come in on their own thread
        // we need to queue them up and run them on the main thread
        // when the methods need to interact with Unity
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0)
            {
                _mainThreadQueue.Dequeue().Invoke();
            }
        }
    }

    /////////////////////////////////////////////////////////////
    // local simulation code
    private float[] _simPlayerPositon = { 0.0f, 0.0f };
    private float[] _simLastFrogHopTimer = { 0.0f, 0.0f };
    private const float _localSimHopTime = 4.0f;
    private const float _localSimClickPenaltyTime = 0.1f;
    private const float _finishPosition = 1.0f;
    private const float _hopLength = 0.20f; // percent of length

    private void LocalSimUpdate()
    {
        // reduce the timer each tick until 0
        for (int i = 0; i < 2; ++i)
        {
            if(_simLastFrogHopTimer[i] > 0.0f)
            {
                _simLastFrogHopTimer[i] -= Time.deltaTime;
            }
        }
    }

    private void LocalSimHopPressed(int playerID)
    {
        // hop is timed by the server simulation so players can't hop constantly
        // they have to wait until a hop is complete before hopping again
        // if they click before the frog is done, they incur a time penalty before
        // they can jump again, and it accumulates based on how many times they click
        if (_simLastFrogHopTimer[playerID] <= 0.0f)
        {
            _simLastFrogHopTimer[playerID] = _hopTime;
            _simPlayerPositon[playerID] += _hopLength;
            NotifyMovePlayer(playerID, _simPlayerPositon[playerID]);
            if (_simPlayerPositon[playerID] >= _finishPosition)
            {
                NotifyWinnerDetermined(playerID, playerID == 0 ? 1 : 0);
            }
        }
        else
        {
            _simLastFrogHopTimer[playerID] += _localSimClickPenaltyTime;
        }
    }

    public void LocalP2HopPressed()
    {
        LocalSimHopPressed(1);
    }

    public void LocalP2FirePressed()
    {

    }
}
