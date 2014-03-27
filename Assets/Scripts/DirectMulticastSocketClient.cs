/**
 * Adapted from johny3212
 * Written by Matt Oskamp
 */

using UnityEngine;
using System.Collections;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

namespace OptitrackManagement
{
	
	public class DirectStateObject 
	{
		public Socket workSocket = null;
		public const int BufferSize = 65507;
		public byte[] buffer = new byte[BufferSize];
		public StringBuilder sb = new StringBuilder();
	}
	
	public static class DirectMulticastSocketClient {
		
		private static Socket client;
		private static bool _isInitRecieveStatus = false;
		private static bool _isIsActiveThread = false;
		private static DataStream _dataStream = null;
		private static String _strFrameLog = String.Empty;
		
		private static int _dataPort = 1511;
		//private static int _commandPort = 1510;
		private static string _multicastIPAddress = "239.255.42.99";
		
		private static void StartClient() 
		{
			// Connect to a remote device.
			try
			{
				
				Debug.Log("[UDP] Starting client");
				_dataStream = new DataStream();
				client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				//client.ExclusiveAddressUse = false;
				
				IPEndPoint ipep=new IPEndPoint(IPAddress.Any, _dataPort);
				client.Bind(ipep);
				
				IPAddress ip=IPAddress.Parse(_multicastIPAddress);
				client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(ip,IPAddress.Any));
				
				_isInitRecieveStatus = Receive(client);
				_isIsActiveThread = _isInitRecieveStatus;
				
			} catch (Exception e) 
			{
				Debug.LogError("[UDP] DirectMulticastSocketClient: " + e.ToString());
			}
		}
		
		private static bool Receive(Socket client) 
		{
			try 
			{
				// Create the state object.
				DirectStateObject state = new DirectStateObject();
				state.workSocket = client;
				
				Debug.Log("[UDP multicast] Receive");
				
				// Begin receiving the data from the remote device.
				client.BeginReceive( state.buffer, 0, DirectStateObject.BufferSize, 0,
				                    new AsyncCallback(ReceiveCallback), state);
				
			} catch (Exception e) 
			{
				Debug.Log(e.ToString());
				return false;
			}
			
			return true;
		}
		
		private static void ReceiveCallback( IAsyncResult ar ) 
		{
			try {
				//Debug.Log("[UDP multicast] Start ReceiveCallback");
				// Retrieve the state object and the client socket 
				// from the asynchronous state object.
				DirectStateObject state = (DirectStateObject) ar.AsyncState;
				Socket client = state.workSocket;
				
				// Read data from the remote device.
				int bytesRead = client.EndReceive(ar);
				
				if (bytesRead > 0 && _isIsActiveThread) 
				{            
					ReadPacket(state.buffer);
					
					//client.Shutdown(SocketShutdown.Both);
					//client.Close();   
					
					client.BeginReceive(state.buffer,0,DirectStateObject.BufferSize,0,
					                    new AsyncCallback(ReceiveCallback), state);
				} else 
				{
					Debug.LogWarning("[UDP] - End ReceiveCallback");
					
					if(_isIsActiveThread == false)
					{
						Debug.LogWarning("[UDP] - Closing port");
						_isInitRecieveStatus = false;
						//client.Shutdown(SocketShutdown.Both);
						client.Close();   
					}
					
					// Signal that all bytes have been received.
				}
			} catch (Exception e) 
			{
				Debug.LogError(e.ToString());
			}
			
		}
		
