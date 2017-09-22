﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class MetworkMessageType {
    public const short MSG_PLAYERSELECT = 1000;
};

public class PlayerSelectMessage : MessageBase {
    public uint clientID;
    public int  selectedModel;
}

//
// https://docs.unity3d.com/ScriptReference/Networking.NetworkLobbyManager.html
//
public class NetworkPlayerSelect : NetworkLobbyManager {

    private string[]              _models     = { "PlayerCharacterBunny", "PlayerCharacterFox" };
    private Dictionary<uint, int> _selections = new Dictionary<uint, int>();

    // Return the unique identifier for the lobby player object instance.
    private uint getClientID(NetworkConnection conn) {
        return (conn.playerControllers[0] != null ? conn.playerControllers[0].unetView.netId.Value : 0);
    }

    // Return the model selection made by the user.
    private int getSelectedModel(uint clientID) {
        return (this._selections.ContainsKey(clientID) ? this._selections[clientID] : 0);
    }

    // Register listening for player select messages from clients.
    public override void OnStartServer() {
        base.OnStartServer();
        NetworkServer.RegisterHandler(MetworkMessageType.MSG_PLAYERSELECT, this.RecieveNetworkMessage);
    }

    //
    // This allows customization of the creation of the GamePlayer object on the server.
    // NB! This event happens after creating the lobby player (OnLobbyServerCreateLobbyPlayer - still in the lobby scene)
    // NB! When we enter this event we are in the game scene and have no access to lobby scene objects that have been destroyed.
    //
    // Load the player character with the selected animal model and all required components.
    // NB! Prefabs for this has to be stored in "Assets/Resources/Prefabs/".
    //
    public override GameObject OnLobbyServerCreateGamePlayer(NetworkConnection conn, short playerControllerId) {
        NetworkStartPosition[] spawnPoints    = Object.FindObjectsOfType<NetworkStartPosition>();
        Vector3                position       = spawnPoints[Random.Range(0, spawnPoints.Length)].transform.position;
        int                    selectedModel  = this.getSelectedModel(this.getClientID(conn));
        GameObject             playerPrefab   = Resources.Load<GameObject>("Prefabs/" + this._models[selectedModel]);
        GameObject             playerInstance = Instantiate(playerPrefab, position, playerPrefab.transform.rotation);

        //foreach (Transform model in playerInstance.transform) {
        //    model.gameObject.tag = playerInstance.tag;
        //
        //    foreach (Transform mesh in model.transform) {
        //        mesh.gameObject.tag = model.gameObject.tag;
        //    }
        //}

        return playerInstance;
    }

    // Parse the network message, and forward handling to the specific method.
    private void RecieveNetworkMessage(NetworkMessage message) {
        switch (message.msgType) {
            case MetworkMessageType.MSG_PLAYERSELECT:
                this.RecievePlayerSelectMessage(message.ReadMessage<PlayerSelectMessage>());
                break;
            default:
                Debug.Log("ERROR! Unknown message type: " + message.msgType);
                break;
        }
    }

    // Parse the player select message, and select the player model.
    private void RecievePlayerSelectMessage(PlayerSelectMessage message) {
        this.selectModel(message.clientID, message.selectedModel);
    }

    // Save the model selection made by the user.
    private void selectModel(uint clientID, int model) {
        if (!this._selections.ContainsKey(clientID))
            this._selections.Add(clientID, model);
        else
            this._selections[clientID] = model;
    }
}