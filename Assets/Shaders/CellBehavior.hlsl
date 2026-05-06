#pragma once

#include "Defines.hlsl"
#include "Common.hlsl"

// Globals
// _Cells;
// _SoilTexRead;
// _SoilTexWrite;
// _Width;
// _Height;

static const uint COND_NUM = 13u;
static const uint COMM_NUM = 5u;
static const uint SINGLE_COMM_NUM = 5u;

void SetActiveGene(in uint2 cellPos, uint geneIdx) {
    uint cellIdx = PosToIdx(cellPos);
    // Cell cell = _Cells[cellIdx];
    _Cells[cellIdx].activeGene = geneIdx  % GENES_NUM;
}

// #region Gene conditions
bool CondOrgNrg(in uint2 cellPos) { // soil has more organics than energy
    int2 p = int2(cellPos.x, cellPos.y);
    float2 soil = _SoilTexRead[p];
    return soil.x > soil.y;
}

bool CondNrgOrg(in uint2 cellPos) { // soil has more energy than organics
    int2 p = int2(cellPos.x, cellPos.y);
    float2 soil = _SoilTexRead[p];
    return soil.y > soil.x;
}

bool CondObstacle1(in uint2 cellPos) { // has obstacle in forward direction
    uint cellIdx = PosToIdx(cellPos);
    uint cellDir = _Cells[cellIdx].direction;
    uint2 targetPos = ShiftCoord(cellPos, cellDir);
    uint tidx = PosToIdx(targetPos);
    return _Cells[tidx].cellType != 0;
}
bool CondObstacle2(in uint2 cellPos) { // has obstacle in left direction
    uint cellIdx = PosToIdx(cellPos);
    uint cellDir = _Cells[cellIdx].direction;
    uint left = RotateDir(cellDir, 3);
    uint2 targetPos = ShiftCoord(cellPos, left);
    uint tidx = PosToIdx(targetPos);
    return _Cells[tidx].cellType != 0;
}
bool CondObstacle4(in uint2 cellPos) { // has obstacle in right direction
    uint cellIdx = PosToIdx(cellPos);
    uint cellDir = _Cells[cellIdx].direction;
    uint right = RotateDir(cellDir, 1);
    uint2 targetPos = ShiftCoord(cellPos, right);
    uint tidx = PosToIdx(targetPos);
    return _Cells[tidx].cellType != 0;
}
bool CondObstacleFree(in uint2 cellPos) { // no obstacle in any direction
    for (uint rel = 0; rel < 4; ++rel) {
        uint2 targetPos = ShiftCoord(cellPos, rel);
        uint tidx = PosToIdx(targetPos);
        if (_Cells[tidx].cellType != 0) return false;
    }
    return true;
}
bool CondOrgCompare(in uint2 cellPos, in float condParam) { // compare organics amount in directions based on param
    uint cellIdx = PosToIdx(cellPos);
    uint cellDir = _Cells[cellIdx].direction;
    // condParam used as selector: <0.5 => compare forward vs left, else forward vs right
    uint bRel = condParam < 0.5 ? 3 : 1;
    uint2 aCoord = ShiftCoord(cellPos, cellDir);
    uint2 bCoord = ShiftCoord(cellPos, RotateDir(cellDir, bRel));
    float2 aSoil = _SoilTexRead[int2(aCoord.x, aCoord.y)];
    float2 bSoil = _SoilTexRead[int2(bCoord.x, bCoord.y)];
    return aSoil.x > bSoil.x;
}
bool CondNrgCompare(in uint2 cellPos, in float condParam) { // compare energy amount in directions based on param
    uint cellIdx = PosToIdx(cellPos);
    uint cellDir = _Cells[cellIdx].direction;
    uint bRel = condParam < 0.5 ? 3 : 1;
    uint2 aCoord = ShiftCoord(cellPos, cellDir);
    uint2 bCoord = ShiftCoord(cellPos, RotateDir(cellDir, bRel));
    float2 aSoil = _SoilTexRead[int2(aCoord.x, aCoord.y)];
    float2 bSoil = _SoilTexRead[int2(bCoord.x, bCoord.y)];
    return aSoil.y > bSoil.y;
}
bool CondOrgAmount(in uint2 cellPos, in float condParam) { // organics amount in cell is more then param*2
    float2 soil = _SoilTexRead[int2(cellPos.x, cellPos.y)];
    return soil.x > condParam * 2.0;
}
bool CondNrgAmount(in uint2 cellPos, in float condParam) { // energy amount in cell is more then param*2
    float2 soil = _SoilTexRead[int2(cellPos.x, cellPos.y)];
    return soil.y > condParam * 2.0;
}
bool CondOrgAmountAround(in uint2 cellPos, in float condParam) { // organics amount in 3*3 is more then param*2
    float sum = 0.0;
    for (int oy = -1; oy <= 1; ++oy) {
        for (int ox = -1; ox <= 1; ++ox) {
            uint nx = (cellPos.x + ox + _Width) % _Width;
            uint ny = (cellPos.y + oy + _Height) % _Height;
            float2 s = _SoilTexRead[int2(nx, ny)];
            sum += s.x;
        }
    }
    return sum > condParam * 2.0;
}
bool CondNrgAmountAround(in uint2 cellPos, in float condParam) { // energy amount in 3*3 is more then param*2
    float sum = 0.0;
    for (int oy = -1; oy <= 1; ++oy) {
        for (int ox = -1; ox <= 1; ++ox) {
            uint nx = (cellPos.x + ox + _Width) % _Width;
            uint ny = (cellPos.y + oy + _Height) % _Height;
            float2 s = _SoilTexRead[int2(nx, ny)];
            sum += s.y;
        }
    }
    return sum > condParam * 2.0;
}
bool CondRand(in uint2 cellPos, in float condParam) { // random number is more then param
    float r = randFloat(cellPos, condParam);
    return r > condParam;
}
// #endregion // Gene conditions

