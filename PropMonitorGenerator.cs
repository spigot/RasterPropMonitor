using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RasterPropMonitorGenerator
{
	public class RasterPropMonitorGenerator: InternalModule
	{
		[KSPField]
		public int refreshRate = 20;
		//[KSPField]
		//public int refreshResourceRate = 20;
		[KSPField]
		public string page1 = "Display$$$ not$$$  configured.";
		[KSPField]
		public string button1 = "";
		[KSPField]
		public string page2 = "";
		[KSPField]
		public string button2 = "";
		[KSPField]
		public string page3 = "";
		[KSPField]
		public string button3 = "";
		[KSPField]
		public string page4 = "";
		[KSPField]
		public string button4 = "";
		[KSPField]
		public string page5 = "";
		[KSPField]
		public string button5 = "";
		[KSPField]
		public string page6 = "";
		[KSPField]
		public string button6 = "";
		[KSPField]
		public string page7 = "";
		[KSPField]
		public string button7 = "";
		[KSPField]
		public string page8 = "";
		[KSPField]
		public string button8 = "";
		// Config syntax.
		private string[] lineSeparator = { Environment.NewLine };
		private string[] variableListSeparator = { "###" };
		private string[] variableSeparator = { "|" };
		private InternalModule targetScript;
		private string[] textArray;
		// Important pointers to the screen's data structures.
		FieldInfo remoteArray;
		FieldInfo remoteFlag;
		// Local variables
		private string[] pages = { "", "", "", "", "", "", "", "" };
		private int activePage = 0;
		private int charPerLine = 23;
		private int linesPerPage = 17;
		private string spacebuffer;
		private int vesselNumParts;
		private int updateCountdown;
		private bool updateForced = false;
		private bool screenWasBlanked = false;
		// Data common for various variable calculations
		Vector3d CoM;
		Vector3d up;
		Vector3d north;
		Quaternion rotationVesselSurface;
		Quaternion rotationSurface;
		ITargetable target;

		public void Start ()
		{
			// Mihara: We're getting at the screen module and it's parameters using reflection here.
			// While I would prefer to use some message passing mechanism instead,
			// it does not look like I can use KSPEvent.
			// I could directly lock at the parameters, seeing as how these two modules
			// are in the same assembly, but instead I'm leaving the reflection-based mechanism here
			// so that you could make your own screen driver module
			// by simply copy-pasting the relevant sections.
			foreach (InternalModule intModule in base.internalProp.internalModules) {
				if (intModule.ClassName == "RasterPropMonitor") {
					targetScript = intModule;
					remoteArray = intModule.GetType ().GetField ("screenText");
					remoteFlag = intModule.GetType ().GetField ("screenUpdateRequired");

					charPerLine = (int)intModule.GetType ().GetField ("screenWidth").GetValue (intModule);
					linesPerPage = (int)intModule.GetType ().GetField ("screenHeight").GetValue (intModule);

					break;
				}
			}

			spacebuffer = new String (' ', charPerLine);

			string[] pageData = new string[] { page1, page2, page3, page4, page5, page6, page7, page8 };
			string[] buttonName = new string[] { button1, button2, button3, button4, button5, button6, button7, button8 };
			for (int i=0; i<8; i++) {
				//Debug.Log ("RasterMonitor: Page " + i.ToString () + " data is \"" + pageData [i] + "\" button name is " + buttonName [i]);
				if (buttonName [i] != "") {
					GameObject buttonObject = base.internalProp.FindModelTransform (buttonName [i]).gameObject;
					buttonHandler pageButton = buttonObject.AddComponent<buttonHandler> ();
					pageButton.ID = i;
					pageButton.handlerFunction = buttonClick;
				}

				try {
					pages [i] = String.Join (Environment.NewLine, File.ReadAllLines (KSPUtil.ApplicationRootPath + "GameData/" + pageData [i], System.Text.Encoding.UTF8));
				} catch {
					// Notice that this will also happen if the referenced file is not found.
					pages [i] = pageData [i].Replace ("<=", "{").Replace ("=>", "}").Replace ("$$$", Environment.NewLine);
				}
			}


			textArray = new string[linesPerPage];
			for (int i = 0; i < textArray.Length; i++) {
				textArray [i] = "";
			}

		}

		public void buttonClick (int buttonID)
		{
			activePage = buttonID;
			updateForced = true;
		}
		// Some snippets from MechJeb...
		private double ClampDegrees360 (double angle)
		{
			angle = angle % 360.0;
			if (angle < 0)
				return angle + 360.0;
			else
				return angle;
		}
		//keeps angles in the range -180 to 180
		private double ClampDegrees180 (double angle)
		{
			angle = ClampDegrees360 (angle);
			if (angle > 180)
				angle -= 360;
			return angle;
		}
		// Has quite a bit of MechJeb code which I barely understand.
		private void fetchCommonData ()
		{
			CoM = vessel.findWorldCenterOfMass ();
			up = (CoM - vessel.mainBody.position).normalized;
			north = Vector3d.Exclude (up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - CoM).normalized;
			rotationSurface = Quaternion.LookRotation (north, up);
			rotationVesselSurface = Quaternion.Inverse (Quaternion.Euler (90, 0, 0) * Quaternion.Inverse (vessel.GetTransform ().rotation) * rotationSurface);
			target = FlightGlobals.fetch.VesselTarget;
		}

		private object processVariable (string input)
		{
			switch (input) {

			// It's a bit crude, but it's simple enough to populate.
			// Would be a bit smoother if I had eval() :)
			case "ALTITUDE":
				return vessel.mainBody.GetAltitude(CoM);
			case "RADARALT":
				return vessel.altitude - Math.Max (vessel.pqsAltitude, 0D);
			case "VERTSPEED":
				return FlightGlobals.ship_verticalSpeed;
			case "SURFSPEED":
				return FlightGlobals.ship_srfSpeed;
			case "ORBTSPEED":
				return FlightGlobals.ship_obtSpeed;
			case "TRGTSPEED":
				return FlightGlobals.ship_tgtSpeed;
			case "HORZVELOCITY":
				return  Vector3d.Exclude (up, vessel.orbit.GetVel ().normalized - vessel.mainBody.getRFrmVel (CoM)).normalized;
			case "PERIAPSIS":
				return FlightGlobals.ship_orbit.PeA;
			case "APOAPSIS":
				return FlightGlobals.ship_orbit.ApA;
			case "INCLINATION":
				return FlightGlobals.ship_orbit.inclination;
			case "LATITUDE":
				return vessel.mainBody.GetLatitude (CoM);
			case "LONGITUDE":
				return ClampDegrees180 (vessel.mainBody.GetLongitude (CoM));
			case "HEADING":
				return rotationVesselSurface.eulerAngles.y;
			case "PITCH":
				return (rotationVesselSurface.eulerAngles.x > 180) ? (360.0 - rotationVesselSurface.eulerAngles.x) : -rotationVesselSurface.eulerAngles.x;
			case "ROLL":
				return (rotationVesselSurface.eulerAngles.z > 180) ? (rotationVesselSurface.eulerAngles.z - 360.0) : rotationVesselSurface.eulerAngles.z;
			case "TARGETNAME":
				if (target == null)
					return "";
				if (target is Vessel || target is CelestialBody)
					return target.GetName ();
					// Later, I think I want to get this to return the ship's name, not the docking node name...
				if (target is ModuleDockingNode)
					return target.GetName ();
				return "???!";
			case "ORBITBODY":
				return vessel.orbit.referenceBody.name;
			case "ECCENTRICITY":
				return vessel.orbit.eccentricity;
			case "TARGETDISTANCE":
				if (FlightGlobals.fetch.VesselTarget != null) {
					return Vector3.Distance (FlightGlobals.fetch.VesselTarget.GetTransform ().position, vessel.GetTransform ().position);
				} else
					return Double.NaN;
			case "RELATIVEINCLINATION":
				if (FlightGlobals.fetch.VesselTarget != null) {
					Orbit targetorbit = FlightGlobals.fetch.VesselTarget.GetOrbit ();
					if (targetorbit.referenceBody != vessel.orbit.referenceBody)
						return Double.NaN;
					// Not finished.
					return "Dunno...";
				} else
					return Double.NaN;
			}
			return input;
		}

		private string processString (string input)
		{
			// Each separate output line is delimited by Environment.NewLine.
			// When loading from a config file, you can't have newlines in it, so they're represented by "$$$".
			//
			// Within each line, if it contains any variables, it contains String.Format's format codes:
			// "Insert {0:0.0} variables {0:0.0} into this string###VARIABLE|VARIABLE"
			// 
			// <= has to be substituted for { and => for } when defining a screen in a config file.
			// It is much easier to write a text file and reference it by URL instead, writing 
			// screen definitions in a config file is only good enough for very small screens.
			// 
			// A more readable string format reference detailing where each variable is to be inserted and 
			// what it should look like can be found here: http://blog.stevex.net/string-formatting-in-csharp/

			if (input.IndexOf (variableListSeparator [0]) >= 0) {

				string[] tokens = input.Split (variableListSeparator, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length != 2) {
					return "FORMAT ERROR";
				} else {
					string[] vars = tokens [1].Split (variableSeparator, StringSplitOptions.RemoveEmptyEntries);

					object[] variables = new object[vars.Length];
					for (int i=0; i<vars.Length; i++) {
						//Debug.Log ("PropMonitorGenerator: Processing " + vars[i]);
						variables [i] = processVariable (vars [i]);
					}
					return String.Format (tokens [0], variables);
				}
			} else
				return input;
		}
		// Update according to the given refresh rate or when number of parts changes.
		private bool updateCheck ()
		{
			if (vesselNumParts != vessel.Parts.Count || updateCountdown <= 0 || updateForced) {
				updateCountdown = refreshRate;
				vesselNumParts = vessel.Parts.Count;
				updateForced = false;
				return true;
			} else {
				updateCountdown--;
				return false;
			}
		}

		public override void OnUpdate ()
		{
			if (!HighLogic.LoadedSceneIsFlight)
				return;

			if (!updateCheck ())
				return;

			if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA && vessel == FlightGlobals.ActiveVessel) {

				for (int i = 0; i < textArray.Length; i++)
					textArray [i] = spacebuffer;

				if (pages [activePage] == "") { // In case the page is empty, the screen is treated as turned off and blanked once.
					if (!screenWasBlanked) {
						screenWasBlanked = true;
						remoteArray.SetValue (targetScript, textArray);
						remoteFlag.SetValue (targetScript, true);
					}
				} else {
					string[] linesArray = pages [activePage].Split (lineSeparator, StringSplitOptions.None);
					fetchCommonData (); // Doesn't seem to be a better place to do it in...
					for (int i=0; i<linesArray.Length && i<linesPerPage; i++) {
						textArray [i] = processString (linesArray [i]) + spacebuffer;
					}
					remoteArray.SetValue (targetScript, textArray);
					remoteFlag.SetValue (targetScript, true);
					screenWasBlanked = false;
				}

			}
		}
	}

	public class buttonHandler:MonoBehaviour
	{
		public delegate void HandlerFunction (int ID);

		public HandlerFunction handlerFunction;
		public int ID;

		public void OnMouseDown ()
		{
			handlerFunction (ID);
		}
	}
}

