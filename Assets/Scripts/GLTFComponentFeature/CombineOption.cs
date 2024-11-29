/* ****************************************************************
 *
 * Copyright 2023 Samsung Electronics All Rights Reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 ******************************************************************/
using System;


/// <summary>
/// 
/// </summary>
[Serializable]
public class CombineOption
{
    /// <summary>
    /// 
    /// </summary>
    [Flags]
    public enum CombineFlags : byte
    {
        /// <summary>
        /// The none
        /// </summary>
        None = 0,
        /// <summary>
        /// The remove blendshapes
        /// </summary>
        RemoveBlendshapes = 1,
        /// <summary>
        /// The remove target meshes
        /// </summary>
        RemoveTargetMeshes = 2,
        /// <summary>
        /// The include material combine
        /// </summary>
        IncludeTextureAtlasing = 4,
        /// <summary>
        /// The separate head body
        /// </summary>
        SeparateHeadBody = 8,
    }
    /// <summary>
    /// 
    /// </summary>
    public enum TextureResolutionRatio : int
    {
        /// <summary>
        /// The one
        /// </summary>
        One = 1,
        /// <summary>
        /// The half
        /// </summary>
        Half = 2,
        /// <summary>
        /// The quarter
        /// </summary>
        Quarter = 4,
        /// <summary>
        /// The eighth
        /// </summary>
        Eighth = 8,
    }

    public bool UseMeshCombiner = true;
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        [EnableIf("UseMeshCombiner", "combineFlags")]
#endif
    /// <summary>
    /// The combine mesh flags
    /// </summary>
    public CombineFlags combineFlags = CombineFlags.RemoveTargetMeshes | CombineFlags.IncludeTextureAtlasing;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        [EnableIf("UseMeshCombiner", "textureResolutionRatio")]
#endif
    public TextureResolutionRatio textureResolutionRatio = TextureResolutionRatio.One;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        [EnableIf("UseMeshCombiner", "TextureAtlasOptimization")]
#endif
    public bool TextureAtlasOptimization = true;
}

