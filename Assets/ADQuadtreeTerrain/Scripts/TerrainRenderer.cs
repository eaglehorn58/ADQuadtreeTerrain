//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ADQuadtreeTerrain
{
	//	Quadtree node that will be drawn
	public struct DRAWDATA
	{
		public QtreeNode node;      // quadtree node
		public Bounds nodeAABB;		// node aabb in world space
		public ComputeBuffer vertCB;    // vertex buffer
		public ComputeBuffer indexCB;	// index buffer
	};

	public interface ITerrainRenderer
	{
		//	Destory
		void Destroy();
		//	Push draw data
		void PushDrawData(DRAWDATA data);
		//	Render routine
		void Render(Camera cam);
	}
}
