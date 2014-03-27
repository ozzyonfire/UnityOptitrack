/**
 * Adapted from johny3212
 * Written by Matt Oskamp
 */
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;

namespace OptitrackManagement
{
	
	// marker
	public class Marker
	{
		public int ID = -1;
		public Vector3 pos;      
	}
	
	// Rigidbody
	public class OptiTrackRigidBody
	{
		public string name = "";
		public int ID = -1;
		public int parentID = -1;
		public Vector3 position;
		public Quaternion orientation;
	}
}