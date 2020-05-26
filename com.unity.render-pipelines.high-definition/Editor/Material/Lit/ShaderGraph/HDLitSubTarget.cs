﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Legacy;
using UnityEditor.Rendering.HighDefinition.ShaderGraph.Legacy;
using static UnityEngine.Rendering.HighDefinition.HDMaterialProperties;
using static UnityEditor.Rendering.HighDefinition.HDShaderUtils;

namespace UnityEditor.Rendering.HighDefinition.ShaderGraph
{
    sealed partial class HDLitSubTarget : LightingSubTarget, ILegacyTarget, IRequiresData<HDLitData>
    {
        static string passTemplatePath => $"{HDUtils.GetHDRenderPipelinePath()}Editor/Material/Lit/ShaderGraph/LitPass.template";

        public HDLitSubTarget() => displayName = "Lit";

        protected override string customInspector => "Rendering.HighDefinition.HDLitGUI";
        protected override string subTargetAssetGuid => "caab952c840878340810cca27417971c"; // HDLitSubTarget.cs
        protected override ShaderID shaderID => HDShaderUtils.ShaderID.SG_Lit;

        HDLitData m_LitData;

        HDLitData IRequiresData<HDLitData>.data
        {
            get => m_LitData;
            set => m_LitData = value;
        }

        public HDLitData litData
        {
            get => m_LitData;
            set => m_LitData = value;
        }

        // Iterate over the sub passes available in the shader
        protected override IEnumerable<SubShaderDescriptor> EnumerateSubShaders()
        {
            yield return SubShaders.Lit;
            yield return SubShaders.LitRaytracing;
        }

