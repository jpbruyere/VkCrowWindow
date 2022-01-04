// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Glfw;
using Vulkan;
using Crow;
using System.Threading;
using System.Runtime.CompilerServices;
using Drawing2D;

namespace vke {
	/// <summary>
	/// Vulkan context with Crow enabled window.
	/// Crow vector drawing is handled with Cairo Image on an Host mapped vulkan image.
	/// This is an easy way to have GUI in my samples with low GPU cost. Most of the ui
	/// is cached on cpu memory images.
	/// </summary>
	public class CrowWindow : VkWindow, ICommandHost {
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged (string MemberName, object _value)
		{
			ValueChanged?.Invoke (this, new ValueChangeEventArgs (MemberName, _value));
		}
		public void NotifyValueChanged (object _value, [CallerMemberName] string caller = null)
		{
			NotifyValueChanged (caller, _value);
		}
		#endregion

		protected CrowWindow(string windowTitle = "VkCrowWindow") : base (
			windowTitle,
			Configuration.Global.Get<uint> ("Width", 800),
			Configuration.Global.Get<uint> ("Height", 600), false) {}
		public bool MouseIsInInterface =>
			iFace.HoverWidget != null;

		/// <summary>Final pipeline to target the swapchain.</summary>
		protected GraphicPipeline mainPipeline;
		/// <summary>descriptor pool for the final pipeline</summary>
		protected DescriptorPool descriptorPool;
		/// <summary>descriptor set for the final pipeline.</summary>
		protected DescriptorSet descriptorSet;
		CommandPool cmdPoolCrow;				//crow ui upload command pool
		PrimaryCommandBuffer cmdUpdateCrow;		//crow ui upload command buff
		Image crowImage;						//ui texture to output to swapchain
		HostBuffer crowBuffer;					//vkBuffer used as backend memory for the main crow surface
		/// <summary>the crow interface</summary>
		protected VkCrowInterface iFace;
		/// <summary>the main renderpass on swapchain.</summary>
		protected RenderPass renderPass;
		/// <summary>the main multi-framebuffer for the swapchain.</summary>
		protected FrameBuffers frameBuffers;

		volatile bool running;


		VkDescriptorSetLayoutBinding dslBinding = new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler);

#if DEBUG
		public override string[] EnabledInstanceExtensions => new string[] {
			Ext.I.VK_EXT_debug_utils,
		};
		vke.DebugUtils.Messenger dbgmsg;
#endif
		protected override void initVulkan () {
			camera = new Camera (Helpers.DegreesToRadians (45), Width / Height, 0.1f, 32f);
			camera.Type = Camera.CamType.LookAt;
			camera.SetPosition (0, 0, -10);

			base.initVulkan ();

#if DEBUG
			dbgmsg = new vke.DebugUtils.Messenger (instance, VkDebugUtilsMessageTypeFlagsEXT.PerformanceEXT | VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT | VkDebugUtilsMessageTypeFlagsEXT.GeneralEXT,
				VkDebugUtilsMessageSeverityFlagsEXT.InfoEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.VerboseEXT);
#endif

			CreateRenderPass ();

			CreatePipeline ();

			cmdPoolCrow = new CommandPool (presentQueue, VkCommandPoolCreateFlags.ResetCommandBuffer);
			cmdUpdateCrow = cmdPoolCrow.AllocateCommandBuffer ();

			CreateAndAllocateDescriptors ();

			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);

			Interface.UPDATE_INTERVAL = 5;
			UpdateFrequency = 30;

			Interface.CrowAssemblyNames = new string[] {"VkCrowWindow"};

			initCrowSurface ();

			iFace = new VkCrowInterface (WindowHandle, crowBuffer, crowImage);
			iFace.Init ();

