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
using System.Collections.Generic;
using UnityEngine;
using UnityGLTF;
using static UnityEngine.Object;



/// <summary>
/// 
/// </summary>
internal class AvatarMaterialCombiner
{
    /// <summary>
    /// The empty texture dic
    /// </summary>
        /// Combines the material.
        /// </summary>
        /// <param name="meshCombineOption">The material sources.</param>
        /// <param name="resolutionRatio">The resolution ratio.</param>
        /// <returns></returns>
        public static (Dictionary<Material, TextureInfoBase>, Material) CombineMaterial(List<Material> materialSources, CombineOption combineOption)
        {
            var (textureInfoBaseSet,
                materialTextureInfoDic) = MakeTextureInfoBases(materialSources, combineOption.textureResolutionRatio);

            var (combinedmainTexture,
                combinedNormalTexture,
                combinedAo_metallicRoughnessTexture) = AvatarTextureCombiner.Process(textureInfoBaseSet);

            return (materialTextureInfoDic, MakeCombinedMaterial(combinedmainTexture,
                combinedNormalTexture,
                combinedAo_metallicRoughnessTexture));
        }
        /// <summary>
        /// Makes the texture information bases.
        /// </summary>
        /// <param name="materialSources">The material sources.</param>
        /// <param name="resolutionRatio">The resolution ratio.</param>
        /// <returns></returns>
        private static (List<TextureInfoBase>, Dictionary<Material, TextureInfoBase>) MakeTextureInfoBases(List<Material> materialSources, CombineOption.TextureResolutionRatio resolutionRatio)
        {
            Dictionary<(Texture, (int, int)), Texture2D> textureDic = new Dictionary<(Texture, (int, int)), Texture2D>();
            Dictionary<(int, int), Texture2D> emptyTextureDic = new Dictionary<(int, int), Texture2D>();

            List<TextureInfoBase> textureInfoBaseSet = new List<TextureInfoBase>();
            Dictionary<Material, TextureInfoBase> materialTextureInfoDic = new Dictionary<Material, TextureInfoBase>();

            foreach (var materialSource in materialSources)
        {
            Texture mainTexture = UniversalRenderPipelineUtils.isURPProject ? materialSource.GetTexture("_BaseMap") : materialSource.GetTexture("baseColorSampler");
            Texture normalTexture = materialSource.GetTexture(UniversalRenderPipelineUtils.isURPProject? "_BumpMap" : "normalSampler");

                Texture Ao_metallicRoughnessTexture = materialSource.GetTexture(UniversalRenderPipelineUtils.isURPProject ? "_MetallicGlossMap" : "metallicRoughnessSampler");


                Vector2Int size = new Vector2Int(mainTexture.width / (int)resolutionRatio, mainTexture.height / (int)resolutionRatio);
                TextureInfoBase textureInfoBase = new TextureInfoBase();
                textureInfoBase.size = size;

                //Set mainTexture
                if (!textureDic.ContainsKey((mainTexture, (size.x, size.y))))
                    textureDic[(mainTexture, (size.x, size.y))] = AvatarTextureCombiner.GetResizedTexture2D(mainTexture, size, TextureFormat.RGBA32);
                textureInfoBase.mainTexture2D = textureDic[(mainTexture, (size.x, size.y))];

                //Set normalTexture
                if (normalTexture == null)
                {
                    if(emptyTextureDic.ContainsKey((size.x, size.y)))
                        emptyTextureDic[(size.x, size.y)] = AvatarTextureCombiner.MakeEmptyTexture(size);
                    Texture2D emptyTexture = emptyTextureDic[(size.x, size.y)];
                    normalTexture = textureDic[(emptyTexture, (size.x, size.y))] = emptyTexture;
                }
                else if (!textureDic.ContainsKey((normalTexture, (size.x, size.y))))
                    textureDic[(normalTexture, (size.x, size.y))] = AvatarTextureCombiner.GetResizedTexture2D(normalTexture, size);
                textureInfoBase.normalTexture2D = textureDic[(normalTexture, (size.x, size.y))];

                //Set Ao_metallicRoughnessTexture
                if (Ao_metallicRoughnessTexture == null)
                {
                    if (emptyTextureDic.ContainsKey((size.x, size.y)))
                        emptyTextureDic[(size.x, size.y)] = AvatarTextureCombiner.MakeEmptyTexture(size);
                    Texture2D emptyTexture = emptyTextureDic[(size.x, size.y)];
                    Ao_metallicRoughnessTexture = textureDic[(emptyTexture, (size.x, size.y))] = emptyTexture;
                }
                else if (!textureDic.ContainsKey((Ao_metallicRoughnessTexture, (size.x, size.y))))
                    textureDic[(Ao_metallicRoughnessTexture, (size.x, size.y))] = AvatarTextureCombiner.GetResizedTexture2D(Ao_metallicRoughnessTexture, size);
                textureInfoBase.Ao_metallicRoughnessTexture2D = textureDic[(Ao_metallicRoughnessTexture, (size.x, size.y))];



            if (UniversalRenderPipelineUtils.isURPProject)
            {
                textureInfoBase.mainTexture2D = GLTFSceneImporter.getTexture2D(textureInfoBase.mainTexture2D, textureInfoBase.mainTexture2D.width, textureInfoBase.mainTexture2D.height, true);

                textureInfoBase.normalTexture2D = GLTFSceneImporter.getTexture2D(textureInfoBase.normalTexture2D, textureInfoBase.normalTexture2D.width, textureInfoBase.normalTexture2D.height, true);

                textureInfoBase.Ao_metallicRoughnessTexture2D = GLTFSceneImporter.getTexture2D(textureInfoBase.Ao_metallicRoughnessTexture2D, textureInfoBase.Ao_metallicRoughnessTexture2D.width, textureInfoBase.Ao_metallicRoughnessTexture2D.height, true);
            }


                textureInfoBaseSet.Add(textureInfoBase);

                materialTextureInfoDic[materialSource] = textureInfoBase;
            }

            return (textureInfoBaseSet, materialTextureInfoDic);
        }

