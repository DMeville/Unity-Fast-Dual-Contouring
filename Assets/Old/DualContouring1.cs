using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DualContouring1:MonoBehaviour {

    /// <summary>
    /// 
    ///  Don't use this code.  It was my first attempt at implimenting the DC algo
    ///  and it's messy and probably broken 
    /// 
    /// </summary>

    //every cell needs
    //8 corners
    //each corner needs an ID
    //4 edges (refs to those two corners)
    //each edge needs a position and normal
    //QEF and vertex position
    //a way to reference their neighbours that share this edge??

    public float gridScale = 1f;
    public int size = 8;
    //public List<GridCell> cells = new List<GridCell>();

    //public GridCell[,,] cells2 = new GridCell[,,];
    //we could throw this in a dictonary instead, and use a vector3 as a key?  I guess that works.  
    public Dictionary<Vector3, GridCell> cells = new Dictionary<Vector3, GridCell>();
    public static List<GridEdge> edges = new List<GridEdge>();
    public static Vector3 noiseOffset = new Vector3(0f, 0f, 0f);

    
    //public Vector3 noiseOffsetScale = 
    //these are the offsets we use from the cell center to find the cell corner positions
    //these actually need to be *=0.5 here because we want to go half of gridScale in every direction for corners
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

    //cornerNeighours[0] will return the three neighbours (if the corner is -1,-1,-1, neighbours will have two components the same, eg 1, -1, -1)
    public static readonly int[,] cornerNeighbours = {
        {1, 3, 4},
        {0, 2, 5},
        {1, 3, 6},
        {0, 2, 7},
        {0, 5, 7},
        {1, 6, 4},
        {2, 5, 7},
        {3, 4, 6}
    };

    public static float QEF_ERROR = 1e-6f;
    public static int QEF_SWEEPS = 4;
    //public static FastNoise noise = new FastNoise();
    
    public void Start() {
        //create a list of the cells and then check their edges
        //noise.SetNoiseType(FastNoise.NoiseType.PerlinFractal);
        //noise.SetFractalType(FastNoise.FractalType.FBM);
        //noise.SetFractalGain(10f);
        //noise.SetFrequency(0.0005f);;
        //noise.SetSeed(UnityEngine.Random.Range(0, 1000000));
        //3d grid
        StartCoroutine(GenerateDC());
    }

    public static List<MeshVertex> verticies = new List<MeshVertex>();
    public static List<int> triangles = new List<int>();
    public MeshFilter mf;
    public IEnumerator GenerateDC() {
        cells = new Dictionary<Vector3, GridCell>();
        edges = new List<GridEdge>();
        for(int x = 0; x < size; x++) {
            for(int y = 0; y < size; y++) {
                for(int z = 0; z < size; z++) {
                    GridCell cell = new GridCell(new Vector3(x, y, z), gridScale);
                    cells.Add(new Vector3(x, y, z), cell);
                    //yield return new WaitForSeconds(0.2f);

                }

            }
        }

        GenerateContour(cells);
        yield return new WaitForSeconds(0.2f);

    }



    /// <summary>
    /// this is super naive.  Just want to get it working
    /// </summary>
    /// <param name="cells"></param>
    public void GenerateContour(Dictionary<Vector3, GridCell> cells) {
        //for every cell that has any edges
        List<List<GridCell>> faces = new List<List<GridCell>>();
        //Debug.Log("Edges count: " + edges.Count);
        for(int i = 0; i < edges.Count; i++) {
            //if(edges[i].contoured) continue; //if we don't want duplicates
            Vector3 ao = edges[i].cornersOffset[0];
            Vector3 bo = edges[i].cornersOffset[1];

            int axis = -1;

            if(ao.x != bo.x) axis = 0; //x
            if(ao.y != bo.y) axis = 1; //y
            if(ao.z != bo.z) axis = 2; //z

            List<GridCell> neighbourCells = new List<GridCell>();
            if(axis == 0) {
                //edge is aligned on the x axis, so our offset to the surrounding cells is yz
                neighbourCells.Add(cells[edges[i].parentCell]);
                Vector3 key = edges[i].parentCell + new Vector3(0f, ao.y, ao.z);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
                key = edges[i].parentCell + new Vector3(0f, 0f, ao.z);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
                key = edges[i].parentCell + new Vector3(0f, ao.y, 0f);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
            } else if(axis == 1) {
                //ege is aligned on the y axis, so our offset to the surrounding cells is xz
                neighbourCells.Add(cells[edges[i].parentCell]);
                Vector3 key = edges[i].parentCell + new Vector3(ao.x, 0f, ao.z);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
                key = edges[i].parentCell + new Vector3(0f, 0f, ao.z);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
                key = edges[i].parentCell + new Vector3(ao.x, 0f, 0f);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
            } else if(axis == 2) {
                //xy
                neighbourCells.Add(cells[edges[i].parentCell]);
                Vector3 key = edges[i].parentCell + new Vector3(ao.x, ao.y, 0f);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
                key = edges[i].parentCell + new Vector3(ao.x, 0f, 0f);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
                key = edges[i].parentCell + new Vector3(0f, ao.y, 0f);
                if(!cells.ContainsKey(key)) {
                    //Debug.Log("");
                    continue;
                }
                neighbourCells.Add(cells[key]);
            }

            //Debug.DrawLine(neighbourCells[0].vertex, neighbourCells[1].vertex);
            //Debug.DrawLine(neighbourCells[1].vertex, neighbourCells[2].vertex);
            //Debug.DrawLine(neighbourCells[2].vertex, neighbourCells[3].vertex);
            //Debug.DrawLine(neighbourCells[3].vertex, neighbourCells[0].vertex);
            //Debug.DrawLine(neighbourCells[0].vertex, neighbourCells[2].vertex);


            faces.Add(neighbourCells);  
        }

        //Debug.Log("Faces count: " + faces.Count);
        for(int i = 0; i < faces.Count; i++) {
            //Color col = new Color(0f, 0f, 0f, 0.1f);
            //float time = 0.4f;
            //Debug.DrawLine(faces[i][2].vertex, faces[i][1].vertex, col, time);
            //Debug.DrawLine(faces[i][1].vertex, faces[i][3].vertex, col, time);
            //Debug.DrawLine(faces[i][3].vertex, faces[i][0].vertex, col, time);
            //Debug.DrawLine(faces[i][1].vertex, faces[i][0].vertex, col, time);
            //Debug.DrawLine(faces[i][0].vertex, faces[i][2].vertex, Color.black, 10f);
            triangles.Add(faces[i][0].vertexIndex);
            triangles.Add(faces[i][1].vertexIndex);
            triangles.Add(faces[i][3].vertexIndex);

            triangles.Add(faces[i][2].vertexIndex);
            triangles.Add(faces[i][1].vertexIndex);
            triangles.Add(faces[i][0].vertexIndex);
        }

        
        if(mf == null) {
            GameObject mesh = new GameObject("Mesh");
            MeshRenderer mr = mesh.AddComponent<MeshRenderer>();
            mf = mesh.AddComponent<MeshFilter>();
            mr.sharedMaterial = Resources.Load("Default") as Material;
        }
        mf.mesh.Clear();
        Vector3[] vertArray = new Vector3[verticies.Count];
        Vector3[] normArray = new Vector3[verticies.Count];
        for(int i = 0; i < verticies.Count; i++) {
            vertArray[i] = verticies[i].xyz;
            normArray[i] = verticies[i].normal;
        }

        mf.mesh.vertices = vertArray;
        mf.mesh.triangles = triangles.ToArray();
        mf.mesh.normals = normArray;
        mf.mesh.RecalculateBounds();
        //mf.mesh.RecalculateNormals()
        verticies.Clear();
        triangles.Clear();
    }

    public float updateTimer = 0.4f;
    void Update () {
        updateTimer -= Time.deltaTime;
        float noiseSpeed = 1f;
        noiseOffset += new Vector3(0.4f, 1f, 0.5f) * Time.deltaTime * noiseSpeed;
        if(updateTimer <= 0f) {
            GenerateDC();
        }
	}

    public void OnDrawGizmos() {
        //for(int i = 0; i < cells.Count; i++) {
        //    cells[i].DrawGizmos();
        //}

        foreach(GridCell c in cells.Values) {
            c.DrawGizmos();
        }

        Gizmos.color = Color.red;
        //Gizmos.DrawWireSphere(Vector3.zero, 2f);
    }

    //this should return a material id I guess, right?
    public static float density(Vector3 worldPosition) {
        //float radius = 2f;
        //Vector3 origin = new Vector3(0f, 0f, 0f);

        //float v = (Vector3.Magnitude(worldPosition - origin) - radius);
        //return v;
        return 0f;
        //float noiseScale = 100f;
        //Debug.Log(n.GetValue(worldPosition.x*noiseScale, worldPosition.y * noiseScale, worldPosition.z * noiseScale));
        //return -1f*noise.GetValueFractal((noiseOffset.x + worldPosition.x) * noiseScale, (noiseOffset.y + worldPosition.y) * noiseScale, (noiseOffset.z + worldPosition.z) * noiseScale);

        //return v >= 0 ? 1 : 0;
    }

    /// <summary>
    /// finds the place in the density function where the edge changes sign
    /// </summary>
    /// <param name="p0">Edge Start</param>
    /// <param name="p1">Edge End</param>
    /// <param name="densityFunction"> The density function. float DensityFunction(Vector3 WorldPosition)</param>
    /// <returns>The approximate intersection point in world position</returns>
    public static Vector3 ApproximateEdgeIntersection(Vector3 p0, Vector3 p1, Func<Vector3, float> densityFunction) {
        // approximate the zero crossing by finding the min value along the edge
        float minValue = 100000f;
        float t = 0f;
        float currentT = 0f;
        const int steps = 8;
        const float increment = 1f / (float)steps;
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

    [System.Serializable]
    public class GridCell {
        public int[] corners = new int[8]; //could be byte instead
        public List<GridEdge> edges = new List<GridEdge>();
        //we should store whether or not we have a vertex here I guess.  For some reason this is getting added...
        public bool hasVertex = false;
        public Vector3 vertex;
        public Vector3 normal;
        public Vector3 center; //int coordinates of the location of the cell
        public Vector3 worldPos;
        public int vertexIndex;

        private float gridScale;
        
        public GridCell(Vector3 center, float gridScale = 1f) {

            QefSolver qef = new QefSolver();

            //check each corner against the density function
            //and generate SOMETHING at that position to see if our density function is right
            //we can cache the results from each corner, as we need to check every one anyways
            //is there a smarter way
            //we can have up to 4 sign changes per cell.. 
            // 0 ---- 1 
            // |      |
            // 1------0  <---- opposite for the top face = 4 total


            //get corner positions
            //take the center (int coords), convert them to world positions, then +/- on every axis
            this.gridScale = gridScale;
            worldPos = center * gridScale;

            //need to do corners in the same order every time
            //assign the corners to their density value at that corner position
            for(int i = 0; i < 8; i++) {
                float v = DualContouring1.density(worldPos + DualContouring1.cornerOffsets[i]*0.5f * gridScale);
                //thresholding here to turn the density into an ID
                corners[i] = v >= 0 ? 1 : 0;
            }

            //check if any edges have sign changes
            //I guess only store the edges that are important, like the ones that have a sign change.
            //we need to check every corner against it's neighbour cells, so we need to know which ones are it's neighbours. 
            //Each corner has three neighbours, 
            for(int i = 0; i < 4; i++) { //but we only want to check half of them, otherwise we have doubles?
                for(int j = 0; j < 3; j++) {
                    int neighbour = cornerNeighbours[i, j];
                    if(corners[i] != corners[neighbour]) { //if this corner has a different density value than any of it's neighbours
                        //we need to be able to find neighbours based on edges...
                        //not sure how
                        //make a naive implimentation first
                        GridEdge e = new GridEdge(i, neighbour, center);
                        
                        edges.Add(e);
                        DualContouring1.edges.Add(e);

                        Vector3 edgeCornerA = worldPos + DualContouring1.cornerOffsets[e.corners[0]] * 0.5f * gridScale;
                        Vector3 edgeCornerB = worldPos + DualContouring1.cornerOffsets[e.corners[1]] * 0.5f * gridScale;
                        //need these for meshing to find shared edges I guess
                        e.cornersOffset[0] = DualContouring1.cornerOffsets[e.corners[0]];
                        e.cornersOffset[1] = DualContouring1.cornerOffsets[e.corners[1]];
                        //computing the intersection position and normal of this edge against the density function
                        e.position = DualContouring1.ApproximateEdgeIntersection(edgeCornerA, edgeCornerB, DualContouring1.density);
                        e.normal = DualContouring1.CalculateSurfaceNormal(e.position, DualContouring1.density);
                        
                        //from here we have to get the actual position of the vertex for this cell
                        //we do this using the QefSolver.  For every edge intersection add the position and normal
                        qef.add(e.position, e.normal);
                        //add the normal so we can grab it later (summed, so we have to grab normal/edges.Count
                        normal += e.normal;
                        Debug.Log("1");
                    }
                }
            }

            if(edges.Count != 0) {
                vertex = Vector3.zero;
                qef.solve(vertex, QEF_ERROR, QEF_SWEEPS, QEF_ERROR);
                Vector3 min = center + DualContouring1.cornerOffsets[0] * 0.5f * gridScale;
                Vector3 max = center + DualContouring1.cornerOffsets[6] * 0.5f * gridScale;

                if(vertex.x < min.x || vertex.x > max.x ||
                    vertex.y < min.y || vertex.y > max.y ||
                    vertex.z < min.z || vertex.z > max.z) {
                    vertex = qef.getMassPoint();
                }
                DualContouring1.verticies.Add(new MeshVertex(vertex, (normal/edges.Count).normalized));
                vertexIndex = DualContouring1.verticies.Count - 1;
                if(vertex == Vector3.zero) Debug.Log("Zero");
                Debug.Log("2");
                hasVertex = true;
            }
        }

        /// <summary>
        /// Draws some debug stuff.  Super slow!
        /// </summary>
        public void DrawGizmos() {
            ///Draws the cells
            Gizmos.color = Color.blue;
            Gizmos.color = new Color(0f, 0f, 1f, 0.11f);
            Gizmos.DrawWireCube(worldPos, Vector3.one * gridScale);

            ///For every corner
            for(int i = 0; i < 8; i++) {
                ///Draw the corner tinted towards it's density
                //Gizmos.color = Color.white;
                //Gizmos.color = Color.Lerp(Color.white, Color.black, corners[i]);
                //Gizmos.DrawSphere(worldPos + DualContouring.cornerOffsets[i] * 0.5f * gridScale, 0.05f);

                ///draw all the edges that have a sign change
                for(int j = 0; j < edges.Count; j++) {
 
                    //Vector3 edgeCornerA = worldPos + DualContouring.cornerOffsets[edges[j].corners[0]] * 0.5f * gridScale;
                    //Vector3 edgeCornerB = worldPos + DualContouring.cornerOffsets[edges[j].corners[1]] * 0.5f * gridScale;
                    //Gizmos.DrawLine(edgeCornerA, edgeCornerB);

                    /// Draw the edges intersection position and normal
                    //Gizmos.color = Color.red;
                    //Gizmos.DrawSphere(edges[j].position, 0.025f);
                    //Gizmos.DrawRay(edges[j].position, edges[j].normal * 0.1f);
                }
            }

            ///draw the Qef Solved position
            Gizmos.color = Color.blue;
            if(hasVertex) {
                //Gizmos.DrawSphere(vertex, 0.025f);
                //Gizmos.color = Color.red;
                //Gizmos.DrawRay(vertex, normal / edges.Count);
            }
                //Gizmos.DrawRay(vertex, new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)));
                //Debug.DrawRay(vertex, new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f)),Color.red, 10f);

                ///checking to make sure our corners array is correct. It is!
                //int c = 0;
                //Gizmos.color = Color.red;
                //Gizmos.DrawSphere(worldPos + DualContouring.cornerOffsets[c] * 0.5f * gridScale, 0.1f);
                //Gizmos.color = Color.green;
                //Gizmos.DrawSphere(worldPos + DualContouring.cornerOffsets[cornerNeighbours[c, 0]] * 0.5f * gridScale, 0.1f);
                //Gizmos.DrawSphere(worldPos + DualContouring.cornerOffsets[cornerNeighbours[c, 1]] * 0.5f * gridScale, 0.1f);
                //Gizmos.DrawSphere(worldPos + DualContouring.cornerOffsets[cornerNeighbours[c, 2]] * 0.5f * gridScale, 0.1f);
            }

    }

    public class GridEdge {
        public bool hasSignChange = false;
        public int[] corners = new int[2];
        public Vector3 normal;
        public Vector3 position;

        //need these things for rendering
        public Vector3 parentCell;
        public Vector3[] cornersOffset = new Vector3[2];
        public bool contoured = false;

        public GridEdge(int corner1, int corner2, Vector3 parent) {
            hasSignChange = true;
            corners[0] = corner1;
            corners[1] = corner2;
            parentCell = parent;
        }
    }
}
