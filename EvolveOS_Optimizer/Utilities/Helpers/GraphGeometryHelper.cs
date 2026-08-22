// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using Windows.Foundation;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class GraphGeometryHelper
    {
        public static Geometry CreatePathGeometry(List<Point> points, bool isArea)
        {
            var geometry = new PathGeometry();
            if (points == null || points.Count == 0) return geometry;

            var figure = new PathFigure
            {
                StartPoint = points[0],
                IsClosed = isArea,
                IsFilled = isArea
            };

            var segment = new PolyLineSegment();
            var pointCollection = new PointCollection();

            for (int i = 1; i < points.Count; i++)
            {
                pointCollection.Add(points[i]);
            }

            segment.Points = pointCollection;
            figure.Segments.Add(segment);
            geometry.Figures.Add(figure);

            return geometry;
        }
    }
}