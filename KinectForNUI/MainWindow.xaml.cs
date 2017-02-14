using System.Windows;
using System.Windows.Controls;
using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using System;
using System.Windows.Media;
using System.Linq;
using System.Windows.Shapes;

namespace KinectForNUI
{

    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {

        // kinect
        KinectSensor kinect;

        // Color
        ColorFrameReader colorFrameReader;
        FrameDescription colorFrameDesc;
        ColorImageFormat colorFormat = ColorImageFormat.Bgra;
        
        // Body
        int BODY_COUNT;
        BodyFrameReader bodyFrameReader;
        Body[] bodies;
        
        // Gesture
        VisualGestureBuilderFrameReader[] gestureFrameReaders;
        IReadOnlyList<Gesture> gestures;

        // WPF
        WriteableBitmap colorBitmap;
        byte[] colorBuffer;
        int colorStride;
        Int32Rect colorRect;

        // VGB
        String gbdFile = "KENUI.gbd";

        public MainWindow()
        {
            InitializeComponent();
        }

        // window起動処理
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            try
            {

                // kinectを開く
                kinect = KinectSensor.GetDefault();
                if (kinect == null)
                {
                    throw new Exception("Kinectを開けません");
                }
                kinect.Open();

                // カラー画像の情報を作成する(BGRAフォーマット)
                colorFrameDesc = kinect.ColorFrameSource.CreateFrameDescription(colorFormat);

                // カラーリーダーを開く
                colorFrameReader = kinect.ColorFrameSource.OpenReader();
                colorFrameReader.FrameArrived += colorFrameReader_FrameArrived;

                // カラー用のビットマップを作成する
                colorBitmap = new WriteableBitmap(colorFrameDesc.Width, colorFrameDesc.Height, 96, 96, PixelFormats.Bgra32, null);
                colorStride = colorFrameDesc.Width * (int)colorFrameDesc.BytesPerPixel;
                colorRect = new Int32Rect(0, 0, colorFrameDesc.Width, colorFrameDesc.Height);
                colorBuffer = new byte[colorStride * colorFrameDesc.Height];
                ImageColor.Source = colorBitmap;

                // Bodyの最大数を取得する
                BODY_COUNT = kinect.BodyFrameSource.BodyCount;

                // Bodyを入れる配列を作る
                bodies = new Body[BODY_COUNT];

                // ボディーリーダーを開く
                bodyFrameReader = kinect.BodyFrameSource.OpenReader();
                bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;

                InitializeGesture();

            }

            // エラー表示
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Close();
            }

        }
 
        // colorFrameReaderの更新
        void colorFrameReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            UpdateColorFrame(e);        // カラーフレームの更新
            DrawColorFrame();           // 骨格情報を画面に描画
        }

        // カラーフレームの更新
        void UpdateColorFrame(ColorFrameArrivedEventArgs e)
        {
            // カラーフレームを取得する
            using (var colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame == null)
                {
                    return;
                }

                // BGRAデータを取得する
                colorFrame.CopyConvertedFrameDataToArray(colorBuffer, colorFormat);
            }
        }

        // 骨格情報を画面に描画
        private void DrawColorFrame()
        {
            // ビットマップにする
            colorBitmap.WritePixels(colorRect, colorBuffer, colorStride, 0);
        }

        // bodyFrameReaderの更新
        void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            UpdateBodyFrame();
            DrowBodyFrame();
        }

        // ジェスチャーの初期化
        void InitializeGesture()
        {

            gestureFrameReaders = new VisualGestureBuilderFrameReader[BODY_COUNT];
            for (int count = 0; count < BODY_COUNT; count++)
            {

                VisualGestureBuilderFrameSource gestureFrameSource = new VisualGestureBuilderFrameSource(kinect, 0);
                gestureFrameReaders[count] = gestureFrameSource.OpenReader();
                gestureFrameReaders[count].FrameArrived += gestureFrameReaders_FrameArrived;

            }

            VisualGestureBuilderDatabase gestureDatabase = new VisualGestureBuilderDatabase(gbdFile);

            uint gestureCount = gestureDatabase.AvailableGesturesCount;
            gestures = gestureDatabase.AvailableGestures;
            for (int count = 0; count < BODY_COUNT; count++)
            {

                VisualGestureBuilderFrameSource gestureFrameSource = gestureFrameReaders[count].VisualGestureBuilderFrameSource;
                gestureFrameSource.AddGestures(gestures);
                foreach (var g in gestures)
                {
                    gestureFrameSource.SetIsEnabled(g, true);
                }

            }

        }

        // gestureFrameReadersの更新
        void gestureFrameReaders_FrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            VisualGestureBuilderFrame gestureFrame = e.FrameReference.AcquireFrame();
            if (gestureFrame == null)
            {
                return;
            }
            UpdateGestureFrame(gestureFrame);
            gestureFrame.Dispose();
        }

        // GestureFrameの更新
        void UpdateGestureFrame(VisualGestureBuilderFrame gestureFrame)
        {
            bool tracked = gestureFrame.IsTrackingIdValid;
            if (tracked)
            {
                foreach (var g in gestures)
                {
                    result(gestureFrame, g);
                }
            }
            
        }

        // 認識したジェスチャーをwindowに表示
        void result(VisualGestureBuilderFrame gestureFrame, Gesture gesture)
        {
            int count = GetIndexofGestureReader(gestureFrame);          // ジェスチャーを行っている人のBodyIndexを取得
            GestureType gestureType = gesture.GestureType;              // ジェスチャータイプ(DiscreteかContinuous)を取得
            switch (gestureType)
            {

                // 検出したジェスチャーがDiscrete(静的)の時
                case GestureType.Discrete:
                    DiscreteGestureResult dGestureResult = gestureFrame.DiscreteGestureResults[gesture];

                    bool detected;
                    detected = dGestureResult.Detected;
                    if (!detected)
                    {
                        break;
                    }

                    float confidence = dGestureResult.Confidence;
                    string discrete = gesture2string(gesture)
                            + " : Detected (" + confidence.ToString() + ")";
                    GetTextBlock(count).Text = discrete;                    // WPFのTextBlockに表示
                    break;

                // 検出したジェスチャーがContinuous(動的)の時
                case GestureType.Continuous:
                    ContinuousGestureResult cGestureResult = gestureFrame.ContinuousGestureResults[gesture];

                    float progress;
                    progress = cGestureResult.Progress;
                    string continuous = gesture2string(gesture)
                            + " : Progress " + progress.ToString();
                    GetTextBlock(count).Text = continuous;                  // WPFのTextBlockに表示
                    break;

                default:
                    break;

            }
        }

        // 対応するBodyIndexのTextBlockのインデックスを取得
        TextBlock GetTextBlock(int index)
        {

            switch (index)
            {
                case 1:
                    return TextBlock1;

                case 2:
                    return TextBlock2;

                case 3:
                    return TextBlock3;

                case 4:
                    return TextBlock4;

                case 5:
                    return TextBlock5;

                default:
                    return TextBlock6;

            }

        }

        // VGBで登録したジェスチャー名を取得
        string gesture2string(Gesture gesture)
        {
            return gesture.Name.Trim();
        }

        // ジェスチャーを行っている人のBodyIndexを取得
        int GetIndexofGestureReader(VisualGestureBuilderFrame gestureFrame)
        {
            for (int index = 0; index < BODY_COUNT; index++)
            {
                if (gestureFrame.TrackingId == gestureFrameReaders[index].VisualGestureBuilderFrameSource.TrackingId)
                {
                    return index;
                }
            }
            return -1;
        }

        // BodyFramの更新
        void UpdateBodyFrame()
        {
            
            if (bodyFrameReader != null)
            {

                // BodyFrameの取得
                BodyFrame bodyFrame = bodyFrameReader.AcquireLatestFrame();

                if (bodyFrame != null)
                {
                    bodyFrame.GetAndRefreshBodyData(bodies);
                    for (int count = 0; count < BODY_COUNT; count++)
                    {
                        Body body = bodies[count];
                        bool tracked = body.IsTracked;
                        if (!tracked)
                        {
                            continue;
                        }
                        ulong trackingId = body.TrackingId;
                        VisualGestureBuilderFrameSource gestureFrameSource = gestureFrameReaders[count].VisualGestureBuilderFrameSource;
                        gestureFrameSource.TrackingId = trackingId;
                    }
                    bodyFrame.Dispose();
                }
                                
            }
                        
        }

        // windowの終了処理
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // センサの終了処理
            if (kinect != null)
            {
                kinect.Close();
                kinect = null;
            }
        }


        // ボディの表示
        private void DrowBodyFrame()
        {
            CanvasBody.Children.Clear();

            // 追跡しているBodyのみループする
            foreach (var body in bodies.Where(b => b.IsTracked))
            {
                foreach (var joint in body.Joints)
                {
                    // 手の位置が追跡状態
                    if (joint.Value.TrackingState == TrackingState.Tracked)
                    {
                        // 骨格を描画
                        DrawEllipse(joint.Value, 10, Brushes.Blue);

                        // 左手が追跡していたら、手の状態を表示する
                        if (joint.Value.JointType == JointType.HandLeft)
                        {
                            DrawHandState(body.Joints[JointType.HandLeft], body.HandLeftConfidence, body.HandLeftState);
                        }
                        // 右手を追跡していたら、手の状態を表示する
                        else if (joint.Value.JointType == JointType.HandRight)
                        {
                            DrawHandState(body.Joints[JointType.HandRight], body.HandRightConfidence, body.HandRightState);
                        }
                    }
                    // 手の位置が推測状態
                    else if (joint.Value.TrackingState == TrackingState.Inferred)
                    {
                        DrawEllipse(joint.Value, 10, Brushes.Yellow);
                    }
                }
            }
        }

        // 手の状態(グー、チョキ、パー)
        private void DrawHandState(Joint joint, TrackingConfidence trackingConfidence, HandState handState)
        {
            // 手の追跡信頼性が高い
            if (trackingConfidence != TrackingConfidence.High)
            {
                return;
            }

            // 手が開いている(バー) : イエロー
            if (handState == HandState.Open)
            {
                DrawEllipse(joint, 40, new SolidColorBrush(new Color()
                {
                    R = 255,
                    G = 255,
                    A = 128
                }));
            }
            // チョキのような感じ : ピンク
            else if (handState == HandState.Lasso)
            {
                DrawEllipse(joint, 40, new SolidColorBrush(new Color()
                {
                    R = 255,
                    B = 255,
                    A = 128
                }));
            }
            // 手が閉じている(グー) : ブルー
            else if (handState == HandState.Closed)
            {
                DrawEllipse(joint, 40, new SolidColorBrush(new Color()
                {
                    G = 255,
                    B = 255,
                    A = 128
                }));
            }
        }

        // 骨を描画
        private void DrawEllipse(Joint joint, int R, Brush brush)
        {
            var ellipse = new Ellipse()
            {
                Width = R,
                Height = R,
                Fill = brush,
            };

            // カメラ座標系をDepth座標系に変換する
            var point = kinect.CoordinateMapper.MapCameraPointToDepthSpace(joint.Position);
            if ((point.X < 0) || (point.Y < 0))
            {
                return;
            }

            // Depth座標系で円を配置する
            Canvas.SetLeft(ellipse, point.X - (R / 2));
            Canvas.SetTop(ellipse, point.Y - (R / 2));

            CanvasBody.Children.Add(ellipse);
        }

    }
}
