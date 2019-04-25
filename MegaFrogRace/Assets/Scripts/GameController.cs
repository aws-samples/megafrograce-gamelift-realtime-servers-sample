// Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RTSGame;
using DG.Tweening;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public Transform Player1;
    public Transform Player2;
    public Transform WinPosition;
    public RTSClient Client;
    public Text WinText;

    // Start is called before the first frame update
    void Start()
    {
        WinText.text = "Waiting for other player...";
        _frogStartXPosition[0] = Player1.position.x;
        _frogXform[0] = Player1;
        _frogStartXPosition[1] = Player2.position.x;
        _frogXform[1] = Player2;
        _localWinPosition = WinPosition.position.x;
    }

    // Update is called once per frame
    void Update()
    {
        // this makes sure that the scene is loaded and we are connected to the server
        // the client then lets the server know it's ok to start the game
        if (_isPreConnection && Client.IsConnectedToServer)
        {
            Client.SceneReady();
            _isPreConnection = false;
        }

        // don't allow input when the game isn't running
        if (_isGameRunning)
        {
            if (Input.GetButtonDown("HopP1"))
            {
                Client.HopButtonPressed();
            }

            // ignore second local input if playing multiplayer
            if (StateManager.isServerSimulated)
            {
                if (Input.GetButtonDown("HopP2"))
                {
                    Client.LocalP2HopPressed();
                }
            }
        }
    }

    public void QuitToMainMenu()
    {
        Client.DisconnectFromServer();
        SceneManager.LoadScene("title");
    }

    public void FrogHopTo(int playerID, float newXPosition)
    {
        // project the new position from the server in to local coordinates
        float localNewXPosition = _frogStartXPosition[playerID] +  (newXPosition * (_localWinPosition - _frogStartXPosition[playerID]));
        _frogXform[playerID].DOMoveX(localNewXPosition, Client.GetHopTime());
        Sequence jumpSequence = DOTween.Sequence();
        jumpSequence.Append(_frogXform[playerID].DOScale(1.5f, Client.GetHopTime() / 2.0f));
        jumpSequence.Append(_frogXform[playerID].DOScale(1.0f, Client.GetHopTime() / 2.0f));
    }

    public void StartCountDown()
    {
        StartCoroutine(RunCountdown());
    }

    private IEnumerator RunCountdown()
    {
        WinText.text = "Get Ready";

        yield return new WaitForSeconds(1);

        WinText.text = "Get Set";

        yield return new WaitForSeconds(1);

        WinText.text = "GO!!!!!";
        _isGameRunning = true;

        yield return new WaitForSeconds(1);
        WinText.text = "";
    }

    public void WinCelebration(int winningPlayerID, int losingPlayerID)
    {
        _isGameRunning = false;
        WinText.text = $"Player {winningPlayerID + 1} Wins!";
        // spiral losing frog
        _frogXform[losingPlayerID].DORotate(new Vector3(0, 0, 359), 0.5f, RotateMode.FastBeyond360).SetEase(Ease.Linear).SetLoops(10, LoopType.Incremental);
        _frogXform[losingPlayerID].DOScale(0.0f, 3.0f);
    }

    private float[] _frogStartXPosition = { 0.0f, 0.0f };
    private float _localWinPosition = 0.0f;
    private Transform[] _frogXform = new Transform[2];
    private bool _isGameRunning = false;
    private bool _isPreConnection = true;
}
