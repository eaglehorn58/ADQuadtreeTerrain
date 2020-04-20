//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ADQuadtreeTerrain
{
	public class QuadtreeTerrain : MonoBehaviour
	{
		public Camera cam = null;       //	bind camera to this
		public Light mainLight = null;
		[Range(100f, 5000f)] public float viewDistance = 2000f;  // current view distance
		[Range(35f, 300f)] public float lodBaseDist = 35.0f;  // base distance for LOD 0
		public bool showLOD = false;    // render LOD info
		public Texture2D mainTexture = null;
		public Texture2D lodTexture = null;

		//	LOD level of vista node
		public int vistaLOD { get; private set; } = 0;
		//	whole terrain should have [wholeGridSize x wholeGridSize] grids
		public int wholeGridSize { get; private set; } = 0;
		//	each quadtree leaf has leafGridSize X leafGridSize grids
		public int leafGridSize { get; private set; } = 32;
		// the size in grids of the biggist drawn quadtree node
		public int maxDrawnNodeGridSize { get; private set; } = 512;
		//	size of each grid in real world, int is more sweet for calculation
		public int gridSize { get; } = 1;
		//	Hightmap size, must be 2^n+1 and must match to heightmap file
		public int hmSize { get; private set; } = 0;
		//	quadtree object
		public Quadtree qtree { get; private set; } = null;
		//	Get vertex number in each row/column of quadtree node's mesh
		public int vertexNumInNodeRow { get { return leafGridSize + 1; } }
		//	terrain height scale, in a pratical project, this should be read from terrain configs
		public float heightScale { get; } = 2625.0f;
		//	geometry builder
		public GeometryBuilder gemBuilder { get; private set; } = null;
		//	mesh manager
		public QuadtreeMeshMan meshMan { get; private set; } = null;
		//	terrain static resources
		public TerrainRes trnRes { get; private set; } = null;
		//	get default shader
		public Shader GetDefaultShader() { return shaderDefault; }

		[SerializeField, HideInInspector] private Shader shaderDefault = null;  // shader for default rendering
		//public Shader shaderDefault = null;  // shader for default rendering
		private HeightStreamRawFile hmStream = null;    // heightmap stream file
		private ITerrainRenderer curRenderer = null;
		private TerrainRendererDefault defRenderer = null;

		private void Awake()
		{
			Camera.onPreCull += Render;

			//	leafGridSize & maxDrawnNodeGridSize must be 2^n number
			Debug.Assert(leafGridSize > 2 && Misc.Is2Power(leafGridSize));
			Debug.Assert(maxDrawnNodeGridSize > leafGridSize && Misc.Is2Power(maxDrawnNodeGridSize));

			//	Determine vista node's LOD
			vistaLOD = 0;
			for (int i = leafGridSize; i <= maxDrawnNodeGridSize; )
			{
				if (i > 128)
					break;

				vistaLOD++;
				i <<= 1;
			}

			//	open heightmap stream
			string fileName = Application.dataPath + "/ADQuadtreeTerrain/heightmap.raw16";

			try
			{
				trnRes = new TerrainRes(this);

				hmStream = new HeightStreamRawFile(fileName, eHMFile.RAW_16, leafGridSize + 1);
				hmSize = hmStream.hmWidth;

				gemBuilder = new GeometryBuilder(this);
				gemBuilder.AddHeightStream(hmStream);

				meshMan = new QuadtreeMeshMan(this);

				qtree = new Quadtree(this, hmSize);

				defRenderer = new TerrainRendererDefault(this);
				curRenderer = defRenderer;
			}
			catch
			{
				Debug.Log("ADQuadtreeTerrain.Start, error happen!");
			}
		}

		private void OnDestroy()
		{
			Camera.onPreRender -= Render;

			if (defRenderer != null)
			{
				defRenderer.Destroy();
			}

			curRenderer = null;

			if (meshMan != null)
			{
				meshMan.Destroy();
			}

			if (hmStream != null)
			{
				hmStream.Close();
			}

			if (trnRes != null)
			{
				trnRes.Destroy();
			}
		}

		void Start()
		{
		}

		// Update is called once per frame
		void Update()
		{
			if (cam == null || qtree == null)
				return;

			meshMan.Update();

			//	Transform center position from world space to terrain's local space
			Vector3 localCenter = cam.transform.position - transform.position;

			//	Calculate LOD for whole quadtree
			qtree.UpdateLOD(localCenter);
		}

		public void Render(Camera cam)
		{
			if (curRenderer != null)
			{
				curRenderer.Render(cam);
			}
		}
	}
}