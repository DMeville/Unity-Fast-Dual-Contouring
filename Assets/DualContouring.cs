using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Runtime.InteropServices;

public class DualContouring : MonoBehaviour {

    //this class creates a grid and contours it.  It also handles switching to different subdivision levels of the grid

    //cells need to all be the same "size", but have different subdivision levels that fill the same size
    //eg.  the size could be 16u, with 2 subdivisions each cube would be 4/4

    /// <summary>
    /// The size of the volume the DC'ing will happen.  The size of each grid cell is volumeSize/subdivisionLevel;
    /// We also offset the cells by the gameobjects position, so neighbouring DC objects should be a distance of volumeSize apart
    /// </summary>
    public float volumeSize = 16;
    [Range(0, 6)]
    public int subdivisionLevel = 1;
    private int subdivisions = 1; // 1, 2, 4, 8, 16, etc.  I guess any value could work, but if do multiples we can cache the density and share it

    public GridCell[,,] cells;
    public GridCorner[,,] corners;
    public List<GridEdge> edges = new List<GridEdge>();
    public Dictionary<Vector3, List<GridFace>> faces;

    public bool drawCells = false;
    public bool drawCorners = false;
    public bool drawEdges = false;
    public bool drawEdgeIntersections = false;
    public bool drawEdgeNormals = false;
    public bool drawVerts = false;
    public bool drawVertNormals = false;
    public float vertScale = 0.1f;
    public float normalsScale = 0.1f;

    private int lastSubdivions = -1;
    public float sphereRadius = 3.5f;
    private float lastSphereRadius;

    public bool doAutoUpdate = false;

    //importing Density function from the DualContouringPlugin.dll
    [DllImport("DualContouringPlugin", EntryPoint = "TestSort")]
    public static extern float DensityDLL(float x, float y, float z);

    [DllImport("DualContouringPlugin", EntryPoint = "CreateOctree")]
    public static extern OctreeMeshData CreateOctreeDLL(int x, int y, int z, int octreeSize, out int indiciesLength, out IntPtr indiciesArray, out int vertexBufferLength, out IntPtr vertexBufferArray);

   

    public float Density(Vector3 worldPos) {


        //return DensityDLL(worldPos.x, worldPos.y, worldPos.z); //12ms for sphere using DLL, 62ms for sphere not using DLL in deep profile mode
        //sphere test                                           //55ms vs                    63ms when not using deep profile
        //                                                      //350ms vs                   480ms when at highest subdiv
        //                                                      //so moderately faster... wonder what would happen if we threaded...
        Vector3 origin = new Vector3(0f, 0f, 0f);
        float v = (Vector3.Magnitude(worldPos - origin) - sphereRadius);
        return Mathf.Clamp(v, -1f, 1f);


        ////Vector3 origin = new Vector3(0f, 0f, 0f);
        ////float v = Mathf.Max(Mathf.Min(worldPos.y - 5f, (Vector3.Magnitude(worldPos - origin) - sphereRadius)), (Vector3.Magnitude(worldPos + (Vector3.up * -3f) - origin) - sphereRadius/2f)*-1f);
        ////return Mathf.Clamp(v, -1f, 1f);

        //float d = (Mathf.Sin((worldPos.y - 0.0527f) * Mathf.Rad2Deg) * Mathf.Cos(worldPos.x * Mathf.Rad2Deg)) * 2f;
        //return Mathf.Clamp(d, -1f, 1f);
    }

    //we could try importing nickgildea's C++ dual contouring lib and use that directly with Unity, to just output the meshes somehow?
   public struct OctreeMeshData {
        public int index;
    }

    public void Update() {
        if(!doAutoUpdate) return;
        //Debug.LogError("Test constructing a terrain from a C++ plugin");
        //if that works, we can start offloading the heaviest parts of this into a plugins.  
        //I don't know what would be the heaviest part
        //density function stuff, and finding the intersection points and stuff?
        //probably
        //if it's MUCH faster, we could probably just dumbly query the density function stuff from a plugin
        //but make sure to profile this stuff
        if(lastSubdivions != subdivisionLevel || lastSphereRadius != sphereRadius) {
            //dirty, regen
            edges.Clear();
            Start();
        }

        lastSubdivions = subdivisionLevel;
        lastSphereRadius = sphereRadius;
    }

   

