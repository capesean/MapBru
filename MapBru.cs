/* coded by sean walsh: http://www.capesean.co.za */
// based on code from: https://mapviewer.codeplex.com/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Microsoft.SqlServer.Types;

namespace WEB
{
	public class MapBru
	{
		internal List<Datum> Data { get; private set; }
		public Color FillColor { get; set; }
		public Color StrokeColor { get; set; }
		public int StrokeWidth { get; set; }
		public int ImageWidth { get; private set; }
		public int ImageHeight { get; private set; }
		public int Padding { get; private set; }

		public MapBru(int imageWidth, int imageHeight, int padding = 0)
		{
			ImageWidth = imageWidth;
			ImageHeight = imageHeight;
			Padding = padding;
			Data = new List<Datum>();
			FillColor = Color.White;
			StrokeColor = Color.Black;
			StrokeWidth = 1;
		}

		public Bitmap GetBitMap()
		{
			if (Data.Count == 0) throw new InvalidOperationException("No data");

			var geometries = Data.Select(d => d.Geometry).ToList();

			// get the bounds (max & min extents) for the geometries
			var bounds = GetBounds(geometries);

			// get the width & height of the extents
			var geoWidth = (float)(bounds.XMax - bounds.XMin);
			var geoHeight = (float)(bounds.YMax - bounds.YMin);

			// get the ratios
			var imageRatio = (ImageWidth - 2 * Padding) / (ImageHeight - 2 * Padding);
			var geoRatio = geoWidth / geoHeight;

			// get the scale that the geo needs to be scaled to, to fit the image (taking padding into consideration)
			var scale = geoRatio > imageRatio ? (ImageWidth - 2 * Padding) / geoWidth : (ImageHeight - 2 * Padding) / geoHeight;

			// if geo proportions aren't the same as image proportions, add some spacing to center the image on the 'canvas'
			var centerSpacingX = geoRatio > imageRatio ? 0 : (ImageWidth - 2 * Padding - geoWidth * scale) / 2;
			var centerSpacingy = geoRatio > imageRatio ? (ImageHeight - 2 * Padding - geoHeight * scale) / 2 : 0;

			// get the offsets, which shifts the geo points to the 'origin' of the image
			// include the padding and centering spacing (which need the scale applied to get them from image points to geo points)
			var offsets = new PointF((float)bounds.XMin - (Padding + centerSpacingX) / scale, (float)bounds.YMin - (Padding + centerSpacingy) / scale);

			// create the bitmap
			var bitmap = new Bitmap(ImageWidth, ImageHeight);

			try
			{
				// use graphics
				using (var graphics = Graphics.FromImage(bitmap))
				{
					// for each data item
					foreach (var datum in Data)
					{
						// convert the geometry into a list of paths
						var paths = ConvertToPaths(datum.Geometry, scale, offsets);

						// for each of the paths
						foreach (var path in paths)
						{
							// fill the shape
							using (var brush = new SolidBrush(datum.FillColor))
							{
								graphics.FillPath(brush, path);
							}

							// draw the line
							using (var pen = new Pen(datum.StrokeColor, datum.StrokeWidth))
							{
								graphics.DrawPath(pen, path);
							}
						}
					}
				}

				// y-axis points are inverse to geometry, so flip vertically
				bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
			}
			catch
			{
				// dispose of the bitmap
				bitmap.Dispose();
				throw;
			}

			return bitmap;
		}

