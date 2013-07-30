using System;
using Engine;
using Engine.Input;
using Engine.Model;
using Engine.Rendering;
using Engine.Modding;
using Engine.Resources;
using Engine.Scripting;
using Common.Cameras;

using System.Runtime.InteropServices;

namespace UpvoidMiner
{
	/// <summary>
	/// Main class for the local scripting domain.
	/// </summary>
	public class LocalScript
	{
		
		/// <summary>
		/// The main world. We will use this to create new entities or query information about the environment.
		/// </summary>
		static World world;

		/// <summary>
		/// The main camera that renders to the screen.
		/// </summary>
		static GenericCamera camera;

		/// <summary>
		/// A camera controller for free camera movement. Used when noclipEnabled is true.
		/// </summary>
		static FreeCameraControl cameraControl;

		/// <summary>
		/// The player entity. Not to confuse with the Player EntityScript.
		/// </summary>
		static Entity player = null;

		/// <summary>
		/// Set this to true to enable free camera movement.
		/// </summary>
		static bool noclipEnabled = false;

		/// <summary>
		/// This is called by the engine at mod startup and initializes the local part of the UpvoidMiner mod.
		/// </summary>
		public static void Startup(IntPtr _unmanagedModule)
		{
			// Create a simple camera that allows free movement.
			camera = new GenericCamera();
			camera.FarClippingPlane = 750.0;
			cameraControl = new FreeCameraControl(-10f, camera);

			// Get the world (created by the host script).
			world = Universe.GetWorldByName("UpvoidMinerWorld");

			// Place the camera in the world.
			world.AttachCamera(camera);
			if(Rendering.ActiveMainPipeline != null)
				Rendering.ActiveMainPipeline.SetCamera(camera);

			// Create the Player EntityScript and add it to the world.
			// For now, place him 30 meters above the ground and let him drop to the ground.
			player = world.AddEntity(new Player(), mat4.Translate(new vec3(0, 30f, 0)));

			// Configure the camera to receive user input.
			Input.RootGroup.AddListener(cameraControl);

			// Register for input press events.
			Input.OnPressInput += HandlePressInput;

			// Registers the update callback that updates the camera position.
			Scripting.RegisterUpdateFunction(Update, 1 / 60f, 3 / 60f);
		}

		/// <summary>
		/// Performs some basic input handling.
		/// </summary>
		private static void HandlePressInput(object sender, InputPressArgs e)
		{
			// For now, gameplay and debug actions are bound to static keys.

			// N toggles noclip.
			if(e.PressType == InputPressArgs.KeyPressType.Up && e.Key == InputKey.N)
				noclipEnabled = !noclipEnabled;
		}

		/// <summary>
		/// Updates the camera position.
		/// </summary>
		public static void Update(float _elapsedSeconds)
		{
			cameraControl.Update(_elapsedSeconds);

			// Set the camera to the player position if free camera movement is disabled.
			if(!noclipEnabled) {
				camera.Position = player.Position;
			}
		}

		/// <summary>
		/// MonoDevelop's debugger requires an executable program, so here is a dummy Main method.
		/// </summary>
		private static void Main() { throw new Exception("I'm a mod, don't execute me like that!"); }
	}
}