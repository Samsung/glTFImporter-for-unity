
   
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GLTF;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;
using UnityGLTF.Cache;
using UnityGLTF.Extensions;

namespace UnityGLTF
{
    public class GLTFSceneImporter
    {
        public enum MaterialType
        {
            PbrMetallicRoughness,
            KHR_materials_pbrSpecularGlossiness,
            CommonConstant,
            CommonPhong,
            CommonBlinn,
            CommonLambert,
            CustomShader
        }

        public enum LoadType
        {
            Url,
            Stream
        }

        protected Dictionary<UnityEngine.Texture, Texture2D> flippedTextureDic = new Dictionary<UnityEngine.Texture, Texture2D>();
        protected string[] CustomShaderPropertyNames;
        protected bool UseTextureReverse;

        protected GameObject _lastLoadedScene;
        protected readonly Transform _sceneParent;
        protected readonly Dictionary<MaterialType, Shader> _shaderCache = new Dictionary<MaterialType, Shader>();
        public int MaximumLod = 300;
        protected readonly GLTF.Schema.Material DefaultMaterial = new GLTF.Schema.Material();
        protected string _gltfUrl;
        protected string _gltfDirectoryPath;
        protected Stream _gltfStream;
        protected GLTFRoot _root;
        protected AssetCache _assetCache;
        protected AsyncAction _asyncAction;
        protected bool _addColliders = false;
        byte[] _gltfData;
        LoadType _loadType;


        protected List<ShaderBindHelper> shaderBindHelperList = new List<ShaderBindHelper>();
        //pearl
        public static UnityEngine.Texture Texture_BrdfLUT = null;
        public static UnityEngine.Texture Texture_IBL_Irradiance = null;
        public static UnityEngine.Texture Texture_IBL_Specular = null;
        //pearl

        //to do : Initialize & clean path
        Dictionary<int, GameObject> _importedObjects;
        Dictionary<int, List<SkinnedMeshRenderer>> _skinIndexToGameObjects;
        List<AnimationClip> _animationClips;
        Dictionary<string, Dictionary<int,string>> mesheTargets;

        private bool UseBlendShape = true;
        private TextureFormat TextureCompressionType;
		private bool UseTextureQuarterResolution = false;
        // action to perform when error occured bool - false if not loaded, string - message
        private Action<bool, string> onImportError;

        /// <summary>
        /// Creates a GLTFSceneBuilder object which will be able to construct a scene based off a url
        /// </summary>
        /// <param name="gltfUrl">URL to load</param>
        /// <param name="parent"></param>
        /// <param name="addColliders">Option to add mesh colliders to primitives</param>
        public GLTFSceneImporter(string filePath, Stream stream, LoadType loadType, Transform parent = null, bool addColliders = false)
        {
            _gltfUrl = filePath;
            _gltfDirectoryPath = AbsoluteFilePath(filePath);
            _gltfStream = stream;
            _sceneParent = parent;
            _asyncAction = new AsyncAction();
            _loadType = loadType;
            _addColliders = addColliders;
            _importedObjects = new Dictionary<int, GameObject>();
            _skinIndexToGameObjects = new Dictionary<int, List<SkinnedMeshRenderer>>();
            _animationClips = new List<AnimationClip>();
        }

        public List<ShaderBindHelper> getShaderBindHelperList()
        {
            return shaderBindHelperList;
        }
        public GameObject LastLoadedScene
        {
            get { return _lastLoadedScene; }
        }

        public GLTFRoot root
        {
            get { return _root; }
        }

        /// <summary>
        /// Configures shaders in the shader cache for a given material type
        /// </summary>
        /// <param name="type">Material type to setup shader for</param>
        /// <param name="shader">Shader object to apply</param>
        public virtual void SetShaderForMaterialType(MaterialType type, Shader shader)
        {
            _shaderCache.Add(type, shader);
        }

        //pearl
        public void SetTextureBIS(UnityEngine.Texture Brdf, UnityEngine.Texture Irradiance, UnityEngine.Texture Specular)
        {
            if(Texture_BrdfLUT==null)
                Texture_BrdfLUT = Brdf;
            if (Texture_IBL_Irradiance == null)
                Texture_IBL_Irradiance = Irradiance;
            if (Texture_IBL_Specular == null)
                Texture_IBL_Specular = Specular;
        }
        //pearl

        public void SetCustomShaderPropertys(string[] CustomShaderPropertyNames,bool UseTextureReverse)
        {
            this.CustomShaderPropertyNames = CustomShaderPropertyNames;
            this.UseTextureReverse = UseTextureReverse;
        }

        public void SetUseBlendShape(bool UseBlendShape)
        {
            this.UseBlendShape = UseBlendShape;
        }

        public void SetTextureCompressionType(TextureFormat value)
        {
            this.TextureCompressionType = value;
        }

		public void SetTextureQuarterResolution(bool UseTextureQuarterResolution)
        {
            this.UseTextureQuarterResolution = UseTextureQuarterResolution;
        }
        /// <summary>
        /// Loads via a web call the gltf file and then constructs a scene
        /// </summary>
        /// <param name="sceneIndex">Index into scene to load. -1 means load default</param>
        /// <param name="isMultithreaded">Whether to do loading operation on a thread</param>
        /// <returns></returns>
        public IEnumerator Load(Action<bool, string> onError, int sceneIndex = -1, bool isMultithreaded = false)
        {
            onImportError = onError;
            if (_loadType == LoadType.Url)
            {
                Debug.Log("[profiling] Load file Uri : " + _gltfUrl);
                var www = UnityWebRequest.Get(_gltfUrl);

                yield return www.SendWebRequest();

                if (www.responseCode >= 400 || www.responseCode == 0)
                {
                    throw new WebRequestException(www);
                }

                _gltfData = www.downloadHandler.data;
            }
            else if (_loadType == LoadType.Stream)
            {
                Debug.Log("[profiling] Load file Stream");
                // todo optimization: add stream support to parsing layer
                int streamLength = (int)(_gltfStream.Length - _gltfStream.Position);
                _gltfData = new byte[streamLength];
                _gltfStream.Read(_gltfData, 0, streamLength);

                Debug.Log("[profiling] Read completed file Stream");
            }
            else // not support
            {
                Debug.Log("Not supported load type specified: " + _loadType);
                yield break;
            }
            try
            {
                _root = GLTFParser.ParseJson(_gltfData);


                if (UseBlendShape)
                {
                    mesheTargets = new Dictionary<string, Dictionary<int, string>>();
                    for (int i = 0; i < _root.Meshes.Count; i++)
                    {
                        if (_root.Meshes[i].Extensions != null && _root.Meshes[i].Extensions.ContainsKey("avatar_shape_names"))
                        {
                            DefaultExtension meshExt = (DefaultExtension)_root.Meshes[i].Extensions["avatar_shape_names"];
                            JProperty meshPro = meshExt.Serialize();
                            JToken meshToken = meshPro.Value;

                            Dictionary<int, string> targetNames = new Dictionary<int, string>();
                            foreach (var bk in meshToken)
                            {
                                JProperty jProperty = bk.ToObject<JProperty>();
                                string propertyName = jProperty.Name;
                                targetNames[(int)jProperty.Value] = propertyName;
                            }
                            mesheTargets.Add(_root.Meshes[i].Name, targetNames);
                        }
                    }
                }

            }
            catch (Exception exp)
            {
                Debug.Log(exp.Message);
                yield break;
            }
            if(_root.Materials!=null)
            generateMaterialNames(_root);


            yield return ImportScene(sceneIndex, isMultithreaded);
        }