			Thread ui = new Thread (crowThread);
			ui.IsBackground = true;
			ui.Start ();
		}
		/// <summary>
		/// Create and allocate the Descriptors for the main pipeline.
		/// The default one is a single image descriptor for the UI texture set
		/// as binding 0
		/// </summary>
		protected virtual void CreateAndAllocateDescriptors () {
			descriptorPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler));
			descriptorSet = descriptorPool.Allocate (mainPipeline.Layout.DescriptorSetLayouts[0]);
		}
		/// <summary>
		/// Create main rendering pipeline that should output to swapchain.
		/// The default one is a simple full screen quad draw pipeline textured with
		/// the crow ui resulting image.
		/// </summary>
		protected virtual void CreatePipeline () {
			mainPipeline = new FSQPipeline (renderPass,
				new PipelineLayout (dev, new DescriptorSetLayout (dev, dslBinding)));
		}
		/// <summary>
		/// Create the main RenderPass for the swapchain.
		/// The default one is a single color RP with loading operator set to Clear.
		/// If you want to draw the ui on top of another vulkan rendering with a dedicated
		/// renderPass, the loadOp of the ui renderPass should be set to 'Load' to blend the ui with the rendering output.
		/// </summary>
		protected virtual void CreateRenderPass () {
			renderPass = new RenderPass (dev, swapChain.ColorFormat, VkSampleCountFlags.SampleCount1);
		}
		/// <summary>
		/// Record command for the target swapchain image index to produce the final rendering.
		/// The default command simply draw a fullscreen quad with the UI texture.
		/// </summary>
		/// <param name="cmd">The recording command buffer, must be a primary one</param>
		/// <param name="imageIndex">The swapchain image index to output to.</param>
		protected virtual void buildCommandBuffer (PrimaryCommandBuffer cmd, int imageIndex) {
			renderPass.Begin(cmd, frameBuffers[imageIndex]);

			cmd.SetViewport (frameBuffers[imageIndex].Width, frameBuffers[imageIndex].Height);
			cmd.SetScissor (frameBuffers[imageIndex].Width, frameBuffers[imageIndex].Height);

			mainPipeline.BindDescriptorSet (cmd, descriptorSet);
			mainPipeline.Bind (cmd);
			cmd.Draw (3, 1, 0, 0);

			renderPass.End (cmd);
		}
		/// <summary>
		/// Build command buffers bound to the swapchain, one per swapchain image.
		/// First Logical device IDLE state is awaited, the main command pool is reseted,
		/// then `VkCrowWindow.buildCommandBuffer` method is called for each image.
		/// </summary>
		protected virtual void buildCommandBuffers () {
			dev.WaitIdle ();
			cmdPool.Reset ();
			for (int i = 0; i < swapChain.ImageCount; ++i) {
				cmds [i].Start ();
				buildCommandBuffer (cmds[i], i);
				cmds [i].End ();
			}
		}


		#region vke overrides
		/// <summary>
		/// Override the default `VkWindow.Update` method.
		/// This is where the transfer between Crow(gui) and vke(vulkan) happens.
		/// If crow interface is dirty (iFace.IsDirty), the crow resulting image is
		/// uploaded to a vulkan texture and the dirty state is reseted.
		/// The vke update frequency may be tuned with 'UpdateFrequency' wich is the
		/// minimal time in milliseconds between call to the Update method by the
		/// vke rendering loop (the App.Run()).
		/// </summary>
		public override void Update () {
			if (iFace.IsDirty) {
				drawFence.Wait ();
				drawFence.Reset ();
				Monitor.Enter (iFace.UpdateMutex);
				presentQueue.Submit (cmdUpdateCrow, default, default, drawFence);
				iFace.IsDirty = false;
			}

			NotifyValueChanged ("fps", fps);
		}
		/// <summary>
		/// Override the default `OnResize` of `vke.VkWindow`. The crow backend surface is resize,
		/// the swapchain frame buffers are recreated and finaly the `buildCommandBuffers` method is called.
		/// </summary>
		protected override void OnResize ()
		{
			base.OnResize ();
			lock (iFace.UpdateMutex) {
				dev.WaitIdle ();
				iFace.MainSurface.Dispose();
				iFace.uiImage.Dispose();
				iFace.crowBuffer.Dispose();
				initCrowSurface ();
				iFace.uiImage = crowImage;
				iFace.crowBuffer = crowBuffer;
				dev.WaitIdle ();

				iFace.ProcessResize (new Rectangle (0,0,(int)Width, (int)Height));
			}
			frameBuffers?.Dispose();
			frameBuffers = renderPass.CreateFrameBuffers(swapChain);

			buildCommandBuffers();
		}
		/// <summary>
		/// The main rendering loop, overrided from the VkWindow to add the crow interface update.
		/// </summary>
		protected override void render () {

			int idx = swapChain.GetNextImage ();
			if (idx < 0) {
				OnResize ();
				return;
			}

			if (cmds[idx] == null)
				return;

			drawFence.Wait ();
			drawFence.Reset ();

			if (Monitor.IsEntered (iFace.UpdateMutex))
				Monitor.Exit (iFace.UpdateMutex);

			presentQueue.Submit (cmds[idx], swapChain.presentComplete, drawComplete[idx], drawFence);
			presentQueue.Present (swapChain, drawComplete[idx]);
		}
		/// <summary>
		/// The overridable method of the dispose pattern.
		/// </summary>
		/// <param name="disposing">True if disposed by use</param>
		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();

			running = false;
			frameBuffers?.Dispose();
			mainPipeline?.Dispose ();
			descriptorPool.Dispose ();
			cmdPoolCrow.Dispose ();
			crowImage?.Dispose ();
			crowBuffer?.Dispose ();
			iFace.Dispose ();