        public override void GetFields(ref TargetFieldContext context)
        {
            base.GetFields(ref context);
            AddDistortionFields(ref context);

            bool hasRefraction = (systemData.surfaceType == SurfaceType.Transparent && systemData.renderingPass != HDRenderQueue.RenderQueueType.PreRefraction && litData.refractionModel != ScreenSpaceRefraction.RefractionModel.None);

            // Lit specific properties
            context.AddField(HDStructFields.FragInputs.IsFrontFace,         systemData.doubleSidedMode != DoubleSidedMode.Disabled && !context.pass.Equals(HDLitSubTarget.LitPasses.MotionVectors));

            context.AddField(HDFields.DotsProperties,                       context.hasDotsProperties);

            // Material
            context.AddField(HDFields.Anisotropy,                           litData.materialType == HDLitData.MaterialType.Anisotropy);
            context.AddField(HDFields.Iridescence,                          litData.materialType == HDLitData.MaterialType.Iridescence);
            context.AddField(HDFields.SpecularColor,                        litData.materialType == HDLitData.MaterialType.SpecularColor);
            context.AddField(HDFields.Standard,                             litData.materialType == HDLitData.MaterialType.Standard);
            context.AddField(HDFields.SubsurfaceScattering,                 litData.materialType == HDLitData.MaterialType.SubsurfaceScattering && systemData.surfaceType != SurfaceType.Transparent);
            context.AddField(HDFields.Transmission,                         (litData.materialType == HDLitData.MaterialType.SubsurfaceScattering && litData.sssTransmission) ||
                                                                                (litData.materialType == HDLitData.MaterialType.Translucent));
            context.AddField(HDFields.Translucent,                          litData.materialType == HDLitData.MaterialType.Translucent);

            context.AddField(HDFields.DoubleSidedFlip,                      systemData.doubleSidedMode == DoubleSidedMode.FlippedNormals && !context.pass.Equals(HDLitSubTarget.LitPasses.MotionVectors));
            context.AddField(HDFields.DoubleSidedMirror,                    systemData.doubleSidedMode == DoubleSidedMode.MirroredNormals && !context.pass.Equals(HDLitSubTarget.LitPasses.MotionVectors));

            // Refraction
            context.AddField(HDFields.Refraction,                           hasRefraction);
            context.AddField(HDFields.RefractionBox,                        hasRefraction && litData.refractionModel == ScreenSpaceRefraction.RefractionModel.Box);
            context.AddField(HDFields.RefractionSphere,                     hasRefraction && litData.refractionModel == ScreenSpaceRefraction.RefractionModel.Sphere);

            // AlphaTest
            // All the DoAlphaXXX field drive the generation of which code to use for alpha test in the template
            // Do alpha test only if we aren't using the TestShadow one
            context.AddField(HDFields.DoAlphaTest,                          systemData.alphaTest && (context.pass.validPixelBlocks.Contains(BlockFields.SurfaceDescription.AlphaClipThreshold) &&
                                                                                !(builtinData.alphaTestShadow && context.pass.validPixelBlocks.Contains(HDBlockFields.SurfaceDescription.AlphaClipThresholdShadow))));

            // Misc

            context.AddField(HDFields.EnergyConservingSpecular,             litData.energyConservingSpecular);
            context.AddField(HDFields.CoatMask,                             context.blocks.Contains(HDBlockFields.SurfaceDescription.CoatMask) && context.pass.validPixelBlocks.Contains(HDBlockFields.SurfaceDescription.CoatMask));
            context.AddField(HDFields.Tangent,                              context.blocks.Contains(HDBlockFields.SurfaceDescription.Tangent) && context.pass.validPixelBlocks.Contains(HDBlockFields.SurfaceDescription.Tangent));
            context.AddField(HDFields.RayTracing,                           litData.rayTracing);
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            bool hasRefraction = (systemData.surfaceType == SurfaceType.Transparent && systemData.renderingPass != HDRenderQueue.RenderQueueType.PreRefraction && litData.refractionModel != ScreenSpaceRefraction.RefractionModel.None);
            bool hasDistortion = (systemData.surfaceType == SurfaceType.Transparent && builtinData.distortion);

            // Vertex
            base.GetActiveBlocks(ref context);
            AddDistortionBlocks(ref context);
            AddNormalBlocks(ref context);

            // Common
            context.AddBlock(HDBlockFields.SurfaceDescription.CoatMask);

            // Refraction
            context.AddBlock(HDBlockFields.SurfaceDescription.RefractionIndex,      hasRefraction);
            context.AddBlock(HDBlockFields.SurfaceDescription.RefractionColor,      hasRefraction);
            context.AddBlock(HDBlockFields.SurfaceDescription.RefractionDistance,   hasRefraction);

            // Material
            context.AddBlock(HDBlockFields.SurfaceDescription.Tangent,              litData.materialType == HDLitData.MaterialType.Anisotropy);
            context.AddBlock(HDBlockFields.SurfaceDescription.Anisotropy,           litData.materialType == HDLitData.MaterialType.Anisotropy);
            context.AddBlock(HDBlockFields.SurfaceDescription.SubsurfaceMask,       litData.materialType == HDLitData.MaterialType.SubsurfaceScattering);
            context.AddBlock(HDBlockFields.SurfaceDescription.Thickness,            ((litData.materialType == HDLitData.MaterialType.SubsurfaceScattering || litData.materialType == HDLitData.MaterialType.Translucent) &&
                                                                                        (litData.sssTransmission || litData.materialType == HDLitData.MaterialType.Translucent)) || hasRefraction);
            context.AddBlock(HDBlockFields.SurfaceDescription.DiffusionProfileHash, litData.materialType == HDLitData.MaterialType.SubsurfaceScattering || litData.materialType == HDLitData.MaterialType.Translucent);
            context.AddBlock(HDBlockFields.SurfaceDescription.IridescenceMask,      litData.materialType == HDLitData.MaterialType.Iridescence);
            context.AddBlock(HDBlockFields.SurfaceDescription.IridescenceThickness, litData.materialType == HDLitData.MaterialType.Iridescence);
            context.AddBlock(BlockFields.SurfaceDescription.Specular,               litData.materialType == HDLitData.MaterialType.SpecularColor);
            context.AddBlock(BlockFields.SurfaceDescription.Metallic,               litData.materialType == HDLitData.MaterialType.Standard || 
                                                                                        litData.materialType == HDLitData.MaterialType.Anisotropy ||
                                                                                        litData.materialType == HDLitData.MaterialType.Iridescence);
        }

        public override void CollectShaderProperties(PropertyCollector collector, GenerationMode generationMode)
        {
            base.CollectShaderProperties(collector, generationMode);

            HDSubShaderUtilities.AddRayTracingProperty(collector, litData.rayTracing);
        }

