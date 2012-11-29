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
        // variable to hold the captured pose (self-comment: need to eventually create an array of static struct poses that the dancer will know about)  
        private skeletonPose CAPTURED_POSE;                                                                                                               

        // create an array to hold 6 different poses
        private skeletonPose[] poseAray = new skeletonPose[6];
        private int numPosesCaptured = 0;

        //Creates a path to the text file with the names of the joints that we want to check for each captured pose
        String jointsToCheckPath = "C:\\Users\\KinectDance\\Documents\\SkeletonBasics-WPF\\poses\\JointsToCheck";




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
                            float RH_PositionX = skel.Joints[JointType.HandRight].Position.X;
                            float RH_PositionY = skel.Joints[JointType.HandRight].Position.Y;

                            float LHPositionX = skel.Joints[JointType.HandLeft].Position.X;
                            float LHPositionY = skel.Joints[JointType.HandLeft].Position.Y;

                            float Elbow_PositionX = skel.Joints[JointType.ElbowRight].Position.X;
                            float Elbow_PositionY = skel.Joints[JointType.ElbowRight].Position.Y;





                            /***************** User-created extensions original code input ****************************************************************/
                            /******************************************************************************************************************************/
                                                                                                                                                          
                            //if(numPosesCaptured == 4)                                                                                                     
                            //{                                                                                                                             
                            //    for(int i = 0; i < numPosesCaptured; i++)
                            //    {
                            //        if(isCurrentFGPose(poseAray[i], skel))
                            //        {
                            //            MessageBox.Show("This pose matches pose " + (i + 1) + "/6");
                            //        }
                            //    }
                            //}




                            // create a new TimeSpan variable which will hold the amount of time elapsed since the Pose CAPTURED_POSE was captured
                            int numPosesCaptured_LOCAL = numPosesCaptured;

                            if (numPosesCaptured_LOCAL > 0)
                            {
                                numPosesCaptured_LOCAL = numPosesCaptured_LOCAL - 1;
                            }

                            TimeSpan capturedTimeElapsed = DateTime.Now - poseAray[numPosesCaptured_LOCAL].timeElapsed;

                            // check to see if the current Pose the kinect is looking at is relatively the same as the Pose CAPTURED_POSE
                            if (isCurrentFGPose(poseAray[numPosesCaptured_LOCAL], skel))
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
            Dictionary<string,int> jointFromName = new Dictionary<string,int>();
        }











        /*************************************************** User created functions **********************************************************************************/
        /*************************************************************************************************************************************************************/

        
        /// <summary>                                                                                                                                                
        /// Struct declaration that will house all of the joint angles and time stamp of a skeleton 
        /// *Note, neckAngle & centerShoulderAngle both use the centerShoulder as the middle 'joint'*
        /// </summary>
        struct skeletonPose
        {
            /// <summary>                                                                                                                                                
            /// map normal names to joint array indices
            /// </summary>
            public enum JointLabels
            {
                rightWristAngle, rightElbowAngle, rightShoulderAngle, leftWristAngle, leftElbowAngle, leftShoulderAngle, rightAnkleAngle,
                rightKneeAngle, rightHipAngle, leftAnkleAngle, leftKneeAngle, leftHipAngle, spineAngle, neckAngle, centerShoulderAngle,
                numJoints
            };

            //Joints array that holds a pose's angles
            public double[] Joints;            

            //Joint name & index dictionary to store in each pose
            public Dictionary<string, int> jointFromName;
            
            //Time variable started when pose is captured in getPose()
            public DateTime timeElapsed;
        }


        /// <summary>
        /// fills a new pose's dictionary with joint names and indicies. This is called from getPose()
        /// </summary>
        /// <returns>filledPose: pose with filled dictionary</returns>
        private skeletonPose fillJointNames()
        {
            skeletonPose filledPose = new skeletonPose();

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


        /// <summary>
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
                else {angle = 0;}
            }
            else {angle = 0;}
            return angle;
        }


        /// <summary>
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


        /// <summary>
        /// Event when a button is clicked to capture a pose. This will be used to capture poses before a performance.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns>N/A</returns>
        private void btnCapture_Click(object sender, RoutedEventArgs e)
        {
            // sign up for the SkeletonFrameReady event when the button is clicked
            if (this.sensor != null)
            {
                this.sensor.SkeletonFrameReady += this.CaptureCurrentSkeleton; //this.CaptureCurrentSkeleton
            }
            /* 
             * should perhaps add in something to display incase the SkeletonFrame is not ready
             * and the button has been clicked
             */
        }



        /**************** Work in progress test functions ****************/



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
        /// Captures the current skeleton from the stream
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="s"></param>
        /// <returns>N/A</returns>
        private void CaptureCurrentSkeleton(object sender, SkeletonFrameReadyEventArgs s)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = s.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            int INDEX_WITH_THE_DATA = 0;

            for (int i = 0; i < skeletons.Length; i++)
            {
                // search the array of 6 skeletons to find the one which does not have 0 for HandRight angle
                if (skeletons[i].Joints[JointType.HandRight].Position.X != 0)
                {
                    INDEX_WITH_THE_DATA = i;
                }

                //txtCapturedInfo.Text = skeletons[0].Joints[JointType.Head].Position.X.ToString();
            }
            Skeleton trackedSkeleton = skeletons[INDEX_WITH_THE_DATA];

            skeletonPose capturedPose1 = getPose(trackedSkeleton);

            txtCapturedInfo.Text = "Pose captured!";

            // Check to see if isCurrentPose returns true
            CAPTURED_POSE = capturedPose1;


            // Write the pose joint angles to a unique text file in .\\poses
            writePoseToFile(CAPTURED_POSE);

            if (numPosesCaptured <= 5)
            {
                poseAray[numPosesCaptured] = CAPTURED_POSE;
                numPosesCaptured++;
                txtCapturedInfo.Text = "Pose added. " + numPosesCaptured + "/6 poses";
            }
            //txtCapturedInfo.Text = "Right elbow angle captured at: " + AngleBetweenJoints(skeletons[INDEX_WITH_THE_DATA].Joints[JointType.ShoulderRight],
                //skeletons[INDEX_WITH_THE_DATA].Joints[JointType.ElbowRight],skeletons[INDEX_WITH_THE_DATA].Joints[JointType.HandRight]);
            
            // sign-out of the event
            this.sensor.SkeletonFrameReady -= this.CaptureCurrentSkeleton;
        }


        /// <summary>
        /// Checks to see if the desired checkable angles in the captured pose is the same (within 20degrees) of the current pose from the stream
        /// </summary>
        /// <param name="capturedPose"></param>
        /// <param name="currentSkel"></param>
        /// /// <param name="jointsToCheckPath"></param>
        /// <returns>isCurrentPose: validity of comparison between the poses</returns>
        private Boolean isCurrentPose(skeletonPose capturedPose, Skeleton currentSkel, String jointsToCheckPath)
        {
            skeletonPose currentPose = getPose(currentSkel);
            
            Boolean isCurrentPose = true;

            String[] jointsToCheck = System.IO.File.ReadAllLines(jointsToCheckPath);

            /* option 2 */
            for (int j = 0; j < (int)skeletonPose.JointLabels.numJoints; ++j)
            {
                if (jointInList(jointsToCheck, currentPose) && !withinRange(currentPose.Joints[j], capturedPose.Joints[j]))
                {
                    isCurrentPose = false;
                    break;
                }
            }

            return isCurrentPose;


            /* option 1                                           /* figure out how to initialize the jointFromName dictionary in a way that C#  
            for (int i = 0; i < jointsToCheck.Length; ++i)
            {
                int j = currentPose.jointFromName[jointsToCheck[i]]; /* joint integer from string name 
                if (!withinRange(currentPose.Joints[j], capturedPose.Joints[j]))
                {
                    isCurrentPose = false;
                    break;
                }
            }
             * */

            /* old 
            int i = jointsToCheck.Length;
            while(isCurrentPose && i > 0)
            {
                if (jointsToCheck[i] == "rightWristAngle") 
                { 
                    if(!withinRange(currentPose.Joints[(int)JointLabels.rightWristAngle], capturedPose.Joints[(int)JointLabels.rightWristAngle])) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "rightElbowAngle")
                {
                    if(!withinRange(currentPose.rightElbowAngle, capturedPose.rightElbowAngle)) {isCurrentPose = false; }  
                }
                else if (jointsToCheck[i] == "rightShoulderAngle")
                {
                    if(!withinRange(currentPose.rightShoulderAngle, capturedPose.rightShoulderAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftWristAngle")
                {
                    if(!withinRange(currentPose.leftWristAngle, capturedPose.leftWristAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftElbowAngle")
                {
                    if(!withinRange(currentPose.leftElbowAngle, capturedPose.leftElbowAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftShoulderAngle")
                {
                    if(!withinRange(currentPose.leftShoulderAngle, capturedPose.leftShoulderAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "rightAnkleAngle")
                {
                    if(!withinRange(currentPose.rightAnkleAngle, capturedPose.rightAnkleAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "rightKneeAngle")
                {
                    if(!withinRange(currentPose.rightKneeAngle, capturedPose.rightKneeAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "rightHipAngle")
                {
                    if(!withinRange(currentPose.rightHipAngle, capturedPose.rightHipAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftAnkelAngle")
                {
                    if(!withinRange(currentPose.leftAnkleAngle, capturedPose.leftAnkleAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftKneeAngle")
                {
                    if(!withinRange(currentPose.leftKneeAngle, capturedPose.leftKneeAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftHipAngle")
                {
                    if(!withinRange(currentPose.leftHipAngle, capturedPose.leftHipAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftAnkelAngle")
                {
                    if(!withinRange(currentPose.leftAnkleAngle, capturedPose.leftAnkleAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftKneeAngle")
                {
                    if(!withinRange(currentPose.leftKneeAngle, capturedPose.leftKneeAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "leftHipAngle")
                {
                    if(!withinRange(currentPose.spineAngle, capturedPose.spineAngle)) {isCurrentPose = false; } 
                }
                else if (jointsToCheck[i] == "spineAngle")
                {
                    if(!withinRange(currentPose.leftAnkleAngle, capturedPose.leftAnkleAngle)) {isCurrentPose = false; }  
                }
                else if (jointsToCheck[i] == "neckAngle")
                {
                    if (!withinRange(currentPose.neckAngle, capturedPose.neckAngle)) {isCurrentPose = false; }   
                }
                else if (jointsToCheck[i] == "leftHipAngle")
                {
                    if(!withinRange(currentPose.centerShoulderAngle, capturedPose.centerShoulderAngle)) {isCurrentPose = false; } 
                }
                else
                {
                }

                i--;
            }
             * 
             */
        }


        /// <summary>
        /// Checks to see if the current joint name from the external joints to check text file is in our array of joints
        /// </summary>
        /// <param name="jointsToCheck"></param>
        /// <param name="currentPose"></param>
        /// <returns>isInList: boolean value if the name is in the joints array</returns>
        private Boolean jointInList(String[] jointsToCheck, skeletonPose currentPose)
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
        /// Checks the validity if the current joint angle is within +- 15deg. of the matching captured joint angle
        /// </summary>
        /// <param name="current"></param>
        /// <param name="captured"></param>
        /// <returns>boolean value if the angle is within range</returns>
        private Boolean withinRange(double currentAngle, double capturedAngle) 
        {
            if ((currentAngle >= capturedAngle - 20) && (currentAngle <= capturedAngle + 20)) 
            {return true;}
            else
            {return false;}
        }


        /// <summary>
        /// Creates a new pose from the skeleton stream and fills it with joint names, captured angles, and a time stamp
        /// </summary>
        /// <param name="skel"></param>
        /// <returns>skeletonPose pose: the pose struct with the updated joint angles & time stamp</returns>
        private skeletonPose getPose(Skeleton skel)
        {
            //Creates a new blank pose
            skeletonPose pose = new skeletonPose();

            //Initilize the Joints array
            pose.Joints = new double[(int)skeletonPose.JointLabels.numJoints];

            //Initilize the jointFromName dictionary
            pose.jointFromName = new Dictionary<string, int>();

            //Fills the pose with the enum joint names
            pose = fillJointNames();


            /* Right arm joint angles */
            double RWA = AngleBetweenJoints(skel.Joints[JointType.ElbowRight],                                                   
                skel.Joints[JointType.WristRight], skel.Joints[JointType.HandRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightWristAngle] = RWA;  /* Determines the right wrist angle */
            double REA = AngleBetweenJoints(skel.Joints[JointType.ShoulderRight],
                skel.Joints[JointType.ElbowRight], skel.Joints[JointType.WristRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightElbowAngle] = REA;  /* Determines the right elbow angle */
            double RSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderCenter],
                skel.Joints[JointType.ShoulderRight], skel.Joints[JointType.ElbowRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightShoulderAngle] = RSA;  /* Determines the right shoulder angle */


            /* Left arm joint angles */
            double LWA = AngleBetweenJoints(skel.Joints[JointType.ElbowLeft],
                skel.Joints[JointType.WristLeft], skel.Joints[JointType.HandLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftWristAngle] = LWA;  /* Determines the left wrist angle */
            double LEA = AngleBetweenJoints(skel.Joints[JointType.ShoulderLeft],
                skel.Joints[JointType.ElbowLeft], skel.Joints[JointType.WristLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftElbowAngle] = LEA;  /* Determines the left elbow angle */
            double LSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderCenter],
                skel.Joints[JointType.ShoulderLeft], skel.Joints[JointType.ElbowLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftShoulderAngle] = LSA;  /* Determines the left shoulder angle */


            /* Right leg joint angles */
            double RAA = AngleBetweenJoints(skel.Joints[JointType.KneeRight],
                skel.Joints[JointType.AnkleRight], skel.Joints[JointType.FootRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightAnkleAngle] = RAA;  /* Determines the right ankle angle */
            double RKA = AngleBetweenJoints(skel.Joints[JointType.HipRight],
                skel.Joints[JointType.KneeRight], skel.Joints[JointType.AnkleRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightKneeAngle] = RKA;  /* Determines the right knee angle */
            double RHA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.HipRight], skel.Joints[JointType.KneeRight]);
            pose.Joints[(int)skeletonPose.JointLabels.rightHipAngle] = RHA;  /* Determines the right hip angle */


            /* Left leg joint angles */
            double LAA = AngleBetweenJoints(skel.Joints[JointType.KneeLeft],
                skel.Joints[JointType.AnkleLeft], skel.Joints[JointType.FootLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftAnkleAngle] = RAA;  /* Determines the left ankle angle */
            double LKA = AngleBetweenJoints(skel.Joints[JointType.HipLeft],
                skel.Joints[JointType.KneeLeft], skel.Joints[JointType.AnkleLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftKneeAngle] = LKA;  /* Determines the left knee angle */
            double LHA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.HipLeft], skel.Joints[JointType.KneeLeft]);
            pose.Joints[(int)skeletonPose.JointLabels.leftHipAngle] = LHA;  /* Determines the left hip angle */


            /* Torso joint angles */
            //We don't have a need for the HipCenter joint
            double SA = AngleBetweenJoints(skel.Joints[JointType.HipCenter],
                skel.Joints[JointType.Spine], skel.Joints[JointType.ShoulderCenter]);
            pose.Joints[(int)skeletonPose.JointLabels.spineAngle] = SA;  /* Determines the spine angle */
            double NA = AngleBetweenJoints(skel.Joints[JointType.Spine],
                skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.Head]);
            pose.Joints[(int)skeletonPose.JointLabels.neckAngle] = NA;  /* Determines the neck angle *NOTE: This is the angle of the 3 vertical points for ShoulderCenter* */
            double CSA = AngleBetweenJoints(skel.Joints[JointType.ShoulderLeft],
                skel.Joints[JointType.ShoulderCenter], skel.Joints[JointType.ShoulderRight]);
            pose.Joints[(int)skeletonPose.JointLabels.centerShoulderAngle] = CSA;  /* Determines the neck angle *NOTE: This is the angle of the 3 horizontal points for ShoulderCenter* */

            /* Gets the current time of the capture; reference for next pose capture delay */
            pose.timeElapsed = DateTime.Now;

            /* Return the new pose with filled joint names, indicies, angles */
            return pose;
        }


        /// <summary>
        /// Puts the skeleton joint angles in an array to be saved to a text file
        /// </summary>
        /// <param name="p"></param>
        /// <returns>N/A</returns>
        private void writePoseToFile(skeletonPose p) 
        {
            double[] angles = new double[15];

            //Path to pose text files folder
            String POSES_FOLDER_PATH = "C:\\Users\\KinectDance\\Documents\\SkeletonBasics-WPF\\poses\\Pose_";

            angles[0] = p.Joints[0]; angles[1] = p.Joints[1]; angles[2] = p.Joints[2]; angles[3] = p.Joints[3];
            angles[4] = p.Joints[4]; angles[5] = p.Joints[5]; angles[6] = p.Joints[6]; angles[7] = p.Joints[7];
            angles[8] = p.Joints[8]; angles[9] = p.Joints[9]; angles[10] = p.Joints[10]; angles[11] = p.Joints[11];
            angles[12] = p.Joints[12]; angles[13] = p.Joints[13]; angles[14] = p.Joints[14];

            //Creating a new path/name for each new pose
            String poseFolderPath = POSES_FOLDER_PATH + numPosesCaptured + ".txt";

            String[] strAngles = new String[15];

            //Converting the angles to strings for writting to the external txt file
            for (int i = 0; i < angles.Length; i++)
            {
                strAngles[i] = Convert.ToString(angles[i]) + "\n";
            }

            System.IO.File.WriteAllLines(poseFolderPath, strAngles);
        }                                                                           
        /*****************************************************************************************************************************************/

    }
}