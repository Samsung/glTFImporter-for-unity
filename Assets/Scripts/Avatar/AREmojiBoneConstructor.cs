using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using UnityGLTF;
using static Constance;

/// <summary>
/// 
/// </summary>
/// <seealso cref="UnityEngine.MonoBehaviour" />
public class AREmojiBoneConstructor
{
    /// <summary>
    /// The male body
    /// </summary>
    private static string MaleBody = "asian_adult_male_GRP";
    /// <summary>
    /// The junior body
    /// </summary>
    private static string JuniorBody = "asian_junior_male_GRP";
    /// <summary>
    /// The body type
    /// </summary>
    private BodyType bodyType = BodyType.Female;
    /// <summary>
    /// The body string
    /// </summary>
    private string bodyStr = "female";
    /// <summary>
    /// The aremoji bone tamplate
    /// </summary>
    private Dictionary<string, Dictionary<string, Dictionary<string, List<float>>>> aremojiBoneTamplate;
    /// <summary>
    /// The load node
    /// </summary>
    private GameObject loadNode;
    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarBoneComposer" /> class.
    /// </summary>
    /// <param name="loadNode">The load node.</param>
    public AREmojiBoneConstructor(GameObject loadNode)
    {
        this.loadNode = loadNode;
        this.aremojiBoneTamplate = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, List<float>>>>>((Resources.Load("avatarDefaultRig") as TextAsset).text);

        ReDefineNodeStructure();
        SetAREmojiDefaultRig(GLTFUtils.getChildrenGameObject(loadNode, HIP_JNT).transform);
    }

    /// <summary>
    /// Res the define node structure.
    /// </summary>
    private void ReDefineNodeStructure()
    {
        Transform head_GRP = null;
        Transform model = null;
        Transform rootNode = null;
        Transform[] allChildren = loadNode.GetComponentsInChildren<Transform>(true);

        foreach (Transform child_ in allChildren)
        {
            if (child_.name.Equals(HEAD_GRP))
                head_GRP = child_;
            else if (child_.name.Equals(MODEL))
                model = child_;
            else if (child_.name.Equals(ROOT_NODE))
                rootNode = child_;
            else if (child_.name.Equals(MaleBody) || child_.name.Equals(JuniorBody))
            {
                bodyType = child_.name.Equals(MaleBody) ? BodyType.Male : BodyType.Junior;
                bodyStr = bodyType.Equals(BodyType.Male) ? "male" : "junior";
                Debug.Log("bodyType : " + bodyStr);
            }
        }
        if (!head_GRP.parent.name.Equals(MODEL))
            head_GRP.parent = model;
        Transform mayDestroyedNode = rootNode.GetChild(0);
        if (mayDestroyedNode.name != MODEL_GRP && mayDestroyedNode.name != RIG_GRP)
        {
            List<Transform> chilren = new List<Transform>();
            for (int idx = 0; idx < mayDestroyedNode.childCount; idx++)
                chilren.Add(mayDestroyedNode.GetChild(idx));
            foreach (var child in chilren)
                child.parent = mayDestroyedNode.parent;
            GLTFComponent.Destroy(mayDestroyedNode.gameObject);
        }
    }

    /// <summary>
    /// The setAREmojiDefaultRig.
    /// </summary>
    /// <param name="node">The node<see cref="Transform" />.</param>
    private void SetAREmojiDefaultRig(Transform node)
    {
        if (!aremojiBoneTamplate[bodyStr].ContainsKey(node.name))
            return;

        List<float> pos = aremojiBoneTamplate[bodyStr][node.name]["m_LocalPosition"];
        List<float> rot = aremojiBoneTamplate[bodyStr][node.name]["m_LocalRotation"];
        List<float> scl = aremojiBoneTamplate[bodyStr][node.name]["m_LocalScale"];
        node.transform.localPosition = new Vector3(-pos[0], pos[1], -pos[2]);
        node.transform.localEulerAngles = new Vector3(-rot[0], rot[1], -rot[2]);
        //node.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

        List<Transform> children = new List<Transform>();
        for (int idx = 0; idx < node.childCount; idx++)
            children.Add(node.GetChild(idx));

        foreach (Transform child in children)
        {
            SetAREmojiDefaultRig(child);
        }
    }

    /// <summary>
    /// Gets the constructed avatar.
    /// </summary>
    /// <returns></returns>
    public Avatar GetConstructedAvatar()
    {
        return new AvatarConstructor(loadNode).ConstructAvatar();
    }
}