    public void Start() {
        
        //List<List<List<List<int>>>> t = new List<List<List<List<int>>>>();
        //t[10][10][10] = new List<int>();
        //int[,,] t = new int[,,];
        //t[0, 0, 0] = 10;

        //given three ints, we return a list of faces that have those three ints as their sorted first face.
        //We need to be able to index the list directly with those three ints quickly.
        //we cant set the size at start because we don't know the max size of the [x,y,z]
        //t[x,y,z] -> List<GridFace> where GridFace f.faces[0].cellIndex.xyz == xyz
        //t[10,1000,10] needs to work so we can put a list there if it doesn't exist
        //t[10,1001,10] needs to work

        lastSubdivions = subdivisionLevel;
        subdivisions = (subdivisionLevel == 0) ? 1 : (int)Mathf.Pow(2, subdivisionLevel);

        cells = new GridCell[subdivisions, subdivisions, subdivisions];
        corners = new GridCorner[subdivisions+1, subdivisions+1, subdivisions+1];
        edges = new List<GridEdge>();
        faces = new Dictionary<Vector3, List<GridFace>>();
        for(int x = 0; x < subdivisions; x++) {
            for(int y = 0; y < subdivisions; y++) {
                for(int z = 0; z < subdivisions; z++) {
                    cells[x, y, z] = new GridCell(new Vector3(x, y, z), volumeSize, subdivisions, this.transform.position);
                }
            }
        }

        //should we find the corners and store them here? with refs to the gridCells so we don't get duplicates?  We need to be able to 
        //access neighbours to share edges, so it would make sense to do it outside.

        //this loop could be combined with with the loop above, if we modify it to use previous entries instead of next entires, right?
        //maybe.  Try it later if it's a bottleneck
        for(int x = 0; x < subdivisions+1; x++) {
            for(int y = 0; y < subdivisions+1; y++) {
                for(int z = 0; z < subdivisions+1; z++) {
                    //for every grid cell, create it's corners and store them in the corners[,,] list.
                    //Also grab the neighbour cells that share that corner and give them that index too
                    bool lx = (x == subdivisions  ? true : false); //last x index, we need to switch to the +1 edge
                    bool ly = (y == subdivisions  ? true : false);
                    bool lz = (z == subdivisions  ? true : false);

                    int xo = 0;
                    int yo = 0;
                    int zo = 0;

                    Vector3 cornerOff = cornerOffsets[0];

                    if(lx) {
                        xo = -1;
                        cornerOff.x = 1;
                    }
                    if(ly) {
                        yo = -1;
                        cornerOff.y = 1;
                    }
                    if(lz) {
                        zo = -1;
                        cornerOff.z = 1;
                    }

                    GridCell cell = cells[x + xo, y + yo, z + zo];
                    GridCorner corner = new GridCorner(new Vector3(x, y, z), cell.center + (cell.cellSize * cornerOff*0.5f));
                    corner.density = Density(corner.position);
                    corners[x, y, z] = corner;
                }
            }
        }
        Debug.Log("Total Corners: " + corners.Length);

        for(int x = 0; x < subdivisions; x++) {
            for(int y = 0; y < subdivisions; y++) {
                for(int z = 0; z < subdivisions; z++) {

                    //this is to smartly add the edges to a list, to make sure we don't have duplicates.
                    //it's gross, but it should be pretty fast


                    //far-end corner (x) and the edges created with it's neighbours (n)
                    //                n____X
                    //                /    /|
                    //               .____n |
                    //               |    | n
                    //               .____./

                    CheckEdge(x, y, z, 0, 1, 1, 1, 1, 1); 
                    CheckEdge(x, y, z, 1, 0, 1, 1, 1, 1);
                    CheckEdge(x, y, z, 1, 1, 0, 1, 1, 1);

                    //need every permutation of x/y/z with any 0 component

                    //100, 010, 001
                    //110, 101,011

                    //this was all figured out with trial and error
                    //depedngin on which edge we're on (and if it's the first corner) we fill in the first half of the edges,
                    //then on the next iteration we add on the next HALF, (as shown in the diagram above)
                    //so that we don't get any duplicate edges (like in a swapped order)

                    if(x == 0 && y == 0 && z == 0) {
                        CheckEdge(x, y, z, 0, 0, 0, 0, 0, 1);
                        CheckEdge(x, y, z, 0, 0, 0, 0, 1, 0);
                        CheckEdge(x, y, z, 0, 0, 0, 1, 0, 0);
                        CheckEdge(x, y, z, 0, 1, 1, 0, 0, 1);
                        CheckEdge(x, y, z, 1, 0, 1, 0, 0, 1);
                        CheckEdge(x, y, z, 0, 1, 0, 0, 1, 1);
                        CheckEdge(x, y, z, 0, 1, 0, 1, 1, 0);
                        CheckEdge(x, y, z, 1, 0, 0, 1, 0, 1);
                        CheckEdge(x, y, z, 1, 0, 0, 1, 1, 0);
                    } else if(x != 0 && y == 0 && z == 0) {
                        CheckEdge(x, y, z, 1, 0, 0, 1, 0, 1);
                        CheckEdge(x, y, z, 1, 0, 0, 1, 1, 0);
                        CheckEdge(x, y, z, 1, 0, 0, 0, 0, 0);
                        CheckEdge(x, y, z, 1, 0, 1, 0, 0, 1);
                        CheckEdge(x, y, z, 1, 1, 0, 0, 1, 0);
                    } else if(x == 0 && y != 0 && z == 0) {
                        CheckEdge(x, y, z, 0, 1, 0, 0, 0, 0);
                        CheckEdge(x, y, z, 0, 1, 1, 0, 0, 1);
                        CheckEdge(x, y, z, 0, 1, 0, 0, 1, 1);
                        CheckEdge(x, y, z, 0, 1, 0, 1, 1, 0);
                        CheckEdge(x, y, z, 1, 0, 0, 1, 1, 0);
                    } else if(x == 0 && y == 0 && z != 0) {
                        CheckEdge(x, y, z, 0, 0, 0, 0, 0, 1);
                        CheckEdge(x, y, z, 0, 1, 1, 0, 0, 1);
                        CheckEdge(x, y, z, 1, 0, 1, 0, 0, 1);
                        CheckEdge(x, y, z, 0, 1, 0, 0, 1, 1);
                        CheckEdge(x, y, z, 1, 0, 0, 1, 0, 1);
                    } else if(x != 0 && y == 0 & z != 0) {
                        CheckEdge(x, y, z, 1, 0, 0, 1, 0, 1);
                        CheckEdge(x, y, z, 1, 0, 1, 0, 0, 1);
                    } else if(x != 0 && y != 0 && z == 0) {
                        CheckEdge(x, y, z, 1, 1, 0, 1, 0, 0);
                        CheckEdge(x, y, z, 0, 1, 0, 1, 1, 0);
                    } else if(x == 0 && y != 0 && z != 0) {
                        CheckEdge(x, y, z, 0, 1, 1, 0, 0, 1);
                        CheckEdge(x, y, z, 0, 1, 0, 0, 1, 1);
                    }
                }
            }
        }

        Debug.Log("Total Edges: " + edges.Count);

        //create corners for every cell.  Find it's neighbours in the direction of the corner and assign 

        //create edges for sign changes.
        //in order to find sign changes we need to check each corners neighbours and see if they have a sign change
        //if we do, we actually creat the edge, and add the intersection to neighour cells QEF's

        //an edge is two corners.  We can get corners by indicies.
        //we need to make sure we don't have duplicates though

        //we need to know the cells that share an edge, not verticies really

        //when we create the mesh we store neighbouring edges by faces.  Can we presort the cells (the four cells used to construct the face)
        //so that they are always in some predetermined order, so when we add a new face we can check if it already exists before
        //adding it to the face list.  This will prevent us from getting duplicates in the face list

        //shoudl we keep a shortlist of faces that have verts instead of searching through all the grid cells again?
        int vertcount = 0;
        Vector3 vertex = Vector3.zero;
        for(int x = 0; x < subdivisions; x++) {
            for(int y = 0; y < subdivisions; y++) {
                for(int z = 0; z < subdivisions; z++) {
                    if(cells[x, y, z].hasVertex) {
                        vertcount++;
                        GridCell cell = cells[x, y, z];
                        cell.vertex = Vector3.zero;
                        cell.qef.solve(cell.vertex, 1e-6f, 4, 1e-6f);

                        Vector3 min = (cell.center - (Vector3.one*(cell.cellSize / 2f)));
                        Vector3 max = (cell.center + (Vector3.one * (cell.cellSize / 2f)));

                        if(cell.vertex.x <= min.x || cell.vertex.x >= max.x ||
                            cell.vertex.y <= min.y || cell.vertex.y >= max.y ||
                            cell.vertex.z <= min.z || cell.vertex.z >= max.z) {
                            cell.vertex = cell.qef.getMassPoint();
                        }

                        //cell.vertex = cell.qef.getMassPoint();

                        //Debug.Log("xyz:" + x + " : " + y + " : " + z);
                        //Debug.Log("Vertex: " + cell.vertex.ToString());
                        //Debug.Log("Edges: " + cell.edgeCount);
                    }
                }
            }
        }

        Debug.Log("Total Verts: " + vertcount);
        Debug.Log("Total Faces: " + faces.Count);

        //construct the mesh
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

        Vector3[] vertArray = new Vector3[faces.Count*4];
        Vector3[] normArray = new Vector3[faces.Count*4];
        List<int> triList = new List<int>();
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();

        foreach(List<GridFace> c in faces.Values) {
            for(int i = 0; i < c.Count; i++) {
                GridFace f = c[i];

                GridCell c0 = f.faces[0];
                GridCell c1 = f.faces[1];
                GridCell c2 = f.faces[2];
                GridCell c3 = f.faces[3];

                if(c0.vertexIndex == -1) {
                    verts.Add(c0.vertex);
                    c0.vertexIndex = verts.Count - 1;
                    norms.Add((c0.normal / c0.edgeCount).normalized);
                }

                if(c1.vertexIndex == -1) {
                    verts.Add(c1.vertex);
                    norms.Add((c1.normal / c1.edgeCount).normalized);
                    c1.vertexIndex = verts.Count - 1;
                }

                if(c2.vertexIndex == -1) {
                    verts.Add(c2.vertex);
                    norms.Add((c2.normal / c2.edgeCount).normalized);
                    c2.vertexIndex = verts.Count - 1;
                }

                if(c3.vertexIndex == -1) {
                    verts.Add(c3.vertex);
                    norms.Add((c3.normal / c3.edgeCount).normalized);
                    c3.vertexIndex = verts.Count - 1;
                }

                triList.Add(c0.vertexIndex);
                triList.Add(c1.vertexIndex);
                triList.Add(c3.vertexIndex);

                triList.Add(c3.vertexIndex);
                triList.Add(c2.vertexIndex);
                triList.Add(c0.vertexIndex);

                //every time we add a vertex to the list, we assign it's vertex index, 
                //we don't want to add the vertex if it's vertex index is NOT -1, it means we've already assigned it.
                //will we get winding order issues? YEP.  This entire system won't work with the way we've tried to smartly sort stuff because
                //sorting messes with the winding order!
                //can we do some magic with the winding order here? order the triangles differently based on...something?
            }
        }

        mf.mesh.vertices = verts.ToArray();
        mf.mesh.triangles = triList.ToArray();
        mf.mesh.normals = norms.ToArray();

    }



