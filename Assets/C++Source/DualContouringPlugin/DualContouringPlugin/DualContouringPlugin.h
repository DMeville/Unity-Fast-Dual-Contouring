#ifdef UNITY_WIN
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif 

#define EXPORT __declspec(dllexport)

#include "octree.h"

extern "C" {
	EXPORT void CreateOctreeAndDualContour(int x, int y, int z, int octreeSize, float res, long* indexBufferLength, int **indexBufferData, long* vertexBufferLength, float **vertexBufferData);

	EXPORT void FastDualContour(int x, int y, int z, int meshScale, float targetPolygonPercent, long* indexBufferLength, int **indexBufferDaeta, long* vertexBufferLength, float **vertexBufferData);
}