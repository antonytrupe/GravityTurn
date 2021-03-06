﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GravityTurn
{
    [KSPAddon(KSPAddon.Startup.Flight,false)]
    public class GravityTurner : MonoBehaviour
    {
        public static Vessel vessel { get { return FlightGlobals.ActiveVessel; } }
        protected Rect windowPos = new Rect(50, 100, 300, 200);
        protected Rect helpWindowPos;
        protected bool helpWindowOpen = false;
        protected string helpWindowText = "";
        ApplicationLauncherButton button;
        bool WindowVisible = false;
        float StartSpeed = 100f;
        float HoldAPTime = 50f;
        float APTimeStart = 40f;
        float APTimeFinish = 40f;
        float TurnAngle = 10f;
        float NeutralThrottle = 0.5f;
        double PrevTime = 0;
        MovingAverage Throttle = new MovingAverage(10, 1);
        float Sensitivity = 0.2f;
        float Roll = -90f;
        double DestinationHeight = 80;
        private float lastTimeMeasured = 0.0f;
        public VesselState vesselState = null;
        double TimeSpeed = 0;
        AttitudeController attitude = null;
        StageController stage;
        bool EnableStaging = true;
        bool InPitchProgram = false;
        bool PitchSet = false;
        StageStats stagestats = null;
        float PitchAdjustment = 0;
        float PitchDelay = 0;
        float PitchDelayTime = 8f;
        string Message = "";
        MechjebWrapper mucore = new MechjebWrapper();
        bool Launching = false;

#if DEBUG            
        public static void Log(string message, [CallerMemberName] string callingMethod = "",
         [CallerFilePath] string callingFilePath = "",
         [CallerLineNumber] int callingFileLineNumber = 0)
        {
            Debug.Log(string.Format("{0} - {1} - {2}: {3}", callingMethod, callingFilePath, callingFileLineNumber, message));
#else
        public static void Log(string message)
        {

#endif
        }


        void Start()
        {
            mucore.init();
            vesselState = new VesselState();
            attitude = new AttitudeController(this);
            stage = new StageController(this);
            attitude.OnStart();
            RenderingManager.AddToPostDrawQueue(3, new Callback(drawGUI));//start the GUI
            helpWindowPos = new Rect(windowPos.x+windowPos.width, windowPos.y, 0, 0);
            stagestats = new StageStats();
            stagestats.editorBody = vessel.mainBody;
            stagestats.OnModuleEnabled();
            stagestats.OnFixedUpdate();
            stagestats.RequestUpdate(this);
            stagestats.OnFixedUpdate();
            CreateButtonIcon();
        }
        private void SetWindowOpen()
        {
            if (vessel.mainBody.atmosphereDepth > 0)
                DestinationHeight = vessel.mainBody.atmosphereDepth + 10000;
            else
                DestinationHeight = vessel.mainBody.timeWarpAltitudeLimits[1] + 10000;
            DestinationHeight /= 1000;
            CalculateSettings();
            vessel.OnFlyByWire += new FlightInputCallback(fly);
            WindowVisible = true;
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
        }

        private void CreateButtonIcon()
        {
            button = ApplicationLauncher.Instance.AddModApplication(
                SetWindowOpen,
                Kill,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.FLIGHT,
                GameDatabase.Instance.GetTexture("GravityTurn/Textures/icon", false)
                );
        }

        bool StageHasSolidEngine(int inverseStage)
        {
            foreach (Part p in vessel.parts)
            {
                if (p.inverseStage == inverseStage)
                {
                    foreach (ModuleEngines e in p.FindModulesImplementing<ModuleEngines>())
                    {
                        if (e.engineType == EngineType.SolidBooster)
                            return true;
                    }
                }
            }
            return false;
        }

        void CalculateSettings()
        {
            stagestats.RequestUpdate(this);
            double TWR = 0;
            for (int i = stagestats.atmoStats.Length - 1; i >= 0; i--)
            {
                double stagetwr = stagestats.atmoStats[i].StartTWR(vessel.mainBody.GeeASL);
                Debug.Log(string.Format("Stage {0} TWR {1:0.00}", i, stagetwr));
                if (stagetwr > 0)
                {
                    if (StageHasSolidEngine(i))
                        TWR = (stagetwr + stagestats.atmoStats[i].MaxTWR(vessel.mainBody.GeeASL))/2.3;
                    else
                        TWR = stagetwr;
                    break;
                }
            }
            if (TWR > 1.2)
            {
                TWR -= 1.2;
                TurnAngle = Mathf.Clamp((float)(10 + TWR * 9), 10, 80);
                StartSpeed = Mathf.Clamp((float)(100 - TWR * 45), 10, 100);
            }
        }

        void helpButton(string helpMessage)
        {
            if (GUILayout.Button("?", GUILayout.ExpandWidth(false),GUILayout.ExpandHeight(false),GUILayout.Height(16)))
            {
                helpWindowOpen = true;
                helpWindowText = helpMessage;
            }
        }
        void HelpWindowGUI(int windowID)
        {
            GUIStyle mySty = new GUIStyle(GUI.skin.button);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(8, 8, 8, 8);
            if (GUI.Button(new Rect(helpWindowPos.width - 18, 2, 16, 16), "X"))
            {
                helpWindowOpen = false;
            }
            GUILayout.BeginVertical();
            GUILayout.TextArea(helpWindowText);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));

        }
        private void WindowGUI(int windowID)
        {
            GUIStyle mySty = new GUIStyle(GUI.skin.button);
            mySty.normal.textColor = mySty.focused.textColor = Color.white;
            mySty.hover.textColor = mySty.active.textColor = Color.yellow;
            mySty.onNormal.textColor = mySty.onFocused.textColor = mySty.onHover.textColor = mySty.onActive.textColor = Color.green;
            mySty.padding = new RectOffset(8, 8, 8, 8);
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Start m/s",GUILayout.ExpandWidth(false));
            StartSpeed = float.Parse(GUILayout.TextField(string.Format("{0:0.0}",StartSpeed),GUILayout.Width(40)));
            helpButton("At this speed, pitch to Turn Angle to begin the gravity turn");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Turn Angle", GUILayout.ExpandWidth(false));
            TurnAngle = float.Parse(GUILayout.TextField(string.Format("{0:0.0}", TurnAngle), GUILayout.Width(40)));
            helpButton("Angle to start turn at Start Speed.  Higher values may cause aerodynamic stress.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hold AP Time Start", GUILayout.ExpandWidth(false));
            APTimeStart = float.Parse(GUILayout.TextField(APTimeStart.ToString(), GUILayout.Width(40)));
            helpButton("Starting value for Time To Prograde.\nHigher values will make a steeper climb.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Hold AP Time Finish", GUILayout.ExpandWidth(false));
            APTimeFinish = float.Parse(GUILayout.TextField(APTimeFinish.ToString(), GUILayout.Width(40)));
            helpButton("AP Time will fade to this value, to vary the steepness of the ascent during the ascent.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sensitivity", GUILayout.ExpandWidth(false));
            Sensitivity = float.Parse(GUILayout.TextField(Sensitivity.ToString(), GUILayout.Width(40)));
            helpButton("Will not throttle below this value.  Mostly a factor at the end of ascent.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Destination Height", GUILayout.ExpandWidth(false));
            DestinationHeight = double.Parse(GUILayout.TextField(DestinationHeight.ToString(), GUILayout.Width(40)));
            helpButton("Desired Apoapsis.  May not be final apoapsis after exiting atmosphere due to drag.");
            GUILayout.Label("km", GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Roll", GUILayout.ExpandWidth(false));
            Roll = float.Parse(GUILayout.TextField(Roll.ToString(), GUILayout.Width(40)));
            helpButton("If you want a particular side of your ship to face downwards.  Shouldn't matter for most ships.");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EnableStaging = GUILayout.Toggle(EnableStaging,"Auto-Staging");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Time to match: {0:0.0}",HoldAPTime), GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();
            if (vessel.Landed && !Launching && GUILayout.Button("Best Guess Settings", GUILayout.ExpandWidth(false)))
                CalculateSettings();
            if (vessel.Landed && !Launching && GUILayout.Button("Launch!"))
            {
                if (Staging.CurrentStage == Staging.StageCount)
                    Staging.ActivateNextStage();
                Launching = true;
            }
            GUILayout.Label(Message);
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
            double StopHeight = vessel.mainBody.atmosphereDepth;
            if (StopHeight <= 0)
                StopHeight = DestinationHeight*1000;
            HoldAPTime = APTimeStart + ((float)vessel.altitude / (float)StopHeight * (APTimeFinish - APTimeStart));
            if (HoldAPTime > Math.Max(APTimeFinish, APTimeStart))
                HoldAPTime = Math.Max(APTimeFinish, APTimeStart);
            if (HoldAPTime < Math.Min(APTimeFinish, APTimeStart))
                HoldAPTime = Math.Min(APTimeFinish, APTimeStart);
        }

        void FixedUpdate()
        {
            stagestats.editorBody = vessel.mainBody;
            vesselState.Update(vessel);
            attitude.OnFixedUpdate();
            stagestats.OnFixedUpdate();
        }

        void Update()
        {
            attitude.OnUpdate();
        }

        private void drawGUI()
        {
            if (WindowVisible)
            {
                GUI.skin = HighLogic.Skin;
                windowPos = GUILayout.Window(1, windowPos, WindowGUI, "GravityTurn", GUILayout.MinWidth(300));
                if (helpWindowOpen)
                    helpWindowPos = GUILayout.Window(2, helpWindowPos, HelpWindowGUI, "GravityTurn Help", GUILayout.MinWidth(300));
            }

        }

        private float MaxAngle()
        {
            float angle = 100000 / (float)vesselState.dynamicPressure;
            if (angle > 15)
                return 15;
            return angle;
        }

        private float APThrottle(double timeToAP)
        {
            if (vessel.speed < StartSpeed)
                Throttle.value = 1.0f;
            else if (timeToAP > vessel.orbit.timeToPe) // We're falling
                Throttle.force(1.0f);
            else
            {
                if (timeToAP > vessel.orbit.timeToPe) // We're falling
                    timeToAP = 0;
                float diff = 0.1f * (float)Math.Abs(HoldAPTime - timeToAP) * Sensitivity;
                TimeSpeed = (PrevTime - timeToAP) / (Time.time - lastTimeMeasured);
                if (Math.Abs(TimeSpeed) < 0.02 && PitchAdjustment==0)
                    NeutralThrottle = (float)Throttle.value;
                if (Math.Abs(timeToAP - HoldAPTime) < 0.1)
                {
                    if (PitchAdjustment > 0)
                        PitchAdjustment -= 0.1f;
                    else
                        Throttle.force(NeutralThrottle);
                }
                else if (timeToAP < HoldAPTime)
                {
                    if (Throttle.value >= 1 && timeToAP < PrevTime)
                    {
                        NeutralThrottle = 1;
                        PitchAdjustment += 0.1f;
                    }
                    Throttle.value += diff;
                }
                else if (timeToAP > HoldAPTime)
                {
                    if (PitchAdjustment > 0)
                        PitchAdjustment -= 0.1f;
                    else
                        Throttle.value -= diff;
                }
            }
            if (PitchAdjustment < 0)
                PitchAdjustment = 0;
            if (PitchAdjustment > MaxAngle())
                PitchAdjustment = MaxAngle();
            PrevTime = vessel.orbit.timeToAp;
            lastTimeMeasured = Time.time;
            if (Throttle.value < Sensitivity)
                Throttle.force(Sensitivity);
            if (Throttle.value > 1)
                Throttle.force(1);
            return (float)Throttle.value;
        }

        private void Kill()
        {
            Launching = false;
            vessel.OnFlyByWire -= new FlightInputCallback(fly);
            WindowVisible = false;
            FlightInputHandler.state.mainThrottle = 0;
            attitude.enabled = false;
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
        }

        private void fly(FlightCtrlState s)
        {
            if (!Launching)
                return;
            if (!WindowVisible) 
                Kill();
            else if (vessel.orbit.ApA > DestinationHeight*1000 && vessel.altitude > vessel.mainBody.atmosphereDepth)
            {
                if (TimeWarp.CurrentRateIndex > 0)
                    TimeWarp.SetRate(0, true);
                if (mucore.Initialized)
                    mucore.CircularizeAtAP();
                Kill();
            }
            else
            {
                if (EnableStaging)
                    stage.Update();
                if (vessel.orbit.ApA < DestinationHeight * 1000)
                    s.mainThrottle = APThrottle(vessel.orbit.timeToAp);
                else
                    s.mainThrottle = 0;
                //if (InPitchProgram && attitude.attitudeGetReferenceRotation(AttitudeReference.SURFACE_NORTH).eulerAngles.x > Quaternion.Euler(-90 + TurnAngle, 90, Roll).eulerAngles.x)
                if (InPitchProgram && PitchSet)
                {
                    if (PitchDelay > 0 && PitchDelay < Time.time)
                        InPitchProgram = false;
                    else if (PitchDelay == 0 && attitude.attitudeAngleFromTarget() < 0.5)
                        PitchDelay = Time.time + PitchDelayTime;
                }
                if (vessel.speed < StartSpeed)
                {
                    InPitchProgram = true;
                    attitude.attitudeTo(Quaternion.Euler(-90, 90, Roll), AttitudeReference.SURFACE_NORTH, this);
                }
                else if (InPitchProgram)
                {
                    attitude.attitudeTo(Quaternion.Euler(-90 + TurnAngle, 90, Roll), AttitudeReference.SURFACE_NORTH, this);
                    PitchSet = true;
                }
                else if (vessel.altitude < vessel.mainBody.atmosphereDepth * 0.5)
                {
                    attitude.attitudeTo(Quaternion.Euler(0 - PitchAdjustment, 0, Roll), AttitudeReference.SURFACE_VELOCITY, this);
                }
                else
                {
                    attitude.attitudeTo(Quaternion.Euler(0 - PitchAdjustment, 0, Roll), AttitudeReference.ORBIT, this);
                }
                attitude.enabled = true;
                attitude.Drive(s);
                if (Math.Abs(s.roll) > 0.2)
                    s.roll *= 0.2f;
            }
        }

        void OnDestroy()
        {
            Kill();
            ApplicationLauncher.Instance.RemoveModApplication(button);
        }

    }
}
