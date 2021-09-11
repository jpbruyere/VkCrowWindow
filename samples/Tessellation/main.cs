// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;
using Glfw;
using vke.glTF;
using System.Collections.Generic;

namespace Tessellation {
	class SimpleModel : PbrModel
	{
		public SimpleModel (Queue transferQ, string path) : base (transferQ, path) {}
		public override void RenderNode(CommandBuffer cmd, PipelineLayout pipelineLayout, Node node, Matrix4x4 currentTransform, bool shadowPass = false)
		{
			Matrix4x4 localMat = node.localMatrix * currentTransform;
			if (node.Mesh != null) {
				foreach (Primitive p in node.Mesh.Primitives)
					cmd.DrawIndexed (p.indexCount, 1, p.indexBase, p.vertexBase, 0);
			}
			if (node.Children == null)
				return;
			foreach (Node child in node.Children)
				RenderNode (cmd, pipelineLayout, child, localMat, shadowPass);
		}
	}
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

		SimpleModel model;
		GraphicPipeline trianglePipeline;

		Matrix4x4 mvp;      //the model view projection matrix
		HostBuffer uboMats; //a host mappable buffer for mvp matrice.

		float tessAlpha = 0.0f, tessLevel = 3.0f;
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

		//We need an additional descriptor for the matrices uniform buffer of the triangle.
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
				cfg.rasterizationState.polygonMode = VkPolygonMode.Line;

				cfg.Layout = new PipelineLayout (dev,
					new DescriptorSetLayout (dev,
						new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
						new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Vertex | VkShaderStageFlags.TessellationEvaluation, VkDescriptorType.UniformBuffer)
				));
				cfg.RenderPass = renderPass;
				cfg.AddVertex<Model.Vertex> ();
				/*cfg.AddVertexBinding<Model.Vertex> (0);
				cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat, VkFormat.R32g32Sfloat);//position + normals + tex*/
				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#shaders.tesselation.vert.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.tesselation.frag.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.TessellationControl, "#shaders.pntriangles2.tesc.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.TessellationEvaluation, "#shaders.pntriangles2.tese.spv")
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

			model = new SimpleModel (presentQueue, "/mnt/devel/vkChess.net/data/models/chess_lowpoly.glb");

			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, (ulong)(Marshal.SizeOf<Matrix4x4>() + 8), true);
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, mainPipeline.Layout.DescriptorSetLayouts[0].Bindings[1]);
			uboUpdate.Write (dev, uboMats.Descriptor);

			loadWindow ("#ui.tessellation.crow", this);
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
			model.Bind (cmd);

			model.RenderNode (cmd, null, model.FindNode ("king"), Matrix4x4.Identity);

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
					model.Dispose ();
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

	}
}
