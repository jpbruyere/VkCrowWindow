using System;
using vke;

namespace HelloWorld
{

	public class Program : CrowWindow
	{
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

		static void Main(string[] args)
		{
			using (Program app = new Program ())
				app.Run();
		}

		protected override void initVulkan()
		{
			base.initVulkan();

			loadWindow ("#ui.HelloWorld.crow", this);
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
