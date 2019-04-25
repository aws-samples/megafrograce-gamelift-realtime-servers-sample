// Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
const uuid = require('uuid');
const AWS = require('aws-sdk');
const GameLift = new AWS.GameLift({region: 'ap-south-1'});

const MegaFrogRaceFleetID = "fleet-00aaaa00-a000-00a0-00a0-aa00a000aa0a";

exports.handler = async (event) => {
    let response;
    let gameSessions;

    // find any sessions that have available players
    await GameLift.searchGameSessions({
        FleetId: MegaFrogRaceFleetID,
        FilterExpression: "hasAvailablePlayerSessions=true"
    }).promise().then(data => {
        gameSessions = data.GameSessions;
    }).catch(err => {
        response = err;
    });

    // if the response object has any value at any point before the end of
    // the function that indicates a failure condition so return the response
    if(response != null) 
    {
        return response;
    }

    // if there are no sessions, then we need to create a game session
    let selectedGameSession;
    if(gameSessions.length == 0)
    {
        console.log("No game session detected, creating a new one");
        await GameLift.createGameSession({
            MaximumPlayerSessionCount: 2,   // only two players allowed per game
            FleetId: MegaFrogRaceFleetID
        }).promise().then(data => {
            selectedGameSession = data.GameSession;
        }).catch(err => {
           response = err; 
        });

        if(response != null)
        {
            return response;
        }
    }
    else
    {
        // we grab the first session we find and join it
        selectedGameSession = gameSessions[0];
        console.log("Game session exists, will join session ", selectedGameSession.GameSessionId);
    }
    
    // there isn't a logical way selectedGameSession could be null at this point
    // but it's worth checking for in case other logic is added
    if(selectedGameSession != null) 
    {
        // now we have a game session one way or the other, create a session for this player
        await GameLift.createPlayerSession({
            GameSessionId : selectedGameSession.GameSessionId ,
            PlayerId: uuid.v4()
        }).promise().then(data => {
            console.log("Created player session ID: ", data.PlayerSession.PlayerSessionId);
            response = data.PlayerSession;
        }).catch(err => {
           response = err; 
        });

    }
    else
    {
        response = {
          statusCode: 500,
          body: JSON.stringify({
              message: "Unable to find game session, check GameLift API status"
          })
        };
    }

    return response;
};
