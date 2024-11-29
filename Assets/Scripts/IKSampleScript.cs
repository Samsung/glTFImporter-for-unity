using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IKSampleScript : MonoBehaviour
{
    public Transform lookTarget;
    public Transform grabTarget;

    private Animator anim;

    // Start is called before the first frame update
    private void Start()
    {
        anim = GetComponent<Animator>();
    }
    
    // Update is called once per frame
    void Update()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
    }

    private void OnAnimatorIK(int layerIndex)
    {
        // 오른손의 Position과 Rotation 
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1.0f);
        anim.SetIKPosition(AvatarIKGoal.RightHand, grabTarget.position);

        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0.1f);
        anim.SetIKRotation(AvatarIKGoal.RightHand, grabTarget.rotation);

        // 머리가 바라보는 위치
        anim.SetLookAtWeight(1.0f);
        anim.SetLookAtPosition(lookTarget.position);
    }

}
