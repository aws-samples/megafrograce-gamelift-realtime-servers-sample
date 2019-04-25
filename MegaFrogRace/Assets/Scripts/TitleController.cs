// Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using RTSGame;
using System.Net;
using UnityEngine.UI;

public class TitleController : MonoBehaviour
{
    public Text IPAddress;
    
    // Start is called before the first frame update
    void Start()
    {
        // For local server experiements it's useful to show the IP address of this client
        IPAddress.text = "No IPv4 address!";
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                IPAddress.text = ip.ToString();
                break;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void StartGameLift()
    {
        StateManager.startMode = StateManager.StartMode.GameLift;
        StartCoroutine(LoadGameScene());
    }

    public void StartLocalServer()
    {
        StateManager.startMode = StateManager.StartMode.LocalServer;
        StartCoroutine(LoadGameScene());
    }

    public void StartLocalPlayer()
    {
        StateManager.startMode = StateManager.StartMode.NoServer;
        StartCoroutine(LoadGameScene());
    }

    private IEnumerator LoadGameScene()
    {
        var asyncLoad = SceneManager.LoadSceneAsync("game");
        while (!asyncLoad.isDone)
        {
            yield return null;
        }
    }
}
