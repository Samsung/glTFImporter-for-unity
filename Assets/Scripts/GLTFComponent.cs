using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using GLTFComponentFeature;
using Object = UnityEngine.Object;
using static CombineOption;

namespace UnityGLTF
{
    /// <summary>
    /// Component to load a GLTF scene with
    /// </summary>
    public class GLTFComponent : MonoBehaviour
    {
        public enum LoadStatus
        {
            NOTSTARTED,
            INPROGRESS,
            DONE
        }

        public event Action<LoadStatus> OnStatusChangedCallback;

        [SerializeField] private AnimatorFeature animatorFeature;
        [Space(5)]
        [SerializeField] private GLTFLoadFeature gltfLoadFeature;
        [SerializeField] private FaceFeature faceFeature;
        [Space(5)]
        [SerializeField] public ShaderFeature shaderFeature;
        [SerializeField] public CombineOption combineFeature;

        [Space(5)]
        public Bounds AvatarBounds = new Bounds();
        public List<ShaderBindHelper> ShaderBindHelperList { get; private set; }
        public GLTF.Schema.GLTFRoot root
        {
            get
            {
                return mRoot;
            }
        }

        public bool IsAsset
        {
            get
            {
                return mIsAsset;
            }
        }

        public bool addColliders = false;
        public bool IsLoadFailed;

        Action<bool, string> onFinishAction;
        private Animator _animator;
        private GameObject loadNode;
        private GLTFSceneImporter _loader;
        private AREmojiBoneConstructor aremojiBoneConstructor = null;
        //private Rotator mRotator;
        private GLTF.Schema.GLTFRoot mRoot;
        //private AvatarControl mAvatarControl = null;
        private static bool mIsAsset; // inited when shader is selected

        private string fullPath;
        private LoadStatus loadstatus = LoadStatus.NOTSTARTED;

        void Awake()
        {
            Debug.Log("[profiling] Awake : assetLocation = " + gltfLoadFeature.assetLocation + ", loadType = " + gltfLoadFeature.loadType + ", Texture Compression Type = " + shaderFeature.TextureCompressionType);
            if (gltfLoadFeature.Url != null)
            {
                Debug.Log("[profiling] Url = " + gltfLoadFeature.Url);
                switch (gltfLoadFeature.assetLocation)
                {
                    case GLTFLoadFeature.AssetLocation.StreamingAsset: fullPath = getStreamingAssetFullPath(); break;
                    case GLTFLoadFeature.AssetLocation.Else: fullPath = getElseAssetLocationFullPath(); break;
                    case GLTFLoadFeature.AssetLocation.Server: fullPath = gltfLoadFeature.Url; break;
                }

                // load on start up
                if (gltfLoadFeature.loadOnStartUp)
                {
                    if (gltfLoadFeature.assetLocation == GLTFLoadFeature.AssetLocation.Server)
                    {
                        gltfLoadFeature.loadType = GLTFSceneImporter.LoadType.Url; // url load type is only allowed when loading asset from server.
                    }
                    StartCoroutine(loadGLTF());
                }
            }
        }

        void Start()
        {

        }

