using Stride.Graphics;
using VL.Core;
using VL.Core.CompilerServices;
using VL.Lib.Basics.Resources;

[assembly: AssemblyInitializer(typeof(VL.Stride.Core.Initialization))]

namespace VL.Stride.Core
{
    public sealed class Initialization : AssemblyInitializer<Initialization>
    {
        protected override void RegisterServices(IVLFactory factory)
        {
            Serialization.RegisterSerializers(factory);

            // VL.MediaFoundation asks for a Direct3D11 device
            factory.RegisterService<NodeContext, IResourceProvider<SharpDX.Direct3D11.Device>>(nodeContext =>
            {
                var key = nodeContext.GetResourceKey();
                return ResourceProvider.NewPooled(key, k =>
                {
                    var gameProvider = nodeContext.GetGameProvider();
                    return gameProvider
                        .Bind(game =>
                        {
                            return SharpDXInterop.GetNativeDevice(game.GraphicsDevice) as SharpDX.Direct3D11.Device;
                        });
                });
            });
        }
    }
}