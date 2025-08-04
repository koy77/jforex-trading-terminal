# RectangleChartObject Documentation

## Overview
`RectangleChartObject` is a chart drawing object in the JForex Dukascopy trading platform that allows traders to draw rectangular shapes on price charts for technical analysis and visual marking of important areas.

## Class Information
- **Package**: `com.dukascopy.charts.drawings`
- **Class**: `RectangleChartObject`
- **Inheritance**: Extends `ChartObject` or similar base class
- **Purpose**: Creates rectangular shapes on trading charts

## Features

### Basic Properties
- **Shape**: Rectangular with four sides
- **Fill**: Can be filled with color or left transparent
- **Border**: Configurable border style, color, and thickness
- **Position**: Defined by two corner points (top-left and bottom-right)

### Drawing Modes
1. **Manual Drawing**: Click and drag to create rectangle
2. **Programmatic Creation**: Create via code with specific coordinates
3. **Template-based**: Use predefined rectangle templates

## Usage Examples

### Basic Rectangle Creation
```java
// Create a new rectangle chart object
RectangleChartObject rectangle = new RectangleChartObject();

// Set the rectangle coordinates
rectangle.setPoint1(new Point(100, 200)); // Top-left corner
rectangle.setPoint2(new Point(300, 400)); // Bottom-right corner

// Set visual properties
rectangle.setFillColor(Color.YELLOW);
rectangle.setFillOpacity(0.3);
rectangle.setBorderColor(Color.RED);
rectangle.setBorderWidth(2);

// Add to chart
chart.addChartObject(rectangle);
```

### Support and Resistance Levels
```java
// Mark support level
RectangleChartObject supportZone = new RectangleChartObject();
supportZone.setPoint1(new Point(time1, price1));
supportZone.setPoint2(new Point(time2, price2));
supportZone.setFillColor(Color.GREEN);
supportZone.setFillOpacity(0.2);
supportZone.setBorderColor(Color.DARK_GREEN);
supportZone.setLabel("Support Zone");
```

### Price Range Marking
```java
// Mark a specific price range
RectangleChartObject priceRange = new RectangleChartObject();
priceRange.setPoint1(new Point(startTime, highPrice));
priceRange.setPoint2(new Point(endTime, lowPrice));
priceRange.setFillColor(Color.BLUE);
priceRange.setFillOpacity(0.1);
priceRange.setBorderColor(Color.BLUE);
priceRange.setBorderStyle(BorderStyle.DASHED);
```

## Properties and Methods

### Core Properties
- `point1`: First corner point (Point object)
- `point2`: Second corner point (Point object)
- `fillColor`: Background color of the rectangle
- `fillOpacity`: Transparency level (0.0 to 1.0)
- `borderColor`: Color of the rectangle border
- `borderWidth`: Thickness of the border
- `borderStyle`: Style of the border (SOLID, DASHED, DOTTED)

### Additional Properties
- `label`: Text label for the rectangle
- `labelPosition`: Position of the label relative to rectangle
- `visible`: Whether the rectangle is visible
- `locked`: Whether the rectangle can be moved/resized
- `timeVisible`: Whether to show time information
- `priceVisible`: Whether to show price information

### Common Methods
```java
// Set rectangle dimensions
void setPoint1(Point point)
void setPoint2(Point point)

// Set visual properties
void setFillColor(Color color)
void setFillOpacity(double opacity)
void setBorderColor(Color color)
void setBorderWidth(int width)
void setBorderStyle(BorderStyle style)

// Set label properties
void setLabel(String label)
void setLabelPosition(LabelPosition position)

// Control visibility and interaction
void setVisible(boolean visible)
void setLocked(boolean locked)

// Get properties
Point getPoint1()
Point getPoint2()
Color getFillColor()
double getFillOpacity()
```

## Use Cases

### 1. Support and Resistance Zones
Mark areas where price has historically found support or resistance.

### 2. Consolidation Periods
Highlight periods when price moves sideways in a range.

### 3. Breakout Areas
Mark areas where price breaks above or below a significant level.

### 4. Risk Management
Define stop-loss and take-profit zones visually.

### 5. Pattern Recognition
Mark chart patterns like rectangles, flags, or pennants.

## Best Practices

### Visual Design
- Use semi-transparent fills (opacity 0.1-0.3) to avoid obscuring price data
- Choose colors that contrast well with the chart background
- Use consistent color schemes across related objects

### Technical Analysis
- Align rectangle edges with significant price levels
- Use multiple timeframes to confirm rectangle validity
- Combine with other technical indicators for confirmation

### Code Organization
- Create reusable methods for common rectangle patterns
- Use meaningful variable names and comments
- Implement error handling for invalid coordinates

## Integration with Other Objects

### Working with Lines
```java
// Create rectangle and trend line together
RectangleChartObject range = new RectangleChartObject();
LineChartObject trendLine = new LineChartObject();

// Align rectangle with trend line
range.setPoint1(trendLine.getStartPoint());
range.setPoint2(trendLine.getEndPoint());
```

### Working with Text Objects
```java
// Add descriptive text to rectangle
TextChartObject description = new TextChartObject();
description.setText("Breakout Zone");
description.setPosition(rectangle.getCenterPoint());
```

## Error Handling

### Common Issues
- Invalid coordinates (negative values, out of chart bounds)
- Missing required properties
- Performance issues with many rectangles

### Solutions
```java
// Validate coordinates before creating rectangle
if (point1.getX() >= 0 && point2.getX() >= 0 && 
    point1.getY() >= 0 && point2.getY() >= 0) {
    RectangleChartObject rectangle = new RectangleChartObject();
    rectangle.setPoint1(point1);
    rectangle.setPoint2(point2);
    // ... set other properties
}
```

## Performance Considerations

### Optimization Tips
- Limit the number of rectangles on a chart
- Use appropriate opacity levels
- Consider removing old rectangles when no longer needed
- Use efficient coordinate calculations

### Memory Management
```java
// Clean up rectangles when no longer needed
public void removeOldRectangles() {
    for (RectangleChartObject rect : chart.getChartObjects()) {
        if (rect.getPoint2().getX() < currentTime - maxAge) {
            chart.removeChartObject(rect);
        }
    }
}
```

## Related Classes
- `ChartObject`: Base class for all chart objects
- `LineChartObject`: For drawing lines on charts
- `TextChartObject`: For adding text annotations
- `EllipseChartObject`: For drawing ellipses/circles
- `TriangleChartObject`: For drawing triangles

## Version Compatibility
This documentation is based on JForex Dukascopy platform. Specific method signatures and properties may vary between versions. Always refer to the latest API documentation for your specific version. 