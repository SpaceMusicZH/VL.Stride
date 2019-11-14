﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xenko.Rendering.Materials;
using Xenko.Shaders;

namespace VL.Xenko.Shaders.ShaderFX
{
    public class Funk2In1Out<TIn1, TIn2, TOut> : ComputeNode<TOut>
    {
        public Funk2In1Out(string functionName, IEnumerable<KeyValuePair<string, IComputeNode>> inputs)
        {
            ShaderName = functionName;
            Inputs = inputs;
        }

        public IEnumerable<KeyValuePair<string, IComputeNode>> Inputs { get; private set; }

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            var shaderSource = new ShaderClassSource(ShaderName);

            //compose if necessary
            if (Inputs != null && Inputs.Any())
            {
                var mixin = shaderSource.CreateMixin();

                foreach (var input in Inputs)
                {
                    mixin.AddComposition(input.Value, input.Key, context, baseKeys);
                }

                return mixin;
            }

            return shaderSource;
        }

        public override IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            if (Inputs != null)
            {
                foreach (var item in Inputs)
                {
                    if (item.Value != null)
                        yield return item.Value;
                }
            }
        }

        public override string ToString()
        {
            return ShaderName;
        }
    }
}