        protected override void AddInspectorPropertyBlocks(SubTargetPropertiesGUI blockList)
        {
            blockList.AddPropertyBlock(new LitSurfaceOptionPropertyBlock(SurfaceOptionPropertyBlock.Features.Lit, litData));
            blockList.AddPropertyBlock(new DistortionPropertyBlock());
            blockList.AddPropertyBlock(new AdvancedOptionsPropertyBlock());
        }

        public override void ProcessPreviewMaterial(Material material)
        {
            // Fixup the material settings:
            material.SetFloat(kSurfaceType, (int)systemData.surfaceType);
            material.SetFloat(kDoubleSidedNormalMode, (int)systemData.doubleSidedMode);
            material.SetFloat(kAlphaCutoffEnabled, systemData.alphaTest ? 1 : 0);
            material.SetFloat(kBlendMode, (int)systemData.blendMode);
            material.SetFloat(kEnableFogOnTransparent, builtinData.transparencyFog ? 1.0f : 0.0f);
            material.SetFloat(kZTestTransparent, (int)systemData.zTest);
            material.SetFloat(kTransparentCullMode, (int)systemData.transparentCullMode);
            material.SetFloat(kZWrite, systemData.zWrite ? 1.0f : 0.0f);

            // No sorting priority for shader graph preview
            material.renderQueue = (int)HDRenderQueue.ChangeType(systemData.renderingPass, offset: 0, alphaTest: systemData.alphaTest);

            HDLitGUI.SetupMaterialKeywordsAndPass(material);
        }

        protected override int ComputeMaterialNeedsUpdateHash()
        {
            bool subsurfaceScattering = litData.materialType == HDLitData.MaterialType.SubsurfaceScattering;
            int hash = base.ComputeMaterialNeedsUpdateHash();

            unchecked
            {
                hash = hash * 23 + lightingData.receiveSSRTransparent.GetHashCode();
                hash = hash * 23 + subsurfaceScattering.GetHashCode();
            }

            return hash;
        }

#region SubShaders
        static class SubShaders
        {
            public static SubShaderDescriptor Lit = new SubShaderDescriptor()
            {
                pipelineTag = HDRenderPipeline.k_ShaderTagName,
                generatesPreview = true,
                passes = new PassCollection
                {
                    { LitPasses.ShadowCaster },
                    { LitPasses.META },
                    { LitPasses.SceneSelection },
                    { LitPasses.DepthOnly },
                    { LitPasses.GBuffer },
                    { LitPasses.MotionVectors },
                    { LitPasses.DistortionVectors, new FieldCondition(HDFields.TransparentDistortion, true) },
                    { LitPasses.TransparentBackface, new FieldCondition(HDFields.TransparentBackFace, true) },
                    { LitPasses.TransparentDepthPrepass, new FieldCondition[]{
                                                            new FieldCondition(HDFields.TransparentDepthPrePass, true),
                                                            new FieldCondition(HDFields.DisableSSRTransparent, true) }},
                    { LitPasses.TransparentDepthPrepass, new FieldCondition[]{
                                                            new FieldCondition(HDFields.TransparentDepthPrePass, true),
                                                            new FieldCondition(HDFields.DisableSSRTransparent, false) }},
                    { LitPasses.TransparentDepthPrepass, new FieldCondition[]{
                                                            new FieldCondition(HDFields.TransparentDepthPrePass, false),
                                                            new FieldCondition(HDFields.DisableSSRTransparent, false) }},
                    { LitPasses.Forward },
                    { LitPasses.TransparentDepthPostpass, new FieldCondition(HDFields.TransparentDepthPostPass, true) },
                    { LitPasses.RayTracingPrepass, new FieldCondition(HDFields.RayTracing, true) },
                },
            };

