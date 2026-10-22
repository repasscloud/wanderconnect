namespace Swoovr.Mobile;

/// <summary>
/// Cold-start launch splash (docs/design/reference/UI-STYLE.md). The platform splash shows the ink
/// background and bird; this page continues on the same ink, so there is no white frame between them.
/// Times are the prototype's (login.html), in milliseconds from the start of the sequence.
/// </summary>
public partial class LaunchPage : ContentPage
{
    // Length of the bird outline in its own path units (measured from the path data; bounds 384 × 324).
    private const double BirdLength = 1892.7;
    private const double BirdWidth = 384, BirdHeight = 324;
    private const uint Total = 3650;
    private readonly Action done;
    private bool started;

    public LaunchPage(Action done)
    {
        this.done = done;
        InitializeComponent();
        // The page is sized before the bird is measured, so start on whichever arrives last.
        SizeChanged += (_, _) => Start();
        Bird.SizeChanged += (_, _) => Start();
    }

    private void Start()
    {
        if (started || Height <= 0 || Bird.Width <= 0) return;
        started = true;
        // The sheet clips its content as it shrinks, so the content keeps the full height and stays put.
        Sheet.HeightRequest = Height;
        Stage.HeightRequest = Height;
        if (Motion.IsReduced) Calm(); else Play();
    }

    private void Play()
    {
        var white = Bird.Stroke is SolidColorBrush brush ? brush.Color : Colors.White;
        // Dash lengths are in units of StrokeThickness, on the outline as scaled by Aspect="Uniform".
        var dash = BirdLength * Math.Min(Bird.Width / BirdWidth, Bird.Height / BirdHeight) / Bird.StrokeThickness;
        Bird.StrokeDashArray = [dash, dash];
        Bird.StrokeDashOffset = dash;
        var rise = Word.Height * 1.1;
        foreach (var letter in Word.Children.OfType<View>()) letter.TranslationY = rise;

        var timeline = new Animation();
        void At(double start, double duration, Animation step) => timeline.Add(start / Total, (start + duration) / Total, step);
        At(150, 1300, new Animation(v => Bird.StrokeDashOffset = v, dash, 0, Motion.InOut));
        At(900, 1500, new Animation(v => BarFill.ScaleX = v, 0, 1, Motion.InOut));
        // The stroke stays: white on a white fill reads the same as the prototype's stroke-width → 0.
        At(1250, 500, new Animation(v => Bird.Fill = new SolidColorBrush(white.WithAlpha((float)v)), 0, 1, Motion.Ease));
        var index = 0;
        foreach (var letter in Word.Children.OfType<View>())
            At(1200 + 55 * index++, 700, new Animation(v => letter.TranslationY = v, rise, 0, Motion.Out));
        At(1500, 600, new Animation(v => Tag.Opacity = v, 0, .55, Motion.Ease));
        At(2750, 900, new Animation(v => Sheet.HeightRequest = v, Height, 0, Motion.InOut));
        timeline.Commit(this, "launch", 16, Total, Easing.Linear, (_, _) => done());
    }

    // Reduced motion: the finished logo for about 600 ms, then a plain cross-fade.
    private async void Calm()
    {
        Bird.Fill = Bird.Stroke;
        BarFill.ScaleX = 1;
        Tag.Opacity = .55;
        await Task.Delay(600);
        await Sheet.FadeToAsync(0, 300, Easing.Linear);
        done();
    }
}
