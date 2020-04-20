//	Copyright(c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ADQuadtreeTerrain
{
	public class MainCamera : MonoBehaviour
	{
		[Range(1.0f, 200f)] public float moveSpeed = 25.0f;     //	camera move speed
		[Range(0.01f, 1.0f)] public float rotSpeed = 0.25f;     //	camera rotate speed

		private bool cameraRotate = false;
		private Vector3 lastMouse = new Vector3(255, 255, 255);

		// Start is called before the first frame update
		void Start()
		{
		}

		// Update is called once per frame
		void Update()
		{
			if (Input.GetMouseButton(1))
			{
				if (cameraRotate)
				{
					Vector3 offset = Input.mousePosition - lastMouse;
					offset = new Vector3(-offset.y * rotSpeed, offset.x * rotSpeed, 0);
					lastMouse = new Vector3(transform.eulerAngles.x + offset.x, transform.eulerAngles.y + offset.y, 0);
					transform.eulerAngles = lastMouse;
				}

				lastMouse = Input.mousePosition;
				cameraRotate = true;
			}
			else
			{
				cameraRotate = false;
			}

			int mx = 0, my = 0, mz = 0;
			if (Input.GetKey(KeyCode.W))
			{
				mz = 1;
			}
			else if (Input.GetKey(KeyCode.S))
			{
				mz = -1;
			}

			if (Input.GetKey(KeyCode.A))
			{
				mx = -1;
			}
			else if (Input.GetKey(KeyCode.D))
			{
				mx = 1;
			}

			if (Input.GetKey(KeyCode.E))
			{
				my = 1;
			}
			else if (Input.GetKey(KeyCode.Q))
			{
				my = -1;
			}

			Vector3 dir = new Vector3(0f, 0f, 0f);
			if (mx != 0 || mz != 0)
			{
				dir = new Vector3(mx, 0.0f, mz);
				dir.Normalize();
				dir = transform.TransformDirection(dir);
			}

			Vector3 delta = dir * moveSpeed * Time.deltaTime;
			delta.y += my * moveSpeed * Time.deltaTime;
			transform.Translate(delta, Space.World);
		}
	}
}
