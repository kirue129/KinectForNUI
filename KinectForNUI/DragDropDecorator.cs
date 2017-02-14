using System.Windows.Controls;
using Microsoft.Kinect.Wpf.Controls;
using Microsoft.Kinect.Toolkit.Input;

namespace KinectForNUI
{
    /// <summary>
    /// DragDropDecoratorはCanvasの子に配置して、子にドラッグ＆ドロップしたいControlを入れる
    /// </summary>
    public class DragDropDecorator : Decorator, IKinectControl
    {
        public bool IsManipulatable
        {
            get
            {
                return true;
            }
        }
        public bool IsPressable
        {
            get
            {
                return false;
            }
        }
        public IKinectController CreateController(IInputModel inputModel, KinectRegion kinectRegion)
        {
            return new DragDropDecoratorController(inputModel, kinectRegion);

        }
    }
}