bool CheckGeneCondition(in uint2 cellPos, in uint condId, in float condParam) {
    // uint cellIdx = PosToIdx(cellPos);
    // Cell cell = _Cells[cellIdx];

    [branch]
    switch (condId) {
        case 0: return CondOrgNrg(cellPos);
        case 1: return CondNrgOrg(cellPos);
        case 2: return CondObstacle1(cellPos);
        case 3: return CondObstacle2(cellPos);
        case 4: return CondObstacle4(cellPos);
        case 5: return CondObstacleFree(cellPos);
        case 6: return CondOrgCompare(cellPos, condParam);
        case 7: return CondNrgCompare(cellPos, condParam);
        case 8: return CondOrgAmount(cellPos, condParam);
        case 9: return CondNrgAmount(cellPos, condParam);
        case 10: return CondOrgAmountAround(cellPos, condParam);
        case 11: return CondNrgAmountAround(cellPos, condParam);
        case 12: return CondRand(cellPos, condParam);
        default: return false;
    }
}

// #region Gene commands
bool CommandMove(in uint2 cellPos/* , uint moveDir */) {
    uint cellIdx = PosToIdx(cellPos);
    // Cell cell = _Cells[cellIdx];

    // copy self in movement direction
    uint moveDir = _Cells[cellIdx].direction;
    uint2 targetCoord = ShiftCoord(cellPos, moveDir);
    uint targetIdx = targetCoord.y * _Width + targetCoord.x;

    if (_Cells[cellIdx].cellType < CELLTYPE_SPROUT || _Cells[targetIdx].cellType > 0) return false;

    _Cells[targetIdx] = _Cells[cellIdx];

    // erase self in current position
    _Cells[cellIdx] = (Cell)0;

    return true;
}
bool CommandRotate(in uint2 cellPos, uint relDir) {
    uint cellIdx = PosToIdx(cellPos);
    // Cell cell = _Cells[cellIdx];

    _Cells[cellIdx].direction = RotateDir(_Cells[cellIdx].direction, _Cells[cellIdx].cellType > 4 ? relDir : 0);
    return _Cells[cellIdx].parentDir < 4 && _Cells[cellIdx].cellType > 4;
}
bool CommandGrow(in uint2 cellPos) {
    uint cellIdx = PosToIdx(cellPos);
    // Cell cell = _Cells[cellIdx];

    uint growDirections = _Genomes[_Cells[cellIdx].genomeId].genes[_Cells[cellIdx].activeGene].growDirections;

    // unpack directions
    uint directionTypes[4];
    directionTypes[0] = UnpackInt1(growDirections) % 32u;
    directionTypes[1] = UnpackInt2(growDirections) % 32u;
    directionTypes[2] = UnpackInt3(growDirections) % 32u;
    directionTypes[3] = UnpackInt4(growDirections) % 32u;

    uint cellDir = _Cells[cellIdx].direction;
    bool growed = false;

    for (int i = 0; i < 4; i++) {
        uint typeToCreate = directionTypes[i];
        uint createDir = RotateDir(i, cellDir);
        uint2 targetCoord = i == 2 ? cellPos : ShiftCoord(cellPos, createDir);
        uint targetIdx = PosToIdx(targetCoord);
        if (typeToCreate == 0 || typeToCreate == CELLTYPE_WOOD || typeToCreate > CELLTYPE_SEED
            || _Cells[cellIdx].energy < ENERGY_GROW
            || _Cells[targetIdx].cellType != 0
        ) continue;

        // has enough energy?
        CreateCell(targetCoord, typeToCreate, createDir, RotateDir(createDir, 2));
        SetIntBit(_Cells[cellIdx].energyFlow, typeToCreate == CELLTYPE_SPROUT || typeToCreate == CELLTYPE_SEED, createDir);

        growed = true;
    }

    if (!growed) return false;

    // make a wood from itself
    ConvertToWood(cellPos);

    // define transport direction!

    return true;
}
bool CommandSkip(in uint2 cellPos) {
    return true;
}
bool CommandDie(in uint2 cellPos) {
    KillCell(cellPos);
    return true;
}
bool CommandBecomeSeed(in uint2 cellPos) {
    ConvertToSeed(cellPos);
    return true;
}
bool CommandSendSeed(in uint2 cellPos) {
    return true; // TODO: implement
}
// #endregion // Gene commands

