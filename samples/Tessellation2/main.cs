#define TEST_BEZIER

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
namespace Tessellation2 {
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

		HostBuffer vbo;     //a host mappable buffer to hold vertices.
		HostBuffer uboMats; //a host mappable buffer for mvp matrice.


		//bezier line vertices (position + color per vertex) and indices.
#if TEST_BEZIER
		Vertex[] vertices = {
			new Vertex (   0f,    0,   0f,  1.0f, 0.0f, 0.0f),
			new Vertex ( 1.0f, 1.0f, 1.0f,  0.0f, 1.0f, 0.0f),
			new Vertex ( 2.0f, 1.0f, 0.0f,  0.0f, 0.0f, 1.0f),
			new Vertex ( 3.0f, 0.0f, 1.0f,  1.0f, 0.0f, 1.0f),
		};
		string testName => "bezier";
		VkPrimitiveTopology topology => VkPrimitiveTopology.PatchList;
#elif TEST_SPHERE
		Vertex[] vertices = {
			new Vertex (   0f,    0,   0f,  0.3f, 0.0f, 0.0f),
			new Vertex (   0f,    1,   0f,  0.4f, 1.0f, 0.0f)
		};

		string testName => "sphere";
		VkPrimitiveTopology topology => VkPrimitiveTopology.PointList;
#endif

		float tessAlpha = 1.0f, tessLevel = 3.0f;
		public float TessAlpha {
			get => tessAlpha;
			set {
				if (value == tessAlpha)
					return;
				tessAlpha = value;
				NotifyValueChanged (tessAlpha);
				updateViewRequested = true;
			}
		}
		public float TessLevel {
			get => tessLevel;
			set {
				if (value == tessLevel)
					return;
				tessLevel = value;
				NotifyValueChanged (tessLevel);
				updateViewRequested = true;
			}
		}
		protected override void configureEnabledFeatures(VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features)
		{
			if (!available_features.tessellationShader)
				throw new Exception ("tessellation not supported");
			enabled_features.tessellationShader = true;
			enabled_features.fillModeNonSolid = true;
		}

		protected override void CreateAndAllocateDescriptors()
		{
			descriptorPool = new DescriptorPool (dev, 1,
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler),
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));
			descriptorSet = descriptorPool.Allocate (base.mainPipeline.Layout.DescriptorSetLayouts[0]);
		}
		protected override void CreatePipeline()
		{
			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.PatchList, VkSampleCountFlags.SampleCount1, false)) {
				cfg.rasterizationState.polygonMode = VkPolygonMode.Fill;
				cfg.Layout = new PipelineLayout (dev,
					new DescriptorSetLayout (dev,
						new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
						new VkDescriptorSetLayoutBinding (1,
							VkShaderStageFlags.Vertex |
							VkShaderStageFlags.TessellationControl |
							VkShaderStageFlags.TessellationEvaluation,
							VkDescriptorType.UniformBuffer)
				));
				cfg.RenderPass = renderPass;

				cfg.AddVertexBinding<Vertex> (0);
				cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat);//position + color
				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.TessellationControl, $"#shaders.{testName}.tesc.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.TessellationEvaluation, $"#shaders.{testName}.tese.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#shaders.main.vert.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.main.frag.spv")
				);
				cfg.TessellationPatchControlPoints = 3;

				trianglePipeline = new GraphicPipeline (cfg);

				cfg.rasterizationState.polygonMode = VkPolygonMode.Fill;
				cfg.inputAssemblyState.topology = VkPrimitiveTopology.TriangleList;

				cfg.ResetShadersAndVerticesInfos ();
				cfg.blendAttachments[0] = new VkPipelineColorBlendAttachmentState (true);
				cfg.AddShader (dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv");
				cfg.AddShader (dev, VkShaderStageFlags.Fragment, "#VkCrowWindow.simpletexture.frag.spv");

				mainPipeline = new GraphicPipeline (cfg);
			}
		}

		protected override void initVulkan () {
			base.initVulkan ();
			camera.SetPosition (0,0,-4);

			//first create the needed buffers
			vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
			//because mvp matrice may be updated by mouse move, we keep it mapped after creation.
			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer,  (ulong)(Marshal.SizeOf<Matrix4x4>() + 8), true);

			//Write the content of the descriptor, the mvp matrice.
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, mainPipeline.Layout.DescriptorSetLayouts[0].Bindings[1]);
			uboUpdate.Write (dev, uboMats.Descriptor);

			loadWindow ("#ui.main.crow", this);
		}

		//view update override, see base method for more informations.
		public override void UpdateView () {
			camera.AspectRatio = (float)swapChain.Width / swapChain.Height;

			mvp = camera.View * camera.Projection;

			uboMats.Update (mvp, (uint)Marshal.SizeOf<Matrix4x4> ());
			uboMats.Update (tessAlpha, 4, (uint)Marshal.SizeOf<Matrix4x4> ());
			uboMats.Update (tessLevel, 4, (uint)Marshal.SizeOf<Matrix4x4> () + 4);
			base.UpdateView ();
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
			cmd.Draw ((uint)vertices.Length);

			//next blend the ui on top
			cmd.BindPipeline (mainPipeline);
			cmd.Draw (3, 1, 0, 0);

			mainPipeline.RenderPass.End (cmd);
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
					uboMats.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
