// Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

var util = require('util');
var Packet_pb = require('../src/proto/Packet_pb');
var gameloop = require('./gameloop.js') //https://github.com/tangmi/node-gameloop

///////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////
// change these to impact gameplay

const hopLength = 0.2; // the percent of the field the frog hops
const hopTime = 4.0;  // how many seconds a hop takes
const clickPenaltyTime = 0.1;  // how many seconds added to the hop time when player tries to hop during a hop

///////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////


// Example override configuration
const configuration = {
};

let players = [];
let logicalPlayerIDs = {};
let session = null;
let sessionTimeoutTimer = null;
const SESSION_TIMEOUT = 1 * 60 * 1000;  // milliseconds to wait for players to join (1 minute)


// server op codes (messages server sends)
const LOGICAL_PLAYER_OP_CODE = 100;
const START_COUNTDOWN_OP_CODE = 101;
const MOVE_PLAYER_OP_CODE = 102;
const WINNER_DETERMINED_OP_CODE = 103;

// client op codes (messages client sends)
const SCENE_READY_OP_CODE = 200;
const HOP_OP_CODE = 201;

///////////////////////////////////////////////////////////////////////////////
// Utility functions
///////////////////////////////////////////////////////////////////////////////

// note that the strings will be Base64 encoded, so they can't contain colon, comma or double quote
// This function takes a list of peers and then send the opcode and string to the peer
function SendStringToClient(peerIds, opCode, stringToSend) {
    session.getLogger().info("[app] SendStringToClient: peerIds = " + peerIds.toString() + " opCode = " + opCode + " stringToSend = " + stringToSend);

    let gameMessage = session.newTextGameMessage(opCode, session.getServerId(), stringToSend);
    let peerArrayLen = peerIds.length;

    for (let index = 0; index < peerArrayLen; ++index) {
        session.getLogger().info("[app] SendStringToClient: sendMessageT " + gameMessage.toString() + " " + peerIds[index].toString());
        session.sendMessage(gameMessage, peerIds[index]);
    };
}

///////////////////////////////////////////////////////////////////////////////
// Game code
///////////////////////////////////////////////////////////////////////////////

// leave these alone to preserve core gameplay
const finishPosition = 1.0;    // this shouldn't really ever change, the client relies on this for projecting the position
let playerPosition = [0.0, 0.0];
let hopTimer = [0.0, 0.0];
let playerReady = [false, false];
let gameLoopId = null;
const serverFrameRate = 10.0;  // this is how many frames per second the server will update the game loop

function fGameLoop(delta) {
    for (let player = 0; player < players.length; ++player) {
        if (hopTimer[player] > 0.0) {
            hopTimer[player] -= delta;
        }
    }
}

function StartGame() {
    gameLoopId = gameloop.setGameLoop(fGameLoop, 1000.0 / serverFrameRate);   // start the game loop
    SendStringToClient(players, START_COUNTDOWN_OP_CODE, hopTime.toString());   // signal clients to start the countdown
}

function StopGame() {
    session.getLogger().info("[app] StopGame - killing game session");

    // stop game loop and clear all state
    if (gameLoopId !== null) {
        gameloop.clearGameLoop(gameLoopId);
        gameLoopId = null;
    }

    playerPosition = [0.0, 0.0];
    hopTimer = [0.0, 0.0];
    playerReady = [false, false];

    players = [];

    if(session != null)
    {
        // processEnding will stop this instance of the game running
        // and will tell the game session to terminate
        session.processEnding().then(function(outcome) {
            session.getLogger().info("Completed process ending with: " + outcome);
            process.exit(0);
        });
    }
}

function ProcessHop(logicalPlayer) {
    // hop is timed by the server simulation so players can't hop constantly
    // they have to wait until a hop is complete before hopping again
    // if they click before the frog is done, they incur a time penalty before
    // they can jump again, and it accumulates based on how many times they click
    if (hopTimer[logicalPlayer] <= 0.0) {
        hopTimer[logicalPlayer] = hopTime;
        playerPosition[logicalPlayer] += hopLength;
        SendStringToClient(players, MOVE_PLAYER_OP_CODE, logicalPlayer.toString() + ":" + playerPosition[logicalPlayer].toString());
        if (playerPosition[logicalPlayer] >= finishPosition) {
            let loser = logicalPlayer == 0 ? 1 : 0; //TODO this is hard coded to two players, should make more flexible
            SendStringToClient(players, WINNER_DETERMINED_OP_CODE, logicalPlayer.toString() + ":" + loser.toString());
        }
    }
    else {
        // if the hop timer hasn't expierd, add the penalty time described above
        hopTimer[logicalPlayer] += clickPenaltyTime;
    }
}

