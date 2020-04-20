//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace ADQuadtreeTerrain
{
	//	child node position
	public enum eChildPos
	{
		POS_INVALID = -1, // Invalid
		POS_LT = 0,       // Left top
		POS_RT,           // Right top
		POS_LB,           // Left bottom
		POS_RB,           // Right bottom
	};

	//	Neighbour position
	public enum eNeighbour
	{
		LEFT = 0,
		TOP,
		RIGHT,
		BOTTOM,
	};

	//	The mesh state a node should have
	public enum eNodeMeshState
	{
		TO_UNLOAD = 0,    // This node doesn't has mesh or it can unload it's mesh
		LOAD_INBORN,      // This node should load its inborn LOD grade mesh for rendering
		LOAD_FOR_CHILD,   // This node should load its mesh for children's LOD grade-up rendering
		LOD_GRADEUP,      // This node should lend parent's mesh for rendering (LOD grade-up)
	};

	//	Quadtree node
	//	Note: In a grand terrain (32K x 32K for example) there may be more than 1 million nodes,
	//	so try to squeeze the structure's size as small as possible. 
	//	There is still optimization potential for current structure layout.
	public class QtreeNode
	{
		//	the fields marked by [+] means that they are easy to change.
		//	other fields without [+] are static, they were set once when quadtree is built.
		public int index { get; set; } = -1; // this node's index in buffer
		public int parent { get; set; } = -1; // parent's index
		public int[] children = new int[4] { -1, -1, -1, -1 };  // children indices
		public int[] neighbour = new int[4] { -1, -1, -1, -1 };  // neighbour indices
		public int hmLeft { get; set; } = 0;  // left border of node's area in heightmap
		public int hmTop { get; set; } = 0;  // top border of node's area in heightmap
		public int hmAreaSize { get; set; } = 0;  // size of square node area in heightmap
		public uint updateCnt { get; set; } = 0; //	[+] update counter
		public ushort gridStep { get; set; } = 0;  // How many grid between 2 adjacement vertex

		public sbyte childPos { get; set; } = (sbyte)eChildPos.POS_INVALID; // eChildPos enum, position in parent's node. -1 for root node
		public sbyte inbornLOD { get; set; } = -1; // Inborn LOD level. -1 means this node doesn't have mesh, and leaf should have 0 level LOD
		public byte meshState { get; set; } = (byte)eNodeMeshState.TO_UNLOAD; //	[+] enodeMeshState enum, Mesh state of this node
		public bool outRange { get; set; } = true;  // [+] true, node is out of terrain's view range
		public sbyte areaLOD { get; set; } = -1;  // [+] Actual LOD grade this node currently should use.

		//	Is this a leaf ?
		public bool IsLeaf() { return (inbornLOD == 0); }
		//	calculate node's area in terrain's space
		public Rect CalcLocalArea(int gridSize)
		{
			Rect rc = new Rect();
			rc.xMin = hmLeft * gridSize;	// left
			rc.yMax = -hmTop * gridSize;	// top
			rc.xMax = rc.xMin + hmAreaSize * gridSize;	// right
			rc.yMin = rc.yMax - hmAreaSize * gridSize; // bottom
			return rc;
		}
	};

	public class Quadtree
	{
		private QuadtreeTerrain terrain = null;
		private int hmSize = 0;  // heightmap size
		private int nodeNum = 0;  // number of nodes
		private QtreeNode[] nodeBuf = null; // node buffer
		private QtreeNode root = null; // root node
		private uint updateCnt; // Update counter

		private const int maxLODGrade = 32;
		private float[] lodDists = new float[maxLODGrade]; // distance of each LOD grade
		private int lodGradeNum = 1;   // current LOD grade number
		private Vector3 updateCenter = new Vector3();

		private Camera curCamera = null;
		private Plane[] camPlanes = null;
		private ITerrainRenderer curRenderer = null;

		//	get quadtree by index
		public QtreeNode GetQtreeNode(int nodeIndex)
		{
			Debug.Assert(nodeIndex >= 0 && nodeIndex < nodeNum);
			return nodeBuf[nodeIndex];
		}

		public Quadtree(QuadtreeTerrain _trn, int _hmSize)
		{
			Debug.Assert(Misc.Is2Power(_hmSize - 1));

			terrain = _trn;
			hmSize = _hmSize;

			//	create node buffer
			AllocateNodeBuffer();

			//	Initialize root node
			root = nodeBuf[0];
			root.index = 0;
			root.hmLeft = 0;
			root.hmTop = 0;
			root.hmAreaSize = (ushort)(_hmSize - 1);

			//	Generate tree recursively
			int nodeTempCnt = 1;
			BuildNode_r(0, (sbyte)eChildPos.POS_INVALID, ref nodeTempCnt);

			//	check node number
			Debug.Assert(nodeTempCnt == (int)nodeNum);

			//	Build neighbours info
			BuildNeighbours_r(0);
		}

		//	create quadtree buffer
		void AllocateNodeBuffer()
		{
			int gridNum = hmSize - 1;
			long nodeCnt = 1;

			if (gridNum > terrain.leafGridSize)
			{
				for (int i = 0; gridNum > terrain.leafGridSize; i++)
				{
					nodeCnt += 4 << (i * 2);
					gridNum >>= 1;
				}
			}

			//	just to ensure there isn't too many nodes
			Debug.Assert(nodeCnt < uint.MaxValue);

			nodeBuf = new QtreeNode[nodeCnt];
			nodeNum = (int)nodeCnt;

			for (int i=0; i < nodeNum; i++)
			{
				nodeBuf[i] = new QtreeNode();
			}
		}

		bool BuildNode_r(int nodeIndex, sbyte childPos, ref int nodeCnt)
		{
			QtreeNode node = nodeBuf[nodeIndex];
			int gridSize = terrain.gridSize;

			node.childPos = childPos;

			//	Check if this node meets leaf condition
			if (node.hmAreaSize <= terrain.leafGridSize)
			{
				//	This is a leaf.
				//	In fact, they should be equal !
				Debug.Assert(node.hmAreaSize == terrain.leafGridSize);

				node.inbornLOD = 0;
				node.gridStep = 1;
				return true;
			}

			int halfSize = node.hmAreaSize / 2;

			//	Split node and generate 4 children
			for (int i = 0; i < 4; i++)
			{
				QtreeNode newNode = nodeBuf[nodeCnt];
				newNode.index = nodeCnt++;

				newNode.parent = node.index;
				node.children[i] = newNode.index;

				if ((i & 0x01) != 0)   //	On right side ?
					newNode.hmLeft = node.hmLeft + halfSize;
				else
					newNode.hmLeft = node.hmLeft;

				if ((i & 0x02) != 0)   //	On bottom side ?
					newNode.hmTop = node.hmTop + halfSize;
				else
					newNode.hmTop = node.hmTop;

				newNode.hmAreaSize = halfSize;
			}

			//	Go on for children
			for (int i = 0; i < 4; i++)
			{
				if (!BuildNode_r(node.children[i], (sbyte)i, ref nodeCnt))
					return false;
			}

			//	Check if this node needs generating mesh ?
			if (node.hmAreaSize <= terrain.maxDrawnNodeGridSize)
			{
				//	Our LOD level should be our childs +1
				node.inbornLOD = (sbyte)(nodeBuf[node.children[0]].inbornLOD + 1);
				node.gridStep = (ushort)(1 << node.inbornLOD);

				Debug.Assert(terrain.leafGridSize == (node.hmAreaSize / node.gridStep));
			}

			return true;
		}

		void BuildNeighbours_r(int nodeIndex)
		{
			QtreeNode node = nodeBuf[nodeIndex];

			//	set our parent's neighbour's child as our neighbour ...
			Action<QtreeNode, int, int, int> SetParentNeighbourChildAsNeighbour =
				(QtreeNode theNode, int target, int neighbour, int child) =>
			{
				int parentNbr = nodeBuf[theNode.parent].neighbour[neighbour];
				if (parentNbr >= 0)
				{
					theNode.neighbour[target] = nodeBuf[parentNbr].children[child];
				}
				else
				{
					theNode.neighbour[target] = -1;
				}
			};

			if (node.parent < 0)
			{
				//	root node
				node.neighbour[(int)eNeighbour.LEFT] = -1;
				node.neighbour[(int)eNeighbour.TOP] = -1;
				node.neighbour[(int)eNeighbour.RIGHT] = -1;
				node.neighbour[(int)eNeighbour.BOTTOM] = -1;
			}
			else
			{
				QtreeNode parentNode = nodeBuf[node.parent];

				switch ((eChildPos)node.childPos)
				{
					case eChildPos.POS_LT:
						{
							SetParentNeighbourChildAsNeighbour(node, (int)eNeighbour.LEFT, (int)eNeighbour.LEFT, (int)eChildPos.POS_RT);
							SetParentNeighbourChildAsNeighbour(node, (int)eNeighbour.TOP, (int)eNeighbour.TOP, (int)eChildPos.POS_LB);
							node.neighbour[(int)eNeighbour.RIGHT] = parentNode.children[(int)eChildPos.POS_RT];
							node.neighbour[(int)eNeighbour.BOTTOM] = parentNode.children[(int)eChildPos.POS_LB];
							break;
						}
					case eChildPos.POS_RT:
						{
							node.neighbour[(int)eNeighbour.LEFT] = parentNode.children[(int)eChildPos.POS_LT];
							SetParentNeighbourChildAsNeighbour(node, (int)eNeighbour.TOP, (int)eNeighbour.TOP, (int)eChildPos.POS_RB);
							SetParentNeighbourChildAsNeighbour(node, (int)eNeighbour.RIGHT, (int)eNeighbour.RIGHT, (int)eChildPos.POS_LT);
							node.neighbour[(int)eNeighbour.BOTTOM] = parentNode.children[(int)eChildPos.POS_RB];
							break;
						}
					case eChildPos.POS_LB:
						{
							SetParentNeighbourChildAsNeighbour(node, (int)eNeighbour.LEFT, (int)eNeighbour.LEFT, (int)eChildPos.POS_RB);
							node.neighbour[(int)eNeighbour.TOP] = parentNode.children[(int)eChildPos.POS_LT];
							node.neighbour[(int)eNeighbour.RIGHT] = parentNode.children[(int)eChildPos.POS_RB];
							SetParentNeighbourChildAsNeighbour(node, (int)eNeighbour.BOTTOM, (int)eNeighbour.BOTTOM, (int)eChildPos.POS_LT);
							break;
						}
					case eChildPos.POS_RB:
						{
							node.neighbour[(int)eNeighbour.LEFT] = parentNode.children[(int)eChildPos.POS_LB];
							node.neighbour[(int)eNeighbour.TOP] = parentNode.children[(int)eChildPos.POS_RT];
							SetParentNeighbourChildAsNeighbour(node, (int)eNeighbour.RIGHT, (int)eNeighbour.RIGHT, (int)eChildPos.POS_LB);
							SetParentNeighbourChildAsNeighbour(node, (int)eNeighbour.BOTTOM, (int)eNeighbour.BOTTOM, (int)eChildPos.POS_RT);
							break;
						}
					default:
						{
							//	Invalid child position
							Debug.Assert(false);
							return;
						}
				}
			}

			if (!node.IsLeaf())
			{
				//	Go on for children
				for (int i = 0; i < 4; i++)
				{
					BuildNeighbours_r(node.children[i]);
				}
			}
		}

		//	update LOD for all nodes
		//	center: center position in terrain's local space
		public void UpdateLOD(Vector3 center)
		{
			updateCnt++;

			//	Distance for LOD 0
			float fBaseDist = terrain.lodBaseDist;
			const float fLODRatio = 3.0f;

			//	Calculate how many LOD grade exists and set distance for each grade
			int min = terrain.leafGridSize;
			int max = terrain.maxDrawnNodeGridSize;

			//	Add negative infinite as a border
			lodDists[0] = float.MinValue;

			lodGradeNum = 1;
			float fDist = fBaseDist;
			while (min < max)
			{
				Debug.Assert(lodGradeNum < maxLODGrade);
				lodDists[lodGradeNum++] = fDist;

				min <<= 1;
				fDist *= fLODRatio;
			}

			updateCenter = center;

			//	Update from root
			UpdateNodeLOD_r(0, -1);
		}
		
		void UpdateNodeLOD_r(int nodeIndex, sbyte areaLOD)
		{
			QtreeNode node = nodeBuf[nodeIndex];

			//	Sync node's update counter
			node.updateCnt = updateCnt;
			node.areaLOD = areaLOD;
			node.outRange = false;

			//	node's area in terrain's local space
			Rect rcLocal = node.CalcLocalArea(terrain.gridSize);

			//	Calculate the distance from m_vUpdateCenter to node's border
			float distx = Mathf.Abs((rcLocal.xMin + rcLocal.xMax) * 0.5f - updateCenter.x);
			float distz = Mathf.Abs((rcLocal.yMin + rcLocal.yMax) * 0.5f - updateCenter.z);
			float min_distx = distx - rcLocal.width * 0.5f;
			float min_distz = distz - rcLocal.height * 0.5f;
			float check_dist = min_distx > min_distz ? min_distx : min_distz;

			if (check_dist > terrain.viewDistance)
			{
				//	this node is out of view distance
				node.outRange = true;

				//	we stop the recursively process only when this node and it's parent
				//	are both out of view distance. this is a tip to avoid node flashing
				//	on the border of view distance.
				if (node.parent >= 0 && nodeBuf[node.parent].outRange)
				{
					node.meshState = (byte)eNodeMeshState.TO_UNLOAD;
					return;
				}
			}

			if (node.inbornLOD < 0)
			{
				//	the node hasn't mesh at all
				node.meshState = (byte)eNodeMeshState.TO_UNLOAD;
				Debug.Assert(!node.IsLeaf());

				//	Go on for children
				for (int i = 0; i < 4; i++)
				{
					UpdateNodeLOD_r(node.children[i], areaLOD);
				}
			}
			else if (areaLOD >= 0)
			{
				//	parent's mesh is to be loaded, so this node needn't mesh any more.
				node.meshState = (byte)eNodeMeshState.TO_UNLOAD;

				//	we can stop the recursive process here now (not go into children anymore),
				//	however we can't stop at parent node early! because this node's neighbours need 
				//	its areaLOD to decide whether their border mesh should do LOD grade-up or not.
				//	if (!node.IsLeaf())
				//	{
				// 		for (int i = 0; i < 4; i++)
				// 			UpdateNodeLOD_r(node.children[i], areaLOD);
				// 	}
			}
			else
			{
				//	Check which LOD grade this node locates in
				int lod_grade;
				for (lod_grade = lodGradeNum - 1; lod_grade >= 0; lod_grade--)
				{
					if (check_dist > lodDists[lod_grade])
						break;
				}

				Debug.Assert(lod_grade >= 0);

				if (lod_grade < node.inbornLOD)
				{
					//	This node totally or partly locates in lower LOD grade area, 
					//	we should render its children
					node.meshState = (byte)eNodeMeshState.TO_UNLOAD;

					//	this shouldn't be a leaf
					Debug.Assert(!node.IsLeaf());

					//	go on for children
					for (int i = 0; i < 4; i++)
					{
						UpdateNodeLOD_r(node.children[i], areaLOD);
					}

					//	If only part of this node in lower LOD grade area, some
					//	children may need our mesh to do LOD grade-up render.
					for (int i = 0; i < 4; i++)
					{
						if (nodeBuf[node.children[i]].meshState == (byte)eNodeMeshState.LOD_GRADEUP)
						{
							node.meshState = (byte)eNodeMeshState.LOAD_FOR_CHILD;
							break;
						}
					}
				}
				else if (lod_grade == node.inbornLOD)
				{
					//	use node's inborn LOD
					node.meshState = (byte)eNodeMeshState.LOAD_INBORN;
					node.areaLOD = node.inbornLOD;

					//	Go on to tell all children that they needn't loading mesh
					if (!node.IsLeaf())
					{
						for (int i = 0; i < 4; i++)
						{
							UpdateNodeLOD_r(node.children[i], node.areaLOD);
						}
					}
				}
				else if (lod_grade == nodeBuf[node.parent].inbornLOD)
				{
					//	LOD grade up, use parent's LOD
					node.meshState = (byte)eNodeMeshState.LOD_GRADEUP;
					node.areaLOD = nodeBuf[node.parent].inbornLOD;

					//	Go on to tell all children that they needn't loading mesh
					if (!node.IsLeaf())
					{
						for (int i = 0; i < 4; i++)
						{
							UpdateNodeLOD_r(node.children[i], node.areaLOD);
						}
					}
				}
				else
				{
					//	Shouldn't go here !
					Debug.Assert(false);
				}
			}

			//	Stream in/out node mesh according to it's rendering flag
			if (node.meshState == (byte)eNodeMeshState.LOAD_INBORN ||
				node.meshState == (byte)eNodeMeshState.LOAD_FOR_CHILD)
			{
				terrain.meshMan.RequestNodeMesh(nodeIndex);
			}
		}

		//	Collect nodes for rendering
		public void CollectRenderNodes(Camera cam, ITerrainRenderer renderer)
		{
			curCamera = cam;
			camPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
			curRenderer = renderer;

			//	collect from root
			CollectRenderNodes_r(0);
		}

		void CollectRenderNodes_r(int nodeIndex)
		{
			QtreeNode node = nodeBuf[nodeIndex];

			//	Only render node whose info is up-to-date
			if (node.updateCnt != updateCnt)
			{
				//	If a node isn't up-to-date, all it's children must not be.
				return;
			}

			//	do camera cull with node's AABB at first, but node may not have min-y and max-y
			//	at this moment if it's mesh wasn't built, so now we only do a conservative check
			//	with x and z axis.
			//	NOTE: we ONLY consider tranlation of terrain, but not rotation and scale
			Vector3 offset = terrain.transform.position;
			Rect rcLocal = node.CalcLocalArea(terrain.gridSize);
			Vector3 mins = new Vector3(rcLocal.xMin + offset.x, -10000.0f, rcLocal.yMin + offset.z);
			Vector3 maxs = new Vector3(rcLocal.xMax + offset.x, 10000.0f, rcLocal.yMax + offset.z);
			Bounds aabb = new Bounds();
			aabb.SetMinMax(mins, maxs);

			if (!GeometryUtility.TestPlanesAABB(camPlanes, aabb))
				return;    //	The whole node isn't visible

			if (node.meshState == (byte)eNodeMeshState.LOAD_INBORN ||
				node.meshState == (byte)eNodeMeshState.LOD_GRADEUP)
			{
				//	Get index buffer according to neighbour's LOD grade
				int sideMask = 0;
				for (int i = 0; i < 4; i++)
				{
					if (node.neighbour[i] < 0)
						continue;

					QtreeNode neighbour = nodeBuf[node.neighbour[i]];
					if (neighbour.updateCnt == updateCnt &&	neighbour.areaLOD > node.areaLOD)
					{
						sideMask |= (1 << i);
					}
				}

				//	This node's may be drawn
				ComputeBuffer _vertCB = null;
				ComputeBuffer _indexCB = null;
				QuadtreeMesh nodeMesh = null;

				if (node.meshState == (byte)eNodeMeshState.LOAD_INBORN)
				{
					nodeMesh = terrain.meshMan.GetNodeMesh(nodeIndex);
					if (nodeMesh != null)
					{
						_vertCB = nodeMesh.vertCB;
						_indexCB = terrain.trnRes.GetNodeIndexCB(sideMask, -1);
					}
				}
				else if (node.meshState == (byte)eNodeMeshState.LOD_GRADEUP)
				{
					//	LOD grade-up rendering, use parent's mesh
					Debug.Assert(node.parent >= 0);
					nodeMesh = terrain.meshMan.GetNodeMesh(node.parent);
					if (nodeMesh != null)
					{
						_vertCB = nodeMesh.vertCB;
						_indexCB = terrain.trnRes.GetNodeIndexCB(sideMask, node.childPos);
					}
				}

				if (_vertCB != null && _indexCB != null)
				{
					//	do camera cull again with more precise aabb
					mins.y = nodeMesh.aabb.min.y + offset.y;
					maxs.y = nodeMesh.aabb.max.y + offset.y;
					aabb.SetMinMax(mins, maxs);
					if (!GeometryUtility.TestPlanesAABB(camPlanes, aabb))
						return;    // The whole node isn't visible

					//	Push node to rendering collector
					DRAWDATA drawData = new DRAWDATA()
					{
						node = node,
						nodeAABB = aabb,
						vertCB = _vertCB,
						indexCB = _indexCB,
					};

					curRenderer.PushDrawData(drawData);
				}
			}

			if (!node.IsLeaf())
			{
				//	Go on for children
				for (int i = 0; i < 4; i++)
				{
					CollectRenderNodes_r(node.children[i]);
				}
			}
		}
	}
}

