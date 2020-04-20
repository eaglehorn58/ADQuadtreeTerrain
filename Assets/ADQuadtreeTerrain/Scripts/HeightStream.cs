//	Copyright <c) 2020, Andy Do
//	eaglehorn58@gmail.com, eaglehorn58@163.com

using System;
using System.Text;
using System.IO;
using UnityEngine;

namespace ADQuadtreeTerrain
{
	//	height map file type
	public enum eHMFile
	{
		UNKNOWN = 0,       //	Unknown
		RAW_16,            //	16-bit RAW
		RAW_F32,           //	32-bit float RAW
	}

	public interface IHeightStream
	{
		//	Get square height map width (is also height)
		int GetWidth();
		//	Get data update counter
		uint GetUpdateCnt();
		//	Sample height values of square area
		//	left, top: index of left-top corner in height map
		//	step: grid between 2 sampling point
		//	width: [width * width] height value will be got.
		//	heiBuf: used to store height values, can hold at least [sample x sample] float values.
		bool SampleHeights(int left, int top, int step, int width, float[] heiBuf);
		//	Close stream
		void Close();
	}

	//	handle raw height map file
	public class HeightStreamRawFile : IHeightStream
	{
		public eHMFile hmType { get; } = eHMFile.UNKNOWN; // heightmap file type
		public int hmWidth { get; } = 0; // height map width (height)

		private FileStream file = null;   // heightmap file
		private uint updateCnt = 0;   // Update counter
		private byte[] tempBuf = null;  // temperoary buffer used to reading height
		private int tempBufLen = 0;  // temperoary buffer length

		public HeightStreamRawFile(string fileName, eHMFile type, int minWidth)
		{
			Debug.Assert(type != eHMFile.UNKNOWN);
			hmType = type;

			//	Guess height map size through file length
			FileInfo fi = new FileInfo(fileName);
			if (!fi.Exists)
			{
				throw new FileNotFoundException("HeightStreamRawFile, don't find heightmap file!");
			}

			//	bytes per pixel
			int bpp = (type == eHMFile.RAW_F32) ? 4 : 2;

			int width = (int)(Mathf.Sqrt((float)fi.Length / bpp) + 0.5f);
			if ((long)width * width * bpp != fi.Length)
			{
				throw new FileLoadException("HeightStreamRawFile, Couldn't guess heightmap size!");
			}

			//	Check if height map size is 2^n+1
			if (!Misc.Is2Power(width - 1) || width < minWidth)
			{
				throw new FileLoadException("HeightStreamRawFile, wrong heightmap size!");
			}

			try
			{
				//	try to open heightmap file
				file = File.OpenRead(fileName);
			}
			catch
			{
				Debug.Log("HeightStreamRawFile, fafiled to open heightmap file");
				return;
			}

			hmWidth = width;
		}

		~HeightStreamRawFile()
		{
			Close();
		}

		//	Close stream
		public void Close()
		{
			if (file != null)
			{
				file.Dispose();
				file = null;
			}
		}

		public int GetWidth()
		{
			return hmWidth;
		}

		public uint GetUpdateCnt()
		{
			return updateCnt;
		}

		public bool SampleHeights(int left, int top, int step, int width, float[] heiBuf)
		{
			if (file == null)
				return false;

			int bpp = sizeof(float);
			float invMaxVal = 1.0f;

			if (hmType == eHMFile.RAW_16)
			{
				bpp = sizeof(ushort);
				invMaxVal = 1.0f / 65535.0f;
			}
			else if (hmType != eHMFile.RAW_F32)
			{
				Debug.Assert(false);
				return false;
			}

			long pitch = hmWidth * bpp;
			int curHei = 0;

			//	temporary buffer for quick reading
			int newTempBufLen = 0;
			if (step == 1 && left >= 0 && left + width <= hmWidth)
			{
				//	Can do quick reading
				newTempBufLen = width * bpp;
			}

			if (newTempBufLen > tempBufLen)
			{
				tempBuf = new byte[newTempBufLen];
				tempBufLen = newTempBufLen;
			}

			using (BinaryReader br = new BinaryReader(file, Encoding.UTF8, true))
			{
				for (int r = 0; r < width; r++)
				{
					int z = top + r * step;

					if (z < 0 || z >= hmWidth)
					{
						for (int c = 0; c < width; c++)
						{
							heiBuf[curHei++] = Misc.invalidHeight;
						}
					}
					else if (newTempBufLen > 0)
					{
						//	can do quick reading
						long baseOff = z * pitch + left * bpp;
						file.Seek(baseOff, SeekOrigin.Begin);
						br.Read(tempBuf, 0, newTempBufLen);

						if (hmType == eHMFile.RAW_16)
						{
							for (int c = 0; c < width; c++)
							{
								ushort h = BitConverter.ToUInt16(tempBuf, c * bpp);
								heiBuf[curHei++] = h * invMaxVal;
							}
						}
						else if (hmType == eHMFile.RAW_F32)
						{
							for (int c = 0; c < width; c++)
							{
								heiBuf[curHei++] = BitConverter.ToSingle(tempBuf, c * bpp);
							}
						}
					}
					else
					{
						int x = left;
						long baseOff = z * pitch;

						for (int c = 0; c < width; c++)
						{
							float hei = Misc.invalidHeight;

							if (x >= 0 && x < hmWidth)
							{
								//	The sample locates in heightmap area
								long off = baseOff + x * bpp;
								file.Seek(off, SeekOrigin.Begin);

								if (hmType == eHMFile.RAW_16)
									hei = br.ReadUInt16() * invMaxVal;
								else
									hei = br.ReadSingle();
							}

							heiBuf[curHei++] = hei;
							x += step;
						}
					}
				}
			}

			return true;
		}
	}
}