///////////////////////////////////////////////////////////////////////////////
// App callbacks
///////////////////////////////////////////////////////////////////////////////

// Called when game server is initialized, is passed server object of current session
function init(_session) {
    session = _session;
    session.getLogger().info("[app] init(_session): ");
    session.getLogger().info(util.inspect(_session));
}

function onMessage(gameMessage) {
    session.getLogger().info("[app] onMessage(gameMessage): ");
    session.getLogger().info(util.inspect(gameMessage));

    // sender 0 is server so we don't process them 
    if (gameMessage.sender != 0) {
        let logicalSender = logicalPlayerIDs[gameMessage.sender];

        switch (gameMessage.opCode) {
            case SCENE_READY_OP_CODE:
                playerReady[logicalSender] = true;
                // have both players signaled they are ready? If so, ready to go
                if (playerReady[0] === true && playerReady[1] === true) {
                    StartGame();
                }
                break;

            case HOP_OP_CODE:
                ProcessHop(logicalSender);
                break;

            default:
                session.getLogger().info("[warning] Unrecognized opCode in gameMessage");
        };
    }
}

// On Player Connect is called when a player has passed initial validation
// Return true if player should connect
function onPlayerConnect(player) {
    session.getLogger().info("[app] onPlayerConnect: " + player.peerId)

    // once a player connects it's fine to let the game session keep going
    // it will be killed once any client disconnects
    if(sessionTimeoutTimer != null)
    {
        clearTimeout(sessionTimeoutTimer);
        sessionTimeoutTimer = null;
    }

    if(players.length >= 2)
    {
        // max of two players so reject any additional connections
        return false;
    }
    return true;
}

// onPlayerAccepted is called when a player has connected and not rejected
// by onPlayerConnect. At this point it's possible to broadcast to the player
//    session.getLogger().info("[app]");

function onPlayerAccepted(player) {
    session.getLogger().info("[app] onPlayerAccepted: player.peerId = " + player.peerId);
    // store the ID. Note that the index the player is assigned will be sent
    // to the client and determines if they are "player 0" or "player 1" independent
    // of the peerId
    players.push(player.peerId);
    session.getLogger().info("[app] onPlayerAccepted: new contents of players array = " + players.toString());

    let logicalID = players.length - 1;
    session.getLogger().info("[app] onPlayerAccepted: logical ID = " + logicalID);

    logicalPlayerIDs[player.peerId] = logicalID;
    session.getLogger().info("[app] onPlayerAccepted: logicalPlayerIDs array = " + logicalPlayerIDs.toString());

    SendStringToClient([player.peerId], LOGICAL_PLAYER_OP_CODE, logicalID.toString());
}

// On Player Disconnect is called when a player has left or been forcibly terminated
// Is only called players that actually connect to the server and not those rejected by validation
// This is called before the player is removed from the player list
function onPlayerDisconnect(peerId) {
    session.getLogger().info("[app] onPlayerDisconnect: " + peerId);
    StopGame();
}

// On Process Started is called when the process has begun and we need to perform any
// bootstrapping.  This is where the developer should insert any necessary code to prepare
// the process to be able to host a game session.
// Return true if the process has been appropriately prepared and it is okay to invoke the
// GameLift ProcessReady() call.
function onProcessStarted() {
    session.getLogger().info("Starting process...");
    session.getLogger().info("Ready to host games...");
    return true;
}

// On Start Game Session is called when GameLift creates a game session that runs this server script
// A Game Session is one instance of your game actually running. Each instance will have its
// own instance of this script.
function onStartGameSession(gameSession)
{
    session.getLogger().info("[app] onStartGameSession");
    // The game session is started by the client service Lambda function
    // If no player joins, we want to kill the game session after
    // a certain period of time so it doesn't hang around forever taking up
    // a game instance.
    sessionTimeoutTimer = setTimeout(StopGame, SESSION_TIMEOUT);
}

exports.ssExports = {
    configuration: configuration,
    init: init,
    onMessage: onMessage,
    onPlayerConnect: onPlayerConnect,
    onPlayerDisconnect: onPlayerDisconnect,
    onProcessStarted: onProcessStarted,
    onPlayerAccepted: onPlayerAccepted,
    onStartGameSession: onStartGameSession
};
