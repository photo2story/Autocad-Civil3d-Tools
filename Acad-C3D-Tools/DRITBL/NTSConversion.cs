﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;

namespace IntersectUtilities.DRITBL
{
    internal static class NTSConversion
    {
        public static Polygon ConvertClosedPlineToNTSPolygon(Polyline pline)
        {
            var points = new List<Coordinate>();
            for (int i = 0; i < pline.NumberOfVertices; i++)
                points.Add(new Coordinate(pline.GetPoint2dAt(i).X, pline.GetPoint2dAt(i).Y));
            points.Add(new Coordinate(pline.GetPoint3dAt(0).X, pline.GetPoint3dAt(0).Y));
            return new Polygon(new LinearRing(points.ToArray()));
        }
        public static LineString ConvertPlineToNTSLineString(Polyline pline)
        {
            var points = new List<Coordinate>();
            var samplePoints = pline.GetSamplePoints();
            foreach (var samplePoint in samplePoints)
                points.Add(new Coordinate(samplePoint.X, samplePoint.Y));
            return new LineString(points.ToArray());
        }
    }
}
