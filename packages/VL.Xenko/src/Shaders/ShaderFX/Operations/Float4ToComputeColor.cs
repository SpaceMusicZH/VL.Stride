﻿using System;
using System.Collections.Generic;
using System.Text;
using Xenko.Rendering.Materials;
using Xenko.Rendering.Materials.ComputeColors;
using Xenko.Shaders;
using Xenko.Core.Mathematics;

namespace VL.Xenko.Shaders.ShaderFX.Operations
{
    public class Float4ToComputeColor : ComputeColor
    {
        public IComputeValue<Vector4> Input { get; set; }

        public override IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            return ComputeNode<Vector4>.ReturnIfNotNull(Input);
        }

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            var shaderSource = new ShaderClassSource("Float4ToColor");

            var mixin = new ShaderMixinSource();
            mixin.Mixins.Add(shaderSource);

            var inputShaderSource = Input?.GenerateShaderSource(context, baseKeys);
            if (inputShaderSource != null)
                mixin.AddComposition("Value", inputShaderSource);

            return mixin;
        }
    }
}
