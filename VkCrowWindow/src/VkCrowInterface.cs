// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Crow;
using Drawing2D;

namespace vke
{
	public class VkTextureBackend : Crow.CairoBackend.ImageBackend {
		public IntPtr textureDataHandle;
		public VkTextureBackend (IntPtr mappedData, int width, int height)
		: base (mappedData, width, height, 4 * width) {
		}
		public override void ResizeMainSurface(int width, int height)
		{
			surf = new Crow.CairoBackend.ImageSurface(textureDataHandle, Format.ARGB32, width, height, 4 * width);
		}
	}
	public class VkCrowInterface : Interface {
		public Image uiImage;
		public HostBuffer crowBuffer;
		public VkCrowInterface (IntPtr glfwWinHandle, HostBuffer crowBuffer, Image uiImage)
		: base ((int)uiImage.Width, (int)uiImage.Height, glfwWinHandle) {
			this.uiImage = uiImage;
			this.crowBuffer = crowBuffer;
			backend = new VkTextureBackend (crowBuffer.MappedData, (int)uiImage.Width, (int)uiImage.Height);
			clipping = Backend.CreateRegion ();
		}
		public override void ProcessResize(Rectangle bounds)
		{
			VkTextureBackend vkb = backend as VkTextureBackend;
			vkb.textureDataHandle = crowBuffer.MappedData;
			base.ProcessResize(bounds);
		}
	}
}
