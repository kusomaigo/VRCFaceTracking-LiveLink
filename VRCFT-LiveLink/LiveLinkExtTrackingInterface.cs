using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Runtime.InteropServices;

using System.Diagnostics;

using VRCFaceTracking;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Types;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using System.Drawing.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace LiveLinkExtTrackingInterface
{
    public class LiveLinkExtTrackingInterface : ExtTrackingModule
    {
        //private static CancellationTokenSource _cancellationToken;

        private UdpClient _liveLinkConnection;
        private IPEndPoint _liveLinkRemoteEndpoint;
        private LiveLinkTrackingDataStruct _latestData;

        private (bool, bool) trackingSupported = (false, false);

        private bool disconnectWarned = false;

        //List<Stream> _images = new List<Stream>();

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No connection found!");
        }

        // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            ModuleInformation.Name = "Apple ARKit via LiveLink";

            var stream = GetType().Assembly.GetManifestResourceStream("VRCFT___LiveLink.Assets.iphone-livelink.png");
            ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

            Logger.LogInformation("Initializing Live Link Tracking module");

            //_cancellationToken?.Cancel();
            //UnifiedTrackingData.LatestEyeData.SupportsImage = false;
            //UnifiedTrackingData.LatestLipData.SupportsImage = false;

            // UPD client stuff
            _liveLinkConnection = new UdpClient(Constants.Port);
            //_liveLinkRemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
            _liveLinkRemoteEndpoint = new IPEndPoint(IPAddress.Any, Constants.Port);
            //_liveLinkConnection.Client.Bind(_liveLinkRemoteEndpoint);

            _latestData = new LiveLinkTrackingDataStruct();

            //_liveLinkConnection.Client.SendTimeout = 1000;
            _liveLinkConnection.Client.ReceiveTimeout = 1000;

            // async wait for connection, timeout after timetoWait
            var timeToWait = TimeSpan.FromSeconds(180);

            Logger.LogInformation($"Seeking LiveLink connection for {timeToWait.TotalSeconds} seconds. Accepting data on {GetLocalIPAddress()}:{Constants.Port}");
            var asyncResult = _liveLinkConnection.BeginReceive(null, null);

            asyncResult.AsyncWaitHandle.WaitOne(timeToWait);
            if (asyncResult.IsCompleted)
            {
                try
                {
                    // EndReceive worked and we have received data and remote endpoint
                    byte[] receivedBytes = _liveLinkConnection.EndReceive(asyncResult, ref _liveLinkRemoteEndpoint);
                    Logger.LogInformation("Successful message receive"); 
                    //if (receivedBytes.Length < 244)
                    //{
                    //    // wrong kind of data
                    //    trackingSupported = (false, false);
                    //    return trackingSupported;
                    //}

                }
                catch (Exception ex)
                {
                    // EndReceive failed and we ended up here
                    Logger.LogError($"Error Occurred Attempting Receiving Data: {ex.ToString()}");
                }
            }
            else
            {
                // The operation wasn't completed before the timeout and we're off the hook
                // nothing init so return false
                Logger.LogWarning("Did not receive message from LiveLink within initialization period, re-initialize the module to try again...");
                trackingSupported = (false, false);
                return trackingSupported;
            }

            // Run UpdateTracking once to fill the struct
            UpdateTracking();

            trackingSupported = (true, true);
            return trackingSupported;
        }

        // This will be run in the tracking thread. This is exposed so you can control when and if the tracking data is updated down to the lowest level.
        public override void Update()
        {
            Thread.Sleep(10);
            UpdateTracking();
        }

        // The update function needs to be defined separately in case the user is running with the --vrcft-nothread launch parameter
        public void UpdateTracking()
        {

            if (ReadData(_liveLinkConnection, _liveLinkRemoteEndpoint, ref _latestData))
            {
                // TESTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT
                //Logger.LogInformation($"JawOpen: {_latestData.lowerface.JawOpen.ToString()}");
                // Eye Stuff
                if (trackingSupported.Item1)
                    UpdateEyeData(ref UnifiedTracking.Data.Eye, ref _latestData);
                    UpdateEyeExpressions(ref UnifiedTracking.Data.Shapes, ref _latestData);
                // Mouth Expression Stuff
                if (trackingSupported.Item2)
                    UpdateMouthExpressions(ref UnifiedTracking.Data.Shapes, ref _latestData);
            }
        }

        private void UpdateEyeData(ref UnifiedEyeData eye, ref LiveLinkTrackingDataStruct trackingData)
        {
            #region Eye Openness parsing

            // Wonder if copying the Quest Pro calculation is a good idea
            eye.Left.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, trackingData.left_eye.EyeBlink +
                trackingData.left_eye.EyeBlink * trackingData.left_eye.EyeSquint));

            eye.Right.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, trackingData.right_eye.EyeBlink +
                trackingData.right_eye.EyeBlink * trackingData.right_eye.EyeSquint));
            #endregion

            #region Eye Data to UnifiedEye

            var radianConst = 0.0174533f;

            //var pitch_R_mod = (float)(Math.Abs(pitch_R) + 4f * Math.Pow(Math.Abs(pitch_R) / 30f, 30f)); // curves the tail end to better accomodate actual eye pos.
            //var pitch_L_mod = (float)(Math.Abs(pitch_L) + 4f * Math.Pow(Math.Abs(pitch_L) / 30f, 30f));
            //var yaw_R_mod = (float)(Math.Abs(yaw_R) + 6f * Math.Pow(Math.Abs(yaw_R) / 27f, 18f)); // curves the tail end to better accomodate actual eye pos.
            //var yaw_L_mod = (float)(Math.Abs(yaw_L) + 6f * Math.Pow(Math.Abs(yaw_L) / 27f, 18f));

            // invert pitch, yaw because that's how VRChat likes the values?
            // assuming meowface interface outputs identically to arkit pitch/yaw
            //eye.Right.Gaze = new Vector2(trackingData.right_eye.EyeYaw * radianConst, -1 * trackingData.right_eye.EyePitch * radianConst);
                //pitch_R < 0 ? pitch_R_mod * radianConst : -1 * pitch_R_mod * radianConst,
                //yaw_R < 0 ? -1 * yaw_R_mod * radianConst : (float)yaw_R * radianConst);
            //eye.Left.Gaze = new Vector2(trackingData.left_eye.EyeYaw * radianConst, -1 * trackingData.left_eye.EyePitch * radianConst);
                //pitch_L < 0 ? pitch_L_mod * radianConst : -1 * pitch_L_mod * radianConst,
                //yaw_L < 0 ? -1 * yaw_L_mod * radianConst : (float)yaw_L * radianConst);

            // the raw values seem to be the most right 
            eye.Right.Gaze.x = trackingData.right_eye.EyeYaw;
            eye.Right.Gaze.y = -trackingData.right_eye.EyePitch;
            eye.Left.Gaze.x = trackingData.left_eye.EyeYaw;
            eye.Left.Gaze.y = -trackingData.left_eye.EyePitch;

            // Eye dilation code, automated process maybe?
            eye.Left.PupilDiameter_MM = 5f;
            eye.Right.PupilDiameter_MM = 5f;

            // Force the normalization values of Dilation to fit avg. pupil values.
            eye._minDilation = 0;
            eye._maxDilation = 10;

            #endregion
        }

        private void UpdateEyeExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref LiveLinkTrackingDataStruct trackingData)
        {
            #region Eye Expressions Set

            unifiedExpressions[(int)UnifiedExpressions.EyeWideLeft].Weight = trackingData.left_eye.EyeWide;
            unifiedExpressions[(int)UnifiedExpressions.EyeWideRight].Weight = trackingData.right_eye.EyeWide;

            unifiedExpressions[(int)UnifiedExpressions.EyeSquintLeft].Weight = trackingData.left_eye.EyeSquint;
            unifiedExpressions[(int)UnifiedExpressions.EyeSquintRight].Weight = trackingData.left_eye.EyeSquint;

            #endregion

            #region Brow Expressions Set

            unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = trackingData.brow.BrowInnerUp;
            unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpRight].Weight = trackingData.brow.BrowInnerUp;
            unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = trackingData.brow.BrowOuterUpLeft;
            unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpRight].Weight = trackingData.brow.BrowOuterUpRight;

            unifiedExpressions[(int)UnifiedExpressions.BrowPinchLeft].Weight = trackingData.brow.BrowDownLeft;
            unifiedExpressions[(int)UnifiedExpressions.BrowLowererLeft].Weight = trackingData.brow.BrowDownLeft;
            unifiedExpressions[(int)UnifiedExpressions.BrowPinchRight].Weight = trackingData.brow.BrowDownRight;
            unifiedExpressions[(int)UnifiedExpressions.BrowLowererRight].Weight = trackingData.brow.BrowDownRight;

            #endregion
        }

        private void UpdateMouthExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref LiveLinkTrackingDataStruct trackingData)
        {

            #region Jaw Expression Set                        
            unifiedExpressions[(int)UnifiedExpressions.JawOpen].Weight = trackingData.lowerface.JawOpen;
            unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = trackingData.lowerface.JawLeft;
            unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = trackingData.lowerface.JawRight;
            unifiedExpressions[(int)UnifiedExpressions.JawForward].Weight = trackingData.lowerface.JawForward;
            #endregion

            #region Mouth Expression Set   
            // using Azmidi's meowface module for reference
            unifiedExpressions[(int)UnifiedExpressions.MouthClosed].Weight = trackingData.lowerface.MouthClose;

            // mouth slides to the side
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = trackingData.lowerface.MouthLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = trackingData.lowerface.MouthLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = trackingData.lowerface.MouthRight;
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = trackingData.lowerface.MouthRight;

            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = trackingData.lowerface.MouthSmileLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = trackingData.lowerface.MouthSmileLeft; 
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullRight].Weight = trackingData.lowerface.MouthSmileRight;
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = trackingData.lowerface.MouthSmileRight;
            unifiedExpressions[(int)UnifiedExpressions.MouthFrownLeft].Weight = trackingData.lowerface.MouthFrownLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthFrownRight].Weight = trackingData.lowerface.MouthFrownRight;

            unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = trackingData.lowerface.MouthLowerDownLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownRight].Weight = trackingData.lowerface.MouthLowerDownRight;

            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = trackingData.lowerface.MouthUpperUpLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = trackingData.lowerface.MouthUpperUpLeft; // apparently this is an ok map? 
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = trackingData.lowerface.MouthUpperUpRight;
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = trackingData.lowerface.MouthUpperUpRight; // apparently this is an ok map? 

            unifiedExpressions[(int)UnifiedExpressions.MouthRaiserUpper].Weight = trackingData.lowerface.MouthShrugUpper;
            unifiedExpressions[(int)UnifiedExpressions.MouthRaiserLower].Weight = trackingData.lowerface.MouthShrugLower;

            unifiedExpressions[(int)UnifiedExpressions.MouthDimpleLeft].Weight = trackingData.lowerface.MouthDimpleLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthDimpleRight].Weight = trackingData.lowerface.MouthDimpleRight;

            //unifiedExpressions[(int)UnifiedExpressions.MouthTightenerLeft].Weight = trackingData.lowerface.Mouth
            //unifiedExpressions[(int)UnifiedExpressions.MouthTightenerRight].Weight = expressions[(int)FBExpression.Lip_Tightener_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthPressLeft].Weight = trackingData.lowerface.MouthPressLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthPressRight].Weight = trackingData.lowerface.MouthPressRight;

            unifiedExpressions[(int)UnifiedExpressions.MouthStretchLeft].Weight = trackingData.lowerface.MouthStretchLeft;
            unifiedExpressions[(int)UnifiedExpressions.MouthStretchRight].Weight = trackingData.lowerface.MouthStretchRight;
            #endregion

            #region Lip Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = trackingData.lowerface.MouthPucker;
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = trackingData.lowerface.MouthPucker;
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = trackingData.lowerface.MouthPucker;
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = trackingData.lowerface.MouthPucker;

            unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = trackingData.lowerface.MouthFunnel;
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = trackingData.lowerface.MouthFunnel;
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = trackingData.lowerface.MouthFunnel;
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = trackingData.lowerface.MouthFunnel;

            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Math.Min(1f - (float)Math.Pow(trackingData.lowerface.MouthUpperUpLeft, 1f/6f), trackingData.lowerface.MouthRollUpper);
            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Math.Min(1f - (float)Math.Pow(trackingData.lowerface.MouthUpperUpRight, 1f/6f), trackingData.lowerface.MouthRollUpper);
            unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = trackingData.lowerface.MouthRollLower;
            unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = trackingData.lowerface.MouthRollLower;
            #endregion

            #region Cheek Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = trackingData.lowerface.CheekPuff;
            unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = trackingData.lowerface.CheekPuff;
            //unifiedExpressions[(int)UnifiedExpressions.CheekSuckLeft].Weight = trackingData.lowerface.CheekPuff;
            //unifiedExpressions[(int)UnifiedExpressions.CheekSuckRight].Weight = trackingData.lowerface.CheekPuff;
            unifiedExpressions[(int)UnifiedExpressions.CheekSquintLeft].Weight = trackingData.lowerface.CheekSquintLeft;
            unifiedExpressions[(int)UnifiedExpressions.CheekSquintRight].Weight = trackingData.lowerface.CheekSquintRight;
            #endregion

            #region Nose Expression Set             
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerLeft].Weight = trackingData.lowerface.NoseSneerLeft;
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerRight].Weight = trackingData.lowerface.NoseSneerRight;
            #endregion

            #region Tongue Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.TongueOut].Weight = trackingData.lowerface.TongueOut;
            #endregion
        }

        // A chance to de-initialize everything.
        public override void Teardown()
        {
            // shut down the upd client
            Logger.LogInformation("Closing LiveLink UDP client...");
            _liveLinkConnection.Close();
            _liveLinkConnection.Dispose();
        }

        // ===============================================================

        // Read the data from the LiveLink UDP stream and place it into a LiveLinkTrackingDataStruct
        private bool ReadData(UdpClient liveLinkConnection, IPEndPoint liveLinkRemoteEndpoint, ref LiveLinkTrackingDataStruct trackingData)
        {
            Dictionary<string, float> values = new Dictionary<string, float>();

            try
            {
                // Grab the packet
                // will block but with a timeout set in the init function
                Byte[] receiveBytes = liveLinkConnection.Receive(ref liveLinkRemoteEndpoint);

                if (receiveBytes.Length < 244)
                {
                    return false;
                } 
                
                // got a good message
                if (disconnectWarned)
                {
                    Logger.LogInformation("LiveLink connection reestablished");
                    disconnectWarned = false;
                }

                // There is a bunch of static data at the beginning of the packet, it may be variable length because it includes phone name
                // So grab the last 244 bytes of the packet sent using some Linq magic, since that's where our blendshapes live
                IEnumerable<Byte> trimmedBytes = receiveBytes.Skip(Math.Max(0, receiveBytes.Count() - 244));

                // More Linq magic, this splits our 244 bytes into 61, 4-byte chunks which we can then turn into floats
                List<List<Byte>> chunkedBytes = trimmedBytes
                    .Select((x, i) => new { Index = i, Value = x })
                    .GroupBy(x => x.Index / 4)
                    .Select(x => x.Select(v => v.Value).ToList())
                    .ToList();

                // Process each float in out chunked out list
                foreach (var item in chunkedBytes.Select((value, i) => new { i, value }))
                {
                    // First, reverse the list because the data will be in big endian, then convert it to a float
                    item.value.Reverse();
                    values.Add(Constants.LiveLinkNames[item.i], BitConverter.ToSingle(item.value.ToArray(), 0));
                }
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode == SocketError.TimedOut)
                {
                    if (!disconnectWarned)
                    {
                        Logger.LogWarning("LiveLink connection lost");
                        disconnectWarned = true;
                    }
                } else
                {
                    // some other network socket exception
                    Logger.LogError(se.ToString());
                }
                return false;
            }
            catch (Exception e)
            {
                // some other exception
                Logger.LogError(e.ToString());
                return false;
            }

            // Check that we got all 61 values before we go processing things
            if (values.Count() != 61)
            {
                return false;
            }

            trackingData.ProcessData(values);
            return true; 
        }

    }
}