		public List<GraphicsPath> ConvertToPaths(SqlGeometry geometry, float scale, PointF offsets)
		{
			var list = new List<GraphicsPath>((int)geometry.STNumGeometries());
			var path = new GraphicsPath();

			for (var index = 0; index < geometry.STNumGeometries(); index++)
			{
				// access geometry and extract its points
				var subgeometry = geometry.STGeometryN(index + 1);

				if (subgeometry.STGeometryType() == "Polygon")
				{
					// process exterior ring
					var points = PointsFromGeom(subgeometry.STExteriorRing());
					path.AddPolygon(points);

					// process any interior rings
					for (var interiorindex = 0; interiorindex < subgeometry.STNumInteriorRing(); interiorindex++)
					{
						points = PointsFromGeom(subgeometry.STInteriorRingN(interiorindex + 1));
						path.AddPolygon(points);
					}
				}
				else
				{
					var points = PointsFromGeom(subgeometry);
					path.AddLines(points);
				}
			}

			// move and scale the path: this will a) position the drawn shapes where we can see them and b) scale them to fit in the requested bitmap size
			using (var m = new Matrix())
			{
				m.Translate(-offsets.X, -offsets.Y);
				m.Scale(scale, scale, MatrixOrder.Append);
				path.Transform(m);
			}

			// add path to the list
			list.Add(path);

			return list;
		}

		private PointF[] PointsFromGeom(SqlGeometry geometry)
		{
			var points = new PointF[(Int32)(geometry.STNumPoints())];

			for (var pointFIndex = 0; pointFIndex < geometry.STNumPoints(); pointFIndex++)
			{
				var geomPointF = geometry.STPointN(pointFIndex + 1);
				points[pointFIndex] = new PointF((float)geomPointF.STX.Value, (float)geomPointF.STY.Value);
			}
			return points;
		}

		private Bounds GetBounds(IEnumerable<SqlGeometry> geoms)
		{
			var bounds = new Bounds();

			bounds.XMin = double.MaxValue;
			bounds.XMax = double.MinValue;
			bounds.YMin = double.MaxValue;
			bounds.YMax = double.MinValue;

			// Loop through each geometry in the dataset
			foreach (var geom in geoms)
			{

				// Loop through each point in this geometry
				for (var i = 1; i <= geom.STNumPoints(); i++)
				{
					var point = geom.STPointN(i);

					// Check whether this point is a new min/max value
					if (point.STX.Value < bounds.XMin)
					{ bounds.XMin = point.STX.Value; }
					else if (point.STX.Value > bounds.XMax)
					{
						bounds.XMax = point.STX.Value;
					}

					if (point.STY.Value < bounds.YMin)
					{ bounds.YMin = point.STY.Value; }
					else if (point.STY.Value > bounds.YMax)
					{ bounds.YMax = point.STY.Value; }
				}
			}
			return bounds;
		}

		#region AddData overloads
		public void AddData(SqlGeometry geometry)
		{
			Add(geometry, FillColor, StrokeColor, StrokeWidth);
		}

		public void AddData(SqlGeometry geometry, Color fillColor)
		{
			Add(geometry, fillColor, StrokeColor, StrokeWidth);
		}

		public void AddData(SqlGeometry geometry, Color fillColor, Color strokeColor)
		{
			Add(geometry, fillColor, strokeColor, StrokeWidth);
		}

		public void AddData(SqlGeometry geometry, Color fillColor, Color strokeColor, int strokeWidth)
		{
			Add(geometry, fillColor, strokeColor, strokeWidth);
		}

		private void Add(SqlGeometry geometry, Color fillColor, Color strokeColor, int strokeWidth)
		{
			Data.Add(new Datum(geometry, fillColor, strokeColor, strokeWidth));
		}
		#endregion

		internal class Bounds
		{
			public double XMin;
			public double XMax;
			public double YMin;
			public double YMax;
		}

		internal class Datum
		{
			public SqlGeometry Geometry { get; private set; }
			public Color FillColor { get; private set; }
			public Color StrokeColor { get; private set; }
			public int StrokeWidth { get; private set; }

			public Datum(SqlGeometry geometry, Color fillColor, Color strokeColor, int strokeWidth)
			{
				Geometry = geometry;
				FillColor = fillColor;
				StrokeColor = strokeColor;
				StrokeWidth = strokeWidth;
			}
		}

	}
}
