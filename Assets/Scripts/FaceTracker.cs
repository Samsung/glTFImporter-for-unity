
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.UI.GridLayoutGroup;
using GLTFComponentFeature;

public class FaceTracker
{
    public List<SkinnedMeshRenderer> meshRendereList = new List<SkinnedMeshRenderer>();
    public Dictionary<string, int> blendShapeNameToIndexDic = new Dictionary<string, int>();
    public const int FACE_TRACKING_INTERVAL_MS = 66;

    public FaceTracker(GameObject gameObject)
    {
        initSxrRuntime();
        findHeadMeshNode(gameObject);
    }

    private void initSxrRuntime(){
        // add when sxr perception lib ready
    }

    private void deinitSxrRuntime(){
        // add when sxr perception lib ready
    }

    public FaceTrackingData getFaceTrackingData()
    {
        // add when sxr perception lib ready
        FaceTrackingData data = null;
        return data;
    }

    public IEnumerator Play(){
        while(true){
            yield return new WaitForSeconds(FACE_TRACKING_INTERVAL_MS * 0.001f);
            FaceTrackingData data = getFaceTrackingData();

            foreach(SkinnedMeshRenderer meshRenderer in meshRendereList){
                Mesh mesh = meshRenderer.sharedMesh;
                for(int i=0;i<mesh.blendShapeCount;i++){
                    String name = mesh.GetBlendShapeName(i);
                    // meshRenderer.SetBlendShapeWeight(blendShapeNameDic[name], data.morphWeight[blendShapeNameDic[name]]);
                }
            }
        }
    }
    private void initializeBlendShapeNameDic(Mesh mesh){
        for (int i = 0; i < 68; i++)
        {
            blendShapeNameToIndexDic.Add(mesh.GetBlendShapeName(i),i);
        }
    }
    private void findHeadMeshNode(GameObject node)
    {
        SkinnedMeshRenderer nodeMeshRenderer = node.GetComponent<SkinnedMeshRenderer>();
        if (nodeMeshRenderer != null)
        {
            Mesh mesh = nodeMeshRenderer.sharedMesh;
            if (mesh.blendShapeCount != 0)
            {
                meshRendereList.Add(nodeMeshRenderer);
            }

            if (nodeMeshRenderer.name.Equals("head_GEO")){
                initializeBlendShapeNameDic(mesh);
            }
        }
        for (int i = 0; i < node.transform.childCount; i++)
            findHeadMeshNode(node.transform.GetChild(i).gameObject);
    }
}
