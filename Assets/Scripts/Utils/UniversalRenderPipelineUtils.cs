using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF;

public class UniversalRenderPipelineUtils
{

    public static bool isURPProject { 
        get {
            RenderPipelineAsset renderPipelineAsset = GraphicsSettings.defaultRenderPipeline;
            if (renderPipelineAsset == null)
                return false;
            return renderPipelineAsset.GetType().Name.Equals("UniversalRenderPipelineAsset"); 
        } 
    }

    public static Material ChangeToURPMaterial(Material material, string name = "")
    {

        Material urpMat = null;
            
        Texture mainTex = material.GetTexture("baseColorSampler");

        if (name.Contains("cornea_") || mainTex == null)
            urpMat = Object.Instantiate(Resources.Load("URPResources/URPMat-Transparent", typeof(Material)) as Material);
        
        else
            urpMat = Object.Instantiate(Resources.Load("URPResources/URPMat-Common", typeof(Material)) as Material);
        

        Color baseColor = material.GetColor("u_basecolor_factor");
        urpMat.SetColor("_BaseColor", baseColor);

        if (mainTex != null)
            urpMat.SetTexture("_BaseMap", GLTFSceneImporter.getTexture2D(mainTex, mainTex.width, mainTex.height, true));
        

        //set bumpMap, metallicGlossMap Texture
        Texture bumpMap = material.GetTexture("normalSampler");

        if (bumpMap != null)
            urpMat.SetTexture("_BumpMap", GLTFSceneImporter.getTexture2D(bumpMap, bumpMap.width, bumpMap.height, true));
        

        Texture metallicGlossMap = material.GetTexture("metallicRoughnessSampler");
        if (metallicGlossMap != null)
        {

            Texture2D tex2D = (Texture2D)metallicGlossMap;

            if (tex2D.isReadable)
            {
                Texture2D aoMetallicRoughness = new Texture2D(tex2D.width, tex2D.height, TextureFormat.RGBA32, false);
                for (int i = 0; i < tex2D.width; i++)
                {

                    int k = tex2D.height - 1;
                    for (int j = 0; j < tex2D.height; j++, k--)
                    {
                        Color color = tex2D.GetPixel(i, j);
                        aoMetallicRoughness.SetPixel(i, k, new Color(color.b, color.r, 0.0f, 1.0f - color.g));
                    }
                }
                aoMetallicRoughness.Apply();
                urpMat.SetTexture("_MetallicGlossMap", aoMetallicRoughness);
                urpMat.SetTexture("_OcclusionMap", aoMetallicRoughness);
            }
        }

        return urpMat;
    }


    public static Material CopyNewURPMaterial(Material material, int resolutionRatio = 1)
    {
        if (resolutionRatio == 1)
            return material;

        Material newMaterial = Object.Instantiate(material);
        newMaterial.name = material.name;

        Texture mainTexture = null;
        Texture normalTexture = null;
        Texture Ao_metallicRoughnessTexture = null;

        mainTexture = material.GetTexture("_BaseMap");
        normalTexture = material.GetTexture("_BumpMap");
        Ao_metallicRoughnessTexture = material.GetTexture("_MetallicGlossMap");
        if (Ao_metallicRoughnessTexture == null)
            Ao_metallicRoughnessTexture = material.GetTexture("_OcclusionMap");

        mainTexture = mainTexture != null ? AvatarTextureCombiner.GetResizedTexture2D(mainTexture, new Vector2Int(mainTexture.width / resolutionRatio, mainTexture.height / resolutionRatio), TextureFormat.RGBA32) : null;
        normalTexture = normalTexture != null ? AvatarTextureCombiner.GetResizedTexture2D(normalTexture, new Vector2Int(normalTexture.width / resolutionRatio, normalTexture.height / resolutionRatio), TextureFormat.RGBA32) : null;
        Ao_metallicRoughnessTexture = Ao_metallicRoughnessTexture != null ? AvatarTextureCombiner.GetResizedTexture2D(Ao_metallicRoughnessTexture, new Vector2Int(Ao_metallicRoughnessTexture.width / resolutionRatio, Ao_metallicRoughnessTexture.height / resolutionRatio), TextureFormat.RGBA32) : null;

        if (mainTexture != null)
            newMaterial.SetTexture("_BaseMap", mainTexture);
        if (normalTexture != null)
            newMaterial.SetTexture("_BumpMap", normalTexture);
        if (Ao_metallicRoughnessTexture != null)
        {
            newMaterial.SetTexture("_MetallicGlossMap", Ao_metallicRoughnessTexture);
            newMaterial.SetTexture("_OcclusionMap", Ao_metallicRoughnessTexture);

        }
        return newMaterial;
    }

    public static Material MakeCombinedURPMaterial(Texture2D combinedmainTexture,
            Texture2D combinedNormalTexture,
            Texture2D combinedAo_metallicRoughnessTexture)
    {
        Material urpMat =  Object.Instantiate(Resources.Load("URPResources/URPMat-Common", typeof(Material)) as Material);

        urpMat.SetTexture("_BaseMap", GLTFSceneImporter.getTexture2D(combinedmainTexture, combinedmainTexture.width, combinedmainTexture.height, true));
        urpMat.SetTexture("_BumpMap", GLTFSceneImporter.getTexture2D(combinedNormalTexture, combinedNormalTexture.width, combinedNormalTexture.height, true));
        urpMat.SetTexture("_MetallicGlossMap", GLTFSceneImporter.getTexture2D(combinedAo_metallicRoughnessTexture, combinedAo_metallicRoughnessTexture.width, combinedAo_metallicRoughnessTexture.height, true));
        urpMat.SetTexture("_OcclusionMap", GLTFSceneImporter.getTexture2D(combinedAo_metallicRoughnessTexture, combinedAo_metallicRoughnessTexture.width, combinedAo_metallicRoughnessTexture.height, true));

        return urpMat;
    }
}
