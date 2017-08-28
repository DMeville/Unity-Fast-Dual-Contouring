
#include "DualContouringPlugin.h"
#include <algorithm>
#include "octree.h"
#include "ng_mesh_simplify.h"
#include "fast_dc.h"

extern "C" {
	void CreateOctreeAndDualContour(int x, int y, int z, int octreeSize, float res, long* indexBufferLength, int **indexBufferData, long* vertexBufferLength, float **vertexBufferData) {
		OctreeNode* root = nullptr;
		root = BuildOctree(glm::ivec3(-octreeSize / 2) + glm::ivec3(x, y, z), octreeSize, res);
		//nodes.push_back(root);

		VertexBuffer verticies;
		IndexBuffer indicies;
		VertexData vertexData;

		//OctreeNode* root = nodes[octreeIndex];

		GenerateMeshFromOctree(root, verticies, indicies, vertexData);

		//return data.verticies;
		//sending the index buffer out
		*indexBufferLength = indicies.size();
		auto indexBufferSize = (*indexBufferLength) * sizeof(int);
		*indexBufferData = static_cast<int*>(malloc(indexBufferSize));
		memcpy(*indexBufferData, indicies.data(), indexBufferSize);

		*vertexBufferLength = vertexData.size();
		auto vertexBufferSize = (*vertexBufferLength) * sizeof(float);
		*vertexBufferData = static_cast<float*>(malloc(vertexBufferSize));
		memcpy(*vertexBufferData, vertexData.data(), vertexBufferSize);
	}

	//we can't generate the mesh at different levels without regenerating the octree I think
	//because BuildOctree calls Simplfy which is controlled by the res

	//maybe I just need to suck it up and write my own DC method in C++
	//and pull in some 3rd party simplfication lib to do some fast polygon simplifications to generate lods? 

	void FastDualContourTest() {
		printf("Starting FastDualContourTest\n");
		MeshSimplificationOptions options;
		options.targetPercentage = 0.05f; //0.05f
		options.maxIterations = 10; //10
		options.edgeFraction = 0.125f; //0.125f
		options.maxEdgeSize = 2.5f; //0.5f
		options.maxError = 1.f; //1.f
		options.minAngleCosine = 0.8f; //9.8f
		float dVal = 0;
		float dVal2 = 0;

		int x = 0;
		int y = 0;
		int z = 0;
		int cellSize = 16;

		printf("Generating Mesh\n");
		VertexData cellData;
		MeshBuffer* buffer = GenerateMesh(x, y, z, cellSize, dVal, cellData);
		printf("Generating Mesh Done\n");
		//CreateGLMesh function
		MeshBuffer* simplfiedMesh = new MeshBuffer;
		VertexData vertexData;
		IndexBuffer indicies;

		VertexData vertData;
		IndexBuffer triData;



		simplfiedMesh->numVertices = buffer->numVertices;
		simplfiedMesh->vertices = (MeshVertex*)malloc(sizeof(MeshVertex) * buffer->numVertices);
		for (int i = 0; i < buffer->numVertices; i++)
		{
			simplfiedMesh->vertices[i].xyz[0] = buffer->vertices[i].xyz[0];
			simplfiedMesh->vertices[i].xyz[1] = buffer->vertices[i].xyz[1];
			simplfiedMesh->vertices[i].xyz[2] = buffer->vertices[i].xyz[2];
			simplfiedMesh->vertices[i].xyz[3] = 1.f;

			simplfiedMesh->vertices[i].normal[0] = buffer->vertices[i].normal[0];
			simplfiedMesh->vertices[i].normal[1] = buffer->vertices[i].normal[1];
			simplfiedMesh->vertices[i].normal[2] = buffer->vertices[i].normal[2];
			simplfiedMesh->vertices[i].normal[3] = 0.f;

			simplfiedMesh->vertices[i].colour[0] = buffer->vertices[i].colour[0];
			simplfiedMesh->vertices[i].colour[1] = buffer->vertices[i].colour[1];
			simplfiedMesh->vertices[i].colour[2] = buffer->vertices[i].colour[2];
			simplfiedMesh->vertices[i].colour[3] = buffer->vertices[i].colour[3];


			/*simplfiedMesh->vertices[i].xyz[0] *= 1;
			simplfiedMesh->vertices[i].xyz[1] *= meshScale;
			simplfiedMesh->vertices[i].xyz[2] *= meshScale;
			simplfiedMesh->vertices[i].xyz[3] = 1.f;*/
		}
		printf("Buffer verts: %d\n", buffer->numVertices);
		simplfiedMesh->numTriangles = buffer->numTriangles;
		simplfiedMesh->triangles = (MeshTriangle*)malloc(sizeof(MeshTriangle)*buffer->numTriangles);
		memcpy(&simplfiedMesh->triangles[0], &buffer->triangles[0], sizeof(buffer->triangles[0]) * buffer->numTriangles);

		vec4 offset;
		offset[0] = 0.f;
		offset[1] = 0.f;
		offset[2] = 0.f;
		offset[3] = 0.f;

		/*printf("Simplifying Mesh\n");
		printf("Unsimplified vert count: %d\n", simplfiedMesh->numVertices);
		ngMeshSimplifier(simplfiedMesh, offset, options, vertexData, indicies, dVal, dVal2);
		printf("Simplifying Mesh Done\n");
		printf("Simplified vert count: %d\n", simplfiedMesh->numVertices);
*/
		for (int i = 0; i < simplfiedMesh->numVertices; i++) {
			/*vertData.push_back(simplfiedMesh->vertices[i].xyz[0]);
			vertData.push_back(simplfiedMesh->vertices[i].xyz[1]);
			vertData.push_back(simplfiedMesh->vertices[i].xyz[2]);

			vertData.push_back(simplfiedMesh->vertices[i].normal[0]);
			vertData.push_back(simplfiedMesh->vertices[i].normal[1]);
			vertData.push_back(simplfiedMesh->vertices[i].normal[2]);*/
		}

		for (int i = 0; i < simplfiedMesh->numTriangles; i++) {
			/*triData.push_back(simplfiedMesh->triangles[i].indices_[0]);
			triData.push_back(simplfiedMesh->triangles[i].indices_[1]);
			triData.push_back(simplfiedMesh->triangles[i].indices_[2]);*/
		}

		printf("Done\n");
	}

	void FastDualContour(int x, int y, int z, int cellSize, float targetPolygonPercent, int maxSimplifyIterations, float edgeFraction, float maxEdgeSize, float maxError, float minAngleCosine, float* debugVal, float* debugVal2,  long* indexBufferLength, int **indexBufferData, long* vertexBufferLength, float **vertexBufferData, long* cellDataLength, float **cellData) {
		MeshSimplificationOptions options;
		options.targetPercentage = targetPolygonPercent;
		options.maxIterations = maxSimplifyIterations;
		options.edgeFraction = edgeFraction;
		options.maxEdgeSize = maxEdgeSize;
		options.maxError = maxError;
		options.minAngleCosine = minAngleCosine;
		float dVal = 0;
		float dVal2 = 0;

		VertexData cells;
		MeshBuffer* buffer = GenerateMesh(x,y,z, cellSize, dVal, cells);

		//CreateGLMesh function
		MeshBuffer* simplfiedMesh = new MeshBuffer;
		VertexData vertexData;
		IndexBuffer indicies;

		VertexData vertData;
		IndexBuffer triData;

		

		simplfiedMesh->numVertices = buffer->numVertices;
		simplfiedMesh->vertices = (MeshVertex*)malloc(sizeof(MeshVertex) * buffer->numVertices);
		for (int i = 0; i < buffer->numVertices; i++)
		{
			simplfiedMesh->vertices[i].xyz[0] = buffer->vertices[i].xyz[0];
			simplfiedMesh->vertices[i].xyz[1] = buffer->vertices[i].xyz[1];
			simplfiedMesh->vertices[i].xyz[2] = buffer->vertices[i].xyz[2];
			simplfiedMesh->vertices[i].xyz[3] = 1.f;

			simplfiedMesh->vertices[i].normal[0] = buffer->vertices[i].normal[0];
			simplfiedMesh->vertices[i].normal[1] = buffer->vertices[i].normal[1];
			simplfiedMesh->vertices[i].normal[2] = buffer->vertices[i].normal[2];
			simplfiedMesh->vertices[i].normal[3] = 0.f;

			simplfiedMesh->vertices[i].colour[0] = buffer->vertices[i].colour[0];
			simplfiedMesh->vertices[i].colour[1] = buffer->vertices[i].colour[1];
			simplfiedMesh->vertices[i].colour[2] = buffer->vertices[i].colour[2];
			simplfiedMesh->vertices[i].colour[3] = buffer->vertices[i].colour[3];


			/*simplfiedMesh->vertices[i].xyz[0] *= 1;
			simplfiedMesh->vertices[i].xyz[1] *= meshScale;
			simplfiedMesh->vertices[i].xyz[2] *= meshScale;
			simplfiedMesh->vertices[i].xyz[3] = 1.f;*/
		}

		

		
		*debugVal2 = buffer->numVertices;

		simplfiedMesh->numTriangles = buffer->numTriangles;
		simplfiedMesh->triangles = (MeshTriangle*)malloc(sizeof(MeshTriangle)*buffer->numTriangles);
		memcpy(&simplfiedMesh->triangles[0], &buffer->triangles[0], sizeof(buffer->triangles[0]) * buffer->numTriangles);

		vec4 offset;
		offset[0] = 0.f;
		offset[1] = 0.f;
		offset[2] = 0.f;
		offset[3] = 0.f;

		
		//ngMeshSimplifier(simplfiedMesh, offset, options, vertexData, indicies, dVal, dVal2);
		
		for (int i = 0; i < simplfiedMesh->numVertices; i++) {
			vertData.push_back(simplfiedMesh->vertices[i].xyz[0]);
			vertData.push_back(simplfiedMesh->vertices[i].xyz[1]);
			vertData.push_back(simplfiedMesh->vertices[i].xyz[2]);

			vertData.push_back(simplfiedMesh->vertices[i].normal[0]);
			vertData.push_back(simplfiedMesh->vertices[i].normal[1]);
			vertData.push_back(simplfiedMesh->vertices[i].normal[2]);
		}

		for (int i = 0; i < simplfiedMesh->numTriangles; i++) {
			triData.push_back(simplfiedMesh->triangles[i].indices_[0]);
			triData.push_back(simplfiedMesh->triangles[i].indices_[1]);
			triData.push_back(simplfiedMesh->triangles[i].indices_[2]);
		}
		
		
		*debugVal = dVal;
		*debugVal2 = dVal2;

		*indexBufferLength = triData.size();
		auto indexBufferSize = (*indexBufferLength) * sizeof(int);
		*indexBufferData = static_cast<int*>(malloc(indexBufferSize));
		memcpy(*indexBufferData, triData.data(), indexBufferSize);

		*vertexBufferLength = vertData.size();
		auto vertexBufferSize = (*vertexBufferLength) * sizeof(float);
		*vertexBufferData = static_cast<float*>(malloc(vertexBufferSize));
		memcpy(*vertexBufferData, vertData.data(), vertexBufferSize);

		*cellDataLength = cells.size();
		auto cellBufferSize = (*cellDataLength) * sizeof(float);
		*cellData = static_cast<float*>(malloc(cellBufferSize));
		memcpy(*cellData, cells.data(), cellBufferSize);
	}
}