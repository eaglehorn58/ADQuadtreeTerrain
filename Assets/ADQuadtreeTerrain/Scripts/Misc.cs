//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ADQuadtreeTerrain
{
	class Misc
	{
		//	invalid height value
		public static readonly float invalidHeight = float.MinValue;

		//	check if specified number is 2^n
		static public bool Is2Power(int num)
		{
			return ((num & (num - 1)) == 0);
		}
	}

	//	terrain vertex
	public struct TRNVERTEX
	{
		public Vector3 pos;
		public Vector3 normal;
		public Vector2 uv;
	};

	//	Vertex data buffer
	public class VertexBuffer
	{
		public static readonly int strideSize = 32;    // stride size in bytes

		public int vertNum { get; private set; }
		public TRNVERTEX[] verts { get; private set; } = null;

		public VertexBuffer(int _vertNum)
		{
			vertNum = _vertNum;
			verts = new TRNVERTEX[_vertNum];
		}
	}
}


