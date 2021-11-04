using System;
using vke;

namespace HelloWorld
{

	public class Program : CrowWindow
	{
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
