using System;
using System.Linq;
using VL.Core;
using VL.Lib.Basics.Resources;
using Stride.Engine;
using Stride.Games;
using Stride.Graphics;

namespace VL.Stride
{
    public static class Resources
    {
        public static IResourceProvider<Game> GetGameProvider(this NodeContext nodeContext)
        {
            return nodeContext.Factory.CreateService<IResourceProvider<Game>>(nodeContext);
        }

        public static IResourceHandle<Game> GetGameHandle(this NodeContext nodeContext)
        {
            return nodeContext.GetGameProvider().GetHandle();
        }

        public static IResourceProvider<GameWindow> GetGameWindowProvider(this NodeContext nodeContext)
        {
            return nodeContext.Factory.CreateService<IResourceProvider<GameWindow>>(nodeContext);
        }

        public static IResourceProvider<GraphicsDevice> GetDeviceProvider(this NodeContext nodeContext)
        {
            return nodeContext.Factory.CreateService<IResourceProvider<GraphicsDevice>>(nodeContext);
        }

        public static IResourceHandle<GraphicsDevice> GetDeviceHandle(this NodeContext nodeContext)
        {
            return nodeContext.GetDeviceProvider().GetHandle();
        }
    }
}
