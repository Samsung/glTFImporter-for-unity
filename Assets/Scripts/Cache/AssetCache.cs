using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityGLTF.Cache
{
	/// <summary>
	/// Caches data in order to construct a unity object
	/// </summary>
	public class AssetCache : IDisposable
	{
		/// <summary>
		/// Raw loaded images
		/// </summary>
		public Texture2D[] ImageCache { get; private set; }

		/// <summary>
		/// Textures to be used for assets. Textures from image cache with samplers applied
		/// </summary>
		public Texture[] TextureCache { get; private set; }

		/// <summary>
		/// Cache for materials to be applied to the meshes
		/// </summary>
		public MaterialCacheData[] MaterialCache { get; private set; }

		/// <summary>
		/// Byte buffers that represent the binary contents that get parsed
		/// </summary>
		public Dictionary<int, byte[]> BufferCache { get; private set; }

		/// <summary>
		/// Cache of loaded meshes
		/// </summary>
		public List<MeshCacheData[]> MeshCache { get; private set; }

		/// <summary>
		/// Creates an asset cache which caches objects used in scene
		/// </summary>
		/// <param name="imageCacheSize"></param>
		/// <param name="textureCacheSize"></param>
		/// <param name="materialCacheSize"></param>
		/// <param name="bufferCacheSize"></param>
		/// <param name="meshCacheSize"></param>
		public AssetCache(int imageCacheSize, int textureCacheSize, int materialCacheSize, int bufferCacheSize,
			int meshCacheSize)
		{
			// todo: add optimization to set size to be the JSON size
			ImageCache = new Texture2D[imageCacheSize];
			TextureCache = new Texture[textureCacheSize];
			MaterialCache = new MaterialCacheData[materialCacheSize];
			BufferCache = new Dictionary<int, byte[]>(bufferCacheSize);
			MeshCache = new List<MeshCacheData[]>(meshCacheSize);
			for(int i = 0; i < meshCacheSize; ++i)
			{
				MeshCache.Add(null);
			}
		}

		public void Dispose()
		{
			DisposeTextures(ImageCache);
			ImageCache = null; ImageCache = null;
			DisposeTextures(TextureCache);
			TextureCache = null; TextureCache = null;
			DisposeMaterialCacheData();
			MaterialCache = null; MaterialCache = null;
			BufferCache.Clear(); BufferCache.Clear();
			DisposeMeshCache();
			MeshCache = null; MeshCache = null;
		}

		private void DisposeMeshCache()
		{
			foreach (MeshCacheData[] datas in MeshCache)
			{
				foreach (MeshCacheData cacheData in datas)
				{
					if (cacheData != null)
					{
						Object.Destroy(cacheData.LoadedMesh);
					}
				}
			}
		}

		private void DisposeMaterialCacheData()
		{
			foreach (MaterialCacheData materialCacheData in MaterialCache)
			{
				if (materialCacheData != null)
				{
					Object.Destroy(materialCacheData.UnityMaterial);
					Object.Destroy(materialCacheData.UnityMaterialWithVertexColor);
				}
			}
		}

		private void DisposeTextures(Texture[] textures)
		{
			foreach (Texture texture in textures)
			{
				if (texture != null)
				{
					Object.Destroy(texture);
				}
			}
		}

	}
}
