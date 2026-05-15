#pragma once

#include "Defines.hlsl"
#include "CommonLib.hlsl"

#define EDGE_WIDTH 1.0

// #region Simple shapes
fixed4 RenderCircle(float2 cellUV, fixed4 baseColor, fixed4 shapeColor, float size) { // size [0,1]
    return lerp(baseColor, shapeColor, length(cellUV - 0.5) < (size/2.0));
}

fixed4 RenderSquare(float2 cellUV, fixed4 baseColor, fixed4 shapeColor, float size) { // size [0,1]
    float2 centeredUV = (cellUV - 0.5) * 2; // [0;1] -> [-1;1]
    return lerp(baseColor, shapeColor, abs(centeredUV.x) < size && abs(centeredUV.y) < size);
}

fixed4 RenderStar(float2 cellUV, fixed4 baseColor, fixed4 shapeColor, float size, float depth) { // depth [0,1]
    float2 centeredUV = (cellUV - 0.5) * 2; // [0;1] -> [-1;1]
    float minCoord = min(abs(centeredUV.x), abs(centeredUV.y));
    float maxCoord = max(abs(centeredUV.x), abs(centeredUV.y));
    float threshold = 1 - depth * (1 - minCoord);
    return lerp(baseColor, shapeColor, maxCoord  / size < threshold);
}

fixed4 RenderEllipse(float2 cellUV, fixed4 baseColor, fixed4 shapeColor, float xSize, float ySize) {
    float2 centeredUV = (cellUV - 0.5) * 2; // [0;1] -> [-1;1]
    float ellipseValue = (centeredUV.x * centeredUV.x) / (xSize * xSize) + (centeredUV.y * centeredUV.y) / (ySize * ySize);
    return lerp(baseColor, shapeColor, ellipseValue <= 1.0);
}

fixed4 RenderLine(float2 cellUV, fixed4 baseColor, fixed4 centerColor, fixed4 edgeColor, float width, uint side) { // from center to side
    float2 centeredUV = (cellUV - 0.5) * 2; // [0;1] -> [-1;1]
    float2 directions[4] = {
        float2(clamp(1 - abs(round(centeredUV.x/width/2.0)), 0, 1) * ceil(centeredUV.y), centeredUV.y), // 0
        float2(ceil(centeredUV.x) * clamp(1 - abs(round(centeredUV.y/width/2.0)), 0, 1), centeredUV.x), // 1
        float2(clamp(1 - abs(round(centeredUV.x/width/2.0)), 0, 1) * ceil(-centeredUV.y), -centeredUV.y), // 2
        float2(ceil(-centeredUV.x) * clamp(1 - abs(round(centeredUV.y/width/2.0)), 0, 1), -centeredUV.x), // 3
    };
    float direction = directions[side].x;
    fixed4 pixelCol = lerp(centerColor, edgeColor, directions[side].y);
    return lerp(baseColor, pixelCol, direction);
}
// #endregion // Simple shapes

// #region Cell types
fixed4 RenderCell(float2 cellUV, fixed4 baseColor, fixed4 cellColor, uint type, uint dir) {
    fixed4 outColor = baseColor;
    switch (type) {
        case CELLTYPE_LEAF:
            outColor = RenderEllipse(cellUV, baseColor, cellColor, (dir%2) > 0 ? 0.8 : 0.5, (dir%2) > 0 ? 0.5 : 0.8);
            break;
        case CELLTYPE_ROOT:
            outColor = RenderStar(cellUV, baseColor, cellColor, 0.9, 0.3);
            break;
        case CELLTYPE_ANTENNA:
            outColor = RenderStar(cellUV, baseColor, cellColor, 0.9, 0.3);
            break;
        case CELLTYPE_WOOD:
            outColor = RenderSquare(cellUV, baseColor, cellColor, 0.5);
            break;
        case CELLTYPE_SPROUT:
            outColor = RenderCircle(cellUV, baseColor, cellColor, 0.8);
            break;
        case CELLTYPE_SEED:
            outColor = RenderCircle(cellUV, baseColor, cellColor, 0.8);
            break;

    }
    return outColor;

    fixed4 circle = RenderCircle(cellUV, baseColor, cellColor, 0.8);
    fixed4 square = RenderSquare(cellUV, baseColor, cellColor, 0.8);
    fixed4 star = RenderStar(cellUV, baseColor, cellColor, 0.9, 0.3);
    fixed4 ellipse = RenderEllipse(cellUV, baseColor, cellColor, 0.8, 0.5);
    fixed4 right = RenderLine(cellUV, baseColor, cellColor, cellColor, 0.5, 3);
    return star;
}

fixed4 RenderConnections(float2 cellUV, fixed4 baseColor) {
    return baseColor; // TODO: implement
}
// #endregion // Cell types