            public static SubShaderDescriptor LitRaytracing = new SubShaderDescriptor()
            {
                pipelineTag = HDRenderPipeline.k_ShaderTagName,
                generatesPreview = false,
                passes = new PassCollection
                {
                    { LitPasses.RaytracingIndirect, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingVisibility, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingForward, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingGBuffer, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingSubSurface, new FieldCondition(Fields.IsPreview, false) },
                    { LitPasses.RaytracingPathTracing, new FieldCondition(Fields.IsPreview, false) },
                },
            };
        }
#endregion

#region Passes
        public static class LitPasses
        {
            public static PassDescriptor GBuffer = new PassDescriptor()
            {
                // Definition
                displayName = "GBuffer",
                referenceName = "SHADERPASS_GBUFFER",
                lightMode = "GBuffer",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitMinimal,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = LitRenderStates.GBuffer,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = LitKeywords.GBuffer,
                includes = LitIncludes.GBuffer,

                virtualTextureFeedback = true,
            };

            public static PassDescriptor META = new PassDescriptor()
            {
                // Definition
                displayName = "META",
                referenceName = "SHADERPASS_LIGHT_TRANSPORT",
                lightMode = "META",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validPixelBlocks = LitBlockMasks.FragmentMeta,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.Meta,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.Meta,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.Meta,
            };

            public static PassDescriptor ShadowCaster = new PassDescriptor()
            {
                // Definition
                displayName = "ShadowCaster",
                referenceName = "SHADERPASS_SHADOWS",
                lightMode = "ShadowCaster",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentShadowCaster,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.BlendShadowCaster,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor SceneSelection = new PassDescriptor()
            {
                // Definition
                displayName = "SceneSelectionPass",
                referenceName = "SHADERPASS_DEPTH_ONLY",
                lightMode = "SceneSelectionPass",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentSceneSelection,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.SceneSelection,
                pragmas = CorePragmas.DotsInstancedInV1AndV2EditorSync,
                defines = CoreDefines.SceneSelection,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor DepthOnly = new PassDescriptor()
            {
                // Definition
                displayName = "DepthOnly",
                referenceName = "SHADERPASS_DEPTH_ONLY",
                lightMode = "DepthOnly",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDepthMotionVectors,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitFull,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.DepthOnly,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = LitKeywords.DepthMotionVectors,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor MotionVectors = new PassDescriptor()
            {
                // Definition
                displayName = "MotionVectors",
                referenceName = "SHADERPASS_MOTION_VECTORS",
                lightMode = "MotionVectors",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDepthMotionVectors,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitFull,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.MotionVectors,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = LitKeywords.DepthMotionVectors,
                includes = LitIncludes.MotionVectors,
            };

            public static PassDescriptor DistortionVectors = new PassDescriptor()
            {
                // Definition
                displayName = "DistortionVectors",
                referenceName = "SHADERPASS_DISTORTION",
                lightMode = "DistortionVectors",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDistortion,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = LitRenderStates.Distortion,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.Distortion,
            };

            public static PassDescriptor TransparentDepthPrepass = new PassDescriptor()
            {
                // Definition
                displayName = "TransparentDepthPrepass",
                referenceName = "SHADERPASS_DEPTH_ONLY",
                lightMode = "TransparentDepthPrepass",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentTransparentDepthPrepass,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitFull,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = LitRenderStates.TransparentDepthPrePostPass,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.TransparentDepthPrepass,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor TransparentBackface = new PassDescriptor()
            {
                // Definition
                displayName = "TransparentBackface",
                referenceName = "SHADERPASS_FORWARD",
                lightMode = "TransparentBackface",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentTransparentBackface,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.TransparentBackface,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.Forward,
                keywords = CoreKeywords.Forward,
                includes = LitIncludes.Forward,
            };

            public static PassDescriptor Forward = new PassDescriptor()
            {
                // Definition
                displayName = "Forward",
                referenceName = "SHADERPASS_FORWARD",
                lightMode = "Forward",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                requiredFields = CoreRequiredFields.LitMinimal,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.ForwardColorMask,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.Forward,
                keywords = CoreKeywords.Forward,
                includes = LitIncludes.Forward,

                virtualTextureFeedback = true,
            };

            public static PassDescriptor TransparentDepthPostpass = new PassDescriptor()
            {
                // Definition
                displayName = "TransparentDepthPostpass",
                referenceName = "SHADERPASS_DEPTH_ONLY",
                lightMode = "TransparentDepthPostpass",
                useInPreview = true,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentTransparentDepthPostpass,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = CoreRenderStates.TransparentDepthPrePostPass,
                pragmas = CorePragmas.DotsInstancedInV1AndV2,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.DepthOnly,
            };

            public static PassDescriptor RayTracingPrepass = new PassDescriptor()
            {
                // Definition
                displayName = "RayTracingPrepass",
                referenceName = "SHADERPASS_CONSTANT",
                lightMode = "RayTracingPrepass",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentRayTracingPrepass,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                renderStates = LitRenderStates.RayTracingPrepass,
                pragmas = LitPragmas.RaytracingBasic,
                defines = CoreDefines.ShaderGraphRaytracingHigh,
                keywords = CoreKeywords.HDBase,
                includes = LitIncludes.RayTracingPrepass,
            };

            public static PassDescriptor RaytracingIndirect = new PassDescriptor()
            {
                // Definition
                displayName = "IndirectDXR",
                referenceName = "SHADERPASS_RAYTRACING_INDIRECT",
                lightMode = "IndirectDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingIndirect,
                keywords = CoreKeywords.RaytracingIndirect,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingIndirect },
            };

            public static PassDescriptor RaytracingVisibility = new PassDescriptor()
            {
                // Definition
                displayName = "VisibilityDXR",
                referenceName = "SHADERPASS_RAYTRACING_VISIBILITY",
                lightMode = "VisibilityDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingVisibility,
                includes = CoreIncludes.Raytracing,
                keywords = CoreKeywords.RaytracingVisiblity,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingVisibility },
            };

            public static PassDescriptor RaytracingForward = new PassDescriptor()
            {
                // Definition
                displayName = "ForwardDXR",
                referenceName = "SHADERPASS_RAYTRACING_FORWARD",
                lightMode = "ForwardDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingForward,
                keywords = CoreKeywords.RaytracingGBufferForward,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingForward },
            };

            public static PassDescriptor RaytracingGBuffer = new PassDescriptor()
            {
                // Definition
                displayName = "GBufferDXR",
                referenceName = "SHADERPASS_RAYTRACING_GBUFFER",
                lightMode = "GBufferDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                // Port Mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDefault,

                // Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingGBuffer,
                keywords = CoreKeywords.RaytracingGBufferForward,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RayTracingGBuffer },
            };

