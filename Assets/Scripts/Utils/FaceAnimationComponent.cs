using GLTFComponentFeature;
using UnityEngine;
using UnityGLTF;
using static UnityGLTF.GLTFComponent;

public class FaceAnimationComponent : MonoBehaviour
{


#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    [StringInList(typeof(StringInListDrawerHelper), "getFaceAnimationFilePathList")]
#endif
    public string FaceAnimationFilePath = "";

    private GLTFComponent gLTFComponent;

    // Start is called before the first frame update
    void Start()
    {
        gLTFComponent = GetComponent<GLTFComponent>();
        gLTFComponent.OnStatusChangedCallback += LoadOnFaceAnimation;
    }


    private void LoadOnFaceAnimation(LoadStatus status)
    {
        if (status.Equals(LoadStatus.DONE))
        {
            SetFaceAnimation(FaceAnimationFilePath);
        }
    }


    public void SetFaceAnimation(string filePath)
    {
        GameObject h = null;

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
            if (child.name.Equals("LoadNode"))
            {
                h = child.gameObject;
                break;
            }

        FaceAnimationLoader loader = new FaceAnimationLoader(filePath);

        AnimationClip clip = loader.CreateAnimationClip();
        var currentAnimation = h.GetComponent<UnityEngine.Animation>();
        if (currentAnimation != null)
        {
            Object.DestroyImmediate(currentAnimation);
        }

        UnityEngine.Animation faceAnimation = h.AddComponent<UnityEngine.Animation>();
        faceAnimation.AddClip(clip, "BlendAnimation");
        faceAnimation.clip = clip;
        faceAnimation.Play();
    }


}
