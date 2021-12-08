// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;
using Glfw;

//the traditional triangle sample with crow ui on top.
//a single pipeline is used to output a triangle with the crow ui directly mixed with it.
namespace Triangle {
	class Program : CrowWindow {
#if NETCOREAPP
		static IntPtr resolveUnmanaged (System.Reflection.Assembly assembly, String libraryName) {
			switch (libraryName) {
				case "glfw3":
					return  System.Runtime.InteropServices.NativeLibrary.Load("glfw", assembly, null);
				case "rsvg-2.40":
					return  System.Runtime.InteropServices.NativeLibrary.Load("rsvg-2", assembly, null);
			}
			return IntPtr.Zero;
		}
		static Program () {
			System.Runtime.Loader.AssemblyLoadContext.Default.ResolvingUnmanagedDll+=resolveUnmanaged;
		}
#endif
		static void Main (string[] args) {
			Instance.VALIDATION = true;
			//Instance.RENDER_DOC_CAPTURE = true;

			using (Program app = new Program ())
				app.Run ();
		}

		const float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, zoom = 1f;

		//vertex structure
		[StructLayout(LayoutKind.Sequential)]
		struct Vertex {
			Vector3 position;
			Vector3 color;

			public Vertex (float x, float y, float z, float r, float g, float b) {
				position = new Vector3 (x, y, z);
				color = new Vector3 (r, g, b);
			}
		}
		GraphicPipeline trianglePipeline;

		Matrix4x4 mvp;      //the model view projection matrix

		HostBuffer ibo;     //a host mappable buffer to hold the indices.
		HostBuffer vbo;     //a host mappable buffer to hold vertices.
		HostBuffer uboMats; //a host mappable buffer for mvp matrice.

		//triangle vertices (position + color per vertex) and indices.
		Vertex[] vertices = {
			new Vertex ( -1.00f, -0.25f,  0.0f ,  1.0f, 0.0f, 0.0f),
			new Vertex (  0.25f, -0.25f,  0.0f ,  1.0f, 0.0f, 0.0f),
			new Vertex (  0.25f,  1.00f,  0.0f ,  1.0f, 0.0f, 0.0f),
			new Vertex ( -1.00f,  1.00f,  0.0f ,  1.0f, 0.0f, 0.0f),

			new Vertex ( -0.25f, -1.00f,  0.0f ,  0.0f, 0.0f, 1.0f),
			new Vertex (  1.00f, -1.00f,  0.0f ,  0.0f, 0.0f, 1.0f),
			new Vertex (  1.00f,  0.25f,  0.0f ,  0.0f, 0.0f, 1.0f),
			new Vertex ( -0.25f,  0.25f,  0.0f ,  0.0f, 0.0f, 1.0f),
		};
		ushort[] indices = new ushort[] { 0, 2, 3, 0, 1, 2, 4, 6, 7, 4, 5, 6 };