		private static void ReadPacket(Byte[] b)
		{
			int offset = 0;
			int nBytes = 0;
			int[] iData = new int[100];
			float[] fData = new float[500];

			Buffer.BlockCopy(b, offset, iData, 0, 2); offset += 2;
			int messageID = iData[0];
			
			Buffer.BlockCopy(b, offset, iData, 0, 2); offset += 2;
			nBytes = iData[0];
			
			//Debug.Log("[UDPClient] Processing Received Packet (Message ID : " + messageID + ")");
			if (messageID == 5)      // Data descriptions
			{
				Debug.Log("DirectParseClient: Data descriptions");
				
			}else if (messageID == 7)   // Frame of Mocap Data
			{
				_strFrameLog = String.Format("DirectParseClient: [UDPClient] Read FrameOfMocapData: {0}\n", nBytes);
				Buffer.BlockCopy(b, offset, iData, 0, 4); offset += 4;
				_strFrameLog += String.Format("Frame # : {0}\n", iData[0]);
				
				//number of data sets (markersets, rigidbodies, etc)
				Buffer.BlockCopy(b, offset, iData, 0, 4); offset += 4;
				int nMarkerSets = iData[0];
				_strFrameLog += String.Format("MarkerSets # : {0}\n", iData[0]);
				
				for (int i = 0; i < nMarkerSets; i++)
				{
					String strName = "";
					int nChars = 0;
					while (b[offset + nChars] != '\0')
					{
						nChars++;
					}
					strName = System.Text.Encoding.ASCII.GetString(b, offset, nChars);
					offset += nChars + 1;
					
					Buffer.BlockCopy(b, offset, iData, 0, 4); offset += 4;
					_strFrameLog += String.Format("{0}:" + strName + ": marker count : {1}\n", i, iData[0]);
					
					nBytes = iData[0] * 3 * 4;
					Buffer.BlockCopy(b, offset, fData, 0, nBytes); offset += nBytes;
					//do not need   
				}
				
				// Other Markers - All 3D points that were triangulated but not labeled for the given frame.
				Buffer.BlockCopy(b, offset, iData, 0, 4); offset += 4;
				_strFrameLog += String.Format("Other Markers : {0}\n", iData[0]);
				nBytes = iData[0] * 3 * 4;
				Buffer.BlockCopy(b, offset, fData, 0, nBytes); offset += nBytes;   
				
				// Rigid Bodies
				//RigidBody rb = new RigidBody();
				Buffer.BlockCopy(b, offset, iData, 0, 4); offset += 4;
				_dataStream._nRigidBodies = iData[0];
				_strFrameLog += String.Format("Rigid Bodies : {0}\n", iData[0]);
				for (int i = 0; i < _dataStream._nRigidBodies; i++)
				{
					ReadRigidBody(b, ref offset, _dataStream._rigidBody[i]);
				}   
				
				//Debug.Log(_strFrameLog);   
				
			}else if (messageID == 100)
			{
				
			}
			
		}
		
		// Unpack RigidBody data
		private static void ReadRigidBody(Byte[] b, ref int offset, OptiTrackRigidBody rb)
		{
			try
			{
				int[] iData = new int[100];
				float[] fData = new float[100];
				
				// RB ID
				Buffer.BlockCopy(b, offset, iData, 0, 4); offset += 4;
				//int iSkelID = iData[0] >> 16;           // hi 16 bits = ID of bone's parent skeleton
				//int iBoneID = iData[0] & 0xffff;       // lo 16 bits = ID of bone
				rb.ID = iData[0]; // already have it from data descriptions
				
				// RB pos
				float[] pos = new float[3];
				Buffer.BlockCopy(b, offset, pos, 0, 4 * 3); offset += 4 * 3;
				rb.position.x = pos[0]; rb.position.y = pos[1]; rb.position.z = pos[2];
				
				// RB ori
				float[] ori = new float[4];
				Buffer.BlockCopy(b, offset, ori, 0, 4 * 4); offset += 4 * 4;
				rb.orientation.x = ori[0]; rb.orientation.y = ori[1]; rb.orientation.z = ori[2]; rb.orientation.w = ori[3];
				Buffer.BlockCopy(b, offset, iData, 0, 4); offset += 4;
				int nMarkers = iData[0];
				Buffer.BlockCopy(b, offset, fData, 0, 4 * 3 * nMarkers); offset += 4 * 3 * nMarkers;
				
				Buffer.BlockCopy(b, offset, iData, 0, 4 * nMarkers); offset += 4 * nMarkers;
				
				Buffer.BlockCopy(b, offset, fData, 0, 4 * nMarkers); offset += 4 * nMarkers;
				
				Buffer.BlockCopy(b, offset, fData, 0, 4); offset += 4;         
			} catch (Exception e)
			{
				Debug.LogError(e.ToString());
			}
		}
		
		// Use this for initialization
		public static void Start () 
		{
			StartClient();
		}
 
		public static void Close () 
		{
			_isIsActiveThread = false;
		}   
		public static bool IsInit()
		{
			return _isInitRecieveStatus;
		}
		public static DataStream GetDataStream()
		{
			return _dataStream;
		}   
	}
}