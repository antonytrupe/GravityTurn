﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using UnityEngine;

namespace GravityTurn
{
    public static class VesselExtensions
    {
        public static bool LiftedOff(this Vessel v)
        {
            return Staging.CurrentStage != Staging.StageCount;
        }
        public static ManeuverNode PlaceManeuverNode(this Vessel vessel, Orbit patch, Vector3d dV, double UT)
        {
            //placing a maneuver node with bad dV values can really mess up the game, so try to protect against that
            //and log an exception if we get a bad dV vector:
            for (int i = 0; i < 3; i++)
            {
                if (double.IsNaN(dV[i]) || double.IsInfinity(dV[i]))
                {
                    throw new Exception("VesselExtensions.PlaceManeuverNode: bad dV: " + dV);
                }
            }

            if (double.IsNaN(UT) || double.IsInfinity(UT))
            {
                throw new Exception("VesselExtensions.PlaceManeuverNode: bad UT: " + UT);
            }

            //It seems that sometimes the game can freak out if you place a maneuver node in the past, so this
            //protects against that.
            UT = Math.Max(UT, Planetarium.GetUniversalTime());

            //convert a dV in world coordinates into the coordinate system of the maneuver node,
            //which uses (x, y, z) = (radial+, normal-, prograde)
            Vector3d nodeDV = patch.DeltaVToManeuverNodeCoordinates(UT, dV);
            ManeuverNode mn = vessel.patchedConicSolver.AddManeuverNode(UT);
            mn.OnGizmoUpdated(nodeDV, UT);
            return mn;
        }
        /*private static Vessel vessel { get { return FlightGlobals.ActiveVessel; } }
        //public static Vector3d CoM { get { return vessel.findWorldCenterOfMass(); } }

        public static Vector3d CoM(this Vessel v)
        {
            return v.findWorldCenterOfMass();
        }
        public static Vector3d up(this Vessel v)
        {
            return (v.CoM() - v.mainBody.position).normalized;

        }

        public static Vector3d orbitalVelocity(this Vessel v)
        {
            return v.obt_velocity;
        }

        public static Quaternion rotationSurface(this Vessel v)
        {
            Vector3d north = Vector3d.Exclude(v.up(), (v.mainBody.position + v.mainBody.transform.up * (float)v.mainBody.Radius) - v.CoM()).normalized;
            Vector3d east = v.mainBody.getRFrmVel(v.CoM()).normalized;
            Vector3d forward = v.GetTransform().up;
            return Quaternion.LookRotation(north, v.up());
        }

        public static Vector3d surfaceVelocity(this Vessel v)
        {
            return v.obt_velocity - v.mainBody.getRFrmVel(v.CoM());
        }

        public static Vector3d normalPlus(this Vessel v)
        {
            Vector3d radialPlus = Vector3d.Exclude(v.orbitalVelocity(), v.up()).normalized;
            return -Vector3d.Cross(radialPlus, v.orbitalVelocity().normalized);

        }

        public static Vector3d forward(this Vessel v)
        {
            return v.GetTransform().up;
        }

        public static Vector3d torqueAvailable(this Vessel v)
        {
            Vector3d torque = Vector3d.zero;
            foreach (ModuleReactionWheel rw in vessel.FindPartModulesImplementing<ModuleReactionWheel>())
            {
                torque += new Vector3d(rw.PitchTorque, rw.RollTorque, rw.YawTorque);
            }
            return torque;
        }*/
    }
}
