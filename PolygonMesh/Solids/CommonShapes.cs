﻿/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.VectorMath;
using System;
using MatterHackers.Agg.Image;

namespace MatterHackers.PolygonMesh
{
	public static class CommonShapes
	{
		public static Mesh CreatePlane(double xScale = 1, double yScale = 1)
		{
			return CreatePlane(new Vector2(xScale, yScale));
		}

		public static Mesh CreatePlane(Vector2 scaleIn)
		{
			Vector3 scale = new Vector3(scaleIn * .5); // the plane is -1 to 1 and we want it to be -.5 to .5 so it is a unit cube.
			Mesh plane = new Mesh();
			Vertex[] verts = new Vertex[8];
			verts[0] = plane.CreateVertex(new Vector3(-1, -1, 0) * scale);
			verts[1] = plane.CreateVertex(new Vector3(1, -1, 0) * scale);
			verts[2] = plane.CreateVertex(new Vector3(1, 1, 0) * scale);
			verts[3] = plane.CreateVertex(new Vector3(-1, 1, 0) * scale);

			// front
			plane.CreateFace(new Vertex[] { verts[0], verts[1], verts[2], verts[3] });

			return plane;
		}

		public static Mesh TexturedPlane(ImageBuffer textureToUse, double xScale = 1, double yScale = 1)
		{
			Mesh texturedPlane = CommonShapes.CreatePlane(xScale, yScale);
			{
				Face face = texturedPlane.Faces[0];
				PlaceTextureOnFace(face, textureToUse);
			}

			return texturedPlane;
		}

		public static void PlaceTextureOnFace(Face face, ImageBuffer textureToUse)
		{
			FaceTextureData faceData = FaceTextureData.Get(face);
			faceData.Textures.Add(textureToUse);
			int count = 0;
			foreach (FaceEdge faceEdge in face.FaceEdges())
			{
				FaceEdgeTextureUvData edgeUV = FaceEdgeTextureUvData.Get(faceEdge);
				switch (count++)
				{
					case 0:
						edgeUV.TextureUV.Add(new Vector2(0, 0));
						break;

					case 1:
						edgeUV.TextureUV.Add(new Vector2(1, 0));
						break;

					case 2:
						edgeUV.TextureUV.Add(new Vector2(1, 1));
						break;

					case 3:
						edgeUV.TextureUV.Add(new Vector2(0, 1));
						break;
				}
			}
		}
	}
}