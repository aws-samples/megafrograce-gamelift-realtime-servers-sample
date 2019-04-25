# megafrogRace-gameLift-realtime-server-sample
This repository contains a sample game project using Amazon GameLift and AWS services including AWS Lambda and Amazon Cognito. This is a simple multi-player 2D racing game, but technically and functionally identical to a complete Realtime online game. It serves as an example of architecture and best practices, as well as an implementation, of a game using GameLift Realtime Servers.

# Requirements
An AWS account with access to GameLift, IAM and Amazon Cognito: https://aws.amazon.com/getting-started/

Software Tools
- Microsoft Visual Studio 2017 or higher (any edition): https://visualstudio.microsoft.com/
- Unity: https://unity.com/

SDK's and Libraries
- GameLift Realtime Client SDK: https://aws.amazon.com/gamelift/getting-started/
- AWS Mobile SDK for Unity: https://docs.aws.amazon.com/mobile/sdkforunity/developerguide/what-is-unity-plugin.html
- Demigiant DOTween: http://dotween.demigiant.com/
- This file from the node-gameloop project: https://github.com/tangmi/node-gameloop/blob/master/lib/gameloop.js


# Contents
<pre>
├── AWS                     # Lambda function and IAM policy for client service
├── ServerApp               # Script to be hosted by GameLift Realtime Servers
└── MegaFrogRace            # The root of the Unity project
    ├── Assets              # Editable assets
    │   ├── Scenes          # Unity scene definition files
    │   ├── Scripts         # C# script files that define the game logic
    │   └── Textures        # Images used by the game
    ├── Packages            # Unity packages folder
    └── ProjectSettings     # Unity project folder
</pre>

# Building the sample

# Step 1: Prepare GameLift Realtime Server
1. Download gameloop.js from above.
2. Create a zip file that contains ServerApp/MegaFrogRaceServer.js and gameloop.js in the root level folder.
3. Create a new GameLift Realtime server script with the zip file created in step 2.
4. Create a new GameLift Realtime server fleet with the script created in step 3.

# Step 2: Create client service
1. Create a new AWS Lambda and choose "Author from Scratch"
2. Create a function called ConnectClientToServer using Node.js 8.10
3. Add the script found in AWS/ClientServiceLambda.js and modify the region to match the region you created the GameLift Realtime server in, and modify the MegaFrogRaceFleetID to match the fleet ID created in the step above.
4. Create an IAM policy for the Lambda function that gives the Lambda access to the following GameLift functions
    - DescribeGameSessions
    - SearchGameSessions
    - CreateGameSession
    - CreatePlayerSession
5. Optional - use Amazon Cognito to allow access by the game client to call the client service.

# Step 3: Build game client
1. Load the MegaFrogRace project in to Unity
2. Build the GameLift Realtime Client SDK making sure to target .Net 4.5
3. From the GameLift Client SDK add the following files to the Unity project
    - GameScaleRealTimeClientSDKNet45.dll
    - Google.Protobuf.dll
    - Log4net.dll
    - SuperSocket.ClientEngine.dll
    - SuperSocket.Common.dll
    - SuperSocket.Facility.dll
    - SuperSocket.SocketBase.dll
    - WebSocket4Net.dll
4. Import the AWS Lambda package from the AWS Mobile SDK for Unity in to the project
5. Add the Demigiant DOTween library to the project follwing the instructions found at http://dotween.demigiant.com/getstarted.php
6. In the function RTSClient:ConnectToGameLiftServer replace the AWS region with the region you created the GameLift Realtime Server fleet and replace the placeholder Amazon Cognito identity pool ID with the identity pool ID you created in the steps above
7. Add both scenes included in the Unity project in to the Unity build settings
8. Add two input axes to the Unity prject settings as follows:
    - Name: HopP1   Positive Button: space
    - Name: HopP2   Positive Button: right shift

At this point the game should be playable.

# For more information or questions
- The steps in this file are condensed from the article found here: https://aws.amazon.com/blogs/gametech/
- Please contact gametech@amazon.com for any comments or requests regarding this content

## License Summary

This sample code is made available under the Apache-2.0 license. See the LICENSE file.





