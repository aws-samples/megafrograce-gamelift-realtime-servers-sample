// Copyright 2019 Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0
namespace RTSGame
{
    // store state to pass between scenes
    static class StateManager
    {
        public enum StartMode
        {
            GameLift,
            LocalServer,
            NoServer
        };


        public static StartMode startMode
        {
            get; set;
        }

        public static bool isServerSimulated
        {
            get { return startMode == StartMode.NoServer; }
        }
    }
}
