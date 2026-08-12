//////////////////////////////////////////////////////
// Shader Packager
// Copyright (c)2021 Jason Booth
//////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;

namespace CityGen3D.ShaderPackager
{
    public class ShaderPackage : ScriptableObject
    {
        static List<string> GetFlags()
        {
            string s = PlayerSettings.GetScriptingDefineSymbols( NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ) );
            string[] split = s.Split( ';' );
            return new List<string>( split );
        }

        static void SetFlags( List<string> flags )
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < flags.Count; ++i)
            {
                sb.Append( flags[i] );
                sb.Append( ";" );
            }

            PlayerSettings.SetScriptingDefineSymbols( NamedBuildTarget.FromBuildTargetGroup( EditorUserBuildSettings.selectedBuildTargetGroup ),  sb.ToString() );
        }

        public enum SRPTarget
        {
            Standard,
            URP,
            HDRP
        }

        public enum UnityVersion
        {
            Min = 0,
            Unity6000_0 = 6000,
            Unity6000_3 = 6300,
            Max = 30000
        }

        [System.Serializable]
        public class Entry
        {
            public SRPTarget srpTarget = SRPTarget.Standard;
            public UnityVersion UnityVersionMin = UnityVersion.Min;
            public UnityVersion UnityVersionMax = UnityVersion.Max;
            public Shader shader;
            [HideInInspector] public string shaderSrc;
        }

        public List<Entry> entries = new List<Entry>();
#if __BETTERSHADERS__
        public Shader betterShader;
        public string betterShaderPath;
        public JBooth.BetterShaders.OptionOverrides optionOverrides;
#endif

        public void Pack( bool warnErrors )
        {
#if __BETTERSHADERS__
            if (betterShader != null)
            {
                betterShaderPath = AssetDatabase.GetAssetPath( betterShader );
            }
            if (betterShader == null)
            {
                if (!System.IO.File.Exists( betterShaderPath ))
                {
                    Debug.LogWarning( "Shader Packager: Source shader GUID and path have changed, you will need to manually repack the shaders from source" );
                    return;
                }
            }
            if (!string.IsNullOrEmpty( betterShaderPath ))
            {
                var assetPath = betterShaderPath;
                if (assetPath.EndsWith( ".surfshader" ))
                {
                    entries.Clear();
                    ShaderPackage.Entry e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.BetterShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.Standard, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.Standard;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Min;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Max;

                    e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.BetterShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.HDRP2023, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.HDRP;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Min;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Unity6000_0;

                    e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.BetterShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.URP2023, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.URP;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Min;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Unity6000_0;

                    e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.BetterShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.HDRP6P3, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.HDRP;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Unity6000_3;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Max;

                    e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.BetterShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.URP6P3, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.URP;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Unity6000_3;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Max;
                }
                else if (assetPath.EndsWith( ".stackedshader" ))
                {
                    entries.Clear();
                    ShaderPackage.Entry e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.StackedShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.Standard, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.Standard;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Min;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Max;

                    e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.StackedShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.URP2023, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.URP;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Min;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Unity6000_0;

                    e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.StackedShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.HDRP2023, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.HDRP;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Min;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Unity6000_0;

                    e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.StackedShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.URP6P3, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.URP;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Unity6000_3;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Max;

                    e = new ShaderPackage.Entry();
                    entries.Add( e );
                    e.shaderSrc = JBooth.BetterShaders.StackedShaderImporterEditor.BuildExportShader(
                        JBooth.BetterShaders.ShaderBuilder.RenderPipeline.HDRP6P3, optionOverrides, assetPath );
                    e.srpTarget = ShaderPackage.SRPTarget.HDRP;
                    e.UnityVersionMin = ShaderPackage.UnityVersion.Unity6000_3;
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Max;
                }
            }
#endif

            foreach (var e in entries)
            {
                if (e.shader
#if __BETTERSHADERS__
               && betterShader == null
#endif
               )
                {
                    if (warnErrors)
                    {
                        Debug.LogError( "Shader is null, cannot pack" );
                    }
                    break;
                }
                if (e.UnityVersionMax == ShaderPackage.UnityVersion.Min && e.UnityVersionMin == ShaderPackage.UnityVersion.Min)
                {
                    e.UnityVersionMax = ShaderPackage.UnityVersion.Max;
                }
                if (e.shader != null)
                {
                    var path = AssetDatabase.GetAssetPath( e.shader );
                    e.shaderSrc = System.IO.File.ReadAllText( path );
                }
            }
        }

        public string GetShaderSrc()
        {
            UnityVersion curVersion = UnityVersion.Min;

#if UNITY_6000_0_OR_NEWER
            curVersion = UnityVersion.Unity6000_0;
#endif
#if UNITY_6000_3_OR_NEWER
            curVersion = UnityVersion.Unity6000_3;
#endif

            SRPTarget target = SRPTarget.Standard;
            if (RenderPipelineDefine.IsHDRP)
            {
                target = SRPTarget.HDRP;
            }
            else if (RenderPipelineDefine.IsURP)
            {
                target = SRPTarget.URP;
            }

            string s = null;
            foreach (var e in entries)
            {
                if (target != e.srpTarget)
                    continue;
                // default init state..
                if (e.UnityVersionMax == UnityVersion.Min && e.UnityVersionMin == UnityVersion.Min)
                {
                    e.UnityVersionMax = UnityVersion.Max;
                }
                if (curVersion >= e.UnityVersionMin && curVersion <= e.UnityVersionMax)
                {
                    if (s != null)
                    {
                        Debug.LogWarning( "Found multiple possible entries for unity version of shader" );
                    }
                    s = e.shaderSrc;
                }
            }
            return s;
        }
    }
}
