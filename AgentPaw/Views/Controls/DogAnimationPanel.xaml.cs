using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using AgentPaw.Models;

namespace AgentPaw.Views.Controls;

public partial class DogAnimationPanel : UserControl
{
    private const double GroundY = 92;   // 지면 Y (120px 패널 기준)
    private const double ImgSize = 66;   // 아바타 이미지 크기

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

        // 지면선
        var ground = new Line
        {
            X1 = 0, Y1 = GroundY, X2 = 3000, Y2 = GroundY,
            Stroke = new SolidColorBrush(Color.FromArgb(50, 200, 169, 110)),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        DogsCanvas.Children.Add(ground);

        var list = personas.ToList();
        if (list.Count == 0) return;

        double panelW = ActualWidth > 80 ? ActualWidth : 1200;
        double spacing = Math.Max(90, panelW / (list.Count + 1));
        for (int i = 0; i < list.Count; i++)
        {
            var dog = new DogSprite(list[i], _rng);
            dog.X = spacing * (i + 1);
            dog.VelX = (i % 2 == 0 ? 1 : -1) * (38 + i * 9);
            dog.Build(DogsCanvas, GroundY, ImgSize);
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
            d.SpeechText = d.IsSpeaking ? Truncate(preview, 42) : null;
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

        private readonly Persona _persona;
        private readonly Color _color;
        private readonly Random _rng;
        private double _groundY;
        private double _imgSize;

        // 메인 요소 (방향 전환을 위해 ScaleTransform 적용)
        private UIElement _dogEl = null!;
        private ScaleTransform _scaleX = null!;
        private TranslateTransform _bounceT = null!;

        // 말풍선 / 이름표 (parent Canvas에 직접 → flip 없음)
        private Border _bubble = null!;
        private TextBlock _bubbleText = null!;
        private TextBlock _nameLabel = null!;

        // 애니메이션 상태
        private double _phase;        // 달리기/흔들기 공통 위상
        private double _bounceOffset; // 현재 바운스 오프셋 (px, 음수=위)
        private bool _facingRight = true;
        private DateTime _lastTime;
        private double _idleTimer;
        private double _idleVelX;

        public DogSprite(Persona persona, Random rng)
        {
            PersonaId = persona.PersonaId;
            _persona = persona;
            _color = MapColor(persona.Color);
            _rng = rng;
        }

        public void Build(Canvas parent, double groundY, double imgSize)
        {
            _groundY = groundY;
            _imgSize = imgSize;
            _lastTime = DateTime.UtcNow;
            _facingRight = VelX >= 0;

            // ── 트랜스폼 그룹 (스케일 X 반전 + 바운스 Y) ──────────────
            _scaleX = new ScaleTransform(1, 1, imgSize / 2, imgSize / 2);
            _bounceT = new TranslateTransform();
            var tg = new TransformGroup();
            tg.Children.Add(_scaleX);
            tg.Children.Add(_bounceT);

            // ── 강아지 요소 ────────────────────────────────────────────
            var src = LoadAvatar(_persona.Avatar);
            if (src != null)
            {
                _dogEl = new Image
                {
                    Source = src,
                    Width = imgSize, Height = imgSize,
                    Stretch = Stretch.Uniform,
                    RenderTransform = tg,
                    RenderTransformOrigin = new Point(0, 0),
                    IsHitTestVisible = false,
                    SnapsToDevicePixels = true
                };
            }
            else
            {
                // 아바타 없을 때: 페르소나 컬러 원 + 발바닥 이모지
                var grid = new Grid
                {
                    Width = imgSize, Height = imgSize,
                    RenderTransform = tg,
                    IsHitTestVisible = false
                };
                grid.Children.Add(new Ellipse
                {
                    Width = imgSize, Height = imgSize,
                    Fill = new SolidColorBrush(Color.FromArgb(140, _color.R, _color.G, _color.B))
                });
                grid.Children.Add(new TextBlock
                {
                    Text = "🐾",
                    FontSize = imgSize * 0.44,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                _dogEl = grid;
            }
            parent.Children.Add(_dogEl);

            // ── 말풍선 (parent에 직접, flip 영향 없음) ─────────────────
            _bubbleText = new TextBlock
            {
                FontSize = 10,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 28)),
                MaxWidth = 160,
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            _bubble = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, _color.R, _color.G, _color.B)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 4, 8, 4),
                Child = _bubbleText,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 6, ShadowDepth = 1, Opacity = 0.18,
                    Color = Colors.Black, Direction = 270
                }
            };
            parent.Children.Add(_bubble);

