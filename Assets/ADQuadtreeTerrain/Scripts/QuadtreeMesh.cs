//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ADQuadtreeTerrain
{
	//	quadtree node mesh
	public class QuadtreeMesh
	{
		public int nodeIndex { get; } = -1;  // Index of quadtree node
		public Bounds aabb; // aabb in terrain local space
		public float ts { get; set; } = 0;   //	time stamp of last visiting
		public float[] heights { get; set; } = null;  // vertex height buffer
		public ComputeBuffer vertCB { get; private set; } = null;

		private QuadtreeTerrain terrain = null;    //  terrain

		//	mins, maxs: aadd in local space
		public QuadtreeMesh(QuadtreeTerrain _trn, int _nodeIdx, Vector3 mins, Vector3 maxs)
		{
			terrain = _trn;
			nodeIndex = _nodeIdx;
			aabb = new Bounds();
			aabb.SetMinMax(mins, maxs);
		}

		//	create vertex compute buffer
		public void CreateVertCB(VertexBuffer vb)
		{
			vertCB = new ComputeBuffer(vb.vertNum, VertexBuffer.strideSize);
			vertCB.SetData(vb.verts);
		}

		//	Release resource
		public void Destroy()
		{
			if (vertCB != null)
			{
				vertCB.Release();
				vertCB = null;
			}
		}
	}

	//	mesh manager
	public class QuadtreeMeshMan
	{
		private QuadtreeTerrain terrain = null;    //  terrain
		private Hashtable meshTable = null;    //  candidate mesh cache

		private int tempVertNum = 0;    // number of vertex that temporary can hold
		private VertexBuffer tempVerts = null;	// temporary vertex buffer
		private float lastUpdateCacheTime = 0.0f;

		public QuadtreeMeshMan(QuadtreeTerrain _trn)
		{
			terrain = _trn;
			meshTable = new Hashtable(256);
		}

		public void Destroy()
		{
			//	Clear mesh cache
			ICollection valueColl = meshTable.Values;
			foreach (QuadtreeMesh nodeMesh in valueColl)
			{
				nodeMesh.Destroy();
			}

			meshTable.Clear();
		}

		public void Update()
		{
			//	Update mesh cache
			UpdateMeshCache();
		}

		//	update mesh cache
		void UpdateMeshCache()
		{
			if (Time.realtimeSinceStartup < lastUpdateCacheTime + 1.0f)
				return;

			lastUpdateCacheTime = Time.realtimeSinceStartup;

			const float maxStayTime = 30.0f;   // 30s
			List<int> rmList = new List<int>();

			foreach (DictionaryEntry de in meshTable)
			{
				QuadtreeMesh mesh = de.Value as QuadtreeMesh;
				if (Time.realtimeSinceStartup > mesh.ts + maxStayTime)
				{
					mesh.Destroy();
					rmList.Add((int)de.Key);
				}
			}

			foreach (int nodeIndex in rmList)
			{
				meshTable.Remove(nodeIndex);
			}
		}

		//	Get mesh for a quadtree node
		public QuadtreeMesh GetNodeMesh(int nodeIndex)
		{
			Debug.Assert(nodeIndex >= 0);

			QuadtreeMesh mesh = meshTable[nodeIndex] as QuadtreeMesh;
			return mesh;
		}

		//	request a node's mesh
		public bool RequestNodeMesh(int nodeIndex)
		{
			Debug.Assert(nodeIndex >= 0);

			//	Check in mesh cache
			QuadtreeMesh mesh = meshTable[nodeIndex] as QuadtreeMesh;
			if (mesh != null)
			{
				//	update time stamp
				mesh.ts = Time.realtimeSinceStartup;
				return true;
			}

			//	Single thread loading
			mesh = CreateNodeMesh(nodeIndex);
			if (mesh != null)
			{
				//	Push to table
				meshTable.Add(nodeIndex, mesh);
				return true;
			}
			else
			{
				Debug.Assert(mesh != null);
				return false;
			}
		}

		QuadtreeMesh CreateNodeMesh(int nodeIndex)
		{
			try
			{
				QtreeNode node = terrain.qtree.GetQtreeNode(nodeIndex);

				int rowVertNum = terrain.vertexNumInNodeRow;
				int numVert = rowVertNum * rowVertNum;

				//	Prepare temporary buffers
				if (tempVertNum != numVert)
				{
					//	In fact, except the first time, the code should never run into here,
					//	for all quadtree nodes should have same number of vertex in their inborn LOD mesh
					tempVerts = new VertexBuffer(numVert);
					tempVertNum = numVert;
				}

				int gridSize = terrain.gridSize;
				float miny = 0.0f;
				float maxy = 0.0f;

				//	Get mesh from height blender
				if (!terrain.gemBuilder.BuildQTreeNodeMesh(node.hmLeft, node.hmTop, node.gridStep,
					rowVertNum, gridSize, tempVerts, out miny, out maxy))
				{
					Debug.Assert(false);
					return null;
				}

				//	calculate mesh's aabb in local space
				Rect rcLocal = node.CalcLocalArea(terrain.gridSize);
				Vector3 mins = new Vector3(rcLocal.xMin, miny, rcLocal.yMin);
				Vector3 maxs = new Vector3(rcLocal.xMax, maxy, rcLocal.yMax);

				//	Create mesh object
				QuadtreeMesh nodeMesh = new QuadtreeMesh(terrain, nodeIndex, mins, maxs)
				{
					ts = Time.realtimeSinceStartup,
				};

				//	Create height map
				nodeMesh.heights = new float[numVert];
				for (int i = 0; i < numVert; i++)
				{
					nodeMesh.heights[i] = tempVerts.verts[i].pos.y;
				}

				//	Create vertex buffer
				nodeMesh.CreateVertCB(tempVerts);

				return nodeMesh;
			}
			catch
			{
				Debug.LogFormat("Failed to create node mesh {0}", nodeIndex);
				return null;
			}
		}
	}
}
