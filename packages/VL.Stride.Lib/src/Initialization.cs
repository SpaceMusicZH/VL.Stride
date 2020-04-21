﻿using System.Linq;
using VL.Core;
using VL.Core.CompilerServices;
using VL.Lib.Basics.Resources;
using VL.Xenko;
using VL.Xenko.Assets;
using VL.Xenko.Games;
using Xenko.Engine;
using Xenko.Games;
using Xenko.Graphics;

[assembly: AssemblyInitializer(typeof(VL.Stride.Lib.Initialization))]

namespace VL.Stride.Lib
{
    public sealed class Initialization : AssemblyInitializer<Initialization>
    {
        protected override void RegisterServices(IVLFactory factory)
        {
            factory.RegisterService<NodeContext, IResourceProvider<Game>>(nodeContext =>
            {
                var rootId = nodeContext.Path.Stack.Last();
                return ResourceProvider.NewPooled(rootId,
                    factory: _ =>
                    {
                        var game = new VLGame();
#if DEBUG
                        game.GraphicsDeviceManager.DeviceCreationFlags |= DeviceCreationFlags.Debug;
#endif

                        var assetBuildService = new AssetBuildService();
                        game.Services.AddService(assetBuildService);

                        var gameStartedHandler = default(System.EventHandler);
                        gameStartedHandler = (s, e) =>
                        {
                            game.Script.Add(assetBuildService);
                            Game.GameStarted -= gameStartedHandler;
                        };
                        Game.GameStarted += gameStartedHandler;

                        var gameContext = new GameContextWinforms(null, 0, 0, isUserManagingRun: true);
                        game.Run(gameContext); // Creates the window
                        game.RunCallback = gameContext.RunCallback;

                        game.AddLayerRenderFeature();

                        return game;
                    }
                    , delayDisposalInMilliseconds: 0);
            });
        }
    }
}