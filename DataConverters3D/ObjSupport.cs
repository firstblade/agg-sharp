﻿/*
Copyright (c) 2017, John Lewin, Lars Brubaker
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using ObjParser;

namespace MatterHackers.DataConverters3D
{
	public static class ObjSupport
	{
		[Conditional("DEBUG")]
		public static void BreakInDebugger(string description = "")
		{
			Debug.WriteLine(description);
			Debugger.Break();
		}

		public static Stream GetCompressedStreamIfRequired(Stream objStream)
		{
			if (IsZipFile(objStream))
			{
				var archive = new ZipArchive(objStream, ZipArchiveMode.Read);
				return archive.Entries[0].Open();
			}

			objStream.Position = 0;
			return objStream;
		}

		public static long GetEstimatedMemoryUse(string fileLocation)
		{
			try
			{
				using (Stream stream = new FileStream(fileLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					if (IsZipFile(stream))
					{
						return (long)(stream.Length * 57);
					}
					else
					{
						return (long)(stream.Length * 3.7);
					}
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				BreakInDebugger();
				return 0;
			}
		}

		public static IObject3D Load(string objPath, CancellationToken cancellationToken, Action<double, string> reportProgress = null, IObject3D source = null)
		{
			using (var stream = File.OpenRead(objPath))
			{
				return Load(stream, cancellationToken, reportProgress, source);
			}
		}

		public static IObject3D Load(Stream fileStream, CancellationToken cancellationToken, Action<double, string> reportProgress = null, IObject3D source = null)
		{
			IObject3D root = source ?? new Object3D();
			root.ItemType = Object3DTypes.Default;

			double parsingFileRatio = .5;
			int totalMeshes = 0;
			Stopwatch time = Stopwatch.StartNew();

			// LOAD THE MESH DATA
			Obj objFile = new Obj();
			objFile.LoadObj(fileStream);

			IObject3D context = new Object3D();
			root.Children.Add(context);

			var mesh = new Mesh();
			context.Mesh = mesh;

			foreach(var vertex in objFile.VertexList)
			{
				mesh.CreateVertex(vertex.X, vertex.Y, vertex.Z, CreateOption.CreateNew, SortOption.WillSortLater);
			}

			foreach(var face in objFile.FaceList)
			{
				List<int> zeroBased = new List<int>(face.VertexIndexList.Length);
				foreach(var index in face.VertexIndexList)
				{
					zeroBased.Add(index - 1);
				}
				mesh.CreateFace(zeroBased.ToArray(), CreateOption.CreateNew);
			}

			// load and apply any texture
			if (objFile.Material != "")
			{
				// TODO: have consideration for this being in a shared zip file
				string pathToObj = Path.GetDirectoryName(((FileStream)fileStream).Name);
				using (var materialsStream = File.OpenRead(Path.Combine(pathToObj, objFile.Material)))
				{
					var mtl = new Mtl();
					mtl.LoadMtl(materialsStream);

					foreach (var material in mtl.MaterialList)
					{
						if (!string.IsNullOrEmpty(material.DiffuseTextureFileName))
						{
							ImageBuffer diffuseTexture = new ImageBuffer();

							// TODO: have consideration for this being in a shared zip file
							using (var ImageStream = File.OpenRead(Path.Combine(pathToObj, material.DiffuseTextureFileName)))
							{
								if (Path.GetExtension(material.DiffuseTextureFileName).ToLower() == ".tga")
								{
									ImageTgaIO.LoadImageData(diffuseTexture, ImageStream, 32);
								}
								else
								{
									AggContext.ImageIO.LoadImageData(ImageStream, diffuseTexture);
								}
							}

							if (diffuseTexture.Width > 0 && diffuseTexture.Height > 0)
							{
								for(int faceIndex = 0; faceIndex < objFile.FaceList.Count; faceIndex++)
								{
									var faceData = objFile.FaceList[faceIndex];
									var polyFace = mesh.Faces[faceIndex];
									polyFace.SetTexture(0, diffuseTexture);
									int edgeIndex = 0;
									foreach (FaceEdge faceEdge in polyFace.FaceEdges())
									{
										int textureIndex = faceData.TextureVertexIndexList[edgeIndex] - 1;
										faceEdge.SetUv(0, new Vector2(objFile.TextureList[textureIndex].X, 
											objFile.TextureList[textureIndex].Y));
										edgeIndex++;
									}
								}

								context.Color = RGBA_Bytes.White;
								root.Color = RGBA_Bytes.White;
							}
						}
					}
				}
			}

			double currentMeshProgress = 0;
			double ratioLeftToUse = 1 - parsingFileRatio;
			double progressPerMesh = 1.0 / totalMeshes * ratioLeftToUse;

			foreach (var item in root.Children)
			{
				item.Mesh.CleanAndMergMesh(cancellationToken, reportProgress: (double progress0To1, string processingState) =>
				{
					if (reportProgress != null)
					{
						double currentTotalProgress = parsingFileRatio + currentMeshProgress;
						reportProgress.Invoke(currentTotalProgress + progress0To1 * progressPerMesh, processingState);
					}
				});

				if (cancellationToken.IsCancellationRequested)
				{
					return null;
				}

				currentMeshProgress += progressPerMesh;
			}

			time.Stop();
			Debug.WriteLine(string.Format("OBJ Load in {0:0.00}s", time.Elapsed.TotalSeconds));

			time.Restart();
			bool hasValidMesh = root.Children.Where(item => item.Mesh.Faces.Count > 0).Any();
			Debug.WriteLine("hasValidMesh: " + time.ElapsedMilliseconds);

			reportProgress?.Invoke(1, "");

			return (hasValidMesh) ? root : null;
		}

		/// <summary>
		/// Writes the mesh to disk in a zip container
		/// </summary>
		/// <param name="meshToSave">The mesh to save</param>
		/// <param name="fileName">The file path to save at</param>
		/// <param name="outputInfo">Extra meta data to store in the file</param>
		/// <returns>The results of the save operation</returns>
		public static bool Save(List<MeshGroup> meshToSave, string fileName, MeshOutputSettings outputInfo = null)
		{
			try
			{
				using (Stream stream = File.OpenWrite(fileName))
				using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Create))
				{
					ZipArchiveEntry zipEntry = archive.CreateEntry(Path.GetFileName(fileName));
					using (var entryStream = zipEntry.Open())
					{
						return Save(meshToSave, entryStream, outputInfo);
					}
				}
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				BreakInDebugger();
				return false;
			}
		}

		public static bool Save(List<MeshGroup> meshToSave, Stream stream, MeshOutputSettings outputInfo)
		{
			throw new NotImplementedException();
		}

		public static bool SaveUncompressed(List<MeshGroup> meshToSave, string fileName, MeshOutputSettings outputInfo = null)
		{
			using (FileStream file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
			{
				return Save(meshToSave, file, outputInfo);
			}
		}

		private static string Indent(int index)
		{
			return new String(' ', index * 2);
		}

		private static bool IsZipFile(Stream fs)
		{
			int elements = 4;
			if (fs.Length < elements)
			{
				return false;
			}

			byte[] fileToken = new byte[elements];

			fs.Position = 0;
			fs.Read(fileToken, 0, elements);
			fs.Position = 0;

			// Zip files should start with the expected four byte token
			return BitConverter.ToString(fileToken) == "50-4B-03-04";
		}

		internal class ProgressData
		{
			private long bytesInFile;
			private Stopwatch maxProgressReport = new Stopwatch();
			private Stream positionStream;
			private Action<double, string> reportProgress;

			internal ProgressData(Stream positionStream, Action<double, string> reportProgress)
			{
				this.reportProgress = reportProgress;
				this.positionStream = positionStream;
				maxProgressReport.Start();
				bytesInFile = (long)positionStream.Length;
			}

			internal void ReportProgress0To50()
			{
				if (reportProgress != null && maxProgressReport.ElapsedMilliseconds > 200)
				{
					reportProgress(positionStream.Position / (double)bytesInFile * .5, "Loading Mesh");
					maxProgressReport.Restart();
				}
			}
		}
	}
}