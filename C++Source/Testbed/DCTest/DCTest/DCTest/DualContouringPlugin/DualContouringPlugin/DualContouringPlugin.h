#ifdef UNITY_WIN
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif 

#define EXPORT __declspec(dllexport)

#include "octree.h"

extern "C" {
	EXPORT void CreateOctreeAndDualContour(int x, int y, int z, int octreeSize, float res, long* indexBufferLength, int **indexBufferData, long* vertexBufferLength, float **vertexBufferData);
	EXPORT void FastDualContourTest();
	EXPORT void FastDualContour(int x, int y, int z, int meshScale, float targetPolygonPercent, int maxSimplifyIterations, float edgeFraction, float maxEdgeSize, float maxError, float minAngleCosine, float* debugVal, float* debugVal2, long* indexBufferLength, int **indexBufferData, long* vertexBufferLength, float **vertexBufferData, long* cellDataLength, float **cellData);
}