		//We need an additional descriptor for the matrices uniform buffer of the triangle.
		protected override void CreateAndAllocateDescriptors()
		{
			descriptorPool = new DescriptorPool (dev, 1,
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler),
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));
			descriptorSet = descriptorPool.Allocate (base.mainPipeline.Layout.DescriptorSetLayouts[0]);
		}

		PipelineLayout pipelineLayout;
		bool rebuildPipeline = true;
		//create the main pipeline that blend the scene with the ui
		protected override void CreatePipeline()
		{
			pipelineLayout = new PipelineLayout (dev,
					new DescriptorSetLayout (dev,
						new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
						new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer)
				));
			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, false)) {
				cfg.Layout = pipelineLayout;
				cfg.RenderPass = renderPass;
				cfg.blendAttachments[0] = new VkPipelineColorBlendAttachmentState(true);
				cfg.AddShader (dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv");
				cfg.AddShader (dev, VkShaderStageFlags.Fragment, "#VkCrowWindow.simpletexture.frag.spv");
				mainPipeline = new GraphicPipeline (cfg);
			}

			createTrianglePipeline ();
		}
		//override the default renderPass creation to add a custom background color with the rd cleal load operation.
		protected override void CreateRenderPass () {
			renderPass = new RenderPass (dev, swapChain.ColorFormat, VkSampleCountFlags.SampleCount1);
			renderPass.ClearValues[0] = new VkClearValue (0.0f, 1.0f, 0);
		}
		//create the scene pipeline, here a simple 2d drawer with editable blend state for the color output.
		void createTrianglePipeline () {
			dev.WaitIdle ();

			trianglePipeline?.Dispose ();

			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, false)) {
				cfg.Layout = pipelineLayout;
				cfg.RenderPass = renderPass;
				cfg.blendAttachments[0] = blendState;
				cfg.AddVertexBinding<Vertex> (0);
				cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat);//position + color
				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#shaders.triangle.vert.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.triangle.frag.spv")
				);

				trianglePipeline = new GraphicPipeline (cfg);
			}
			rebuildPipeline = false;

			dev.WaitIdle ();
		}

		protected override void initVulkan () {
			base.initVulkan ();

			//first create the needed buffers
			vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
			ibo = new HostBuffer<ushort> (dev, VkBufferUsageFlags.IndexBuffer, indices);
			//because mvp matrice may be updated by mouse move, we keep it mapped after creation.
			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, mvp, true);

			//Write the content of the descriptor, the mvp matrice.
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, mainPipeline.Layout.DescriptorSetLayouts[0].Bindings[1]);
			uboUpdate.Write (dev, uboMats.Descriptor);

			loadWindow ("#ui.Triangle.crow", this);
		}

		//view update override, see base method for more informations.
		public override void UpdateView () {
			mvp =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -3f * zoom) *
				Helpers.CreatePerspectiveFieldOfView (Helpers.DegreesToRadians (45f), (float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f);

			uboMats.Update (mvp, (uint)Marshal.SizeOf<Matrix4x4> ());
			base.UpdateView ();
		}
		protected override void onMouseMove (double xPos, double yPos) {
			if (iFace.OnMouseMove ((int)xPos, (int)yPos))
				return;

			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (GetButton (MouseButton.Left) == InputAction.Press) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
				updateViewRequested = true;
			} else if (GetButton (MouseButton.Right) == InputAction.Press) {
				zoom += zoomSpeed * (float)diffY;
				updateViewRequested = true;
			}
		}
		protected override void buildCommandBuffer (PrimaryCommandBuffer cmd, int imageIndex) {
			mainPipeline.RenderPass.Begin (cmd, frameBuffers[imageIndex]);

			cmd.SetViewport (swapChain.Width, swapChain.Height);
			cmd.SetScissor (swapChain.Width, swapChain.Height);
			//common layout for both pipelines
			cmd.BindDescriptorSet (mainPipeline.Layout, descriptorSet);
			//first draw the triangle
			cmd.BindPipeline (trianglePipeline);
			cmd.BindVertexBuffer (vbo);
			cmd.BindIndexBuffer (ibo, VkIndexType.Uint16);
			cmd.DrawIndexed (6,1,0);
			cmd.DrawIndexed (6,1,6);
			//next blend the ui on top
			cmd.BindPipeline (mainPipeline);
			cmd.Draw (3, 1, 0, 0);

			mainPipeline.RenderPass.End (cmd);
		}
		public override void Update()
		{
			if (rebuildPipeline) {
				createTrianglePipeline ();
				buildCommandBuffers ();
			}
			base.Update();
		}

		protected override void OnResize () {
			base.OnResize ();

			UpdateView ();
		}
		//clean up
		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();
			if (disposing) {
				if (!isDisposed) {
					trianglePipeline.Dispose ();
					vbo.Dispose ();
					ibo.Dispose ();
					uboMats.Dispose ();
				}
			}

			base.Dispose (disposing);
		}


		public int CrowUpdateInterval {
			get => Crow.Interface.UPDATE_INTERVAL;
			set {
				if (Crow.Interface.UPDATE_INTERVAL == value)
					return;
				Crow.Interface.UPDATE_INTERVAL = value;
				NotifyValueChanged (Crow.Interface.UPDATE_INTERVAL);
			}
		}
		public long VkeUpdateInterval {
			get => UpdateFrequency;
			set {
				if (UpdateFrequency == value)
					return;
				UpdateFrequency = value;
				NotifyValueChanged (UpdateFrequency);
			}
		}
		VkPipelineColorBlendAttachmentState blendState = new VkPipelineColorBlendAttachmentState (true);
		public VkBlendFactor srcColorBlendFactor {
			get => blendState.srcColorBlendFactor;
			set {
				if (srcColorBlendFactor == value)
					return;
				blendState.srcColorBlendFactor = value;
				NotifyValueChanged (srcColorBlendFactor);
				rebuildPipeline = true;
			}
		}
		public VkBlendFactor srcAlphaBlendFactor {
			get => blendState.srcAlphaBlendFactor;
			set {
				if (srcAlphaBlendFactor == value)
					return;
				blendState.srcAlphaBlendFactor = value;
				NotifyValueChanged (srcAlphaBlendFactor);
				rebuildPipeline = true;
			}
		}
		public VkBlendFactor dstColorBlendFactor {
			get => blendState.dstColorBlendFactor;
			set {
				if (dstColorBlendFactor == value)
					return;
				blendState.dstColorBlendFactor = value;
				NotifyValueChanged (dstColorBlendFactor);
				rebuildPipeline = true;
			}
		}
		public VkBlendFactor dstAlphaBlendFactor {
			get => blendState.dstAlphaBlendFactor;
			set {
				if (dstAlphaBlendFactor == value)
					return;
				blendState.dstAlphaBlendFactor = value;
				NotifyValueChanged (dstAlphaBlendFactor);
				rebuildPipeline = true;
			}
		}
	}
}
