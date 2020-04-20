﻿//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ADQuadtreeTerrain
{
	public class GeometryBuilder
	{
		private QuadtreeTerrain terrain = null;    //  terrain
		List<IHeightStream> hsLayers = null;  // height stream list

		//	Temporary Mesh
		private struct TEMPMESH
		{
			public int width;				//	vertex number on square mesh border, so a mesh has count [width * width] vertices
			public float[] heights;			//	final height buffer
			public float[] layerHeights;	//	height buffer of current layer
			public VertexBuffer vb;			//	vertex buffer
			public float[] height5;			//	a float[5] temporary buffer
		};

		private TEMPMESH tempMesh;

		public GeometryBuilder(QuadtreeTerrain _trn)
		{
			terrain = _trn;
			hsLayers = new List<IHeightStream>();
			tempMesh = new TEMPMESH();
		}

		//	add a height stream
		public void AddHeightStream(IHeightStream hs)
		{
			if (hs != null)
			{
				hsLayers.Add(hs);
			}
		}

		//	Build mesh for a quadtree node
		//	left, top: index of left-top corner in height map
		//	step: grid number between 2 sampling point
		//	width: vertex number on square mesh border, so a mesh has [width * width] vertices
		//	gridSize: grid size
		//	outVerts: buffer used to store mesh, at least [width * width] vertices
		//	outMinY, outMaxY (out): min and max height in terrain's local space
		public bool BuildQTreeNodeMesh(int left, int top, int step, int width, int gridSize,
						VertexBuffer outVerts, out float outMinY, out float outMaxY)
		{
			//	In order to calculate the normals for border vertices, we need to get one more row/column
			//	outside the specified area
			int left1 = left - step;
			int top1 = top - step;
			int width1 = width + 2;

			outMinY = 0.0f;
			outMaxY = 0.0f;

			//	Create the little bigger temporary mesh. 
			CreateTempMesh(width1);

			if (!SampleTempMeshHeights(left1, top1, step, width1))
			{
				Debug.Log("GeometryBuilder.BuildQTreeNodeMesh, failed to call SampleTempMeshHeights!");
				return false;
			}

			//	Build temporary mesh data through heights
			//	Fill position at first
			int cur = 0;
			int stepsize = step * gridSize;
			for (int r = 0; r < width1; r++)
			{
				int z = -r * stepsize;
				int x = 0;

				for (int c = 0; c < width1; c++, x += stepsize, cur++)
				{
					tempMesh.vb.verts[cur].pos.Set(x, tempMesh.heights[cur], z);
				}
			}

			//	Calculate normals (only calculate core area)
			for (int r = 1; r < width1 - 1; r++)
			{
				int center = r * width1 + 1;

				for (int c = 1; c < width1 - 1; c++, center++)
				{
					//	Calculate normal only through neighbour heights
					tempMesh.height5[0] = tempMesh.vb.verts[center].pos.y;
					tempMesh.height5[1] = tempMesh.vb.verts[center - 1].pos.y;
					tempMesh.height5[2] = tempMesh.vb.verts[center - width1].pos.y;
					tempMesh.height5[3] = tempMesh.vb.verts[center + 1].pos.y;
					tempMesh.height5[4] = tempMesh.vb.verts[center + width1].pos.y;

					CalcCenterNormal(tempMesh.height5, stepsize, ref tempMesh.vb.verts[center].normal);
				}
			}

			//	Hollow out real node mesh from temporary one
			//	Calculate node's left-top corner offset in terrain's local space
			//	Consider the 1 step offset generated by temporary mesh
			int offx = left * gridSize - stepsize;
			int offz = -top * gridSize + stepsize;
			float invWidth = 1.0f / (width - 1);

			float miny = float.MaxValue;
			float maxy = float.MinValue;
			int dst = 0;

			for (int r = 1; r < width1 - 1; r++)
			{
				int src = r * width1 + 1;
				float v = (r - 1) * invWidth;

				for (int c = 1; c < width1 - 1; c++)
				{
					ref Vector3 srcPos = ref tempMesh.vb.verts[src].pos;
					ref Vector3 dstPos = ref outVerts.verts[dst].pos;
					dstPos.Set(srcPos.x + offx, srcPos.y, srcPos.z + offz);

					outVerts.verts[dst].normal = tempMesh.vb.verts[src].normal;
					outVerts.verts[dst].uv.Set((c - 1) * invWidth, v);

					if (dstPos.y < miny) miny = dstPos.y;
					if (dstPos.y > maxy) maxy = dstPos.y;

					src++;
					dst++;
				}
			}

			Debug.Assert(dst == width * width);

			outMinY = miny;
			outMaxY = maxy;

			return true;
		}

		bool SampleTempMeshHeights(int left, int top, int step, int width)
		{
			Debug.Assert(width == tempMesh.width);

			float heiScale = terrain.heightScale;
			int numVert = width * width;

			for (int i = 0; i < hsLayers.Count; i++)
			{
				IHeightStream hs = hsLayers[i];
				hs.SampleHeights(left, top, step, width, tempMesh.layerHeights);

				if (i == 0)
				{
					//	base layer
					for (int j = 0; j < numVert; j++)
					{
						float hei = tempMesh.layerHeights[j];
						tempMesh.heights[j] = (hei != Misc.invalidHeight) ? hei * heiScale : Misc.invalidHeight;
					}
				}
				else
				{
					//	Now, higher layer covers lower layer
					for (int j = 0; j < numVert; j++)
					{
						if (tempMesh.layerHeights[j] != Misc.invalidHeight)
						{
							tempMesh.heights[j] = tempMesh.layerHeights[j] * heiScale;
						}
					}
				}
			}

			return true;
		}

		bool CreateTempMesh(int width)
		{
			//	For all quadtree nodes should have the same number of vertex in their
			//	inborn LOD grade mesh, the temporary mesh may be created only once in fact.
			if (tempMesh.width == width)
				return true;

			int numVert = width * width;
			tempMesh.width = width;
			tempMesh.vb = new VertexBuffer(numVert);
			tempMesh.heights = new float[numVert];
			tempMesh.layerHeights = new float[numVert];
			tempMesh.height5 = new float[5];

			//	Fill UV in node's projecting area on xz plane
			float invWidth = 1.0f / (width - 1);
			int count = 0;
			for (int r = 0; r < width; r++)
			{
				float v = r * invWidth;

				for (int c = 0; c < width; c++)
				{
					tempMesh.vb.verts[count].uv.x = c * invWidth;
					tempMesh.vb.verts[count].uv.y = v;
					count++;
				}
			}

			return true;
		}

		//	Calculate center vertex's normal through heights
		//	aHei: store center/left/top/right/bottom neighbour vertex height
		void CalcCenterNormal(float[] heights, float stepSize, ref Vector3 normal)
		{
			float fHeight = heights[0];
			float left = (heights[1] == Misc.invalidHeight) ? fHeight : heights[1];
			float top = (heights[2] == Misc.invalidHeight) ? fHeight : heights[2];
			float right = (heights[3] == Misc.invalidHeight) ? fHeight : heights[3];
			float bottom = (heights[4] == Misc.invalidHeight) ? fHeight : heights[4];

			float dx = left - right;
			if (heights[1] == Misc.invalidHeight || heights[3] == Misc.invalidHeight)
				dx *= 2.0f;

			float dz = bottom - top;
			if (heights[2] == Misc.invalidHeight || heights[4] == Misc.invalidHeight)
				dz *= 2.0f;

			normal.Set(dx, stepSize* 2.0f, dz);
			normal.Normalize();
		}
	}
}