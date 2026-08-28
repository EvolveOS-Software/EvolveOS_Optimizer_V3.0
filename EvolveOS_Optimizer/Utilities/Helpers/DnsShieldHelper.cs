// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public class DnsShieldHelper
    {
        #region Fields
        private Canvas? _canvas;
        private Path? _shieldBase;
        private Path? _leftFacet;
        private Path? _networkWireframe;
        private Path? _scanner;
        private Path? _centerSymbolShadow;
        private Path? _centerSymbol;

        private Storyboard? _scannerStoryboard;
        private RotateTransform? _borderRotateTransform;

        private DispatcherTimer? _edgeGlowTimer;
        #endregion

        #region Geometry
        private PathGeometry CreateShieldGeometry()
        {
            var geo = new PathGeometry();
            var figure = new PathFigure { StartPoint = new Point(35, 5), IsClosed = true };
            figure.Segments.Add(new LineSegment { Point = new Point(65, 20) });
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(65, 50),
                Point2 = new Point(35, 75),
                Point3 = new Point(35, 75)
            });
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(35, 75),
                Point2 = new Point(5, 50),
                Point3 = new Point(5, 20)
            });
            geo.Figures.Add(figure);
            return geo;
        }
        #endregion

        #region Initialization
        public void Initialize(Canvas targetCanvas)
        {
            _canvas = targetCanvas;
            _canvas.Children.Clear();
            _canvas.Resources.Clear();

            _borderRotateTransform = new RotateTransform { CenterX = 35, CenterY = 40 };

            var premiumBorderBrush = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(35, 0),
                EndPoint = new Point(35, 80),
                Transform = _borderRotateTransform,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Offset = 0.0 },
                    new GradientStop { Offset = 0.05 },
                    new GradientStop { Offset = 0.25 },
                    new GradientStop { Offset = 1.0 }
                }
            };

            _shieldBase = new Path
            {
                Data = CreateShieldGeometry(),
                Fill = new SolidColorBrush(Color.FromArgb(70, 0, 120, 215)),
                StrokeThickness = 2.5,
                Stroke = premiumBorderBrush
            };
            _canvas.Children.Add(_shieldBase);

            var leftFacetGeo = new PathGeometry();
            var leftFigure = new PathFigure { StartPoint = new Point(35, 5), IsClosed = true };
            leftFigure.Segments.Add(new LineSegment { Point = new Point(5, 20) });
            leftFigure.Segments.Add(new BezierSegment { Point1 = new Point(5, 50), Point2 = new Point(35, 75), Point3 = new Point(35, 75) });
            leftFigure.Segments.Add(new LineSegment { Point = new Point(35, 5) });
            leftFacetGeo.Figures.Add(leftFigure);

            _leftFacet = new Path
            {
                Data = leftFacetGeo,
                Fill = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255))
            };
            _canvas.Children.Add(_leftFacet);

            var wireframeGeo = new PathGeometry();
            var wireFig1 = new PathFigure { StartPoint = new Point(35, 5), IsClosed = false };
            wireFig1.Segments.Add(new LineSegment { Point = new Point(35, 75) });
            var wireFig2 = new PathFigure { StartPoint = new Point(10, 40), IsClosed = false };
            wireFig2.Segments.Add(new LineSegment { Point = new Point(60, 40) });
            var wireFig3 = new PathFigure { StartPoint = new Point(18, 22), IsClosed = false };
            wireFig3.Segments.Add(new LineSegment { Point = new Point(52, 58) });
            var wireFig4 = new PathFigure { StartPoint = new Point(52, 22), IsClosed = false };
            wireFig4.Segments.Add(new LineSegment { Point = new Point(18, 58) });

            wireframeGeo.Figures.Add(wireFig1);
            wireframeGeo.Figures.Add(wireFig2);
            wireframeGeo.Figures.Add(wireFig3);
            wireframeGeo.Figures.Add(wireFig4);

            _networkWireframe = new Path
            {
                Data = wireframeGeo,
                StrokeThickness = 1.2,
                Opacity = 0.4
            };
            _canvas.Children.Add(_networkWireframe);

            var scannerTransform = new TranslateTransform { Y = -25 };

            _scanner = new Path
            {
                Data = CreateShieldGeometry(),
                Fill = new LinearGradientBrush
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 25),
                    Transform = scannerTransform,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 0.0 },
                        new GradientStop { Color = Color.FromArgb(180, 255, 255, 255), Offset = 0.5 },
                        new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1.0 }
                    }
                },
                Visibility = Visibility.Collapsed
            };
            _canvas.Children.Add(_scanner);

            _centerSymbolShadow = new Path
            {
                StrokeThickness = 5.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                RenderTransform = new TranslateTransform { X = 0, Y = 2 },
                Visibility = Visibility.Collapsed
            };
            _canvas.Children.Add(_centerSymbolShadow);

            _centerSymbol = new Path
            {
                StrokeThickness = 5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Visibility = Visibility.Collapsed
            };
            _canvas.Children.Add(_centerSymbol);

            _scannerStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
            var sweepAnimation = new DoubleAnimation
            {
                From = -25,
                To = 80,
                Duration = new Duration(TimeSpan.FromSeconds(0.9)),
                AutoReverse = true,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(sweepAnimation, scannerTransform);
            Storyboard.SetTargetProperty(sweepAnimation, "Y");
            _scannerStoryboard.Children.Add(sweepAnimation);
            _canvas.Resources.Add("ScannerStory", _scannerStoryboard);

            _edgeGlowTimer?.Stop();
            _edgeGlowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 FPS
            _edgeGlowTimer.Tick += (s, e) =>
            {
                if (_borderRotateTransform != null)
                {
                    _borderRotateTransform.Angle = (_borderRotateTransform.Angle + 2.5) % 360;
                }
            };
            _edgeGlowTimer.Start();
        }
        #endregion

        #region State Management
        public void SetState(bool isConnecting, bool isRunning)
        {
            if (_centerSymbol == null || _centerSymbolShadow == null || _scanner == null) return;

            if (isConnecting)
            {
                // Blue Scanning/Connecting State
                SetColors(Color.FromArgb(255, 0, 140, 255), Color.FromArgb(255, 0, 230, 255));
                _centerSymbol.Visibility = Visibility.Collapsed;
                _centerSymbolShadow.Visibility = Visibility.Collapsed;
                _scanner.Visibility = Visibility.Visible;
                _scannerStoryboard?.Begin();
                return;
            }

            _scannerStoryboard?.Stop();
            _scanner.Visibility = Visibility.Collapsed;
            _centerSymbol.Visibility = Visibility.Visible;
            _centerSymbolShadow.Visibility = Visibility.Visible;

            if (isRunning)
            {
                // Green Running/Secure State
                SetSymbolGeometry(true);
                SetColors(Color.FromArgb(255, 46, 139, 87), Color.FromArgb(255, 80, 240, 140));
            }
            else
            {
                // Red Stopped/Disconnected State
                SetSymbolGeometry(false);
                SetColors(Color.FromArgb(255, 232, 17, 35), Color.FromArgb(255, 255, 80, 100));
            }
        }

        private void SetSymbolGeometry(bool isRunning)
        {
            var geo1 = new PathGeometry();
            var geo2 = new PathGeometry();

            void PopulateGeometry(PathGeometry geo)
            {
                if (isRunning)
                {
                    // Checkmark
                    var fig = new PathFigure { StartPoint = new Point(24, 42), IsClosed = false };
                    fig.Segments.Add(new LineSegment { Point = new Point(32, 50) });
                    fig.Segments.Add(new LineSegment { Point = new Point(46, 30) });
                    geo.Figures.Add(fig);
                }
                else
                {
                    // X Mark
                    var fig1 = new PathFigure { StartPoint = new Point(26, 30), IsClosed = false };
                    fig1.Segments.Add(new LineSegment { Point = new Point(44, 48) });

                    var fig2 = new PathFigure { StartPoint = new Point(44, 30), IsClosed = false };
                    fig2.Segments.Add(new LineSegment { Point = new Point(26, 48) });

                    geo.Figures.Add(fig1);
                    geo.Figures.Add(fig2);
                }
            }

            PopulateGeometry(geo1);
            PopulateGeometry(geo2);

            if (_centerSymbol != null) _centerSymbol.Data = geo1;
            if (_centerSymbolShadow != null) _centerSymbolShadow.Data = geo2;
        }

        private void SetColors(Color baseColor, Color brightSymbolColor)
        {
            if (_shieldBase != null)
            {
                if (_shieldBase.Stroke is LinearGradientBrush borderBrush && borderBrush.GradientStops.Count == 4)
                {
                    borderBrush.GradientStops[0].Color = Colors.White;
                    borderBrush.GradientStops[1].Color = brightSymbolColor;
                    borderBrush.GradientStops[2].Color = baseColor;
                    borderBrush.GradientStops[3].Color = baseColor;
                }

                _shieldBase.Fill = new SolidColorBrush(Color.FromArgb(70, baseColor.R, baseColor.G, baseColor.B));
            }

            if (_networkWireframe != null)
            {
                _networkWireframe.Stroke = new SolidColorBrush(baseColor);
            }

            if (_centerSymbol != null)
            {
                _centerSymbol.Stroke = new SolidColorBrush(brightSymbolColor);
            }

            if (_scanner != null && _scanner.Fill is LinearGradientBrush scannerBrush && scannerBrush.GradientStops.Count >= 3)
            {
                scannerBrush.GradientStops[0].Color = Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B);
                scannerBrush.GradientStops[1].Color = Color.FromArgb(180, baseColor.R, baseColor.G, baseColor.B);
                scannerBrush.GradientStops[2].Color = Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B);
            }
        }

        public void UpdateSize(bool isExpanded)
        {
            if (_canvas == null) return;

            double scale = isExpanded ? 1.5 : 1.0;

            _canvas.RenderTransform = new ScaleTransform
            {
                ScaleX = scale,
                ScaleY = scale,
                CenterX = 35,
                CenterY = 40
            };
        }
        #endregion
    }
}