        private string getStreamingAssetFullPath(){
            string path = string.Empty;
            Debug.Log("[profiling] platform : " + Application.platform);
            if (Application.platform == RuntimePlatform.Android)
            {
                gltfLoadFeature.loadType = GLTFSceneImporter.LoadType.Url; // you can only read file with load type url in Android platform Streaming Asset.
                path = "jar:file://" + Application.dataPath + "!/assets" + gltfLoadFeature.Url;
                Debug.Log("[profiling] Android Platform StreamingAssets Load file = " + path);
                //jar:file:///data/app/~~s_lJw1yShVLHNh89ZFDxkA==/com.samsung.aremojiimporter-ICA4Lm2bzTruXNhsJ4BhrQ==/base.apk!/assets/Basemodel_female/model_external.gltf
            }
            else if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.VisionOS)
            {
                path = Application.streamingAssetsPath + gltfLoadFeature.Url;
                Debug.Log("[profiling] OSXEditor IPhonePlayer VisionOS StreamingAssets Load file : " + path);
            }
            else if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                path = Application.streamingAssetsPath + gltfLoadFeature.Url;
                path = (gltfLoadFeature.loadType == GLTFSceneImporter.LoadType.Url) ? "file://" + path : path; // Url load type need prefix "file://"
                Debug.Log("[profiling] WindowsEditor StreamingAssets Load file : " + path);
                //file://C:/GalaxyAREmojiSDKforUnity/Assets/StreamingAssets/Basemodel_female/model_external.gltf
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                path = Application.streamingAssetsPath + gltfLoadFeature.Url;
                //Application.streamingAssetsPath : http://localhost:53634/StreamingAssets
                Debug.Log("[profiling] WebGLPlayer StreamingAssets Load file : " + path);
            }
            else
            {
                path = Application.dataPath + "/Raw" + gltfLoadFeature.Url;
                Debug.Log("[profiling] StreamingAssets Load file : " + path);

            }

