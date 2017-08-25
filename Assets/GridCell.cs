using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridCell : IComparable<GridCell> {
    /// <summary>
    /// Index of the cell in cells[..] do we can index it directly
    /// </summary>
    public Vector3 cellIndex;

    /// <summary>
    /// The world position center of the cell.  This is offset by the gameobject at creation time.  Moving a parent gameobject will not affect this grid cell
    /// </summary>
    public Vector3 center;
    public float cellSize;

    public QefSolver qef;
    public int edgeCount = 0;
    public Vector3 normal;
    public Vector3 vertex;
    public bool hasVertex = false;
    public int vertexIndex = -1;

    public GridCell(Vector3 cellIndex, float volumeSize, float subdivisionLevel, Vector3 worldOffset) {
        this.cellIndex = cellIndex;
        this.cellSize = volumeSize / subdivisionLevel;
        this.center = worldOffset + (cellIndex * cellSize) + (Vector3.one * (cellSize - volumeSize) / 2f);

        //find the corners of each cell, because that's what we need to query
    }

    public void AddQEF(Vector3 position, Vector3 normal) {
        if(qef == null) qef = new QefSolver();
        qef.add(position, normal);
        this.normal += normal;
        edgeCount++;
        hasVertex = true;
    }
    
    public int Compare(GridCell x, GridCell y) {
        if(x.cellIndex.x == y.cellIndex.x) {
            if(x.cellIndex.y == y.cellIndex.y) {
                if(x.cellIndex.z == y.cellIndex.z) {
                    return 0;
                } else if(x.cellIndex.z < y.cellIndex.z) {
                    return -1;
                } else {
                    return 1;
                }
            } else if(x.cellIndex.y < y.cellIndex.y) {
                return -1;
            } else {
                return 1;
            }
        } else if(x.cellIndex.x < y.cellIndex.x) {
            return -1;
        } else {
            return 1;
        }
    }

    public int CompareTo(GridCell other) {
        return Compare(this, other);
    }
}

public class GridCorner {
    public Vector3 cornerIndex;
    public Vector3 position;
    public float density;
    
    public GridCorner(Vector3 cornerIndex, Vector3 position) {
        this.cornerIndex = cornerIndex;
        this.position = position;
    }
}

public class GridEdge {
    public Vector3[] corners = new Vector3[2];
    public bool hasIntersection = false; //don't need this because we only create edges when they have intersections..right?
    public Vector3 intersection;
    public Vector3 normal;
    public Color debugColor = Color.green;

    public GridEdge(Vector3 cornerA, Vector3 cornerB, Color c) {
        corners[0] = cornerA;
        corners[1] = cornerB;
        debugColor = c;
    }

    public GridEdge(Vector3 cornerA, Vector3 cornerB) {
        corners[0] = cornerA;
        corners[1] = cornerB;
        //debugColor = c;
    }
}

public class GridFace {
    public List<GridCell> faces = new List<GridCell>();
}
