# ShortLineChartObject Documentation

## Overview
`ShortLineChartObject` is a specialized chart drawing object in the JForex Dukascopy trading platform designed for creating short, precise line segments on price charts. This object is particularly useful for marking specific price levels, trend lines, or short-term support/resistance areas.

## Class Information
- **Package**: `com.dukascopy.charts.drawings`
- **Class**: `ShortLineChartObject`
- **Inheritance**: Extends `LineChartObject` or similar base class
- **Purpose**: Creates short, precise line segments on trading charts

## Features

### Basic Properties
- **Shape**: Straight line segment with defined start and end points
- **Length**: Optimized for short line segments (typically within a few bars)
- **Style**: Configurable line style, color, and thickness
- **Position**: Defined by start and end points with precise coordinates

### Drawing Modes
1. **Manual Drawing**: Click and drag to create short line
2. **Programmatic Creation**: Create via code with specific coordinates
3. **Snap-to-Price**: Automatically snap to nearest price levels
4. **Time-based**: Create lines spanning specific time periods

## Usage Examples

### Basic Short Line Creation
```java
// Create a new short line chart object
ShortLineChartObject shortLine = new ShortLineChartObject();

// Set the line coordinates
shortLine.setStartPoint(new Point(100, 1.2500)); // Start point (time, price)
shortLine.setEndPoint(new Point(150, 1.2520));   // End point (time, price)

// Set visual properties
shortLine.setColor(Color.RED);
shortLine.setWidth(2);
shortLine.setStyle(LineStyle.SOLID);

// Add to chart
chart.addChartObject(shortLine);
```

### Support Level Marking
```java
// Mark a short-term support level
ShortLineChartObject supportLine = new ShortLineChartObject();
supportLine.setStartPoint(new Point(currentTime - 10, supportPrice));
supportLine.setEndPoint(new Point(currentTime, supportPrice));
supportLine.setColor(Color.GREEN);
supportLine.setWidth(3);
supportLine.setStyle(LineStyle.SOLID);
supportLine.setLabel("Support");
```

### Trend Line Segment
```java
// Create a short trend line segment
ShortLineChartObject trendSegment = new ShortLineChartObject();
trendSegment.setStartPoint(new Point(time1, price1));
trendSegment.setEndPoint(new Point(time2, price2));
trendSegment.setColor(Color.BLUE);
trendSegment.setWidth(2);
trendSegment.setStyle(LineStyle.DASHED);
trendSegment.setLabel("Trend");
```

### Price Level Marker
```java
// Mark a specific price level for a short period
ShortLineChartObject priceLevel = new ShortLineChartObject();
priceLevel.setStartPoint(new Point(startTime, targetPrice));
priceLevel.setEndPoint(new Point(endTime, targetPrice));
priceLevel.setColor(Color.YELLOW);
priceLevel.setWidth(1);
priceLevel.setStyle(LineStyle.DOTTED);
priceLevel.setLabel("Target");
```

## Properties and Methods

### Core Properties
- `startPoint`: Starting point of the line (Point object)
- `endPoint`: Ending point of the line (Point object)
- `color`: Color of the line
- `width`: Thickness of the line in pixels
- `style`: Style of the line (SOLID, DASHED, DOTTED, DASH_DOT)

### Additional Properties
- `label`: Text label for the line
- `labelPosition`: Position of the label relative to line
- `visible`: Whether the line is visible
- `locked`: Whether the line can be moved/resized
- `extendLeft`: Whether to extend line to the left edge
- `extendRight`: Whether to extend line to the right edge
- `ray`: Whether the line extends infinitely in one direction

### Common Methods
```java
// Set line coordinates
void setStartPoint(Point point)
void setEndPoint(Point point)

// Set visual properties
void setColor(Color color)
void setWidth(int width)
void setStyle(LineStyle style)

// Set label properties
void setLabel(String label)
void setLabelPosition(LabelPosition position)

// Control visibility and interaction
void setVisible(boolean visible)
void setLocked(boolean locked)

// Set extension properties
void setExtendLeft(boolean extend)
void setExtendRight(boolean extend)
void setRay(boolean ray)

// Get properties
Point getStartPoint()
Point getEndPoint()
Color getColor()
int getWidth()
LineStyle getStyle()
```

## Use Cases

### 1. Short-term Support/Resistance
Mark brief support or resistance levels that may not persist long.

### 2. Entry/Exit Points
Highlight specific price levels for trade entries or exits.

### 3. Trend Line Segments
Create short segments of larger trend lines.

### 4. Price Targets
Mark short-term price targets or objectives.

### 5. Breakout Levels
Indicate levels where price might break out of a range.

### 6. Stop Loss Levels
Mark precise stop-loss levels for risk management.

## Best Practices

### Visual Design
- Use appropriate line thickness (1-3 pixels for most cases)
- Choose colors that contrast well with the chart background
- Use consistent line styles across related objects
- Consider using different colors for different types of lines

### Technical Analysis
- Keep lines short and precise for better accuracy
- Align with significant price levels or swing points
- Use multiple timeframes to confirm line validity
- Combine with other technical indicators

### Code Organization
```java
// Create a utility method for common short lines
public ShortLineChartObject createSupportLine(long startTime, long endTime, double price) {
    ShortLineChartObject line = new ShortLineChartObject();
    line.setStartPoint(new Point(startTime, price));
    line.setEndPoint(new Point(endTime, price));
    line.setColor(Color.GREEN);
    line.setWidth(2);
    line.setStyle(LineStyle.SOLID);
    line.setLabel("Support");
    return line;
}
```

