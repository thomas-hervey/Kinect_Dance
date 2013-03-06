//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System.IO;
    using System;
    using System.Windows;
    using System.Windows.Media;
    using System.Diagnostics;
    using System.Threading;
    using System.Collections.Generic;
    using Microsoft.Kinect;



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /***************** User-created constants and variables *******************************************************************************************/
        /**************************************************************************************************************************************************/
        



        /* Match pose and dance function variables */


        // skeletonPose variable that is extracted from poseArray when it's found to match currentStreamPose  ***********
        private skeletonPose matchedSavedPose;

        // Timer that holds the elapsed time since a racognized match  ***********
        private TimeSpan timeSinceMatch;




        /* On load, saving and capturing variables */


        // Pose Capture function counter
        private int numPosesCaptured = 0;

        // Path to the text file containing joint names the user wants to check with each according captured pose
        private String JOINTS_TO_CHECK_PATH = "C:\\Users\\KinectDance\\Documents\\SkeletonRepo\\Working\\texts\\jointsToCheck\\";

        //Path to pose text files folder where pose text files will be written out
        private String POSES_FOLDER_PATH = "C:\\Users\\KinectDance\\Documents\\SkeletonRepo\\Working\\texts\\poses\\Pose#_";




        /* Stream variables */


        // skeletonPose array to hold 6 different poses captured before a performance
        private skeletonPose[] poseArray = new skeletonPose[6];

        // skeletonPose variable that is constantly updated directly from the skeleton stream
        private skeletonPose currentStreamPose;

        /// <summary>                                                                                                                                                
        /// Struct declaration that will house all of the joint angles and time stamp of a skeleton 
        /// *Note, neckAngle & centerShoulderAngle both use the centerShoulder as the middle 'joint'*
        /// </summary>
        struct skeletonPose
        {
            /// Map normal names to joint array indices
            /// </summary>
            public enum JointLabels
            {
                rightWristAngle, rightElbowAngle, rightShoulderAngle, leftWristAngle, leftElbowAngle, leftShoulderAngle, rightAnkleAngle,
                rightKneeAngle, rightHipAngle, leftAnkleAngle, leftKneeAngle, leftHipAngle, spineAngle, neckAngle, centerShoulderAngle,
                numJoints
            };

            // Joints array that holds a pose's angles
            public double[] Joints;

            // Joint name & index dictionary to store in each pose
            public Dictionary<string, int> jointFromName;


            // Time variable started when pose is captured in getPose()
            public DateTime timeElapsed;
        }

        /***************************************************************************************************************************************************/


        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }

            //Added on load function to check if the user wants to use prerecorded poses or create new ones
            if (containsPoseFiles())
            {
                /* Prompt the user and ask if they want to start a new recording session
                 * or if they want to use the current poses in '.../poses'
                 */

                /* If they want to start a new recording session, just continue on
                 * with the program as usual (capture button enabled)
                 */

                /* If they want to dance then disable capture button and load '.../poses' files
                 * into poseArray
                 */
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);


                            /***************** User-created extensions original code input ****************************************************************/
                            /******************************************************************************************************************************/

                            /* Seeing as how this function is looped foreach skeleton in the stream, here is where we have decided to 'get a pose' and save
                             * it to a global. This global will be constantly updated with new information from the stream */


                            // Getting the correct number of poses captured
                            int numPosesCaptured_LOCAL = numPosesCaptured;
                            if (numPosesCaptured_LOCAL > 0)
                            {
                                numPosesCaptured_LOCAL = numPosesCaptured_LOCAL - 1;
                            }
                            // Create a new TimeSpan variable which will hold the amount of time elapsed since the Pose CAPTURED_POSE was captured
                            TimeSpan capturedTimeElapsed = DateTime.Now - poseArray[numPosesCaptured_LOCAL].timeElapsed;

                            // Fills the current pose variable with the appropriate pose data including joint angles, and joint names
                            currentStreamPose = getPose(skel);
                            
                            // Compares if the currentStreamPose matches any of the saved poses
                            int matchingPoseArrayIndex = poseChecker();

                            // If there is a match with one of the poses, activate the according dance performance function
                            if (matchingPoseArrayIndex != 0)
                            {
                                // Calls a static lighting functions overseer & structural handler, passing in the matched pose index
                                staticLightingHandler(matchingPoseArrayIndex);
                              
                                //check to see which function we want to do
                                //set the whole timer situation up
                            }



                            /* isCurrentFGPose test function

                            // check to see if the current Pose the kinect is looking at is relatively the same as the Pose CAPTURED_POSE
                            if (isCurrentFGPose(poseArray[numPosesCaptured_LOCAL], skel))
                            {
                                // if it IS the same Pose, check to see if 10 seconds have elapsed 
                                if (capturedTimeElapsed.Seconds > 10)
                                {
                                    txtCapturedInfo.Text = "SAME POSE (after 10)!! " + capturedTimeElapsed.Seconds.ToString();
                                }
                                else
                                {
                                    txtCapturedInfo.Text = "Same pose (before 10) " + capturedTimeElapsed.Seconds.ToString();
                                }                                                                                                          
                            }  */
                        }

                        /************************************************************************************************************************************/

                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawPen = this.trackedBonePen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
            Dictionary<string, int> jointFromName = new Dictionary<string, int>();
        }



        /*************************************************** User created functions **********************************************************************************/
        /*************************************************************************************************************************************************************/


        /* *****ON LOAD FUNCTIONS***** */


        /// <summary>
        /// ONLOAD function that checks if there are poses already saved
        /// </summary>
        /// <returns>containsPoseFiles: Boolean if there are poses already saved/returns>
        private Boolean containsPoseFiles()
        {
            Boolean containsPoseFiles = false;
            //Check to see if ".../Pose#_1.txt" exists
            if(File.Exists(POSES_FOLDER_PATH + "1.txt"))
            {
                containsPoseFiles = true;
            }

            return containsPoseFiles;
        }






        /* *****CONSTRANT STREAM FUNCTIONS***** */


        /// <summary>
        /// Creates a new pose from the skeleton stream and fills it with joint names, captured angles, and a time stamp
        /// </summary>
        /// <param name="skel"></param>
        /// <returns>skeletonPose pose: the pose struct with the updated joint angles & time stamp</returns>
        private skeletonPose getPose(Skeleton skel)
        {
            // Creates a new blank pose
            skeletonPose pose = new skeletonPose();

            // Fills the pose with the enum joint names & initilizes Joints
            pose = fillJointNames();

            /* Right arm joint angles */
            double RWA = AngleBetweenJoints(skel.Joints[JointType.ElbowRight],
                skel.Joints[JointType.WristRight], skel.Joints[JointType.HandRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightWristAngle] = RWA;  // Determines the right wrist angle
            double REA = AngleBetweenJoints(skel.Joints[JointType.ShoulderRight],
                skel.Joints[JointType.ElbowRight], skel.Joints[JointType.WristRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightElbowAngle] = REA;  // Determines the right elbow angle
            double RSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderCenter],
                skel.Joints[JointType.ShoulderRight], skel.Joints[JointType.ElbowRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightShoulderAngle] = RSA;  // Determines the right shoulder angle
            /* Left arm joint angles */
            double LWA = AngleBetweenJoints(skel.Joints[JointType.ElbowLeft],
                skel.Joints[JointType.WristLeft], skel.Joints[JointType.HandLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftWristAngle] = LWA;  // Determines the left wrist angle
            double LEA = AngleBetweenJoints(skel.Joints[JointType.ShoulderLeft],
                skel.Joints[JointType.ElbowLeft], skel.Joints[JointType.WristLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftElbowAngle] = LEA;  // Determines the left elbow angle
            double LSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderCenter],
                skel.Joints[JointType.ShoulderLeft], skel.Joints[JointType.ElbowLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftShoulderAngle] = LSA;  // Determines the left shoulder angle
            /* Right leg joint angles */
            double RAA = AngleBetweenJoints(skel.Joints[JointType.KneeRight],
                skel.Joints[JointType.AnkleRight], skel.Joints[JointType.FootRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightAnkleAngle] = RAA;  // Determines the right ankle angle
            double RKA = AngleBetweenJoints(skel.Joints[JointType.HipRight],
                skel.Joints[JointType.KneeRight], skel.Joints[JointType.AnkleRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightKneeAngle] = RKA;  // Determines the right knee angle
            double RHA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.HipRight], skel.Joints[JointType.KneeRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightHipAngle] = RHA;  // Determines the right hip angle
            /* Left leg joint angles */
            double LAA = AngleBetweenJoints(skel.Joints[JointType.KneeLeft],
                skel.Joints[JointType.AnkleLeft], skel.Joints[JointType.FootLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftAnkleAngle] = RAA;  // Determines the left ankle angle
            double LKA = AngleBetweenJoints(skel.Joints[JointType.HipLeft],
                skel.Joints[JointType.KneeLeft], skel.Joints[JointType.AnkleLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftKneeAngle] = LKA;  // Determines the left knee angle
            double LHA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.HipLeft], skel.Joints[JointType.KneeLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftHipAngle] = LHA;  // Determines the left hip angle
            /* Torso joint angles */
            // We don't have a need for the HipCenter joint, it would a duplicate
            double SA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.Spine], skel.Joints[JointType.ShoulderCenter]);
            pose.Joints[(int)skeletonPose.JointLabels.spineAngle] = SA;  // Determines the spine angle
            double NA = AngleBetweenJoints(skel.Joints[JointType.Spine],
                skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.Head]);
            pose.Joints[(int)skeletonPose.JointLabels.neckAngle] = NA;  // Determines the neck angle *NOTE: This is the angle of the 3 vertical points for ShoulderCenter*
            double CSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderLeft],
                skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.ShoulderRight]);
            pose.Joints[(int)skeletonPose.JointLabels.centerShoulderAngle] = CSA;  // Determines the neck angle *NOTE: This is the angle of the 3 horizontal points for ShoulderCenter*

            // Gets the current time of the capture; reference for next pose capture delay
            pose.timeElapsed = DateTime.Now;

            // Return the new pose with filled joint names, indicies, angles
            return pose;
        }

        /// <summary>
        /// Fills a new pose's dictionary with joint names and indicies. This is called from getPose()
        /// </summary>
        /// <returns>filledPose: pose with filled dictionary</returns>
        private skeletonPose fillJointNames()
        {
            // Creating a new skeletonPose, filling its dictionary and returning it setting it equal to pose in getPose
            skeletonPose filledPose = new skeletonPose();

            // Initilizing the jointFromName dictionary 
            filledPose.jointFromName = new Dictionary<string, int>();

            // Initilize the Joints array
            filledPose.Joints = new double[(int)skeletonPose.JointLabels.numJoints];

            filledPose.jointFromName.Add("rightWristAngle", Convert.ToInt32(skeletonPose.JointLabels.rightWristAngle));
            filledPose.jointFromName.Add("rightElbowAngle", Convert.ToInt32(skeletonPose.JointLabels.rightElbowAngle));
            filledPose.jointFromName.Add("rightShoulderAngle", Convert.ToInt32(skeletonPose.JointLabels.rightShoulderAngle));
            filledPose.jointFromName.Add("leftWristAngle", Convert.ToInt32(skeletonPose.JointLabels.leftWristAngle));
            filledPose.jointFromName.Add("leftElbowAngle", Convert.ToInt32(skeletonPose.JointLabels.leftElbowAngle));
            filledPose.jointFromName.Add("leftShoulderAngle", Convert.ToInt32(skeletonPose.JointLabels.leftShoulderAngle));
            filledPose.jointFromName.Add("rightAnkleAngle", Convert.ToInt32(skeletonPose.JointLabels.rightAnkleAngle));
            filledPose.jointFromName.Add("rightKneeAngle", Convert.ToInt32(skeletonPose.JointLabels.rightKneeAngle));
            filledPose.jointFromName.Add("rightHipAngle", Convert.ToInt32(skeletonPose.JointLabels.rightHipAngle));
            filledPose.jointFromName.Add("leftAnkleAngle", Convert.ToInt32(skeletonPose.JointLabels.leftAnkleAngle));
            filledPose.jointFromName.Add("leftKneeAngle", Convert.ToInt32(skeletonPose.JointLabels.leftKneeAngle));
            filledPose.jointFromName.Add("leftHipAngle", Convert.ToInt32(skeletonPose.JointLabels.leftHipAngle));
            filledPose.jointFromName.Add("spineAngle", Convert.ToInt32(skeletonPose.JointLabels.spineAngle));
            filledPose.jointFromName.Add("neckAngle", Convert.ToInt32(skeletonPose.JointLabels.neckAngle));
            filledPose.jointFromName.Add("centerShoulderAngle", Convert.ToInt32(skeletonPose.JointLabels.centerShoulderAngle));
            filledPose.jointFromName.Add("numJoints", Convert.ToInt32(skeletonPose.JointLabels.numJoints));

            return filledPose;
        }






        /* *****CAPTURING & SAVING POSES FUNCTIONS***** */


        /// <summary>
        /// Event when a button is clicked to capture a pose. This will be used to capture poses before a performance.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns>N/A</returns>
        private void btnCapture_Click(object sender, RoutedEventArgs e)
        {
            // Sign up for the SkeletonFrameReady event when the button is clicked
            if (this.sensor != null)
            {
                // Add the current stream skeleton
                this.sensor.SkeletonFrameReady += this.CaptureCurrentSkeleton;
            }
            else
                txtCapturedInfo.Text = "No skeleton present";
        }

        /// <summary>
        /// Structural flow for 'capturing' a desired pose; Once the Capture button is clicked,
        /// gets the global currentStreamPose; if the pose is acceptable by the user, it's
        /// written to a .txt file to be loaded for a dance session
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="s"></param>
        /// <returns>N/A</returns>
        private void CaptureCurrentSkeleton(object sender, SkeletonFrameReadyEventArgs s)
        {
            ///* Create a new Skeleton array to fill with the 6 poses that the data stream is constantly 
            // * capturing because there can be up to six people in frame when using the Kinect */
            //Skeleton[] skeletons = new Skeleton[0];

            //// Making sure that the skeletonFrame & stream are working correctly
            //using (SkeletonFrame skeletonFrame = s.OpenSkeletonFrame())
            //{
            //    if (skeletonFrame != null)
            //    {
            //        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
            //        skeletonFrame.CopySkeletonDataTo(skeletons);
            //    }
            //}

            //// Declaring the variable to get the correct skeleton index that has an actual person/non-null data
            //int INDEX_WITH_THE_DATA = 0;

            //// Look through the 6 skeleton indexes for the correct one
            //for (int i = 0; i < skeletons.Length; i++)
            //{
            //    // If the skeleton at index i doesn't have empty data (using the right Hand position as an example)... 
            //    if (skeletons[i].Joints[JointType.HandRight].Position.X != 0)
            //    {
            //        // Recognize this as the [1/6] correct skeleton index
            //        INDEX_WITH_THE_DATA = i;
            //    }
            //                                               // txtCapturedInfo.Text = skeletons[0].Joints[JointType.Head].Position.X.ToString();
            //}

            //// Create a new Skeleton that is linked to the correct skeleton index
            //Skeleton trackedSkeleton = skeletons[INDEX_WITH_THE_DATA];








            // Creates a new skeleton as the current global stream skeleton
            skeletonPose capturedPose = currentStreamPose;


            /* conditional... put something in here about if and only if the user wants to save this pose */
            
            
            // When the capture is a success, print out a message
            txtCapturedInfo.Text = "Pose added. " + numPosesCaptured + " poses";
            //txtCapturedInfo.Text = "Right elbow angle captured at: " + AngleBetweenJoints(skeletons[INDEX_WITH_THE_DATA].Joints[JointType.ShoulderRight],
            //skeletons[INDEX_WITH_THE_DATA].Joints[JointType.ElbowRight],skeletons[INDEX_WITH_THE_DATA].Joints[JointType.HandRight]);


            // Write the pose joint angles to a unique text file in .\\poses
            writePoseToFile(capturedPose);

            // sign-out of the event
            this.sensor.SkeletonFrameReady -= this.CaptureCurrentSkeleton;
        }

        /// <summary>
        /// Puts the skeleton joint angles in an array to be saved to a text file
        /// </summary>
        /// <param name="p"></param>
        /// <returns>N/A</returns>
        private void writePoseToFile(skeletonPose p)
        {
            // Increase the poses captured counter
            numPosesCaptured++;

            // Creating an array of double joint angles
            double[] angles = new double[15];

            angles[0] = p.Joints[0]; angles[1] = p.Joints[1]; angles[2] = p.Joints[2]; angles[3] = p.Joints[3];
            angles[4] = p.Joints[4]; angles[5] = p.Joints[5]; angles[6] = p.Joints[6]; angles[7] = p.Joints[7];
            angles[8] = p.Joints[8]; angles[9] = p.Joints[9]; angles[10] = p.Joints[10]; angles[11] = p.Joints[11];
            angles[12] = p.Joints[12]; angles[13] = p.Joints[13]; angles[14] = p.Joints[14];

            // Creating an array that will hold the converted toString angles
            String[] strAngles = new String[15];

            // String that will hold the concatenated angles as a string in display in a pop-up
            String promptAngles = "";

            // Converting the angles to strings for writting to the external txt file
            for (int i = 0; i < angles.Length; i++)
            {
                strAngles[i] = Convert.ToString(angles[i]) + "\n";
                promptAngles += i + ": " + Convert.ToString(angles[i]) + "\n";
            }

            // Creating a new path/name for each new pose
            String poseFolderPath = POSES_FOLDER_PATH + numPosesCaptured + ".txt";
            
            // Write out the string angles to the text file
            System.IO.File.WriteAllLines(poseFolderPath, strAngles);

        }






        /* *****POSE COMPARISON & LIVE PERFORMANCE FUNCTIONS***** */


        /// <summary>
        /// Loop function that searches all of the poses in the array to see if any pose matches the currentStreamPose
        /// </summary>
        /// <returns>matchingPoseArrayIndex: index in poseArray of a pose that matches the currentStreamPose</returns>
        private int poseChecker() 
        {
            // Declare variable that will hold the index of a pose if it's a match
            int matchingPoseArrayIndex = 0;

            // for each of the poses in poseArray, see if it's a match to the currentStreamPose by checking isCurrentPose
            foreach (skeletonPose p in poseArray) 
            {
                // If the current pose in the array matches, return that index and break
                if(isCurrentPose(poseArray[p], currentStreamPose, ))
                {
                    matchingPoseArrayIndex = poseArray[p];
                    return matchingPoseArrayIndex;
                    break;
                    // need to break the foreach loop
                }
            }

            return matchingPoseArrayIndex;
        }

        /// <summary>
        /// Checks to see if the desired checkable angles in the captured pose is the same (within 20degrees) of the current pose from the stream
        /// </summary>
        /// <param name="capturedPose"></param>
        /// <param name="currentSkel"></param>
        /// <param name="jointsToCheckPoseNumber"></param>
        /// <returns>isCurrentPose: validity of comparison between the poses</returns>
        private Boolean isCurrentPose(skeletonPose capturedPose, skeletonPose currentStreamPose, string jointsToCheckPoseNumber)
        {

            Boolean isCurrentPose = true;

            // Reads in the lines from joints to check; each joint will be checked to see if the two poses are within range of each other
            String[] jointsToCheck = System.IO.File.ReadAllLines(JOINTS_TO_CHECK_PATH + "pose_" + jointsToCheckPoseNumber + ".txt");

            // For all of the joints to check, see if any aren't the same, if so break
            for (int j = 0; j < (int)skeletonPose.JointLabels.numJoints; ++j)
            {
                if (isJointInList(jointsToCheck, currentStreamPose) && !withinRange(currentStreamPose.Joints[j], capturedPose.Joints[j]))
                {
                    isCurrentPose = false;
                    break;
                }
            }


            /* option 1 /* figure out how to initialize the jointFromName dictionary in a way that C#  
            for (int i = 0; i < jointsToCheck.Length; ++i)
            {
                int j = currentStreamPose.jointFromName[jointsToCheck[i]]; // joint integer from string name 
                if (!withinRange(currentStreamPose.Joints[j], capturedPose.Joints[j]))
                {
                    isCurrentPose = false;
                    break;
                }
            }
            */

            return isCurrentPose;
        }

        /// <summary>
        /// isCurrentPose helper method: 
        /// Checks to see if the current joint name from the external joints to check text file is in our array of joints
        /// </summary>
        /// <param name="jointsToCheck"></param>
        /// <param name="currentPose"></param>
        /// <returns>isInList: boolean value if the name is in the joints array</returns>
        private Boolean isJointInList(String[] jointsToCheck, skeletonPose currentPose)
        {
            Boolean isInList = false;

            for (int i = 0; i < jointsToCheck.Length; i++)
            {
                if (currentPose.jointFromName.ContainsKey(jointsToCheck[i]))
                {
                    isInList = true;
                }
            }
            return isInList;
        }

        /// <summary>
        /// isCurrentPose helper method: 
        /// Checks the validity if the current joint angle is within +- 15deg. of the matching captured joint angle
        /// </summary>
        /// <param name="current"></param>
        /// <param name="captured"></param>
        /// <returns>boolean value if the angle is within range</returns>
        private Boolean withinRange(double currentAngle, double capturedAngle)
        {
            if ((currentAngle >= capturedAngle - 15) && (currentAngle <= capturedAngle + 15))
            { return true; }
            else
            { return false; }
        }






        /* *****LIGHTING FUNCTIONS***** */

        /// <summary>
        /// Static lighting functions overseer & structural handler
        /// </summary>
        /// <param name="macthingPoseArrayIndex"></param>
        /// <returns>N/A</returns>
        private void staticLightingHandler(int matchingPoseArrayIndex)
        {
            // Save off the matching saved pose from poseArray globally
            matchedSavedPose = poseArray[matchingPoseArrayIndex];
        }





        /* *****TEST FUNCTIONS***** */


        /// <summary>
        /// Checks to see if the current pose from the stream is in the field goal pose (both elbow joints are at a 90degree angle)
        /// </summary>
        /// <param name="capturedPose"></param>
        /// <param name="currentSkel"></param>
        /// <returns>field goal pose validity</returns>
        private Boolean isCurrentFGPose(skeletonPose capturedPose, Skeleton currentSkel)
        {
            Boolean isCurrentPose = false;
            skeletonPose currentPose = getPose(currentSkel);

            return isCurrentPose;
        }






        /* *****UTILITY FUNCTIONS***** */


        /// <summary>
        /// getPose helper Method
        /// Return the angle between 3 Joints
        /// </summary>
        /// <param name="j1"></param>
        /// <param name="j2"></param>
        /// <param name="j3"></param>
        /// <returns>angle: the angle of the middle joint j2</returns>
        public static double AngleBetweenJoints(Joint j1, Joint j2, Joint j3)
        {
            double angle = 0;
            double shrhX = j1.Position.X - j2.Position.X;
            double shrhY = j1.Position.Y - j2.Position.Y;
            double shrhZ = j1.Position.Z - j2.Position.Z;
            double hsl = vectorNorm(shrhX, shrhY, shrhZ);
            double unrhX = j3.Position.X - j2.Position.X;
            double unrhY = j3.Position.Y - j2.Position.Y;
            double unrhZ = j3.Position.Z - j2.Position.Z;
            double hul = vectorNorm(unrhX, unrhY, unrhZ);
            double mhshu = shrhX * unrhX + shrhY * unrhY + shrhZ * unrhZ;
            double x = mhshu / (hul * hsl);
            if (x != Double.NaN)
            {
                if (-1 <= x && x <= 1)
                {
                    double angleRad = Math.Acos(x);
                    angle = angleRad * (180.0 / Math.PI);
                }
                else { angle = 0; }
            }
            else { angle = 0; }
            return angle;
        }

        /// <summary>
        /// AngleBetweenJoints helper method:
        /// Euclidean norm of 3-component Vector
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="z"></param>
        /// <returns>vector normal</returns>
        private static double vectorNorm(double x, double y, double z)
        {
            return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(y, 2) + Math.Pow(z, 2));
        }

        /// <summary>
        /// Get distance between two joints
        /// </summary>
        /// <param name="joint1"></param>
        /// <param name="joint2"></param>
        /// <returns>joint distance in [*missing unit*]</returns>
        private double getDistanceJoints(Joint joint1, Joint joint2)
        {
            double x1, x2, y1, y2;

            x1 = joint1.Position.X;
            x2 = joint2.Position.X;
            y1 = joint1.Position.Y;
            y2 = joint2.Position.Y;

            double xValuesSqrd = Math.Pow((x2 - x1), 2);
            double yValuesSqrd = Math.Pow((y2 - y1), 2);

            return Math.Sqrt(xValuesSqrd + yValuesSqrd) * 100;
        }

    }
}