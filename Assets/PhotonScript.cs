﻿using System;
using System.Linq;
using System.Threading.Tasks;
using AzureSpatialAnchors;
using ExitGames.Client.Photon;
using Microsoft.MixedReality.Toolkit.Utilities;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonScript : MonoBehaviourPunCallbacks
{
    [SerializeField]
    GameObject cubePrefab;

    [SerializeField]
    GameObject statusSphere;

    [SerializeField]
    Material createdAnchorMaterial;

    [SerializeField]
    Material locatedAnchorMaterial;

    [SerializeField]
    Material failedMaterial;

    [SerializeField]
    GameObject haloPrefab;

    enum RoomStatus
    {
        None,
        CreatedRoom,
        JoinedRoom,
        JoinedRoomDownloadedAnchor
    }

    public int emptyRoomTimeToLiveSeconds = 120;

    RoomStatus roomStatus = RoomStatus.None;

    void Start()
    {
        PhotonNetwork.ConnectUsingSettings();
    }
    public override void OnConnectedToMaster()
    {
        base.OnConnectedToMaster();

        var roomOptions = new RoomOptions();
        roomOptions.EmptyRoomTtl = this.emptyRoomTimeToLiveSeconds * 1000;
        PhotonNetwork.JoinOrCreateRoom(ROOM_NAME, roomOptions, null);
    }
    public async override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        // Note that the creator of the room also joins the room...
        if (this.roomStatus == RoomStatus.None)
        {
            this.roomStatus = RoomStatus.JoinedRoom;
        }
        await this.PopulateAnchorAsync();

        var halo = PhotonNetwork.Instantiate(this.haloPrefab.name, Vector3.zero, Quaternion.identity);
        halo.transform.SetParent(CameraCache.Main.transform);
    }
    public async override void OnCreatedRoom()
    {
        base.OnCreatedRoom();
        this.roomStatus = RoomStatus.CreatedRoom;
        await this.CreateAnchorAsync();
    }
    async Task CreateAnchorAsync()
    {
#if !UNITY_EDITOR
        // If we created the room then we will attempt to create an anchor for the parent
        // of the cubes that we are creating.
        var anchorService = this.GetComponent<AzureSpatialAnchorService>();

        var anchorId = await anchorService.CreateAnchorOnObjectAsync(this.gameObject);

        // Put this ID into a custom property so that other devices joining the
        // room can get hold of it.
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable()
            {
                { ANCHOR_ID_CUSTOM_PROPERTY, anchorId }
            }
        );

        this.statusSphere.GetComponent<Renderer>().material = 
            string.IsNullOrEmpty(anchorId) ? this.failedMaterial : this.createdAnchorMaterial;

#endif
    }
    async Task PopulateAnchorAsync()
    {
#if !UNITY_EDITOR
        if (this.roomStatus == RoomStatus.JoinedRoom)
        {
            object keyValue = null;

            // First time around, this property may not be here so we see if is there.
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                ANCHOR_ID_CUSTOM_PROPERTY, out keyValue))
            {
                // If the anchorId property is present then we will try and get the
                // anchor but only once so change the status.
                this.roomStatus = RoomStatus.JoinedRoomDownloadedAnchor;

                // If we didn't create the room then we want to try and get the anchor
                // from the cloud and apply it.
                var anchorService = this.GetComponent<AzureSpatialAnchorService>();

                var located = await anchorService.PopulateAnchorOnObjectAsync(
                    (string)keyValue, this.gameObject);

                this.statusSphere.GetComponent<Renderer>().material =
                    located ? this.locatedAnchorMaterial : this.failedMaterial;
            }
        }
#endif
    }
    public async override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        base.OnRoomPropertiesUpdate(propertiesThatChanged);

        if (propertiesThatChanged.Keys.Contains(ANCHOR_ID_CUSTOM_PROPERTY))
        {
            await this.PopulateAnchorAsync();
        }
    }
    public void OnCreateCube()
    {
        // Position it down the gaze vector
        var position = Camera.main.transform.position + Camera.main.transform.forward.normalized * 1.2f;

        // Create the cube
        var cube = PhotonNetwork.InstantiateSceneObject(this.cubePrefab.name, position, Quaternion.identity);
    }
    static readonly string ANCHOR_ID_CUSTOM_PROPERTY = "anchorId";
    static readonly string ROOM_NAME = "HardCodedRoomName";
}