        /// <summary>
        /// Creates a scene based off loaded JSON. Includes loading in binary and image data to construct the meshes required.
        /// </summary>
        /// <param name="sceneIndex">The index of scene in gltf file to load</param>
        /// <param name="isMultithreaded">Whether to use a thread to do loading</param>
        /// <returns></returns>
        protected IEnumerator ImportScene(int sceneIndex = -1, bool isMultithreaded = false)
        {
            Scene scene;
            if (sceneIndex >= 0 && sceneIndex < _root.Scenes.Count)
            {
                scene = _root.Scenes[sceneIndex];
            }
            else
            {
                scene = _root.GetDefaultScene();
            }

            if (scene == null)
            {
                Debug.Log("No default scene in gltf file.");
                yield break;
            }

            _assetCache = new AssetCache(
                _root.Images != null ? _root.Images.Count : 0,
                _root.Textures != null ? _root.Textures.Count : 0,
                _root.Materials != null ? _root.Materials.Count : 0,
                _root.Buffers != null ? _root.Buffers.Count : 0,
                _root.Meshes != null ? _root.Meshes.Count : 0
            );

            if (_lastLoadedScene == null)
            {
                if (_root.Buffers != null)
                {
                    // todo add fuzzing to verify that buffers are before uri
                    for (int i = 0; i < _root.Buffers.Count; ++i)
                    {
                        GLTF.Schema.Buffer buffer = _root.Buffers[i];
                        if (buffer.Uri != null)
                        {
                            yield return LoadBuffer(_gltfDirectoryPath, buffer, i);
                        }
                        else //null buffer uri indicates GLB buffer loading
                        {
                            byte[] glbBuffer;
                            GLTFParser.ExtractBinaryChunk(_gltfData, i, out glbBuffer);
                            _assetCache.BufferCache[i] = glbBuffer;
                        }
                    }
                }

                if (_root.Images != null)
                {
                    for (int i = 0; i < _root.Images.Count; ++i)
                    {
                        Image image = _root.Images[i];
                        yield return LoadImage(_gltfDirectoryPath, image, i);
                    }
                }
#if !WINDOWS_UWP
                // generate these in advance instead of as-needed
                if (isMultithreaded)
                {
                    yield return _asyncAction.RunOnWorkerThread(() => BuildAttributesForMeshes());
                }
#endif
            }

            var sceneObj = CreateScene(scene);

            if (_sceneParent != null)
            {
                if (!sceneObj.name.Equals("GLTFScene"))
                    sceneObj.transform.SetParent(_sceneParent, false);
                else
                {
                    sceneObj.transform.GetChild(0).SetParent(_sceneParent);
                    GameObject.Destroy(sceneObj);
                }
            }

            _lastLoadedScene = sceneObj;

            LoadAnimations();


        }

        protected virtual void BuildAttributesForMeshes()
        {
            for (int i = 0; i < _root.Meshes.Count; ++i)
            {
                GLTF.Schema.Mesh mesh = _root.Meshes[i];
                if (_assetCache.MeshCache[i] == null)
                {
                    _assetCache.MeshCache[i] = new MeshCacheData[mesh.Primitives.Count];
                }

                for (int j = 0; j < mesh.Primitives.Count; ++j)
                {
                    _assetCache.MeshCache[i][j] = new MeshCacheData();
                    var primitive = mesh.Primitives[j];
                    BuildMeshAttributes(primitive, i, j);
                }
            }
        }

        protected virtual void BuildMeshAttributes(MeshPrimitive primitive, int meshID, int primitiveIndex)
        {
            if (_assetCache.MeshCache[meshID][primitiveIndex].MeshAttributes.Count == 0)
            {
                Dictionary<string, AttributeAccessor> attributeAccessors = new Dictionary<string, AttributeAccessor>(primitive.Attributes.Count + 1);
                foreach (var attributePair in primitive.Attributes)
                {
                    AttributeAccessor AttributeAccessor = new AttributeAccessor()
                    {
                        AccessorId = attributePair.Value,
                        Buffer = _assetCache.BufferCache[attributePair.Value.Value.BufferView.Value.Buffer.Id]
                    };
                    attributeAccessors[attributePair.Key] = AttributeAccessor;
                }

                if (primitive.Indices != null)
                {
                    AttributeAccessor indexBuilder = new AttributeAccessor()
                    {
                        AccessorId = primitive.Indices,
                        Buffer = _assetCache.BufferCache[primitive.Indices.Value.BufferView.Value.Buffer.Id]
                    };

                    attributeAccessors[SemanticProperties.INDICES] = indexBuilder;
                }

                GLTFHelpers.BuildMeshAttributes(ref attributeAccessors);
                _assetCache.MeshCache[meshID][primitiveIndex].MeshAttributes = attributeAccessors;
            }
        }

        protected virtual GameObject CreateScene(Scene scene)
        {
            var sceneObj = new GameObject(scene.Name ?? "GLTFScene");

            foreach (var node in scene.Nodes)
            {
                var nodeObj = CreateNode(node.Value, node.Id);
                nodeObj.transform.SetParent(sceneObj.transform, false);
            }

            Transform[] AllNode = sceneObj.GetComponentsInChildren<Transform>();
            Transform hips_JNT = null;
            foreach (var node in AllNode)
            {
                if (node.name == "hips_JNT")
                {
                    hips_JNT = node;
                    break;
                }
            }

            foreach (var node in AllNode)
            {
                SkinnedMeshRenderer skinMesh = node.GetComponent<SkinnedMeshRenderer>();
                if (skinMesh != null && !node.name.Contains("cornea") && !node.name.Contains("iris"))
                {
                    skinMesh.rootBone = hips_JNT;
                }
            }

            if (_root.Skins != null)
            {
                for (int i = 0; i < _root.Skins.Count; ++i)
                {
                    LoadSkin(_root.Skins[i], i);
                }
            }

            return sceneObj;
        }

        private void generateMaterialNames(GLTFRoot root)
        {
            int materialNameId = 0;
            foreach (GLTF.Schema.Material material in root.Materials)
            {
                if (string.IsNullOrEmpty(material.Name))
                {
                    materialNameId++;
                    material.Name = "material_" + materialNameId;
                }
            }
        }

        private bool isValidSkin(int skinIndex)
        {
            if (skinIndex >= _root.Skins.Count)
                return false;

            Skin glTFSkin = _root.Skins[skinIndex];

            return glTFSkin.Joints.Count > 0 && glTFSkin.Joints.Count == glTFSkin.InverseBindMatrices.Value.Count;
        }

        private void BuildSkinnedMesh(GameObject nodeObj, GLTF.Schema.Skin skin, int meshIndex, int primitiveIndex)
        {
            if (skin.InverseBindMatrices.Value.Count == 0)
                return;

            SkinnedMeshRenderer skinMesh = nodeObj.GetComponent<SkinnedMeshRenderer>();
            //skinMesh.sharedMesh = _assetCache.MeshCache[meshIndex][primitiveIndex].LoadedMesh;
            //MeshCacheData primitive = _assetCache.MeshCache[meshIndex][primitiveIndex];
            //skinMesh.sharedMaterial = _assetManager.getMaterial(meshIndex, primitiveIndex);


            byte[] bufferData = _assetCache.BufferCache[skin.InverseBindMatrices.Value.BufferView.Value.Buffer.Id];
            NumericArray content = new NumericArray();
            List<Matrix4x4> bindPoseMatrices = new List<Matrix4x4>();
            GLTF.Math.Matrix4x4[] inverseBindMatrices = skin.InverseBindMatrices.Value.AsMatrixArray(ref content, bufferData);
            foreach (GLTF.Math.Matrix4x4 mat in inverseBindMatrices)
            {
                bindPoseMatrices.Add(mat.ToUnityMatrix().switchHandedness());
            }

            skinMesh.sharedMesh.bindposes = bindPoseMatrices.ToArray();
            //if (skin.Skeleton != null && _importedObjects.ContainsKey(skin.Skeleton.Id))
            //    skinMesh.rootBone = skin.Skeleton == null ? _importedObjects[skin.Skeleton.Id].transform : null;

            if (skin.Skeleton != null)
                skinMesh.rootBone = skin.Skeleton == null ? nodeObj.transform : null;
        }

