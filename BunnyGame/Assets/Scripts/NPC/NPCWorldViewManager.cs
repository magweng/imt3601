﻿using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public class NPCWorldViewManager : MonoBehaviour {
    public bool debugRenderLand;        //Should NPCWorldView be rendered in the editor?
    public bool debugRenderWater;

    public Transform[] waterBodies;

    private float _cellSize;  //The size of cells in NPCWorldView
    private int _cellCount;   //Amount of cells in NPCWorldView

    // Use this for initialization
    void Start() {
        NPCWorldView.init();
        _cellSize = NPCWorldView.cellSize;
        _cellCount = NPCWorldView.cellCount;
        if (!NPCWorldView.readFromFile()) {
            calcNPCWorld();
            NPCWorldView.writeToFile();
        }
        NPCWorldView.ready = true;
    }

    

    private void calcNPCWorld() {
        float time = Time.realtimeSinceStartup;
        Debug.Log("NPCManager: Setting up NPCWorldView by detecting obstacles!");
        findObstacles(NPCWorldView.WorldPlane.LAND);
        findObstacles(NPCWorldView.WorldPlane.WATER);
        Debug.Log("NPCManager: Finished detecting obstacles for NPCWorldView, time elapsed: " + (Time.realtimeSinceStartup - time));
    }

    //Finds obstacles in every cell of NPCWorldView, and marks them as blocked
    //Areas that are closed off by blocked cells will also be blocked
    private void findObstacles(NPCWorldView.WorldPlane plane) {
        for (int y = 0; y < _cellCount; y++) {
            for (int x = 0; x < _cellCount; x++) {
                var cell = NPCWorldView.getCell(plane, x, y);
                cell.blocked = obstacleInCell(cell);
            }
        }

        NPCWorldView.worldCellData[] targets;
        //Generate targets depending on plane type
        if (plane == NPCWorldView.WorldPlane.LAND) {
            targets = new NPCWorldView.worldCellData[] { NPCWorldView.getCell(plane, 20, 20) };
        } else {
            targets = new NPCWorldView.worldCellData[waterBodies.Length];
            for (int i = 0; i < waterBodies.Length; i++) {
                int[] index = NPCWorldView.convertWorld2Cell(waterBodies[i].position);
                targets[i] = NPCWorldView.getCell(plane, index[0], index[1]);
            }
        }

        bool lastCellBlocked = false;
        for (int y = 0; y < _cellCount; y++) {
            for (int x = 0; x < _cellCount; x++) {
                var cell = NPCWorldView.getCell(plane, x, y);
                if (!cell.blocked && lastCellBlocked) 
                    this.fillAreaIfBlocked(plane, cell, targets);
                lastCellBlocked = cell.blocked;
            }
        }
    }

    //Determines if there are any colliders inside a cell
    bool obstacleInCell(NPCWorldView.worldCellData cell) {
        bool obstacle = false;
        float modifier = 1.0f;
        Vector3 halfExtents = new Vector3(_cellSize / 2, _cellSize / 2, 0) * modifier;
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
        foreach (Vector3 dir in directions) {
            Vector3 rayStart = cell.pos - dir * _cellSize / 2;
            obstacle = Physics.BoxCast(rayStart, halfExtents, dir, Quaternion.identity, _cellSize * modifier);
            if (obstacle) return obstacle;
        }
        return obstacle;
    }

    //This is basically a*, if it cant find a path from startPos to any target node, then all the nodes in
    //  the closed list are blocked nodes.
    void fillAreaIfBlocked(NPCWorldView.WorldPlane plane, NPCWorldView.worldCellData startCell, NPCWorldView.worldCellData[] targets) {
        Dictionary<Vector3, NPCWorldView.worldCellData> closed = null;
        foreach (var target in targets) {
            SortedList<float, NPCWorldView.worldCellData> open =
                new SortedList<float, NPCWorldView.worldCellData>(new NPCWorldView.DuplicateKeyComparer<float>()); //For quickly finding best node to visit
            closed = new Dictionary<Vector3, NPCWorldView.worldCellData>();                                             //For quickly looking up closed nodes

            var goal = target;
            var current = startCell;

            NPCWorldView.resetAStarData();
            current.g = 0;
            open.Add(current.f, current); //Push the start node
            while (open.Count > 0) {
                do { //Outdated cells might still be in the list
                    current = open.Values[0];
                    open.RemoveAt(0);
                } while (closed.ContainsKey(current.pos) && open.Count > 0);

                if (current.pos == goal.pos)    //Victor                                               
                    return;                     // The node is connected to the target, just return
                if (open.Count == 0 && closed.ContainsKey(current.pos))
                    break;
                //Close current tile
                closed.Add(current.pos, current);

                for (int i = 0; i < current.neighbours.Count; i++) {
                    var cell = current.neighbours[i];
                    if (!closed.ContainsKey(cell.pos) && !cell.blocked) {
                        float g = current.g + 1;
                        if (g < cell.g) { //New and better G value?
                            cell.h = Mathf.Abs(goal.x - cell.x) + Mathf.Abs(goal.y - cell.y);
                            cell.g = g;
                            open.Add(cell.f, cell);
                        }
                    }
                }
            }
        }
        // If the search made it this far, that means the node is blocked in
        for (int i = 0; i < closed.Count; i++)
            closed.ElementAt(i).Value.blocked = true;
    }

    void OnDrawGizmos() {
        if (debugRenderLand) {
            drawGizmo(NPCWorldView.WorldPlane.LAND);
        }
        if (debugRenderWater) {
            drawGizmo(NPCWorldView.WorldPlane.WATER);
            Vector3 offset = NPCWorldView.waterOffset;
            foreach (var waterbody in waterBodies) {
                int[] fuck = NPCWorldView.convertWorld2Cell(waterbody.position);
                Gizmos.color = Color.cyan;
                Vector3 cubeCenter = new Vector3(fuck[0] * _cellSize + _cellSize / 2, 0, fuck[1] * _cellSize + _cellSize / 2) + offset;
                Gizmos.DrawCube(cubeCenter, new Vector3(_cellSize, 0, _cellSize));
            }
        }
    }

    void drawGizmo(NPCWorldView.WorldPlane plane) {
        Vector3 offset = NPCWorldView.landOffset;
        if (plane == NPCWorldView.WorldPlane.WATER)
            offset = NPCWorldView.waterOffset;

        for (int y = 0; y < _cellCount; y++) {
            for (int x = 0; x < _cellCount; x++) {
                if (!NPCWorldView.getCell(plane, x, y).blocked)
                    Gizmos.color = Color.green;
                else
                    Gizmos.color = Color.red;
                Vector3 cubeCenter = new Vector3(x * _cellSize + _cellSize / 2, 0, y * _cellSize + _cellSize / 2) + offset;
                Gizmos.DrawCube(cubeCenter, new Vector3(_cellSize, 0, _cellSize));
            }
        }
        foreach (var npc in NPCWorldView.getNpcs().Values) {
            Gizmos.DrawSphere(npc.getPos(), 2);
            Vector3 dir = npc.getDir();
            Gizmos.DrawLine(npc.getPos(), npc.getPos() + dir);
        }
          
        foreach (var player in NPCWorldView.getPlayers().Values) {
            Gizmos.DrawSphere(player.getPos(), 2);
            Gizmos.DrawLine(player.getPos(), player.getPos() + player.getDir());
        }
    }
}