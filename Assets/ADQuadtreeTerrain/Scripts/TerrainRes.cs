//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ADQuadtreeTerrain
{
	public class TerrainRes
	{
		//	Side flags
		private enum eSideFlag
		{
			LEFT = 0x01,
			TOP = 0x02,
			RIGHT = 0x04,
			BOTTOM = 0x08,
		};

		//	Index buffer enum
		private enum eIndexBuf
		{
			IB_NO_LOD = 0,      //	No lod
			IB_LOD_L,           //	Lod grade up on left border
			IB_LOD_T,           //	Lod grade up on top border
			IB_LOD_R,           //	Lod grade up on right border
			IB_LOD_B,           //	Lod grade up on bottom border
			IB_LOD_LT,          //	Lod grade up on left-top border
			IB_LOD_RT,          //	Lod grade up on right-top border
			IB_LOD_LB,          //	Lod grade up on right-bottom border
			IB_LOD_RB,          //	Lod grade up on left-bottom border
			COUNT,
		};

		private QuadtreeTerrain terrain = null;    //  terrain
		private List<int>[] idxBufs = new List<int>[(int)eIndexBuf.COUNT];
		private List<int>[][] gradeUpIdxBufs = new List<int>[4][];

		private List<ComputeBuffer> idxCBs = new List<ComputeBuffer>((int)eIndexBuf.COUNT);
		private List<ComputeBuffer>[] gradeUpIdxCBs = new List<ComputeBuffer>[4];

		//	Lookup table for converting side mask to index buffer
		private int[] sideMask2IdxBuf = new int[16]
		{
			(int)eIndexBuf.IB_NO_LOD, (int)eIndexBuf.IB_LOD_L, (int)eIndexBuf.IB_LOD_T, (int)eIndexBuf.IB_LOD_LT,	//	0 ~ 3
			(int)eIndexBuf.IB_LOD_R, -1, (int)eIndexBuf.IB_LOD_RT, -1,	 //	4 ~ 7
			(int)eIndexBuf.IB_LOD_B, (int)eIndexBuf.IB_LOD_LB, -1, -1,	 //	8 ~ 11
			(int)eIndexBuf.IB_LOD_RB, -1, -1, -1,					     //	12 ~ 15
		};

		public TerrainRes(QuadtreeTerrain _trn)
		{
			terrain = _trn;

			//	Create index buffers for nodes rendering with inborn LOD grade
			int gridWidth = terrain.leafGridSize;
			int vertPitch = gridWidth + 1;

			//	create comuter buffer
			Func<List<int>, ComputeBuffer> CreateCB = (List<int> ib) =>
			{
				ComputeBuffer cb = new ComputeBuffer(ib.Count, 4);
				cb.SetData(ib.ToArray());
				return cb;
			};

			idxBufs[(int)eIndexBuf.IB_NO_LOD] = CreateIndexBuffer(gridWidth, vertPitch, 0, 0);
			idxBufs[(int)eIndexBuf.IB_LOD_L] = CreateIndexBuffer(gridWidth, vertPitch, 0, (int)eSideFlag.LEFT);
			idxBufs[(int)eIndexBuf.IB_LOD_T] = CreateIndexBuffer(gridWidth, vertPitch, 0, (int)eSideFlag.TOP);
			idxBufs[(int)eIndexBuf.IB_LOD_R] = CreateIndexBuffer(gridWidth, vertPitch, 0, (int)eSideFlag.RIGHT);
			idxBufs[(int)eIndexBuf.IB_LOD_B] = CreateIndexBuffer(gridWidth, vertPitch, 0, (int)eSideFlag.BOTTOM);
			idxBufs[(int)eIndexBuf.IB_LOD_LT] = CreateIndexBuffer(gridWidth, vertPitch, 0, (int)eSideFlag.LEFT | (int)eSideFlag.TOP);
			idxBufs[(int)eIndexBuf.IB_LOD_RT] = CreateIndexBuffer(gridWidth, vertPitch, 0, (int)eSideFlag.RIGHT | (int)eSideFlag.TOP);
			idxBufs[(int)eIndexBuf.IB_LOD_LB] = CreateIndexBuffer(gridWidth, vertPitch, 0, (int)eSideFlag.LEFT | (int)eSideFlag.BOTTOM);
			idxBufs[(int)eIndexBuf.IB_LOD_RB] = CreateIndexBuffer(gridWidth, vertPitch, 0, (int)eSideFlag.RIGHT | (int)eSideFlag.BOTTOM);

			for (int i = 0; i < (int)eIndexBuf.COUNT; i++)
			{
				ComputeBuffer cb = CreateCB(idxBufs[i]);
				idxCBs.Add(cb);
			}

			//	Create index buffer for nodes LOD grade-up rendering (use parent's LOD)
			int halfGridWidth = gridWidth >> 1;
			Debug.Assert(halfGridWidth > 0);
			//	Base index of child's left-top corner in it's parent's node mesh
			int[] baseIdx = new int[4]
			{
				0,
				halfGridWidth,
				halfGridWidth * vertPitch,
				halfGridWidth * vertPitch + halfGridWidth
			};

			for (int i = 0; i < 4; i++)
			{
				int ltbase = baseIdx[i];

				gradeUpIdxBufs[i] = new List<int>[(int)eIndexBuf.COUNT];

				gradeUpIdxBufs[i][(int)eIndexBuf.IB_NO_LOD] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, 0);
				gradeUpIdxBufs[i][(int)eIndexBuf.IB_LOD_L] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, (int)eSideFlag.LEFT);
				gradeUpIdxBufs[i][(int)eIndexBuf.IB_LOD_T] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, (int)eSideFlag.TOP);
				gradeUpIdxBufs[i][(int)eIndexBuf.IB_LOD_R] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, (int)eSideFlag.RIGHT);
				gradeUpIdxBufs[i][(int)eIndexBuf.IB_LOD_B] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, (int)eSideFlag.BOTTOM);
				gradeUpIdxBufs[i][(int)eIndexBuf.IB_LOD_LT] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, (int)eSideFlag.LEFT | (int)eSideFlag.TOP);
				gradeUpIdxBufs[i][(int)eIndexBuf.IB_LOD_RT] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, (int)eSideFlag.RIGHT | (int)eSideFlag.TOP);
				gradeUpIdxBufs[i][(int)eIndexBuf.IB_LOD_LB] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, (int)eSideFlag.LEFT | (int)eSideFlag.BOTTOM);
				gradeUpIdxBufs[i][(int)eIndexBuf.IB_LOD_RB] = CreateIndexBuffer(halfGridWidth, vertPitch, ltbase, (int)eSideFlag.RIGHT | (int)eSideFlag.BOTTOM);

				gradeUpIdxCBs[i] = new List<ComputeBuffer>((int)eIndexBuf.COUNT);
				for (int j = 0; j < (int)eIndexBuf.COUNT; j++)
				{
					ComputeBuffer cb = CreateCB(gradeUpIdxBufs[i][j]);
					gradeUpIdxCBs[i].Add(cb);
				}
			}
		}

		public void Destroy()
		{
			for (int j = 0; j < (int)eIndexBuf.COUNT; j++)
			{
				idxCBs[j].Release();
				idxCBs[j] = null;
			}

			for (int i = 0; i < 4; i++)
			{
				for (int j = 0; j < (int)eIndexBuf.COUNT; j++)
				{
					gradeUpIdxCBs[i][j].Release();
					gradeUpIdxCBs[i][j] = null;
				}
			}
		}

		List<int> CreateIndexBuffer(int gridWidth, int vertPitch, int ltbase, int sideMask)
		{
			//	iGridWidth must be a 2^n number which is between [2, 256),
			//	< 256 is in order to use 16-bit index
			Debug.Assert(gridWidth > 2 && Misc.Is2Power(gridWidth));

			//	example: terrain mesh when gridWidth == 4
			//	---------
			//	|\|/|\|/|
			//	---------
			//	|/|\|/|\|
			//	---------
			//	|\|/|\|/|
			//	---------
			//	|/|\|/|\|
			//	---------

			//	Allocate length enough buffer
			int maxIdxNum = gridWidth * gridWidth * 6;
			List<int> indices = new List<int>(maxIdxNum);

			//	--------------------------------
			//	Fill center indices at first
			//	--------------------------------
			int iCenterGridNum = gridWidth - 2;
			int startIdx = ltbase + vertPitch + 1;
			for (int r = 0; r < iCenterGridNum; r++)
			{
				//	left-top's index
				int lt = startIdx;

				for (int c = 0; c < iCenterGridNum; c++)
				{
					//	case1: (even row & even col) || (odd row & odd col)
					//	case2: (even row & odd col) || (odd row & even col)
					if ((r & 1) == (c & 1))
					{
						//	case 1
						//	---
						//	|\|
						//	---
						indices.Add(lt);
						indices.Add(lt + 1);
						indices.Add(lt + vertPitch + 1);
						indices.Add(lt);
						indices.Add(lt + vertPitch + 1);
						indices.Add(lt + vertPitch);
					}
					else
					{
						//	case 2
						//	---
						//	|/|
						//	---
						indices.Add(lt);
						indices.Add(lt + 1);
						indices.Add(lt + vertPitch);
						indices.Add(lt + 1);
						indices.Add(lt + vertPitch + 1);
						indices.Add(lt + vertPitch);
					}

					lt++;
				}

				startIdx += vertPitch;
			}

			//  Fill top border
			startIdx = ltbase;
			bool bLODGradeUp = (sideMask & (int)eSideFlag.TOP) != 0;
			FillBorderIndices(gridWidth, startIdx, 1, vertPitch, indices, bLODGradeUp);

			//  Fill left border
			startIdx = ltbase + gridWidth * vertPitch;
			bLODGradeUp = (sideMask & (int)eSideFlag.LEFT) != 0;
			FillBorderIndices(gridWidth, startIdx, -vertPitch, 1, indices, bLODGradeUp);

			//  Fill right border
			startIdx = ltbase + gridWidth;
			bLODGradeUp = (sideMask & (int)eSideFlag.RIGHT) != 0;
			FillBorderIndices(gridWidth, startIdx, vertPitch, -1, indices, bLODGradeUp);

			//  Fill bottom border
			startIdx = ltbase + gridWidth * vertPitch + gridWidth;
			bLODGradeUp = (sideMask & (int)eSideFlag.BOTTOM) != 0;
			FillBorderIndices(gridWidth, startIdx, -1, -vertPitch, indices, bLODGradeUp);

			Debug.Assert(indices.Count <= maxIdxNum);

			return indices;
		}

		void FillBorderIndices(int gridWidth, int startIdx, int x_step, int z_step,
						List<int> indices, bool bLODGradeUp)
		{
			int halfGrid = gridWidth >> 1;

			//  Index of base point
			int baseIdx = startIdx;

			if (bLODGradeUp)
			{
				//  LOD grade up border:
				//  example：4x4 grids mesh
				//  -------------
				//  |\   /|\   /|
				//  | \ / | \ / |
				//  |  -------  |
				for (int c = 0; c < halfGrid; c++)
				{
					indices.Add(baseIdx);
					indices.Add(baseIdx + x_step + x_step);
					indices.Add(baseIdx + z_step + x_step);

					baseIdx += x_step + x_step;
				}
			}
			else
			{
				//  Normal border
				//  example：4x4 grids mesh 
				//  -------------
				//  |\ | /|\ | /|
				//  | \|/ | \|/ |
				//  |  -------  |
				for (int c = 0; c < halfGrid; c++)
				{
					indices.Add(baseIdx);
					indices.Add(baseIdx + x_step);
					indices.Add(baseIdx + z_step + x_step);

					indices.Add(baseIdx + x_step);
					indices.Add(baseIdx + x_step + x_step);
					indices.Add(baseIdx + z_step + x_step);

					baseIdx += x_step + x_step;
				}
			}

			baseIdx = startIdx + x_step + x_step;
			for (int c = 0; c < halfGrid - 1; c++)
			{
				indices.Add(baseIdx);
				indices.Add(baseIdx + z_step);
				indices.Add(baseIdx + z_step - x_step);

				indices.Add(baseIdx);
				indices.Add(baseIdx + z_step + x_step);
				indices.Add(baseIdx + z_step);

				baseIdx += x_step + x_step;
			}
		}

		public List<int> GetNodeIndexBuf(int sideMask, int childPos)
		{
			sideMask &= 0x0F;

			int index = sideMask2IdxBuf[sideMask];
			Debug.Assert(index >= 0);

			if (childPos >= 0)
			{
				Debug.Assert(childPos < 4);
				return gradeUpIdxBufs[childPos][index];
			}
			else
			{
				return idxBufs[index];
			}
		}

		public ComputeBuffer GetNodeIndexCB(int sideMask, int childPos)
		{
			sideMask &= 0x0F;

			int index = sideMask2IdxBuf[sideMask];
			Debug.Assert(index >= 0);

			if (childPos >= 0)
			{
				Debug.Assert(childPos < 4);
				return gradeUpIdxCBs[childPos][index];
			}
			else
			{
				return idxCBs[index];
			}
		}
	}
}


