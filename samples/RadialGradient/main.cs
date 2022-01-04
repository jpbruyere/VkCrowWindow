// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;
using Glfw;
using shaderc;
using System.IO;

//the traditional triangle sample with crow ui on top.
//a single pipeline is used to output a triangle with the crow ui directly mixed with it.
namespace Triangle {
	class Program : CrowWindow {

		static void Main (string[] args) {
			Instance.VALIDATION = true;
			Instance.RENDER_DOC_CAPTURE = true;
			SwapChain.PREFERED_FORMAT = VkFormat.B8g8r8a8Srgb;

			using (Program app = new Program ())
				app.Run ();
		}


		static string fragPath = @"shaders/radgrad.frag";
		GraphicPipeline trianglePipeline;


		static string fragSource = "";
		bool recompileFragShader = true;
		bool rebuildMainPipeline = true;
		public string FragSource {
			get => fragSource;
			set {
				if (fragSource == value)
					return;
				fragSource = value;
				NotifyValueChanged (fragSource);
				recompileFragShader = true;
			}
		}
		public Crow.ActionCommand CMDSave = new Crow.ActionCommand ("Save", save);

		static void save() {
			using (StreamWriter sw = new StreamWriter(fragPath))
				sw.Write(fragSource);
		}


		//We need an additional descriptor for the matrices uniform buffer of the triangle.
		protected override void CreateAndAllocateDescriptors()
		{
			descriptorPool = new DescriptorPool (dev, 1,
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler),
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));
			descriptorSet = descriptorPool.Allocate (base.mainPipeline.Layout.DescriptorSetLayouts[0]);
		}
		bool tryCompile (Compiler comp, string path, ShaderKind shaderKind, out uint[] uintCode) {
			uintCode = null;
			using (Result res = comp.Compile (path, shaderKind)) {
				Console.WriteLine ($"{path}: {res.Status}");
				if (res.Status != Status.Success) {
					Console.ForegroundColor = ConsoleColor.DarkRed;
					Console.WriteLine ($"\terrs:{res.ErrorCount} warns:{res.WarningCount}");
					Console.WriteLine ($"\t{res.ErrorMessage}");
					Console.ResetColor();
					return false;
				}
				byte[] vCode = new byte[res.CodeLength];
				Marshal.Copy (res.CodePointer, vCode, 0, vCode.Length);
				Span<uint> tmp = MemoryMarshal.Cast<byte, uint> (vCode);
				uintCode = tmp.ToArray();
				return true;
			}
		}
		bool tryCompile (Compiler comp, string source, string path, ShaderKind shaderKind, out uint[] uintCode) {
			uintCode = null;
			using (Result res = comp.Compile (source, path, shaderKind)) {
				Console.WriteLine ($"{path}: {res.Status}");
				if (res.Status != Status.Success) {
					Console.ForegroundColor = ConsoleColor.DarkRed;
					Console.WriteLine ($"\terrs:{res.ErrorCount} warns:{res.WarningCount}");
					Console.ResetColor();
					NotifyValueChanged  ("FragShaderError", (object)res.ErrorMessage);
					NotifyValueChanged  ("FragShaderHasError", true);
					return false;
				}
				NotifyValueChanged  ("FragShaderHasError", false);
				byte[] vCode = new byte[res.CodeLength];
				Marshal.Copy (res.CodePointer, vCode, 0, vCode.Length);
				Span<uint> tmp = MemoryMarshal.Cast<byte, uint> (vCode);
				uintCode = tmp.ToArray();
				return true;
			}
		}
		ShaderInfo vertexShader, fragmentShader;

		public override void Update()
		{
			base.Update();

			if (recompileFragShader) {
				bool updatePL = false;
				using (Compiler comp = new Compiler ()) {
					if (tryCompile (comp, FragSource, fragPath, ShaderKind.FragmentShader, out uint[] code)) {
						fragmentShader?.Dispose();
						fragmentShader = new ShaderInfo (dev, VkShaderStageFlags.Fragment, code, (UIntPtr)(code.Length * 4));
						updatePL = true;
					}
				}
				recompileFragShader = false;
				if (updatePL)
					rebuildPipeline();
			}
		}
		void rebuildPipeline () {
			trianglePipeline?.Dispose();
			CreatePipeline();
			buildCommandBuffers();
		}
		protected override void CreatePipeline()
		{
			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, false)) {
				cfg.Layout = new PipelineLayout (dev);
				cfg.Layout = new PipelineLayout (dev,
					new DescriptorSetLayout (dev,
						new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler)
				));
				cfg.Layout.AddPushConstants (
					new VkPushConstantRange (VkShaderStageFlags.Fragment, (uint)Marshal.SizeOf<Vector2>())
				);
				cfg.RenderPass = renderPass;

				using (Compiler comp = new Compiler ()) {
					vertexShader = new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv");
					if (fragmentShader == null && tryCompile (comp, FragSource, fragPath, ShaderKind.FragmentShader, out uint[] code))
						fragmentShader = new ShaderInfo (dev, VkShaderStageFlags.Fragment, code, (UIntPtr)(code.Length * 4));
				}

				cfg.AddShaders (vertexShader, fragmentShader);

				trianglePipeline = new GraphicPipeline (cfg);

				if (rebuildMainPipeline) {
					cfg.ResetShadersAndVerticesInfos ();
					cfg.blendAttachments[0] = new VkPipelineColorBlendAttachmentState (true);
					cfg.AddShader (dev, VkShaderStageFlags.Vertex, "#vke.FullScreenQuad.vert.spv");
					cfg.AddShader (dev, VkShaderStageFlags.Fragment, "#VkCrowWindow.simpletexture.frag.spv");

					mainPipeline = new GraphicPipeline (cfg);
				}
			}
		}

		protected override void initVulkan () {
			using (StreamReader sr = new StreamReader (fragPath))
				FragSource = sr.ReadToEnd();

			base.initVulkan ();

			loadWindow ("#ui.Triangle.crow", this);
		}

		protected override void onMouseMove (double xPos, double yPos) {
			if (iFace.OnMouseMove ((int)xPos, (int)yPos))
				return;

			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
		}
		protected override void buildCommandBuffer (PrimaryCommandBuffer cmd, int imageIndex) {
			mainPipeline.RenderPass.Begin (cmd, frameBuffers[imageIndex]);

			cmd.SetViewport (swapChain.Width, swapChain.Height);
			cmd.SetScissor (swapChain.Width, swapChain.Height);
			//common layout for both pipelines
			//first draw the triangle
			cmd.BindPipeline (trianglePipeline);
			cmd.PushConstant (mainPipeline.Layout, VkShaderStageFlags.Fragment, new Vector2(Width, Height));
			cmd.Draw (3, 1, 0, 0);
			cmd.BindDescriptorSet (mainPipeline.Layout, descriptorSet);
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