## Integration with Other Objects

### Working with Rectangles
```java
// Create short line at rectangle boundary
RectangleChartObject rectangle = new RectangleChartObject();
ShortLineChartObject boundaryLine = new ShortLineChartObject();

// Align line with rectangle top
boundaryLine.setStartPoint(new Point(rectangle.getPoint1().getX(), rectangle.getPoint1().getY()));
boundaryLine.setEndPoint(new Point(rectangle.getPoint2().getX(), rectangle.getPoint1().getY()));
```

### Working with Text Objects
```java
// Add descriptive text to short line
TextChartObject description = new TextChartObject();
description.setText("Breakout Level");
description.setPosition(shortLine.getMidPoint());
```

### Multiple Short Lines
```java
// Create a series of short lines for a pattern
List<ShortLineChartObject> patternLines = new ArrayList<>();
for (int i = 0; i < points.size() - 1; i++) {
    ShortLineChartObject line = new ShortLineChartObject();
    line.setStartPoint(points.get(i));
    line.setEndPoint(points.get(i + 1));
    line.setColor(Color.BLUE);
    line.setWidth(1);
    patternLines.add(line);
}
```

## Error Handling

### Common Issues
- Invalid coordinates (negative values, out of chart bounds)
- Lines that are too short or too long
- Performance issues with many short lines
- Overlapping or conflicting lines

### Solutions
```java
// Validate line coordinates and length
public boolean isValidShortLine(Point start, Point end) {
    // Check coordinates are valid
    if (start.getX() < 0 || end.getX() < 0 || 
        start.getY() < 0 || end.getY() < 0) {
        return false;
    }
    
    // Check line is not too long (adjust threshold as needed)
    double length = Math.sqrt(Math.pow(end.getX() - start.getX(), 2) + 
                             Math.pow(end.getY() - start.getY(), 2));
    return length <= maxShortLineLength;
}
```

## Performance Considerations

### Optimization Tips
- Limit the number of short lines on a chart
- Use appropriate line styles (SOLID is fastest)
- Consider removing old lines when no longer needed
- Use efficient coordinate calculations

### Memory Management
```java
// Clean up old short lines
public void removeOldShortLines() {
    long currentTime = System.currentTimeMillis();
    for (ShortLineChartObject line : chart.getChartObjects()) {
        if (line.getEndPoint().getX() < currentTime - maxAge) {
            chart.removeChartObject(line);
        }
    }
}
```

### Batch Operations
```java
// Create multiple short lines efficiently
public void createMultipleShortLines(List<LineSegment> segments) {
    for (LineSegment segment : segments) {
        ShortLineChartObject line = new ShortLineChartObject();
        line.setStartPoint(segment.getStart());
        line.setEndPoint(segment.getEnd());
        line.setColor(segment.getColor());
        line.setWidth(segment.getWidth());
        chart.addChartObject(line);
    }
}
```

## Advanced Features

### Dynamic Lines
```java
// Create lines that update with price movement
public void createDynamicSupportLine(double basePrice) {
    ShortLineChartObject dynamicLine = new ShortLineChartObject();
    dynamicLine.setStartPoint(new Point(currentTime - 20, basePrice));
    dynamicLine.setEndPoint(new Point(currentTime, basePrice));
    dynamicLine.setColor(Color.GREEN);
    dynamicLine.setLabel("Dynamic Support");
    
    // Update line position periodically
    Timer timer = new Timer();
    timer.scheduleAtFixedRate(new TimerTask() {
        @Override
        public void run() {
            updateLinePosition(dynamicLine, basePrice);
        }
    }, 0, 1000); // Update every second
}
```

### Pattern Recognition
```java
// Create short lines for chart patterns
public void createPatternLines(ChartPattern pattern) {
    for (LineSegment segment : pattern.getSegments()) {
        ShortLineChartObject line = new ShortLineChartObject();
        line.setStartPoint(segment.getStart());
        line.setEndPoint(segment.getEnd());
        line.setColor(pattern.getColor());
        line.setWidth(pattern.getWidth());
        line.setLabel(pattern.getName());
        chart.addChartObject(line);
    }
}
```

## Related Classes
- `ChartObject`: Base class for all chart objects
- `LineChartObject`: For longer line segments
- `RectangleChartObject`: For rectangular shapes
- `TextChartObject`: For adding text annotations
- `TrendLineChartObject`: For trend line analysis

## Version Compatibility
This documentation is based on JForex Dukascopy platform. Specific method signatures and properties may vary between versions. Always refer to the latest API documentation for your specific version.

## Troubleshooting

### Common Problems
1. **Lines not appearing**: Check visibility and coordinate validity
2. **Wrong positioning**: Verify coordinate system and chart scaling
3. **Performance issues**: Reduce number of lines or optimize rendering
4. **Style not applied**: Ensure proper style enumeration values

### Debug Tips
```java
// Debug line creation
public void debugShortLine(ShortLineChartObject line) {
    System.out.println("Start Point: " + line.getStartPoint());
    System.out.println("End Point: " + line.getEndPoint());
    System.out.println("Color: " + line.getColor());
    System.out.println("Width: " + line.getWidth());
    System.out.println("Style: " + line.getStyle());
    System.out.println("Visible: " + line.isVisible());
}
``` 