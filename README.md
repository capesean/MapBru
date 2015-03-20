# MapBru
A C# .NET tool for drawing SqlGeometry objects on a bitmap image, to create maps etc.

Usage would be along the lines of:

```C#
// new up a mapBru object
var mapBru = new MapBru(800, 640, 5);

// set some default parameters
mapBru.FillColor = Color.Yellow;
mapBru.StrokeColor = Color.Green;
mapBru.StrokeWidth = 2;

// get a collection of geometries
var geometries = getGeometries(); // implement this!

// add geometries to mapBru
foreach (var geometry in geometries)
	mapBru.AddData(geometry);

// or, add a data item with one of the overloads...
var geometry = getGeometry(); // implement this!
	mapBru.AddData(geometry, Color.Red, Color.Black, 3);

// save the file
using (var bitmap = mapBru.GetBitMap())
{
	bitmap.Save(@"C:\test.png");
}
```
