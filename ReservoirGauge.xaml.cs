using System.Windows;
using System.Windows.Media.Animation;

namespace Waterline;

public partial class ReservoirGauge : System.Windows.Controls.UserControl
{
    private double _percentage;
    private bool _motionEnabled;

    public ReservoirGauge()
    {
        InitializeComponent();
        Loaded += (_, _) => SetProgress(_percentage, false);
        SizeChanged += (_, _) => SetProgress(_percentage, false);
    }

    public void SetProgress(double percentage, bool animate, double response = 1)
    {
        _percentage = Math.Clamp(double.IsFinite(percentage) ? percentage : 0, 0, 100);
        var target = Math.Max(0, ReservoirBody.ActualHeight - 4) * _percentage / 100;
        var previous = ReservoirFill.ActualHeight;

        ReservoirFill.BeginAnimation(HeightProperty, null);
        ReservoirFill.Height = target;
        ReservoirFill.Visibility = target > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!animate || target <= 0) return;

        ReservoirFill.BeginAnimation(HeightProperty, new DoubleAnimation
        {
            From = previous,
            To = target,
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        }, HandoffBehavior.SnapshotAndReplace);

        ReservoirSurfaceScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new EasingDoubleKeyFrame(1 + .34 * response, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))),
                new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1050)))
            }
        }, HandoffBehavior.SnapshotAndReplace);

        ReservoirHighlightTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation
        {
            From = 70,
            To = -Math.Max(70, target),
            Duration = TimeSpan.FromMilliseconds(900),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        }, HandoffBehavior.SnapshotAndReplace);
        ReservoirHighlight.BeginAnimation(OpacityProperty, new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new EasingDoubleKeyFrame(.28 * response, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))),
                new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)))
            }
        }, HandoffBehavior.SnapshotAndReplace);
    }

    public void SetMotionEnabled(bool enabled)
    {
        if (_motionEnabled == enabled) return;
        _motionEnabled = enabled;
        if (!enabled)
        {
            StopMotion(WaveTranslate);
            StopMotion(SecondWaveTranslate);
            StopMotion(WaveGlowTranslate);
            StopMotion(CausticTranslate);
            StopMotion(BubbleOneTranslate);
            StopMotion(BubbleTwoTranslate);
            StopMotion(BubbleThreeTranslate);
            return;
        }

        AnimateHorizontal(WaveTranslate, -2, 2, 6.4);
        AnimateHorizontal(SecondWaveTranslate, 2, -2, 7.8);
        AnimateHorizontal(WaveGlowTranslate, -1.5, 1.5, 7.1);
        AnimateHorizontal(CausticTranslate, -7, 7, 16);
        AnimateBubble(BubbleOneTranslate, 18, -58, 7.3, 0.4);
        AnimateBubble(BubbleTwoTranslate, 12, -48, 6.1, 2.1);
        AnimateBubble(BubbleThreeTranslate, 20, -70, 8.2, 4.2);
    }

    private static void AnimateHorizontal(System.Windows.Media.TranslateTransform transform, double from, double to, double seconds) =>
        transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        });

    private static void AnimateBubble(System.Windows.Media.TranslateTransform transform, double from, double to, double seconds, double delay) =>
        transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            BeginTime = TimeSpan.FromSeconds(delay),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        });

    private static void StopMotion(System.Windows.Media.TranslateTransform transform)
    {
        transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
        transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
        transform.X = 0;
        transform.Y = 0;
    }
}
