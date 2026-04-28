using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AgentPaw.Models;

namespace AgentPaw.Views.Controls;

public partial class DogAnimationPanel : UserControl
{
    private const double GroundY = 82;

    private readonly List<DogSprite> _dogs = [];
    private readonly DispatcherTimer _timer;
    private readonly Random _rng = new();

    public DogAnimationPanel()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += OnTick;
        Loaded += (_, _) => _timer.Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    public void SetPersonas(IEnumerable<Persona> personas)
    {
        DogsCanvas.Children.Clear();
        _dogs.Clear();

        var ground = new Line
        {
            X1 = 0, Y1 = GroundY, X2 = 3000, Y2 = GroundY,
            Stroke = new SolidColorBrush(Color.FromArgb(55, 200, 169, 110)),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        DogsCanvas.Children.Add(ground);

        var list = personas.ToList();
        if (list.Count == 0) return;

        double panelW = ActualWidth > 80 ? ActualWidth : 1200;
        double spacing = Math.Max(80, panelW / (list.Count + 1));
        for (int i = 0; i < list.Count; i++)
        {
            var dog = new DogSprite(list[i], _rng);
            dog.X = spacing * (i + 1);
            dog.VelX = (i % 2 == 0 ? 1 : -1) * (35 + i * 8);
            dog.Build(DogsCanvas, GroundY);
            _dogs.Add(dog);
        }
    }

    public void SetActive(bool active)
    {
        foreach (var d in _dogs) d.IsActive = active;
    }

    public void SetSpeaker(string? personaId, string? preview)
    {
        foreach (var d in _dogs)
        {
            d.IsSpeaking = personaId != null && d.PersonaId == personaId;
            d.SpeechText = d.IsSpeaking ? Truncate(preview, 40) : null;
        }
    }

    private static string? Truncate(string? s, int max)
    {
        if (s == null) return null;
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var w = ActualWidth;
        if (w < 10) return;
        foreach (var d in _dogs) d.Update(w);
    }

    // ─────────────────────────────────────────────────────────────────
    private sealed class DogSprite
    {
        public readonly string PersonaId;
        public double X;
        public double VelX;
        public bool IsActive;
        public bool IsSpeaking;
        public string? SpeechText;

        private readonly Color _color;
        private readonly Color _darkColor;
        private readonly string _label;
        private readonly Random _rng;
        private double _groundY;

        private Canvas _dogCanvas = null!;
        private ScaleTransform _scaleX = null!;
        private Rectangle _legFL = null!, _legFR = null!, _legBL = null!, _legBR = null!;
        private Ellipse _tail = null!;
        private RotateTransform _tailRot = null!;
        private Border _bubble = null!;
        private TextBlock _bubbleText = null!;
        private TextBlock _nameLabel = null!;

        private double _legPhase;
        private double _tailPhase;
        private double _bounceOffset;
        private bool _facingRight = true;
        private DateTime _lastTime;
        private double _idleTimer;
        private double _idleVelX;

        private const double LegBase = 34;

        public DogSprite(Persona persona, Random rng)
        {
            PersonaId = persona.PersonaId;
            _label = persona.Label;
            _color = MapColor(persona.Color);
            _darkColor = Darken(_color, 0.35);
            _rng = rng;
        }

        public void Build(Canvas parent, double groundY)
        {
            _groundY = groundY;
            _lastTime = DateTime.UtcNow;
            _facingRight = VelX >= 0;

            var fill = B(_color);
            var dark = B(_darkColor);

            _dogCanvas = new Canvas { Width = 52, Height = 52, IsHitTestVisible = false };
            _scaleX = new ScaleTransform(1, 1, 26, 26);
            _dogCanvas.RenderTransform = _scaleX;

            // Tail
            _tail = new Ellipse { Width = 14, Height = 8, Fill = dark };
            _tailRot = new RotateTransform(0, 1, 4);
            _tail.RenderTransform = _tailRot;
            At(_tail, 2, 25);

            // Back legs
            _legBL = Leg(dark); At(_legBL, 12, LegBase);
            _legBR = Leg(dark); At(_legBR, 18, LegBase);

            // Body
            var body = new Ellipse { Width = 28, Height = 15, Fill = fill };
            At(body, 11, 26);

            // Front legs
            _legFL = Leg(dark); At(_legFL, 27, LegBase);
            _legFR = Leg(dark); At(_legFR, 33, LegBase);

            // Head
            var head = new Ellipse { Width = 16, Height = 15, Fill = fill };
            At(head, 32, 20);

            // Ears
            var earL = new Ellipse { Width = 8, Height = 10, Fill = dark };
            earL.RenderTransform = new RotateTransform(-20, 4, 5);
            At(earL, 32, 12);

            var earR = new Ellipse { Width = 8, Height = 10, Fill = dark };
            earR.RenderTransform = new RotateTransform(20, 4, 5);
            At(earR, 38, 12);

            // Eye + pupil
            var eye = new Ellipse { Width = 4, Height = 4, Fill = Brushes.White };
            At(eye, 37, 22);
            var pupil = new Ellipse { Width = 2, Height = 2, Fill = Brushes.Black };
            At(pupil, 38, 23);

            // Nose
            var nose = new Ellipse { Width = 5, Height = 4, Fill = B(Color.FromRgb(55, 38, 32)) };
            At(nose, 44, 27);

            foreach (var el in new UIElement[] { _tail, _legBL, _legBR, body, _legFL, _legFR, head, earL, earR, eye, pupil, nose })
                _dogCanvas.Children.Add(el);

            parent.Children.Add(_dogCanvas);

            // Speech bubble (in parent canvas, not inside dog canvas — so it doesn't flip)
            _bubbleText = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = B(Color.FromRgb(28, 28, 28)),
                MaxWidth = 150,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _bubble = new Border
            {
                Background = B(Color.FromArgb(242, 255, 255, 255)),
                BorderBrush = B(Color.FromArgb(210, _color.R, _color.G, _color.B)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 3, 7, 3),
                Child = _bubbleText,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            parent.Children.Add(_bubble);

            // Name label
            _nameLabel = new TextBlock
            {
                Text = _label,
                Width = 64,
                FontSize = 9,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Center,
                Foreground = B(Color.FromArgb(170, 200, 169, 110)),
                IsHitTestVisible = false
            };
            parent.Children.Add(_nameLabel);

            Reposition();
        }

        public void Update(double canvasWidth)
        {
            var now = DateTime.UtcNow;
            var dt = Math.Min((now - _lastTime).TotalSeconds, 0.1);
            _lastTime = now;

            if (IsActive)
            {
                X += VelX * dt;
                if (X < 28 && VelX < 0) { VelX = -VelX; _facingRight = true; }
                if (X > canvasWidth - 28 && VelX > 0) { VelX = -VelX; _facingRight = false; }

                _legPhase += dt * Math.Abs(VelX) * 0.13;
                _bounceOffset = -Math.Abs(Math.Sin(_legPhase * 2)) * 2.5;
                _tailPhase += dt * 10;
            }
            else
            {
                _idleTimer -= dt;
                if (_idleTimer <= 0)
                {
                    _idleTimer = 2.5 + _rng.NextDouble() * 3.0;
                    _idleVelX = _rng.NextDouble() > 0.6
                        ? (_rng.NextDouble() > 0.5 ? 1 : -1) * 18.0
                        : 0;
                    if (_idleVelX != 0) _facingRight = _idleVelX > 0;
                }

                if (_idleVelX != 0)
                {
                    X += _idleVelX * dt;
                    _legPhase += dt * 18 * 0.13;
                    _bounceOffset = -Math.Abs(Math.Sin(_legPhase * 2)) * 1.5;
                    if (X < 28 || X > canvasWidth - 28) _idleVelX = 0;
                }
                else
                {
                    _legPhase = 0;
                    _bounceOffset = 0;
                }

                _tailPhase += dt * 3.0;
            }

            _scaleX.ScaleX = _facingRight ? 1 : -1;

            _tailRot.Angle = Math.Sin(_tailPhase) * (IsActive ? 28 : 16);

            double legAnim = Math.Sin(_legPhase) * (IsActive ? 4.5 : (_idleVelX != 0 ? 2.5 : 0));
            Canvas.SetTop(_legFL, LegBase + legAnim);
            Canvas.SetTop(_legFR, LegBase - legAnim);
            Canvas.SetTop(_legBL, LegBase - legAnim);
            Canvas.SetTop(_legBR, LegBase + legAnim);

            if (IsSpeaking && !string.IsNullOrEmpty(SpeechText))
            {
                _bubbleText.Text = SpeechText;
                _bubble.Visibility = Visibility.Visible;
                _bubble.Measure(new Size(200, 60));
                var bw = _bubble.DesiredSize.Width;
                var bh = _bubble.DesiredSize.Height;
                Canvas.SetLeft(_bubble, X - bw / 2);
                Canvas.SetTop(_bubble, _groundY - 44 + _bounceOffset - bh - 6);
            }
            else
            {
                _bubble.Visibility = Visibility.Collapsed;
            }

            Reposition();
        }

        private void Reposition()
        {
            Canvas.SetLeft(_dogCanvas, X - 26);
            Canvas.SetTop(_dogCanvas, _groundY - 44 + _bounceOffset);
            Canvas.SetLeft(_nameLabel, X - 32);
            Canvas.SetTop(_nameLabel, _groundY + 2);
        }

        private static void At(UIElement el, double l, double t)
        {
            Canvas.SetLeft(el, l);
            Canvas.SetTop(el, t);
        }

        private static Rectangle Leg(Brush fill) =>
            new() { Width = 5, Height = 11, Fill = fill, RadiusX = 2, RadiusY = 2 };

        private static SolidColorBrush B(Color c) => new(c);

        private static Color MapColor(string name) => name.ToLowerInvariant() switch
        {
            "blue"   => Color.FromRgb(82,  140, 255),
            "green"  => Color.FromRgb(72,  185,  90),
            "orange" => Color.FromRgb(255, 158,  50),
            "red"    => Color.FromRgb(240,  80,  70),
            "purple" => Color.FromRgb(170,  80, 210),
            "teal"   => Color.FromRgb(0,   170, 155),
            "indigo" => Color.FromRgb(90,  100, 200),
            "pink"   => Color.FromRgb(240,  80, 130),
            "yellow" => Color.FromRgb(230, 200,  50),
            "gold"   => Color.FromRgb(200, 169, 110),
            "cyan"   => Color.FromRgb(40,  195, 215),
            "brown"  => Color.FromRgb(140, 100,  80),
            "lime"   => Color.FromRgb(140, 200,  50),
            _        => Color.FromRgb(160, 145, 130)
        };

        private static Color Darken(Color c, double f) => Color.FromRgb(
            (byte)(c.R * (1 - f)),
            (byte)(c.G * (1 - f)),
            (byte)(c.B * (1 - f)));
    }
}
