// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    // Plasma Core
    public class GamingCoreHelper
    {
        #region Fields
        private Canvas? _canvas;
        private Path? _coreBase;
        private Path? _coreFacets;
        private Path? _coreCircuit;
        private Path? _scanner;
        private Path? _centerTurbine;
        private Path? _centerTurbineShadow;

        private RotateTransform? _borderRotateTransform;

        private DispatcherTimer? _animationTimer;
        private bool _isScannerActive = false;
        private double _scannerY = -25;
        private double _scannerDirection = 1;
        private readonly double _scannerSpeed = 2.0;
        #endregion

        #region Geometry
        private PathGeometry CreateHexCoreGeometry(double cx, double cy, double radius)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { IsClosed = true };

            for (int i = 0; i < 6; i++)
            {
                double angleDeg = (60 * i) - 30;
                double angleRad = Math.PI / 180.0 * angleDeg;
                var pt = new Point(cx + (radius * Math.Cos(angleRad)), cy + (radius * Math.Sin(angleRad)));

                if (i == 0) fig.StartPoint = pt;
                else fig.Segments.Add(new LineSegment { Point = pt });
            }

            geo.Figures.Add(fig);
            return geo;
        }

        private PathGeometry CreateTurbineGeometry(double cx, double cy)
        {
            var geo = new PathGeometry();

            // Turbine Cross / Core Blades
            var fig1 = new PathFigure { StartPoint = new Point(cx, cy - 12), IsClosed = false };
            fig1.Segments.Add(new LineSegment { Point = new Point(cx, cy + 12) });

            var fig2 = new PathFigure { StartPoint = new Point(cx - 12, cy), IsClosed = false };
            fig2.Segments.Add(new LineSegment { Point = new Point(cx + 12, cy) });

            var fig3 = new PathFigure { StartPoint = new Point(cx - 8, cy - 8), IsClosed = false };
            fig3.Segments.Add(new LineSegment { Point = new Point(cx + 8, cy + 8) });

            var fig4 = new PathFigure { StartPoint = new Point(cx + 8, cy - 8), IsClosed = false };
            fig4.Segments.Add(new LineSegment { Point = new Point(cx - 8, cy + 8) });

            geo.Figures.Add(fig1);
            geo.Figures.Add(fig2);
            geo.Figures.Add(fig3);
            geo.Figures.Add(fig4);

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

            // 1. Rotating Gradient Border Brush
            var premiumBorderBrush = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(35, 0),
                EndPoint = new Point(35, 80),
                Transform = _borderRotateTransform,
                GradientStops = new GradientStopCollection
                {
                    new GradientStop { Offset = 0.0, Color = Colors.White },
                    new GradientStop { Offset = 0.15, Color = Color.FromArgb(255, 0, 230, 255) },
                    new GradientStop { Offset = 0.45, Color = Color.FromArgb(120, 0, 120, 215) },
                    new GradientStop { Offset = 1.0, Color = Color.FromArgb(40, 0, 120, 215) }
                }
            };

            // 2. Base Outer Hexagon
            _coreBase = new Path
            {
                Data = CreateHexCoreGeometry(35, 40, 32),
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215)),
                StrokeThickness = 2.5,
                Stroke = premiumBorderBrush
            };
            _canvas.Children.Add(_coreBase);

            // 3. Inner Angular Shading Facets
            var facetGeo = new PathGeometry();
            var facetFig = new PathFigure { StartPoint = new Point(35, 8), IsClosed = true };
            facetFig.Segments.Add(new LineSegment { Point = new Point(7, 24) });
            facetFig.Segments.Add(new LineSegment { Point = new Point(7, 56) });
            facetFig.Segments.Add(new LineSegment { Point = new Point(35, 40) });
            facetGeo.Figures.Add(facetFig);

            _coreFacets = new Path
            {
                Data = facetGeo,
                Fill = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255))
            };
            _canvas.Children.Add(_coreFacets);

            // 4. Circuit Wireframe Geometry
            var circuitGeo = new PathGeometry();
            var cFig1 = new PathFigure { StartPoint = new Point(35, 8), IsClosed = false };
            cFig1.Segments.Add(new LineSegment { Point = new Point(35, 72) });

            var cFig2 = new PathFigure { StartPoint = new Point(7, 24), IsClosed = false };
            cFig2.Segments.Add(new LineSegment { Point = new Point(63, 56) });

            var cFig3 = new PathFigure { StartPoint = new Point(7, 56), IsClosed = false };
            cFig3.Segments.Add(new LineSegment { Point = new Point(63, 24) });

            circuitGeo.Figures.Add(cFig1);
            circuitGeo.Figures.Add(cFig2);
            circuitGeo.Figures.Add(cFig3);

            _coreCircuit = new Path
            {
                Data = circuitGeo,
                StrokeThickness = 1.0,
                Stroke = new SolidColorBrush(Color.FromArgb(100, 0, 200, 255)),
                Opacity = 0.5
            };
            _canvas.Children.Add(_coreCircuit);

            // 5. Linear Scanner Beam (Active during optimization)
            _scannerY = -25;
            _scannerDirection = 1;
            var scannerTransform = new TranslateTransform { Y = _scannerY };

            _scanner = new Path
            {
                Data = CreateHexCoreGeometry(35, 40, 32),
                Fill = new LinearGradientBrush
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 25),
                    Transform = scannerTransform,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Color.FromArgb(0, 0, 230, 255), Offset = 0.0 },
                        new GradientStop { Color = Color.FromArgb(200, 0, 255, 255), Offset = 0.5 },
                        new GradientStop { Color = Color.FromArgb(0, 0, 230, 255), Offset = 1.0 }
                    }
                },
                Visibility = Visibility.Collapsed
            };
            _canvas.Children.Add(_scanner);

            // 6. Central Core Turbine / Symbol
            _centerTurbineShadow = new Path
            {
                Data = CreateTurbineGeometry(35, 40),
                StrokeThickness = 4.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                RenderTransform = new TranslateTransform { X = 0, Y = 2 }
            };
            _canvas.Children.Add(_centerTurbineShadow);

            _centerTurbine = new Path
            {
                Data = CreateTurbineGeometry(35, 40),
                StrokeThickness = 3.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 180, 180, 180))
            };
            _canvas.Children.Add(_centerTurbine);

            // 7. Dispatcher Timer Loop
            _animationTimer?.Stop();
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60 FPS
            _animationTimer.Tick += (s, e) =>
            {
                // Smooth rotation of the border glow
                if (_borderRotateTransform != null)
                {
                    double speed = _isScannerActive ? 6.0 : 2.0;
                    _borderRotateTransform.Angle = (_borderRotateTransform.Angle + speed) % 360;
                }

                // Smooth laser beam translation
                if (_isScannerActive && _scanner?.Fill is LinearGradientBrush brush && brush.Transform is TranslateTransform trans)
                {
                    _scannerY += _scannerSpeed * _scannerDirection;
                    if (_scannerY >= 80)
                    {
                        _scannerY = 80;
                        _scannerDirection = -1;
                    }
                    else if (_scannerY <= -25)
                    {
                        _scannerY = -25;
                        _scannerDirection = 1;
                    }
                    trans.Y = _scannerY;
                }
            };
            _animationTimer.Start();
        }
        #endregion

        #region State Management
        public void SetState(bool isOptimizing, bool isActive)
        {
            if (_coreBase == null || _coreCircuit == null || _centerTurbine == null || _scanner == null) return;

            _isScannerActive = isOptimizing;

            if (isOptimizing)
            {
                SetColors(
                    baseColor: Color.FromArgb(255, 0, 120, 215),
                    highlightColor: Color.FromArgb(255, 0, 240, 255),
                    fillOpacity: 50);

                _scanner.Visibility = Visibility.Visible;
                _centerTurbine.Visibility = Visibility.Collapsed;
                if (_centerTurbineShadow != null) _centerTurbineShadow.Visibility = Visibility.Collapsed;
            }
            else if (isActive)
            {
                SetColors(
                    baseColor: Color.FromArgb(255, 46, 139, 87),
                    highlightColor: Color.FromArgb(255, 57, 255, 20), // Neon Gaming Green
                    fillOpacity: 70);

                _scanner.Visibility = Visibility.Collapsed;
                _centerTurbine.Visibility = Visibility.Visible;
                if (_centerTurbineShadow != null) _centerTurbineShadow.Visibility = Visibility.Visible;
            }
            else
            {
                SetColors(
                    baseColor: Color.FromArgb(255, 120, 120, 120),
                    highlightColor: Color.FromArgb(255, 190, 190, 190), // Stealth Gray
                    fillOpacity: 25);

                _scanner.Visibility = Visibility.Collapsed;
                _centerTurbine.Visibility = Visibility.Visible;
                if (_centerTurbineShadow != null) _centerTurbineShadow.Visibility = Visibility.Visible;
            }
        }

        private void SetColors(Color baseColor, Color highlightColor, byte fillOpacity)
        {
            if (_coreBase != null)
            {
                if (_coreBase.Stroke is LinearGradientBrush borderBrush && borderBrush.GradientStops.Count >= 4)
                {
                    borderBrush.GradientStops[0].Color = Colors.White;
                    borderBrush.GradientStops[1].Color = highlightColor;
                    borderBrush.GradientStops[2].Color = baseColor;
                    borderBrush.GradientStops[3].Color = baseColor;
                }

                _coreBase.Fill = new SolidColorBrush(Color.FromArgb(fillOpacity, baseColor.R, baseColor.G, baseColor.B));
            }

            if (_coreCircuit != null)
            {
                _coreCircuit.Stroke = new SolidColorBrush(highlightColor);
            }

            if (_centerTurbine != null)
            {
                _centerTurbine.Stroke = new SolidColorBrush(highlightColor);
            }

            if (_scanner != null && _scanner.Fill is LinearGradientBrush scannerBrush && scannerBrush.GradientStops.Count >= 3)
            {
                scannerBrush.GradientStops[0].Color = Color.FromArgb(0, highlightColor.R, highlightColor.G, highlightColor.B);
                scannerBrush.GradientStops[1].Color = Color.FromArgb(200, highlightColor.R, highlightColor.G, highlightColor.B);
                scannerBrush.GradientStops[2].Color = Color.FromArgb(0, highlightColor.R, highlightColor.G, highlightColor.B);
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

    // GameController
    public class GamingControllerHelper
    {
        #region Fields
        private Canvas? _canvas;
        private Path? _controllerBody;
        private Path? _dpad;
        private Path? _buttons;
        private Path? _scannerOverlay;

        private TranslateTransform? _scannerTranslate;
        private ScaleTransform? _buttonsScale;

        private DispatcherTimer? _animationTimer;
        private bool _isOptimizing = false;
        private bool _isActive = false;

        private double _scannerY = -20;
        private double _pulseScale = 1.0;
        private double _pulseDir = 0.01;
        #endregion

        #region Initialization
        public void Initialize(Canvas targetCanvas)
        {
            _canvas = targetCanvas;
            _canvas.Children.Clear();

            _scannerTranslate = new TranslateTransform { Y = -20 };
            _buttonsScale = new ScaleTransform { CenterX = 35, CenterY = 40 };

            _controllerBody = new Path
            {
                Data = CreateControllerGeometry(),
                StrokeThickness = 2.5,
                StrokeLineJoin = PenLineJoin.Round,
                Stroke = new SolidColorBrush(Color.FromArgb(150, 150, 150, 150)),
                Fill = new SolidColorBrush(Color.FromArgb(20, 150, 150, 150))
            };
            _canvas.Children.Add(_controllerBody);

            var dpadGeo = new PathGeometry();
            var dpadFig = new PathFigure { StartPoint = new Point(22, 35), IsClosed = false };
            dpadFig.Segments.Add(new LineSegment { Point = new Point(22, 45) });
            var dpadFig2 = new PathFigure { StartPoint = new Point(17, 40), IsClosed = false };
            dpadFig2.Segments.Add(new LineSegment { Point = new Point(27, 40) });
            dpadGeo.Figures.Add(dpadFig);
            dpadGeo.Figures.Add(dpadFig2);

            _dpad = new Path
            {
                Data = dpadGeo,
                StrokeThickness = 2,
                Stroke = new SolidColorBrush(Color.FromArgb(200, 150, 150, 150)),
                RenderTransform = _buttonsScale
            };
            _canvas.Children.Add(_dpad);

            var btnGeo = new GeometryGroup();
            btnGeo.Children.Add(new EllipseGeometry { Center = new Point(48, 35), RadiusX = 1.5, RadiusY = 1.5 });
            btnGeo.Children.Add(new EllipseGeometry { Center = new Point(48, 45), RadiusX = 1.5, RadiusY = 1.5 });
            btnGeo.Children.Add(new EllipseGeometry { Center = new Point(43, 40), RadiusX = 1.5, RadiusY = 1.5 });
            btnGeo.Children.Add(new EllipseGeometry { Center = new Point(53, 40), RadiusX = 1.5, RadiusY = 1.5 });

            _buttons = new Path
            {
                Data = btnGeo,
                Fill = new SolidColorBrush(Color.FromArgb(200, 150, 150, 150)),
                RenderTransform = _buttonsScale
            };
            _canvas.Children.Add(_buttons);

            _scannerOverlay = new Path
            {
                Data = CreateControllerGeometry(),
                Fill = new LinearGradientBrush
                {
                    MappingMode = BrushMappingMode.Absolute,
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 20),
                    Transform = _scannerTranslate,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = Colors.Transparent, Offset = 0.0 },
                        new GradientStop { Color = Color.FromArgb(200, 0, 230, 255), Offset = 0.8 },
                        new GradientStop { Color = Colors.White, Offset = 1.0 }
                    }
                },
                Visibility = Visibility.Collapsed
            };
            _canvas.Children.Add(_scannerOverlay);

            _animationTimer?.Stop();
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _animationTimer.Tick += (s, e) =>
            {
                if (_isOptimizing && _scannerTranslate != null)
                {
                    _scannerY += 2.5;
                    if (_scannerY > 70) _scannerY = -20;
                    _scannerTranslate.Y = _scannerY;
                }

                if (_isActive && _buttonsScale != null)
                {
                    _pulseScale += _pulseDir;
                    if (_pulseScale > 1.08) { _pulseScale = 1.08; _pulseDir = -0.01; }
                    else if (_pulseScale < 0.95) { _pulseScale = 0.95; _pulseDir = 0.01; }

                    _buttonsScale.ScaleX = _pulseScale;
                    _buttonsScale.ScaleY = _pulseScale;
                }
            };
            _animationTimer.Start();
        }
        #endregion

        #region Geometry
        private PathGeometry CreateControllerGeometry()
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(20, 25), IsClosed = true };
            fig.Segments.Add(new LineSegment { Point = new Point(50, 25) }); // Top flat
            fig.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(65, 25), Point2 = new Point(65, 40) }); // Right shoulder
            fig.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(65, 60), Point2 = new Point(55, 60) }); // Right grip bottom
            fig.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(45, 60), Point2 = new Point(40, 48) }); // Right inner grip
            fig.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(35, 43), Point2 = new Point(30, 48) }); // Center dip
            fig.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(25, 60), Point2 = new Point(15, 60) }); // Left inner grip
            fig.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(5, 60), Point2 = new Point(5, 40) }); // Left grip bottom
            fig.Segments.Add(new QuadraticBezierSegment { Point1 = new Point(5, 25), Point2 = new Point(20, 25) }); // Left shoulder
            geo.Figures.Add(fig);
            return geo;
        }
        #endregion

        #region State Management
        public void SetState(bool isOptimizing, bool isActive)
        {
            _isOptimizing = isOptimizing;
            _isActive = isActive;

            if (_controllerBody == null || _dpad == null || _buttons == null || _scannerOverlay == null || _buttonsScale == null) return;

            if (isOptimizing)
            {
                _controllerBody.Stroke = new SolidColorBrush(Color.FromArgb(200, 0, 180, 255));
                _controllerBody.Fill = new SolidColorBrush(Color.FromArgb(30, 0, 180, 255));
                _scannerOverlay.Visibility = Visibility.Visible;

                _buttonsScale.ScaleX = 1; _buttonsScale.ScaleY = 1; // Reset pulse
            }
            else if (isActive)
            {
                var accent = Color.FromArgb(255, 57, 255, 20); // Neon Green

                _controllerBody.Stroke = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop { Color = accent, Offset = 0.0 },
                        new GradientStop { Color = Color.FromArgb(255, 0, 200, 100), Offset = 1.0 }
                    }
                };
                _controllerBody.Fill = new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B));
                _dpad.Stroke = new SolidColorBrush(Colors.White);
                _buttons.Fill = new SolidColorBrush(Colors.White);
                _scannerOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                var gray = Color.FromArgb(255, 150, 150, 150);
                _controllerBody.Stroke = new SolidColorBrush(gray);
                _controllerBody.Fill = new SolidColorBrush(Color.FromArgb(20, gray.R, gray.G, gray.B));
                _dpad.Stroke = new SolidColorBrush(Color.FromArgb(200, gray.R, gray.G, gray.B));
                _buttons.Fill = new SolidColorBrush(Color.FromArgb(200, gray.R, gray.G, gray.B));
                _scannerOverlay.Visibility = Visibility.Collapsed;

                _buttonsScale.ScaleX = 1; _buttonsScale.ScaleY = 1;
            }
        }

        public void UpdateSize(bool isExpanded)
        {
            if (_canvas == null) return;
            double scale = isExpanded ? 1.5 : 1.0;
            _canvas.RenderTransform = new ScaleTransform { ScaleX = scale, ScaleY = scale, CenterX = 35, CenterY = 40 };
        }
        #endregion
    }
}