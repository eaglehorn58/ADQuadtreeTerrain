//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ADQuadtreeTerrain
{
	public class TerrainRendererDefault : ITerrainRenderer
	{
		private QuadtreeTerrain terrain = null;
		private List<DRAWDATA> drawDataList = null;
		private Material mtlDefault = null;
		private MaterialPropertyBlock defMtlProp = null;
		private List<MaterialPropertyBlock> lodMtlProps = null;
		private const int maxLODCount = 16;

		public TerrainRendererDefault(QuadtreeTerrain _trn)
		{
			terrain = _trn;
			drawDataList = new List<DRAWDATA>(256);

			//	load materials
			mtlDefault = new Material(terrain.GetDefaultShader());
			mtlDefault.hideFlags = HideFlags.DontSave;

			defMtlProp = new MaterialPropertyBlock();
			defMtlProp.SetColor("lodColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));

			//	Create lod material property block
			Action<Color> CreateLODMtlProp = (Color lodColor) =>
			{
				MaterialPropertyBlock prop = new MaterialPropertyBlock();
				prop.SetColor("lodColor", lodColor);
				lodMtlProps.Add(prop);
			};

			//	LOD grade color table
			lodMtlProps = new List<MaterialPropertyBlock>(maxLODCount);
			CreateLODMtlProp(new Color(1.0f, 0.5f, 0.5f, 1.0f));
			CreateLODMtlProp(new Color(0.5f, 1.0f, 0.5f, 1.0f));
			CreateLODMtlProp(new Color(0.5f, 0.5f, 1.0f, 1.0f));
			CreateLODMtlProp(new Color(1.0f, 1.0f, 0.0f, 1.0f));
			CreateLODMtlProp(new Color(0.0f, 1.0f, 1.0f, 1.0f));
			CreateLODMtlProp(new Color(0.75f, 0.0f, 0.0f, 1.0f));
			CreateLODMtlProp(new Color(0.0f, 0.75f, 0.0f, 1.0f));
			CreateLODMtlProp(new Color(0.0f, 0.0f, 0.75f, 1.0f));
			CreateLODMtlProp(new Color(1.0f, 0.75f, 0.0f, 1.0f));
			CreateLODMtlProp(new Color(0.0f, 1.0f, 0.75f, 1.0f));
			CreateLODMtlProp(new Color(0.5f, 0.0f, 0.0f, 1.0f));
			CreateLODMtlProp(new Color(0.0f, 0.5f, 0.0f, 1.0f));
			CreateLODMtlProp(new Color(0.0f, 0.0f, 0.5f, 1.0f));
			CreateLODMtlProp(new Color(1.0f, 0.5f, 0.0f, 1.0f));
			CreateLODMtlProp(new Color(0.0f, 1.0f, 0.5f, 1.0f));
			CreateLODMtlProp(new Color(0.5f, 0.5f, 0.5f, 1.0f));
		}

		public void Destroy()
		{
			drawDataList.Clear();

			if (mtlDefault != null)
			{
				if (Application.isPlaying)
					UnityEngine.Object.Destroy(mtlDefault);
				else
					UnityEngine.Object.DestroyImmediate(mtlDefault);

				mtlDefault = null;
			}
		}

		//	Push draw data
		public void PushDrawData(DRAWDATA data)
		{
			drawDataList.Add(data);
		}

		//	Render routine
		public void Render(Camera cam)
		{
			//	reset draw list
			drawDataList.Clear();

			//	Collect quadtree nodes that will be renderred
			terrain.qtree.CollectRenderNodes(cam, this);

			if (drawDataList.Count == 0)
				return;

			//	pass transform matrix to shader
			//	NOTE: we ONLY consider tranlation of terrain, but not rotation and scale
			Matrix4x4 matTM = new Matrix4x4();
			matTM.SetTRS(terrain.transform.position, Quaternion.identity, Vector3.one);
			mtlDefault.SetMatrix("localToWorld", matTM);
			mtlDefault.SetMatrix("worldToLocal", matTM.inverse);

			int pidVertBuffer = Shader.PropertyToID("vertBuffer");
			int pidIndexBuffer = Shader.PropertyToID("indexBuffer");

			if (terrain.showLOD == true)
			{
				mtlDefault.SetTexture("_mainTex", terrain.lodTexture);

				foreach (DRAWDATA dd in drawDataList)
				{
					int idxMtl = (dd.node.areaLOD < maxLODCount) ? dd.node.areaLOD : (maxLODCount - 1);
					MaterialPropertyBlock pb = lodMtlProps[idxMtl];

					pb.SetBuffer(pidVertBuffer, dd.vertCB);
					pb.SetBuffer(pidIndexBuffer, dd.indexCB);

					Graphics.DrawProcedural(
						mtlDefault,
						dd.nodeAABB,
						MeshTopology.Triangles, dd.indexCB.count, 1,
						null, lodMtlProps[idxMtl],
						ShadowCastingMode.On, true, terrain.gameObject.layer);
				}
			}
			else
			{
				mtlDefault.SetTexture("_mainTex", terrain.mainTexture);

				foreach (DRAWDATA dd in drawDataList)
				{
					defMtlProp.SetBuffer(pidVertBuffer, dd.vertCB);
					defMtlProp.SetBuffer(pidIndexBuffer, dd.indexCB);

					Graphics.DrawProcedural(
						mtlDefault,
						dd.nodeAABB,
						MeshTopology.Triangles, dd.indexCB.count, 1,
						null, defMtlProp,
						ShadowCastingMode.On, true, terrain.gameObject.layer);
				}
			}
		}
	}
}