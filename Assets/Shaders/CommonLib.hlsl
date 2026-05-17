#pragma once

#include "Defines.hlsl"

#define READ_CELL(pos) \
uint2 cellPos = pos; \
uint cellIdx = PosToIdx(cellPos); \
Cell cell = _CellsRO[cellIdx];

#define READ_SOIL(pos) \
uint2 cellPos = pos; \
float2 soil = _SoilTexRO[cellPos];

#define READ_CELL_SOIL(pos) \
uint2 cellPos = pos; \
uint cellIdx = PosToIdx(cellPos); \
Cell cell = _CellsRO[cellIdx]; \
float2 soil = _SoilTexRO[cellPos];

#define READ_NEIGHBOR_CELL(pos, dir) \
uint2 neighborPos = ShiftCoord(pos, dir); \
uint neighborIdx = PosToIdx(neighborPos); \
Cell neighborCell = _CellsRO[neighborIdx];

#define GUARD_CELL_COORD(pos) \
if (pos.x >= _Width || pos.y >= _Height) return;

uint PosToIdx(uint2 pos) {
    return pos.y * _Width + pos.x;
}

uint2 IdxToPos(uint idx) {
    return uint2(idx % _Width, idx / _Width);
}

// #region Packing
// uint UnpackFloat1(float data) {
//     return (asuint(data) >> 0) & 0xFF;
// }
// uint UnpackFloat2(float data) {
//     return (asuint(data) >> 8) & 0xFF;
// }
// uint UnpackFloat3(float data) {
//     return (asuint(data) >> 16) & 0xFF;
// }
// uint UnpackFloat4(float data) {
//     return (asuint(data) >> 24) & 0xFF;
// }

uint MergeByMask(uint a, uint b, uint mask) {
    return (a & ~mask) | (b & mask);
}

uint GetIntBit(uint data, uint bitIdx) {
    return (data >> bitIdx) & 1u;
}

uint GetIntByte(uint data, uint byteIdx) {
    return (data >> (byteIdx * 8u)) & 0xffu;
}

uint UnpackInt1(uint data) {
    return GetIntByte(data, 0u);
}
uint UnpackInt2(uint data) {
    return GetIntByte(data, 1u);
}
uint UnpackInt3(uint data) {
    return GetIntByte(data, 2u);
}
uint UnpackInt4(uint data) {
    return GetIntByte(data, 3u);
}

// void PackToFloat1(inout float container, uint data) {
//     uint bits = asuint(container);
//     bits |= (data << 0);
//     container = asfloat(bits);
// }
// void PackToFloat2(inout float container, uint data) {
//     uint bits = asuint(container);
//     bits |= (data << 8);
//     container = asfloat(bits);
// }
// void PackToFloat3(inout float container, uint data) {
//     uint bits = asuint(container);
//     bits |= (data << 16);
//     container = asfloat(bits);
// }
// void PackToFloat4(inout float container, uint data) {
//     uint bits = asuint(container);
//     bits |= (data << 24);
//     container = asfloat(bits);
// }

void SetIntBit(inout uint container, uint data, uint bitIdx) {
    container = MergeByMask(container, data << bitIdx, 1u << bitIdx);
}

void SetIntByte(inout uint container, uint data, uint byteIdx) {
    container = MergeByMask(container, data << (byteIdx * 8u), 0xffu << (byteIdx * 8u));
}

void PackToInt1(inout uint container, uint data) {
    SetIntByte(container, data, 0u);
}
void PackToInt2(inout uint container, uint data) {
    SetIntByte(container, data, 1u);
}
void PackToInt3(inout uint container, uint data) {
    SetIntByte(container, data, 2u);
}
void PackToInt4(inout uint container, uint data) {
    SetIntByte(container, data, 3u);
}
// #endregion // Packing

// #region Direction
// 0 10 ( 0, y) forward up
// 1 11 ( x, 0) right   right
// 2 00 ( 0,-y) back    down
// 3 01 (-x, 0) left    left
uint RotateDir(uint baseDir, int relDir) {
    return (baseDir + relDir + 4) % 4;
}

uint2 ShiftCoord(int2 inPos, uint dir) {
    static const int2 offsets[4] = {
        int2( 0,  1), // 0
        int2( 1,  0), // 1
        int2( 0, -1), // 2
        int2(-1,  0), // 3
    };

    int2 mapSize = int2(_Width,_Height);
    return (inPos + offsets[dir] + mapSize) % mapSize;
}

uint2 MoveCoord(uint2 inPos, int2 offset) {
    int2 mapSize = int2(_Width,_Height);
    return (inPos + offset + mapSize) % mapSize;
}
// #endregion // Direction

// #region Cell analysis
uint GetOutFlow(in Cell cell) {
    return cell.energyFlow & 0xfu;
}
uint GetInFlow(in Cell cell) {
    return (cell.energyFlow >> 4) & 0xfu;
}
uint HasOutFlow(in Cell cell, uint dir) {
    return GetIntBit(GetOutFlow(cell), dir);
}
uint HasInFlow(in Cell cell, uint dir) {
    return GetIntBit(GetInFlow(cell), dir);
}
bool IsCellSendEnergy(in Cell cell) {
    return GetOutFlow(cell) > 0;
}
bool IsCellReceiveEnergy(in Cell cell) {
    return GetInFlow(cell) > 0;
}
bool IsCellHasParent(in Cell cell) {
    return cell.parentDir < 4; // cell.parentDir == 0xFFFFFFFFu
}
bool IsCellSingle(uint2 cellPos, in Cell cell) {
    return !IsCellHasParent(cell) && !IsCellSendEnergy(cell) && !IsCellReceiveEnergy(cell);
}
uint DirectionsToSendEnergy(Cell cell) {
    uint neighborsToSend = 0;
    uint outFlow = GetOutFlow(cell);
    for (uint i = 0; i < 4; ++i) {
        neighborsToSend += GetIntBit(outFlow, i);
    }
    return neighborsToSend;
}
// #endregion // Cell analysis

void SetOutFlow(inout Cell cell, uint dir, bool value) {
    SetIntBit(cell.energyFlow, value, dir);
    uint dirIn = GetIntBit(cell.energyFlow, dir+4);
    SetIntBit(cell.energyFlow, !value && dirIn, dir+4);
}
void SetInFlow(inout Cell cell, uint dir, bool value) {
    SetIntBit(cell.energyFlow, value, dir+4);
    uint dirOut = GetIntBit(cell.energyFlow, dir);
    SetIntBit(cell.energyFlow, !value && dirOut, dir);
}
