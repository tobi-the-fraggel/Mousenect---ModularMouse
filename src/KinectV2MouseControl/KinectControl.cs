﻿using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Kinect;
using LightBuzz.Vitruvius;

namespace Mousenect
{
    class KinectControl
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        public KinectSensor sensor;
        /// <summary>
        /// Timer für Gesten-Pausen
        /// </summary>
        DispatcherTimer timer = new DispatcherTimer();
        /// <summary>
        /// Vitruvius erzeugter GestureController
        /// </summary>
        GestureController gestureController;
        /// <summary>
        /// Vitruvius Erzeugter MultiSourceFrameReader
        /// </summary>
        MultiSourceFrameReader _reader;
        /// <summary>
        /// Reader for body frames
        /// </summary>
        BodyFrameReader bodyFrameReader;
        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;
        /// <summary>
        /// Screen width and height for determining the exact mouse sensitivity
        /// </summary>
        int screenWidth, screenHeight;

        /// <summary>
        /// How far the cursor move according to your hand's movement
        /// </summary>
        public float mouseSensitivity = MOUSE_SENSITIVITY;

        /// <summary>
        /// Decide if the user need to do clicks or only move the cursor
        /// </summary>
        public bool doClick = DO_CLICK;
        /// <summary>
        /// Use Grip gesture to click or not
        /// </summary>
        public bool useGripGesture = USE_GRIP_GESTURE;
        /// <summary>
        /// Value 0 - 0.95f, the larger it is, the smoother the cursor would move
        /// </summary>
        public float cursorSmoothing = CURSOR_SMOOTHING;

        // Default values
        public const float MOUSE_SENSITIVITY = 3.5f;
        public const bool DO_CLICK = true;
        public const bool USE_GRIP_GESTURE = true;
        public const float CURSOR_SMOOTHING = 0.2f;

        /// <summary>
        /// For storing last cursor position
        /// </summary>
        Point lastCurPos = new Point(0, 0);

        /// <summary>
        /// If true, user did a left hand Grip gesture
        /// </summary>
        bool wasLeftGrip = false;
        /// <summary>
        /// Verzögerung zwischen den Gesten
        /// </summary>
        bool wasGesture = false;
        /// <summary>
        /// Variable zur Steuerung verschiedener Programme
        /// Maus = 1
        /// Powerpoint = 2
        /// </summary>
        public byte Programm;

        public KinectControl()
        {
            // get Active Kinect Sensor
            sensor = KinectSensor.GetDefault();

            // get screen with and height
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            //Timer-Setup
            timer.Interval = new TimeSpan(0, 0, 0, 0, 1000);
            timer.Tick += new EventHandler(Timer_Tick);

            //Default für Programm (Maus)
            Programm = 1;

            // open the sensor
            sensor.Open();

            //Vitruvius
            _reader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Depth | FrameSourceTypes.Infrared | FrameSourceTypes.Body);
            _reader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;
            gestureController = new GestureController();
            gestureController.GestureRecognized += GestureController_GestureRecognized;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            wasGesture = true;
            timer.Stop();
        }

        void GestureController_GestureRecognized(object sender, GestureEventArgs e)
        {
            timer.Start();
        }

        void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var reference = e.FrameReference.AcquireFrame();
            // Body
            using (var frame = reference.BodyFrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    //Den Closest Body wählen
                    Body body = frame.Bodies().Closest();



                    // get closest tracked body only, notice there's a break below.
                    if (body != null)
                    {
                        gestureController.Update(body);
                        // get various skeletal positions
                        CameraSpacePoint handLeft = body.Joints[JointType.HandLeft].Position;
                        CameraSpacePoint handRight = body.Joints[JointType.HandRight].Position;
                        CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;

                        if (body.HandRightState != HandState.Unknown && body.HandRightState != HandState.NotTracked) // Rechte Hand muss getrackt sein
                        {
                            //2x Lasso zum beenden der Anwendung
                            if (body.HandLeftState == HandState.Lasso && body.HandRightState == HandState.Lasso)
                            {
                                System.Environment.Exit(0);
                            }
                            /* hand x calculated by this. we don't use shoulder right as a reference cause the shoulder right
                             * is usually behind the lift right hand, and the position would be inferred and unstable.
                             * because the spine base is on the left of right hand, we plus 0.05f to make it closer to the right. */
                            float x = handRight.X - spineBase.X + 0.05f;
                            /* hand y calculated by this. ss spine base is way lower than right hand, we plus 0.51f to make it
                             * higer, the value 0.51f is worked out by testing for a several times, you can set it as another one you like. */
                            float y = spineBase.Y - handRight.Y + 0.51f;
                            // get current cursor position
                            Point curPos = MouseControl.GetCursorPosition();
                            // smoothing for using should be 0 - 0.95f. The way we smooth the cusor is: oldPos + (newPos - oldPos) * smoothValue
                            float smoothing = 1 - cursorSmoothing;
                            // set cursor position
                            MouseControl.SetCursorPos((int)(curPos.X + (x * mouseSensitivity * screenWidth - curPos.X) * smoothing), (int)(curPos.Y + ((y + 0.25f) * mouseSensitivity * screenHeight - curPos.Y) * smoothing));

                            // Grip gesture
                            if (doClick && useGripGesture)
                            {
                                if (body.HandLeftState == HandState.Closed)
                                {
                                    if (!wasLeftGrip)
                                    {
                                        MouseControl.MouseLeftDown();
                                        wasLeftGrip = true;
                                    }
                                }
                                else if (body.HandLeftState == HandState.Open)
                                {
                                    if (wasLeftGrip)
                                    {
                                        MouseControl.MouseLeftUp();
                                        wasLeftGrip = false;
                                    }
                                }
                            }
                        }
                        else
                        {
                            wasLeftGrip = true;
                        }
                    }
                }
            }
        }

        public void Close()
        {
            if (this.sensor != null)
            {
                this.sensor.Close();
                this.sensor = null;
            }
        }

    }
}