            public static PassDescriptor RaytracingPathTracing = new PassDescriptor()
            {
                //Definition
                displayName = "PathTracingDXR",
                referenceName = "SHADERPASS_PATH_TRACING",
                lightMode = "PathTracingDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                //Port mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDefault,

                //Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingPathTracing,
                keywords = CoreKeywords.HDBaseNoCrossFade,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingPathTracing },
            };

            public static PassDescriptor RaytracingSubSurface = new PassDescriptor()
            {
                //Definition
                displayName = "SubSurfaceDXR",
                referenceName = "SHADERPASS_RAYTRACING_SUB_SURFACE",
                lightMode = "SubSurfaceDXR",
                useInPreview = false,

                // Template
                passTemplatePath = passTemplatePath,
                sharedTemplateDirectory = HDTarget.sharedTemplateDirectory,

                //Port mask
                validVertexBlocks = CoreBlockMasks.Vertex,
                validPixelBlocks = LitBlockMasks.FragmentDefault,

                //Collections
                structs = CoreStructCollections.Default,
                fieldDependencies = CoreFieldDependencies.Default,
                pragmas = CorePragmas.RaytracingBasic,
                defines = LitDefines.RaytracingGBuffer,
                keywords = CoreKeywords.RaytracingGBufferForward,
                includes = CoreIncludes.Raytracing,
                requiredFields = new FieldCollection(){ HDFields.SubShader.Lit, HDFields.ShaderPass.RaytracingSubSurface },
            };
        }
#endregion

#region BlockMasks
        static class LitBlockMasks
        {
            public static BlockFieldDescriptor[] FragmentDefault = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                HDBlockFields.SurfaceDescription.BentNormal,
                HDBlockFields.SurfaceDescription.Tangent,
                HDBlockFields.SurfaceDescription.SubsurfaceMask,
                HDBlockFields.SurfaceDescription.Thickness,
                HDBlockFields.SurfaceDescription.DiffusionProfileHash,
                HDBlockFields.SurfaceDescription.IridescenceMask,
                HDBlockFields.SurfaceDescription.IridescenceThickness,
                BlockFields.SurfaceDescription.Specular,
                HDBlockFields.SurfaceDescription.CoatMask,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                HDBlockFields.SurfaceDescription.SpecularOcclusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.Anisotropy,
                HDBlockFields.SurfaceDescription.SpecularAAScreenSpaceVariance,
                HDBlockFields.SurfaceDescription.SpecularAAThreshold,
                HDBlockFields.SurfaceDescription.RefractionIndex,
                HDBlockFields.SurfaceDescription.RefractionColor,
                HDBlockFields.SurfaceDescription.RefractionDistance,
                HDBlockFields.SurfaceDescription.BakedGI,
                HDBlockFields.SurfaceDescription.BakedBackGI,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentMeta = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                HDBlockFields.SurfaceDescription.BentNormal,
                HDBlockFields.SurfaceDescription.Tangent,
                HDBlockFields.SurfaceDescription.SubsurfaceMask,
                HDBlockFields.SurfaceDescription.Thickness,
                HDBlockFields.SurfaceDescription.DiffusionProfileHash,
                HDBlockFields.SurfaceDescription.IridescenceMask,
                HDBlockFields.SurfaceDescription.IridescenceThickness,
                BlockFields.SurfaceDescription.Specular,
                HDBlockFields.SurfaceDescription.CoatMask,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                HDBlockFields.SurfaceDescription.SpecularOcclusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.Anisotropy,
                HDBlockFields.SurfaceDescription.SpecularAAScreenSpaceVariance,
                HDBlockFields.SurfaceDescription.SpecularAAThreshold,
                HDBlockFields.SurfaceDescription.RefractionIndex,
                HDBlockFields.SurfaceDescription.RefractionColor,
                HDBlockFields.SurfaceDescription.RefractionDistance,
            };