bool ExecuteCommand(in uint2 cellPos, in uint commandId, bool single) {
    uint cellIdx = PosToIdx(cellPos);
    // Cell cell = _Cells[cellIdx];

    [branch]
    if (single) {
        [branch] // Принуждает GPU использовать реальное ветвление
        switch(commandId) {
            case 0: return CommandSkip(cellPos);
            case 1: return CommandGrow(cellPos);
            case 2: return CommandMove(cellPos);
            case 3: return CommandRotate(cellPos, 1);
            case 4: return CommandBecomeSeed(cellPos);
        }
    } else {
        [branch] // Принуждает GPU использовать реальное ветвление
        switch(commandId) {
            case 0: return CommandSkip(cellPos);
            case 1: return CommandGrow(cellPos);
            case 2: return CommandDie(cellPos);
            case 3: return CommandBecomeSeed(cellPos);
            case 4: return CommandSendSeed(cellPos);
        }
    }

    return false;
}

void ExecuteGene(in uint2 cellPos, in Gene gene)
{
    // CommandMove(cellPos);
    // CommandGrow(cellPos);
    // return;

    uint cellIdx = PosToIdx(cellPos);
    // Cell cell = _Cells[cellIdx];

    // determine if cell is single or not
    bool isSingle = _Cells[cellIdx].parentDir == 0xFFFFFFFF;
    // check canditions are valid
    uint cond1 = UnpackInt1(gene.conditions) % (COND_NUM * 2u);
    uint cond2 = UnpackInt2(gene.conditions) % (COND_NUM * 2u);
    bool cond1Valid = cond1 < COND_NUM;
    bool cond2Valid = cond2 < COND_NUM;

    // try to grow
    if (!cond1Valid && !cond2Valid) {
        CommandGrow(cellPos);
    } else {
        // read conditions
        bool check1 = CheckGeneCondition(cellPos, cond1, gene.condParam1);
        bool check2 = CheckGeneCondition(cellPos, cond2, gene.condParam2);
        bool check = check1 && check2;

        uint commNum = isSingle ? SINGLE_COMM_NUM : COMM_NUM;
        if (check) {
            uint commandId = UnpackInt1(isSingle ? gene.aloneCommands : gene.condResult) % (commNum * 4u);
            if (commandId > commNum) {
                SetActiveGene(cellPos, UnpackInt3(gene.condResult));
            } else {
                bool commandResult = ExecuteCommand(cellPos, commandId, isSingle);
                if (commandResult) {
                    SetActiveGene(cellPos, UnpackInt1(isSingle ? gene.aloneComGenes : gene.comGenes));
                } else {
                    SetActiveGene(cellPos, UnpackInt2(isSingle ? gene.aloneComGenes : gene.comGenes));
                }
            }
        } else {
            uint commandId = UnpackInt2(isSingle ? gene.aloneCommands : gene.condResult) % (commNum * 4u);
            if (commandId > commNum) {
                SetActiveGene(cellPos, UnpackInt4(gene.condResult));
            } else {
                bool commandResult = ExecuteCommand(cellPos, commandId, isSingle);
                if (commandResult) {
                    SetActiveGene(cellPos, UnpackInt3(isSingle ? gene.aloneComGenes : gene.comGenes));
                } else {
                    SetActiveGene(cellPos, UnpackInt4(isSingle ? gene.aloneComGenes : gene.comGenes));
                }
            }
        }
    }
}