            return path;
        }

        private string getElseAssetLocationFullPath(){
            string path = string.Empty;
            path = (gltfLoadFeature.loadType == GLTFSceneImporter.LoadType.Url) ? "file://" + gltfLoadFeature.Url : gltfLoadFeature.Url;
            return path;
        }

        private void setSceneImporter(FileStream gltfStream, string fullPath){
            Debug.Log("[profiling] setSceneImporter : loadType = " + gltfLoadFeature.loadType);

            if (gltfLoadFeature.loadType == GLTFSceneImporter.LoadType.Url)
            {
                _loader = new GLTFSceneImporter(
                    fullPath,
                    null,
                    GLTFSceneImporter.LoadType.Url,
                    loadNode.transform,
                    addColliders
                );

            } else {
                Debug.Log("[profiling] before gltfStream read path : " + fullPath);
                gltfStream = File.OpenRead(fullPath);
                Debug.Log("[profiling] after gltfStream read");
                _loader = new GLTFSceneImporter(
                    fullPath,
                    gltfStream,
                    GLTFSceneImporter.LoadType.Stream,
                    loadNode.transform,
                    addColliders
                );
            }
        }

        public void SelectShader(bool isAsset)
        {
            mIsAsset = isAsset;
            shaderFeature.GLTFStandard = isAsset ? shaderFeature.AssetStandardShader : shaderFeature.CharacterStandardShader;
        }

        private void onError(bool success, string message)
        {
            IsLoadFailed = true;
            Debug.Log("[profiling] Load file error : " + message);
            //onFinishAction(success, message);
        }

        private void InitLoadNode()
        {
            if (loadNode != null)
            {
                DestroyImmediate(loadNode);
                loadNode = null;
                aremojiBoneConstructor = null;
            }
        }

        private GameObject MakeSubNode(string name, bool isActive, Transform parent)
        {
            GameObject subNode = new GameObject(name);
            subNode.transform.parent = parent;
            subNode.transform.localPosition = Vector3.zero;
            subNode.transform.localEulerAngles = Vector3.zero;
            subNode.transform.localScale = Vector3.one;
            subNode.SetActive(isActive);
            return subNode;
        }

        public IEnumerator loadGLTF(string filePath){
            Debug.Log("[profiling] loadGLTF(For Deprecated) : filePath = " + filePath);
            gltfLoadFeature.Url = filePath;
            gltfLoadFeature.loadType = GLTFSceneImporter.LoadType.Url;
            gltfLoadFeature.assetLocation = GLTFLoadFeature.AssetLocation.Else;
            fullPath = getElseAssetLocationFullPath();
            yield return loadGLTF();
        }

        public IEnumerator loadGLTFWithFilePath(string filePath, bool isStreamingAsset, int loadTypeIndex)
        {
            Debug.Log("[profiling] loadGLTFWithFilePath : filePath = " + filePath);
            gltfLoadFeature.Url = filePath;
            gltfLoadFeature.assetLocation = isStreamingAsset ? GLTFLoadFeature.AssetLocation.StreamingAsset : GLTFLoadFeature.AssetLocation.Else;
            gltfLoadFeature.loadType = (GLTFSceneImporter.LoadType) loadTypeIndex;
            Debug.Log("[profiling] loadGLTFWithFilePath : loadType = " + gltfLoadFeature.loadType);
            fullPath = (gltfLoadFeature.assetLocation == GLTFLoadFeature.AssetLocation.StreamingAsset) ? getStreamingAssetFullPath() : getElseAssetLocationFullPath();
            Debug.Log("[profiling] loadGLTFWithFilePath : fullPath = " + fullPath);
           yield return loadGLTF();
        }

        public IEnumerator loadGLTFFromServerUrl(string url)
        {
            Debug.Log("[profiling] loadGLTFFromServerUrl : url = " + url);
            fullPath = url;
            gltfLoadFeature.loadType = GLTFSceneImporter.LoadType.Url;
            yield return loadGLTF();

        }

        private void ChangeStatus(LoadStatus status) 
        {
            loadstatus = status;
            OnStatusChangedCallback?.Invoke(status);
        }

        private IEnumerator loadGLTF()
        {
            yield return new WaitUntil(() => loadstatus != LoadStatus.INPROGRESS);
            ChangeStatus(LoadStatus.INPROGRESS);

            InitLoadNode();
            loadNode = MakeSubNode("LoadNode",false,null);

            if (fullPath == null){
                Debug.Log("[profiling] full path is null");
                ChangeStatus(LoadStatus.NOTSTARTED);
                yield break;
            }

            Debug.Log("[profiling] Load start");
            IsLoadFailed = false;

            // transform.localScale = new Vector3(0.009894463f, 0.009894463f, 0.009894463f);
            // transform.localScale = new Vector3(0.009894463f, 0.009894463f, 0.009894463f);
            //onFinishAction = onFinish;
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            fullPath = new System.Uri(fullPath).LocalPath;
#endif

            FileStream gltfStream = null;
            _loader?.Dispose();
            //fullPath = Application.streamingAssetsPath + Url;
            Debug.Log("[profiling] before load");
            try
            {
                setSceneImporter(gltfStream, fullPath);
            }
            catch (Exception ex)
            {
                Debug.Log("[profiling] Load file error");
                onError(false, "GLTFComponent load failded : " + ex.Message);
                ChangeStatus(LoadStatus.NOTSTARTED);
                yield break;
            }

            setShaderProperties();

            // TODO: Finalize model validation (for ex., buffer's presence)
            yield return _loader.Load(onError, -1, gltfLoadFeature.Multithreaded);
            if (gltfStream != null)
            {
#if WINDOWS_UWP
				gltfStream.Dispose();
#else
                gltfStream.Close();
#endif
            }

            if (IsLoadFailed)
            {
                ChangeStatus(LoadStatus.NOTSTARTED);
                yield break;
            }

            mRoot = _loader.root;

            onFinishAction?.Invoke(true, fullPath);

            loadNode.transform.parent = transform;
            loadNode.transform.localScale = Vector3.one * 0.01f;

            //Set Bone Constructor
            aremojiBoneConstructor = new AREmojiBoneConstructor(gameObject);

            if(combineFeature.UseMeshCombiner)
            {
                SetCombinedMesh();
                if (combineFeature.combineFlags.HasFlag(CombineOption.CombineFlags.RemoveTargetMeshes))
                {
                    _loader.Dispose();
                    _loader = null;
                }
            }

            if (faceFeature.UseBlendShape)
            {
                loadNode.AddComponent<AvatarBlendshapeDriver>().InitBlendshapeDriver();
            }

            AddAnimator();
            loadNode.SetActive(true);

            ChangeStatus(LoadStatus.DONE);
        }
        private void SetCombinedMesh()
        {
            Transform hips_JNT = null;
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {

                if (AvatarConstructor.boneStructure.ContainsValue(child.name))
                {
                    if (child.name.Equals("hips_JNT"))
                    {
                        hips_JNT = child;
                        break;
                    }
                }
            }


            var (combineTargetMeshSet1, combineTargetMeshSet2) = InitAvatarMeshSet();

            MakeCombinedMesh(hips_JNT, combineTargetMeshSet1, combineTargetMeshSet2);
        }

        private void MakeCombinedMesh(Transform hips_JNT, List<SkinnedMeshRenderer> combineTargetMeshSet1, List<SkinnedMeshRenderer> combineTargetMeshSet2)
        {
            if (combineFeature.combineFlags.HasFlag(CombineOption.CombineFlags.SeparateHeadBody))
            {
                MakeCombinedMesh(hips_JNT, combineTargetMeshSet1, "CombinedHeadMesh");
                MakeCombinedMesh(hips_JNT, combineTargetMeshSet2, "CombinedBodyMesh");
            }
            else
                MakeCombinedMesh(hips_JNT, combineTargetMeshSet1, "combinedMesh");
        }

        private SkinnedMeshRenderer MakeCombinedMesh(Transform hips_JNT, List<SkinnedMeshRenderer> combineTargetMeshSet, string name)
        {
            GameObject combinedMeshObject = MakeSubNode(name, true, loadNode.transform);
            SkinnedMeshRenderer combinedMesh = combinedMeshObject.AddComponent<SkinnedMeshRenderer>();
            combinedMesh.rootBone = hips_JNT;
            Transform avatarCompTransform = loadNode.transform.parent;
            loadNode.SetActive(false);
            loadNode.transform.parent = null;
            loadNode.transform.localScale = Vector3.one * 0.01f;
            AvatarMeshCombiner.CombineSkinnedMesh(combineTargetMeshSet.ToArray(), combinedMesh, combineFeature);
            if (!UniversalRenderPipelineUtils.isURPProject)
                combinedMesh.gameObject.AddComponent<ShaderBindHelper>();

            loadNode.transform.parent = avatarCompTransform;
            loadNode.transform.localScale = Vector3.one * 0.01f;
            loadNode.SetActive(true);
            return combinedMesh;
        }

        private (List<SkinnedMeshRenderer>, List<SkinnedMeshRenderer>) InitAvatarMeshSet()
        {

            List<SkinnedMeshRenderer> combineTargetMeshSet1 = new List<SkinnedMeshRenderer>();
            List<SkinnedMeshRenderer> combineTargetMeshSet2 = new List<SkinnedMeshRenderer>();

            Renderer[] allChildren = loadNode.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer child_ in allChildren)
            {

                if (!AvatarMaterialCombiner.MaterialCombineVerification(child_.material) || child_.name.Contains("iris_") || child_.name.Contains("cornea_"))
                {
                    if (combineFeature.combineFlags.HasFlag(CombineOption.CombineFlags.RemoveTargetMeshes))
                        AvatarMeshCombiner.InstantiateRendererParms(child_);
                }
                else
                {
                    if (combineFeature.combineFlags.HasFlag(CombineOption.CombineFlags.SeparateHeadBody))
                    {
                        if (IsContainedHeadNode(child_.transform) && child_ is SkinnedMeshRenderer)
                            combineTargetMeshSet1.Add((SkinnedMeshRenderer)child_);
                        else if (child_ is SkinnedMeshRenderer)
                            combineTargetMeshSet2.Add((SkinnedMeshRenderer)child_);
                    }
                    else if (child_ is SkinnedMeshRenderer)
                        combineTargetMeshSet1.Add((SkinnedMeshRenderer)child_);
                }
            }
            return (combineTargetMeshSet1, combineTargetMeshSet2);
        }

        private bool IsContainedHeadNode(Transform node)
        {
            if (AvatarConstructor.INCLUDED_HEADONLY_PARENTS.Contains(node.name))
                return true;
            else if (node.parent != null)
                return IsContainedHeadNode(node.parent);
            else
                return false;
        }

        private void setShaderProperties()
        {
            _loader.SetTextureBIS(shaderFeature.brdf, shaderFeature.IBLIrradiance, shaderFeature.IBLSpecular);

            if (!shaderFeature.UseCustomShader)
            {
                _loader.SetShaderForMaterialType(GLTFSceneImporter.MaterialType.PbrMetallicRoughness, shaderFeature.GLTFStandard);
                _loader.SetShaderForMaterialType(GLTFSceneImporter.MaterialType.KHR_materials_pbrSpecularGlossiness, shaderFeature.GLTFStandardSpecular);
                _loader.SetShaderForMaterialType(GLTFSceneImporter.MaterialType.CommonConstant, shaderFeature.GLTFConstant);
                ShaderBindHelperList = _loader.getShaderBindHelperList();
            }
            else
            {
                _loader.SetShaderForMaterialType(GLTFSceneImporter.MaterialType.CustomShader, shaderFeature.CustomShader);
                _loader.SetCustomShaderPropertys(shaderFeature.CustomShaderPropertyNames, shaderFeature.UseTextureReverse);
            }
            _loader.SetUseBlendShape(faceFeature.UseBlendShape);
            _loader.SetTextureQuarterResolution(shaderFeature.UseTextureQuarterResolution);
            _loader.SetTextureCompressionType(shaderFeature.TextureCompressionType);
            _loader.MaximumLod = shaderFeature.MaximumLod;
        }

        private void AddAnimator()
        {
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<Animator>();
            }
            if (animatorFeature.useLegacyAnimator)
            {
               /* GameObject o = GLTFUtils.getChildrenGameObject(gameObject, "rig_GRP");
                _animator = o.AddComponent<Animator>();*/
                _animator.runtimeAnimatorController = animatorFeature.legacyAnimatorController;
                _animator.Rebind();

            }
            else
            {
                _animator.runtimeAnimatorController = animatorFeature.humanoidAnimatorController;
                _animator.enabled = false;
                _animator.avatar = new AvatarConstructor(gameObject).ConstructAvatar();
                _animator.Rebind();
                _animator.enabled = true;
            }
            int animationIndex = (int)animatorFeature.humanoidAnimationList - 1;
            Debug.Log("[profiling] selected body animation index : " + animationIndex);
            _animator.SetInteger("animationInteger", animationIndex); // set idle animation
        }

        public static void DestroyImmediateNode(GameObject node)
        {
            DestroyImmediate(node);
        }

        public void ApplyIdleAnimation(int clipIndex)
        {
            if (clipIndex == 0)
                return;

            swapName(0, clipIndex);
        }

        public void RestoreIdleAnimation(int clipIndex)
        {
            swapName(clipIndex, 0);
        }

        private void swapName(int from, int to)
        {
            string idleName = mRoot.Animations[from].Name;
            string clipName = mRoot.Animations[to].Name;

            mRoot.Animations[from].Name = clipName;
            mRoot.Animations[to].Name = idleName;
        }

        private void playFaceTracking(){
            FaceTracker faceTracker = new FaceTracker(gameObject);
            StartCoroutine(faceTracker.Play());   
        }

        public void applyHumanoidAnimation(int animationId)
        {
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<Animator>();
            }
            _animator.SetInteger("animationInteger", animationId - 1);
        }
        
        public List<string> getHumanoidAnimationList(){
            return Enum.GetNames(typeof(AnimatorFeature.HumanoidAnimation)).ToList();
        }

        public LoadStatus getLoadStatus(){
           return loadstatus; 
        }
    }
}