            public static BlockFieldDescriptor[] FragmentShadowCaster = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.AlphaClipThresholdShadow,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentSceneSelection = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentDepthMotionVectors = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentDistortion = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.Distortion,
                HDBlockFields.SurfaceDescription.DistortionBlur,
            };

            public static BlockFieldDescriptor[] FragmentTransparentDepthPrepass = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPrepass,
                HDBlockFields.SurfaceDescription.DepthOffset,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                BlockFields.SurfaceDescription.Smoothness,
            };

            public static BlockFieldDescriptor[] FragmentTransparentBackface = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.BaseColor,
                BlockFields.SurfaceDescription.NormalTS,
                BlockFields.SurfaceDescription.NormalWS,
                BlockFields.SurfaceDescription.NormalOS,
                HDBlockFields.SurfaceDescription.BentNormal,
                HDBlockFields.SurfaceDescription.Tangent,
                HDBlockFields.SurfaceDescription.SubsurfaceMask,
                HDBlockFields.SurfaceDescription.Thickness,
                HDBlockFields.SurfaceDescription.DiffusionProfileHash,
                HDBlockFields.SurfaceDescription.IridescenceMask,
                HDBlockFields.SurfaceDescription.IridescenceThickness,
                BlockFields.SurfaceDescription.Specular,
                HDBlockFields.SurfaceDescription.CoatMask,
                BlockFields.SurfaceDescription.Metallic,
                BlockFields.SurfaceDescription.Emission,
                BlockFields.SurfaceDescription.Smoothness,
                BlockFields.SurfaceDescription.Occlusion,
                HDBlockFields.SurfaceDescription.SpecularOcclusion,
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.Anisotropy,
                HDBlockFields.SurfaceDescription.SpecularAAScreenSpaceVariance,
                HDBlockFields.SurfaceDescription.SpecularAAThreshold,
                HDBlockFields.SurfaceDescription.RefractionIndex,
                HDBlockFields.SurfaceDescription.RefractionColor,
                HDBlockFields.SurfaceDescription.RefractionDistance,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentTransparentDepthPostpass = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                HDBlockFields.SurfaceDescription.AlphaClipThresholdDepthPostpass,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };

            public static BlockFieldDescriptor[] FragmentRayTracingPrepass = new BlockFieldDescriptor[]
            {
                BlockFields.SurfaceDescription.Alpha,
                BlockFields.SurfaceDescription.AlphaClipThreshold,
                HDBlockFields.SurfaceDescription.DepthOffset,
            };
        }