            // ── 이름표 ─────────────────────────────────────────────────
            _nameLabel = new TextBlock
            {
                Text = _persona.Label,
                Width = 72,
                FontSize = 9,
                FontFamily = new FontFamily("Segoe UI"),
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(165, 200, 169, 110)),
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
                if (X < _imgSize / 2 + 4 && VelX < 0) { VelX = -VelX; _facingRight = true; }
                if (X > canvasWidth - _imgSize / 2 - 4 && VelX > 0) { VelX = -VelX; _facingRight = false; }

                _phase += dt * Math.Abs(VelX) * 0.11;
                // 달리기 바운스: 한 걸음마다 한 번 솟아오름
                _bounceOffset = -Math.Abs(Math.Sin(_phase * 2)) * 3.0;
            }
            else
            {
                // 아이들: 가끔 짧게 이동
                _idleTimer -= dt;
                if (_idleTimer <= 0)
                {
                    _idleTimer = 2.5 + _rng.NextDouble() * 3.0;
                    _idleVelX = _rng.NextDouble() > 0.55
                        ? (_rng.NextDouble() > 0.5 ? 1 : -1) * 20.0
                        : 0;
                    if (_idleVelX != 0) _facingRight = _idleVelX > 0;
                }
                if (_idleVelX != 0)
                {
                    X += _idleVelX * dt;
                    _phase += dt * 20 * 0.11;
                    _bounceOffset = -Math.Abs(Math.Sin(_phase * 2)) * 1.5;
                    if (X < _imgSize / 2 + 4 || X > canvasWidth - _imgSize / 2 - 4) _idleVelX = 0;
                }
                else
                {
                    _phase += dt * 1.8;   // 미세한 제자리 흔들기용 위상 유지
                    _bounceOffset = Math.Sin(_phase) * 0.8; // 숨쉬는 듯한 미세 움직임
                }
            }

            // ── 방향 전환 ──────────────────────────────────────────────
            _scaleX.ScaleX = _facingRight ? 1 : -1;

            // ── 바운스 적용 ────────────────────────────────────────────
            _bounceT.Y = _bounceOffset;

            // ── 말풍선 업데이트 ────────────────────────────────────────
            if (IsSpeaking && !string.IsNullOrEmpty(SpeechText))
            {
                _bubbleText.Text = SpeechText;
                _bubble.Visibility = Visibility.Visible;
                _bubble.Measure(new Size(220, 60));
                var bw = _bubble.DesiredSize.Width;
                var bh = _bubble.DesiredSize.Height;
                // 개 이미지 위에 말풍선 배치 (위로 약간 overflow 허용)
                Canvas.SetLeft(_bubble, X - bw / 2);
                Canvas.SetTop(_bubble, _groundY - _imgSize + _bounceOffset - bh - 5);
            }
            else
            {
                _bubble.Visibility = Visibility.Collapsed;
            }

            Reposition();
        }

        private void Reposition()
        {
            Canvas.SetLeft(_dogEl, X - _imgSize / 2);
            Canvas.SetTop(_dogEl, _groundY - _imgSize);
            Canvas.SetLeft(_nameLabel, X - 36);
            Canvas.SetTop(_nameLabel, _groundY + 2);
        }

        // ── 이미지 로드 (AvatarToImageConverter와 동일한 로직, 별도 캐시) ──
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource?> _imgCache = new();

        private static ImageSource? LoadAvatar(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (_imgCache.TryGetValue(path, out var cached)) return cached;

            ImageSource? result = null;
            try
            {
                var bmp = new BitmapImage();
                if (path.StartsWith("data:image/"))
                {
                    var b64 = path[(path.IndexOf(',', StringComparison.Ordinal) + 1)..];
                    var bytes = Convert.FromBase64String(b64);
                    using var ms = new MemoryStream(bytes);
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    result = bmp;
                }
                else if (File.Exists(path))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    result = bmp;
                }
            }
            catch { }

            _imgCache[path] = result;
            return result;
        }

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
            _        => Color.FromRgb(160, 145, 130)
        };
    }
}