    //caxo - corner A x offset
    //cbyo - corner B y offset, etc
    //should be faster to just use ints instead of passing in vec3's, might be negligicable, but isn't too much more compliated
    /// <summary>
    /// Checks the edge created beteween the two points xyz+ cornerAXYZoffset and xyz + cornerBXYZoffset for a sign change between the densities of the corners
    /// if there is a sign change the edge is added to a list and the neighbouring cells that share this edge have the edge added to their QEF solver
    /// </summary>
    public void CheckEdge(int x, int y, int z, int caxo, int cayo, int cazo, int cbxo, int cbyo, int cbzo) {
        
        GridCorner cA = corners[x + caxo, y + cayo, z + cazo];
        GridCorner cB = corners[x + cbxo, y + cbyo, z + cbzo];

       

        List<GridCell> facesTemp = new List<GridCell>();
        if(cA.density * cB.density <= 0f) { //different sign
            GridEdge e = new GridEdge(cA.position, cB.position);

            int intersectionSteps = 8;
            if(subdivisionLevel == 1) intersectionSteps = 16;
            
            e.intersection = ApproximateEdgeIntersection(cA.position, cB.position, Density, intersectionSteps);
            e.normal = CalculateSurfaceNormal(e.intersection, Density);
            //e.intersection = e.normal = Vector3.zero;

            edges.Add(e);
            #region swapping and comments
            //need to find every cell that shares this edge so we can do QEF solve stuff per cell
            //get start corner indicies, sorted by smallest corner first 
            //then find the direction we're going on (offset from start to end)
            //then just manually find neighbours

            #region swapping logic
            //swap so they're ordred properly
            //could do this in the density block only when we need to
            bool swap = false;
            if(x + caxo < x + cbxo) {
                //good
            } else if(x + caxo == x + cbxo) { //same check next
                if(y + cayo < y + cbyo) {
                    //good
                } else if(y + cayo == y + cbyo) { //same, check next
                    if(z + cazo < z + cbzo) {
                        //good
                    } else if(z + cazo == z + cbzo) {
                        //same...all components were the same..  This should never happen
                    } else {
                        swap = true;
                    }
                } else {
                    swap = true;
                }
            } else {
                swap = true;
            }
            #endregion

            int sx;
            int sy;
            int sz;
            int dx;
            int dy;
            int dz;

            if(swap) {
                sx = x + cbxo;
                sy = y + cbyo;
                sz = z + cbzo;

                dx = (caxo - cbxo);
                dy = (cayo - cbyo);
                dz = (cazo - cbzo);
            } else {
                sx = x + caxo;
                sy = y + cayo;
                sz = z + cazo;

                dx = (cbxo - caxo);
                dy = (cbyo - cayo);
                dz = (cbzo - cazo);
            }
            #endregion

            //we should have six directions
            if(dx == 1 && dy == 0 && dz == 0) {
                //cells offsets [0,0,0] [0,0,-1] [0,-1,-1] [0,-1,0]
                //check each offset to see if it exists (and doesn't cause an out of range error)
                //if it exists add to the QEF and solve and stuff
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, 0, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0,  0, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, -1, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, -1,  0));


            } else if(dx == -1 && dy == 0 && dz == 0) {
                //this case will never happen, because we sort on x first so that the direction is always+.  
                //If x *was* to be -, we would have swapped corners
                //but lets just fill it in anyways
                //cells offsets [-1,0,0] [-1,0,-1] [-1,-1,-1] [-1,-1,0]
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, 0, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, 0, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, -1, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, -1, 0));
            } else if(dx == 0 && dy == 1 && dz == 0) {
                //cell offsets [0,0,-1] [0,0,0] [-1,0,0] [-1,0,-1]
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, 0, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, 0, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, 0, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, 0, -1));
            } else if(dx == 0 && dy == -1 && dz == 0) {
                //cell offsets [0,-1,0] [0,-1,-1] [-1,-1,-1] [-1,-1,0]
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, -1, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, -1, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, -1, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, -1, -1));
            } else if(dx == 0 && dy == 0 && dz == 1) {
                //cell offsets [0,0,0] [0,-1,0] [-1,-1,0] [-1,0,0]
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, 0, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, -1, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, -1, 0));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, 0, 0));
            } else if(dx == 0 && dy == 0 && dz == -1) {
                //cell offsets [0,0,-1] [0,-1,-1] [-1,-1,-1] [-1,0,-1]
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, 0, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, 0, -1, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, -1, -1));
                facesTemp.Add(AddToCellQEF(e, sx, sy, sz, -1, 0, -1));
            }

            if(facesTemp.Count == 4) {
                facesTemp.Sort();
                //need to sort the faces so we don't get any duplicates
                //and then we need to check to make sure the faces list doesn't contain this face
                GridCell ft0 = facesTemp[0];
                //Debug.Log(ft0 == null);
                //Debug.Log(ft0.cellIndex == null);
                Vector3 index = new Vector3(ft0.cellIndex.x, ft0.cellIndex.y, ft0.cellIndex.z);
                
                List<GridFace> fl = null;
                if(faces.ContainsKey(index)) {
                    fl = faces[index];
                }

                //we need to check every element in FL to make sure we don't already have this face
                 
                if(fl == null) {
                    //cant check because this face doesn't have a spot in the list yet
                    //so we can just add it because it doesn't exist, so this will be the first one
                    fl = new List<GridFace>();
                    GridFace f = new GridFace();
                    f.faces.Add(facesTemp[0]);
                    f.faces.Add(facesTemp[1]);
                    f.faces.Add(facesTemp[2]);
                    f.faces.Add(facesTemp[3]);
                    fl.Add(f);
                    faces[index] = fl;
                } else {
                    if(fl.Count == 0) {
                        //cant check.  For some reason the list was created, but never added to
                        //add because it doesn't exist yet
                        GridFace f = new GridFace();
                        f.faces.Add(facesTemp[0]);
                        f.faces.Add(facesTemp[1]);
                        f.faces.Add(facesTemp[2]);
                        f.faces.Add(facesTemp[3]);
                        fl.Add(f);
                    } else {
                        for(int i = 0; i < fl.Count; i++) {
                            //we can skip checking facesTemp[0].cellIndex against fl[0][0] because we know they're the same
                            //because that's how we sort the main list anyways
                            if(fl[i].faces[1] == facesTemp[1]) {
                                //second cell is the same
                                if(fl[i].faces[2] == facesTemp[2]) {
                                    //third cell is the same
                                    if(fl[i].faces[3] == facesTemp[3]) {
                                        //last cell is the same... all match, so we don't want to add this faceTemp to the real list
                                        //because it will be a duplicate
                                    } else {
                                        //diff
                                        GridFace f = new GridFace();
                                        f.faces.Add(facesTemp[0]);
                                        f.faces.Add(facesTemp[1]);
                                        f.faces.Add(facesTemp[2]);
                                        f.faces.Add(facesTemp[3]);
                                        fl.Add(f);
                                        break;
                                    }
                                } else {
                                    //diff
                                    GridFace f = new GridFace();
                                    f.faces.Add(facesTemp[0]);
                                    f.faces.Add(facesTemp[1]);
                                    f.faces.Add(facesTemp[2]);
                                    f.faces.Add(facesTemp[3]);
                                    fl.Add(f);
                                    break;
                                }
                            } else {
                                //diff
                                GridFace f = new GridFace();
                                f.faces.Add(facesTemp[0]);
                                f.faces.Add(facesTemp[1]);
                                f.faces.Add(facesTemp[2]);
                                f.faces.Add(facesTemp[3]);
                                fl.Add(f);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds the edge data to the cell at startXYZ + cornerOffsetXYZ if it exists (not out of range)
    /// return the cell so we can preprocess for meshing?
    /// </summary>
    public GridCell AddToCellQEF(GridEdge e, int sx, int sy, int sz, int cox, int coy, int coz) {
        int x = sx + cox;
        int y = sy + coy;
        int z = sz + coz;

        if(x >=0 && x < subdivisions &&
            y >=0 && y < subdivisions &&
            z >=0 && z < subdivisions) {
            //it exists
            GridCell cell = cells[x, y, z];
            cell.AddQEF(e.intersection, e.normal);
            return cell;
        } else {
            //NOPE! Chuck Testa
            return null;
        }
    }
 

    public void OnDrawGizmos() {
        //draw all the cells

        if(cells == null) return;

        //debug checking density
        //for(int x = -10; x<10; x++) {
        //    for(int y = -10; y < 0; y++) {
        //        for(int z = -10; z < 10; z++) {
        //            float d = Density(new Vector3(x, y, z));
        //            Gizmos.color = Color.Lerp(Color.green, Color.Lerp(Color.blue, Color.red, (d + 0.5f) / 1f), (d + 1f) / 2f);
        //            Gizmos.DrawSphere(new Vector3(x,y,z), 0.1f * vertScale);
        //        }
        //    }
        //}

        if(drawCells) {
            foreach(GridCell c in cells) {
                Gizmos.color = new Color(0f, 0f, 1f, 0.1f);
                if(c.hasVertex) Gizmos.color = new Color(0f, 1f, 0f, 0.1f);
                Gizmos.DrawWireCube(c.center, new Vector3(c.cellSize, c.cellSize, c.cellSize));
            }
        }

        if(drawCorners) {
            foreach(GridCorner c in corners) {
                Gizmos.color = Color.Lerp(Color.blue, Color.red, (c.density + 1f) / 2f);
                Gizmos.DrawSphere(c.position, 0.1f*vertScale);
            }
        }

        if(drawEdgeIntersections) {
            foreach(GridEdge e in edges) {
                //Gizmos.color = e.debugColor;
                //Gizmos.DrawLine(e.corners[0], e.corners[1]);
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(e.intersection, 0.1f*vertScale);
                if(drawEdgeNormals) {
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(e.intersection, e.normal*normalsScale);
                }
            }
        }

        if(drawEdges) {
            foreach(GridEdge e in edges) {
                //Gizmos.color = e.debugColor;
                //Gizmos.DrawLine(e.corners[0], e.corners[1]);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(e.corners[0], e.corners[1]);
                
            }
        }

        if(drawVerts) {
            foreach(GridCell c in cells) {
                if(c.hasVertex) {
                    Gizmos.color = Color.black;
                    Gizmos.DrawSphere(c.vertex, 0.1f*vertScale);

                    if(drawVertNormals) {
                        Gizmos.color = Color.red;
                        Gizmos.DrawRay(c.vertex, (c.normal / c.edgeCount)*normalsScale);
                    }
                }
            }
        }
    }

    

    //ApproximateEdgeIntersection and CalculateSurfaceNormal have a lot of calls to the desity function. 
    //This has the potential to be a bottleneck, especially in complex density functions

    /// <summary>
    /// finds the place in the density function where the edge changes sign
    /// </summary>
    /// <param name="p0">Edge Start</param>
    /// <param name="p1">Edge End</param>
    /// <param name="densityFunction"> The density function. float DensityFunction(Vector3 WorldPosition)</param>
    /// <returns>The approximate intersection point in world position</returns>
    public static Vector3 ApproximateEdgeIntersection(Vector3 p0, Vector3 p1, Func<Vector3, float> densityFunction, int steps) {
        // approximate the zero crossing by finding the min value along the edge
        float minValue = 100000f;
        float t = 0f;
        float currentT = 0f;
        float increment = 1f / (float)steps;
        while(currentT <= 1.0f) {
            Vector3 p = p0 + ((p1 - p0) * currentT);
            float density = Mathf.Abs(densityFunction(p));
            if(density < minValue) {
                minValue = density;
                t = currentT;
            }

            currentT += increment;
        }

        return p0 + ((p1 - p0) * t);
    }

    /// <summary>
    /// Approximates the surface normal of the density function at P
    /// </summary>
    /// <param name="p"></param>
    /// <returns></returns>
    public static Vector3 CalculateSurfaceNormal(Vector3 p, Func<Vector3, float> densityFunction) {
        float H = 0.001f;
        float dx = densityFunction(p + new Vector3(H, 0.0f, 0.0f)) - densityFunction(p - new Vector3(H, 0.0f, 0.0f));
        float dy = densityFunction(p + new Vector3(0.0f, H, 0.0f)) - densityFunction(p - new Vector3(0.0f, H, 0.0f));
        float dz = densityFunction(p + new Vector3(0.0f, 0.0f, H)) - densityFunction(p - new Vector3(0.0f, 0.0f, H));

        return new Vector3(dx, dy, dz).normalized;
    }

    ///These are not used anywhere anymore
    //since we want shared corners, this is the worked out shared cells relative to the corner index.
    //These cells may not exist, so we need to make sure they're within the range before we try and index that element in cells
    #region
    public static readonly Vector3[] sharedCornerCells = {
        new Vector3(-1, -1, -1),
        new Vector3( 0, -1, -1),
        new Vector3(-1,  0, -1),
        new Vector3( 0,  0, -1),
        new Vector3( 0,  0,  0),
        new Vector3(-1, -1,  0),
        new Vector3( 0, -1,  0),
        new Vector3(-1,  0,  0)
    };

    //these actually need to be *=0.5 here because we want to go half of gridScale in every direction for corners
    //since grid center is in the center of the cell
    public static readonly Vector3[] cornerOffsets = {
            new Vector3( -1, -1, -1 ), //0    
            new Vector3( -1, -1,  1 ), //1    
            new Vector3( -1,  1,  1 ), //2     
            new Vector3( -1,  1, -1 ), //3    
            new Vector3(  1, -1, -1 ), //4    
            new Vector3(  1, -1,  1 ), //5     
            new Vector3(  1,  1,  1 ), //6     
            new Vector3(  1,  1, -1 )  //7    
    };
    #endregion
}