#endregion

#region RenderStates
        static class LitRenderStates
        {
            public static RenderStateCollection GBuffer = new RenderStateCollection
            {
                { RenderState.Cull(CoreRenderStates.Uniforms.cullMode) },
                { RenderState.ZTest(CoreRenderStates.Uniforms.zTestGBuffer) },
                { RenderState.Stencil(new StencilDescriptor()
                {
                    WriteMask = CoreRenderStates.Uniforms.stencilWriteMaskGBuffer,
                    Ref = CoreRenderStates.Uniforms.stencilRefGBuffer,
                    Comp = "Always",
                    Pass = "Replace",
                }) },
            };

            public static RenderStateCollection Distortion = new RenderStateCollection
            {
                { RenderState.Blend(Blend.One, Blend.One, Blend.One, Blend.One), new FieldCondition(HDFields.DistortionAdd, true) },
                { RenderState.Blend(Blend.DstColor, Blend.Zero, Blend.DstAlpha, Blend.Zero), new FieldCondition(HDFields.DistortionMultiply, true) },
                { RenderState.Blend(Blend.One, Blend.Zero, Blend.One, Blend.Zero), new FieldCondition(HDFields.DistortionReplace, true) },
                { RenderState.BlendOp(BlendOp.Add, BlendOp.Add) },
                { RenderState.ZWrite(ZWrite.Off) },
                { RenderState.ZTest(ZTest.Always), new FieldCondition(HDFields.DistortionDepthTest, false) },
                { RenderState.ZTest(ZTest.LEqual), new FieldCondition(HDFields.DistortionDepthTest, true) },
                { RenderState.Stencil(new StencilDescriptor()
                {
                    WriteMask = CoreRenderStates.Uniforms.stencilWriteMaskDistortionVec,
                    Ref = CoreRenderStates.Uniforms.stencilRefDistortionVec,
                    Comp = "Always",
                    Pass = "Replace",
                }) },
            };

            public static RenderStateCollection TransparentDepthPrePostPass = new RenderStateCollection
            {
                { RenderState.Blend(Blend.One, Blend.Zero) },
                { RenderState.Cull(CoreRenderStates.Uniforms.cullMode) },
                { RenderState.ZWrite(ZWrite.On) },
                { RenderState.Stencil(new StencilDescriptor()
                {
                    WriteMask = CoreRenderStates.Uniforms.stencilWriteMaskDepth,
                    Ref = CoreRenderStates.Uniforms.stencilRefDepth,
                    Comp = "Always",
                    Pass = "Replace",
                }) },
            };

            public static RenderStateCollection RayTracingPrepass = new RenderStateCollection
            {
                { RenderState.Blend(Blend.One, Blend.Zero) },
                { RenderState.Cull(CoreRenderStates.Uniforms.cullMode) },
                { RenderState.ZWrite(ZWrite.On) },
                // Note: we use default ZTest LEqual so if the object have already been render in depth prepass, it will re-render to tag stencil
            };
        }
#endregion

#region Pragmas
        static class LitPragmas
        {
            public static PragmaCollection RaytracingBasic = new PragmaCollection
            {
                { Pragma.Target(ShaderModel.Target45) },
                { Pragma.Vertex("Vert") },
                { Pragma.Fragment("Frag") },
                { Pragma.OnlyRenderers(new Platform[] {Platform.D3D11}) },
            };
        }
#endregion

#region Defines
        static class LitDefines
        {
            public static DefineCollection RaytracingForward = new DefineCollection
            {
                { CoreKeywordDescriptors.Shadow, 0 },
                { RayTracingNode.GetRayTracingKeyword(), 0 },
                { CoreKeywordDescriptors.HasLightloop, 1 },
            };

            public static DefineCollection RaytracingIndirect = new DefineCollection
            {
                { CoreKeywordDescriptors.Shadow, 0 },
                { RayTracingNode.GetRayTracingKeyword(), 1 },
                { CoreKeywordDescriptors.HasLightloop, 1 },
            };

            public static DefineCollection RaytracingGBuffer = new DefineCollection
            {
                { CoreKeywordDescriptors.Shadow, 0 },
                { RayTracingNode.GetRayTracingKeyword(), 1 },
            };

            public static DefineCollection RaytracingVisibility = new DefineCollection
            {
                { RayTracingNode.GetRayTracingKeyword(), 1 },
            };

            public static DefineCollection RaytracingPathTracing = new DefineCollection
            {
                { CoreKeywordDescriptors.Shadow, 0 },
                { RayTracingNode.GetRayTracingKeyword(), 0 },
                { CoreKeywordDescriptors.HasLightloop, 1 },
            };
        }
