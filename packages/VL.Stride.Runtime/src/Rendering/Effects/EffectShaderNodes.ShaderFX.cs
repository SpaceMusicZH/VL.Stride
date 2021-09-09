﻿using Stride.Core;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using VL.Core;
using VL.Model;
using VL.Stride.Shaders.ShaderFX;

namespace VL.Stride.Rendering
{
    static partial class EffectShaderNodes
    {
        static IVLNodeDescription NewShaderFXNode(this IVLNodeDescriptionFactory factory, NameAndVersion name, string shaderName, ShaderMetadata shaderMetadata, IObservable<object> changes, Func<bool> openEditor, IServiceRegistry serviceRegistry, GraphicsDevice graphicsDevice)
        {
            return factory.NewNodeDescription(
                name: name,
                category: "Stride.Rendering.Experimental.ShaderFX",
                tags: shaderMetadata.Tags,
                fragmented: true,
                invalidated: changes,
                init: buildContext =>
                {
                    var outputType = shaderMetadata.GetShaderFXOutputType(out var innerType);
                    var mixinParams = BuildBaseMixin(shaderName, shaderMetadata, graphicsDevice, out var shaderMixinSource);
                    var (_effect, _messages) = CreateEffectInstance("ShaderFXEffect", shaderMetadata, serviceRegistry, graphicsDevice, mixinParams, baseShaderName: shaderName);

                    var _inputs = new List<IVLPinDescription>();
                    var _outputs = new List<IVLPinDescription>() { buildContext.Pin("Output", outputType) };

                    var usedNames = new HashSet<string>();
                    var needsWorld = false;


                    foreach (var parameter in GetParameters(_effect))
                    {
                        var key = parameter.Key;
                        var name = key.Name;

                        if (WellKnownParameters.PerDrawMap.ContainsKey(name))
                        {
                                // Expose World only - all other world dependent parameters we can compute on our own
                                needsWorld = true;
                            continue;
                        }

                        var typeInPatch = shaderMetadata.GetPinType(key, out var boxedDefaultValue);
                        shaderMetadata.GetPinDocuAndVisibility(key, out var summary, out var remarks, out var isOptional);
                        _inputs.Add(new ParameterPinDescription(usedNames, key, parameter.Count, defaultValue: boxedDefaultValue, typeInPatch: typeInPatch) { IsVisible = !isOptional, Summary = summary, Remarks = remarks });
                    }

                        // local input values
                        foreach (var key in shaderMetadata.ParsedShader?.GetUniformInputs() ?? Enumerable.Empty<ParameterKey>())
                    {
                        var name = key.Name;

                        if (WellKnownParameters.PerDrawMap.ContainsKey(name))
                        {
                                // Expose World only - all other world dependent parameters we can compute on our own
                                needsWorld = true;
                            continue;
                        }

                        var typeInPatch = shaderMetadata.GetPinType(key, out var boxedDefaultValue);
                        if (boxedDefaultValue == null)
                            boxedDefaultValue = key.DefaultValueMetadata.GetDefaultValue();

                        shaderMetadata.GetPinDocuAndVisibility(key, out var summary, out var remarks, out var isOptional);

                        _inputs.Add(new ParameterPinDescription(usedNames, key, 1, defaultValue: boxedDefaultValue, typeInPatch: typeInPatch) { IsVisible = !isOptional, Summary = summary, Remarks = remarks });
                    }

                    if (needsWorld)
                        _inputs.Add(new ParameterPinDescription(usedNames, TransformationKeys.World));

                    return buildContext.NewNode(
                        inputs: _inputs,
                        outputs: _outputs,
                        messages: _messages,
                        summary: shaderMetadata.Summary,
                        remarks: shaderMetadata.Remarks,
                        newNode: nodeBuildContext =>
                        {
                            var gameHandle = nodeBuildContext.NodeContext.GetGameHandle();
                            var game = gameHandle.Resource;

                            var tempParameters = new ParameterCollection(); // only needed for pin construction - parameter updater will later take care of multiple sinks
                                var nodeState = new ShaderFXNodeState(shaderName);

                            var inputs = new List<IVLPin>();
                            foreach (var _input in _inputs)
                            {
                                if (_input is ParameterPinDescription parameterPinDescription)
                                    inputs.Add(parameterPinDescription.CreatePin(game.GraphicsDevice, tempParameters));
                            }

                            var outputMaker = typeof(EffectShaderNodes).GetMethod(nameof(BuildOutput), BindingFlags.Static | BindingFlags.NonPublic);
                            outputMaker = outputMaker.MakeGenericMethod(outputType, innerType);
                            outputMaker.Invoke(null, new object[] { nodeBuildContext, nodeState, inputs });

                            return nodeBuildContext.Node(
                                inputs: inputs,
                                outputs: new[] { nodeState.OutputPin },
                                update: default,
                                dispose: () =>
                                {
                                    gameHandle.Dispose();
                                });
                        },
                        openEditor: openEditor
                    );
                });
        }

        // For example T = SetVar<Vector3> and TInner = Vector3
        static void BuildOutput<T, TInner>(NodeBuilding.NodeInstanceBuildContext context, ShaderFXNodeState nodeState, IReadOnlyList<IVLPin> inputPins)
        {
            var compositionPins = inputPins.OfType<ShaderFXPin>().ToList();
            var inputs = inputPins.OfType<ParameterPin>().ToList();

            Func<T> getOutput = () =>
            {
                //check shader fx inputs
                var shaderChanged = nodeState.CurrentComputeNode == null;
                for (int i = 0; i < compositionPins.Count; i++)
                {
                    shaderChanged |= compositionPins[i].ShaderSourceChanged;
                    compositionPins[i].ShaderSourceChanged = false; //change seen
                }

                if (shaderChanged)
                {
                    var comps = compositionPins.Select(p => new KeyValuePair<string, IComputeNode>(p.Key.Name, p.GetValueOrDefault()));

                    var newComputeNode = new GenericComputeNode<TInner>(
                        getShaderSource: (c, k) =>
                        {
                            //let the pins subscribe to the parameter collection of the sink
                            foreach (var pin in inputs)
                                pin.SubscribeTo(c);

                            return new ShaderClassSource(nodeState.ShaderName);
                        },
                        inputs: comps);

                    nodeState.CurrentComputeNode = newComputeNode;

                    if (typeof(TInner) == typeof(VoidOrUnknown))
                        nodeState.CurrentOutputValue = nodeState.CurrentComputeNode;
                    else
                        nodeState.CurrentOutputValue = ShaderFXUtils.DeclAndSetVar(nodeState.ShaderName + "Result", newComputeNode);
                }

                return (T)nodeState.CurrentOutputValue;
            };

            nodeState.OutputPin = context.Output(getOutput);
        }

        class ShaderFXNodeState
        {
            public readonly string ShaderName;
            public IVLPin OutputPin;
            public object CurrentOutputValue;
            public object CurrentComputeNode;

            public ShaderFXNodeState(string shaderName)
            {
                ShaderName = shaderName;
            }
        }
    }
}
