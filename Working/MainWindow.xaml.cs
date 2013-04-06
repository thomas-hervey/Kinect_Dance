//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Forms;
    using System.Diagnostics;
    using System.Threading;
    using System.Collections.Generic;
    using System.Collections;
    using Microsoft.Kinect;
    // Zachs DMX
    using DmxComm;




    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        /***************** User-created constants and variables *******************************************************************************************/
        /**************************************************************************************************************************************************/

        // Dmx Object
        DmxDriver dmxdev = new DmxDriver(150);


        /* Mode selection variables */

        // Mode designation
        private Boolean isLiveMode = false;


        int tempc = 0;

        /* Saving pose variables */
        
        // writePose counter
        private int numPosesWritten = 0;
        


        /* Loading pose variables */

        // skeletonPose array to hold 6 different poses captured before a live mode
        private ArrayList poseArrayList = new ArrayList();
        // Constants for text loading
        private int NAME_INDEX = 0;
        private int ANGLE_INDEX = 1;
        private int TOLERANCE_INDEX = 2;

        // loadPose counter
        private int numPosesLoaded = 0;




        /* Stream variables */

        // SkeletonPose variable that is constantly updated directly from the skeleton stream
        private skeletonPose currentStreamPose;
        /// <summary>                                                                                                                                                
        /// Struct declaration that will house all of the joint angles and time stamp of a skeleton 
        /// *Note, neckAngle & centerShoulderAngle both use the centerShoulder as the middle 'joint'*
        /// </summary>
        struct skeletonPose
        {
            // Map normal names to joint array indices
            public enum JointLabels
            {
                rightWristAngle, rightElbowAngle, rightShoulderAngle, leftWristAngle, leftElbowAngle, leftShoulderAngle, rightAnkleAngle,
                rightKneeAngle, rightHipAngle, leftAnkleAngle, leftKneeAngle, leftHipAngle, spineAngle, neckAngle, centerShoulderAngle,
                numJoints
            };

            // Joints array that holds a pose's joint angles
            public double[] Joints;

            // Turn string name into an integer index as jointFromName["rightWristAngle"]
            public Dictionary<string, int> jointFromName;

            // Turn integer into a name as skeletonPose.Names[i]
            public string[] Names;

            // Tolerance array that holds how close a pose needs to be for each joint
            public double[] Tolerance;

            // Time variable started when pose is captured in getPose()
            public DateTime timeElapsed;
        }




        /* Match pose and live performance mode function variables */

        // poseArray index that identifies a match between the stream and a saved pose
        private int matchingPoseArrayIndex;
        // SkeletonPose variable that is extracted from poseArray when it's found to match currentStreamPose  ***********
        private skeletonPose matchedSavedPose;
        // Timer that holds the elapsed time since a racognized match
        // so that the program can't recapture the same pose in a short time
        private TimeSpan timeSinceMatch;

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


                            //// Getting the correct number of poses captured
                            //int numPosesCaptured_LOCAL = numPosesCaptured;
                            //if (numPosesCaptured_LOCAL > 0)
                            //{
                            //    numPosesCaptured_LOCAL = numPosesCaptured_LOCAL - 1;
                            //}


                            // Create a new TimeSpan variable which will hold the amount of time elapsed since the Pose CAPTURED_POSE was captured
                            // **********  TimeSpan capturedTimeElapsed = DateTime.Now - poseArray[numPosesCaptured_LOCAL].timeElapsed;



                            // Constantly assigning the currentStreamPose variable to the current stream data (entailing joint angles and joint names)
                            currentStreamPose = getPose(skel);

                            // If the user is in capture mode Don't allow pose checking or lighting effects
                            if (isLiveMode == false)
                            {
                                txtCapturedInfo.Text = "In capture mode";
                            }

                            // If the user is in live mode: Allow pose checking & lighting effects
                            else if (isLiveMode == true)
                            {
                                // Compares if the currentStreamPose pose matches any of the saved poses in poseArray
                                matchingPoseArrayIndex = poseChecker();

                                // If one of the poses matches, activate its according dance performance function
                                if (matchingPoseArrayIndex != -99)
                                {
                                    // Calls a static lighting functions overseer & structural handler, passing in the matched pose index
                                    staticLightingHandler(matchingPoseArrayIndex);
                                    //Lets the user know which pose the program thinks is a match
                                    txtCapturedInfo.Text = "Matching Pose seen: " + matchingPoseArrayIndex;

                                    //check to see which function we want to do
                                    //set the whole timer situation up
                                }
                            }


                            //// NON-FILE LIGHTING EFFECT TEST FUNCTION
                            //if (currentStreamPose.Joints[1] > 70 && currentStreamPose.Joints[1] < 110)
                            //{
                            //    txtCapturedInfo.Text = "YAY POSE";
                            //    doTestFunction();
                            //}






                     /*         isCurrentFGPose test function

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
                            }  
                      */

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

                // prevent drawing outside of our render areaoa
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






        /* *****MODE SELECTION FUNCTIONS***** */

        
        /// <summary>
        /// Radio button function to select capture mode (instead of live performance mode)
        /// </summary>
        /// <returns> N/A </returns>
        private void captureMode_Checked(object sender, RoutedEventArgs e)
        {
            // Setting the mode option to capture
            isLiveMode = false;
            // Enabling capture function buttons
            capturePose.IsEnabled = true;
            loadPose.IsEnabled = true;
        }

        /// <summary>
        /// Radio button function to select live performance mode (instead of capture mode)
        /// </summary>
        /// <returns> N/A </returns>
        private void liveMode_Checked(object sender, RoutedEventArgs e)
        {
            // If there aren't any poses in our array, don't set up live mode yet
            if (poseArrayList.Count == 0)
            {
                txtCapturedInfo.Text = "There are no loaded poses yet. Cannot start live mode.";
                isLiveMode = false;

                // Change the radio buttons
                liveMode.IsChecked = false;
                captureMode.IsChecked = true;
            }
            else
            {
                // Setting the mode option to live
                isLiveMode = true;
                // Disabling capture function buttons
                capturePose.IsEnabled = false;
                loadPose.IsEnabled = false;
            }
        }






        /* ***** WRITE POSE FUNCTIONS***** */


        // to be removed later
        ///// <summary>
        ///// Event when a button is clicked to capture a pose. This will be used to capture poses before a performance.
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        ///// <returns>N/A</returns>
        //private void CapturePose(object sender, RoutedEventArgs e)
        //{
        //    // Sign up for the SkeletonFrameReady event when the button is clicked
        //    if (this.sensor != null)
        //    {
        //        // Add the current stream skeleton
        //        this.sensor.SkeletonFrameReady += this.CaptureCurrentSkeleton;
        //    }
        //    else
        //        txtCapturedInfo.Text = "No skeleton present";
        //}

        ///// <summary>
        ///// Structural flow for 'capturing' a desired pose; Once the Capture button is clicked,
        ///// gets the global currentStreamPose; if the pose is acceptable by the user, it's
        ///// written to a .txt file to be loaded for a dance session
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="s"></param>
        ///// <returns>N/A</returns>
        //private void CaptureCurrentSkeleton(object sender, SkeletonFrameReadyEventArgs s)
        //{
        //    ///* Create a new Skeleton array to fill with the 6 poses that the data stream is constantly 
        //    // * capturing because there can be up to six people in frame when using the Kinect */
        //    //Skeleton[] skeletons = new Skeleton[0];

        //    //// Making sure that the skeletonFrame & stream are working correctly
        //    //using (SkeletonFrame skeletonFrame = s.OpenSkeletonFrame())
        //    //{
        //    //    if (skeletonFrame != null)
        //    //    {
        //    //        skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
        //    //        skeletonFrame.CopySkeletonDataTo(skeletons);
        //    //    }
        //    //}

        //    //// Declaring the variable to get the correct skeleton index that has an actual person/non-null data
        //    //int INDEX_WITH_THE_DATA = 0;

        //    //// Look through the 6 skeleton indexes for the correct one
        //    //for (int i = 0; i < skeletons.Length; i++)
        //    //{
        //    //    // If the skeleton at index i doesn't have empty data (using the right Hand position as an example)... 
        //    //    if (skeletons[i].Joints[JointType.HandRight].Position.X != 0)
        //    //    {
        //    //        // Recognize this as the [1/6] correct skeleton index
        //    //        INDEX_WITH_THE_DATA = i;
        //    //    }
        //    //                                               // txtCapturedInfo.Text = skeletons[0].Joints[JointType.Head].Position.X.ToString();
        //    //}

        //    //// Create a new Skeleton that is linked to the correct skeleton index
        //    //Skeleton trackedSkeleton = skeletons[INDEX_WITH_THE_DATA];








        //    // Creates a new skeleton as the current global stream skeleton
        //    skeletonPose capturedPose = currentStreamPose;


        //    /* conditional... put something in here about if and only if the user wants to save this pose */


        //    // When the capture is a success, print out a message
        //    txtCapturedInfo.Text = "Pose added. " + numPosesCaptured + " poses";
        //    //txtCapturedInfo.Text = "Right elbow angle captured at: " + AngleBetweenJoints(skeletons[INDEX_WITH_THE_DATA].Joints[JointType.ShoulderRight],
        //    //skeletons[INDEX_WITH_THE_DATA].Joints[JointType.ElbowRight],skeletons[INDEX_WITH_THE_DATA].Joints[JointType.HandRight]);


        //    // Write the pose joint angles to a unique text file in .\\poses
        //    writePoseToFile(capturedPose);

        //    // sign-out of the event
        //    this.sensor.SkeletonFrameReady -= this.CaptureCurrentSkeleton;
        //}
        ///// <summary>
        ///// Puts the skeleton joint angles in an array to be saved to a text file
        ///// </summary>
        ///// <param name="p"></param>
        ///// <returns>N/A</returns>
        //private void writePoseToFile(skeletonPose p)
        //{
        //    skeletonPose capturedPose = currentStreamPose;

        //    // Open file dialog to pick a save location
        //    SaveFileDialog saveFileDiag = new SaveFileDialog();

        //    if (saveFileDiag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        //    {
        //        String filePath = saveFileDiag.FileName;
        //        StreamWriter streamWriter = new StreamWriter(filePath);
        //        streamWriter.Write("TEST GUCCI NIGGA");

        //    }

        //    // Save fileName path as a string


        //    // Create a stream writer to write to a new file



        //    // Increase the poses captured counter
        //    //numPosesCaptured++;

        //    // String that will hold the concatenated angles as a string in display in a pop-up
        //    //String promptAngles = "";

        //    // Converting the angles to strings for writting to the external txt file
        //    //for (int i = 0; i < p.Joints.Length; i++)
        //    //{
        //    // streamWriter.writeLine(p.Names[i] + " " + Convert.ToString(p.Joints[i]) + " 15");   // when we have a tolerance, we wouldn't have 15 here as a default string




        //    //strAngles[i] = Convert.ToString(p.Joints[i]) + "\n";  //turn an index back into a name
        //    //promptAngles += i + ": " + Convert.ToString(p.Joints[i]) + "\n";
        //    //}

        //}

        /// <summary>
        /// Function that opens up the joints to check window
        /// </summary>
        /// <returns> N/A </returns>
        public void CapturePose(object sender, RoutedEventArgs e)
        {
            // Save off a pose from the stream
            skeletonPose capturedPose = currentStreamPose;

            // Open up the joint selection window
            JSF jointSelectionForm = new JSF();
            
            // Call the joint selection form
            jointSelectionForm.ShowDialog();
            
           

            if(jointSelectionForm.save == true)
            {
                // Save the pose with the desired joints to check
                savePose(capturedPose, jointSelectionForm.jointTolerances);
            }
        }

        /// <summary>
        /// Once joints to check have been selected, write the pose to text
        /// </summary>
        /// <returns> N/A </returns>
        private void savePose(skeletonPose capturedPose, double[] jointTolerances)
        {

            // Open file dialog to pick a save location
            SaveFileDialog saveFileDiag = new SaveFileDialog();

            // If the file dialog works...
            if (saveFileDiag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Create file writing path
                String filePath = saveFileDiag.FileName;
                // Create a new stream writer to save information from capturedPose
                StreamWriter streamWriter = new StreamWriter(filePath, true);

                // For all of our joints, write out the joint name, angle and tolerance to a line
                for (int i = 0; i < capturedPose.Joints.Length; i++)
                {
                    streamWriter.WriteLine(capturedPose.Names[i] + " " + Convert.ToString(capturedPose.Joints[i]) + " " + jointTolerances[i]);   
                }
                streamWriter.Close();
            }
            saveFileDiag.Dispose();

            // Increase the pose written counter
            numPosesWritten++;
        }




        /* *****LOAD POSE FUNCTIONS***** */


        /// <summary>
        /// Function to load poses from the openBox path once the user has specified a path
        /// </summary>
        /// <returns> N/A </returns>
        private void LoadPose(object sender, RoutedEventArgs e)
        {
            // Open file dialog to get the pose text to open
            OpenFileDialog openFile = new OpenFileDialog();
            DialogResult result = openFile.ShowDialog();
            String filePath = "";
            filePath = openFile.FileName;
            //if (DialogResult.HasValue)
            //{
            //    // Save fileName path as a string
            //    filePath = openFile.FileName;
            //}
            if(!Directory.Exists(filePath)){
                return;
            }
            
            // Create a stream reader to read from the newly opened file
            StreamReader streamReader = new StreamReader(filePath);

            // Creating a new pose that'll be filled with loaded pose joint angle information
            skeletonPose poseToFill = new skeletonPose();
            // Initilizing the dictionary for our new pose
            poseToFill = fillJointNames();

            // Delimiters to break up the components on each line of the pose text file
            char[] delimiters = { ' ',':' };
            // String that'll hold each line of the pose text file
            string line;
            // Read and display lines from the file until the end of the file is reached
            while ((line = streamReader.ReadLine()) != null) 
            {
                string[] words = line.Split(delimiters);
                int whichJoint = poseToFill.jointFromName[words[NAME_INDEX]];                  // joint name = first word of line
                poseToFill.Joints[whichJoint] = Convert.ToDouble(words[ANGLE_INDEX]);          // joint angle = second word
                poseToFill.Tolerance[whichJoint] = Convert.ToDouble(words[TOLERANCE_INDEX]);   // joint tolerance = third word
            }
            // Set the effectName to the user's chosen effect
            //poseToFill.effectName = effectName;

            // TEST OUTPUT:  txtCapturedInfo.Text = Convert.ToString(poseToFill.Joints[1]);

            // Place the newly filled pose into the poseArray
            poseArrayList.Add(poseToFill);
            // Increase the pose loaded counter
            numPosesLoaded++;

            streamReader.Close();
            openFile.Dispose();


            // tests

            //skeletonPose loadedTest = (skeletonPose)poseArrayList[0];
            txtCapturedInfo.Text = "Pose added to index: " + poseArrayList.Count; //loadedTest.Names[3] + " " + loadedTest.Joints[4] + " " + loadedTest.Tolerance[3];
        }






        /* *****CONSTANT STREAM FUNCTIONS***** */


        /// <summary>
        /// Creates a new pose from the skeleton stream and fills it with joint names, captured angles, and a time stamp
        /// </summary>
        /// <param name="skel"></param>
        /// <returns>skeletonPose pose: the pose struct with the updated joint angles & time stamp</returns>
        private skeletonPose getPose(Skeleton skel)
        {
            // Creates a new blank pose
            skeletonPose pose = new skeletonPose();

            // Fills the pose with the enum joint names and initilizes Joints & fills NAMES array
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
        /// Fills a new pose's dictionary with joint names and indicies & fills NAMES array. This is called from getPose()
        /// </summary>
        /// <returns>filledPose: pose with filled dictionary</returns>
        private skeletonPose fillJointNames()
        {
            // Creating a new skeletonPose, filling its dictionary and returning it setting it equal to pose in getPose
            skeletonPose filledPose = new skeletonPose();

            // string array constant to hold the names of the joints 
            string[] NAMES = {"rightWristAngle", "rightElbowAngle", "rightShoulderAngle", "leftWristAngle", "leftElbowAngle", "leftShoulderAngle", "rightAnkleAngle",
                "rightKneeAngle", "rightHipAngle", "leftAnkleAngle", "leftKneeAngle", "leftHipAngle", "spineAngle", "neckAngle", "centerShoulderAngle"};
            filledPose.Names = NAMES;

            // Initilizing the jointFromName dictionary 
            filledPose.jointFromName = new Dictionary<string, int>();

            // Initilize the Joints array
            filledPose.Joints = new double[(int)skeletonPose.JointLabels.numJoints];

            // Initilize the tolerance array
            filledPose.Tolerance = new double[(int)skeletonPose.JointLabels.numJoints];

            filledPose.jointFromName.Add(filledPose.Names[0], Convert.ToInt32(skeletonPose.JointLabels.rightWristAngle));
            filledPose.jointFromName.Add(filledPose.Names[1], Convert.ToInt32(skeletonPose.JointLabels.rightElbowAngle));
            filledPose.jointFromName.Add(filledPose.Names[2], Convert.ToInt32(skeletonPose.JointLabels.rightShoulderAngle));
            filledPose.jointFromName.Add(filledPose.Names[3], Convert.ToInt32(skeletonPose.JointLabels.leftWristAngle));
            filledPose.jointFromName.Add(filledPose.Names[4], Convert.ToInt32(skeletonPose.JointLabels.leftElbowAngle));
            filledPose.jointFromName.Add(filledPose.Names[5], Convert.ToInt32(skeletonPose.JointLabels.leftShoulderAngle));
            filledPose.jointFromName.Add(filledPose.Names[6], Convert.ToInt32(skeletonPose.JointLabels.rightAnkleAngle));
            filledPose.jointFromName.Add(filledPose.Names[7], Convert.ToInt32(skeletonPose.JointLabels.rightKneeAngle));
            filledPose.jointFromName.Add(filledPose.Names[8], Convert.ToInt32(skeletonPose.JointLabels.rightHipAngle));
            filledPose.jointFromName.Add(filledPose.Names[9], Convert.ToInt32(skeletonPose.JointLabels.leftAnkleAngle));
            filledPose.jointFromName.Add(filledPose.Names[10], Convert.ToInt32(skeletonPose.JointLabels.leftKneeAngle));
            filledPose.jointFromName.Add(filledPose.Names[11], Convert.ToInt32(skeletonPose.JointLabels.leftHipAngle));
            filledPose.jointFromName.Add(filledPose.Names[12], Convert.ToInt32(skeletonPose.JointLabels.spineAngle));
            filledPose.jointFromName.Add(filledPose.Names[13], Convert.ToInt32(skeletonPose.JointLabels.neckAngle));
            filledPose.jointFromName.Add(filledPose.Names[14], Convert.ToInt32(skeletonPose.JointLabels.centerShoulderAngle));
            //filledPose.jointFromName.Add(filledPose.Names[15], Convert.ToInt32(skeletonPose.JointLabels.numJoints));

            return filledPose;
        }






        /* *****POSE COMPARISON FUNCTIONS***** */


        /// <summary>
        /// Loop function that searches all of the poses in the array to see if any pose matches the currentStreamPose
        /// </summary>
        /// <returns>matchingPoseArrayIndex: index of a matching saved pose in poseArray</returns>
        private int poseChecker()
        {
            // Declare variable that will hold the index of a pose if it's a match
            int matchingPoseArrayIndex = -99;

            // for each of the poses in poseArray, see if it's a match to the currentStreamPose by checking isCurrentPose
            foreach (skeletonPose savedPose in poseArrayList)
            {
                // If the current pose in the array matches, return that index and break
                if (isPoseMatch(savedPose, currentStreamPose))
                {
                    // Save off the matching poseArray pose index
                    matchingPoseArrayIndex = poseArrayList.IndexOf(savedPose);
                    return matchingPoseArrayIndex;
                }
            }
            return matchingPoseArrayIndex;
        }

        /// <summary>
        /// Matching pose handler that checks if the passed in save pose matches the current stream pose
        /// </summary>
        /// <param name="capturedPose"></param>
        /// <param name="currentSkel"></param>
        /// <param name="jointsToCheckPoseNumber"></param>
        /// <returns>isCurrentPose: validity of comparison between the poses</returns>
        private Boolean isPoseMatch(skeletonPose savedPose, skeletonPose currentStreamPose)
        {
            // Setting pose match variable to true; after looking through all the desired joints and 
            // an non-matchis found, the variable will become false
            Boolean isPoseMatch = true;

            // For all of the joints, see if any aren't the same, if so break
            foreach (String jointByName in savedPose.jointFromName.Keys)
            {
                // If the particular joint is desired to be checked (there isn't a tolerance of -77 )
                if ( (savedPose.Tolerance[savedPose.jointFromName[jointByName]] != -77) )
                {
                    // If that joint is isn't within the tolerance range
                    if (!angleWithinRange(currentStreamPose.Joints[currentStreamPose.jointFromName[jointByName]], 
                                            savedPose.Joints[savedPose.jointFromName[jointByName]], 
                                            savedPose.Tolerance[savedPose.jointFromName[jointByName]]))
                    {
                        isPoseMatch = false;
                        return isPoseMatch;
                    }
                }



                //if (isJointInList(jointsToCheck, currentStreamPose) && angleWithinRange(currentStreamPose.Joints[j], savedPose.Joints[j]))
                //{
                //    isPoseMatch = true;
                //    break;
                //}
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

            return isPoseMatch;
        }

        // Likely to be removed later
        ///// <summary>
        ///// isCurrentPose helper method: 
        ///// Checks to see if the current joint name from the external joints to check text file is in our array of joints
        ///// </summary>
        ///// <param name="jointsToCheck"></param>
        ///// <param name="currentPose"></param>
        ///// <returns>isInList: boolean value if the name is in the joints array</returns>
        //private Boolean isJointInList(String[] jointsToCheck, skeletonPose currentPose)
        //{
        //    Boolean isInList = false;

        //    for (int i = 0; i < jointsToCheck.Length; i++)
        //    {
        //        if (currentPose.jointFromName.ContainsKey(jointsToCheck[i]))
        //        {
        //            isInList = true;
        //        }
        //    }
        //    return isInList;
        //}

        /// <summary>
        /// isCurrentPose helper method: 
        /// Checks the validity if the current joint angle is within +- "tolerance" of the matching saved joint angle
        /// </summary>
        /// <param name="current"></param>
        /// <param name="captured"></param>
        /// <returns>boolean value if the angle is within range</returns>
        private Boolean angleWithinRange(double currentAngle, double savedAngle, double tolerance)
        {
            if ((currentAngle >= savedAngle - tolerance) && (currentAngle <= savedAngle + tolerance))
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
            matchedSavedPose = (skeletonPose)poseArrayList[matchingPoseArrayIndex];
            
            if (matchingPoseArrayIndex == 0)
            { doTestFunction(); }
            else if (matchingPoseArrayIndex == 1)
            {
                doTestFunction2();
            }

         
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

        /// <summary>
        /// Checks to see if the light will have a result: TEST FUNCTION
        /// </summary>
        /// <param name="capturedPose"></param>
        /// <param name="currentSkel"></param>
        /// <returns> N/A </returns>
        private void doTestFunction()
        {
            Console.WriteLine("DO TEST FUNCTION");
            tempc += 1;
            dmxdev.setLampOn();
            dmxdev.setDimmerLevel((byte)(tempc & 0xff));
            dmxdev.setPan((byte)((tempc % 255) - 128));
            dmxdev.setTilt((byte)((tempc % 255) - 128));
            dmxdev.setColorContinuous(DmxDriver.color_t.PINK);
         
        }

        /// <summary>
        /// Checks to see if the light will have a result: TEST FUNCTION
        /// </summary>
        /// <param name="capturedPose"></param>
        /// <param name="currentSkel"></param>
        /// <returns> N/A </returns>
        private void doTestFunction2()
        {
            Console.WriteLine("DO TEST FUNCTION2");
            tempc -= 1;
            dmxdev.setLampOn();
            dmxdev.setDimmerLevel((byte)(tempc & 0xff));
            dmxdev.setPan((byte)((tempc % 255) - 128));
            dmxdev.setTilt((byte)((tempc % 255) - 128));
            dmxdev.setColorContinuous(DmxDriver.color_t.BLUE_101);
            dmxdev.setGoboStandard(3);
            
        }






        /* *****UTILITY FUNCTIONS***** */


        /// <summary>
        /// getPose helper Method to create original angles
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
        /// Unused function: could be used with other pose match criteria
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