        /// <summary>
        /// Makes the combined material.
        /// </summary>
        /// <param name="combinedmainTexture">The combinedmain texture.</param>
        /// <param name="combinedNormalTexture">The combined normal texture.</param>
        /// <param name="combinedAo_metallicRoughnessTexture">The combined ao metallic roughness texture.</param>
        /// <returns></returns>
        private static Material MakeCombinedMaterial(Texture2D combinedmainTexture,
            Texture2D combinedNormalTexture,
            Texture2D combinedAo_metallicRoughnessTexture)
        {
            combinedmainTexture.Apply();
            combinedNormalTexture.Apply();
            combinedAo_metallicRoughnessTexture.Apply();

            if (UniversalRenderPipelineUtils.isURPProject)
                return UniversalRenderPipelineUtils.MakeCombinedURPMaterial(combinedmainTexture, combinedNormalTexture, combinedAo_metallicRoughnessTexture);
  

            Material combinedMat = new Material(Shader.Find("Custom/AssetPBR"));
            combinedMat.EnableKeyword("_NORMALMAP");

            combinedMat.SetTexture(Shader.PropertyToID("baseColorSampler"), combinedmainTexture);
            combinedMat.SetTexture(Shader.PropertyToID("normalSampler"), combinedNormalTexture);
            combinedMat.SetTexture(Shader.PropertyToID("metallicRoughnessSampler"), combinedAo_metallicRoughnessTexture);


            combinedMat.SetVector(Shader.PropertyToID("u_texcoord_offset"), new Vector2(0, 0.0f));

            combinedMat.SetVector(Shader.PropertyToID("u_basecolor_factor"), new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
            combinedMat.SetVector(Shader.PropertyToID("u_metallic_roughness_factor"), new Vector4(1.0f, 1.0f, 1.0f, 1.0f));
            combinedMat.SetVector(Shader.PropertyToID("u_emissive_factor"), new Vector4(0.0f, 0.0f, 0.0f, 0.0f));


            combinedMat.SetVector(Shader.PropertyToID("u_primitive_usage"), new Vector4(0, 0, 1, 0));
            combinedMat.SetVector(Shader.PropertyToID("u_sampler_usage"), new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
            combinedMat.SetVector(Shader.PropertyToID("u_blend_usage"), new Vector4(0, 0, 0, 0));
            return combinedMat;
        }

        /// <summary>
        /// Materials the combine verification.
        /// </summary>
        /// <param name="material">The material.</param>
        /// <param name="strictCheck">if set to <c>true</c> [strict check].</param>
        /// <returns></returns>
        public static bool MaterialCombineVerification(Material material, bool strictCheck = false)
        {
        Texture baseColorTexture = UniversalRenderPipelineUtils.isURPProject?material.GetTexture("_BaseMap"): material.GetTexture("baseColorSampler");
            if (baseColorTexture == null || material.renderQueue == 3000)
                return false;
        return true;
        }
        /// <summary>
        /// Copies the new material.
        /// </summary>
        /// <param name="material">The material.</param>
        /// <returns></returns>
        public static Material CopyNewMaterial(Material material, CombineOption CombineOption = null)
        {


            int resolutionRatio = CombineOption == null ? 1: (int)CombineOption.textureResolutionRatio;


            if (UniversalRenderPipelineUtils.isURPProject)
            {
                return UniversalRenderPipelineUtils.CopyNewURPMaterial(material, resolutionRatio);
            }
            Material newMaterial = Instantiate(material);

            Texture mainTexture = null;
            Texture normalTexture = null;
            Texture Ao_metallicRoughnessTexture = null;

 

            mainTexture = material.GetTexture(Shader.PropertyToID("baseColorSampler"));
            normalTexture = material.GetTexture(Shader.PropertyToID("normalSampler"));
            Ao_metallicRoughnessTexture = material.GetTexture(Shader.PropertyToID("metallicRoughnessSampler"));
            //if (Ao_metallicRoughnessTexture == null)
            //    Ao_metallicRoughnessTexture = material.GetTexture("occlusionTexture");

            newMaterial.EnableKeyword("_NORMALMAP");

            mainTexture = mainTexture!=null? AvatarTextureCombiner.GetResizedTexture2D(mainTexture, new Vector2Int(mainTexture.width / resolutionRatio, mainTexture.height / resolutionRatio), TextureFormat.RGBA32) : null;
            normalTexture = normalTexture != null ? AvatarTextureCombiner.GetResizedTexture2D(normalTexture, new Vector2Int(normalTexture.width / resolutionRatio, normalTexture.height / resolutionRatio), TextureFormat.RGBA32) : null;
            Ao_metallicRoughnessTexture = Ao_metallicRoughnessTexture != null ? AvatarTextureCombiner.GetResizedTexture2D(Ao_metallicRoughnessTexture, new Vector2Int(Ao_metallicRoughnessTexture.width / resolutionRatio, Ao_metallicRoughnessTexture.height / resolutionRatio), TextureFormat.RGBA32) : null;


            if (mainTexture != null)
                newMaterial.SetTexture(Shader.PropertyToID("baseColorSampler"), mainTexture);
            if (normalTexture != null)
                newMaterial.SetTexture(Shader.PropertyToID("normalSampler"), normalTexture);
            if (Ao_metallicRoughnessTexture != null)
                newMaterial.SetTexture(Shader.PropertyToID("metallicRoughnessSampler"), Ao_metallicRoughnessTexture);

        return newMaterial;
        }
    }