#endregion

#region Keywords
        static class LitKeywords
        {
            public static KeywordCollection GBuffer = new KeywordCollection
            {
                { CoreKeywords.HDBase },
                { CoreKeywordDescriptors.DebugDisplay },
                { CoreKeywords.Lightmaps },
                { CoreKeywordDescriptors.ShadowsShadowmask },
                { CoreKeywordDescriptors.LightLayers },
                { CoreKeywordDescriptors.Decals },
            };

            public static KeywordCollection DepthMotionVectors = new KeywordCollection
            {
                { CoreKeywords.HDBase },
                { CoreKeywordDescriptors.WriteMsaaDepth },
                { CoreKeywordDescriptors.WriteNormalBuffer },
                { CoreKeywordDescriptors.AlphaToMask, new FieldCondition(Fields.AlphaToMask, true) },
            };
        }
#endregion

#region Includes
        static class LitIncludes
        {
            const string kLitDecalData = "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitDecalData.hlsl";
            const string kPassGBuffer = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassGBuffer.hlsl";
            const string kPassConstant = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassConstant.hlsl";
            
            public static IncludeCollection Common = new IncludeCollection
            {
                { CoreIncludes.CorePregraph },
                { CoreIncludes.kNormalSurfaceGradient, IncludeLocation.Pregraph },
                { CoreIncludes.kLit, IncludeLocation.Pregraph },
                { CoreIncludes.CoreUtility },
                { CoreIncludes.kDecalUtilities, IncludeLocation.Pregraph },
                { kLitDecalData, IncludeLocation.Pregraph },
                { CoreIncludes.kShaderGraphFunctions, IncludeLocation.Pregraph },
            };

            public static IncludeCollection GBuffer = new IncludeCollection
            {
                { Common },
                { kPassGBuffer, IncludeLocation.Postgraph },
            };

            public static IncludeCollection Meta = new IncludeCollection
            {
                { Common },
                { CoreIncludes.kPassLightTransport, IncludeLocation.Postgraph },
            };

            public static IncludeCollection DepthOnly = new IncludeCollection
            {
                { Common },
                { CoreIncludes.kPassDepthOnly, IncludeLocation.Postgraph },
            };

            public static IncludeCollection RayTracingPrepass = new IncludeCollection
            {
                { Common },
                { kPassConstant, IncludeLocation.Postgraph },
            };

            public static IncludeCollection MotionVectors = new IncludeCollection
            {
                { Common },
                { CoreIncludes.kPassMotionVectors, IncludeLocation.Postgraph },
            };

            public static IncludeCollection Forward = new IncludeCollection
            {
                { CoreIncludes.CorePregraph },
                { CoreIncludes.kNormalSurfaceGradient, IncludeLocation.Pregraph },
                { CoreIncludes.kLighting, IncludeLocation.Pregraph },
                { CoreIncludes.kLightLoopDef, IncludeLocation.Pregraph },
                { CoreIncludes.kLit, IncludeLocation.Pregraph },
                { CoreIncludes.kLightLoop, IncludeLocation.Pregraph },
                { CoreIncludes.CoreUtility },
                { CoreIncludes.kDecalUtilities, IncludeLocation.Pregraph },
                { kLitDecalData, IncludeLocation.Pregraph },
                { CoreIncludes.kShaderGraphFunctions, IncludeLocation.Pregraph },
                { CoreIncludes.kPassForward, IncludeLocation.Postgraph },
            };

            public static IncludeCollection Distortion = new IncludeCollection
            {
                { Common },
                { CoreIncludes.kDisortionVectors, IncludeLocation.Postgraph },
            };
        }
#endregion
    }
}
