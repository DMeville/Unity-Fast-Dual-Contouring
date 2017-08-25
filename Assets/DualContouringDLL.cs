using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Threading;

public class DualContouringDLL : MonoBehaviour {

    
    [DllImport("DualContouringPlugin", EntryPoint = "CreateOctreeAndDualContour")]
    public static extern void CreateOctreeDLL(int x, int y, int z, int octreeSize, float res, out int indiciesLength, out IntPtr indiciesArray, out int vertexBufferLength, out IntPtr vertexBufferArray);


    [DllImport("DualContouringPlugin", EntryPoint = "FastDualContour")]
    public static extern void FastDualContourDLL(int x, int y, int z, int cellSize, float targetPolygonPercent, out int indiciesLength, out IntPtr indiciesArray, out int vertexBufferLength, out IntPtr vertexBufferArray);


    public float res = 0f;
    public float lastRes = 0;

    public Thread generationThread;
    public List<Action> mainThreadCallbacks = new List<Action>();

    public Vector3 pos = new Vector3();
    public float startTime = 1f;

    public bool fastDC = false;

    void Start () {
        if(fastDC) StartFastDC();
        else StartOctreeDC();
    }


    /// <summary>
    /// Starts the Octree version of DC in a thread and once it returns generates a mesh
    /// </summary>
    public void StartOctreeDC() {
        generationThread = new Thread(() => { GenerateOctreeAndMesh((int)pos.x, (int)pos.y, (int)pos.z); });
        generationThread.Start();
    }

    /// <summary>
    /// starts the Fast DC in a thread and generates a mesh
    /// </summary>
    public void StartFastDC() {
        generationThread = new Thread(() => { FastDualContour((int)pos.x, (int)pos.y, (int)pos.z, 64, 100f); });
        generationThread.Start();
    }

    /// <summary>
    /// Calls into the C++ plugin to generate our mesh data.
    /// 
    /// Not super familiar with C++, so this may or may not leak memory.
    /// 
    ///x/y/z offset works (but doesn't correspond to the gameobjects position.  Gameobject pos should be 0,0,0 and we set the pos property in this class to handle offsets
    /// cell size works.  Cell size is how many voxels you create on every axis.  first cell starts at center - cellSize/2.  Cells are always 1m I think, might be good to make a way to increase res
    /// targetPolygonPercent does not work.  Not sure why..
    /// </summary>
    public void FastDualContour(int x, int y, int z, int cellSize, float targetPolygonPercent) {
        int indiciesLength;
        IntPtr indiciesArrayPtr;

        int vertexBufferLength;
        IntPtr vertexBufferArrayPtr;
        
        FastDualContourDLL(x, y, z, cellSize, targetPolygonPercent, out indiciesLength, out indiciesArrayPtr, out vertexBufferLength, out vertexBufferArrayPtr);
        int[] indiciesArray = new int[indiciesLength];
        float[] vertexBufferArray = new float[vertexBufferLength];
        //Debug.Log(vertexBufferLength);
        Marshal.Copy(indiciesArrayPtr, indiciesArray, 0, indiciesLength);
        Marshal.FreeCoTaskMem(indiciesArrayPtr);

        Marshal.Copy(vertexBufferArrayPtr, vertexBufferArray, 0, vertexBufferLength);
        Marshal.FreeCoTaskMem(vertexBufferArrayPtr);

        //Debug.Log(string.Format("{0}-{1}", indiciesLength, vertexBufferLength));
        mainThreadCallbacks.Add(() => { BuildMesh(vertexBufferArray, indiciesArray); });
    }

  
    /// <summary>
    /// x/y/z is world offset.
    /// </summary>
    public void GenerateOctreeAndMesh(int x, int y, int z) {
        int indiciesLength;
        IntPtr indiciesArrayPtr;

        int vertexBufferLength;
        IntPtr vertexBufferArrayPtr;

        CreateOctreeDLL(x, y, z, 64, res, out indiciesLength, out indiciesArrayPtr, out vertexBufferLength, out vertexBufferArrayPtr);
        int[] indiciesArray = new int[indiciesLength];
        float[] vertexBufferArray = new float[vertexBufferLength];
        //Debug.Log(vertexBufferLength);
        Marshal.Copy(indiciesArrayPtr, indiciesArray, 0, indiciesLength);
        Marshal.FreeCoTaskMem(indiciesArrayPtr);

        Marshal.Copy(vertexBufferArrayPtr, vertexBufferArray, 0, vertexBufferLength);

        //need to push this to the main thread
        mainThreadCallbacks.Add(() => { BuildMesh(vertexBufferArray, indiciesArray); });
    }

    public static int count = 0;

    /// <summary>
    /// Build mesh function.  Takes in a flat array of vertex (which is actually processed 6 at a time)
    /// because I don't know how to C++ and this was the only way that made sense to me to return
    /// each entry in the array is one component of a vec3.  goes vertexX, vertexY, vertexZ, normalX, normalY, normalZ, ...
    /// 
    /// Indicies array is the triangles list
    /// </summary>
    public void BuildMesh(float[] vertexArray, int[] indiciesArray) {
        MeshRenderer mr = this.gameObject.GetComponent<MeshRenderer>();
        MeshFilter mf = null;
        if(mr == null) {
            mr = this.gameObject.AddComponent<MeshRenderer>();
            mf = this.gameObject.AddComponent<MeshFilter>();
        } else {
            mf = this.gameObject.GetComponent<MeshFilter>();
        }

        mf.mesh.Clear();
        mr.sharedMaterial = Resources.Load("Default") as Material;

        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();


        //Debug.Log("length: " + vertexArray.Length);
        for(int i = 0; i < vertexArray.Length; i += 6) {
            //Debug.Log("i: " + i);
            verts.Add(new Vector3(vertexArray[i], vertexArray[i + 1], vertexArray[i + 2]));
            norms.Add(new Vector3(vertexArray[i + 3], vertexArray[i + 4], vertexArray[i + 5]));
        }

        mf.mesh.vertices = verts.ToArray();
        mf.mesh.normals = norms.ToArray();
        mf.mesh.triangles = indiciesArray;

        UIConsole.instance.AddText(("\n[" + count + "] - time since startup - " + Time.realtimeSinceStartup));
        count++;
    }

    public void Update() {
        if(res != lastRes) {
            //regen
            //we don't need to regen the octree, just re-contour the mesh right?
            //so that means we need a way to keep the octree stuff alive on the C++ side
            //GenerateOctreeAndMesh();
        }
        lastRes = res;

        while(mainThreadCallbacks.Count > 0) {
            Action act = mainThreadCallbacks[0];
            mainThreadCallbacks.RemoveAt(0);
            act();
        }
    }

}