#if DEBUG
			dbgmsg?.Dispose ();
#endif

			Configuration.Global.Set ("Width", Width);
			Configuration.Global.Set ("Height", Height);

			base.Dispose (disposing);
		}
		#endregion

		#region Mouse and Keyboard routing between vke and crow
		public event EventHandler<KeyEventArgs> KeyDown;

		protected override void onMouseMove (double xPos, double yPos)
		{
			if (iFace.OnMouseMove ((int)xPos, (int)yPos))
				return;
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;

			if (GetButton (MouseButton.Left) == InputAction.Press)
				camera.Rotate ((float)-diffY, (float)-diffX);
			else if (GetButton (MouseButton.Right) == InputAction.Press)
				camera.Move (0, 0, (float)diffY * 0.2f);
			else if (GetButton (MouseButton.Middle) == InputAction.Press)
				camera.Move ((float)diffX * -0.2f, (float)diffY * 0.2f, 0);
			else
				return;
			updateViewRequested = true;
		}
		protected override void onMouseButtonDown (MouseButton button, Modifier mods) {
			if (iFace.OnMouseButtonDown (button))
				return;
			base.onMouseButtonDown (button, mods);
		}
		protected override void onMouseButtonUp (MouseButton button, Modifier mods)
		{
			if (iFace.OnMouseButtonUp (button))
				return;
			base.onMouseButtonUp (button, mods);
		}
		protected override void onScroll (double xOffset, double yOffset) {
			if (iFace.OnMouseWheelChanged ((float)yOffset))
				return;
			base.onScroll (xOffset, yOffset);
		}
		protected override void onChar (CodePoint cp) {
			if (iFace.OnKeyPress (cp.ToChar()))
				return;
			base.onChar (cp);
		}
		protected override void onKeyUp (Key key, int scanCode, Modifier modifiers) {
			if (iFace.OnKeyUp (new KeyEventArgs (key, scanCode, modifiers)))
				return;
			base.onKeyUp (key, scanCode, modifiers);
		}
		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			KeyEventArgs e = new KeyEventArgs (key, scanCode, modifiers);
			if (KeyDown != null)
				KeyDown.Raise (this, e);
			if (e.Handled || iFace.OnKeyDown (e))
				return;
			base.onKeyDown (key, scanCode, modifiers);
		}
		#endregion

		/// <summary>
		/// The crow update thread where the layouting and so on are computed and the
		/// drawing is done for the ui.
		/// The update interval may be controled with the static field of the Crow.Interface class
		/// 'Interface.UPDATE_INTERVAL', which is a delay in milliseconds between the update of crow.
		/// </summary>
		void crowThread () {
			/*while (iFace.surf == null) {
				Thread.Sleep (10);
			}*/
			running = true;
			while (running) {
				iFace.Update ();
				Thread.Sleep (Interface.UPDATE_INTERVAL);
			}
		}
		/// <summary>
		/// Create the main Crow surface as a data surface pointing to a vulkan buffer visible by host.
		/// The buffer is created, as well as a VkImage to bind to the rendering pipeline that will receive
		/// the ui buffer content.
		/// The command to upload the buffer to the texture is also created.
		/// </summary>
		void initCrowSurface () {
			crowBuffer = new HostBuffer (dev, VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, Width * Height * 4, true);

			crowImage = new Image (dev, VkFormat.B8g8r8a8Srgb, VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
				VkMemoryPropertyFlags.DeviceLocal, Width, Height, VkImageType.Image2D, VkSampleCountFlags.SampleCount1, VkImageTiling.Linear);
			crowImage.CreateView (VkImageViewType.ImageView2D, VkImageAspectFlags.Color);
			crowImage.CreateSampler (VkFilter.Nearest, VkFilter.Nearest, VkSamplerMipmapMode.Nearest, VkSamplerAddressMode.ClampToBorder);
			crowImage.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;

			DescriptorSetWrites dsw = new DescriptorSetWrites (descriptorSet, dslBinding);
			dsw.Write (dev, crowImage.Descriptor);

			//iFace.surf = iFace.CreateSurfaceForData (crowBuffer.MappedData, (int)Width, (int)Height);

			PrimaryCommandBuffer cmd = cmdPoolCrow.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);
			crowImage.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.Preinitialized, VkImageLayout.ShaderReadOnlyOptimal);
			presentQueue.EndSubmitAndWait (cmd, true);

			recordUpdateCrowCmd ();
		}

		/// <summary>
		/// Create the vulkan command buffer to upload the crow ui image to vulkan texture from
		/// the host visible VkBuffer used as rendering surface's memory backend.
		/// This command will be triggered by the VkWindow.Update method if the dirty state of crow is true.
		/// </summary>
		void recordUpdateCrowCmd () {
			cmdPoolCrow.Reset ();
			cmdUpdateCrow.Start ();
			crowImage.SetLayout (cmdUpdateCrow, VkImageAspectFlags.Color,
				VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferDstOptimal,
				VkPipelineStageFlags.FragmentShader, VkPipelineStageFlags.Transfer);

			crowBuffer.CopyTo (cmdUpdateCrow, crowImage, VkImageLayout.ShaderReadOnlyOptimal);

			crowImage.SetLayout (cmdUpdateCrow, VkImageAspectFlags.Color,
				VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
				VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader);
			cmdUpdateCrow.End ();
		}

		#region crow ui interface loading methods
		/// <summary>
		/// Load IML file or embedded resource (if prefixed with '#'). The name of
		/// the root widget will be set with the `path` provided as first argument.
		/// If the current interface has already loaded this IML file, the existing one will
		/// be returned and put on top.
		/// This will also allow  to call the closeWindow method with the `path` as argument.
		/// </summary>
		/// <remarks>
		/// This method may be used to load any widget class, not only window.
		/// </remarks>
		/// <param name="path">the IML resource path, embeded or on disk.</param>
		/// <param name="dataSource">Optional datasource</param>
		/// <returns>The newly created widget instance</returns>
		protected Widget loadWindow (string path, object dataSource = null) {
			Widget w = null;
			try {
				w = iFace.FindByName (path);
				if (w != null)
					iFace.PutOnTop (w);
				else {
					w = iFace.Load (path);
					w.Name = path;
					w.DataSource = dataSource;
				}
			} catch (Exception ex) {
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine ($"VkCrowWindo: error loading interface ({path})");
				Console.WriteLine (ex);
				Console.ResetColor();
			}
			return w;
		}
		/// <summary>
		/// Load IML fragment string into this interface.
		/// </summary>
		/// <param name="imlFragment">The IML source in plain text.</param>
		/// <param name="dataSource">Optional datasource</param>
		protected void loadIMLFragment (string imlFragment, object dataSource = null) {
			iFace.LoadIMLFragment (imlFragment).DataSource = dataSource;
		}
		/// <summary>
		/// Load IML fragment string into this interface and return the root widget
		/// </summary>
		/// <param name="imlFragment">The IML source in plain text.</param>
		/// <param name="dataSource">Optional datasource</param>
		/// <typeparam name="T">The type of the root widget of the IML fragment.</typeparam>
		/// <returns>The newly created widget of type T</returns>
		protected T loadIMLFragment<T> (string imlFragment, object dataSource = null) {
			Widget tmp = iFace.LoadIMLFragment (imlFragment);
			tmp.DataSource = dataSource;
			return (T)Convert.ChangeType (tmp,typeof(T));
		}
		/// <summary>
		/// Close a top level widget loaded with the `loadWindow` method by providing
		/// it's IML file path.
		/// </summary>
		/// <param name="path">The IML resource path.</param>
		protected void closeWindow (string path) {
			Widget g = iFace.FindByName (path);
			if (g != null)
				iFace.DeleteWidget (g);
		}
		#endregion

	}
}