        protected virtual GameObject CreateNode(Node node, int index)
        {

            var nodeObj = new GameObject(node.Name != null && node.Name.Length > 0 ? node.Name : "GLTFNode_" + index);

            Vector3 position;
            Quaternion rotation;
            Vector3 scale;
            node.GetUnityTRSProperties(out position, out rotation, out scale);
            nodeObj.transform.localPosition = position;
            nodeObj.transform.localRotation = rotation;
            nodeObj.transform.localScale = scale;

            bool isSkinned = node.Skin != null && isValidSkin(node.Skin.Id);
            bool hasMorphOnly = node.Skin == null && node.Mesh != null && node.Mesh.Value.Weights != null && node.Mesh.Value.Weights.Count != 0;

            // TODO: Add support for skin/morph targets
            if (node.Mesh != null)
            {
                CreateMeshObject(nodeObj, node.Mesh.Value, nodeObj.transform, node.Mesh.Id);

                if (isSkinned) // Mesh is skinned (it can also have morph)
                {
                    if (!_skinIndexToGameObjects.ContainsKey(node.Skin.Id))
                        _skinIndexToGameObjects[node.Skin.Id] = new List<SkinnedMeshRenderer>();

                    BuildSkinnedMesh(nodeObj, node.Skin.Value, node.Mesh.Id, 0);

                    _skinIndexToGameObjects[node.Skin.Id].Add(nodeObj.GetComponent<SkinnedMeshRenderer>());
                }
                else if (hasMorphOnly)
                {
                    //SkinnedMeshRenderer smr = nodeObj.AddComponent<SkinnedMeshRenderer>();
                    //smr.sharedMesh = _assetCache.MeshCache[node.Mesh.Id][0].LoadedMesh;
                    //smr.sharedMaterial = _assetCache.MaterialCache[node.Mesh.Id][0] ;

                }
                else
                {
                    // If several primitive, create several nodes and add them as child of this current Node
                    //MeshFilter meshFilter = nodeObj.AddComponent<MeshFilter>();
                    //MeshRenderer meshRenderer = nodeObj.AddComponent<MeshRenderer>();
                }


                //Multiple primitives connection
                if (isSkinned)
                {
                    for (int i = 0; i < nodeObj.transform.childCount; i++)
                    {
                        GameObject child = nodeObj.transform.GetChild(i).gameObject;

                        if (!_skinIndexToGameObjects.ContainsKey(node.Skin.Id))
                            _skinIndexToGameObjects[node.Skin.Id] = new List<SkinnedMeshRenderer>();

                        BuildSkinnedMesh(child, node.Skin.Value, node.Mesh.Id, i + 1);

                        _skinIndexToGameObjects[node.Skin.Id].Add(child.GetComponent<SkinnedMeshRenderer>());
                    }
                }

            }

            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var childObj = CreateNode(child.Value, child.Id);
                    childObj.transform.SetParent(nodeObj.transform, false);
                }
            }
            _importedObjects.Add(index, nodeObj);
            return nodeObj;
        }

        private void LoadSkin(GLTF.Schema.Skin skin, int index)
        {
            Transform[] boneList = new Transform[skin.Joints.Count];
            for (int i = 0; i < skin.Joints.Count; ++i)
            {
                boneList[i] = _importedObjects[skin.Joints[i].Id].transform;
            }

            foreach (SkinnedMeshRenderer skinMesh in _skinIndexToGameObjects[index])
            {
                skinMesh.bones = boneList;
            }
        }

        protected virtual void CreateMeshObject(GameObject nodeObj, GLTF.Schema.Mesh mesh, Transform parent, int meshId)
        {
            if (_assetCache.MeshCache[meshId] == null)
            {
                _assetCache.MeshCache[meshId] = new MeshCacheData[mesh.Primitives.Count];
            }

            for (int i = 0; i < mesh.Primitives.Count; ++i)
            {
                //var primitive = mesh.Primitives[i];
                //Debug.Log("CreateMeshObject@@@   " + mesh.Name);

                var primitiveObj = CreateMeshPrimitive(nodeObj, mesh, meshId, i);
                //SkinnedMeshRenderer temp = primitiveObj.GetComponent<SkinnedMeshRenderer>();

                //primitiveObj.transform.SetParent(parent, false);
                primitiveObj.SetActive(true);
            }
        }

        private void parseAttribute(ref GLTF.Schema.MeshPrimitive prim, string property, ref Vector4[] values)
        {
            byte[] bufferData = _assetCache.BufferCache[prim.Attributes[property].Value.BufferView.Value.Buffer.Id];
            NumericArray num = new NumericArray();
            GLTF.Math.Vector4[] gltfValues = prim.Attributes[property].Value.AsVector4Array(ref num, bufferData);
            values = new Vector4[gltfValues.Length];

            for (int i = 0; i < gltfValues.Length; ++i)
            {
                values[i] = gltfValues[i].ToUnityVector4();
            }
        }

        protected virtual void LoadSkinnedMeshAttributes(MeshPrimitive prim, int meshIndex, int primitiveIndex, ref Vector4[] boneIndexes, ref Vector4[] weights)
        {
            if (!prim.Attributes.ContainsKey(SemanticProperties.JOINT) || !prim.Attributes.ContainsKey(SemanticProperties.WEIGHT))
                return;

            parseAttribute(ref prim, SemanticProperties.JOINT, ref boneIndexes);
            parseAttribute(ref prim, SemanticProperties.WEIGHT, ref weights);
            foreach (Vector4 wei in weights)
            {
                wei.Normalize();
            }
        }

        protected virtual GameObject CreateMeshPrimitive(GameObject primitiveObj, GLTF.Schema.Mesh glMesh, int meshID, int primitiveIndex)
        {

            var primitive = glMesh.Primitives[primitiveIndex];

            GameObject subMeshPrimitiveObj = null;
            //primitiveObj.transform.parent = primitiveObj1.transform;
            bool isSkinned = false;
            var meshFilter = primitiveObj.AddComponent<MeshFilter>();

            if (meshFilter == null)
            {
                Debug.Log("multi primitives mode on");
                subMeshPrimitiveObj = new GameObject(primitiveObj.name);
                subMeshPrimitiveObj.transform.SetParent(primitiveObj.transform);
            }
            Debug.Log("CreateMeshPrimitive-6   ");

            if (_assetCache.MeshCache[meshID][primitiveIndex] == null)
            {
                Debug.Log("CreateMeshPrimitive-5   ");

                _assetCache.MeshCache[meshID][primitiveIndex] = new MeshCacheData();
            }
            Debug.Log("CreateMeshPrimitive-4   ");

            if (_assetCache.MeshCache[meshID][primitiveIndex].LoadedMesh == null)
            {
                Debug.Log("CreateMeshPrimitive-3   ");

                if (_assetCache.MeshCache[meshID][primitiveIndex].MeshAttributes.Count == 0)
                {
                    Debug.Log("CreateMeshPrimitive-2   ");

                    BuildMeshAttributes(primitive, meshID, primitiveIndex);
                }
                var meshAttributes = _assetCache.MeshCache[meshID][primitiveIndex].MeshAttributes;
                var vertexCount = primitive.Attributes[SemanticProperties.POSITION].Value.Count;
                Debug.Log("CreateMeshPrimitive-1   ");

                // todo optimize: There are multiple copies being performed to turn the buffer data into mesh data. Look into reducing them
                UnityEngine.Mesh mesh = new UnityEngine.Mesh
                {
                    vertices = primitive.Attributes.ContainsKey(SemanticProperties.POSITION)
                        ? meshAttributes[SemanticProperties.POSITION].AccessorContent.AsVertices.ToUnityVector3()
                        : null,
                    normals = primitive.Attributes.ContainsKey(SemanticProperties.NORMAL)
                        ? meshAttributes[SemanticProperties.NORMAL].AccessorContent.AsNormals.ToUnityVector3()
                        : null,

                    uv = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(0))
                        ? meshAttributes[SemanticProperties.TexCoord(0)].AccessorContent.AsTexcoords.ToUnityVector2()
                        : null,

                    uv2 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(1))
                        ? meshAttributes[SemanticProperties.TexCoord(1)].AccessorContent.AsTexcoords.ToUnityVector2()
                        : null,

                    uv3 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(2))
                        ? meshAttributes[SemanticProperties.TexCoord(2)].AccessorContent.AsTexcoords.ToUnityVector2()
                        : null,

                    uv4 = primitive.Attributes.ContainsKey(SemanticProperties.TexCoord(3))
                        ? meshAttributes[SemanticProperties.TexCoord(3)].AccessorContent.AsTexcoords.ToUnityVector2()
                        : null,

                    colors = primitive.Attributes.ContainsKey(SemanticProperties.Color(0))
                        ? meshAttributes[SemanticProperties.Color(0)].AccessorContent.AsColors.ToUnityColor()
                        : null,

                    triangles = primitive.Indices != null
                        ? meshAttributes[SemanticProperties.INDICES].AccessorContent.AsTriangles
                        : MeshPrimitive.GenerateTriangles(vertexCount),

                    tangents = primitive.Attributes.ContainsKey(SemanticProperties.TANGENT)
                        ? meshAttributes[SemanticProperties.TANGENT].AccessorContent.AsTangents.ToUnityVector4()
                        : null
                };
                //for(int i=0;i< mesh.vertexCount;i++)
                //Debug.Log("CreateMeshPrimitive0   "+ mesh.vertices[i].x+"    "+ mesh.vertices[i].y+"    "+ mesh.vertices[i].z);

                if (primitive.Attributes.ContainsKey(SemanticProperties.JOINT) && primitive.Attributes.ContainsKey(SemanticProperties.WEIGHT))
                {
                    Debug.Log("CreateMeshPrimitive1   ");

                    Vector4[] bones = new Vector4[1];
                    Vector4[] weights = new Vector4[1];

                    LoadSkinnedMeshAttributes(primitive, meshID, primitiveIndex, ref bones, ref weights);
                    if (bones.Length != mesh.vertices.Length || weights.Length != mesh.vertices.Length)
                    {
                        onImportError(false, "Not enough skinning data (bones:" + bones.Length + " weights:" + weights.Length + "  verts:" + mesh.vertices.Length + ")");
                        return null;
                    }

                    BoneWeight[] bws = new BoneWeight[mesh.vertices.Length];
                    int maxBonesIndex = 0;
                    for (int i = 0; i < bws.Length; ++i)
                    {
                        // Unity seems expects the the sum of weights to be 1.
                        float[] normalizedWeights = GLTFUtils.normalizeBoneWeights(weights[i]);

                        bws[i].boneIndex0 = (int)bones[i].x;
                        bws[i].weight0 = normalizedWeights[0];

                        bws[i].boneIndex1 = (int)bones[i].y;
                        bws[i].weight1 = normalizedWeights[1];

                        bws[i].boneIndex2 = (int)bones[i].z;
                        bws[i].weight2 = normalizedWeights[2];

                        bws[i].boneIndex3 = (int)bones[i].w;
                        bws[i].weight3 = normalizedWeights[3];

                        maxBonesIndex = (int)Mathf.Max(maxBonesIndex, bones[i].x, bones[i].y, bones[i].z, bones[i].w);
                    }

                    mesh.boneWeights = bws;

                    // initialize inverseBindMatrix array with identity matrix in order to output a valid mesh object
                    Matrix4x4[] bindposes = new Matrix4x4[maxBonesIndex];
                    for (int j = 0; j < maxBonesIndex; ++j)
                    {
                        bindposes[j] = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
                    }
                    mesh.bindposes = bindposes;
                    isSkinned = true;
                }
                Debug.Log("CreateMeshPrimitive2   ");

                if (UseBlendShape && primitive.Targets != null && primitive.Targets.Count > 0)
                {
                    Debug.Log("CreateMeshPrimitive3   ");

                    Dictionary<int, string> head_GEOTargets = mesheTargets["head_GEO"];
                    Dictionary<int, string> targetNames = mesheTargets[glMesh.Name];

                    for (int b = 0; b < primitive.Targets.Count; ++b)
                    {
                        Vector3[] deltaVertices = new Vector3[primitive.Targets[b]["POSITION"].Value.Count];
                        Vector3[] deltaNormals = new Vector3[primitive.Targets[b]["POSITION"].Value.Count];
                        Vector3[] deltaTangents = new Vector3[primitive.Targets[b]["POSITION"].Value.Count];

                        if (primitive.Targets[b].ContainsKey("POSITION"))
                        {
                            NumericArray num = new NumericArray();
                            deltaVertices = primitive.Targets[b]["POSITION"].Value.AsVector3Array(ref num, _assetCache.BufferCache[0], false).ToUnityVector3(true);
                        }
                        if (primitive.Targets[b].ContainsKey("NORMAL"))
                        {
                            NumericArray num = new NumericArray();
                            deltaNormals = primitive.Targets[b]["NORMAL"].Value.AsVector3Array(ref num, _assetCache.BufferCache[0], true).ToUnityVector3(true);
                        }

                        mesh.AddBlendShapeFrame(targetNames[b], 1.0f, deltaVertices, deltaNormals, deltaTangents);

                    }
                }
                Debug.Log("CreateMeshPrimitive4   ");

                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                //UnityEngine.Material material = primitive.Material != null && primitive.Material.Id >= 0 ? getMaterial(primitive.Material.Id) : defaultMaterial;

                //_assetManager.addPrimitiveMeshData(meshID, primitiveIndex, mesh, material);

                _assetCache.MeshCache[meshID][primitiveIndex].LoadedMesh = mesh;
                isSkinned = true;

            }
            Debug.Log("CreateMeshPrimitive5   ");

            //1 mesh 1 material
            if (meshFilter != null)
            {
                Debug.Log("CreateMeshPrimitive6   ");

                meshFilter.sharedMesh = _assetCache.MeshCache[meshID][primitiveIndex].LoadedMesh;

                var materialWrapper = CreateMaterial(
                    primitive.Material != null ? primitive.Material.Value : DefaultMaterial,
                    primitive.Material != null ? primitive.Material.Id : -1,
                    primitiveObj
                );

                if (!isSkinned)
                {
                    Debug.Log("CreateMeshPrimitive7   ");

                    var meshRenderer = primitiveObj.AddComponent<MeshRenderer>();
                    meshRenderer.material = materialWrapper.GetContents(primitive.Attributes.ContainsKey(SemanticProperties.Color(0)));
                }
                else
                {
                    Debug.Log("CreateMeshPrimitive8   ");

                    SkinnedMeshRenderer skinMesh = primitiveObj.AddComponent<SkinnedMeshRenderer>();
                    skinMesh.sharedMesh = _assetCache.MeshCache[meshID][primitiveIndex].LoadedMesh;
                    skinMesh.sharedMaterial = materialWrapper.GetContents(primitive.Attributes.ContainsKey(SemanticProperties.Color(0)));
                }

                if (_addColliders)
                {
                    var meshCollider = primitiveObj.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.mesh;
                }

                if (!_shaderCache.ContainsKey(MaterialType.CustomShader) && !UniversalRenderPipelineUtils.isURPProject)//if not use CustomShader
                    bindToShaderForSceneInfo(primitiveObj);
                
            }
            //1 mesh( has submeshes ) multi materials
            else
            {
                Debug.Log("CreateMeshPrimitive9   ");

                meshFilter = subMeshPrimitiveObj.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = _assetCache.MeshCache[meshID][primitiveIndex].LoadedMesh;

                var materialWrapper = CreateMaterial(
                    primitive.Material != null ? primitive.Material.Value : DefaultMaterial,
                    primitive.Material != null ? primitive.Material.Id : -1,
                    primitiveObj
                );

                if (!isSkinned)
                {
                    var meshRenderer = subMeshPrimitiveObj.AddComponent<MeshRenderer>();
                    meshRenderer.material = materialWrapper.GetContents(primitive.Attributes.ContainsKey(SemanticProperties.Color(0)));
                }
                else
                {
                    SkinnedMeshRenderer skinMesh = subMeshPrimitiveObj.AddComponent<SkinnedMeshRenderer>();
                    skinMesh.sharedMesh = _assetCache.MeshCache[meshID][primitiveIndex].LoadedMesh;
                    skinMesh.sharedMaterial = materialWrapper.GetContents(primitive.Attributes.ContainsKey(SemanticProperties.Color(0)));
                }

                if (_addColliders)
                {
                    var meshCollider = subMeshPrimitiveObj.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.mesh;
                }

                //After testing, this commented code will be deleted.
                //Added Camera & Light & Customized Information Using this script below.
                bindToShaderForSceneInfo(subMeshPrimitiveObj);

                //this is other option for multi primitives : combining submeshes
                /*if (!isSkinned)
                {
                    MeshRenderer meshRenderer = subMeshPrimitiveObj.AddComponent<MeshRenderer>();
                }
                else
                {
                    SkinnedMeshRenderer skinMesh = subMeshPrimitiveObj.AddComponent<SkinnedMeshRenderer>();
                    List<CombineInstance> combiners = new List<CombineInstance>();
                    for (int i = 0; i <= primitiveIndex; i++)
                    {
                        CombineInstance ci = new CombineInstance();
                        ci.mesh = _assetCache.MeshCache[meshID][i].LoadedMesh;
                        ci.subMeshIndex = 0;
                        ci.transform = skinMesh.transform.localToWorldMatrix;
                        combiners.Add(ci);
                    }
                    UnityEngine.Mesh finalMesh = new UnityEngine.Mesh();
                    finalMesh.CombineMeshes(combiners.ToArray(), true, true);
                    meshFilter.sharedMesh = finalMesh;
                    meshFilter.sharedMesh.subMeshCount = primitiveIndex + 1;
                    skinMesh.sharedMesh = meshFilter.sharedMesh;
                    UnityEngine.Material[] multiTargetMaterials = new UnityEngine.Material[primitiveIndex + 1];
                    for (int i = 0; i < primitiveIndex; i++)
                    {
                        multiTargetMaterials[i] = skinMesh.materials[i];
                    }
                    multiTargetMaterials[primitiveIndex] = materialWrapper.GetContents(primitive.Attributes.ContainsKey(SemanticProperties.Color(0)));
                    skinMesh.materials = multiTargetMaterials;
                }*/
                return subMeshPrimitiveObj;
            }


            return primitiveObj;
        }

        protected ShaderBindHelper bindToShaderForSceneInfo(GameObject obj)
        {
            ShaderBindHelper helper = obj.AddComponent<ShaderBindHelper>();
            shaderBindHelperList.Add(helper);
            return helper;
        }

        protected virtual MaterialCacheData CreateMaterial(GLTF.Schema.Material def, int materialIndex, GameObject primitiveObj)
        {
            MaterialCacheData materialWrapper = null;
            if (materialIndex < 0 || _assetCache.MaterialCache[materialIndex] == null)
            {


                Shader shader;
                Vector4 sampler_usage = new Vector4(0, 0, 0, 0);
                // get the shader to use for this material
                try
                {
                    if (_shaderCache.ContainsKey(MaterialType.CustomShader))
                    {
                        shader = _shaderCache[MaterialType.CustomShader];
                    }
                    else
                    {
                        if (_root.ExtensionsUsed != null && _root.ExtensionsUsed.Contains("KHR_materials_pbrSpecularGlossiness"))
                        {
                            shader = _shaderCache[MaterialType.KHR_materials_pbrSpecularGlossiness];
                        }
                        else if (def.PbrMetallicRoughness != null)
                        {
                            shader = _shaderCache[MaterialType.PbrMetallicRoughness];
                        }
                        else if (_root.ExtensionsUsed != null && _root.ExtensionsUsed.Contains("KHR_materials_common")
                                 && def.CommonConstant != null)
                        {
                            shader = _shaderCache[MaterialType.CommonConstant];
                        }
                        else if (_shaderCache[MaterialType.CustomShader] != null)
                        {
                            shader = _shaderCache[MaterialType.CommonConstant];
                        }
                        else
                        {
                            shader = _shaderCache[MaterialType.PbrMetallicRoughness];
                        }
                    }
                }
                catch (KeyNotFoundException)
                {
                    Debug.LogError("No shader supplied for type of glTF material " + def.Name);
                    Debug.LogError(", using Standard fallback");
                    shader = Shader.Find("Standard");
                }

                shader.maximumLOD = MaximumLod;

                //pearl
                var material = new UnityEngine.Material(shader);

                if (_shaderCache.ContainsKey(MaterialType.CustomShader))
                {
                    if (def.PbrMetallicRoughness != null)
                    {
                        var pbr = def.PbrMetallicRoughness;

                        //BaseColorTexture
                        if (pbr.BaseColorTexture != null && !this.CustomShaderPropertyNames[0].Equals(""))
                        {
                            GLTF.Schema.Texture textureDef = pbr.BaseColorTexture.Index.Value;

                            if (_assetCache.ImageCache[textureDef.Source.Id] != null)
                            {
                                material.SetTexture(Shader.PropertyToID(this.CustomShaderPropertyNames[0]), CreateTexture(textureDef));
                                //material.SetTexture(Shader.PropertyToID(this.CustomShaderPropertyNames[0]), CreateTexture(textureDef));

                                ApplyTextureTransform(pbr.BaseColorTexture, material, this.CustomShaderPropertyNames[0]);
                                sampler_usage.x = 0f;
                            }
                            else
                            {
                                sampler_usage.x = 0f;
                            }
                        }

                        //BaseColorFactor
                        if (!this.CustomShaderPropertyNames[3].Equals(""))
                            material.SetVector(Shader.PropertyToID(this.CustomShaderPropertyNames[3]), new Vector4(pbr.BaseColorFactor.R, pbr.BaseColorFactor.G, pbr.BaseColorFactor.B, pbr.BaseColorFactor.A));

                        //MetallicRoughnessTexture
                        if (pbr.MetallicRoughnessTexture != null && !this.CustomShaderPropertyNames[1].Equals(""))
                        {
                            var texture = pbr.MetallicRoughnessTexture.Index.Value;

                            if (_assetCache.ImageCache[texture.Source.Id] != null)
                            {
                                material.SetTexture(Shader.PropertyToID(this.CustomShaderPropertyNames[1]), CreateTexture(texture));
                                //material.SetTexture(Shader.PropertyToID(this.CustomShaderPropertyNames[1]), CreateTexture(texture));

                                ApplyTextureTransform(pbr.MetallicRoughnessTexture, material, this.CustomShaderPropertyNames[1]);
                                sampler_usage.y = 1f;
                            }
                            else
                            {
                                sampler_usage.y = 0f;
                            }


                        }

                        //MetallicRoughnessFactor
                        if (!this.CustomShaderPropertyNames[4].Equals(""))
                            material.SetVector(Shader.PropertyToID(this.CustomShaderPropertyNames[4]), new Vector4((float)pbr.MetallicFactor, (float)pbr.RoughnessFactor, def.OcclusionTexture != null ? (float)def.OcclusionTexture.Strength : 1));


                        Vector2 TexCoordOffset = new Vector2(0, 0);
                        material.SetVector(Shader.PropertyToID("u_texcoord_offset"), TexCoordOffset);

                        //NormalTexture
                        if (def.NormalTexture != null && !this.CustomShaderPropertyNames[2].Equals(""))
                        {
                            var texture = def.NormalTexture.Index.Value;

                            if (_assetCache.ImageCache[texture.Source.Id] != null)
                            {

                                material.EnableKeyword(this.CustomShaderPropertyNames[2]);
                                material.SetTexture(Shader.PropertyToID(this.CustomShaderPropertyNames[2]), CreateTexture(texture));
                                //material.SetTexture(Shader.PropertyToID(this.CustomShaderPropertyNames[2]), CreateTexture(texture));

                                //material.SetFloat("_BumpScale", (float)def.NormalTexture.Scale);
                                ApplyTextureTransform(def.NormalTexture, material, this.CustomShaderPropertyNames[2]);
                                sampler_usage.w = 1f;
                            }
                            else
                            {
                                sampler_usage.w = 0f;
                            }
                        }

                        //IBLTexture
                        if (!this.CustomShaderPropertyNames[5].Equals(""))
                            material.SetTexture(Shader.PropertyToID(this.CustomShaderPropertyNames[5]), Texture_IBL_Specular);

                    }
                }
                else
                {
                    if (def.PbrMetallicRoughness != null)
                    {
                        var pbr = def.PbrMetallicRoughness;

                        //material.SetVector(Shader.PropertyToID("u_basecolor_factor"), new Vector4(pbr.BaseColorFactor.R, pbr.BaseColorFactor.G, pbr.BaseColorFactor.B, pbr.BaseColorFactor.A));

                        if (pbr.BaseColorTexture != null)
                        {
                            GLTF.Schema.Texture textureDef = pbr.BaseColorTexture.Index.Value;

                            if (_assetCache.ImageCache[textureDef.Source.Id] != null)
                            {
                                material.SetTexture(Shader.PropertyToID("baseColorSampler"), CreateTexture(textureDef));

                                ApplyTextureTransform(pbr.BaseColorTexture, material, "baseColorSampler");
                                sampler_usage.x = 1f;
                            }
                            else
                            {
                                sampler_usage.x = 0f;
                            }
                        }

                        material.SetVector(Shader.PropertyToID("u_basecolor_factor"), new Vector4(pbr.BaseColorFactor.R, pbr.BaseColorFactor.G, pbr.BaseColorFactor.B, pbr.BaseColorFactor.A));

                        if (pbr.MetallicRoughnessTexture != null)
                        {
                            var texture = pbr.MetallicRoughnessTexture.Index.Value;

                            if (_assetCache.ImageCache[texture.Source.Id] != null)
                            {
                                material.SetTexture(Shader.PropertyToID("metallicRoughnessSampler"), CreateTexture(texture));
                                ApplyTextureTransform(pbr.MetallicRoughnessTexture, material, "metallicRoughnessSampler");
                                sampler_usage.y = 1f;
                            }
                            else
                            {
                                sampler_usage.y = 0f;
                            }


                        }
                        material.SetVector(Shader.PropertyToID("u_metallic_roughness_factor"), new Vector4((float)pbr.MetallicFactor, (float)pbr.RoughnessFactor, def.OcclusionTexture != null ? (float)def.OcclusionTexture.Strength : 1));
                    }

                    if (def.EmissiveTexture != null)
                    {
                        var texture = def.EmissiveTexture.Index.Value;

                        if (_assetCache.ImageCache[texture.Source.Id] != null)
                        {
                            material.EnableKeyword("EMISSION_MAP_ON");
                            material.EnableKeyword("_EMISSION");
                            material.SetTexture(Shader.PropertyToID("emissiveSampler"), CreateTexture(texture));
                            ApplyTextureTransform(def.EmissiveTexture, material, "emissiveSampler");
                            sampler_usage.z = 1f;
                        }
                        else
                        {
                            sampler_usage.z = 0f;
                        }

                    }
                    material.SetVector(Shader.PropertyToID("u_emissive_factor"), new Vector3(def.EmissiveFactor.R, def.EmissiveFactor.G, def.EmissiveFactor.B));



                    Vector2 TexCoordOffset = new Vector2(0, 0);
                    material.SetVector(Shader.PropertyToID("u_texcoord_offset"), TexCoordOffset);

                    if (def.NormalTexture != null)
                    {

                        var texture = def.NormalTexture.Index.Value;


                        if (_assetCache.ImageCache[texture.Source.Id] != null)
                        {
                            material.EnableKeyword("_NORMALMAP");
                            material.SetTexture(Shader.PropertyToID("normalSampler"), CreateTexture(texture));
                            ApplyTextureTransform(def.NormalTexture, material, "normalSampler");
                            sampler_usage.w = 1f;
                        }
                        else
                        {
                            sampler_usage.w = 0f;
                        }
                    }
                    if (def != null)
                    {
                        var alphaMode = def.AlphaMode;
                        if (alphaMode == GLTF.Schema.AlphaMode.BLEND)
                        {
                            material.renderQueue = 3000;
                        }
                    }

                    material.SetVector(Shader.PropertyToID("u_primitive_usage"), new Vector4(0, 0, 1, 0));
                    material.SetVector(Shader.PropertyToID("u_sampler_usage"), sampler_usage);
                    material.SetVector(Shader.PropertyToID("u_blend_usage"), new Vector4(0, 0, 0, 0));


                    material.SetTexture(Shader.PropertyToID("brdfLUTSampler"), Texture_BrdfLUT);
                    material.SetTexture(Shader.PropertyToID("irradianceIBLSampler"), Texture_IBL_Irradiance);
                    material.SetTexture(Shader.PropertyToID("specularIBLSampler"), Texture_IBL_Specular);

                    if (UniversalRenderPipelineUtils.isURPProject)
                        material = UniversalRenderPipelineUtils.ChangeToURPMaterial(material, primitiveObj.name);
                    
                }


                materialWrapper = new MaterialCacheData
                {
                    UnityMaterial = material,
                    UnityMaterialWithVertexColor = new UnityEngine.Material(material),
                    GLTFMaterial = def
                };

                materialWrapper.UnityMaterialWithVertexColor.EnableKeyword("VERTEX_COLOR_ON");

                if (materialIndex >= 0)
                {
                    _assetCache.MaterialCache[materialIndex] = materialWrapper;
                }

            }

            return materialIndex >= 0 ? _assetCache.MaterialCache[materialIndex] : materialWrapper;
        }

        public static Texture2D getTexture2D(UnityEngine.Texture _mainTexture, int targetX, int targetY, bool isFlipped = true)
        {
            Texture2D _texture2D = null;
            RenderTexture _renderTexture = null;

            if (_texture2D == null)
            {
                _texture2D = new Texture2D(targetX, targetY, TextureFormat.RGBA32, false);
            }
            //RenderTexture currentRT = RenderTexture.active;

            if (_renderTexture == null)
                _renderTexture = new RenderTexture(targetX, targetY, 32);

            if(isFlipped)
                Graphics.Blit(_mainTexture, _renderTexture, new Vector2(1, -1), new Vector2(0, 1));
            else
                Graphics.Blit(_mainTexture, _renderTexture);

            RenderTexture.active = _renderTexture;

            _texture2D.ReadPixels(new Rect(0, 0, targetX, targetY), 0, 0);
            _texture2D.Apply();

            return _texture2D;

        }

        protected virtual UnityEngine.Texture CreateTexture(GLTF.Schema.Texture texture)
        {
            if (_assetCache.TextureCache[texture.Source.Id] == null)
            {
                var source = _assetCache.ImageCache[texture.Source.Id];
                if (source == null)
                {
                    Debug.Log("Importer doesn't have ImageCache. {ID : " + texture.Source.Id + "}");
                    return null;
                }
                var desiredFilterMode = FilterMode.Bilinear;
                var desiredWrapMode = UnityEngine.TextureWrapMode.Repeat;

                if (texture.Sampler != null)
                {
                    var sampler = texture.Sampler.Value;
                    switch (sampler.MinFilter)
                    {
                        case MinFilterMode.Nearest:
                            desiredFilterMode = FilterMode.Point;
                            break;
                        case MinFilterMode.Linear:
                        default:
                            desiredFilterMode = FilterMode.Bilinear;
                            break;
                    }

                    switch (sampler.WrapS)
                    {
                        case GLTF.Schema.WrapMode.ClampToEdge:
                            desiredWrapMode = UnityEngine.TextureWrapMode.Clamp;
                            break;
                        case GLTF.Schema.WrapMode.Repeat:
                        default:
                            desiredWrapMode = UnityEngine.TextureWrapMode.Repeat;
                            break;
                    }
                }

                if (source.filterMode == desiredFilterMode && source.wrapMode == desiredWrapMode)
                {
                    _assetCache.TextureCache[texture.Source.Id] = source;
                }
                else
                {
                    var unityTexture = UnityEngine.Object.Instantiate(source);
                    unityTexture.filterMode = desiredFilterMode;
                    unityTexture.wrapMode = desiredWrapMode;
                    _assetCache.TextureCache[texture.Source.Id] = unityTexture;
                }

                bool isFlipped = _shaderCache.ContainsKey(MaterialType.CustomShader) ? this.UseTextureReverse : false;
                float resizeVal = UseTextureQuarterResolution ? 0.5f : 1.0f;
                UnityEngine.Texture resizedTexture = _assetCache.TextureCache[texture.Source.Id];
                Texture2D resized2DTexture = getTexture2D(resizedTexture, (int)(resizedTexture.width * resizeVal), (int)(resizedTexture.height * resizeVal), isFlipped);
                _assetCache.TextureCache[texture.Source.Id] = resized2DTexture;

            }

            return _assetCache.TextureCache[texture.Source.Id];
        }

        protected virtual void ApplyTextureTransform(TextureInfo def, UnityEngine.Material mat, string texName)
        {
            Extension extension;
            if (_root.ExtensionsUsed != null &&
                _root.ExtensionsUsed.Contains(ExtTextureTransformExtensionFactory.EXTENSION_NAME) &&
                def.Extensions != null &&
                def.Extensions.TryGetValue(ExtTextureTransformExtensionFactory.EXTENSION_NAME, out extension))
            {
                ExtTextureTransformExtension ext = (ExtTextureTransformExtension)extension;
                Vector2 temp = ext.Offset.ToUnityVector2();
                temp = new Vector2(temp.x, -temp.y);
                mat.SetTextureOffset(texName, temp);

                mat.SetTextureScale(texName, ext.Scale.ToUnityVector2());
            }
        }

        public static readonly string Base64StringInitializer = "^data:[a-z-]+/[a-z-]+;base64,";

        protected virtual IEnumerator LoadImage(string rootPath, Image image, int imageID)
        {
            if (_assetCache.ImageCache[imageID] == null)
            {
                Texture2D texture = null;
                if (image.Uri != null)
                {
                    var uri = image.Uri;

                    Regex regex = new Regex(Base64StringInitializer);
                    Match match = regex.Match(uri);
                    if (match.Success)
                    {
                        var base64Data = uri.Substring(match.Length);
                        var textureData = Convert.FromBase64String(base64Data);
                        texture = new Texture2D(0, 0);
                        texture.LoadImage(textureData);
                        texture.name = "Embedded";
                        //texture.name = uri;
                    }
                    else if (_loadType == LoadType.Url)
                    {
                        string texturePath = Path.Combine(rootPath, uri);
                        var www = UnityWebRequest.Get(texturePath);
                        www.downloadHandler = new DownloadHandlerTexture();

                        yield return www.SendWebRequest();

                        // HACK to enable mipmaps :(
                        var tempTexture = DownloadHandlerTexture.GetContent(www);
                        if (tempTexture != null)
                        {
                            texture = new Texture2D(tempTexture.width, tempTexture.height, tempTexture.format, true);
                            texture.name = texturePath;
                            texture.SetPixels(tempTexture.GetPixels());
                            texture.Apply(true);
                        }
                        else
                        {
                            Debug.Log(www.responseCode);
                            Debug.Log(", " + www.url);
                            texture = new Texture2D(16, 16);
                        }
                    }
                    else if (_loadType == LoadType.Stream)
                    {
                        var pathToLoad = Path.Combine(rootPath, uri);
                        try
                        {
                            var file = File.OpenRead(pathToLoad);

                            if (Path.GetExtension(pathToLoad).ToLower() == ".tga")
                            {
                                texture = LoadTGA(file);
                                texture.name = pathToLoad;
                                texture.Apply();
                            }
                            else
                            {
                                byte[] bufferData = new byte[file.Length];
                                file.Read(bufferData, 0, (int)file.Length);

                                texture = new Texture2D(0, 0, TextureCompressionType, false);
                                texture.name = pathToLoad;
                                texture.LoadImage(bufferData);
                            }
#if !WINDOWS_UWP
                            file.Close();
#else
						    file.Dispose();
#endif
                        }
                        catch
                        {
                            string message = "Required file: (" + pathToLoad + ") is missing or damaged.";
                            onImportError(false, message);
                            yield break;
                        }
                    }
                }
                else
                {
                    texture = new Texture2D(0, 0);
                    var bufferView = image.BufferView.Value;
                    var buffer = bufferView.Buffer.Value;
                    var data = new byte[bufferView.ByteLength];

                    var bufferContents = _assetCache.BufferCache[bufferView.Buffer.Id];
                    System.Buffer.BlockCopy(bufferContents, bufferView.ByteOffset, data, 0, data.Length);
                    texture.LoadImage(data);
                }

                _assetCache.ImageCache[imageID] = texture;
            }
        }

        public Texture2D LoadTGA(Stream TGAStream)
        {

            using (BinaryReader r = new BinaryReader(TGAStream))
            {
                r.BaseStream.Seek(12, SeekOrigin.Begin);

                short width = r.ReadInt16();
                short height = r.ReadInt16();
                int bitDepth = r.ReadByte();

                r.BaseStream.Seek(1, SeekOrigin.Current);

                Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, true);
                Color32[] pulledColors = new Color32[width * height];

                if (bitDepth == 32)
                {
                    for (int i = 0; i < width * height; i++)
                    {
                        byte red = r.ReadByte();
                        byte green = r.ReadByte();
                        byte blue = r.ReadByte();
                        byte alpha = r.ReadByte();

                        pulledColors[i] = new Color32(blue, green, red, alpha);

                    }
                }
                else if (bitDepth == 24)
                {
                    for (int i = 0; i < width * height; i++)
                    {
                        byte red = r.ReadByte();
                        byte green = r.ReadByte();
                        byte blue = r.ReadByte();

                        pulledColors[i] = new Color32(blue, green, red, 255);
                    }
                }
                else
                {
                    Debug.LogError("TGA texture had non 32/24 bit depth.");
                    tex = new Texture2D(0, 0);
                    return tex;
                }

                tex.SetPixels32(pulledColors);
                tex.Apply();
                return tex;

            }
        }

        /// <summary>
        /// Load the remote URI data into a byte array.
        /// </summary>
        protected virtual IEnumerator LoadBuffer(string sourceUri, GLTF.Schema.Buffer buffer, int bufferIndex)
        {
            if (buffer.Uri != null)
            {
                byte[] bufferData = null;
                var uri = buffer.Uri;

                Regex regex = new Regex(Base64StringInitializer);
                Match match = regex.Match(uri);
                if (match.Success)
                {
                    var base64Data = uri.Substring(match.Length);
                    bufferData = Convert.FromBase64String(base64Data);
                }
                else if (_loadType == LoadType.Url)
                {
                    var www = UnityWebRequest.Get(Path.Combine(sourceUri, uri));

                    yield return www.SendWebRequest();

                    bufferData = www.downloadHandler.data;
                }
                else if (_loadType == LoadType.Stream)
                {
                    var pathToLoad = Path.Combine(sourceUri, uri);
                    FileStream file = null;
                    try
                    {
                        file = File.OpenRead(pathToLoad);
                        bufferData = new byte[buffer.ByteLength];
                        file.Read(bufferData, 0, buffer.ByteLength);
                    }
                    catch (Exception ex)
                    {
                        onImportError(false, ex.Message);
                        yield break;
                    }
                    finally
                    {
                        if (file != null)
                        {
#if !WINDOWS_UWP
                            file.Close();
#else
					        file.Dispose();
#endif

                        }
                    }
                }

                _assetCache.BufferCache[bufferIndex] = bufferData;
            }
        }

        /// <summary>
        ///  Get the absolute path to a gltf uri reference.
        /// </summary>
        /// <param name="gltfPath">The path to the gltf file</param>
        /// <returns>A path without the filename or extension</returns>
        protected static string AbsoluteUriPath(string gltfPath)
        {
            var uri = new Uri(gltfPath);
            var partialPath = uri.AbsoluteUri.Remove(uri.AbsoluteUri.Length - uri.Query.Length - uri.Segments[uri.Segments.Length - 1].Length);
            return partialPath;
        }

        /// <summary>
        /// Get the absolute path a gltf file directory
        /// </summary>
        /// <param name="gltfPath">The path to the gltf file</param>
        /// <returns>A path without the filename or extension</returns>
        protected static string AbsoluteFilePath(string gltfPath)
        {
            var fileName = Path.GetFileName(gltfPath);
            var lastIndex = gltfPath.IndexOf(fileName);
            var partialPath = gltfPath.Substring(0, lastIndex);
            return partialPath;
        }

        private void LoadAnimations()
        {
            if (_root.Animations != null)
            {
                for (int i = 0; i < _root.Animations.Count; ++i)
                {
                    AnimationClip clip = new AnimationClip();
                    clip.wrapMode = UnityEngine.WrapMode.Loop;
                    clip.legacy = true;
                    LoadAnimation(_root.Animations[i], i, clip);
                    _animationClips.Add(clip);
                }
            }

            if (_animationClips.Count != 0)
            {
                //RuntimeAnimatorController runtimeAnimatorController = _lastLoadedScene.AddComponent<RuntimeAnimatorController>();
                //AnimatorOverrideController animatorOverrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
                //animatorOverrideController[_animationClips[0].name] = _animationClips[0];
                //animator.runtimeAnimatorController = animatorOverrideController as RuntimeAnimatorController;
                _lastLoadedScene.AddComponent<UnityEngine.Animation>();

                AnimationClip idleAnimation = null;
                UnityEngine.Animation anim = _lastLoadedScene.GetComponent<UnityEngine.Animation>();
                foreach (AnimationClip clip in _animationClips)
                {
                    if (clip.name.Equals("idle"))
                        idleAnimation = clip;
                    clip.legacy = true;
                    clip.wrapMode = UnityEngine.WrapMode.Loop;
                    anim.AddClip(clip, clip.name);
                }

                if (idleAnimation == null)
                    idleAnimation = _animationClips[0];

                anim.clip = idleAnimation;
                anim.playAutomatically = true;
                anim.Play();
            }
        }

        private void LoadAnimation(GLTF.Schema.Animation gltfAnimation, int index, AnimationClip clip)
        {
            clip.name = gltfAnimation.Name != null && gltfAnimation.Name.Length > 0 ? gltfAnimation.Name : "GLTFAnimation_" + index;
            for (int i = 0; i < gltfAnimation.Channels.Count; ++i)
            {
                AnimationChannel channel = gltfAnimation.Channels[i];
                addGLTFChannelDataToClip(gltfAnimation.Channels[i], clip);
            }

            clip.EnsureQuaternionContinuity();
        }

        private void addGLTFChannelDataToClip(GLTF.Schema.AnimationChannel channel, AnimationClip clip)
        {
            int animatedNodeIndex = channel.Target.Node.Id;
            if (!_importedObjects.ContainsKey(animatedNodeIndex))
            {
                Debug.Log("Node '" + animatedNodeIndex + "' found for animation, aborting.");
            }

            Transform animatedNode = _importedObjects[animatedNodeIndex].transform;

            //AnimationUtility can't be used at runtime
            //string nodePath = AnimationUtility.CalculateTransformPath(animatedNode, _lastLoadedScene.transform);

            string nodePath = CalculateTransformPath(animatedNode, _lastLoadedScene.transform);
            bool isStepInterpolation = channel.Sampler.Value.Interpolation != InterpolationType.LINEAR;

            byte[] timeBufferData = _assetCache.BufferCache[channel.Sampler.Value.Output.Value.BufferView.Value.Buffer.Id];
            float[] times = GLTFHelpers.ParseKeyframeTimes(channel.Sampler.Value.Input.Value, timeBufferData);

            if (channel.Target.Path == GLTFAnimationChannelPath.translation || channel.Target.Path == GLTFAnimationChannelPath.scale)
            {
                byte[] bufferData = _assetCache.BufferCache[channel.Sampler.Value.Output.Value.BufferView.Value.Buffer.Id];
                GLTF.Math.Vector3[] keyValues = GLTFHelpers.ParseVector3Keyframes(channel.Sampler.Value.Output.Value, bufferData);
                if (keyValues == null)
                    return;

                Vector3[] values = keyValues.ToUnityVector3();
                AnimationCurve[] vector3Curves = GLTFUtils.createCurvesFromArrays(times, values, isStepInterpolation, channel.Target.Path == GLTFAnimationChannelPath.translation);

                if (channel.Target.Path == GLTFAnimationChannelPath.translation)
                    GLTFUtils.addTranslationCurvesToClip(vector3Curves, nodePath, ref clip);
                else
                    GLTFUtils.addScaleCurvesToClip(vector3Curves, nodePath, ref clip);
            }
            else if (channel.Target.Path == GLTFAnimationChannelPath.rotation)
            {
                byte[] bufferData = _assetCache.BufferCache[channel.Sampler.Value.Output.Value.BufferView.Value.Buffer.Id];
                Vector4[] values = GLTFHelpers.ParseRotationKeyframes(channel.Sampler.Value.Output.Value, bufferData).ToUnityVector4();
                AnimationCurve[] rotationCurves = GLTFUtils.createCurvesFromArrays(times, values, isStepInterpolation);

                GLTFUtils.addRotationCurvesToClip(rotationCurves, nodePath, ref clip);
            }
            else if (channel.Target.Path == GLTFAnimationChannelPath.weights)
            {
                List<string> morphTargets = new List<string>();
                int meshIndex = _root.Nodes[animatedNodeIndex].Mesh.Id;

                //To do : need to check multiple morphtarget case (after setting up the push mode)
                for (int i = 0; i < _root.Meshes[meshIndex].Primitives[0].Targets.Count; ++i)
                {
                    morphTargets.Add(GLTFUtils.buildBlendShapeName(meshIndex, i));
                }

                byte[] bufferData = _assetCache.BufferCache[channel.Sampler.Value.Output.Value.BufferView.Value.Buffer.Id];
                float[] values = GLTFHelpers.ParseKeyframeTimes(channel.Sampler.Value.Output.Value, bufferData);
                AnimationCurve[] morphCurves = GLTFUtils.buildMorphAnimationCurves(times, values, morphTargets.Count);

                GLTFUtils.addMorphAnimationCurvesToClip(morphCurves, nodePath, morphTargets.ToArray(), ref clip);
            }
            else
            {
                Debug.Log("Unsupported animation channel target: " + channel.Target.Path);
            }
        }

        private string CalculateTransformPath(Transform target, Transform root)
        {
            if (target == null || root == null)
                return "";
            string path = "";

            List<string> pathList = new List<string>();
            Transform current = target;
            while (current != null)
            {
                //skip root
                if (current == root)
                    break;
                pathList.Add(current.name);
                current = current.parent;
            }

            for (int i = pathList.Count - 1; i >= 0; i--)
            {
                if (i != pathList.Count - 1)
                    path += '/';
                path += pathList[i];
            }

            return path;
        }
        public void Dispose()
        {
            _assetCache?.Dispose();
        }
    }
}