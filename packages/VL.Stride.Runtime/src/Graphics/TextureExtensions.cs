using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;
using VL.Lib.Basics.Imaging;
using VL.Lib.Collections;
using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;
using StridePixelFormat = Stride.Graphics.PixelFormat;
using VLPixelFormat = VL.Lib.Basics.Imaging.PixelFormat;
using Stride.Core;

namespace VL.Stride.Graphics
{
    public static class TextureExtensions
    {

        public static bool TryToTypeless(this StridePixelFormat format, out StridePixelFormat typelessFormat)
        {
            typelessFormat = format;

            var formatString = Enum.GetName(typeof(StridePixelFormat), format);
            var idx = formatString.IndexOf('_');
            
            if (idx > 0)
            {
                formatString = formatString.Remove(idx);
                formatString += "_Typeless";

                if (Enum.TryParse<StridePixelFormat>(formatString, out var newFormat))
                {
                    typelessFormat = newFormat;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Copies the <paramref name="fromData"/> to the given <paramref name="buffer"/> on GPU memory.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="texture"></param>
        /// <param name="commandList">The <see cref="CommandList"/>.</param>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="arraySlice"></param>
        /// <param name="mipSlice"></param>
        /// <param name="region"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <remarks>
        /// See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <returns>The GPU buffer.</returns>
        public static unsafe Texture SetData<TData>(this Texture texture, CommandList commandList, Spread<TData> fromData, int arraySlice, int mipSlice, ResourceRegion? region) where TData : struct
        {
            var immutableArray = fromData._array;
            var array = Unsafe.As<ImmutableArray<TData>, TData[]>(ref immutableArray);
            texture.SetData(commandList, array, arraySlice, mipSlice, region);
            return texture;
        }

        public static unsafe Texture SetDataFromIImage(this Texture texture, CommandList commandList, IImage image, int arraySlice, int mipSlice, ResourceRegion? region)
        {
            using (var data = image.GetData())
            using (var handle = data.Bytes.Pin())
            {
                var dp = new DataPointer(handle.Pointer, data.Bytes.Length);
                texture.SetData(commandList, dp, arraySlice, mipSlice, region);
            }

            return texture;
        }

        /// <summary>
        /// Similiar to <see cref="Texture.Load(GraphicsDevice, Stream, TextureFlags, GraphicsResourceUsage, bool)"/> but allocates memory on unmanaged heap only.
        /// </summary>
        public static unsafe Texture Load(GraphicsDevice device, string file, TextureFlags textureFlags = TextureFlags.ShaderResource, GraphicsResourceUsage usage = GraphicsResourceUsage.Immutable, bool loadAsSRGB = false)
        {
            using var src = File.OpenRead(file);
            var ptr = Utilities.AllocateMemory((int)src.Length);
            using var dst = new UnmanagedMemoryStream((byte*)ptr.ToPointer(), 0, (int)src.Length, FileAccess.ReadWrite);
            src.CopyTo(dst);
            var dataBuffer = new DataPointer(ptr, (int)dst.Length);
            using var image = Image.Load(dataBuffer, makeACopy: false, loadAsSRGB: loadAsSRGB);
            return Texture.New(device, image, textureFlags, usage);
        }

        public static void SaveTexture(this Texture texture, CommandList commandList, string filename, ImageFileType imageFileType = ImageFileType.Png)
        {
            using (var image = texture.GetDataAsImage(commandList))
            {
                using (var resultFileStream = File.OpenWrite(filename))
                {
                    image.Save(resultFileStream, imageFileType);
                }
            }
        }

        public static StridePixelFormat GetStridePixelFormat(ImageInfo info, bool isSRgb = true)
        {
            var format = info.Format;
            switch (format)
            {
                case VLPixelFormat.Unknown:
                    return StridePixelFormat.None;
                case VLPixelFormat.R8:
                    return StridePixelFormat.R8_UNorm;
                case VLPixelFormat.R16:
                    return StridePixelFormat.R16_UNorm;
                case VLPixelFormat.R32F:
                    return StridePixelFormat.R32_Float;
                case VLPixelFormat.R8G8B8X8:
                    return isSRgb ? StridePixelFormat.R8G8B8A8_UNorm_SRgb : StridePixelFormat.R8G8B8A8_UNorm;
                case VLPixelFormat.R8G8B8A8:
                    return isSRgb ? StridePixelFormat.R8G8B8A8_UNorm_SRgb : StridePixelFormat.R8G8B8A8_UNorm;
                case VLPixelFormat.B8G8R8X8:
                    return isSRgb ? StridePixelFormat.B8G8R8X8_UNorm_SRgb : StridePixelFormat.B8G8R8X8_UNorm;
                case VLPixelFormat.B8G8R8A8:
                    return isSRgb ? StridePixelFormat.B8G8R8A8_UNorm_SRgb : StridePixelFormat.B8G8R8A8_UNorm;
                case VLPixelFormat.R32G32F:
                    return StridePixelFormat.R32G32_Float;
                case VLPixelFormat.R32G32B32A32F:
                    return StridePixelFormat.R32G32B32A32_Float;
                default:
                    throw new UnsupportedPixelFormatException(format);
            }
        }

        public static VLPixelFormat GetVLImagePixelFormat(Texture texture, out bool isSRgb)
        {
            isSRgb = false;

            if (texture == null)
                return VLPixelFormat.Unknown;

                var format = texture.Format;
            switch (format)
            {
                case StridePixelFormat.None:
                    return VLPixelFormat.Unknown;
                case StridePixelFormat.R8_UNorm:
                    return VLPixelFormat.R8;
                case StridePixelFormat.R16_UNorm:
                    return VLPixelFormat.R16;
                case StridePixelFormat.R32_Float:
                    return VLPixelFormat.R32F;
                case StridePixelFormat.R8G8B8A8_UNorm:
                    return VLPixelFormat.R8G8B8A8;
                case StridePixelFormat.R8G8B8A8_UNorm_SRgb:
                    isSRgb = true;
                    return VLPixelFormat.R8G8B8A8;
                case StridePixelFormat.B8G8R8X8_UNorm:
                    return VLPixelFormat.B8G8R8X8;
                case StridePixelFormat.B8G8R8X8_UNorm_SRgb:
                    isSRgb = true;
                    return VLPixelFormat.B8G8R8X8;
                case StridePixelFormat.B8G8R8A8_UNorm:
                    return VLPixelFormat.B8G8R8A8;
                case StridePixelFormat.B8G8R8A8_UNorm_SRgb:
                    isSRgb = true;
                    return VLPixelFormat.B8G8R8A8;
                case StridePixelFormat.R32G32_Float:
                    return VLPixelFormat.R32G32F;
                case StridePixelFormat.R32G32B32A32_Float:
                    return VLPixelFormat.R32G32B32A32F;
                default:
                    throw new Exception("Unsupported Pixel Format");
            }
        }

    }
}
