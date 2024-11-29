using System;
using UnityEngine;

namespace GLTFComponentFeature
{
    [Serializable]
    public class AnimatorFeature
    {
        [SerializeField] public bool useLegacyAnimator;

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("useLegacyAnimator", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        [SerializeField] public bool useTransitionAnimator;

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("useLegacyAnimator", true, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        [SerializeField] public RuntimeAnimatorController legacyAnimatorController;

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("useLegacyAnimator", false, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        [SerializeField] public RuntimeAnimatorController humanoidAnimatorController;

#if (UNITY_EDITOR || UNITY_STANDALONE_WIN)
        [DrawIf("useTransitionAnimator", true, DrawIfAttribute.DisablingType.DontDraw)]
#endif
        [SerializeField] public HumanoidAnimation humanoidAnimationList;
        public enum HumanoidAnimation{
            Idle,
            GangNam_Style,
            Capoeira,
            Falling,
            Angry
        }
    }
}